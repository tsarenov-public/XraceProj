using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Checks the actual selected prefab after Garage, rather than the old scene fallback car.
[InitializeOnLoad]
public static class VehicleLightsSmokeCheck
{
    private const string Key = "Xrace.VehicleLightsSmoke";
    private const string LensPath = "Assets/Models/car/toyota-ae86-sprinter-trueno-zenki/materials/";
    private static readonly int Emission = Shader.PropertyToID("_EmissionColor");
    private static readonly int Sentinel = Shader.PropertyToID("_XraceLightsTestSentinel");
    private static readonly List<Material> oldRuntimeMaterials = new List<Material>();
    private static readonly List<Material> sharedMaterials = new List<Material>();
    private static readonly List<Color> originalEmissions = new List<Color>();
    private static int frames;
    private static Material tailLens;
    private static Material headLens;

    static VehicleLightsSmokeCheck()
    {
        EditorApplication.update += Tick;
        Application.logMessageReceived += CaptureError;
    }

    public static void Run()
    {
        if (!Application.isBatchMode) throw new InvalidOperationException("Run this check in a separate batch editor.");
        CarDefinition ae86 = AssetDatabase.LoadAssetAtPath<CarDefinition>("Assets/ScriptableObjects/Cars/ae86.asset");
        BrakeLightController source = ae86.carPrefab.GetComponentInChildren<BrakeLightController>(true);
        Require(source != null, "AE86 prefab has no brake light controller.");
        Scene raceScene = EditorSceneManager.OpenScene("Assets/Scenes/Touge01.unity");
        BrakeLightController fallback = Components<BrakeLightController>(raceScene.GetRootGameObjects()).Single();
        Require(Equal(source.tailColor, fallback.tailColor) && Equal(source.brakeColor, fallback.brakeColor),
            "Selected prefab uses different lamp colors from the configured scene car.");
        Require(source.brakeColor.maxColorComponent <= 1f, "AE86 brake color unexpectedly uses excessive HDR intensity.");
        CarDefinition other = AssetDatabase.LoadAssetAtPath<CarDefinition>("Assets/ScriptableObjects/Cars/rmcar26.asset");
        Require(other.carPrefab.GetComponentInChildren<BrakeLightController>(true) == null,
            "RMCar26 now has a brake controller; extend the state checks for its lens setup.");

        SessionState.SetInt(Key + "Completed", 0);
        SessionState.SetString(Key + "Error", string.Empty);
        SessionState.SetString(Key + "Deadline", DateTime.UtcNow.AddMinutes(4).ToString("O"));
        SessionState.SetInt(Key, 1);
        EditorSceneManager.OpenScene("Assets/Scenes/Garage.unity");
        EditorApplication.EnterPlaymode();
    }

    private static void CaptureError(string message, string stackTrace, LogType type)
    {
        if (SessionState.GetInt(Key, 0) == 0 || type != LogType.Error && type != LogType.Exception && type != LogType.Assert) return;
        if (string.IsNullOrEmpty(SessionState.GetString(Key + "Error", string.Empty)))
            SessionState.SetString(Key + "Error", message + "\n" + stackTrace);
    }

    private static void Tick()
    {
        int step = SessionState.GetInt(Key, 0);
        if (step == 0) return;
        try
        {
            Require(DateTime.UtcNow < DateTime.Parse(SessionState.GetString(Key + "Deadline", string.Empty)), "Vehicle lamp check timed out.");
            Require(string.IsNullOrEmpty(SessionState.GetString(Key + "Error", string.Empty)),
                "Runtime error: " + SessionState.GetString(Key + "Error", string.Empty));
            if (!EditorApplication.isPlaying || EditorApplication.isCompiling || ++frames < 15) return;
            frames = 0;
            Scene scene = SceneManager.GetActiveScene();
            GameObject[] roots = scene.GetRootGameObjects();
            if (step == 1)
            {
                if (scene.name != "Garage") return;
                if (tailLens == null) CaptureSharedAssets();
                CheckSharedAssets();
                Require(oldRuntimeMaterials.All(material => material == null), "Headlight runtime materials survived scene unload.");
                oldRuntimeMaterials.Clear();
                if (SessionState.GetInt(Key + "Completed", 0) == 2)
                {
                    Debug.Log("XRACE_VEHICLE_LIGHTS_SMOKE_OK: scene/prefab colors match; Garage idle, race idle/tail/brake/handbrake, 20 toggles, disable restore, two scene cycles, shared materials unchanged, runtime materials freed.");
                    End(0);
                    return;
                }
                GarageManager garage = Components<GarageManager>(roots).Single();
                TMPro.TMP_Text name = Components<TMPro.TMP_Text>(roots).Single(text => text.name == "CarName_Text");
                if (name.text != "Toyota AE86") { garage.NextCar(); return; }
                Transform preview = garage.GetComponentsInChildren<Transform>().Single(value => value.name == "CarPreview");
                CheckEmission(preview.gameObject, tailLens.GetColor(Emission), false);
                Button button = Components<Button>(roots).Single(value => value.name == "Btn_Race");
                if (!button.interactable) return;
                button.onClick.Invoke();
                SessionState.SetInt(Key, 2);
            }
            else if (step == 2)
            {
                if (scene.name != "Touge01") return;
                RaceSessionManager session = Components<RaceSessionManager>(roots).Single();
                if (session.CarBody == null) return;
                GameObject car = session.CarBody.gameObject;
                WheelController wheels = car.GetComponent<WheelController>();
                BrakeLightController lights = car.GetComponentInChildren<BrakeLightController>(true);
                PopupHeadlights popup = car.GetComponentInChildren<PopupHeadlights>(true);
                Require(wheels != null && lights != null && popup != null, "Selected AE86 has missing light/input components.");
                wheels.enabled = false;
                popup.enabled = false;
                foreach (WheelCollider wheel in car.GetComponentsInChildren<WheelCollider>(true)) wheel.enabled = false;
                session.CarBody.isKinematic = true;
                popup.SetRaised(false);
                SetInputs(wheels, 0f, false);
                Refresh(lights);
                CheckEmission(car, tailLens.GetColor(Emission), false);
                SetSentinel(car);
                for (int i = 0; i < 20; i++)
                {
                    popup.SetRaised(true);
                    SetInputs(wheels, 0f, false);
                    Refresh(lights);
                    CheckEmission(car, lights.tailColor, true);
                    SetInputs(wheels, 1f, false);
                    Refresh(lights);
                    CheckEmission(car, lights.brakeColor, true);
                    popup.SetRaised(false);
                    SetInputs(wheels, 0f, true);
                    Refresh(lights);
                    CheckEmission(car, lights.brakeColor, true);
                    SetInputs(wheels, 0f, false);
                    Refresh(lights);
                    CheckEmission(car, tailLens.GetColor(Emission), true);
                }
                SetInputs(wheels, 1f, false);
                Refresh(lights);
                lights.enabled = false;
                CheckEmission(car, tailLens.GetColor(Emission), true);
                CheckSharedAssets();
                foreach (Renderer renderer in car.GetComponentsInChildren<Renderer>(true))
                    foreach (Material material in renderer.sharedMaterials)
                        if (material != null && material.name == headLens.name + " (Runtime)" && !oldRuntimeMaterials.Contains(material))
                            oldRuntimeMaterials.Add(material);
                Require(oldRuntimeMaterials.Count == 1, "Headlights did not use exactly one owned runtime material.");
                SessionState.SetInt(Key + "Completed", SessionState.GetInt(Key + "Completed", 0) + 1);
                Components<Button>(roots).Single(button => button.name == "Btn_ExitGarage").onClick.Invoke();
                SessionState.SetInt(Key, 1);
            }
        }
        catch (Exception error)
        {
            Debug.LogException(error);
            End(1);
        }
    }

    private static void CaptureSharedAssets()
    {
        tailLens = AssetDatabase.LoadAssetAtPath<Material>(LensPath + "M_TaillightLens.mat");
        headLens = AssetDatabase.LoadAssetAtPath<Material>(LensPath + "M_HeadlightLens.mat");
        foreach (string path in new[] { "Assets/ScriptableObjects/Cars/ae86.asset", "Assets/ScriptableObjects/Cars/rmcar26.asset" })
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<CarDefinition>(path).carPrefab;
            foreach (Renderer renderer in prefab.GetComponentsInChildren<Renderer>(true))
                foreach (Material material in renderer.sharedMaterials)
                    if (material != null && material.HasProperty(Emission) && !sharedMaterials.Contains(material))
                    {
                        sharedMaterials.Add(material);
                        originalEmissions.Add(material.GetColor(Emission));
                    }
        }
    }

    private static void CheckSharedAssets()
    {
        for (int i = 0; i < sharedMaterials.Count; i++)
            Require(Equal(sharedMaterials[i].GetColor(Emission), originalEmissions[i]), "A vehicle lamp mutated shared asset " + sharedMaterials[i].name);
    }

    private static void SetSentinel(GameObject car)
    {
        foreach (Renderer renderer in car.GetComponentsInChildren<Renderer>(true))
        {
            Material[] materials = renderer.sharedMaterials;
            for (int i = 0; i < materials.Length; i++)
            {
                if (materials[i] != tailLens) continue;
                var block = new MaterialPropertyBlock();
                renderer.GetPropertyBlock(block, i);
                block.SetFloat(Sentinel, 123f);
                renderer.SetPropertyBlock(block, i);
            }
        }
    }

    private static void CheckEmission(GameObject car, Color expected, bool sentinel)
    {
        int checkedSlots = 0;
        foreach (Renderer renderer in car.GetComponentsInChildren<Renderer>(true))
        {
            Material[] materials = renderer.sharedMaterials;
            for (int i = 0; i < materials.Length; i++)
            {
                if (materials[i] != tailLens) continue;
                var block = new MaterialPropertyBlock();
                renderer.GetPropertyBlock(block, i);
                Color actual = block.HasColor(Emission) ? block.GetColor(Emission) : materials[i].GetColor(Emission);
                Require(Equal(actual, expected), "Unexpected lamp emission on " + renderer.name + ": " + actual + " instead of " + expected);
                if (sentinel) Require(block.GetFloat(Sentinel) == 123f, "Lamp update removed an unrelated material property override.");
                checkedSlots++;
            }
        }
        Require(checkedSlots > 0, "No taillight material slots were checked.");
    }

    private static void SetInputs(WheelController wheels, float brake, bool handbrake)
    {
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        typeof(WheelController).GetField("<BrakeInput>k__BackingField", flags).SetValue(wheels, brake);
        typeof(WheelController).GetField("<IsHandbraking>k__BackingField", flags).SetValue(wheels, handbrake);
    }

    private static void Refresh(BrakeLightController lights) => typeof(BrakeLightController)
        .GetMethod("Update", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(lights, null);
    private static bool Equal(Color left, Color right) => (left - right).maxColorComponent < 0.00001f
        && (right - left).maxColorComponent < 0.00001f;
    private static IEnumerable<T> Components<T>(GameObject[] roots) where T : Component => roots.SelectMany(root => root.GetComponentsInChildren<T>(true));
    private static void Require(bool condition, string message) { if (!condition) throw new Exception(message); }
    private static void End(int code) { SessionState.SetInt(Key, 0); EditorApplication.Exit(code); }
}
