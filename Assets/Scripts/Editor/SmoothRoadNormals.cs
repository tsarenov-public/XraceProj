using UnityEditor;
using UnityEngine;

public static class SmoothRoadNormals
{
    [MenuItem("Tools/Smooth ER Road Mesh Normals")]
    static void Smooth()
    {
        Mesh mesh = null;
        string where = null;

        var guids = AssetDatabase.FindAssets("ER Road Mesh t:Mesh");
        if (guids.Length > 0)
        {
            var path = AssetDatabase.GUIDToAssetPath(guids[0]);
            mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            where = path;
        }

        if (mesh == null && Selection.activeGameObject != null)
        {
            var mf = Selection.activeGameObject.GetComponent<MeshFilter>();
            if (mf != null && mf.sharedMesh != null)
            {
                mesh = mf.sharedMesh;
                where = "MeshFilter объекта " + Selection.activeGameObject.name;
            }
        }

        if (mesh == null)
        {
            Debug.LogError("Меш не найден: ни ассета 'ER Road Mesh', ни MeshFilter у выделенного объекта.");
            return;
        }

        mesh.RecalculateNormals();
        EditorUtility.SetDirty(mesh);
        if (guids.Length > 0) AssetDatabase.SaveAssets();
        Debug.Log("Нормали сглажены: " + where);
    }
}