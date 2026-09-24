using UnityEngine;

[DisallowMultipleComponent]
public class PopupHeadlights : MonoBehaviour
{
    [Header("Управление")]
    public KeyCode toggleKey = KeyCode.L;
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

    [Header("Свечение линз")]
    public Material headlightLens;                          // M_HeadlightLens
    private static readonly int EmissionColor = Shader.PropertyToID("_EmissionColor");
    private static readonly Color LensWarm = new Color(1f, 0.93f, 0.75f) * 3f;

    public bool IsRaised { get; private set; }

    private float openAmount;
    private float lastLensAmount = -1f;                     // кэш: пишем только по смене

    private void Awake()
    {
        IsRaised = startRaised;
        openAmount = IsRaised ? 1f : 0f;
        ApplyState();
    }

    private void Update()
    {
        if (Input.GetKeyDown(toggleKey))
            IsRaised = !IsRaised;

        openAmount = Mathf.MoveTowards(
            openAmount,
            IsRaised ? 1f : 0f,
            movementSpeed * Time.deltaTime);
        ApplyState();
    }

    public void SetRaised(bool raised)
    {
        IsRaised = raised;
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
        if (headlightLens != null && smoothAmount != lastLensAmount)
        {
            headlightLens.SetColor(EmissionColor, LensWarm * smoothAmount);
            lastLensAmount = smoothAmount;
        }
    }

    private void UpdateLight(Light lamp, float amount)
    {
        if (lamp == null)
            return;

        lamp.intensity = lightIntensity * amount;   // enabled НЕ трогаем: 0 = выкл
    }
}