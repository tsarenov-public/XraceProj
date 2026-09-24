using UnityEngine;

[DisallowMultipleComponent]
[DefaultExecutionOrder(650)]
public sealed class VehiclePresentation : MonoBehaviour
{
    [Header("Автомобиль")]
    public WheelController controller;

    [Header("Руль")]
    public Transform steeringWheel;
    [Tooltip("Полный визуальный угол руля при максимальном вывороте передних колёс")]
    public float steeringWheelMaximumAngle = 420f;
    public float steeringWheelSpeed = 900f;
    public Vector3 steeringWheelAxis = Vector3.forward;

    private Quaternion steeringWheelRestRotation;
    private float steeringWheelAngle;

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
    }

    private void LateUpdate()
    {
        if (steeringWheel == null || controller == null)
            return;

        float normalizedWheelAngle = controller.FrontWheelSteerAngle
            / Mathf.Max(1f, controller.lowSpeedSteerAngle);
        float targetAngle = -Mathf.Clamp(normalizedWheelAngle, -1f, 1f)
            * steeringWheelMaximumAngle;

        steeringWheelAngle = Mathf.MoveTowards(
            steeringWheelAngle,
            targetAngle,
            steeringWheelSpeed * Time.deltaTime);
        steeringWheel.localRotation = steeringWheelRestRotation
            * Quaternion.AngleAxis(steeringWheelAngle, steeringWheelAxis.normalized);
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
}
