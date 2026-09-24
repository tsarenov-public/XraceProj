using UnityEngine;

public class BrakeLightController : MonoBehaviour
{
    [SerializeField] private Material taillightLens;
    [SerializeField] private PopupHeadlights headlights;
    [SerializeField] private WheelController wheels;

    [Header("Цвета на твой вкус (HDR: цвет + Intensity)")]
    [ColorUsage(true, true)] public Color tailColor  = new Color(1f, 0.03f, 0.03f) * 2f;
    [ColorUsage(true, true)] public Color brakeColor = new Color(1f, 0.01f, 0.01f) * 4f;

    private static readonly int EmissionColor = Shader.PropertyToID("_EmissionColor");

    void Update()
    {
        float brake = -Mathf.Min(0f, Input.GetAxis("Vertical"));
        bool handbrake = wheels != null && wheels.IsHandbraking;
        bool braking = brake > 0.1f || handbrake;

        Color c = Color.black;                              // выкл
        if (headlights != null && headlights.IsRaised) c = tailColor;   // габарит
        if (braking) c = brakeColor;                        // стоп перебивает

        taillightLens.SetColor(EmissionColor, c);
    }
}