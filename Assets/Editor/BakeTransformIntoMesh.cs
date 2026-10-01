using UnityEngine;
using UnityEditor;

public static class BakeTransformIntoMesh
{
    [MenuItem("Tools/Bake Transform Into Mesh")]
    static void Bake()
    {
        GameObject go = Selection.activeGameObject;
        if (go == null || go.GetComponent<MeshFilter>() == null)
        {
            Debug.LogWarning("Сначала выдели объект дерева на сцене");
            return;
        }

        Transform t = go.transform;
        Matrix4x4 bake = Matrix4x4.TRS(t.localPosition, t.localRotation, t.localScale);
        if (bake == Matrix4x4.identity)
        {
            Debug.Log("Трансформ уже единичный — запекать нечего");
            return;
        }

        Mesh src = go.GetComponent<MeshFilter>().sharedMesh;
        Mesh dst = Object.Instantiate(src);
        dst.name = src.name + "_baked";

        // Вершины — с переносом
        Vector3[] v = dst.vertices;
        for (int i = 0; i < v.Length; i++) v[i] = bake.MultiplyPoint(v[i]);
        dst.vertices = v;

        // Нормали — только поворот+масштаб
        Vector3[] n = dst.normals;
        for (int i = 0; i < n.Length; i++) n[i] = bake.MultiplyVector(n[i]);
        dst.normals = n;

        // Тангенты — только поворот+масштаб
        Vector4[] tg = dst.tangents;
        for (int i = 0; i < tg.Length; i++)
        {
            Vector3 xyz = bake.MultiplyVector(new Vector3(tg[i].x, tg[i].y, tg[i].z));
            tg[i] = new Vector4(xyz.x, xyz.y, xyz.z, tg[i].w);
        }
        dst.tangents = tg;

        dst.RecalculateBounds();

        string path = "Assets/Prefabs/trees/" + dst.name + ".asset";
        AssetDatabase.CreateAsset(dst, path);
        AssetDatabase.SaveAssets();

        go.GetComponent<MeshFilter>().sharedMesh = dst;
        t.localPosition = Vector3.zero;
        t.localRotation = Quaternion.identity;
        t.localScale = Vector3.one;

        Debug.Log("Готово: " + path);
    }
}