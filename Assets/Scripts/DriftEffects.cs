using UnityEngine;

public class DriftEffects : MonoBehaviour
{
    [Header("Задние колёса")]
    public WheelCollider rearLeft;
    public WheelCollider rearRight;
    public WheelController controller;

    [Header("Дым")]
    public ParticleSystem smokeRL;
    public ParticleSystem smokeRR;
    public float slipThreshold = 0.32f;
    public float minSpeedKph = 18f;
    public float rateMin = 30f;
    public float rateMax = 110f;

    private Rigidbody rb;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        if (controller == null) controller = GetComponent<WheelController>();
        PrepareSmoke(smokeRL);
        PrepareSmoke(smokeRR);
    }

    private void Start()
    {
        ClearSmoke(smokeRL);
        ClearSmoke(smokeRR);
    }

    private void Update()
    {
        if (Time.timeScale <= 0f) return;
        if (rb == null || rb.isKinematic)
        {
            StopSmoke(smokeRL);
            StopSmoke(smokeRR);
            return;
        }
        float speedKph = rb == null ? 0f : rb.linearVelocity.magnitude * 3.6f;
        bool handbrake = controller != null ? controller.IsHandbraking : Input.GetKey(KeyCode.Space);

        UpdateSmoke(rearLeft, smokeRL, speedKph, handbrake);
        UpdateSmoke(rearRight, smokeRR, speedKph, handbrake);
    }

    private void UpdateSmoke(WheelCollider wheel, ParticleSystem smoke, float speedKph, bool handbrake)
    {
        if (smoke == null || wheel == null || !wheel.enabled || !wheel.GetGroundHit(out WheelHit hit))
        {
            StopSmoke(smoke);
            return;
        }

        float slip = Mathf.Max(Mathf.Abs(hit.sidewaysSlip), Mathf.Abs(hit.forwardSlip) * 0.7f);
        bool shouldSmoke = speedKph > minSpeedKph && (slip > slipThreshold || handbrake);

        if (!shouldSmoke)
        {
            StopSmoke(smoke);
            return;
        }

        if (!smoke.isPlaying) smoke.Play();
        ParticleSystem.EmissionModule emission = smoke.emission;
        float intensity = Mathf.InverseLerp(slipThreshold, 1.2f, slip);
        emission.rateOverTimeMultiplier = Mathf.Lerp(rateMin, rateMax, intensity);
    }

    private static void StopSmoke(ParticleSystem smoke)
    {
        if (smoke != null && smoke.isPlaying)
            smoke.Stop(true, ParticleSystemStopBehavior.StopEmitting);
    }

    private static void PrepareSmoke(ParticleSystem smoke)
    {
        if (smoke == null) return;
        ParticleSystem.MainModule main = smoke.main;
        main.playOnAwake = false;
        ClearSmoke(smoke);
    }

    private static void ClearSmoke(ParticleSystem smoke)
    {
        if (smoke != null) smoke.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
    }

    private void OnDisable()
    {
        ClearSmoke(smokeRL);
        ClearSmoke(smokeRR);
    }
}
