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
    [Range(0f, 1f)] public float counterSteerAssist = 0.86f;
    public float counterSteerStartAngle = 4f;
    public float counterSteerMaxAngle = 34f;
    [Tooltip("Минимальный доступный угол контрруления в заносе на высокой скорости")]
    public float minimumDriftSteerAngle = 26f;
    [Tooltip("Насколько заранее ассистент прогнозирует развитие заноса")]
    [Range(0f, 0.35f)] public float counterSteerPrediction = 0.19f;
    public float wheelbase = 3.07f;
    public float trackWidth = 1.57f;

    [Header("Двигатель")]
    public float peakEngineTorque = 460f;
    public float peakTorqueRpm = 5200f;
    public float idleRpm = 900f;
    public float redlineRpm = 7800f;
    public float engineRpmResponse = 9f;
    [Tooltip("Скорость реакции дросселя. Сглаживание убирает мгновенный игрушечный рывок при нажатии газа")]
    public float throttleResponse = 3.8f;
    public float engineBrakingTorque = 90f;
    [Range(0.5f, 1f)] public float drivetrainEfficiency = 0.88f;
    [Tooltip("Короткое отключение тяги на отсечке не даёт колёсам раскручиваться выше диапазона текущей передачи")]
    public float revLimiterCutDuration = 0.055f;
    [Tooltip("Мягкое ограничение максимальной скорости автомобиля. Тяга плавно уменьшается перед этим значением")]
    public float maximumVehicleSpeedKph = 195f;
    public float topSpeedTorqueFadeRangeKph = 14f;

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
    [Tooltip("Зацеп задней оси после выхода на устойчивый угол заноса")]
    public float powerSlideRearStiffness = 0.86f;
    [Tooltip("Кратковременное снижение зацепа при намеренной инициации газом и рулём")]
    public float driftEntryRearStiffness = 0.69f;
    public float driftEntryCompleteAngle = 18f;
    public float handbrakeRearStiffness = 0.36f;
    public float gripChangeSpeed = 5f;
    [Tooltip("Медленный возврат сцепления не даёт машине резко выстреливать из заноса")]
    public float gripRecoverySpeed = 1.45f;
    public float driftMinSpeedKph = 22f;
    public float driftSustainSlip = 0.22f;
    [Tooltip("Небольшая помощь вращению машины при начале силового заноса")]
    public float driftYawAssist = 640f;
    [Tooltip("После этого угла помощь инициации плавно отключается")]
    public float driftYawAssistFadeAngle = 32f;
    [Range(0f, 1f)] public float driftStabilityAssist = 0.84f;
    [Tooltip("С этого угла система начинает плавно гасить лишнее вращение кузова")]
    public float driftStabilityStartAngle = 30f;
    public float driftStabilityTorque = 1050f;
    public float driftYawDamping = 820f;
    [Tooltip("Допустимая скорость вращения кузова в устойчивом заносе (радиан/с)")]
    public float maxDriftYawRate = 1.32f;
    [Tooltip("После этого угла ассистент начинает мягко ловить разворот")]
    public float spinRecoveryStartAngle = 42f;
    public float spinRecoveryFullAngle = 65f;
    public float spinRecoveryTorque = 2900f;
    [Tooltip("На большом угле газ немного ограничивается, чтобы задние колёса снова получили боковой зацеп")]
    public float spinPowerLimitStartAngle = 38f;
    public float spinPowerLimitFullAngle = 63f;
    [Range(0.2f, 1f)] public float minimumDriftPower = 0.52f;

    [Header("Шасси")]
    public float antiRollFront = 7200f;
    public float antiRollRear = 6100f;
    public float aerodynamicDownforce = 8f;
    public float aerodynamicDrag = 0.42f;
    [Tooltip("Сопротивление шин и трансмиссии в ньютонах")]
    public float rollingResistance = 125f;
    [Tooltip("Дополнительное сопротивление качению на каждый м/с скорости")]
    public float rollingResistancePerSpeed = 2.5f;
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
    private float driftEntryIntent;
    private float shiftTimer;
    private float clutch = 1f;
    private float driveThrottle;
    private float revLimiterTimer;
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
            float yawRateDegrees = Vector3.Dot(RB.angularVelocity, transform.up) * Mathf.Rad2Deg;
            float predictedDriftAngle = DriftAngle - yawRateDegrees * counterSteerPrediction;
            float assistStrength = Mathf.InverseLerp(counterSteerStartAngle, 28f, Mathf.Abs(predictedDriftAngle));
            maximumSteer = Mathf.Max(maximumSteer, Mathf.Lerp(maximumSteer, minimumDriftSteerAngle, assistStrength));
            float counterAngle = Mathf.Clamp(predictedDriftAngle, -counterSteerMaxAngle, counterSteerMaxAngle);
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
        driveThrottle = Mathf.MoveTowards(
            driveThrottle,
            ThrottleInput,
            Mathf.Max(0.1f, throttleResponse) * Time.fixedDeltaTime);

        float drivenWheelRpm = AverageDrivenWheelRpm();
        float ratio = GetActiveGearRatio();
        float coupledRpm = Mathf.Abs(drivenWheelRpm * ratio * finalDriveRatio);
        float freeRevRpm = Mathf.Lerp(idleRpm, redlineRpm * 0.94f, driveThrottle);
        float targetRpm = currentGear == 0 || clutch < 0.15f
            ? freeRevRpm
            : Mathf.Max(idleRpm, coupledRpm);

        EngineRpm = Mathf.Lerp(EngineRpm, targetRpm, 1f - Mathf.Exp(-engineRpmResponse * Time.fixedDeltaTime));
        EngineRpm = Mathf.Clamp(EngineRpm, idleRpm, redlineRpm * 1.015f);

        if (revLimiterTimer > 0f)
            revLimiterTimer -= Time.fixedDeltaTime;

        bool wheelSpeedAtLimiter = currentGear != 0
            && clutch > 0.5f
            && coupledRpm >= redlineRpm * 0.995f;
        if (wheelSpeedAtLimiter && driveThrottle > 0.05f)
            revLimiterTimer = Mathf.Max(revLimiterTimer, revLimiterCutDuration);

        float motorTorquePerWheel = 0f;
        if (currentGear != 0
            && clutch > 0f
            && revLimiterTimer <= 0f
            && coupledRpm < redlineRpm)
        {
            float engineTorque = EvaluateEngineTorque(EngineRpm) * driveThrottle;
            motorTorquePerWheel = engineTorque * ratio * finalDriveRatio * drivetrainEfficiency * clutch * 0.5f;
            motorTorquePerWheel *= GetDriftPowerScale();

            float absoluteForwardSpeed = Mathf.Abs(ForwardSpeedKph);
            float speedFadeStart = Mathf.Max(0f, maximumVehicleSpeedKph - topSpeedTorqueFadeRangeKph);
            float topSpeedFade = 1f - Mathf.InverseLerp(
                speedFadeStart,
                Mathf.Max(speedFadeStart + 1f, maximumVehicleSpeedKph),
                absoluteForwardSpeed);
            motorTorquePerWheel *= topSpeedFade;
        }
        else if (wheelSpeedAtLimiter && coupledRpm > redlineRpm * 1.005f)
        {
            // A selected gear mechanically resists wheel overspeed instead of
            // letting the car coast far beyond that gear's usable RPM range.
            motorTorquePerWheel = -Mathf.Sign(drivenWheelRpm) * engineBrakingTorque * 0.5f;
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
        bool movingForward = Vector3.Dot(RB.linearVelocity, transform.forward) > 1f;
        float rearSlip = AverageRearSlip();
        float speedFactor = Mathf.InverseLerp(driftMinSpeedKph, 70f, speedKph);
        float entrySpeedFactor = Mathf.InverseLerp(driftMinSpeedKph, 50f, speedKph);
        float powerOversteer = ThrottleInput * Mathf.Abs(smoothedSteerInput) * speedFactor;
        float sustainedSlide = Mathf.InverseLerp(driftSustainSlip, 0.9f, rearSlip) * speedFactor;
        float entryAngleFade = 1f - Mathf.InverseLerp(
            driftEntryCompleteAngle * 0.55f,
            driftEntryCompleteAngle,
            Mathf.Abs(DriftAngle));
        driftEntryIntent = Mathf.InverseLerp(0.18f, 0.72f, Mathf.Abs(smoothedSteerInput))
            * Mathf.InverseLerp(0.25f, 0.85f, ThrottleInput)
            * entrySpeedFactor
            * entryAngleFade;
        float targetDrift = movingForward
            ? Mathf.Max(driftEntryIntent, Mathf.Max(powerOversteer, sustainedSlide))
            : 0f;
        if (IsHandbraking && speedKph > 8f) targetDrift = 1f;

        float gripBlendSpeed = targetDrift > rearGripBlend ? gripChangeSpeed : gripRecoverySpeed;
        rearGripBlend = Mathf.MoveTowards(rearGripBlend, targetDrift, gripBlendSpeed * Time.fixedDeltaTime);
        float slidingRearStiffness = Mathf.Lerp(
            powerSlideRearStiffness,
            driftEntryRearStiffness,
            driftEntryIntent);
        float targetRearStiffness = Mathf.Lerp(rearSidewaysStiffness, slidingRearStiffness, rearGripBlend);
        if (IsHandbraking) targetRearStiffness = handbrakeRearStiffness;

        float recovery = Mathf.InverseLerp(spinRecoveryStartAngle, spinRecoveryFullAngle, Mathf.Abs(DriftAngle));
        if (!IsHandbraking)
        {
            float recoveryGrip = rearSidewaysStiffness * 1.06f;
            targetRearStiffness = Mathf.Lerp(
                targetRearStiffness,
                recoveryGrip,
                recovery * driftStabilityAssist);
        }

        SetSidewaysStiffness(frontLeft, frontSidewaysStiffness);
        SetSidewaysStiffness(frontRight, frontSidewaysStiffness);
        SetSidewaysStiffness(rearLeft, targetRearStiffness);
        SetSidewaysStiffness(rearRight, targetRearStiffness);
        SetRearForwardGrip(IsHandbraking ? 0.72f : 1.24f);

        bool steeringIntoSlide = Mathf.Abs(DriftAngle) < 2f
            || Mathf.Sign(smoothedSteerInput) == -Mathf.Sign(DriftAngle);
        if (!IsHandbraking
            && movingForward
            && steeringIntoSlide
            && driftEntryIntent > 0.05f
            && Mathf.Abs(DriftAngle) < driftYawAssistFadeAngle)
        {
            // DriftAngle has the opposite sign to the body's yaw in a powerslide.
            float yawDirection = Mathf.Abs(DriftAngle) > 2f ? -Mathf.Sign(DriftAngle) : Mathf.Sign(smoothedSteerInput);
            float initiationFade = 1f - Mathf.InverseLerp(
                driftYawAssistFadeAngle * 0.55f,
                driftYawAssistFadeAngle,
                Mathf.Abs(DriftAngle));
            RB.AddTorque(
                transform.up * yawDirection * driftYawAssist * driftEntryIntent * initiationFade,
                ForceMode.Force);
        }

        ApplyDriftStability(speedKph, recovery);
    }

    private float GetDriftPowerScale()
    {
        if (currentGear <= 0 || IsHandbraking || driftStabilityAssist <= 0f) return 1f;

        float danger = Mathf.InverseLerp(
            spinPowerLimitStartAngle,
            spinPowerLimitFullAngle,
            Mathf.Abs(DriftAngle));
        return Mathf.Lerp(1f, minimumDriftPower, danger * driftStabilityAssist);
    }

    private void ApplyDriftStability(float speedKph, float recovery)
    {
        float absoluteAngle = Mathf.Abs(DriftAngle);
        if (speedKph < driftMinSpeedKph || absoluteAngle < counterSteerStartAngle) return;

        float slideAmount = Mathf.InverseLerp(counterSteerStartAngle, driftStabilityStartAngle, absoluteAngle);
        float angleCorrection = Mathf.InverseLerp(
            driftStabilityStartAngle,
            spinRecoveryFullAngle,
            absoluteAngle);
        float yawRate = Vector3.Dot(RB.angularVelocity, transform.up);
        float excessYawRate = Mathf.Max(0f, Mathf.Abs(yawRate) - maxDriftYawRate);
        float entrySuppression = 1f - driftEntryIntent * 0.82f;

        float stabilizingTorque = -yawRate * driftYawDamping * slideAmount * entrySuppression;
        stabilizingTorque += Mathf.Sign(DriftAngle) * driftStabilityTorque * angleCorrection;
        stabilizingTorque -= Mathf.Sign(yawRate) * excessYawRate * spinRecoveryTorque;
        stabilizingTorque -= yawRate * spinRecoveryTorque * recovery;

        float handbrakeInfluence = IsHandbraking ? 0.28f : 1f;
        float maximumTorque = spinRecoveryTorque * 1.35f;
        stabilizingTorque = Mathf.Clamp(stabilizingTorque, -maximumTorque, maximumTorque);
        RB.AddTorque(
            transform.up * stabilizingTorque * driftStabilityAssist * handbrakeInfluence,
            ForceMode.Force);
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
        if (HasGroundedWheel())
        {
            float rollingForce = rollingResistance + rollingResistancePerSpeed * speed;
            RB.AddForce(-RB.linearVelocity.normalized * rollingForce, ForceMode.Force);
        }
    }

    private bool HasGroundedWheel()
    {
        if (steerableWheels == null) return false;

        foreach (WheelAlignment wheel in steerableWheels)
        {
            if (wheel != null && wheel.wheelCol != null && wheel.wheelCol.isGrounded)
                return true;
        }

        return false;
    }
}
