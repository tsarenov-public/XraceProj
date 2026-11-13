using System;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

public static class GarageSceneBuilder
{
    private const string ScenePath = "Assets/Scenes/Garage.unity";

    [MenuItem("Xrace/Create Beta Garage")]
    public static void Build()
    {
        if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        if (File.Exists(ScenePath)) throw new InvalidOperationException("Garage already exists; preserve scene edits.");
        if (AssetDatabase.LoadAssetAtPath<TMP_Settings>("Assets/TextMesh Pro/Resources/TMP Settings.asset") == null)
        {
            throw new InvalidOperationException("Import TMP Essential Resources before building the garage.");
        }
        Directory.CreateDirectory("Assets/ScriptableObjects/Cars");
        Directory.CreateDirectory("Assets/Audio/Music");
        Directory.CreateDirectory("Assets/Prefabs/Systems");
        AssetDatabase.Refresh();
        CarDefinition[] cars =
        {
            CreateCar("ae86", "Toyota AE86", "Assets/Prefabs/Toyota AE86 Sprinter Trueno Zenki.prefab"),
            CreateCar("rmcar26", "RMCar26", "Assets/Prefabs/Car.prefab")
        };
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        RenderSettings.ambientLight = new Color(0.5f, 0.5f, 0.55f);
        var cameraObject = new GameObject("GarageCamera", typeof(Camera), typeof(AudioListener));
        cameraObject.tag = "MainCamera";
        var camera = cameraObject.GetComponent<Camera>();
        camera.transform.position = new Vector3(6, 3, -7);
        camera.transform.LookAt(new Vector3(0, 0.9f, 0));
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(0.035f, 0.045f, 0.065f);
        camera.fieldOfView = 42;
        var eventSystem = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
        eventSystem.GetComponent<InputSystemUIInputModule>().AssignDefaultActions();
        var root = new GameObject("GarageRoot");
        var spot = new GameObject("CarSpot").transform;
        spot.SetParent(root.transform, false);
        spot.localPosition = new Vector3(0, 0.05f, 0);
        var floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
        floor.name = "GarageFloor";
        floor.transform.position = new Vector3(0, -0.15f, 0);
        floor.transform.localScale = new Vector3(20, 0.3f, 20);
        var light = new GameObject("GarageLight", typeof(Light)).GetComponent<Light>();
        light.type = LightType.Directional;
        light.intensity = 2;
        light.transform.rotation = Quaternion.Euler(45, -30, 0);
        var canvas = new GameObject("Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvas.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
        var scaler = canvas.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;
        // Dynamic TMP font includes Cyrillic rather than relying on Latin-only default SDF.
        Font sourceFont = AssetDatabase.LoadAssetAtPath<Font>("Assets/UI/Fonts/GarageFont.ttf");
        if (sourceFont == null) throw new InvalidOperationException("Missing Assets/UI/Fonts/GarageFont.ttf");
        TMP_FontAsset font = TMP_FontAsset.CreateFontAsset(sourceFont);
        AssetDatabase.CreateAsset(font, "Assets/UI/Fonts/GarageFont.asset");
        AssetDatabase.AddObjectToAsset(font.material, font);
        foreach (Texture2D atlas in font.atlasTextures) AssetDatabase.AddObjectToAsset(atlas, font);
        var manager = root.AddComponent<GarageManager>();
        var name = Text("CarName_Text", canvas.transform, font, "", 42);
        Rect(name.rectTransform, new Vector2(0.5f, 0), new Vector2(0.5f, 0.5f), new Vector2(0, 140), new Vector2(700, 80));
        Button prev = Button("Btn_PrevCar", "<", canvas.transform, font);
        Rect(prev.GetComponent<RectTransform>(), new Vector2(0, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(100, 0), new Vector2(100, 100));
        Button next = Button("Btn_NextCar", ">", canvas.transform, font);
        Rect(next.GetComponent<RectTransform>(), new Vector2(1, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-100, 0), new Vector2(100, 100));
        Button race = Button("Btn_Race", "В ГОНКУ", canvas.transform, font);
        Rect(race.GetComponent<RectTransform>(), new Vector2(1, 0), new Vector2(1, 0), new Vector2(-60, 60), new Vector2(300, 90));
        UnityEventTools.AddPersistentListener(prev.onClick, manager.PrevCar);
        UnityEventTools.AddPersistentListener(next.onClick, manager.NextCar);
        UnityEventTools.AddPersistentListener(race.onClick, manager.Race);
        var musicObject = new GameObject("MusicManager", typeof(AudioSource), typeof(MusicManager));
        musicObject.GetComponent<AudioSource>().playOnAwake = false;
        var musicSerialized = new SerializedObject(musicObject.GetComponent<MusicManager>());
        musicSerialized.FindProperty("audioSource").objectReferenceValue = musicObject.GetComponent<AudioSource>();
        string[] audioGuids = AssetDatabase.FindAssets("t:AudioClip", new[] { "Assets/Audio/Music" });
        var tracks = musicSerialized.FindProperty("tracks");
        tracks.arraySize = audioGuids.Length;
        for (int i = 0; i < audioGuids.Length; i++) tracks.GetArrayElementAtIndex(i).objectReferenceValue = AssetDatabase.LoadAssetAtPath<AudioClip>(AssetDatabase.GUIDToAssetPath(audioGuids[i]));
        musicSerialized.ApplyModifiedPropertiesWithoutUndo();
        GameObject musicPrefab = PrefabUtility.SaveAsPrefabAsset(musicObject, "Assets/Prefabs/Systems/MusicManager.prefab");
        var serialized = new SerializedObject(manager);
        serialized.FindProperty("carSpot").objectReferenceValue = spot;
        serialized.FindProperty("carNameText").objectReferenceValue = name;
        serialized.FindProperty("raceButton").objectReferenceValue = race;
        serialized.FindProperty("musicManagerPrefab").objectReferenceValue = musicPrefab.GetComponent<MusicManager>();
        var carArray = serialized.FindProperty("cars");
        carArray.arraySize = cars.Length;
        for (int i = 0; i < cars.Length; i++)
            carArray.GetArrayElementAtIndex(i).objectReferenceValue = AssetDatabase.LoadAssetAtPath<CarDefinition>(
                "Assets/ScriptableObjects/Cars/" + (i == 0 ? "ae86" : "rmcar26") + ".asset");
        serialized.ApplyModifiedPropertiesWithoutUndo();
        EditorSceneManager.SaveScene(scene, ScenePath);
        EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true), new EditorBuildSettingsScene("Assets/Scenes/Touge01.unity", true) };
        AssetDatabase.SaveAssets();
        Debug.Log("XRACE_GARAGE_BUILD_OK");
    }

    public static void RepairCarReferencesAndVerify()
    {
        var scene = EditorSceneManager.OpenScene(ScenePath);
        GarageManager manager = null;
        foreach (GameObject root in scene.GetRootGameObjects())
            if (root.TryGetComponent(out GarageManager candidate)) manager = candidate;
        if (manager == null) throw new InvalidOperationException("GarageManager missing.");
        var serialized = new SerializedObject(manager);
        var cars = serialized.FindProperty("cars");
        cars.arraySize = 2;
        cars.GetArrayElementAtIndex(0).objectReferenceValue = AssetDatabase.LoadAssetAtPath<CarDefinition>("Assets/ScriptableObjects/Cars/ae86.asset");
        cars.GetArrayElementAtIndex(1).objectReferenceValue = AssetDatabase.LoadAssetAtPath<CarDefinition>("Assets/ScriptableObjects/Cars/rmcar26.asset");
        serialized.ApplyModifiedPropertiesWithoutUndo();
        EditorSceneManager.SaveScene(scene);
        GarageSmokeCheck.Run();
    }

    private static CarDefinition CreateCar(string id, string name, string prefabPath)
    {
        string path = "Assets/ScriptableObjects/Cars/" + id + ".asset";
        CarDefinition car = AssetDatabase.LoadAssetAtPath<CarDefinition>(path);
        if (car != null) return car;
        car = ScriptableObject.CreateInstance<CarDefinition>();
        car.carId = id;
        car.displayName = name;
        car.carPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        if (car.carPrefab == null) throw new InvalidOperationException("Missing car prefab: " + prefabPath);
        AssetDatabase.CreateAsset(car, path);
        return car;
    }

    private static TextMeshProUGUI Text(string name, Transform parent, TMP_FontAsset font, string value, float size)
    {
        var text = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI)).GetComponent<TextMeshProUGUI>();
        text.transform.SetParent(parent, false);
        text.font = font;
        text.text = value;
        text.fontSize = size;
        text.alignment = TextAlignmentOptions.Center;
        text.raycastTarget = false;
        return text;
    }

    private static Button Button(string name, string label, Transform parent, TMP_FontAsset font)
    {
        var button = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button)).GetComponent<Button>();
        button.transform.SetParent(parent, false);
        button.GetComponent<Image>().color = new Color(0.12f, 0.17f, 0.23f, 0.95f);
        button.targetGraphic = button.GetComponent<Image>();
        button.navigation = new Navigation { mode = Navigation.Mode.None };
        var text = Text("Label", button.transform, font, label, 38);
        text.rectTransform.anchorMin = Vector2.zero;
        text.rectTransform.anchorMax = Vector2.one;
        text.rectTransform.offsetMin = text.rectTransform.offsetMax = Vector2.zero;
        return button;
    }

    private static void Rect(RectTransform rect, Vector2 anchor, Vector2 pivot, Vector2 position, Vector2 size)
    {
        rect.anchorMin = rect.anchorMax = anchor;
        rect.pivot = pivot;
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
    }
}
