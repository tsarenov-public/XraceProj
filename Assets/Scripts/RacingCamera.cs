using UnityEngine;

public class RacingCamera : MonoBehaviour
{
    [Header("Цель")]
    public Transform target;
    public Rigidbody targetRb;

    [Header("Позиция")]
    [Tooltip("Высота камеры, когда машина стоит")]
    public float heightStand = 3.5f;
    [Tooltip("Высота камеры в движении (ниже = динамичнее)")]
    public float heightDrive = 2.6f;
    [Tooltip("Дистанция, когда машина стоит")]
    public float distanceStand = 9f;
    [Tooltip("Дистанция в движении (ближе = агрессивнее)")]
    public float distanceDrive = 5.5f;
    [Tooltip("Скорость (м/с), при которой камера полностью 'в движении'")]
    public float maxDistanceSpeed = 25f;
    [Tooltip("Плавность следования позиции (меньше = резче)")]
    public float positionSmoothTime = 0.15f;

    [Header("Взгляд")]
    [Tooltip("Высота точки взгляда над машиной")]
    public float lookHeight = 1.5f;
    [Tooltip("Как далеко впереди машины точка взгляда")]
    public float lookDistance = 8f;
    [Tooltip("0 = по вектору движения, 1 = по носу машины")]
    [Range(0f, 1f)] public float forwardLookBias = 0.7f;
    [Tooltip("Плавность поворота камеры")]
    public float rotationSmoothTime = 0.12f;

    [Header("Динамика и FOV")]
    public float minFov = 60f;
    public float maxFov = 85f;
    public float fovSmoothTime = 0.3f;
    [Tooltip("Крен камеры в заносе (градусы). 0 = выключить")]
    public float driftRollMax = 4f;

    [Header("Препятствия")]
    [Tooltip("Слои препятствий (стены трассы)")]
    public LayerMask blockingLayers = 0;
    public float collisionRadius = 0.3f;

    private Vector3 smoothPosition;
    private Vector3 smoothVelocity;
    private float currentFov;
    private Camera cam;
    private float localVelX;

    void Start()
    {
        cam = GetComponent<Camera>();
        if (cam == null) cam = gameObject.AddComponent<Camera>();

        currentFov = cam.fieldOfView;

        if (target != null)
        {
            smoothPosition = target.position - target.forward * distanceStand + Vector3.up * heightStand;
            transform.position = smoothPosition;
        }
    }

    void LateUpdate()
    {
        if (target == null) return;

        // 1 Скорость и направление
        float speed = 0f;
        Vector3 velocityDir = target.forward;
        localVelX = 0f;

        if (targetRb != null)
        {
            Vector3 vel = targetRb.linearVelocity;
            speed = vel.magnitude;

            Vector3 localVel = target.InverseTransformDirection(vel);
            localVelX = localVel.x;

            if (localVel.z < -1f)
            {
                velocityDir = target.forward;
            }
            else if (speed > 1f)
            {
                velocityDir = vel.normalized;
            }
        }

        // 2 Динамические дистанция и высота 
        float t = Mathf.Clamp01(speed / maxDistanceSpeed);
        float targetDistance = Mathf.Lerp(distanceStand, distanceDrive, t);
        float targetHeight = Mathf.Lerp(heightStand, heightDrive, t);

        // 3 Желаемая позиция позади машины 
        Vector3 desiredPos = target.position - velocityDir * targetDistance + Vector3.up * targetHeight;

        // 4 Плавное следование 
        smoothPosition = Vector3.SmoothDamp(smoothPosition, desiredPos, ref smoothVelocity, positionSmoothTime);

        // если камера вдруг оказалась внутри— выталкиваем
        if (Vector3.Distance(smoothPosition, target.position) < 2f)
        {
            smoothPosition = target.position - target.forward * distanceStand + Vector3.up * heightStand;
        }

        // 5 Препятствия луч от машины к камере 
        if (blockingLayers != 0)
        {
            Vector3 from = target.position + Vector3.up * lookHeight;
            Vector3 dirToCam = smoothPosition - from;
            float dist = dirToCam.magnitude;

            if (dist > 0.01f)
            {
                dirToCam /= dist;
                if (Physics.Raycast(from, dirToCam, out RaycastHit hit, dist, blockingLayers))
                {
                    smoothPosition = from + dirToCam * Mathf.Max(hit.distance - collisionRadius, 1f);
                }
            }
        }

        transform.position = smoothPosition;

        // 6 Взгляд смесь вектора движения и носа машины 
        Vector3 lookDir = Vector3.Lerp(velocityDir, target.forward, forwardLookBias).normalized;
        Vector3 lookAtPoint = target.position + Vector3.up * lookHeight + lookDir * lookDistance;

        Quaternion targetRotation = Quaternion.LookRotation(lookAtPoint - transform.position);

        // Кинематографичный крен в заносе
        if (driftRollMax > 0f)
        {
            float roll = Mathf.Clamp(-localVelX * 0.2f, -1f, 1f) * driftRollMax;
            targetRotation *= Quaternion.Euler(0f, 0f, roll);
        }

        // Плавный поворот 
        float rotT = 1f - Mathf.Exp(-Time.deltaTime / rotationSmoothTime);
        transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, rotT);

        // 7 Динамический FOV 
        float targetFov = Mathf.Lerp(minFov, maxFov, t);
        float fovT = 1f - Mathf.Exp(-Time.deltaTime / fovSmoothTime);
        currentFov = Mathf.Lerp(currentFov, targetFov, fovT);
        cam.fieldOfView = currentFov;
    }
}