using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

// Repairs only input bindings; keep the user's positions, fonts and car list intact.
public static class GarageEditorRepair
{
    [MenuItem("Xrace/Repair Garage Controls")]
    public static void RepairGarage()
    {
        if (EditorApplication.isPlaying)
            throw new InvalidOperationException("Stop Play Mode before repairing the garage.");
        if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        var scene = EditorSceneManager.OpenScene("Assets/Scenes/Garage.unity");
        GarageManager manager = null;
        Button raceButton = null;
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            foreach (GarageManager candidate in root.GetComponentsInChildren<GarageManager>(true))
            {
                if (manager != null) throw new InvalidOperationException("Garage contains multiple GarageManager components.");
                manager = candidate;
            }
            foreach (Button button in root.GetComponentsInChildren<Button>(true))
            {
                if (button.name != "Btn_Race" && button.name != "Btn_PrevCar" && button.name != "Btn_NextCar") continue;
                button.navigation = new Navigation { mode = Navigation.Mode.None };
                if (button.name == "Btn_Race")
                {
                    if (raceButton != null) throw new InvalidOperationException("Garage contains multiple Btn_Race buttons.");
                    raceButton = button;
                }
            }
        }
        if (manager == null || raceButton == null)
            throw new InvalidOperationException("GarageManager or Btn_Race missing; preserve scene and repair those references manually.");
        var serialized = new SerializedObject(manager);
        serialized.FindProperty("raceButton").objectReferenceValue = raceButton;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("XRACE_GARAGE_REPAIR_OK: race button connected and car-selection arrows no longer move UI focus.");
    }
}
