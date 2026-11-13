using System.IO;
using System.Text;
using UnityEditor.SceneManagement;
using UnityEngine;
public static class RaceSceneProbe
{
    public static void Run()
    {
        var scene = EditorSceneManager.OpenScene("Assets/Scenes/Touge01.unity");
        var output = new StringBuilder();
        foreach (var root in scene.GetRootGameObjects())
        {
            foreach (var body in root.GetComponentsInChildren<Rigidbody>(true))
                Debug.Log("RACE_BODY " + body.name + " " + body.position);
            foreach (var filter in root.GetComponentsInChildren<MeshFilter>(true))
            {
                if (filter.sharedMesh == null || filter.sharedMesh.name != "ER Road Mesh") continue;
                Debug.Log("ROAD_MESH " + filter.sharedMesh.vertexCount);
                foreach (var v in filter.sharedMesh.vertices)
                {
                    var p = filter.transform.TransformPoint(v);
                    output.AppendLine(string.Format(System.Globalization.CultureInfo.InvariantCulture, "{0},{1},{2}",p.x,p.y,p.z));
                }
            }
        }
        File.WriteAllText("Logs/RoadVertices.csv", output.ToString());
    }
}
