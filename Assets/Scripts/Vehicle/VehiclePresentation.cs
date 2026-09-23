using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
[DefaultExecutionOrder(650)]
public sealed class VehiclePresentation : MonoBehaviour
{
    [Header("Автомобиль")]
    public WheelController controller;

    [Header("Камеры")]
    public KeyCode chaseCameraKey = KeyCode.Alpha1;
    public KeyCode cockpitCameraKey = KeyCode.Alpha2;
    [Range(45f, 100f)] public float cockpitFieldOfView = 68f;
    public Vector3 cockpitOffsetFromSteeringWheel = new Vector3(0f, 0.29f, -0.38f);

    [Header("Руль")]
    public Transform steeringWheel;
    public float steeringWheelMaximumAngle = 420f;
    public float steeringWheelSpeed = 900f;
    public Vector3 steeringWheelAxis = Vector3.forward;

    [Header("Стоп-сигналы")]
    public float brakeLightIntensity = 7f;
    public float brakeLightRange = 4.5f;

    private readonly List<RearLampBinding> rearLampBindings = new List<RearLampBinding>();
    private Quaternion steeringWheelRestRotation;
    private float steeringWheelAngle;
    private RacingCamera chaseController;
    private Camera chaseCamera;
    private AudioListener chaseListener;
    private Camera cockpitCamera;
    private AudioListener cockpitListener;
    private Light brakeLightLeft;
    private Light brakeLightRight;
    private bool brakingLastFrame;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void InstallForVehicles()
    {
        foreach (WheelController candidate in FindObjectsByType<WheelController>())
        {
            if (candidate != null && candidate.isActiveAndEnabled && candidate.GetComponent<VehiclePresentation>() == null)
                candidate.gameObject.AddComponent<VehiclePresentation>();
        }
    }

    private void Awake()
    {
        if (controller == null)
            controller = GetComponent<WheelController>();

        if (steeringWheel == null)
            steeringWheel = FindDeep(transform, "SteeringWheel");
        if (steeringWheel != null)
            steeringWheelRestRotation = steeringWheel.localRotation;

        ResolveRearLampMaterials();
        CreateBrakeLights();
        SetupCameras();
        SetBrakeLights(false, true);
        SelectCockpitCamera(false);
    }

    private void Update()
    {
        if (Input.GetKeyDown(chaseCameraKey) || Input.GetKeyDown(KeyCode.Keypad1))
            SelectCockpitCamera(false);
        if (Input.GetKeyDown(cockpitCameraKey) || Input.GetKeyDown(KeyCode.Keypad2))
            SelectCockpitCamera(true);

        UpdateSteeringWheel();

        bool braking = controller != null && controller.BrakeInput > 0.05f;
        if (braking != brakingLastFrame)
            SetBrakeLights(braking, false);
    }

    private void UpdateSteeringWheel()
    {
        if (steeringWheel == null || controller == null)
            return;

        float targetAngle = -controller.Horizontal * steeringWheelMaximumAngle;
        steeringWheelAngle = Mathf.MoveTowards(
            steeringWheelAngle,
            targetAngle,
            steeringWheelSpeed * Time.deltaTime);
        steeringWheel.localRotation = steeringWheelRestRotation
            * Quaternion.AngleAxis(steeringWheelAngle, steeringWheelAxis.normalized);
    }

    private void SetupCameras()
    {
        foreach (RacingCamera candidate in FindObjectsByType<RacingCamera>())
        {
            if (candidate.target != transform)
                continue;

            chaseController = candidate;
            chaseCamera = candidate.GetComponent<Camera>();
            chaseListener = candidate.GetComponent<AudioListener>();
            break;
        }

        if (chaseCamera == null)
            return;

        Vector3 steeringPosition = steeringWheel != null
            ? transform.InverseTransformPoint(steeringWheel.position)
            : new Vector3(0.32f, 0.82f, 0.38f);

        GameObject cameraObject = new GameObject("Driver Camera");
        cameraObject.transform.SetParent(transform, false);
        cameraObject.transform.localPosition = steeringPosition + cockpitOffsetFromSteeringWheel;
        cameraObject.transform.localRotation = Quaternion.Euler(2f, 0f, 0f);

        cockpitCamera = cameraObject.AddComponent<Camera>();
        cockpitCamera.CopyFrom(chaseCamera);
        cockpitCamera.fieldOfView = cockpitFieldOfView;
        cockpitCamera.nearClipPlane = 0.035f;
        cockpitCamera.enabled = false;

        cockpitListener = cameraObject.AddComponent<AudioListener>();
        cockpitListener.enabled = false;
    }

    private void SelectCockpitCamera(bool cockpit)
    {
        if (chaseCamera == null || cockpitCamera == null)
            return;

        if (chaseController != null)
            chaseController.enabled = !cockpit;
        chaseCamera.enabled = !cockpit;
        if (chaseListener != null)
            chaseListener.enabled = !cockpit;

        cockpitCamera.enabled = cockpit;
        if (cockpitListener != null)
            cockpitListener.enabled = cockpit;
    }

    private void CreateBrakeLights()
    {
        brakeLightLeft = CreateBrakeLight("Left Brake Light", new Vector3(-0.55f, 0.65f, -1.94f));
        brakeLightRight = CreateBrakeLight("Right Brake Light", new Vector3(0.55f, 0.65f, -1.94f));
    }

    private Light CreateBrakeLight(string name, Vector3 localPosition)
    {
        GameObject lightObject = new GameObject(name);
        lightObject.transform.SetParent(transform, false);
        lightObject.transform.localPosition = localPosition;
        lightObject.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);

        Light light = lightObject.AddComponent<Light>();
        light.type = LightType.Spot;
        light.color = new Color(1f, 0.015f, 0.005f);
        light.intensity = 0f;
        light.range = brakeLightRange;
        light.spotAngle = 105f;
        light.innerSpotAngle = 72f;
        light.shadows = LightShadows.None;
        light.enabled = false;
        return light;
    }

    private void ResolveRearLampMaterials()
    {
        foreach (Renderer targetRenderer in GetComponentsInChildren<Renderer>(true))
        {
            Vector3 localBoundsCenter = transform.InverseTransformPoint(targetRenderer.bounds.center);
            Material[] materials = targetRenderer.sharedMaterials;
            for (int index = 0; index < materials.Length; index++)
            {
                Material material = materials[index];
                if (material == null)
                    continue;

                string materialName = material.name.ToLowerInvariant();
                bool explicitlyRear = materialName.Contains("tail") || materialName.Contains("rear_lamp");
                bool rearLampCover = materialName.Contains("lamp") && localBoundsCenter.z < -0.65f;
                if ((!explicitlyRear && !rearLampCover) || materialName.Contains("disc"))
                    continue;

                Color baseColor = ReadColor(material, "_BaseColor", "_Color", Color.red * 0.2f);
                Color emissionColor = material.HasProperty("_EmissionColor")
                    ? material.GetColor("_EmissionColor")
                    : Color.black;
                if (material.HasProperty("_EmissionColor"))
                    material.EnableKeyword("_EMISSION");
                rearLampBindings.Add(new RearLampBinding(targetRenderer, index, baseColor, emissionColor));
            }
        }
    }

    private void SetBrakeLights(bool braking, bool force)
    {
        if (!force && braking == brakingLastFrame)
            return;

        brakingLastFrame = braking;
        SetLightState(brakeLightLeft, braking);
        SetLightState(brakeLightRight, braking);

        foreach (RearLampBinding binding in rearLampBindings)
            binding.Apply(braking);
    }

    private void SetLightState(Light light, bool active)
    {
        if (light == null)
            return;
        light.enabled = active;
        light.intensity = active ? brakeLightIntensity : 0f;
    }

    private static Color ReadColor(Material material, string primaryProperty, string fallbackProperty, Color fallback)
    {
        if (material.HasProperty(primaryProperty)) return material.GetColor(primaryProperty);
        if (material.HasProperty(fallbackProperty)) return material.GetColor(fallbackProperty);
        return fallback;
    }

    private static Transform FindDeep(Transform root, string objectName)
    {
        if (root.name == objectName)
            return root;

        foreach (Transform child in root)
        {
            Transform found = FindDeep(child, objectName);
            if (found != null)
                return found;
        }
        return null;
    }

    private sealed class RearLampBinding
    {
        private readonly Renderer targetRenderer;
        private readonly int materialIndex;
        private readonly Color originalBaseColor;
        private readonly Color originalEmissionColor;
        private readonly MaterialPropertyBlock propertyBlock = new MaterialPropertyBlock();

        public RearLampBinding(Renderer renderer, int index, Color baseColor, Color emissionColor)
        {
            targetRenderer = renderer;
            materialIndex = index;
            originalBaseColor = baseColor;
            originalEmissionColor = emissionColor;
        }

        public void Apply(bool braking)
        {
            if (targetRenderer == null)
                return;

            targetRenderer.GetPropertyBlock(propertyBlock, materialIndex);
            Color baseColor = braking
                ? Color.Lerp(originalBaseColor, new Color(1f, 0.015f, 0.005f, originalBaseColor.a), 0.82f)
                : originalBaseColor;
            Color emissionColor = braking
                ? new Color(5.5f, 0.025f, 0.01f, 1f)
                : originalEmissionColor;
            propertyBlock.SetColor("_BaseColor", baseColor);
            propertyBlock.SetColor("_Color", baseColor);
            propertyBlock.SetColor("_EmissionColor", emissionColor);
            targetRenderer.SetPropertyBlock(propertyBlock, materialIndex);
        }
    }
}
