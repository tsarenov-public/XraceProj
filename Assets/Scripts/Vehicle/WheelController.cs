using UnityEngine;

public class WheelController : MonoBehaviour
{
    [Header("Колёса (порядок: FL, FR, RL, RR)")]
    public WheelAlignment[] steerableWheels;

    [Header("Руль")]
    public float wheelRotateSpeed = 20f;
    public float wheelSteeringAngle = 40f;
    [Tooltip("Скорость (м/с), на которой руль становится минимально резким")]
    public float steerFadeSpeed = 40f;
    [Tooltip("Множитель руля на максимальной скорости (0.1-1)")]
    [Range(0.1f, 1f)] public float minSteerFactor = 0.35f;

    [Header("Мотор")]
    public float wheelAcceleration = 30f;
    public float wheelMaxSpeed = 1000f;

    [Header("Тормоз и задний ход")]
    [Tooltip("Сила тормоза на S / стрелка вниз")]
    public float brakeTorque = 1500f;
    [Tooltip("Клавиша заднего хода (отдельно от тормоза)")]
    public KeyCode reverseKey = KeyCode.LeftShift;
    [Tooltip("Мощность заднего хода (слабая)")]
    public float reverseTorque = 400f;
    [Tooltip("Максимальная скорость заднего хода (м/с)")]
    public float reverseMaxSpeed = 8f;

    [Header("Ручник (Space)")]
    public float handbrakeStiffness = 0.3f;
    public float handbrakeForce = 1200f;

    [Header("Drift assist (вход и удержание заноса)")]
    [Tooltip("Боковое сцепление задних колёс в заносе (ниже = скользче)")]
    [Range(0.05f, 1f)] public float driftStiffness = 0.5f;
    [Tooltip("Как быстро занос включается")]
    public float driftEngageSpeed = 6f;
    [Tooltip("Как быстро сцепление возвращается после заноса")]
    public float driftReleaseSpeed = 3f;
    [Tooltip("Скольжение, при котором занос самоподдерживается")]
    public float driftSustainSlip = 0.25f;
    [Tooltip("Насколько выкрутить руль, чтобы войти в дрифт на скорости")]
    [Range(0f, 1f)] public float driftSteerThreshold = 0.4f;
    [Tooltip("Минимальная скорость для входа рулём")]
    public float driftMinSpeed = 6f;

    [Header("Анти-крены (стабилизаторы)")]
    public float antiRollFront = 5000f;
    public float antiRollRear = 5000f;

    [Header("Защита от спина")]
    [Tooltip("Максимальный угол заноса (градусов). Выше — сцепление возвращается")]
    public float maxDriftAngle = 55f;
    [Tooltip("Демпфер рыскания: сопротивляется вращению. Выше = стабильнее")]
    public float yawDamping = 1000f;

    [Header("Физика")]
    public Rigidbody RB;

    public float Horizontal { get; private set; }
    public float Vertical { get; private set; }

    private float driftFactor; // 0..1, сглаженная "глубина" дрифта
    private float spinRisk;    // 0..1, риск разворота на 180

    void FixedUpdate()
    {
        ReadInput();
        Vector3 vel = RB.linearVelocity;
        float speed = vel.magnitude;

        // Риск спина: угол между вектором скорости и носом машины
        spinRisk = 0f;
        if (speed > 4f && !Input.GetKey(KeyCode.Space))
        {
            float driftAngle = Mathf.Abs(Vector3.SignedAngle(vel, transform.forward, Vector3.up));
            spinRisk = Mathf.Clamp01((driftAngle - maxDriftAngle) / 25f);

            // Демпфер рыскания: гасим вращение вокруг своей оси
            RB.AddTorque(transform.up * (-RB.angularVelocity.y * yawDamping));
        }

        UpdateDriftFactor(speed);
        UpdateSteering(speed);
        UpdateMotorAndBrake(speed);
        UpdateRearGrip();
        ApplyAntiRoll();
    }

    void ReadInput()
    {
        Horizontal = Input.GetAxis("Horizontal");
        Vertical = Input.GetAxis("Vertical");
    }

    // #### DRIFT ASSIST: плавный вход + самоподдержка ####
    void UpdateDriftFactor(float speed)
    {
        bool handbrake = Input.GetKey(KeyCode.Space);

        float rearSlip = AverageRearSlip();
        float steerAbs = Mathf.Abs(Horizontal);

        float target = 0f;
        if (handbrake)
        {
            target = 1f;
        }
        else
        {
            // Самоподдержка: если уже скользим — держим занос
            float sustain = Mathf.Clamp01((rearSlip - driftSustainSlip) / (0.6f - driftSustainSlip));
            // Вход: сильно выкрутил руль на скорости — помогаем сорваться
            float entry = (steerAbs > driftSteerThreshold && speed > driftMinSpeed) ? steerAbs : 0f;
            target = Mathf.Max(sustain, entry);
        }

        // На грани спина — не даём дрифт-ассисту держать скольжение
        target = Mathf.Min(target, 1f - spinRisk);

        float rate = target > driftFactor ? driftEngageSpeed : driftReleaseSpeed;
        driftFactor = Mathf.MoveTowards(driftFactor, target, rate * Time.fixedDeltaTime);
    }

    float AverageRearSlip()
    {
        float sum = 0f; int n = 0;
        for (int i = 0; i < steerableWheels.Length; i++)
        {
            if (steerableWheels[i].steerable) continue;
            WheelHit hit;
            if (steerableWheels[i].wheelCol.GetGroundHit(out hit))
            {
                sum += Mathf.Abs(hit.sidewaysSlip);
                n++;
            }
        }
        return n > 0 ? sum / n : 0f;
    }

    // #### РУЛЬ: скоростезависимый ####
    void UpdateSteering(float speed)
    {
        float speedFactor = Mathf.Clamp01(speed / steerFadeSpeed);
        float maxSteer = wheelSteeringAngle * Mathf.Lerp(1f, minSteerFactor, speedFactor);

        for (int i = 0; i < steerableWheels.Length; i++)
        {
            WheelAlignment w = steerableWheels[i];
            w.steeringAngle = Mathf.LerpAngle(w.steeringAngle, 0, Time.fixedDeltaTime * wheelRotateSpeed);

            if (Horizontal > 0.1f)
                w.steeringAngle = Mathf.LerpAngle(w.steeringAngle, -maxSteer, Time.fixedDeltaTime * wheelRotateSpeed);
            if (Horizontal < -0.1f)
                w.steeringAngle = Mathf.LerpAngle(w.steeringAngle, maxSteer, Time.fixedDeltaTime * wheelRotateSpeed);
        }
    }

    // #### МОТОР + ТОРМОЗ + ЗАДНИЙ ХОД ####
    void UpdateMotorAndBrake(float speed)
    {
        float throttle = Mathf.Max(0f, Vertical);          // W
        float brake = Mathf.Max(0f, -Vertical);           // S = тормоз
        bool wantReverse = Input.GetKey(reverseKey);

        for (int i = 0; i < steerableWheels.Length; i++)
        {
            WheelAlignment w = steerableWheels[i];

            // базовое затухание момента
            w.wheelCol.motorTorque = Mathf.Lerp(w.wheelCol.motorTorque, 0f, Time.fixedDeltaTime * wheelAcceleration);

            // вперёд
            if (throttle > 0.1f && !wantReverse)
                w.wheelCol.motorTorque = Mathf.Lerp(w.wheelCol.motorTorque, wheelMaxSpeed * throttle, Time.fixedDeltaTime * wheelAcceleration);

            // задний ход (только когда почти стоим)
            if (wantReverse && speed < reverseMaxSpeed)
                w.wheelCol.motorTorque = Mathf.Lerp(w.wheelCol.motorTorque, -reverseTorque, Time.fixedDeltaTime * wheelAcceleration);

            // тормоз: S + ручник на задние
            float bt = 0f;
            if (brake > 0.1f) bt += brakeTorque * brake;
            if (!w.steerable && Input.GetKey(KeyCode.Space)) bt += handbrakeForce;
            w.wheelCol.brakeTorque = bt;
        }

        // лёгкое демпфирование при торможении
        RB.linearDamping = brake > 0.1f ? 0.3f : 0f;
    }

    // #### СЦЕПЛЕНИЕ ЗАДНИХ: плавно падает в дрифте ####
    void UpdateRearGrip()
    {
        bool handbrake = Input.GetKey(KeyCode.Space);
        float rearStiff = Mathf.Lerp(1f, driftStiffness, driftFactor);
        if (handbrake) rearStiff = Mathf.Min(rearStiff, handbrakeStiffness);

        // У запредельного угла возвращаем сцепление — машина "ловит" себя сама
        rearStiff = Mathf.Lerp(rearStiff, 1f, spinRisk);

        for (int i = 0; i < steerableWheels.Length; i++)
        {
            if (steerableWheels[i].steerable) continue;
            WheelFrictionCurve side = steerableWheels[i].wheelCol.sidewaysFriction;
            side.stiffness = rearStiff;
            steerableWheels[i].wheelCol.sidewaysFriction = side;
        }
    }

    // #### АНТИ-КРЕНЫ: не даём кузову валиться и переворачиваться ####
    void ApplyAntiRoll()
    {
        if (steerableWheels.Length < 4) return;
        ApplyAntiRollPair(steerableWheels[0].wheelCol, steerableWheels[1].wheelCol, antiRollFront);
        ApplyAntiRollPair(steerableWheels[2].wheelCol, steerableWheels[3].wheelCol, antiRollRear);
    }

    void ApplyAntiRollPair(WheelCollider L, WheelCollider R, float stiffness)
    {
        WheelHit hitL, hitR;
        bool gL = L.GetGroundHit(out hitL);
        bool gR = R.GetGroundHit(out hitR);

        // В воздухе = подвеска полностью распущена (travel = 1)
        float travelL = 1f, travelR = 1f;
        if (gL) travelL = 1f - SuspensionCompression01(L, hitL);
        if (gR) travelR = 1f - SuspensionCompression01(R, hitR);

        float diff = (travelL - travelR) * stiffness;

        // Сила, противодействующая крену: поджатую сторону толкаем вверх, висящую — вниз
        if (gL) RB.AddForceAtPosition(-transform.up * diff, L.transform.position);
        if (gR) RB.AddForceAtPosition(transform.up * diff, R.transform.position);
    }

    // Насколько поджата подвеска (0 = распущена, 1 = полностью сжата)
    float SuspensionCompression01(WheelCollider wheel, WheelHit hit)
    {
        float d = Vector3.Dot(wheel.transform.position - hit.point, wheel.transform.up);
        return Mathf.Clamp01(1f - (d - wheel.radius) / wheel.suspensionDistance);
    }
}