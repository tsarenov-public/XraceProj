using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

public static class RaceSceneBuilder
{
    [MenuItem("Xrace/Create Beta Race Core")]
    public static void Build()
    {
        if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        var scene = EditorSceneManager.OpenScene("Assets/Scenes/Touge01.unity");
        var roots = scene.GetRootGameObjects();
        if (roots.Any(root => root.name == "RaceRoot")) throw new InvalidOperationException("RaceRoot already exists; preserve manual edits.");
        MeshFilter road = roots.SelectMany(root => root.GetComponentsInChildren<MeshFilter>(true)).First(f => f.sharedMesh != null && f.sharedMesh.name == "ER Road Mesh");
        var vertices = road.sharedMesh.vertices;
        var route = new List<Vector3>();
        for (int i = 0; i + 1 < vertices.Length; i += 2) route.Add(road.transform.TransformPoint((vertices[i] + vertices[i + 1]) * 0.5f));
        Vector3 startPosition = new Vector3(311,152,-156), finishPosition = new Vector3(-372,40,190);
        int startIndex = Nearest(route, startPosition), finishIndex = Nearest(route, finishPosition);
        if (finishIndex < startIndex) { route.Reverse(); startIndex = Nearest(route,startPosition); finishIndex = Nearest(route,finishPosition); }
        route = route.GetRange(startIndex, finishIndex - startIndex + 1);
        Debug.Log("RACE_ROUTE start=" + route[0] + " finish=" + route[route.Count-1]);
        var root = new GameObject("RaceRoot");
        var start = Child("StartSpawn", root.transform);
        start.position = startPosition;
        start.rotation = Quaternion.LookRotation(route[Mathf.Min(8,route.Count-1)] - route[0]);
        var finish = Child("FinishTrigger", root.transform);
        finish.position = finishPosition;
        finish.rotation = Quaternion.LookRotation(route[route.Count-1] - route[Mathf.Max(0,route.Count-9)]);
        var finishBox = finish.gameObject.AddComponent<BoxCollider>();
        finishBox.isTrigger = true;
        finishBox.size = new Vector3(20,5,4);
        var session = root.AddComponent<RaceSessionManager>();
        var timer = root.AddComponent<RaceTimer>();
        var state = root.AddComponent<VehicleDriftStateAdapter>();
        var scorer = root.AddComponent<DriftScorer>();
        var stopper = root.AddComponent<CarStopOnFinish>();
        var spawner = root.AddComponent<CarSpawner>();
        var finishTrigger = finish.gameObject.AddComponent<RaceFinishTrigger>();
        Set(session, "timer",timer,"driftState",state,"scorer",scorer,"stopper",stopper);
        Set(timer,"session",session);
        Set(scorer,"session",session,"driftState",state);
        Set(finishTrigger,"session",session);
        var fallback = roots.SelectMany(r => r.GetComponentsInChildren<Rigidbody>(true)).First(b => b.GetComponent<WheelController>() != null);
        Set(spawner,"startSpawn",start,"fallbackCar",fallback,"roadSurface",road.GetComponent<Collider>(),"session",session);
        var existingVehicles = new HashSet<GameObject> { fallback.transform.root.gameObject };
        foreach(var camera in roots.SelectMany(r => r.GetComponentsInChildren<Camera>(true))) existingVehicles.Add(camera.gameObject);
        var serializedSpawner = new SerializedObject(spawner);
        var objects = serializedSpawner.FindProperty("sceneVehicleObjects");
        objects.arraySize = existingVehicles.Count;
        int objectIndex = 0;
        foreach(var existing in existingVehicles) objects.GetArrayElementAtIndex(objectIndex++).objectReferenceValue = existing;
        serializedSpawner.ApplyModifiedPropertiesWithoutUndo();
        var zonesRoot = Child("DriftZones",root.transform);
        var ranges = new List<Vector2Int>();
        int begin = -1, lastBend = -1;
        for (int i = 10; i < route.Count - 10; i++)
        {
            var before = Vector3.ProjectOnPlane(route[i] - route[i-10], Vector3.up);
            var after = Vector3.ProjectOnPlane(route[i+10] - route[i], Vector3.up);
            bool bend = Vector3.Angle(before, after) > 16;
            if (bend) { if (begin < 0) begin = i; lastBend = i; }
            else if (begin >= 0 && i - lastBend > 10)
            { if(lastBend - begin >= 6) ranges.Add(new Vector2Int(Mathf.Max(1,begin-5), Mathf.Min(route.Count-2,lastBend+5))); begin = -1; }
        }
        if (begin >= 0 && lastBend-begin >= 6) ranges.Add(new Vector2Int(begin-5,Mathf.Min(route.Count-2,lastBend+5)));
        var boxes = new List<BoxCollider>();
        var report = new System.Text.StringBuilder("Generated from road mesh. Verify every box in Scene View and with a real drive.\n");
        int id = 0;
        foreach (var range in ranges)
        {
            var zoneTransform = Child("DriftZone_" + (++id).ToString("00"), zonesRoot);
            Vector3 midpoint = route[(range.x+range.y)/2];
            Vector3 direction = route[range.y] - route[range.x];
            zoneTransform.position = midpoint;
            zoneTransform.rotation = Quaternion.LookRotation(direction.sqrMagnitude > 1 ? direction : route[range.x+1]-route[range.x]);
            var bounds = new Bounds(zoneTransform.InverseTransformPoint(route[range.x]), Vector3.zero);
            for(int i=range.x+1;i<=range.y;i++) bounds.Encapsulate(zoneTransform.InverseTransformPoint(route[i]));
            var box = zoneTransform.gameObject.AddComponent<BoxCollider>();
            box.isTrigger = true;
            box.center = bounds.center + Vector3.up*1.5f;
            box.size = bounds.size + new Vector3(12,6,4);
            var zone = zoneTransform.gameObject.AddComponent<DriftZone>();
            Set(zone,"session",session,"scorer",scorer);
            var serializedZone = new SerializedObject(zone);
            serializedZone.FindProperty("zoneId").intValue = id;
            serializedZone.ApplyModifiedPropertiesWithoutUndo();
            boxes.Add(box);
            report.AppendLine(zoneTransform.name+" position="+zoneTransform.position+" size="+box.size);
        }
        Physics.SyncTransforms();
        ResolveOverlaps(boxes);
        for(int i=0;i<boxes.Count;i++) for(int j=i+1;j<boxes.Count;j++)
            if(Physics.ComputePenetration(boxes[i],boxes[i].transform.position,boxes[i].transform.rotation,boxes[j],boxes[j].transform.position,boxes[j].transform.rotation,out var direction,out var distance))
                report.AppendLine("CHECK OVERLAP: "+boxes[i].name+" / "+boxes[j].name+" depth="+distance);
        File.WriteAllText("Logs/RaceZonesReport.txt",report.ToString());
        CreateHud(root.transform,timer,scorer);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        Debug.Log("XRACE_RACE_BUILD_OK zones="+id);
    }
    public static void RepairGeneratedOverlapsAndCheck()
    {
        var scene=EditorSceneManager.OpenScene("Assets/Scenes/Touge01.unity");
        var boxes=scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<DriftZone>()).OrderBy(z=>z.ZoneId).Select(z=>z.GetComponent<BoxCollider>()).ToList();
        ResolveOverlaps(boxes);
        EditorSceneManager.SaveScene(scene);
        RaceCoreSmokeCheck.Run();
    }
    public static void UpdateCameraReferencesAndCheckGarage()
    {
        var scene=EditorSceneManager.OpenScene("Assets/Scenes/Touge01.unity");
        var roots=scene.GetRootGameObjects();
        var spawner=roots.Select(r=>r.GetComponent<CarSpawner>()).First(s=>s!=null);
        var serialized=new SerializedObject(spawner);
        var body=(Rigidbody)serialized.FindProperty("fallbackCar").objectReferenceValue;
        var objects=new HashSet<GameObject>{body.transform.root.gameObject};
        foreach(var camera in roots.SelectMany(r=>r.GetComponentsInChildren<Camera>(true))) objects.Add(camera.gameObject);
        var list=serialized.FindProperty("sceneVehicleObjects");list.arraySize=objects.Count;int i=0;
        foreach(var obj in objects)list.GetArrayElementAtIndex(i++).objectReferenceValue=obj;
        serialized.ApplyModifiedPropertiesWithoutUndo();EditorSceneManager.SaveScene(scene);
        GarageSmokeCheck.Run();
    }
    private static void ResolveOverlaps(List<BoxCollider> boxes)
    {
        Physics.SyncTransforms();
        for(int i=0;i<boxes.Count;i++) for(int j=i+1;j<boxes.Count;j++)
        {
            int attempts=0;
            while(Physics.ComputePenetration(boxes[i],boxes[i].transform.position,boxes[i].transform.rotation,boxes[j],boxes[j].transform.position,boxes[j].transform.rotation,out _,out _) && attempts++<40)
            {
                if(boxes[i].size.z<=10 || boxes[j].size.z<=10) throw new InvalidOperationException("Zones require manual overlap correction.");
                boxes[i].size-=new Vector3(0,0,0.5f);boxes[j].size-=new Vector3(0,0,0.5f);
            }
        }
    }
    private static int Nearest(List<Vector3> points, Vector3 position)
    {
        int index=0; float best=float.MaxValue;
        for(int i=0;i<points.Count;i++) { float distance=(points[i]-position).sqrMagnitude; if(distance<best){best=distance;index=i;} }
        return index;
    }
    private static Transform Child(string name, Transform parent)
    { var child=new GameObject(name).transform;child.SetParent(parent,false);return child; }
    private static void Set(UnityEngine.Object target, params object[] fields)
    {
        var serialized=new SerializedObject(target);
        for(int i=0;i<fields.Length;i+=2) serialized.FindProperty((string)fields[i]).objectReferenceValue=(UnityEngine.Object)fields[i+1];
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }
    private static void CreateHud(Transform root, RaceTimer timer, DriftScorer scorer)
    {
        var canvas=new GameObject("RaceCanvas",typeof(RectTransform),typeof(Canvas),typeof(CanvasScaler));
        canvas.GetComponent<Canvas>().renderMode=RenderMode.ScreenSpaceOverlay;
        var scaler=canvas.GetComponent<CanvasScaler>();scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;scaler.referenceResolution=new Vector2(1920,1080);scaler.matchWidthOrHeight=0.5f;
        var panel=new GameObject("HUD_Panel",typeof(RectTransform));panel.transform.SetParent(canvas.transform,false);
        var rect=panel.GetComponent<RectTransform>();rect.anchorMin=rect.anchorMax=new Vector2(0,1);rect.pivot=new Vector2(0,1);rect.anchoredPosition=new Vector2(40,-40);rect.sizeDelta=new Vector2(500,180);
        TMP_FontAsset font=AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/UI/Fonts/GarageFont.asset");
        var names=new[]{"Time_Text","Score_Text","Multiplier_Text"};var texts=new TMP_Text[3];
        for(int i=0;i<3;i++)
        {
            var text=new GameObject(names[i],typeof(RectTransform),typeof(TextMeshProUGUI)).GetComponent<TextMeshProUGUI>();text.transform.SetParent(panel.transform,false);text.font=font;text.fontSize=36;text.raycastTarget=false;text.text=i==0?"00:00.000":i==1?"Дрифт: 0":"x1";
            text.rectTransform.anchorMin=text.rectTransform.anchorMax=new Vector2(0,1);text.rectTransform.pivot=new Vector2(0,1);text.rectTransform.anchoredPosition=new Vector2(0,-55*i);text.rectTransform.sizeDelta=new Vector2(500,55);texts[i]=text;
        }
        Set(canvas.AddComponent<RaceHUDController>(),"session",root.GetComponent<RaceSessionManager>(),"timer",timer,"scorer",scorer,"hudPanel",panel,"timeText",texts[0],"scoreText",texts[1],"multiplierText",texts[2]);
    }
}
