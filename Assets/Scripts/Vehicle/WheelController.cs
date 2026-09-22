using System;
using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class WheelController : MonoBehaviour
{
    [Header("Колёса (FL, FR, RL, RR)")]
    public WheelAlignment[] steerableWheels;

    [Header("Управление")]
    [Tooltip("W — газ, S — тормоз, A/D — руль, Space — ручник, Q/E — передача вниз/вверх")]
    public KeyCode handbrakeKey = KeyCode.Space;
    public KeyCode shiftDownKey = KeyCode.Q;
    public KeyCode shiftUpKey = KeyCode.E;
    public float steeringInputSpeed = 5.5f;
    public float steeringReturnSpeed = 7.5f;

    [Header("Рулевое управление")]
    public float lowSpeedSteerAngle = 38f;
    public float highSpeedSteerAngle = 11f;
    [Tooltip("Скорость в км/ч, на которой используется минимальный угол руля")]
    public float fullSteerFadeSpeedKph = 150f;
    [Range(0f, 1f)] public float counterSteerAssist = 0.72f;
    public float counterSteerStartAngle = 4f;
    public float counterSteerMaxAngle = 28f;
    public float wheelbase = 3.07f;
    public float trackWidth = 1.57f;

    [Header("Двигатель")]
    public float peakEngineTorque = 460f;
    public float peakTorqueRpm = 5200f;
    public float idleRpm = 900f;
    public float redlineRpm = 7800f;
    public float engineRpmResponse = 9f;
    public float engineBrakingTorque = 90f;
    [Range(0.5f, 1f)] public float drivetrainEfficiency = 0.88f;

    [Header("Механическая коробка")]
    [Tooltip("Передаточные числа передач 1..N")]
    public float[] forwardGearRatios = { 3.20f, 2.12f, 1.55f, 1.25f, 1.03f, 0.86f };
    public float reverseGearRatio = 3.10f;
    public float finalDriveRatio = 3.90f;
    public float shiftDuration = 0.22f;
    [SerializeField] private int currentGear = 1;

    [Header("Тормоза")]
    public float frontBrakeTorque = 2800f;
    public float rearBrakeTorque = 1900f;
    public float handbrakeTorque = 4200f;
    public float absSlipLimit = 0.95f;

    [Header("Шины и дрифт")]
    public float frontSidewaysStiffness = 1.35f;
    public float rearSidewaysStiffness = 1.16f;
    public float powerSlideRearStiffness = 0.78f;
    public float handbrakeRearStiffness = 0.36f;
    public float gripChangeSpeed = 5f;
    public float driftMinSpeedKph = 22f;
    public float driftSustainSlip = 0.22f;
    [Tooltip("Небольшая помощь вращению машины при начале силового заноса")]
    public float driftYawAssist = 650f;
    [Tooltip("После этого угла ассистент начинает мягко ловить разворот")]
    public float spinRecoveryStartAngle = 52f;
    public float spinRecoveryFullAngle = 78f;
    public float spinRecoveryTorque = 2100f;

    [Header("Шасси")]
    public float antiRollFront = 7200f;
    public float antiRollRear = 6100f;
    public float aerodynamicDownforce = 8f;
    public float aerodynamicDrag = 0.42f;
    [Tooltip("Смещение центра масс по высоте относительно центра колёс")]
    public float centerOfMassHeightOffset = -0.24f;

    [Header("Физика")]
    public Rigidbody RB;

    public float Horizontal { get; private set; }
    public float Vertical { get; private set; }
    public float ThrottleInput { get; private set; }
    public float BrakeInput { get; private set; }
    public bool IsHandbraking { get; private set; }
    public float EngineRpm { get; private set; }
    public float SpeedKph => RB == null ? 0f : RB.linearVelocity.magnitude * 3.6f;
    public float ForwardSpeedKph => RB == null ? 0f : Vector3.Dot(RB.linearVelocity, transform.forward) * 3.6f;
    public float DriftAngle { get; private set; }
    public int CurrentGear => currentGear;
    public string GearLabel => currentGear < 0 ? "R" : currentGear == 0 ? "N" : currentGear.ToString();
    public event Action<int> GearChanged;

    private float rawSteerInput;
    private float smoothedSteerInput;
    private float rearGripBlend;
    private float shiftTimer;
    private float clutch = 1f;
    private int pendingShift;
    private WheelAlignment frontLeft;
    private WheelAlignment frontRight;
    private WheelAlignment rearLeft;
    private WheelAlignment rearRight;

    private void Awake()
    {
        if (RB == null) RB = GetComponent<Rigidbody>();

        ResolveWheelLayout();
        ConfigureRigidbody();
        ConfigureWheelColliders();

        currentGear = Mathf.Clamp(currentGear, -1, forwardGearRatios == null ? 0 : forwardGearRatios.Length);
        EngineRpm = idleRpm;
    }

    private void Update()
    {
        rawSteerInput = Input.GetAxisRaw("Horizontal");
        Vertical = Input.GetAxisRaw("Vertical");
        ThrottleInput = Mathf.Clamp01(Vertical);
        BrakeInput = Mathf.Clamp01(-Vertical);
        IsHandbraking = Input.GetKey(handbrakeKey);

        if (Input.GetKeyDown(shiftUpKey)) pendingShift++;
        if (Input.GetKeyDown(shiftDownKey)) pendingShift--;
    }

    private void FixedUpdate()
    {
        if (RB == null || steerableWheels == null || steerableWheels.Length == 0) return;

        ProcessPendingShift();
        UpdateClutch();
        UpdateSteering();
        UpdateEngineAndDrivetrain();
        ApplyBrakes();
        UpdateTyreGripAndDriftAssist();
        ApplyAntiRollBars();
        ApplyAerodynamics();
    }

    private void ResolveWheelLayout()
    {
        if (steerableWheels == null) return;

        foreach (WheelAlignment wheel in steerableWheels)
        {
            if (wheel == null || wheel.wheelCol == null) continue;

            Vector3 localPosition = transform.InverseTransformPoint(wheel.wheelCol.transform.position);
            bool isLeft = localPosition.x < 0f;

            if (wheel.steerable)
            {
                if (isLeft) frontLeft = wheel;
                else frontRight = wheel;
            }
            else
            {
                if (isLeft) rearLeft = wheel;
                else rearRight = wheel;
            }
        }
    }

    private void ConfigureRigidbody()
    {
        if (RB == null) return;

        RB.interpolation = RigidbodyInterpolation.Interpolate;
        RB.angularDamping = 0.12f;
        RB.maxAngularVelocity = 12f;

        float wheelCenterHeight = 0.5f;
        int wheelCount = 0;
        float heightSum = 0f;

        if (steerableWheels != null)
        {
            foreach (WheelAlignment wheel in steerableWheels)
            {
                if (wheel == null || wheel.wheelCol == null) continue;
                heightSum += transform.InverseTransformPoint(wheel.wheelCol.transform.position).y;
                wheelCount++;
            }
        }

        if (wheelCount > 0) wheelCenterHeight = heightSum / wheelCount;
        RB.centerOfMass = new Vector3(0f, wheelCenterHeight + centerOfMassHeightOffset, 0.08f);
    }

    private void ConfigureWheelColliders()
    {
        if (steerableWheels == null) return;

        bool substepsConfigured = false;
        foreach (WheelAlignment wheel in steerableWheels)
        {
            if (wheel == null || wheel.wheelCol == null) continue;

            WheelCollider collider = wheel.wheelCol;
            collider.wheelDampingRate = 0.28f;

            WheelFrictionCurve forward = collider.forwardFriction;
            forward.extremumSlip = 0.38f;
            forward.extremumValue = 1f;
            forward.asymptoteSlip = 0.82f;
            forward.asymptoteValue = 0.52f;
            forward.stiffness = wheel.steerable ? 1.32f : 1.24f;
            collider.forwardFriction = forward;

            WheelFrictionCurve sideways = collider.sidewaysFriction;
            sideways.extremumSlip = wheel.steerable ? 0.24f : 0.28f;
            sideways.extremumValue = 1f;
            sideways.asymptoteSlip = wheel.steerable ? 0.78f : 0.92f;
            sideways.asymptoteValue = wheel.steerable ? 0.62f : 0.66f;
            sideways.stiffness = wheel.steerable ? frontSidewaysStiffness : rearSidewaysStiffness;
            collider.sidewaysFriction = sideways;

            if (!substepsConfigured)
            {
                collider.ConfigureVehicleSubsteps(5f, 12, 15);
                substepsConfigured = true;
            }
        }
    }

    private void ProcessPendingShift()
    {
        if (pendingShift == 0 || shiftTimer > 0f) return;

        int direction = Math.Sign(pendingShift);
        pendingShift -= direction;
        int maximumGear = forwardGearRatios == null ? 0 : forwardGearRatios.Length;
        int requestedGear = Mathf.Clamp(currentGear + direction, -1, maximumGear);

        if (requestedGear < 0 && Mathf.Abs(ForwardSpeedKph) > 5f) return;
        if (requestedGear == currentGear) return;

        currentGear = requestedGear;
        shiftTimer = shiftDuration;
        clutch = 0f;
        GearChanged?.Invoke(currentGear);
    }

    private void UpdateClutch()
    {
        if (shiftTimer > 0f)
        {
            shiftTimer -= Time.fixedDeltaTime;
            clutch = 0f;
            return;
        }

        clutch = Mathf.MoveTowards(clutch, 1f, Time.fixedDeltaTime * 5.5f);
    }

    private void UpdateSteering()
    {
        float inputRate = Mathf.Abs(rawSteerInput) > 0.01f ? steeringInputSpeed : steeringReturnSpeed;
        smoothedSteerInput = Mathf.MoveTowards(smoothedSteerInput, rawSteerInput, inputRate * Time.fixedDeltaTime);
        Horizontal = smoothedSteerInput;

        Vector3 localVelocity = transform.InverseTransformDirection(RB.linearVelocity);
        float speedKph = RB.linearVelocity.magnitude * 3.6f;
        DriftAngle = speedKph > 3f
            ? Mathf.Atan2(localVelocity.x, Mathf.Max(1f, Mathf.Abs(localVelocity.z))) * Mathf.Rad2Deg
            : 0f;

        float speedFactor = Mathf.Clamp01(speedKph / Mathf.Max(1f, fullSteerFadeSpeedKph));
        float maximumSteer = Mathf.Lerp(lowSpeedSteerAngle, highSpeedSteerAngle, speedFactor);
        float requestedAngle = smoothedSteerInput * maximumSteer;

        if (speedKph > driftMinSpeedKph && Mathf.Abs(DriftAngle) > counterSteerStartAngle)
        {
            float assistStrength = Mathf.InverseLerp(counterSteerStartAngle, 25f, Mathf.Abs(DriftAngle));
            float counterAngle = Mathf.Clamp(DriftAngle, -counterSteerMaxAngle, counterSteerMaxAngle);
            requestedAngle += counterAngle * counterSteerAssist * assistStrength;
        }

        requestedAngle = Mathf.Clamp(requestedAngle, -maximumSteer, maximumSteer);
        SetAckermannSteering(requestedAngle);
    }

    private void SetAckermannSteering(float steeringAngle)
    {
        float leftAngle = steeringAngle;
        float rightAngle = steeringAngle;

        if (Mathf.Abs(steeringAngle) > 0.1f && wheelbase > 0.1f && trackWidth > 0.1f)
        {
            float radius = wheelbase / Mathf.Tan(Mathf.Abs(steeringAngle) * Mathf.Deg2Rad);
            float inner = Mathf.Atan(wheelbase / Mathf.Max(0.1f, radius - trackWidth * 0.5f)) * Mathf.Rad2Deg;
            float outer = Mathf.Atan(wheelbase / (radius + trackWidth * 0.5f)) * Mathf.Rad2Deg;

            if (steeringAngle > 0f)
            {
                leftAngle = outer;
                rightAngle = inner;
            }
            else
            {
                leftAngle = -inner;
                rightAngle = -outer;
            }
        }

        SetWheelSteer(frontLeft, leftAngle);
        SetWheelSteer(frontRight, rightAngle);
    }

    private static void SetWheelSteer(WheelAlignment wheel, float angle)
    {
        if (wheel == null || wheel.wheelCol == null) return;
        wheel.steeringAngle = angle;
        wheel.wheelCol.steerAngle = angle;
    }

    private void UpdateEngineAndDrivetrain()
    {
        float drivenWheelRpm = AverageDrivenWheelRpm();
        float ratio = GetActiveGearRatio();
        float coupledRpm = Mathf.Abs(drivenWheelRpm * ratio * finalDriveRatio);
        float freeRevRpm = Mathf.Lerp(idleRpm, redlineRpm * 0.94f, ThrottleInput);
        float targetRpm = currentGear == 0 || clutch < 0.15f
            ? freeRevRpm
            : Mathf.Max(idleRpm, coupledRpm);

        EngineRpm = Mathf.Lerp(EngineRpm, targetRpm, 1f - Mathf.Exp(-engineRpmResponse * Time.fixedDeltaTime));
        EngineRpm = Mathf.Clamp(EngineRpm, idleRpm, redlineRpm * 1.04f);

        float motorTorquePerWheel = 0f;
        if (currentGear != 0 && clutch > 0f && EngineRpm < redlineRpm)
        {
            float engineTorque = EvaluateEngineTorque(EngineRpm) * ThrottleInput;
            motorTorquePerWheel = engineTorque * ratio * finalDriveRatio * drivetrainEfficiency * clutch * 0.5f;
        }

        ApplyDrivenWheelTorque(rearLeft, motorTorquePerWheel);
        ApplyDrivenWheelTorque(rearRight, motorTorquePerWheel);

        if (ThrottleInput < 0.05f && BrakeInput < 0.05f && currentGear != 0 && clutch > 0.5f)
        {
            ApplyEngineBraking(rearLeft);
            ApplyEngineBraking(rearRight);
        }

        if (frontLeft != null && frontLeft.wheelCol != null) frontLeft.wheelCol.motorTorque = 0f;
        if (frontRight != null && frontRight.wheelCol != null) frontRight.wheelCol.motorTorque = 0f;
    }

    private float GetActiveGearRatio()
    {
        if (currentGear < 0) return -reverseGearRatio;
        if (currentGear == 0 || forwardGearRatios == null || forwardGearRatios.Length == 0) return 0f;
        return forwardGearRatios[Mathf.Clamp(currentGear - 1, 0, forwardGearRatios.Length - 1)];
    }

    private float EvaluateEngineTorque(float rpm)
    {
        if (rpm <= peakTorqueRpm)
        {
            float t = Mathf.InverseLerp(idleRpm, peakTorqueRpm, rpm);
            return peakEngineTorque * Mathf.Lerp(0.62f, 1f, Mathf.SmoothStep(0f, 1f, t));
        }

        float falloff = Mathf.InverseLerp(peakTorqueRpm, redlineRpm, rpm);
        return peakEngineTorque * Mathf.Lerp(1f, 0.48f, falloff);
    }

    private float AverageDrivenWheelRpm()
    {
        float total = 0f;
        int count = 0;
        if (rearLeft != null && rearLeft.wheelCol != null) { total += rearLeft.wheelCol.rpm; count++; }
        if (rearRight != null && rearRight.wheelCol != null) { total += rearRight.wheelCol.rpm; count++; }
        return count == 0 ? 0f : total / count;
    }

    private static void ApplyDrivenWheelTorque(WheelAlignment wheel, float torque)
    {
        if (wheel != null && wheel.wheelCol != null) wheel.wheelCol.motorTorque = torque;
    }

    private void ApplyEngineBraking(WheelAlignment wheel)
    {
        if (wheel == null || wheel.wheelCol == null) return;
        float rpmSign = Mathf.Sign(wheel.wheelCol.rpm);
        wheel.wheelCol.motorTorque -= rpmSign * engineBrakingTorque;
    }

    private void ApplyBrakes()
    {
        SetBrakeTorque(frontLeft, frontBrakeTorque * BrakeInput, true);
        SetBrakeTorque(frontRight, frontBrakeTorque * BrakeInput, true);

        float rearTorque = rearBrakeTorque * BrakeInput;
        if (IsHandbraking) rearTorque += handbrakeTorque;
        SetBrakeTorque(rearLeft, rearTorque, !IsHandbraking);
        SetBrakeTorque(rearRight, rearTorque, !IsHandbraking);
    }

    private void SetBrakeTorque(WheelAlignment wheel, float requestedTorque, bool useAbs)
    {
        if (wheel == null || wheel.wheelCol == null) return;

        float torque = requestedTorque;
        if (useAbs && requestedTorque > 0f && wheel.wheelCol.GetGroundHit(out WheelHit hit))
        {
            float slipExcess = Mathf.InverseLerp(absSlipLimit, absSlipLimit * 1.8f, Mathf.Abs(hit.forwardSlip));
            torque *= Mathf.Lerp(1f, 0.28f, slipExcess);
        }

        wheel.wheelCol.brakeTorque = torque;
    }

    private void UpdateTyreGripAndDriftAssist()
    {
        float speedKph = RB.linearVelocity.magnitude * 3.6f;
        float rearSlip = AverageRearSlip();
        float speedFactor = Mathf.InverseLerp(driftMinSpeedKph, 70f, speedKph);
        float powerOversteer = ThrottleInput * Mathf.Abs(smoothedSteerInput) * speedFactor;
        float sustainedSlide = Mathf.InverseLerp(driftSustainSlip, 0.9f, rearSlip) * speedFactor;
        float targetDrift = Mathf.Max(powerOversteer, sustainedSlide);
        if (IsHandbraking && speedKph > 8f) targetDrift = 1f;

        rearGripBlend = Mathf.MoveTowards(rearGripBlend, targetDrift, gripChangeSpeed * Time.fixedDeltaTime);
        float targetRearStiffness = Mathf.Lerp(rearSidewaysStiffness, powerSlideRearStiffness, rearGripBlend);
        if (IsHandbraking) targetRearStiffness = handbrakeRearStiffness;

        float recovery = Mathf.InverseLerp(spinRecoveryStartAngle, spinRecoveryFullAngle, Mathf.Abs(DriftAngle));
        targetRearStiffness = Mathf.Lerp(targetRearStiffness, rearSidewaysStiffness * 1.18f, recovery);

        SetSidewaysStiffness(frontLeft, frontSidewaysStiffness);
        SetSidewaysStiffness(frontRight, frontSidewaysStiffness);
        SetSidewaysStiffness(rearLeft, targetRearStiffness);
        SetSidewaysStiffness(rearRight, targetRearStiffness);
        SetRearForwardGrip(IsHandbraking ? 0.72f : 1.24f);

        if (targetDrift > 0.25f && Mathf.Abs(DriftAngle) < spinRecoveryStartAngle)
        {
            // DriftAngle has the opposite sign to the body's yaw in a powerslide.
            float yawDirection = Mathf.Abs(DriftAngle) > 2f ? -Mathf.Sign(DriftAngle) : Mathf.Sign(smoothedSteerInput);
            RB.AddTorque(transform.up * yawDirection * driftYawAssist * targetDrift, ForceMode.Force);
        }

        if (recovery > 0f)
        {
            float yawRate = Vector3.Dot(RB.angularVelocity, transform.up);
            RB.AddTorque(transform.up * (-yawRate * spinRecoveryTorque * recovery), ForceMode.Force);
        }
    }

    private float AverageRearSlip()
    {
        float total = 0f;
        int count = 0;
        AccumulateSlip(rearLeft, ref total, ref count);
        AccumulateSlip(rearRight, ref total, ref count);
        return count == 0 ? 0f : total / count;
    }

    private static void AccumulateSlip(WheelAlignment wheel, ref float total, ref int count)
    {
        if (wheel == null || wheel.wheelCol == null) return;
        if (!wheel.wheelCol.GetGroundHit(out WheelHit hit)) return;
        total += Mathf.Max(Mathf.Abs(hit.sidewaysSlip), Mathf.Abs(hit.forwardSlip) * 0.55f);
        count++;
    }

    private static void SetSidewaysStiffness(WheelAlignment wheel, float stiffness)
    {
        if (wheel == null || wheel.wheelCol == null) return;
        WheelFrictionCurve friction = wheel.wheelCol.sidewaysFriction;
        friction.stiffness = stiffness;
        wheel.wheelCol.sidewaysFriction = friction;
    }

    private void SetRearForwardGrip(float stiffness)
    {
        SetForwardStiffness(rearLeft, stiffness);
        SetForwardStiffness(rearRight, stiffness);
    }

    private static void SetForwardStiffness(WheelAlignment wheel, float stiffness)
    {
        if (wheel == null || wheel.wheelCol == null) return;
        WheelFrictionCurve friction = wheel.wheelCol.forwardFriction;
        friction.stiffness = stiffness;
        wheel.wheelCol.forwardFriction = friction;
    }

    private void ApplyAntiRollBars()
    {
        ApplyAntiRollPair(frontLeft, frontRight, antiRollFront);
        ApplyAntiRollPair(rearLeft, rearRight, antiRollRear);
    }

    private void ApplyAntiRollPair(WheelAlignment left, WheelAlignment right, float stiffness)
    {
        if (left == null || right == null || left.wheelCol == null || right.wheelCol == null) return;

        WheelCollider leftCollider = left.wheelCol;
        WheelCollider rightCollider = right.wheelCol;
        bool leftGrounded = leftCollider.GetGroundHit(out WheelHit leftHit);
        bool rightGrounded = rightCollider.GetGroundHit(out WheelHit rightHit);

        float leftTravel = leftGrounded ? SuspensionTravel(leftCollider, leftHit) : 1f;
        float rightTravel = rightGrounded ? SuspensionTravel(rightCollider, rightHit) : 1f;
        float antiRollForce = (leftTravel - rightTravel) * stiffness;

        if (leftGrounded)
            RB.AddForceAtPosition(leftCollider.transform.up * -antiRollForce, leftCollider.transform.position);
        if (rightGrounded)
            RB.AddForceAtPosition(rightCollider.transform.up * antiRollForce, rightCollider.transform.position);
    }

    private static float SuspensionTravel(WheelCollider wheel, WheelHit hit)
    {
        float localHitY = wheel.transform.InverseTransformPoint(hit.point).y;
        return Mathf.Clamp01((-localHitY - wheel.radius) / Mathf.Max(0.01f, wheel.suspensionDistance));
    }

    private void ApplyAerodynamics()
    {
        float speed = RB.linearVelocity.magnitude;
        if (speed < 0.1f) return;

        RB.AddForce(-transform.up * aerodynamicDownforce * speed * speed, ForceMode.Force);
        RB.AddForce(-RB.linearVelocity.normalized * aerodynamicDrag * speed * speed, ForceMode.Force);
    }
}
