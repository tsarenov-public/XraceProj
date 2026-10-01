using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// Temporary scene preview lets the artist arrange the camera, CarSpot and lighting without Play Mode.
[InitializeOnLoad]
[CustomEditor(typeof(GarageManager))]
public sealed class GarageManagerEditor : Editor
{
    private const string PreviewName = "CarPreview_Editor";
    private int previewIndex;

    static GarageManagerEditor()
    {
        EditorApplication.playModeStateChanged += state =>
        {
            if (state == PlayModeStateChange.ExitingEditMode) RemoveAll();
        };
        EditorSceneManager.sceneSaving += (scene, path) => RemoveInScene(scene);
        EditorSceneManager.sceneClosing += (scene, removingScene) => RemoveInScene(scene);
        AssemblyReloadEvents.beforeAssemblyReload += RemoveAll;
        EditorApplication.quitting += RemoveAll;
    }

    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        EditorGUILayout.Space();
        EditorGUILayout.HelpBox("Превью ниже временное: двигайте CarSpot, камеру и свет вне Play Mode. Оно не сохраняется в сцену и удаляется перед запуском игры.", MessageType.Info);
        using (new EditorGUI.DisabledScope(EditorApplication.isPlayingOrWillChangePlaymode))
        {
            serializedObject.Update();
            SerializedProperty cars = serializedObject.FindProperty("cars");
            if (cars.arraySize > 0)
            {
                var labels = new string[cars.arraySize];
                for (int i = 0; i < labels.Length; i++)
                {
                    var car = cars.GetArrayElementAtIndex(i).objectReferenceValue as CarDefinition;
                    labels[i] = car != null ? car.displayName : "(не назначена)";
                }
                previewIndex = Mathf.Clamp(previewIndex, 0, labels.Length - 1);
                previewIndex = EditorGUILayout.Popup("Машина для оформления", previewIndex, labels);
                if (GUILayout.Button("Показать превью в сцене")) Show(cars);
            }
            if (GUILayout.Button("Убрать превью")) Remove(GetSpot((GarageManager)target));
        }
    }

    private void Show(SerializedProperty cars)
    {
        var car = cars.GetArrayElementAtIndex(previewIndex).objectReferenceValue as CarDefinition;
        ShowForEditing((GarageManager)target, car);
        SceneView.RepaintAll();
    }

    internal static GameObject ShowForEditing(GarageManager manager, CarDefinition car)
    {
        Transform spot = GetSpot(manager);
        if (spot == null || car == null || car.carPrefab == null)
        {
            Debug.LogWarning("Assign CarSpot and a CarDefinition with Car Prefab before showing the editor preview.", manager);
            return null;
        }
        Remove(spot);
        return GaragePreviewBuilder.Create(car.carPrefab, spot, PreviewName, HideFlags.DontSaveInEditor | HideFlags.DontSaveInBuild);
    }

    private static Transform GetSpot(GarageManager manager)
        => new SerializedObject(manager).FindProperty("carSpot").objectReferenceValue as Transform;

    private static void Remove(Transform spot)
    {
        if (spot == null) return;
        for (int i = spot.childCount - 1; i >= 0; i--)
            if (spot.GetChild(i).name == PreviewName) Object.DestroyImmediate(spot.GetChild(i).gameObject);
    }

    private static void RemoveInScene(Scene scene)
    {
        if (!scene.IsValid() || !scene.isLoaded) return;
        foreach (GameObject root in scene.GetRootGameObjects())
            foreach (GarageManager manager in root.GetComponentsInChildren<GarageManager>(true)) Remove(GetSpot(manager));
    }

    private static void RemoveAll()
    {
        for (int i = 0; i < SceneManager.sceneCount; i++) RemoveInScene(SceneManager.GetSceneAt(i));
    }
}
