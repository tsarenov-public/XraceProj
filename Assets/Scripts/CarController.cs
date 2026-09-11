using UnityEngine;

public class CarController : MonoBehaviour
{
    public WheelCollider frontLeft, frontRight, rearLeft, rearRight;
    
    [Header("Настройки движения")]
    public float motorForce = 1500f;
    public float steerAngle = 30f;
    
    [Header("Настройки дрифта")]
    public float driftFactor = 0.5f; // Насколько сильно заносит (0-1)

    private float currentSteerAngle;
    private bool isDrifting;

    void FixedUpdate()
    {
        // Получаем ввод
        float gas = Input.GetAxis("Vertical"); // W/S
        float steer = Input.GetAxis("Horizontal"); // A/D
        isDrifting = Input.GetKey(KeyCode.Space); // Пробел = ручник

        // Применяем газ к задним колёсам
        rearLeft.motorTorque = gas * motorForce;
        rearRight.motorTorque = gas * motorForce;

        // Плавный поворот руля
        currentSteerAngle = Mathf.Lerp(currentSteerAngle, steer * steerAngle, Time.deltaTime * 5f);
        frontLeft.steerAngle = currentSteerAngle; // Минус для корректного направления
        frontRight.steerAngle = currentSteerAngle;

        // Настройка дрифта
        if (isDrifting)
        {
            // Уменьшаем сцепление задних колёс
            SetDrift(rearLeft, driftFactor);
            SetDrift(rearRight, driftFactor);
        }
        else
        {
            // Возвращаем нормальное сцепление
            SetNormal(rearLeft);
            SetNormal(rearRight);
        }
    }

    void SetDrift(WheelCollider wheel, float factor)
    {
        WheelFrictionCurve sidewaysFriction = wheel.sidewaysFriction;
        sidewaysFriction.stiffness = factor; // Уменьшаем жёсткость (сцепление)
        wheel.sidewaysFriction = sidewaysFriction;
    }

    void SetNormal(WheelCollider wheel)
    {
        WheelFrictionCurve sidewaysFriction = wheel.sidewaysFriction;
        sidewaysFriction.stiffness = 1f; // Нормальное сцепление
        wheel.sidewaysFriction = sidewaysFriction;
    }
}