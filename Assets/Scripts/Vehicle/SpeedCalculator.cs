using UnityEngine;
using UnityEngine.UI;

public class SpeedCalculator : MonoBehaviour
{
    [Tooltip("Скорость в км/ч")]
    public float Speed;
    public Rigidbody rb;
    public WheelController controller;

    public Text SpeedText;
    public Text GearText;
    public Text RPMText;

    private void Awake()
    {
        if (rb == null) rb = GetComponent<Rigidbody>();
        if (controller == null) controller = GetComponent<WheelController>();
    }

    private void Update()
    {
        Speed = rb == null ? 0f : rb.linearVelocity.magnitude * 3.6f;

        if (SpeedText != null) SpeedText.text = $"{Speed:0} km/h";
        if (GearText != null) GearText.text = controller == null ? "-" : controller.GearLabel;
        if (RPMText != null) RPMText.text = controller == null ? "0" : $"{controller.EngineRpm:0} RPM";
    }
}
