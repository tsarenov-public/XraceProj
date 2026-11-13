using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Exercises the reported finish -> garage -> race path with both actual prefabs.
// Run without -nographics as well, so URP renders the resources before scene unload.
[InitializeOnLoad]
public static class ReentrySmokeCheck
{
    private const string Key = "Xrace.ReentrySmoke";
    private const int RaceCount = 4;
    private static int frames;
    private static RaceSessionManager session;
    private static FinishPanelController finish;
    private static readonly List<UnityEngine.Object> previousResources = new List<UnityEngine.Object>();
    private static readonly List<UnityEngine.Object> temporaryObjects = new List<UnityEngine.Object>();

    static ReentrySmokeCheck()
    {
        EditorApplication.update += Tick;
        Application.logMessageReceived += CaptureError;
    }

    public static void Run()
    {
        if (!Application.isBatchMode) throw new InvalidOperationException("Run this check in a separate batch editor.");
        string path = ResultsRepository.FilePath;
        if (File.Exists(path + ".tmp")) throw new IOException("Pending result temporary file exists; leave it intact.");
        string backup = path + ".reentry-test-backup-" + Guid.NewGuid().ToString("N");
        SessionState.SetString(Key + "Path", path);
        SessionState.SetString(Key + "Backup", backup);
        SessionState.SetBool(Key + "HadFile", File.Exists(path));
        if (File.Exists(path)) File.Move(path, backup);
        SessionState.SetString(Key + "Error", string.Empty);
        SessionState.SetInt(Key + "Completed", 0);
        SessionState.SetString(Key + "Deadline", DateTime.UtcNow.AddMinutes(7).ToString("O"));
        SessionState.SetInt(Key, 1);
        EditorSceneManager.OpenScene("Assets/Scenes/Garage.unity");
        EditorApplication.EnterPlaymode();
    }

    private static void CaptureError(string condition, string stackTrace, LogType type)
    {
        if (SessionState.GetInt(Key, 0) == 0 || type != LogType.Error && type != LogType.Exception && type != LogType.Assert) return;
        if (string.IsNullOrEmpty(SessionState.GetString(Key + "Error", string.Empty)))
            SessionState.SetString(Key + "Error", condition + "\n" + stackTrace);
    }

    private static void Tick()
    {
        int step = SessionState.GetInt(Key, 0);
        if (step == 0) return;
        try
        {
            Require(DateTime.UtcNow < DateTime.Parse(SessionState.GetString(Key + "Deadline", string.Empty)), "Repeated race check timed out.");
            string loggedError = SessionState.GetString(Key + "Error", string.Empty);
            Require(string.IsNullOrEmpty(loggedError), "A runtime error occurred: " + loggedError);
            if (!EditorApplication.isPlaying || EditorApplication.isCompiling || ++frames < 15) return;
            frames = 0;
            Scene scene = SceneManager.GetActiveScene();
            int completed = SessionState.GetInt(Key + "Completed", 0);

            if (step == 1)
            {
                if (scene.name != "Garage") return;
                Require(previousResources.All(value => value == null), "The previous race retained native mesh/material objects after scene unload.");
                previousResources.Clear();
                GameObject[] roots = scene.GetRootGameObjects();
                GarageManager garage = Components<GarageManager>(roots).Single();
                Button race = Components<Button>(roots).Single(value => value.name == "Btn_Race");
                if (!race.interactable) return;
                Require(!Components<ParticleSystem>(roots).Any(value => value.IsAlive(true)), "Garage contains live smoke particles.");
                Require(MusicManager.Instance != null && !MusicManager.Instance.GetComponent<AudioSource>().isPlaying, "Music is audible in Garage.");
                if (completed == RaceCount)
                {
                    Require(ResultsRepository.Load().results.Count == RaceCount, "Finish saved a duplicate or lost a result.");
                    Debug.Log("XRACE_REENTRY_SMOKE_OK: four real Garage -> race -> finish -> Garage cycles, both cars, native resources freed, no runtime errors.");
                    End(0);
                    return;
                }
                string desiredCar = completed % 2 == 0 ? "Toyota AE86" : "RMCar26";
                TMPro.TMP_Text name = Components<TMPro.TMP_Text>(roots).Single(value => value.name == "CarName_Text");
                if (name.text != desiredCar) { garage.NextCar(); return; }
                race.onClick.Invoke();
                SessionState.SetInt(Key, 2);
            }
            else if (step == 2)
            {
                if (scene.name != "Touge01") return;
                GameObject[] roots = scene.GetRootGameObjects();
                session = Components<RaceSessionManager>(roots).Single();
                finish = Components<FinishPanelController>(roots).Single();
                if (session.CarBody == null) return;
                Require(session.State == RaceSessionManager.RaceState.Ready, "New race did not start in Ready.");
                Require(Components<SpawnedRaceCar>(roots).Count(value => value.gameObject.activeInHierarchy) == 1, "Duplicate active race cars.");
                Require(Components<AudioListener>(roots).Count(value => value.isActiveAndEnabled) == 1, "Race has missing or duplicate AudioListeners.");
                Require(Time.timeScale == 1f && !AudioListener.pause, "Previous race left time/audio paused.");
                CaptureSkidResources(roots, previousResources);
                foreach (Skidmarks marks in Components<Skidmarks>(roots).Where(value => value.isActiveAndEnabled))
                {
                    Collider road = new SerializedObject(session.GetComponent<CarSpawner>()).FindProperty("roadSurface").objectReferenceValue as Collider;
                    Require(road != null && new SerializedObject(marks).FindProperty("roadSurface").objectReferenceValue == road,
                        "Spawned skidmarks were not bound to the serialized scene road collider.");
                    // Exercise a rendered mesh and reject an invalid caller index safely.
                    int index = marks.AddSkidMark(session.CarBody.position, Vector3.up, 1f, 9000);
                    marks.AddSkidMark(session.CarBody.position + Vector3.right, Vector3.up, 1f, index);
                }

                // A detached camera/marks object must also die if only its car is removed.
                HashSet<GameObject> existing = new HashSet<GameObject>(scene.GetRootGameObjects());
                GameObject temporaryCar = UnityEngine.Object.Instantiate(SelectedCarService.Get().carPrefab, session.CarBody.position + Vector3.up * 30f, Quaternion.identity);
                GameObject[] added = scene.GetRootGameObjects().Where(root => !existing.Contains(root)).ToArray();
                temporaryObjects.Clear();
                temporaryObjects.AddRange(added);
                CaptureSkidResources(added, temporaryObjects);
                UnityEngine.Object.Destroy(temporaryCar);

                Rigidbody body = session.CarBody;
                WheelController wheels = body.GetComponent<WheelController>();
                if (wheels != null) wheels.enabled = false;
                body.useGravity = false;
                body.position += Vector3.up * 20f;
                body.linearVelocity = body.transform.forward * 10f;
                SessionState.SetInt(Key, 3);
            }
            else if (step == 3)
            {
                Require(temporaryObjects.All(value => value == null), "Removing a car retained its detached camera, skidmark object or native resources.");
                if (session.State != RaceSessionManager.RaceState.Racing) return;
                RaceFinishTrigger trigger = Components<RaceFinishTrigger>(scene.GetRootGameObjects()).Single();
                session.CarBody.position = trigger.GetComponent<BoxCollider>().bounds.center;
                Physics.SyncTransforms();
                SessionState.SetInt(Key, 4);
            }
            else if (step == 4)
            {
                if (session.State != RaceSessionManager.RaceState.Finished || !session.CarBody.isKinematic) return;
                Require(finish.SaveSucceeded, "Race result save failed.");
                Require(ResultsRepository.Load().results.Count == completed + 1, "Result saved more than once.");
                Require(finish.LastResult.carId == (completed % 2 == 0 ? "ae86" : "rmcar26"), "Wrong car metadata after a round trip.");
                finish.GetComponentsInChildren<Button>(true).Single(value => value.name == "Btn_Garage").onClick.Invoke();
                SessionState.SetInt(Key + "Completed", completed + 1);
                SessionState.SetInt(Key, 1);
            }
        }
        catch (Exception error)
        {
            SessionState.SetInt(Key, 0);
            Debug.LogException(error);
            End(1);
        }
    }

    private static IEnumerable<T> Components<T>(GameObject[] roots) where T : Component => roots.SelectMany(root => root.GetComponentsInChildren<T>(true));

    private static void CaptureSkidResources(GameObject[] roots, List<UnityEngine.Object> destination)
    {
        foreach (Skidmarks marks in Components<Skidmarks>(roots))
        {
            MeshFilter filter = marks.GetComponent<MeshFilter>();
            MeshRenderer renderer = marks.GetComponent<MeshRenderer>();
            if (filter != null && filter.sharedMesh != null && !AssetDatabase.Contains(filter.sharedMesh)) destination.Add(filter.sharedMesh);
            if (renderer != null && renderer.sharedMaterial != null && !AssetDatabase.Contains(renderer.sharedMaterial)) destination.Add(renderer.sharedMaterial);
        }
    }

    private static void End(int exitCode)
    {
        SessionState.SetInt(Key, 0);
        try
        {
            string path = SessionState.GetString(Key + "Path", string.Empty);
            string backup = SessionState.GetString(Key + "Backup", string.Empty);
            if (SessionState.GetBool(Key + "HadFile", false) && !File.Exists(backup)) throw new IOException("Original result history backup missing; leave current history intact.");
            if (File.Exists(path)) File.Delete(path);
            if (File.Exists(backup)) File.Move(backup, path);
            if (File.Exists(path + ".tmp")) File.Delete(path + ".tmp");
        }
        catch (Exception error) { Debug.LogException(error); exitCode = 1; }
        EditorApplication.Exit(exitCode);
    }

    private static void Require(bool condition, string message) { if (!condition) throw new Exception(message); }
}
