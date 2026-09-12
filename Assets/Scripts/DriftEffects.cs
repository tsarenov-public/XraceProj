using UnityEngine;

public class DriftEffects : MonoBehaviour
{
    [Header("Задние колёса (физика)")]
    public WheelCollider rearLeft, rearRight;

    [Header("Дым из-под колёс")]
    public ParticleSystem smokeRL, smokeRR;

    [Header("Условия дыма")]
    [Tooltip("Насколько сильным должен быть занос (0-1). Выше = только настоящий дрифт")]
    public float slipThreshold = 0.75f;
    [Tooltip("Минимальная скорость для дыма (м/с). Ниже = дыма нет")]
    public float minSpeed = 6f;

    private Rigidbody rb;

    void Start()
    {
        rb = GetComponent<Rigidbody>();
    }

    void Update()
    {
        float speed = rb.linearVelocity.magnitude;

        TryEmit(rearLeft, smokeRL, speed);
        TryEmit(rearRight, smokeRR, speed);
    }

    void TryEmit(WheelCollider wheel, ParticleSystem smoke, float speed)
    {
        if (smoke == null || wheel == null) return;

        // Колесо в воздухе — дыма нет
        WheelHit hit;
        if (!wheel.GetGroundHit(out hit)) return;

        float slip = Mathf.Abs(hit.sidewaysSlip);

        // Дым ТОЛЬКО при настоящем заносе на скорости
        if (slip > slipThreshold && speed > minSpeed)
        {
            // Чем глубже занос — тем гуще дым, но без перебора
            int count = Mathf.Clamp(Mathf.RoundToInt((slip - slipThreshold) * 4f), 1, 3);
            smoke.Emit(count);
        }
    }
}