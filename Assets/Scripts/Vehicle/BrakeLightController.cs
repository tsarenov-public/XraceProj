using UnityEngine;

public class BrakeLightController : MonoBehaviour
{
    [SerializeField] private Material taillightLens;      // M_TaillightLens
    [SerializeField] private PopupHeadlights headlights;  // знать про габариты
    [SerializeField] private WheelController wheels;      // знать про ручник

    [Header("Цвета на твой вкус (HDR: цвет + Intensity)")]
    [ColorUsage(true, true)] public Color tailColor  = new Color(1f, 0.03f, 0.03f) * 2f;
    [ColorUsage(true, true)] public Color brakeColor = new Color(1f, 0.01f, 0.01f) * 4f;

    private static readonly int EmissionColor = Shader.PropertyToID("_EmissionColor");

    private Color lastColor = new Color(0f, 0f, 0f, -1f);   // кэш: пишем только по смене

    void Update()
    {
        float brake = -Mathf.Min(0f, Input.GetAxis("Vertical")); // педаль тормоза
        bool handbrake = wheels != null && wheels.IsHandbraking;
        bool braking = brake > 0.1f || handbrake;

        Color c = Color.black;                                       // выкл
        if (headlights != null && headlights.IsRaised) c = tailColor; // габарит
        if (braking) c = brakeColor;                                  // стоп перебивает

        if (c != lastColor)
        {
            taillightLens.SetColor(EmissionColor, c);
            lastColor = c;
        }
    }
}