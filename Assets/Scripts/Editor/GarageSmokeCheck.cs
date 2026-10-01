using System;
using System.Linq;
using System.Reflection;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

// Batch verification of real scene wiring, preview safety and transition.
[InitializeOnLoad]
public static class GarageSmokeCheck
{
    private const string Key = "Xrace.GarageSmokeStep";
    private static int frames;
    private static Material lensAsset;
    private static Color lensColor;
    private static bool alternateCar;
    private static bool waitingAlternate;
    static GarageSmokeCheck() => EditorApplication.update += Tick;

    public static void Run()
    {
        alternateCar = Environment.GetCommandLineArgs().Contains("-xrace-alternate-car");
        waitingAlternate = false;
        lensAsset = AssetDatabase.LoadAssetAtPath<Material>("Assets/Models/car/toyota-ae86-sprinter-trueno-zenki/materials/M_HeadlightLens.mat");
        lensColor = lensAsset.GetColor("_EmissionColor");
        EditorSceneManager.OpenScene("Assets/Scenes/Garage.unity");
        CheckEditorPreview();
        SessionState.SetInt(Key, 1);
        SessionState.SetString(Key + "Deadline", DateTime.UtcNow.AddMinutes(3).ToString("O"));
        EditorApplication.EnterPlaymode();
    }

    private static void Tick()
    {
        int step = SessionState.GetInt(Key, 0);
        if (step == 0) return;
        try
        {
            if (DateTime.UtcNow > DateTime.Parse(SessionState.GetString(Key + "Deadline", "")))
                throw new Exception("Garage smoke check timed out.");
            if (!EditorApplication.isPlaying || EditorApplication.isCompiling) return;
            if (++frames < 15) return;
            frames = 0;
            Scene scene = SceneManager.GetActiveScene();
            Debug.Log("XRACE_GARAGE_STEP " + step + ": " + scene.name);
            if (step == 4)
            {
                if (scene.name != "Touge01") return;
                Require(SelectedCarService.Get() != null && SelectedCarService.Get().carId == (alternateCar ? "rmcar26" : "ae86"), "Selected car did not survive scene transition.");
                var raceSession=scene.GetRootGameObjects().Select(root=>root.GetComponent<RaceSessionManager>()).First(value=>value!=null);
                Require(raceSession.CarBody!=null && raceSession.CarBody.GetComponentInParent<SpawnedRaceCar>()!=null,"Selected car was not spawned on track.");
                Require(raceSession.GetComponent<CarSpawner>().Spawn()==raceSession.CarBody,"Repeated spawn created a second car.");
                Require(raceSession.CarBody.GetComponent<VehicleHud>() != null && raceSession.CarBody.GetComponent<VehiclePresentation>() != null, "Spawned car presentation missing after Garage.");
                Require(lensAsset.GetColor("_EmissionColor") == lensColor, "Vehicle lights modified the shared material asset.");
                Debug.Log("XRACE_GARAGE_SMOKE_OK: initial preview, next, previous, safe preview, selected car and Touge01 load.");
                SessionState.SetInt(Key, 0);
                EditorApplication.Exit(0);
                return;
            }
            Require(scene.name == "Garage", "Unexpected active scene.");
            GameObject[] roots = scene.GetRootGameObjects();
            GarageManager manager = roots.Select(root => root.GetComponent<GarageManager>()).First(value => value != null);
            TMP_Text name = roots.SelectMany(root => root.GetComponentsInChildren<TMP_Text>()).First(value => value.name == "CarName_Text");
            Button[] buttons = roots.SelectMany(root => root.GetComponentsInChildren<Button>()).ToArray();
            Require(MusicManager.Instance != null, "Music manager missing.");
            Require(!MusicManager.Instance.GetComponent<AudioSource>().isPlaying, "Music playing in Garage.");
            Require(buttons.First(b => b.name == "Btn_Race").interactable, "Race button disabled with a valid car.");
            GameObject preview = manager.GetComponentsInChildren<Transform>().First(value => value.name == "CarPreview").gameObject;
            Require(preview.activeInHierarchy, "Preview inactive.");
            Require(preview.GetComponentsInChildren<Renderer>().Length > 0, "Preview has no renderers.");
            Require(preview.GetComponentsInChildren<MonoBehaviour>(true).Length == 0, "Vehicle scripts still active on preview.");
            Require(preview.GetComponentsInChildren<Rigidbody>(true).Length == 0, "Preview has native vehicle physics.");
            Require(preview.GetComponentsInChildren<Collider>(true).Length == 0, "Preview has colliders.");
            Require(preview.GetComponentsInChildren<Camera>(true).Length == 0, "Preview camera active.");
            Require(preview.GetComponentsInChildren<AudioSource>(true).Length == 0, "Preview has vehicle audio.");
            Require(preview.GetComponentsInChildren<ParticleSystem>(true).Length == 0, "Wheel smoke exists in garage preview.");
            foreach (LODGroup group in preview.GetComponentsInChildren<LODGroup>(true))
                foreach (LOD lod in group.GetLODs())
                    Require(lod.renderers.All(renderer => renderer != null && renderer.transform.IsChildOf(preview.transform)), "Preview LOD references a source prefab renderer.");
            if (step == 1)
            {
                Require(name.text == "Toyota AE86", "Wrong initial car label.");
                CheckPreviewRotation(manager, preview, buttons.First(button => button.name == "Btn_Race"));
                if (Environment.GetCommandLineArgs().Contains("-xrace-capture-preview")) CapturePreview(roots);
                buttons.First(button => button.name == "Btn_NextCar").onClick.Invoke();
            }
            else if (step == 2)
            {
                Require(name.text == "RMCar26", "Next button failed.");
                buttons.First(button => button.name == "Btn_PrevCar").onClick.Invoke();
            }
            else
            {
                if (alternateCar && !waitingAlternate)
                {
                    Require(name.text == "Toyota AE86", "Previous button failed.");
                    buttons.First(button => button.name == "Btn_NextCar").onClick.Invoke();
                    waitingAlternate = true;
                    return; // Let preview safety initialization finish before the Race click.
                }
                Require(name.text == (alternateCar ? "RMCar26" : "Toyota AE86"), "Selected car label incorrect.");
                buttons.First(button => button.name == "Btn_Race").onClick.Invoke();
            }
            SessionState.SetInt(Key, step + 1);
        }
        catch (Exception error)
        {
            Debug.LogException(error);
            SessionState.SetInt(Key, 0);
            EditorApplication.Exit(1);
        }
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
    }

    private static void CheckPreviewRotation(GarageManager manager, GameObject preview, Button raceButton)
    {
        var serialized = new SerializedObject(manager);
        Require(serialized.FindProperty("allowPreviewRotation").boolValue, "Garage preview rotation disabled by default.");
        float sensitivity = serialized.FindProperty("previewRotationDegreesPerPixel").floatValue;
        Require(sensitivity > 0f, "Garage rotation sensitivity must be positive.");
        Vector3 position = preview.transform.localPosition;
        Quaternion spotRotation = preview.transform.parent.localRotation;
        MethodInfo rotate = typeof(GarageManager).GetMethod("ApplyPreviewRotation", BindingFlags.Instance | BindingFlags.NonPublic);
        rotate.Invoke(manager, new object[] { 90f / sensitivity });
        Require(Quaternion.Angle(preview.transform.localRotation, Quaternion.AngleAxis(90f, Vector3.right)) < 0.01f, "Preview rotates on an axis other than local X.");
        Require(preview.transform.localPosition == position && preview.transform.parent.localRotation == spotRotation, "Preview rotation moved CarSpot or the car position.");
        rotate.Invoke(manager, new object[] { 270f / sensitivity });
        Require(Quaternion.Angle(preview.transform.localRotation, Quaternion.identity) < 0.01f, "Preview cannot complete a full 360 degree turn.");
        Canvas.ForceUpdateCanvases();
        Vector2 buttonPosition = RectTransformUtility.WorldToScreenPoint(null, raceButton.transform.position);
        MethodInfo blocked = typeof(GarageManager).GetMethod("IsPointerOverUi", BindingFlags.Instance | BindingFlags.NonPublic);
        Require((bool)blocked.Invoke(manager, new object[] { buttonPosition }), "UI clicks are not excluded from preview dragging.");
    }

    private static void CheckEditorPreview()
    {
        Scene original = SceneManager.GetActiveScene();
        Scene temporary = default;
        string path = "Assets/Scenes/GaragePreviewSmoke_" + Guid.NewGuid().ToString("N") + ".unity";
        try
        {
            temporary = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            SceneManager.SetActiveScene(temporary);
            var root = new GameObject("GaragePreviewTestRoot");
            var manager = root.AddComponent<GarageManager>();
            var spot = new GameObject("CarSpot").transform;
            spot.SetParent(root.transform, false);
            var serialized = new SerializedObject(manager);
            serialized.FindProperty("carSpot").objectReferenceValue = spot;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            CarDefinition car = AssetDatabase.LoadAssetAtPath<CarDefinition>("Assets/ScriptableObjects/Cars/ae86.asset");
            GameObject preview = GarageManagerEditor.ShowForEditing(manager, car);
            Require(preview != null && preview.GetComponentsInChildren<Renderer>(true).Length > 0, "Editor car preview has no visual model.");
            Require(preview.GetComponentsInChildren<MonoBehaviour>(true).Length == 0, "Editor preview contains vehicle scripts.");
            Require((preview.hideFlags & HideFlags.DontSaveInEditor) != 0 && (preview.hideFlags & HideFlags.DontSaveInBuild) != 0, "Editor preview can leak into a saved scene or build.");
            Require(EditorSceneManager.SaveScene(temporary, path), "Could not save temporary editor preview verification scene.");
            Require(preview == null && spot.childCount == 0, "Editor preview was not removed before scene save.");
            Require(!System.IO.File.ReadAllText(path).Contains("CarPreview_Editor"), "Temporary preview was serialized into the scene.");
            Debug.Log("XRACE_GARAGE_EDITOR_PREVIEW_OK: safe visuals, transient flags and save cleanup.");
        }
        finally
        {
            if (temporary.IsValid() && temporary.isLoaded) EditorSceneManager.CloseScene(temporary, true);
            AssetDatabase.DeleteAsset(path);
            SceneManager.SetActiveScene(original);
        }
    }

    private static void CapturePreview(GameObject[] roots)
    {
        Camera camera = roots.Select(root => root.GetComponent<Camera>()).First(value => value != null);
        Canvas canvas = roots.Select(root => root.GetComponent<Canvas>()).First(value => value != null);
        var renderTexture = new RenderTexture(1280, 720, 24);
        var texture = new Texture2D(1280, 720, TextureFormat.RGB24, false);
        RenderTexture previous = RenderTexture.active;
        try
        {
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = camera;
            canvas.planeDistance = 1;
            camera.targetTexture = renderTexture;
            Canvas.ForceUpdateCanvases();
            RenderPipeline.SubmitRenderRequest(camera, new UniversalRenderPipeline.SingleCameraRequest { destination = renderTexture });
            RenderTexture.active = renderTexture;
            texture.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0);
            texture.Apply();
            System.IO.File.WriteAllBytes("Logs/GaragePreview.png", texture.EncodeToPNG());
        }
        finally
        {
            camera.targetTexture = null;
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            RenderTexture.active = previous;
            UnityEngine.Object.DestroyImmediate(texture);
            UnityEngine.Object.DestroyImmediate(renderTexture);
        }
    }
}
