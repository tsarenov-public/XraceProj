using UnityEngine;
public class CarStopOnFinish : MonoBehaviour
{
    [SerializeField, Range(0.6f, 0.8f)] private float stopDuration = 0.75f;
    private Rigidbody body;
    private WheelController controller;
    private WheelCollider[] wheels;
    private bool stopping;
    private float elapsed;
    private Vector3 initialVelocity;
    private Vector3 initialAngularVelocity;
    public void Bind(Rigidbody target)
    {
        body = target;
        controller = body != null ? body.GetComponent<WheelController>() : null;
        wheels = body != null ? body.GetComponentsInChildren<WheelCollider>(true) : System.Array.Empty<WheelCollider>();
    }
    public void StopCar()
    {
        if (stopping || body == null || body.isKinematic) return;
        // WheelController exposes read-only brake input, so use its existing wheel brakes
        // and the permitted velocity fallback; do not modify suspension/friction settings.
        if (controller != null) controller.enabled = false;
        foreach (var wheel in wheels)
        {
            if (wheel == null) continue;
            wheel.motorTorque = 0;
            wheel.brakeTorque = controller != null ? controller.handbrakeTorque : 4200;
        }
        initialVelocity = body.linearVelocity;
        initialAngularVelocity = body.angularVelocity;
        stopping = true;
    }
    private void FixedUpdate()
    {
        if (!stopping || body == null || body.isKinematic) return;
        elapsed += Time.fixedDeltaTime;
        float remaining = Mathf.Clamp01(1 - elapsed / Mathf.Max(0.01f, stopDuration));
        body.linearVelocity = initialVelocity * remaining;
        body.angularVelocity = initialAngularVelocity * remaining;
        if (remaining <= 0)
        {
            // Wheel physics must stop before the body becomes kinematic. Keep the
            // wheel objects and suspension settings intact for the next fresh car.
            foreach (var wheel in wheels) if (wheel != null) wheel.enabled = false;
            body.isKinematic = true;
            stopping = false;
        }
    }
}
