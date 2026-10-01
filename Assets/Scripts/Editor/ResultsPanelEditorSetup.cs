using System;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

// Additive upgrade: existing finish, HUD and pause RectTransforms/design stay intact.
public static class ResultsPanelEditorSetup
{
    public static void RenderPreview()
    {
        if (!Application.isBatchMode) throw new InvalidOperationException("Results preview is batch-only; preserve the open scene layout.");
        var scene = EditorSceneManager.OpenScene("Assets/Scenes/Touge01.unity");
        var controller = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<ResultsPanelController>(true)).Single();
        var serialized = new SerializedObject(controller);
        var panel = (GameObject)serialized.FindProperty("resultsPanel").objectReferenceValue;
        var current = (TMP_Text)serialized.FindProperty("currentResultText").objectReferenceValue;
        var record = (TMP_Text)serialized.FindProperty("currentRecordText").objectReferenceValue;
        var status = (TMP_Text)serialized.FindProperty("statusText").objectReferenceValue;
        var content = (RectTransform)serialized.FindProperty("historyContent").objectReferenceValue;
        var template = (GameObject)serialized.FindProperty("rowTemplate").objectReferenceValue;
        panel.SetActive(true);
        current.text = "ТЕКУЩИЙ ЗАЕЗД · Место: 1\nToyota AE86\n01:23.456  ·  Дрифт: 12450";
        record.gameObject.SetActive(true); status.text = string.Empty;
        for (int i = 0; i < 8; i++)
        {
            var row = UnityEngine.Object.Instantiate(template, content, false);
            row.GetComponentInChildren<TMP_Text>(true).text = (i + 2) + ". " + (i % 2 == 0 ? "Toyota AE86" : "RMCar26") +
                "\n01:35.789  ·  Дрифт: " + (12000 - i * 1000);
            row.SetActive(true);
        }
        var canvas = controller.GetComponent<Canvas>();
        foreach (var transform in panel.GetComponentsInChildren<Transform>(true)) transform.gameObject.layer = 5;
        var camera = new GameObject("ResultsPreviewCamera", typeof(Camera)).GetComponent<Camera>();
        camera.transform.position = new Vector3(0, 0, -1000); camera.cullingMask = 1 << 5;
        camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = Color.black;
        var target = new RenderTexture(1280, 720, 24); var image = new Texture2D(1280, 720, TextureFormat.RGB24, false);
        try
        {
            canvas.gameObject.layer = 5; canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = camera;
            canvas.planeDistance = 1; camera.targetTexture = target;
            Canvas.ForceUpdateCanvases(); LayoutRebuilder.ForceRebuildLayoutImmediate(content);
            RenderPipeline.SubmitRenderRequest(camera, new UniversalRenderPipeline.SingleCameraRequest { destination = target });
            var previous = RenderTexture.active;
            try
            {
                RenderTexture.active = target; image.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0); image.Apply();
                System.IO.Directory.CreateDirectory("Logs");
                System.IO.File.WriteAllBytes("Logs/ResultsPreview.png", image.EncodeToPNG());
            }
            finally { RenderTexture.active = previous; }
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(image); UnityEngine.Object.DestroyImmediate(target);
            UnityEngine.Object.DestroyImmediate(camera.gameObject);
        }
        // No scene or history is saved by the rendering probe.
        Debug.Log("XRACE_RESULTS_PREVIEW_OK");
    }

    [MenuItem("Xrace/Add Finish Results Panel")]
    public static void Apply()
    {
        if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        var scene = EditorSceneManager.OpenScene("Assets/Scenes/Touge01.unity");
        var roots = scene.GetRootGameObjects();
        var finish = roots.SelectMany(r => r.GetComponentsInChildren<FinishPanelController>(true)).Single();
        var canvas = finish.GetComponent<Canvas>();
        if (canvas == null) throw new InvalidOperationException("Finish controller must be on the active Canvas.");
        var existing = canvas.GetComponentsInChildren<ResultsPanelController>(true);
        if (existing.Length != 0)
        {
            if (existing.Length != 1 || !canvas.GetComponentsInChildren<Transform>(true).Any(t => t.name == "Results_Panel"))
                throw new InvalidOperationException("Existing results setup is incomplete; preserve it and repair references in Inspector.");
            Debug.Log("XRACE_RESULTS_SETUP_OK: existing results panel preserved.");
            return;
        }
        if (canvas.GetComponentsInChildren<Transform>(true).Any(t => t.name == "Results_Panel" || t.name == "Btn_Results"))
            throw new InvalidOperationException("Named results UI already exists; preserve manual layout.");
        var finishSerialized = new SerializedObject(finish);
        var finishPanel = (GameObject)finishSerialized.FindProperty("finishPanel").objectReferenceValue;
        if (finishPanel == null) throw new InvalidOperationException("Finish panel reference is missing.");
        var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/UI/Fonts/GarageFont.asset");
        if (font == null) throw new InvalidOperationException("Garage font asset is missing.");
        if (canvas.GetComponent<GraphicRaycaster>() == null) canvas.gameObject.AddComponent<GraphicRaycaster>();

        var resultsButton = Button("Btn_Results", "РЕЗУЛЬТАТЫ", finishPanel.transform, font,
            new Vector2(0, -325), new Vector2(520, 90));
        var panel = Rect("Results_Panel", canvas.transform, Vector2.zero, Vector2.zero);
        var panelRect = panel.GetComponent<RectTransform>();
        panelRect.anchorMin = Vector2.zero; panelRect.anchorMax = Vector2.one;
        panelRect.offsetMin = panelRect.offsetMax = Vector2.zero;
        panel.AddComponent<Image>().color = new Color(0.025f, 0.035f, 0.055f, 0.98f);
        Text("ResultsTitle_Text", "РЕЗУЛЬТАТЫ ЗАЕЗДОВ", panel.transform, font,
            new Vector2(0, 425), new Vector2(1300, 75), 44);
        var current = Rect("CurrentResult_Panel", panel.transform, new Vector2(0, 285), new Vector2(1200, 160));
        current.AddComponent<Image>().color = new Color(0.1f, 0.22f, 0.36f, 1);
        var record = Text("CurrentRecord_Text", "НОВЫЙ РЕКОРД", current.transform, font,
            new Vector2(0, 55), new Vector2(1140, 42), 28);
        record.color = new Color(1, 0.8f, 0.25f); record.gameObject.SetActive(false);
        var currentText = Text("CurrentResult_Text", "ТЕКУЩИЙ ЗАЕЗД", current.transform, font,
            new Vector2(0, -20), new Vector2(1140, 108), 26);
        Text("HistoryTitle_Text", "ИСТОРИЯ · ПО ОЧКАМ ДРИФТА", panel.transform, font,
            new Vector2(0, 180), new Vector2(1200, 40), 26);

        var scrollObject = Rect("History_Scroll", panel.transform, new Vector2(0, -55), new Vector2(1200, 430));
        var scroll = scrollObject.AddComponent<ScrollRect>();
        scroll.horizontal = false; scroll.vertical = true; scroll.movementType = ScrollRect.MovementType.Clamped;
        scroll.scrollSensitivity = 45; scroll.inertia = true;
        var viewportObject = Rect("Viewport", scrollObject.transform, Vector2.zero, Vector2.zero);
        Stretch(viewportObject.GetComponent<RectTransform>());
        viewportObject.AddComponent<Image>().color = new Color(0.07f, 0.09f, 0.13f, 1);
        viewportObject.AddComponent<RectMask2D>();
        var contentObject = Rect("Content", viewportObject.transform, Vector2.zero, Vector2.zero);
        var content = contentObject.GetComponent<RectTransform>();
        content.anchorMin = new Vector2(0, 1); content.anchorMax = Vector2.one; content.pivot = new Vector2(0.5f, 1);
        content.anchoredPosition = Vector2.zero; content.sizeDelta = Vector2.zero;
        var layout = contentObject.AddComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(10, 10, 10, 10); layout.spacing = 8;
        layout.childControlWidth = layout.childControlHeight = true;
        layout.childForceExpandWidth = true; layout.childForceExpandHeight = false;
        var fitter = contentObject.AddComponent<ContentSizeFitter>();
        fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained; fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        scroll.viewport = viewportObject.GetComponent<RectTransform>(); scroll.content = content;
        var template = Rect("ResultRow_Template", content, Vector2.zero, new Vector2(1180, 86));
        template.AddComponent<Image>().color = new Color(0.12f, 0.15f, 0.2f, 1);
        var element = template.AddComponent<LayoutElement>(); element.minHeight = element.preferredHeight = 86;
        var rowText = Text("ResultRow_Text", "1. Машина\n00:00.000 · Дрифт: 0", template.transform, font,
            Vector2.zero, Vector2.zero, 26);
        Stretch(rowText.rectTransform); rowText.rectTransform.offsetMin = new Vector2(24, 6);
        rowText.rectTransform.offsetMax = new Vector2(-24, -6);
        rowText.alignment = TextAlignmentOptions.MidlineLeft; template.SetActive(false);
        var status = Text("ResultsStatus_Text", string.Empty, panel.transform, font,
            new Vector2(0, -320), new Vector2(1300, 65), 24);
        var back = Button("Btn_ResultsBack", "НАЗАД", panel.transform, font,
            new Vector2(0, -425), new Vector2(520, 90));

        var controller = canvas.gameObject.AddComponent<ResultsPanelController>();
        var serialized = new SerializedObject(controller);
        Set(serialized, "finishController", finish); Set(serialized, "finishPanel", finishPanel); Set(serialized, "resultsPanel", panel);
        Set(serialized, "currentResultPanel", current); Set(serialized, "currentResultText", currentText); Set(serialized, "currentRecordText", record);
        Set(serialized, "statusText", status); Set(serialized, "historyScroll", scroll); Set(serialized, "historyContent", content); Set(serialized, "rowTemplate", template);
        serialized.ApplyModifiedPropertiesWithoutUndo();
        Set(finishSerialized, "resultsButton", resultsButton); Set(finishSerialized, "resultsController", controller);
        finishSerialized.ApplyModifiedPropertiesWithoutUndo();
        UnityEventTools.AddPersistentListener(resultsButton.onClick, finish.OpenResults);
        UnityEventTools.AddPersistentListener(back.onClick, controller.Back);
        panel.SetActive(false);
        EditorSceneManager.SaveScene(scene); AssetDatabase.SaveAssets();
        Debug.Log("XRACE_RESULTS_SETUP_OK: finish layout preserved, editable current result and sorted scrolling history added.");
    }

    private static void Set(SerializedObject target, string name, UnityEngine.Object value) => target.FindProperty(name).objectReferenceValue = value;
    private static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.offsetMin = rect.offsetMax = Vector2.zero;
    }
    private static GameObject Rect(string name, Transform parent, Vector2 position, Vector2 size)
    {
        var obj = new GameObject(name, typeof(RectTransform)); obj.transform.SetParent(parent, false);
        var rect = obj.GetComponent<RectTransform>(); rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position; rect.sizeDelta = size; return obj;
    }
    private static TextMeshProUGUI Text(string name, string value, Transform parent, TMP_FontAsset font, Vector2 position, Vector2 size, float fontSize)
    {
        var text = Rect(name, parent, position, size).AddComponent<TextMeshProUGUI>();
        text.font = font; text.text = value; text.fontSize = fontSize; text.alignment = TextAlignmentOptions.Center;
        text.raycastTarget = false; text.richText = false; text.overflowMode = TextOverflowModes.Ellipsis;
        return text;
    }
    private static Button Button(string name, string value, Transform parent, TMP_FontAsset font, Vector2 position, Vector2 size)
    {
        var obj = Rect(name, parent, position, size);
        var image = obj.AddComponent<Image>(); image.color = new Color(0.13f, 0.2f, 0.3f);
        var button = obj.AddComponent<Button>(); button.targetGraphic = image;
        var label = Text("Label", value, obj.transform, font, Vector2.zero, Vector2.zero, 32); Stretch(label.rectTransform);
        var navigation = button.navigation; navigation.mode = Navigation.Mode.None; button.navigation = navigation;
        return button;
    }
}
