using System;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public static class FinishPanelEditorSetup
{
    public static void RenderPreview()
    {
        // This helper changes the open scene temporarily; batch exit discards it.
        if (!Application.isBatchMode) throw new InvalidOperationException("RenderPreview is batch-only; preserve the open scene layout.");
        var scene=EditorSceneManager.OpenScene("Assets/Scenes/Touge01.unity");
        var roots=scene.GetRootGameObjects();var canvas=roots.Select(r=>r.GetComponent<Canvas>()).First(c=>c!=null);
        var texts=canvas.GetComponentsInChildren<TMP_Text>(true);
        var time=texts.First(t=>t.name=="FinishTime_Text");var panel=time.transform.parent.gameObject;
        panel.SetActive(true);time.text="01:23.456";texts.First(t=>t.name=="FinishScore_Text").text="Дрифт: 12450";texts.First(t=>t.name=="Record_Text").gameObject.SetActive(true);
        foreach(var transform in panel.GetComponentsInChildren<Transform>(true))transform.gameObject.layer=5;
        var camera=new GameObject("FinishPreviewCamera",typeof(Camera)).GetComponent<Camera>();camera.transform.position=new Vector3(0,0,-1000);camera.cullingMask=1<<5;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=Color.black;
        var target=new RenderTexture(1280,720,24);var image=new Texture2D(1280,720,TextureFormat.RGB24,false);
        canvas.gameObject.layer=5;canvas.renderMode=RenderMode.ScreenSpaceCamera;canvas.worldCamera=camera;canvas.planeDistance=1;camera.targetTexture=target;
        Canvas.ForceUpdateCanvases();RenderPipeline.SubmitRenderRequest(camera,new UniversalRenderPipeline.SingleCameraRequest{destination=target});
        var previous=RenderTexture.active;RenderTexture.active=target;image.ReadPixels(new Rect(0,0,1280,720),0,0);image.Apply();
        System.IO.File.WriteAllBytes("Logs/FinishPreview.png",image.EncodeToPNG());RenderTexture.active=previous;
        UnityEngine.Object.DestroyImmediate(image);UnityEngine.Object.DestroyImmediate(target);UnityEngine.Object.DestroyImmediate(camera.gameObject);
        // Preview-only changes are deliberately not saved to the scene.
        Debug.Log("XRACE_FINISH_PREVIEW_OK");
    }
    [MenuItem("Xrace/Create Finish Panel")]
    public static void Apply()
    {
        if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        var scene=EditorSceneManager.OpenScene("Assets/Scenes/Touge01.unity");
        var roots=scene.GetRootGameObjects();
        var canvas=roots.Select(r=>r.GetComponent<Canvas>()).First(c=>c!=null);
        if(canvas.GetComponentsInChildren<Transform>(true).Any(t=>t.name=="Finish_Panel"))
            throw new InvalidOperationException("Finish panel already exists; preserve manual layout.");
        if(canvas.GetComponent<GraphicRaycaster>()==null)canvas.gameObject.AddComponent<GraphicRaycaster>();
        if(!roots.SelectMany(r=>r.GetComponentsInChildren<EventSystem>(true)).Any())
        {
            var events=new GameObject("EventSystem",typeof(EventSystem),typeof(InputSystemUIInputModule));
            events.GetComponent<InputSystemUIInputModule>().AssignDefaultActions();
        }
        var panel=new GameObject("Finish_Panel",typeof(RectTransform),typeof(Image));
        panel.transform.SetParent(canvas.transform,false);
        var rect=panel.GetComponent<RectTransform>();rect.anchorMin=Vector2.zero;rect.anchorMax=Vector2.one;rect.offsetMin=rect.offsetMax=Vector2.zero;
        panel.GetComponent<Image>().color=new Color(0.025f,0.035f,0.055f,0.94f);
        var font=AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/UI/Fonts/GarageFont.asset");
        var time=Text("FinishTime_Text","00:00.000",panel.transform,font,new Vector2(0,150),new Vector2(900,110),72);
        var score=Text("FinishScore_Text","Дрифт: 0",panel.transform,font,new Vector2(0,35),new Vector2(1100,100),60);
        var record=Text("Record_Text","НОВЫЙ РЕКОРД",panel.transform,font,new Vector2(0,285),new Vector2(1100,80),44);
        record.color=new Color(1,0.8f,0.25f);record.gameObject.SetActive(false);
        var status=Text("SaveStatus_Text","",panel.transform,font,new Vector2(0,-65),new Vector2(1100,60),28);
        var garage=Button("Btn_Garage","В ГАРАЖ",panel.transform,font,new Vector2(-230,-190));
        var retry=Button("Btn_Retry","ИГРАТЬ",panel.transform,font,new Vector2(230,-190));
        var controller=canvas.gameObject.AddComponent<FinishPanelController>();
        var session=roots.Select(r=>r.GetComponent<RaceSessionManager>()).First(s=>s!=null);
        var serialized=new SerializedObject(controller);
        Set(serialized,"session",session);Set(serialized,"timer",session.GetComponent<RaceTimer>());Set(serialized,"scorer",session.GetComponent<DriftScorer>());
        Set(serialized,"fallbackCar",AssetDatabase.LoadAssetAtPath<CarDefinition>("Assets/ScriptableObjects/Cars/ae86.asset"));
        Set(serialized,"finishPanel",panel);Set(serialized,"finishTimeText",time);Set(serialized,"finishScoreText",score);Set(serialized,"recordText",record);Set(serialized,"saveStatusText",status);
        Set(serialized,"garageButton",garage);Set(serialized,"retryButton",retry);serialized.ApplyModifiedPropertiesWithoutUndo();
        UnityEventTools.AddPersistentListener(garage.onClick,controller.GoGarage);UnityEventTools.AddPersistentListener(retry.onClick,controller.Retry);
        panel.SetActive(false);EditorSceneManager.SaveScene(scene);AssetDatabase.SaveAssets();
        Debug.Log("XRACE_FINISH_SETUP_OK");
    }
    private static void Set(SerializedObject target,string name,UnityEngine.Object value)=>target.FindProperty(name).objectReferenceValue=value;
    private static TextMeshProUGUI Text(string name,string value,Transform parent,TMP_FontAsset font,Vector2 position,Vector2 size,float fontSize)
    {
        var text=new GameObject(name,typeof(RectTransform),typeof(TextMeshProUGUI)).GetComponent<TextMeshProUGUI>();text.transform.SetParent(parent,false);
        text.font=font;text.text=value;text.fontSize=fontSize;text.alignment=TextAlignmentOptions.Center;text.raycastTarget=false;
        var rect=text.rectTransform;rect.anchorMin=rect.anchorMax=rect.pivot=new Vector2(0.5f,0.5f);rect.anchoredPosition=position;rect.sizeDelta=size;return text;
    }
    private static Button Button(string name,string label,Transform parent,TMP_FontAsset font,Vector2 position)
    {
        var button=new GameObject(name,typeof(RectTransform),typeof(Image),typeof(Button)).GetComponent<Button>();button.transform.SetParent(parent,false);
        var rect=button.GetComponent<RectTransform>();rect.anchorMin=rect.anchorMax=rect.pivot=new Vector2(0.5f,0.5f);rect.anchoredPosition=position;rect.sizeDelta=new Vector2(380,100);
        button.GetComponent<Image>().color=new Color(0.13f,0.2f,0.3f);button.targetGraphic=button.GetComponent<Image>();
        var text=Text("Label",label,button.transform,font,Vector2.zero,new Vector2(380,100),36);return button;
    }
}
