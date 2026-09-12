using UnityEngine;

public class CarController : MonoBehaviour
{
    [Header("Физические колёса")]
    public WheelCollider frontLeft, frontRight, rearLeft, rearRight;

    [Header("Визуальные колёса (модели)")]
    public Transform frontLeftMesh, frontRightMesh, rearLeftMesh, rearRightMesh;

    [Header("Настройки движения")]
    public float motorForce = 1500f;
    public float steerAngle = 30f;

    [Header("Настройки дрифта")]
    public float driftFactor = 0.5f;

    private float currentSteerAngle;

    void FixedUpdate()
    {
        float gas = Input.GetAxis("Vertical");
        float steer = Input.GetAxis("Horizontal");
        bool isDrifting = Input.GetKey(KeyCode.Space);

        // Газ на задние колёса
        rearLeft.motorTorque = gas * motorForce;
        rearRight.motorTorque = gas * motorForce;

        // Плавный руль на передние колёса
        currentSteerAngle = Mathf.Lerp(currentSteerAngle, steer * steerAngle, Time.deltaTime * 5f);
        frontLeft.steerAngle = currentSteerAngle;
        frontRight.steerAngle = currentSteerAngle;

        // Дрифт: ослабляем боковое сцепление задних колёс на пробеле
        if (isDrifting)
        {
            SetDrift(rearLeft, driftFactor);
            SetDrift(rearRight, driftFactor);
        }
        else
        {
            SetNormal(rearLeft);
            SetNormal(rearRight);
        }
    }

    void Update()
    {
        // Синхронизируем визуальные колёса с физическими
        SyncWheel(frontLeft, frontLeftMesh);
        SyncWheel(frontRight, frontRightMesh);
        SyncWheel(rearLeft, rearLeftMesh);
        SyncWheel(rearRight, rearRightMesh);
    }

    void SyncWheel(WheelCollider col, Transform mesh)
    {
        if (mesh == null) return;
        Vector3 pos;
        Quaternion rot;
        col.GetWorldPose(out pos, out rot);
        mesh.position = pos;
        mesh.rotation = rot;
    }

    void SetDrift(WheelCollider wheel, float factor)
    {
        WheelFrictionCurve f = wheel.sidewaysFriction;
        f.stiffness = factor;
        wheel.sidewaysFriction = f;
    }

    void SetNormal(WheelCollider wheel)
    {
        WheelFrictionCurve f = wheel.sidewaysFriction;
        f.stiffness = 1f;
        wheel.sidewaysFriction = f;
    }
}