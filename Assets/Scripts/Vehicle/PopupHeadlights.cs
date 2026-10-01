using UnityEngine;

[DisallowMultipleComponent]
public class PopupHeadlights : MonoBehaviour
{
    [Header("Управление")]
    public KeyCode toggleKey = KeyCode.L;
    public KeyCode highBeamKey = KeyCode.O;
    public bool startRaised;
    [Min(0.1f)] public float movementSpeed = 4.5f;

    [Header("Механизм")]
    public Transform headlightAssembly;
    public Vector3 closedLocalEuler = new Vector3(270.02f, 0f, 0f);
    public Vector3 openLocalEuler = new Vector3(334.51f, 180f, 180f);

    [Header("Свет")]
    public Light leftLight;
    public Light rightLight;
    [Min(0f)] public float lightIntensity = 12f;
    [Min(0f)] public float lowBeamRange = 42f;
    [Min(0f)] public float highBeamIntensity = 30f;
    [Min(0f)] public float highBeamRange = 80f;
    [Range(1f, 179f)] public float lowBeamSpotAngle = 52f;
    [Range(1f, 179f)] public float highBeamSpotAngle = 34f;

    [Header("Свечение линз")]
    public Material headlightLens;                          // M_HeadlightLens
    private static readonly int EmissionColor = Shader.PropertyToID("_EmissionColor");
    private static readonly Color LensWarm = new Color(1f, 0.93f, 0.75f) * 3f;

    public bool IsRaised { get; private set; }
    public bool IsLowBeamOn { get; private set; }
    public bool IsHighBeamOn { get; private set; }

    private float openAmount;
    private float lastLensAmount = -1f;                     // кэш: пишем только по смене
    private Material runtimeLens;
    private Renderer[] lensRenderers;

    private void Awake()
    {
        CreateRuntimeLens();
        IsLowBeamOn = startRaised;
        IsRaised = IsLowBeamOn;
        openAmount = IsRaised ? 1f : 0f;
        ApplyState();
    }

    private void Update()
    {
        if (Time.timeScale <= 0f) return;
        if (Input.GetKeyDown(toggleKey))
            IsLowBeamOn = !IsLowBeamOn;
        if (Input.GetKeyDown(highBeamKey))
            IsHighBeamOn = !IsHighBeamOn;

        IsRaised = IsLowBeamOn || IsHighBeamOn;

        openAmount = Mathf.MoveTowards(
            openAmount,
            IsRaised ? 1f : 0f,
            movementSpeed * Time.deltaTime);
        ApplyState();
    }

    public void SetRaised(bool raised)
    {
        IsLowBeamOn = raised;
        IsRaised = IsLowBeamOn || IsHighBeamOn;
    }

    private void ApplyState()
    {
        float smoothAmount = Mathf.SmoothStep(0f, 1f, openAmount);
        if (headlightAssembly != null)
        {
            Quaternion closedRotation = Quaternion.Euler(closedLocalEuler);
            Quaternion openRotation = Quaternion.Euler(openLocalEuler);
            headlightAssembly.localRotation = Quaternion.Slerp(closedRotation, openRotation, smoothAmount);
        }

        UpdateLight(leftLight, smoothAmount);
        UpdateLight(rightLight, smoothAmount);

        // линзы: пишем цвет только пока значение реально меняется
        if (runtimeLens != null && smoothAmount != lastLensAmount)
        {
            runtimeLens.SetColor(EmissionColor, LensWarm * smoothAmount);
            lastLensAmount = smoothAmount;
        }
    }

    private void CreateRuntimeLens()
    {
        if (headlightLens == null) return;
        runtimeLens = new Material(headlightLens) { name = headlightLens.name + " (Runtime)", hideFlags = HideFlags.DontSave };
        lensRenderers = GetComponentsInChildren<Renderer>(true);
        foreach (Renderer renderer in lensRenderers)
        {
            Material[] materials = renderer.sharedMaterials;
            bool changed = false;
            for (int i = 0; i < materials.Length; i++)
            {
                if (materials[i] != headlightLens) continue;
                materials[i] = runtimeLens;
                changed = true;
            }
            if (changed) renderer.sharedMaterials = materials;
        }
    }

    private void OnDestroy()
    {
        if (runtimeLens == null) return;
        // Also keep renderers valid when only this component is removed.
        if (lensRenderers != null)
        {
            foreach (Renderer renderer in lensRenderers)
            {
                if (renderer == null) continue;
                Material[] materials = renderer.sharedMaterials;
                bool changed = false;
                for (int i = 0; i < materials.Length; i++)
                {
                    if (materials[i] != runtimeLens) continue;
                    materials[i] = headlightLens;
                    changed = true;
                }
                if (changed) renderer.sharedMaterials = materials;
            }
        }
        Destroy(runtimeLens);
    }

    private void UpdateLight(Light lamp, float amount)
    {
        if (lamp == null)
            return;

        lamp.enabled = amount > 0.02f;
        lamp.intensity = (IsHighBeamOn ? highBeamIntensity : lightIntensity) * amount;
        lamp.range = IsHighBeamOn ? highBeamRange : lowBeamRange;
        lamp.spotAngle = IsHighBeamOn ? highBeamSpotAngle : lowBeamSpotAngle;
        lamp.innerSpotAngle = lamp.spotAngle * 0.58f;
    }
}
