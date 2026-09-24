using UnityEngine;

public class WheelAlignment : MonoBehaviour
{
    [Header("Ссылки")]
    public GameObject wheelGraphic;  // меш колеса
    public WheelCollider wheelCol;   // физика колеса

    [Header("Руление")]
    public bool steerable;
    public float steeringAngle;      // сюда пишет WheelController

    [Header("Визуальная правка")]
    [Tooltip("Постоянный доворот поверх, если нужно")]
    public Vector3 baseRotation;

    Quaternion rest; // родная поза меша 
    float spin;      // накопленный угол вращения, градусы

    void Start()
    {
        if (wheelGraphic != null)
            rest = wheelGraphic.transform.localRotation;
    }

    void LateUpdate()
    {
        if (wheelGraphic == null || wheelCol == null) return;

        if (steerable)
            wheelCol.steerAngle = steeringAngle;

        // Вращение накапливаем: rpm - градусы за кадр
        spin += wheelCol.rpm / 60f * 360f * Time.deltaTime;

        // руль (Y) - родная поза - вращение вокруг своей оси (X)
        wheelGraphic.transform.localRotation =
            Quaternion.Euler(baseRotation.x, baseRotation.y - wheelCol.steerAngle, baseRotation.z)
            * rest
            * Quaternion.Euler(spin, 0f, 0f);
    }
}