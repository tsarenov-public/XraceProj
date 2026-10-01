using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Batch-only regression check. Opens scenes for inspection and closes without saving.
// Repository probes always restore the original history, including on assertion failure.
public static class BetaValidationCheck
{
    public static void Run()
    {
        Require(!EditorApplication.isPlaying, "Run beta validation outside Play Mode.");
        var buildScenes = EditorBuildSettings.scenes.Where(s => s.enabled).ToArray();
        Require(buildScenes.Length == 2 && buildScenes[0].path == "Assets/Scenes/Garage.unity" &&
            buildScenes[1].path == "Assets/Scenes/Touge01.unity", "Build scenes must be Garage (0), Touge01 (1).");
        var input = Regex.Match(File.ReadAllText("ProjectSettings/ProjectSettings.asset"), @"(?m)^\s*activeInputHandler:\s*(\d+)");
        Require(input.Success && input.Groups[1].Value == "2", "Both input systems must remain enabled for existing vehicle controls.");
        var desktopPipeline = UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.Rendering.Universal.UniversalRenderPipelineAsset>("Assets/Settings/PC_RPAsset.asset");
        Require(desktopPipeline != null && new SerializedObject(desktopPipeline).FindProperty("m_GPUResidentDrawerMode").intValue == 0,
            "GPU Resident Drawer must stay disabled for this beta: native ObjectDispatcher crash reproduced during scene changes.");
        InspectScene("Assets/Scenes/Garage.unity", ValidateGarage);
        InspectScene("Assets/Scenes/Touge01.unity", ValidateRace);
        ValidateRepository();
        Debug.Log("XRACE_BETA_VALIDATION_OK: build/input, scene references/buttons/triggers, corrupt history, schema, strict records, sort, duplicate and invalid entry rejection; original history restored.");
    }

    private static void InspectScene(string path, Action<GameObject[]> inspect)
    {
        Scene previous = SceneManager.GetActiveScene();
        Scene scene = SceneManager.GetSceneByPath(path);
        bool opened = !scene.IsValid() || !scene.isLoaded;
        if (opened) scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
        try
        {
            var roots = scene.GetRootGameObjects();
            foreach (var transform in roots.SelectMany(r => r.GetComponentsInChildren<Transform>(true)))
                Require(GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(transform.gameObject) == 0,
                    path + ": missing script on " + transform.name);
            Require(Components<EventSystem>(roots).Length == 1, path + ": exactly one EventSystem is required.");
            inspect(roots);
        }
        finally
        {
            if (opened) EditorSceneManager.CloseScene(scene, true);
            if (previous.IsValid() && previous.isLoaded) SceneManager.SetActiveScene(previous);
        }
    }

    private static void ValidateGarage(GameObject[] roots)
    {
        var manager = Single<GarageManager>(roots);
        References(manager, "carSpot", "carNameText", "raceButton", "musicManagerPrefab");
        var cars = new SerializedObject(manager).FindProperty("cars");
        Require(cars.arraySize > 0, "Garage has no cars.");
        var ids = new HashSet<string>();
        for (int i = 0; i < cars.arraySize; i++)
        {
            var car = cars.GetArrayElementAtIndex(i).objectReferenceValue as CarDefinition;
            Require(car != null && car.carPrefab != null && !string.IsNullOrWhiteSpace(car.displayName) &&
                !string.IsNullOrWhiteSpace(car.carId) && ids.Add(car.carId), "Invalid or duplicate garage CarDefinition.");
            Require(car.carPrefab.GetComponentInChildren<Rigidbody>(true) != null, car.carId + ": no Rigidbody.");
            Require(car.carPrefab.GetComponentInChildren<WheelController>(true) != null, car.carId + ": no WheelController.");
        }
        ButtonBinding(roots, "Btn_PrevCar", manager, "PrevCar");
        ButtonBinding(roots, "Btn_NextCar", manager, "NextCar");
        ButtonBinding(roots, "Btn_Race", manager, "Race");
        var prefab = (MusicManager)new SerializedObject(manager).FindProperty("musicManagerPrefab").objectReferenceValue;
        References(prefab, "audioSource");
        Require(!prefab.GetComponent<AudioSource>().playOnAwake, "Music prefab must not play automatically in Garage.");
        var tracks = new SerializedObject(prefab).FindProperty("tracks");
        if (tracks.arraySize == 0) Debug.LogWarning("XRACE_BETA_MANUAL: MusicManager Tracks is empty; add clips before beta.");
        for (int i = 0; i < tracks.arraySize; i++)
            Require(tracks.GetArrayElementAtIndex(i).objectReferenceValue != null, "Music tracks contain an empty slot.");
    }

    private static void ValidateRace(GameObject[] roots)
    {
        var session = Single<RaceSessionManager>(roots);
        References(session, "timer", "driftState", "scorer", "stopper");
        var spawner = Single<CarSpawner>(roots);
        References(spawner, "startSpawn", "fallbackCar", "session", "roadSurface");
        var start = (Transform)new SerializedObject(spawner).FindProperty("startSpawn").objectReferenceValue;
        Require(Vector3.Distance(start.position, new Vector3(311, 152, -156)) < 0.01f, "StartSpawn coordinates changed.");
        References(Single<RaceTimer>(roots), "session");
        References(Single<DriftScorer>(roots), "session", "driftState");
        References(Single<RaceHUDController>(roots), "session", "timer", "scorer", "hudPanel", "timeText", "scoreText", "multiplierText");
        var finish = Single<FinishPanelController>(roots);
        References(finish, "session", "timer", "scorer", "fallbackCar", "finishPanel", "finishTimeText", "finishScoreText", "recordText", "garageButton", "retryButton");
        Require(finish.enabled, "Finish controller is disabled.");
        var pause = Single<PauseMenuController>(roots);
        References(pause, "session", "pausePanel", "settingsPanel", "musicVolumeSlider", "musicManagerPrefab");
        Require(pause.enabled, "Pause controller is disabled.");
        foreach (var name in new[] { "Finish_Panel", "Record_Text", "Pause_Panel", "Settings_Panel" })
            Require(!Named(roots, name).activeSelf, name + " must be initially hidden.");
        Require(Named(roots, "HUD_Panel").activeSelf, "HUD must initially be visible.");
        var slider = Named(roots, "MusicVolume_Slider").GetComponent<Slider>();
        Require(slider != null && slider.minValue == 0 && slider.maxValue == 1 && !slider.wholeNumbers, "Volume slider must span 0..1.");
        Require(slider.navigation.mode == Navigation.Mode.None, "Volume slider must not consume track-selection arrows.");
        EventBinding(slider.onValueChanged, pause, "SetMusicVolume", "MusicVolume_Slider");
        ButtonBinding(roots, "Btn_Garage", finish, "GoGarage");
        ButtonBinding(roots, "Btn_Retry", finish, "Retry");
        ButtonBinding(roots, "Btn_Continue", pause, "Continue");
        ButtonBinding(roots, "Btn_Settings", pause, "OpenSettings");
        ButtonBinding(roots, "Btn_ExitGarage", pause, "ExitGarage");
        ButtonBinding(roots, "Btn_MusicPrev", pause, "PrevTrack");
        ButtonBinding(roots, "Btn_MusicNext", pause, "NextTrack");
        ButtonBinding(roots, "Btn_SettingsBack", pause, "SettingsBack");
        var trigger = Single<RaceFinishTrigger>(roots);
        References(trigger, "session");
        var finishBox = trigger.GetComponent<BoxCollider>();
        Require(finishBox != null && finishBox.enabled && finishBox.isTrigger, "FinishTrigger collider is invalid.");
        Require(Vector3.Distance(trigger.transform.position, new Vector3(-372, 40, 190)) < 0.01f, "FinishTrigger coordinates changed.");
        var zones = Components<DriftZone>(roots).OrderBy(z => z.ZoneId).ToArray();
        Require(zones.Length >= 3, "Not enough drift zones for the beta route.");
        for (int i = 0; i < zones.Length; i++)
        {
            var box = zones[i].GetComponent<BoxCollider>();
            Require(zones[i].ZoneId == i + 1 && zones[i].enabled && zones[i].gameObject.activeInHierarchy, "Zone IDs/activation are invalid.");
            Require(box != null && box.enabled && box.isTrigger && box.size.x > 0 && box.size.y > 0 && box.size.z > 0 &&
                zones[i].GetComponent<Rigidbody>() == null, zones[i].name + ": collider configuration invalid.");
            References(zones[i], "session", "scorer");
            for (int j = 0; j < i; j++)
                Require(!Physics.ComputePenetration(box, box.transform.position, box.transform.rotation,
                    zones[j].GetComponent<BoxCollider>(), zones[j].transform.position, zones[j].transform.rotation, out _, out _),
                    zones[i].name + " overlaps " + zones[j].name);
        }
    }

    private static void ValidateRepository()
    {
        string path = ResultsRepository.FilePath;
        string temporary = path + ".tmp";
        Require(!File.Exists(temporary), "Pending result .tmp exists; preserve it and skip the data probe.");
        Directory.CreateDirectory(Application.persistentDataPath);
        Require(Directory.GetFiles(Application.persistentDataPath, "race_results.json.*test-backup-*").Length == 0,
            "A previous test history backup exists; recover it before running another data probe.");
        string backup = path + ".beta-test-backup-" + Guid.NewGuid().ToString("N");
        bool hadHistory = File.Exists(path);
        var generatedCorruptFiles = new List<string>();
        if (hadHistory) File.Move(path, backup);
        Debug.Log("XRACE_BETA_HISTORY_PROTECTED: " + (hadHistory ? backup : "original history absent"));
        try
        {
            Require(ResultsRepository.Load().results.Count == 0 && !File.Exists(path), "Missing history must load empty without writing.");
            var first = ResultsRepository.SaveNewEntry(Entry("a", 100, 30));
            Require(first.place == 1 && first.resultId == "a" && first.isNewRecord, "First positive score record failed.");
            var tied = ResultsRepository.SaveNewEntry(Entry("b", 100, 20));
            Require(tied.place == 1 && !tied.isNewRecord, "Tied score must use time and must not set a record.");
            var better = ResultsRepository.SaveNewEntry(Entry("c", 101, 40));
            Require(better.place == 1 && better.isNewRecord, "Strictly higher score must set a record.");
            ResultsRepository.SaveNewEntry(Entry("d", 100, 20));
            var sorted = ResultsRepository.Load().results;
            Require(sorted.Select(r => r.id).SequenceEqual(new[] { "c", "b", "d", "a" }), "Score/time sorting is incorrect.");
            string before = File.ReadAllText(path);
            Expect<ArgumentException>(() => ResultsRepository.SaveNewEntry(Entry("c", 200, 1)), "Duplicate ID accepted.");
            Expect<ArgumentException>(() => ResultsRepository.SaveNewEntry(Entry("negative", -1, 1)), "Negative score accepted.");
            Expect<ArgumentException>(() => ResultsRepository.SaveNewEntry(Entry("nan", 1, float.NaN)), "NaN time accepted.");
            Expect<ArgumentException>(() => ResultsRepository.SaveNewEntry(Entry("infinite", 1, float.PositiveInfinity)), "Infinite time accepted.");
            Require(File.ReadAllText(path) == before, "Rejected entries changed history.");
            ProbeCorrupt(path, "{\"schemaVersion\":1,\"results\":[", generatedCorruptFiles);
            ProbeCorrupt(path, "{\"schemaVersion\":1,\"results\":[{\"id\":\"bad\",\"timeSeconds\":-1,\"driftScore\":1}]}", generatedCorruptFiles);
            const string futureSchema = "{\"schemaVersion\":2,\"results\":[]}";
            File.WriteAllText(path, futureSchema);
            Expect<NotSupportedException>(() => ResultsRepository.Load(), "Unsupported schema accepted.");
            Require(File.ReadAllText(path) == futureSchema, "Unsupported schema file was modified.");
            File.Delete(path);
            Require(!ResultsRepository.SaveNewEntry(Entry("zero", 0, 1)).isNewRecord, "A zero score must not exceed the empty-history baseline.");
        }
        finally
        {
            // Refuse to destroy the test history if the original backup disappeared.
            if (hadHistory && !File.Exists(backup)) throw new IOException("Original history backup missing; current history left untouched.");
            if (File.Exists(path)) File.Delete(path);
            if (File.Exists(temporary)) File.Delete(temporary); // Run refused a pre-existing .tmp.
            foreach (string corrupt in generatedCorruptFiles) if (File.Exists(corrupt)) File.Delete(corrupt);
            if (hadHistory) File.Move(backup, path);
        }
    }

    private static void ProbeCorrupt(string path, string fixture, List<string> generated)
    {
        var previous = new HashSet<string>(Directory.GetFiles(Application.persistentDataPath, "race_results_corrupt_*.json"));
        File.WriteAllText(path, fixture);
        ResultsData loaded = null;
        try { loaded = ResultsRepository.Load(); }
        finally
        {
            // Only files produced by this probe are eligible for removal.
            foreach (string candidate in Directory.GetFiles(Application.persistentDataPath, "race_results_corrupt_*.json"))
                if (!previous.Contains(candidate) && File.ReadAllText(candidate) == fixture) generated.Add(candidate);
        }
        Require(loaded != null && loaded.results.Count == 0 && !File.Exists(path), "Corrupt history was not replaced by an empty in-memory list.");
        Require(generated.Count > 0 && File.ReadAllText(generated[generated.Count - 1]) == fixture, "Corrupt history backup is missing or changed.");
    }

    private static RaceResultEntry Entry(string id, int score, float seconds) => new RaceResultEntry
    {
        id = id, carId = "validation-car", carName = "Validation", timeSeconds = seconds,
        driftScore = score, maxMultiplier = 1, createdAtUtc = DateTime.UtcNow.ToString("O")
    };
    private static T[] Components<T>(GameObject[] roots) where T : Component => roots.SelectMany(r => r.GetComponentsInChildren<T>(true)).ToArray();
    private static T Single<T>(GameObject[] roots) where T : Component
    {
        var values = Components<T>(roots);
        Require(values.Length == 1, "Expected exactly one " + typeof(T).Name + "; found " + values.Length);
        return values[0];
    }
    private static GameObject Named(GameObject[] roots, string name)
    {
        var matches = roots.SelectMany(r => r.GetComponentsInChildren<Transform>(true)).Where(t => t.name == name).ToArray();
        Require(matches.Length == 1, "Expected exactly one object named " + name);
        return matches[0].gameObject;
    }
    private static void References(UnityEngine.Object component, params string[] fields)
    {
        var serialized = new SerializedObject(component);
        foreach (var field in fields)
        {
            var property = serialized.FindProperty(field);
            Require(property != null && property.objectReferenceValue != null, component.GetType().Name + "." + field + " is unassigned.");
        }
    }
    private static void ButtonBinding(GameObject[] roots, string name, UnityEngine.Object target, string method)
    {
        var button = Named(roots, name).GetComponent<Button>();
        Require(button != null, name + ": Button component missing.");
        EventBinding(button.onClick, target, method, name);
    }
    private static void EventBinding(UnityEventBase action, UnityEngine.Object target, string method, string name)
    {
        Require(action.GetPersistentEventCount() == 1 && action.GetPersistentTarget(0) == target &&
            action.GetPersistentMethodName(0) == method && action.GetPersistentListenerState(0) != UnityEventCallState.Off,
            name + ": missing, duplicate or incorrect callback.");
    }
    private static void Expect<T>(Action action, string message) where T : Exception
    {
        try { action(); } catch (T) { return; }
        throw new InvalidOperationException(message);
    }
    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
