using UnityEngine;

[RequireComponent(typeof(WheelCollider))]
public class WheelSkid : MonoBehaviour
{
    public AudioSource SkidSound;
    public float SkidSoundMultiplyer = 1f;
    [SerializeField] private Rigidbody rb;
    [SerializeField] private Skidmarks skidmarksController;

    public float skidSlipThreshold = 0.28f;
    public float fullSkidSlip = 1.15f;

    private WheelCollider wheelCollider;
    private WheelController controller;
    private int lastSkid = -1;
    private float lastFixedUpdateTime;

    private void Awake()
    {
        wheelCollider = GetComponent<WheelCollider>();
        if (rb == null) rb = GetComponentInParent<Rigidbody>();
        controller = GetComponentInParent<WheelController>();
        lastFixedUpdateTime = Time.time;
    }

    private void FixedUpdate()
    {
        lastFixedUpdateTime = Time.time;
    }

    private void LateUpdate()
    {
        if (!wheelCollider.GetGroundHit(out WheelHit hit))
        {
            EndSkid();
            return;
        }

        float sidewaysSlip = Mathf.Abs(hit.sidewaysSlip);
        float forwardSlip = Mathf.Abs(hit.forwardSlip) * 0.65f;
        float totalSlip = Mathf.Max(sidewaysSlip, forwardSlip);
        bool handbrakeSlip = controller != null && controller.IsHandbraking &&
                             rb != null && rb.linearVelocity.magnitude > 4f;

        if (totalSlip < skidSlipThreshold && !handbrakeSlip)
        {
            EndSkid();
            return;
        }

        float visibleSlip = Mathf.Max(totalSlip, handbrakeSlip ? 0.5f : 0f);
        float intensity = Mathf.InverseLerp(skidSlipThreshold, fullSkidSlip, visibleSlip);

        if (skidmarksController != null && rb != null)
        {
            Vector3 skidPoint = hit.point + rb.linearVelocity * (Time.time - lastFixedUpdateTime);
            lastSkid = skidmarksController.AddSkidMark(skidPoint, hit.normal, intensity, lastSkid);
        }

        if (SkidSound != null)
        {
            SkidSound.volume = intensity / Mathf.Max(0.01f, SkidSoundMultiplyer);
            if (!SkidSound.isPlaying) SkidSound.Play();
        }
    }

    private void EndSkid()
    {
        lastSkid = -1;
        if (SkidSound != null) SkidSound.volume = 0f;
    }
}
