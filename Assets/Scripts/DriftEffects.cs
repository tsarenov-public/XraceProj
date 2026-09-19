using UnityEngine;

public class DriftEffects : MonoBehaviour
{
    [Header("Задние колёса (физика)")]
    public WheelCollider rearLeft, rearRight;

    [Header("Дым из-под колёс")]
    public ParticleSystem smokeRL, smokeRR;

    [Header("Условия появления дыма")]
    public float slipThreshold = 0.75f;
    public float minSpeed = 6f;
    public float minSpeedHandbrake = 4f;

    [Header("Густота")]
    public float rateMin = 30f;   // эмиссия в лёгком заносе
    public float rateMax = 80f;   // эмиссия в глубоком дрифте

    private Rigidbody rb;

    void Start()
    {
        rb = GetComponent<Rigidbody>();
        StopSmoke(smokeRL);
        StopSmoke(smokeRR);
    }

    void Update()
    {
        float speed = rb.linearVelocity.magnitude;
        bool handbrake = Input.GetKey(KeyCode.Space);

        UpdateSmoke(rearLeft, smokeRL, speed, handbrake);
        UpdateSmoke(rearRight, smokeRR, speed, handbrake);
    }

    void UpdateSmoke(WheelCollider wheel, ParticleSystem smoke, float speed, bool handbrake)
    {
        if (smoke == null || wheel == null) return;

        WheelHit hit;
        if (!wheel.GetGroundHit(out hit))
        {
            StopSmoke(smoke);
            return;
        }

        float slip = Mathf.Abs(hit.sidewaysSlip);
        bool driftSmoke = slip > slipThreshold && speed > minSpeed;
        bool handbrakeSmoke = handbrake && speed > minSpeedHandbrake;

        if (driftSmoke || handbrakeSmoke)
        {
            if (!smoke.isPlaying) smoke.Play();

            // чем глубже занос — тем гуще дым
            var emission = smoke.emission;
            emission.rateOverTimeMultiplier = Mathf.Lerp(rateMin, rateMax, Mathf.Clamp01(slip));
        }
        else
        {
            StopSmoke(smoke);
        }
    }

    void StopSmoke(ParticleSystem smoke)
    {
        if (smoke == null) return;
        // прекращаем рождение новы даём старым дожить и растаять
        if (smoke.isPlaying) smoke.Stop(true, ParticleSystemStopBehavior.StopEmitting);
    }
}