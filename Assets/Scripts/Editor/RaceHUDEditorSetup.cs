using System;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class RaceHUDEditorSetup
{
    [MenuItem("Xrace/Upgrade Race HUD")]
    public static void Apply()
    {
        if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        var scene = EditorSceneManager.OpenScene("Assets/Scenes/Touge01.unity");
        var roots = scene.GetRootGameObjects();
        var texts = roots.SelectMany(r => r.GetComponentsInChildren<TMP_Text>(true)).ToArray();
        var time = texts.First(t => t.name == "Time_Text");
        var score = texts.First(t => t.name == "Score_Text");
        var multiplier = texts.First(t => t.name == "Multiplier_Text");
        var panel = time.transform.parent.gameObject;
        var canvas = panel.GetComponentInParent<Canvas>();
        if (canvas == null || panel.name != "HUD_Panel") throw new InvalidOperationException("HUD hierarchy missing.");
        var old = panel.GetComponent<RaceHud>();
        if (old != null) UnityEngine.Object.DestroyImmediate(old);
        var controller = canvas.GetComponent<RaceHUDController>();
        if (controller == null) controller = canvas.gameObject.AddComponent<RaceHUDController>();
        var session = roots.Select(r => r.GetComponent<RaceSessionManager>()).First(s => s != null);
        var serialized = new SerializedObject(controller);
        Set(serialized,"session",session);Set(serialized,"timer",session.GetComponent<RaceTimer>());
        Set(serialized,"scorer",session.GetComponent<DriftScorer>());Set(serialized,"hudPanel",panel);
        Set(serialized,"timeText",time);Set(serialized,"scoreText",score);Set(serialized,"multiplierText",multiplier);
        serialized.ApplyModifiedPropertiesWithoutUndo();
        panel.SetActive(true);
        time.text="00:00.000";score.text="Дрифт: 0";multiplier.text="x1";
        foreach(var text in new[]{time,score,multiplier})text.raycastTarget=false;
        var group=panel.GetComponent<CanvasGroup>();if(group==null)group=panel.AddComponent<CanvasGroup>();
        group.blocksRaycasts=false;group.interactable=false;
        EditorSceneManager.SaveScene(scene);AssetDatabase.SaveAssets();
        Debug.Log("XRACE_HUD_SETUP_OK");
    }
    public static void ApplyAndCheck(){Apply();RaceCoreSmokeCheck.Run();}
    private static void Set(SerializedObject target,string name,UnityEngine.Object value)=>target.FindProperty(name).objectReferenceValue=value;
}
