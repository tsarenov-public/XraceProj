using UnityEngine;

public class SkidMarks : MonoBehaviour
{
    [Header("Задние колёса (физика)")]
    public WheelCollider rearLeft, rearRight;

    [Header("Следы")]
    public TrailRenderer trailRL, trailRR;

    [Header("Условия появления следа")]
    public float slipThreshold = 0.6f;  // минимальное скольжение для следа
    public float minSpeed = 5f;         // минимальная скорость (м/с)

    [Header("Ширина следа")]
    public float minWidth = 0.18f;      // ширина в лёгком заносе
    public float maxWidth = 0.3f;       // ширина в глубоком дрифте

    private Rigidbody rb;

    void Start()
    {
        rb = GetComponent<Rigidbody>();
        ResetTrail(trailRL);
        ResetTrail(trailRR);
    }

    void Update()
    {
        if (rb == null || Time.timeScale <= 0f) return;
        if (rb.isKinematic)
        {
            ResetTrail(trailRL);
            ResetTrail(trailRR);
            return;
        }
        float speed = rb.linearVelocity.magnitude;
        UpdateTrail(rearLeft, trailRL, speed);
        UpdateTrail(rearRight, trailRR, speed);
    }

    void UpdateTrail(WheelCollider wheel, TrailRenderer trail, float speed)
    {
        if (trail == null || wheel == null) return;
        if (!wheel.enabled) { trail.emitting = false; return; }

        // Колесо в воздухе след не рисуется
        WheelHit hit;
        if (!wheel.GetGroundHit(out hit))
        {
            trail.emitting = false;
            return;
        }

        float slip = Mathf.Abs(hit.sidewaysSlip);
        bool skidding = slip > slipThreshold && speed > minSpeed;

        trail.emitting = skidding;

        // Чем сильнее занос тем шире и злее след
        if (skidding)
        {
            trail.widthMultiplier = Mathf.Lerp(minWidth, maxWidth, Mathf.Clamp01(slip));
        }
    }

    private void OnDisable()
    {
        ResetTrail(trailRL);
        ResetTrail(trailRR);
    }

    private static void ResetTrail(TrailRenderer trail)
    {
        if (trail == null) return;
        trail.emitting = false;
        trail.Clear();
    }
}
