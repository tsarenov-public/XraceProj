using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class BetaSceneRepair
{
    public static void ApplyAndValidate()
    {
        Apply();
        BetaValidationCheck.Run();
    }
    public static void Apply()
    {
        if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        GarageEditorRepair.RepairGarage();
        var scene = EditorSceneManager.OpenScene("Assets/Scenes/Touge01.unity");
        var canvas = scene.GetRootGameObjects().Select(r => r.GetComponent<PauseMenuController>())
            .First(c => c != null).GetComponent<Canvas>();
        // Keep race menus above the car's own speedometer canvas.
        canvas.sortingOrder = 200;
        var trackLabel = canvas.GetComponentsInChildren<TMP_Text>(true).First(t => t.name == "TrackName_Text");
        trackLabel.text = "Нет треков";
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        Debug.Log("XRACE_BETA_SCENE_REPAIR_OK");
    }
}
