using System;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

public static class PauseMenuEditorSetup
{
    public static void FinalizeSetupAndCheckVolume()
    {
        LinkGarageMusicPrefab();
        PauseMenuSmokeCheck.WriteRestartVolume();
    }
    public static void LinkGarageMusicPrefab()
    {
        var scene = EditorSceneManager.OpenScene("Assets/Scenes/Garage.unity");
        var current = scene.GetRootGameObjects().Select(r => r.GetComponent<MusicManager>()).FirstOrDefault(m => m != null);
        if (current != null)
        {
            var tracks = new SerializedObject(current).FindProperty("tracks");
            if (tracks.arraySize != 0) throw new InvalidOperationException("Preserve existing garage tracks before linking prefab.");
            UnityEngine.Object.DestroyImmediate(current.gameObject);
        }
        PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Systems/MusicManager.prefab"), scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("XRACE_GARAGE_MUSIC_PREFAB_OK");
    }
    [MenuItem("Xrace/Create Pause And Settings Panels")]
    public static void Apply()
    {
        if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        var scene = EditorSceneManager.OpenScene("Assets/Scenes/Touge01.unity");
        var roots = scene.GetRootGameObjects();
        var canvas = roots.Select(r => r.GetComponent<Canvas>()).First(c => c != null);
        if (canvas.GetComponentsInChildren<Transform>(true).Any(t => t.name == "Pause_Panel")) throw new InvalidOperationException("Pause panel already exists; preserve manual layout.");
        var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/UI/Fonts/GarageFont.asset");
        var pause = Panel("Pause_Panel", canvas.transform);
        Text("Title_Text", "ПАУЗА", pause.transform, font, new Vector2(0, 250), 60);
        var resume = Button("Btn_Continue", "Продолжить", pause.transform, font, new Vector2(0, 100), new Vector2(620, 100));
        var settingsButton = Button("Btn_Settings", "Настройки", pause.transform, font, new Vector2(0, -30), new Vector2(620, 100));
        var exit = Button("Btn_ExitGarage", "Выход в гараж", pause.transform, font, new Vector2(0, -160), new Vector2(620, 100));
        var settings = Panel("Settings_Panel", canvas.transform);
        Text("Title_Text", "ГРОМКОСТЬ МУЗЫКИ", settings.transform, font, new Vector2(0, 270), 48);
        var sliderObject = Rect("MusicVolume_Slider", settings.transform, new Vector2(0, 130), new Vector2(750, 60));
        var slider = sliderObject.AddComponent<Slider>();
        slider.minValue = 0; slider.maxValue = 1; slider.wholeNumbers = false; slider.value = SettingsService.Get();
        var background = Rect("Background", sliderObject.transform, Vector2.zero, new Vector2(750, 16)).AddComponent<Image>(); background.color = new Color(0.2f, 0.25f, 0.32f);
        var fillArea = Rect("Fill Area", sliderObject.transform, Vector2.zero, new Vector2(710, 16));
        var fill = Rect("Fill", fillArea.transform, Vector2.zero, new Vector2(710, 16)).AddComponent<Image>(); fill.color = new Color(0.3f, 0.65f, 1);
        var handleArea = Rect("Handle Slide Area", sliderObject.transform, Vector2.zero, new Vector2(710, 60));
        var handle = Rect("Handle", handleArea.transform, Vector2.zero, new Vector2(36, 60)).AddComponent<Image>(); handle.color = Color.white;
        slider.fillRect = fill.rectTransform; slider.handleRect = handle.rectTransform; slider.targetGraphic = handle;
        ConfigureVolumeSlider(slider);
        var prev = Button("Btn_MusicPrev", "<", settings.transform, font, new Vector2(-490, -30), new Vector2(100, 100));
        var next = Button("Btn_MusicNext", ">", settings.transform, font, new Vector2(490, -30), new Vector2(100, 100));
        var track = Text("TrackName_Text", "Добавьте музыкальные треки", settings.transform, font, new Vector2(0, -30), 30);
        track.rectTransform.sizeDelta = new Vector2(820, 90);
        var back = Button("Btn_SettingsBack", "Назад", settings.transform, font, new Vector2(0, -200), new Vector2(620, 100));
        var controller = canvas.gameObject.AddComponent<PauseMenuController>();
        var session = roots.Select(r => r.GetComponent<RaceSessionManager>()).First(s => s != null);
        var serialized = new SerializedObject(controller);
        Set(serialized, "session", session); Set(serialized, "pausePanel", pause); Set(serialized, "settingsPanel", settings);
        Set(serialized, "musicVolumeSlider", slider); Set(serialized, "trackNameText", track);
        Set(serialized, "musicManagerPrefab", AssetDatabase.LoadAssetAtPath<MusicManager>("Assets/Prefabs/Systems/MusicManager.prefab"));
        serialized.ApplyModifiedPropertiesWithoutUndo();
        UnityEventTools.AddPersistentListener(resume.onClick, controller.Continue);
        UnityEventTools.AddPersistentListener(settingsButton.onClick, controller.OpenSettings);
        UnityEventTools.AddPersistentListener(exit.onClick, controller.ExitGarage);
        UnityEventTools.AddPersistentListener(prev.onClick, controller.PrevTrack);
        UnityEventTools.AddPersistentListener(next.onClick, controller.NextTrack);
        UnityEventTools.AddPersistentListener(back.onClick, controller.SettingsBack);
        UnityEventTools.AddPersistentListener(slider.onValueChanged, controller.SetMusicVolume);
        pause.SetActive(false); settings.SetActive(false);
        EditorSceneManager.SaveScene(scene); AssetDatabase.SaveAssets();
        Debug.Log("XRACE_PAUSE_SETUP_OK");
    }

    [MenuItem("Xrace/Repair Music Volume Slider")]
    public static void RepairVolumeSlider()
    {
        if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        var scene = EditorSceneManager.OpenScene("Assets/Scenes/Touge01.unity");
        var controller = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<PauseMenuController>(true)).FirstOrDefault();
        if (controller == null) throw new InvalidOperationException("Touge01 has no PauseMenuController.");
        var slider = new SerializedObject(controller).FindProperty("musicVolumeSlider").objectReferenceValue as Slider;
        if (slider == null) throw new InvalidOperationException("PauseMenuController has no volume slider assigned.");
        ConfigureVolumeSlider(slider);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("XRACE_VOLUME_SLIDER_REPAIR_OK: fixed fill/handle anchors and full-height pointer hit area; preserved slider placement, size and colours.");
    }

    private static void ConfigureVolumeSlider(Slider slider)
    {
        if (slider.fillRect == null || slider.handleRect == null)
            throw new InvalidOperationException("Volume slider needs Fill Rect and Handle Rect.");
        var sliderRect = (RectTransform)slider.transform;
        var fillArea = slider.fillRect.parent as RectTransform;
        var handleArea = slider.handleRect.parent as RectTransform;
        if (fillArea == null || handleArea == null || fillArea == sliderRect || handleArea == sliderRect)
            throw new InvalidOperationException("Volume slider needs separate fill and handle slide areas.");
        // Release driven anchors before editing so Unity saves the repaired base geometry.
        bool wasEnabled = slider.enabled;
        slider.enabled = false;
        // Preserve the root's placement/size, track thickness, handle width and colours.
        float handleWidth = Mathf.Max(1f, slider.handleRect.sizeDelta.x);
        float padding = Mathf.Max(handleWidth, sliderRect.rect.width - handleArea.rect.width);
        float trackHeight = Mathf.Max(1f, fillArea.rect.height);
        fillArea.anchorMin = new Vector2(0, 0.5f);
        fillArea.anchorMax = new Vector2(1, 0.5f);
        fillArea.sizeDelta = new Vector2(-padding, trackHeight);
        fillArea.anchoredPosition = Vector2.zero;
        slider.fillRect.anchorMin = Vector2.zero;
        slider.fillRect.anchorMax = new Vector2(slider.normalizedValue, 1);
        slider.fillRect.sizeDelta = Vector2.zero;
        slider.fillRect.anchoredPosition = Vector2.zero;
        handleArea.anchorMin = Vector2.zero;
        handleArea.anchorMax = Vector2.one;
        handleArea.sizeDelta = new Vector2(-padding, 0);
        handleArea.anchoredPosition = Vector2.zero;
        slider.handleRect.anchorMin = new Vector2(slider.normalizedValue, 0);
        slider.handleRect.anchorMax = new Vector2(slider.normalizedValue, 1);
        slider.handleRect.sizeDelta = new Vector2(handleWidth, 0);
        slider.handleRect.anchoredPosition = Vector2.zero;
        foreach (var image in slider.GetComponentsInChildren<Image>(true))
            image.raycastTarget = image.rectTransform == slider.handleRect;
        var hitArea = slider.GetComponent<Image>();
        if (hitArea == null)
        {
            hitArea = slider.gameObject.AddComponent<Image>();
            hitArea.color = Color.clear;
        }
        hitArea.raycastTarget = true;
        hitArea.canvasRenderer.cullTransparentMesh = false;
        slider.direction = Slider.Direction.LeftToRight;
        slider.minValue = 0;
        slider.maxValue = 1;
        slider.wholeNumbers = false;
        // Arrow keys belong to track switching, never to this selected slider.
        var navigation = slider.navigation;
        navigation.mode = Navigation.Mode.None;
        slider.navigation = navigation;
        if (slider.GetComponent<MusicVolumeSliderCommitter>() == null)
            slider.gameObject.AddComponent<MusicVolumeSliderCommitter>();
        slider.enabled = wasEnabled;
    }
    private static void Set(SerializedObject target, string name, UnityEngine.Object value) => target.FindProperty(name).objectReferenceValue = value;
    private static GameObject Rect(string name, Transform parent, Vector2 position, Vector2 size)
    {
        var obj = new GameObject(name, typeof(RectTransform)); obj.transform.SetParent(parent, false);
        var rect = obj.GetComponent<RectTransform>(); rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f); rect.anchoredPosition = position; rect.sizeDelta = size; return obj;
    }
    private static GameObject Panel(string name, Transform parent)
    {
        var obj = Rect(name, parent, Vector2.zero, Vector2.zero); var rect = obj.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.offsetMin = rect.offsetMax = Vector2.zero;
        obj.AddComponent<Image>().color = new Color(0.025f, 0.035f, 0.055f, 0.96f); return obj;
    }
    private static TextMeshProUGUI Text(string name, string label, Transform parent, TMP_FontAsset font, Vector2 position, float size)
    {
        var text = Rect(name, parent, position, new Vector2(1000, 100)).AddComponent<TextMeshProUGUI>();
        text.font = font; text.text = label; text.fontSize = size; text.alignment = TextAlignmentOptions.Center; text.raycastTarget = false; return text;
    }
    private static Button Button(string name, string label, Transform parent, TMP_FontAsset font, Vector2 position, Vector2 size)
    {
        var obj = Rect(name, parent, position, size); var image = obj.AddComponent<Image>(); image.color = new Color(0.13f, 0.2f, 0.3f);
        var button = obj.AddComponent<Button>(); button.targetGraphic = image;
        var text = Text("Label", label, obj.transform, font, Vector2.zero, 36); text.rectTransform.sizeDelta = size;
        var navigation = button.navigation; navigation.mode = Navigation.Mode.None; button.navigation = navigation; return button;
    }
}
