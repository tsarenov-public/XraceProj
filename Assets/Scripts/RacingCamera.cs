using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Camera))]
public class RacingCamera : MonoBehaviour
{
    [Header("Target")]
    public Transform target;
    public Rigidbody targetRb;
    [Tooltip("Camera pivot relative to the car. The vertical offset stays world-aligned so body roll does not shake the camera.")]
    public Vector3 targetOffset = new Vector3(0f, 1.15f, 0.15f);

    [Header("Chase position")]
    [Min(0.1f)] public float standDistance = 4.8f;
    [Min(0.1f)] public float highSpeedDistance = 5.35f;
    [Min(0f)] public float standHeight = 1.2f;
    [Min(0f)] public float highSpeedHeight = 1.38f;
    [Min(1f)] public float fullDistanceSpeedKph = 160f;
    [Range(0.03f, 0.6f)] public float positionSmoothTime = 0.14f;
    [Range(0f, 1f)] public float accelerationDistance = 0.1f;
    [Min(1f)] public float teleportDistance = 18f;

    [Header("Direction and drift")]
    [Tooltip("How much the camera follows the car trajectory instead of the body direction during a drift.")]
    [Range(0f, 1f)] public float trajectoryInfluence = 0.55f;
    [Min(0.1f)] public float headingSharpness = 5.5f;
    [Min(0f)] public float lookAheadDistance = 5.5f;
    [Range(0f, 0.5f)] public float velocityLookAhead = 0.14f;
    [Range(0f, 8f)] public float maxDriftRoll = 3f;
    [Range(0f, 5f)] public float accelerationPitch = 1.1f;
    [Min(0.1f)] public float rotationSharpness = 9f;

    [Header("Field of view")]
    [Range(30f, 110f)] public float minFov = 62f;
    [Range(30f, 110f)] public float maxFov = 78f;
    [Min(1f)] public float fullFovSpeedKph = 180f;
    [Range(0f, 12f)] public float driftFovBonus = 3.5f;
    [Min(0.1f)] public float fovSharpness = 3.5f;

    [Header("Obstacles")]
    [Tooltip("Layers that can push the camera forward. Everything is used by default; the car itself is ignored.")]
    public LayerMask blockingLayers = ~0;
    [Range(0.05f, 1f)] public float collisionRadius = 0.32f;
    [Range(0f, 0.5f)] public float collisionSkin = 0.08f;
    [Min(0.1f)] public float collisionReturnSharpness = 10f;

    [Header("Manual view")]
    [Tooltip("Hold the right mouse button and move the mouse to orbit around the car.")]
    public bool enableMouseOrbit = true;
    [Range(0.1f, 10f)] public float mouseSensitivityX = 3f;
    [Range(0.1f, 10f)] public float mouseSensitivityY = 2.2f;
    [Range(-45f, 0f)] public float minOrbitPitch = -12f;
    [Range(0f, 60f)] public float maxOrbitPitch = 28f;
    [Min(0f)] public float orbitRecenterDelay = 1f;
    [Min(0.1f)] public float orbitRecenterSharpness = 3.5f;
    [Tooltip("Hold to look backwards.")]
    public KeyCode rearViewKey = KeyCode.C;

    private readonly RaycastHit[] collisionHits = new RaycastHit[24];

    private Camera cameraComponent;
    private Vector3 smoothedPosition;
    private Vector3 positionVelocity;
    private Vector3 smoothedHeading;
    private Vector3 previousTargetPosition;
    private Vector3 previousVelocity;
    private float smoothedLongitudinalAcceleration;
    private float collisionDistance;
    private float currentFov;
    private float orbitYaw;
    private float orbitPitch;
    private float orbitIdleTime;
    private bool initialized;

    private void Awake()
    {
        cameraComponent = GetComponent<Camera>();

        if (target == null && transform.parent != null)
            target = transform.parent;

        if (targetRb == null && target != null)
            targetRb = target.GetComponentInParent<Rigidbody>();

        // A chase camera must live in world space. Otherwise the car movement is
        // applied once by the hierarchy and a second time by this script.
        if (target != null && transform.IsChildOf(target))
            transform.SetParent(null, true);
    }

    private void Start()
    {
        if (target != null)
            SnapToTarget();
    }

    private void LateUpdate()
    {
        if (target == null)
            return;

        float dt = Mathf.Min(Time.deltaTime, 0.05f);
        if (dt <= 0f)
            return;

        if (!initialized || Vector3.Distance(target.position, previousTargetPosition) > teleportDistance)
            SnapToTarget();

        UpdateManualOrbit(dt);

        Vector3 up = Vector3.up;
        Vector3 carForward = FlattenDirection(target.forward, up, Vector3.forward);
        Vector3 carRight = FlattenDirection(target.right, up, Vector3.right);
        Vector3 velocity = targetRb != null ? targetRb.linearVelocity : Vector3.zero;
        Vector3 horizontalVelocity = Vector3.ProjectOnPlane(velocity, up);
        float speed = horizontalVelocity.magnitude;
        float speedKph = speed * 3.6f;
        float speedT = Mathf.Clamp01(speedKph / Mathf.Max(1f, fullDistanceSpeedKph));

        Vector3 velocityDirection = speed > 0.5f ? horizontalVelocity / speed : carForward;
        bool reversing = Vector3.Dot(velocityDirection, carForward) < -0.25f;
        float driftAngle = reversing || speed < 2f
            ? 0f
            : Vector3.SignedAngle(carForward, velocityDirection, up);

        float driftT = Mathf.InverseLerp(6f, 55f, Mathf.Abs(driftAngle));
        float movementT = Mathf.InverseLerp(2f, 12f, speed);
        float headingBlend = reversing ? 0f : trajectoryInfluence * driftT * movementT;
        Vector3 desiredHeading = Vector3.Slerp(carForward, velocityDirection, headingBlend).normalized;
        smoothedHeading = Vector3.Slerp(
            smoothedHeading,
            desiredHeading,
            Damp(headingSharpness, dt)).normalized;

        Vector3 acceleration = (velocity - previousVelocity) / dt;
        float longitudinalAcceleration = Vector3.Dot(acceleration, carForward);
        longitudinalAcceleration = Mathf.Clamp(longitudinalAcceleration, -15f, 15f);
        smoothedLongitudinalAcceleration = Mathf.Lerp(
            smoothedLongitudinalAcceleration,
            longitudinalAcceleration,
            Damp(5f, dt));
        float accelerationT = Mathf.Clamp(smoothedLongitudinalAcceleration / 10f, -1f, 1f);

        bool rearView = Input.GetKey(rearViewKey);
        float viewYaw = orbitYaw + (rearView ? 180f : 0f);
        Vector3 orbitBack = Quaternion.AngleAxis(viewYaw, up) * -smoothedHeading;
        Vector3 pivot = target.position
            + carRight * targetOffset.x
            + up * targetOffset.y
            + carForward * targetOffset.z;

        float desiredDistance = Mathf.Lerp(standDistance, highSpeedDistance, speedT)
            + accelerationT * accelerationDistance;
        float desiredHeight = Mathf.Lerp(standHeight, highSpeedHeight, speedT)
            + Mathf.Tan(orbitPitch * Mathf.Deg2Rad) * desiredDistance;
        Vector3 desiredPosition = pivot + orbitBack * desiredDistance + up * desiredHeight;

        smoothedPosition = Vector3.SmoothDamp(
            smoothedPosition,
            desiredPosition,
            ref positionVelocity,
            Mathf.Max(0.03f, positionSmoothTime),
            Mathf.Infinity,
            dt);

        transform.position = ResolveCameraCollision(pivot, smoothedPosition, dt);

        Vector3 lookDirection = rearView ? -smoothedHeading : smoothedHeading;
        float baseLookAhead = Mathf.Lerp(1.2f, lookAheadDistance, speedT);
        Vector3 predictedMovement = reversing || rearView
            ? Vector3.zero
            : horizontalVelocity * velocityLookAhead;
        Vector3 lookPoint = pivot + lookDirection * baseLookAhead + predictedMovement;

        Vector3 toLookPoint = lookPoint - transform.position;
        if (toLookPoint.sqrMagnitude > 0.001f)
        {
            float roll = -Mathf.Clamp(driftAngle / 50f, -1f, 1f) * maxDriftRoll * movementT;
            float pitch = -accelerationT * accelerationPitch;
            Quaternion desiredRotation = Quaternion.LookRotation(toLookPoint, up)
                * Quaternion.Euler(pitch, 0f, roll);
            transform.rotation = Quaternion.Slerp(
                transform.rotation,
                desiredRotation,
                Damp(rotationSharpness, dt));
        }

        float fovT = Mathf.Clamp01(speedKph / Mathf.Max(1f, fullFovSpeedKph));
        float targetFov = Mathf.Lerp(minFov, maxFov, fovT)
            + driftT * movementT * driftFovBonus;
        currentFov = Mathf.Lerp(currentFov, targetFov, Damp(fovSharpness, dt));
        cameraComponent.fieldOfView = currentFov;

        previousTargetPosition = target.position;
        previousVelocity = velocity;
    }

    private void UpdateManualOrbit(float dt)
    {
        bool orbiting = enableMouseOrbit && Input.GetMouseButton(1);

        if (orbiting)
        {
            orbitYaw += Input.GetAxisRaw("Mouse X") * mouseSensitivityX;
            orbitPitch -= Input.GetAxisRaw("Mouse Y") * mouseSensitivityY;
            orbitPitch = Mathf.Clamp(orbitPitch, minOrbitPitch, maxOrbitPitch);
            orbitIdleTime = 0f;
            return;
        }

        orbitIdleTime += dt;
        if (orbitIdleTime < orbitRecenterDelay)
            return;

        float recenterT = Damp(orbitRecenterSharpness, dt);
        orbitYaw = Mathf.LerpAngle(orbitYaw, 0f, recenterT);
        orbitPitch = Mathf.Lerp(orbitPitch, 0f, recenterT);
    }

    private Vector3 ResolveCameraCollision(Vector3 pivot, Vector3 wantedPosition, float dt)
    {
        Vector3 castVector = wantedPosition - pivot;
        float wantedDistance = castVector.magnitude;
        if (wantedDistance < 0.001f)
            return pivot;

        Vector3 castDirection = castVector / wantedDistance;
        float allowedDistance = wantedDistance;
        int hitCount = Physics.SphereCastNonAlloc(
            pivot,
            collisionRadius,
            castDirection,
            collisionHits,
            wantedDistance,
            blockingLayers,
            QueryTriggerInteraction.Ignore);

        for (int i = 0; i < hitCount; i++)
        {
            Collider hitCollider = collisionHits[i].collider;
            if (hitCollider == null || IsTargetCollider(hitCollider.transform))
                continue;

            allowedDistance = Mathf.Min(
                allowedDistance,
                Mathf.Max(0.05f, collisionHits[i].distance - collisionSkin));
        }

        // Move in immediately to prevent clipping, but return smoothly when the
        // obstacle disappears so walls do not make the camera pop.
        if (allowedDistance < collisionDistance)
            collisionDistance = allowedDistance;
        else
            collisionDistance = Mathf.Lerp(
                collisionDistance,
                allowedDistance,
                Damp(collisionReturnSharpness, dt));

        collisionDistance = Mathf.Clamp(collisionDistance, 0.05f, wantedDistance);
        return pivot + castDirection * collisionDistance;
    }

    private bool IsTargetCollider(Transform candidate)
    {
        return target != null && (candidate == target || candidate.IsChildOf(target));
    }

    private void SnapToTarget()
    {
        Vector3 up = Vector3.up;
        Vector3 forward = FlattenDirection(target.forward, up, Vector3.forward);
        Vector3 right = FlattenDirection(target.right, up, Vector3.right);
        Vector3 pivot = target.position
            + right * targetOffset.x
            + up * targetOffset.y
            + forward * targetOffset.z;

        smoothedHeading = forward;
        smoothedPosition = pivot - forward * standDistance + up * standHeight;
        positionVelocity = Vector3.zero;
        collisionDistance = Vector3.Distance(pivot, smoothedPosition);
        previousTargetPosition = target.position;
        previousVelocity = targetRb != null ? targetRb.linearVelocity : Vector3.zero;
        smoothedLongitudinalAcceleration = 0f;
        currentFov = cameraComponent != null ? cameraComponent.fieldOfView : minFov;
        orbitYaw = 0f;
        orbitPitch = 0f;
        orbitIdleTime = 0f;

        transform.position = smoothedPosition;
        Vector3 lookPoint = pivot + forward * 1.2f;
        transform.rotation = Quaternion.LookRotation(lookPoint - transform.position, up);
        initialized = true;
    }

    private static Vector3 FlattenDirection(Vector3 direction, Vector3 up, Vector3 fallback)
    {
        Vector3 flat = Vector3.ProjectOnPlane(direction, up);
        return flat.sqrMagnitude > 0.0001f ? flat.normalized : fallback;
    }

    private static float Damp(float sharpness, float dt)
    {
        return 1f - Mathf.Exp(-Mathf.Max(0.01f, sharpness) * dt);
    }

    private void OnValidate()
    {
        standDistance = Mathf.Max(0.1f, standDistance);
        highSpeedDistance = Mathf.Max(0.1f, highSpeedDistance);
        standHeight = Mathf.Max(0f, standHeight);
        highSpeedHeight = Mathf.Max(0f, highSpeedHeight);
        fullDistanceSpeedKph = Mathf.Max(1f, fullDistanceSpeedKph);
        fullFovSpeedKph = Mathf.Max(1f, fullFovSpeedKph);
        maxFov = Mathf.Max(minFov, maxFov);
        teleportDistance = Mathf.Max(1f, teleportDistance);
        collisionRadius = Mathf.Max(0.05f, collisionRadius);
        maxOrbitPitch = Mathf.Max(0f, maxOrbitPitch);
        minOrbitPitch = Mathf.Min(0f, minOrbitPitch);
    }
}
