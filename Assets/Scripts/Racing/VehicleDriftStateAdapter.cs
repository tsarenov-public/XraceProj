using UnityEngine;
public class VehicleDriftStateAdapter : MonoBehaviour, IVehicleDriftState
{
    [SerializeField, Min(0)] private float minSpeedKmh = 25;
    [SerializeField, Range(0, 85)] private float minSlipAngle = 12;
    [SerializeField] private LayerMask groundLayers = ~0;
    private Rigidbody body;
    private WheelController controller;
    private WheelCollider[] wheels = System.Array.Empty<WheelCollider>();
    private readonly RaycastHit[] groundHits = new RaycastHit[16];
    public float SpeedKmh => body == null ? 0 : controller != null ? controller.SpeedKph : body.linearVelocity.magnitude * 3.6f;
    public float SlipAngle
    {
        get
        {
            if (body == null) return 0;
            if (controller != null) return Mathf.Abs(controller.DriftAngle);
            Vector3 velocity = Vector3.ProjectOnPlane(body.linearVelocity, Vector3.up);
            return velocity.sqrMagnitude < 0.01f ? 0 : Vector3.Angle(Vector3.ProjectOnPlane(body.transform.forward, Vector3.up), velocity);
        }
    }
    public bool IsGrounded
    {
        get
        {
            if (body == null) return false;
            if (wheels.Length > 0) { foreach (var wheel in wheels) if (wheel != null && wheel.isGrounded) return true; return false; }
            int count = Physics.RaycastNonAlloc(body.worldCenterOfMass, Vector3.down, groundHits, 2, groundLayers, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++) if (groundHits[i].collider != null && groundHits[i].collider.attachedRigidbody != body) return true;
            return false;
        }
    }
    // WheelController uses abs(localVelocity.z) for its steering assist, which
    // also reports an angle in reverse. Scoring must exclude reverse slides.
    public bool IsDrifting => body != null
        && Vector3.Dot(Vector3.ProjectOnPlane(body.linearVelocity, Vector3.up), Vector3.ProjectOnPlane(body.transform.forward, Vector3.up)) > 0
        && IsGrounded && SpeedKmh >= minSpeedKmh && SlipAngle >= minSlipAngle && SlipAngle < 85;
    public void Bind(Rigidbody target)
    {
        body = target;
        controller = target != null ? target.GetComponent<WheelController>() : null;
        wheels = target != null ? target.GetComponentsInChildren<WheelCollider>(true) : System.Array.Empty<WheelCollider>();
    }
}
