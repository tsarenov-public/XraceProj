using UnityEngine;

public class CameraFollow : MonoBehaviour
{
    [Header("Цель (машина)")]
    public Transform target;

    [Header("Позиция камеры")]
    public Vector3 offset = new Vector3(0f, 5f, -8f); // сзади и сверху машины
    public float followSpeed = 5f;      // как быстро камера догоняет машину
    public float rotationSpeed = 3.5f;  // как быстро камера поворачивает

    [Header("Эффект скорости")]
    public float maxFovKick = 15f;  // насколько расширяется обзор на скорости
    public float maxSpeed = 40f;    // скорость, при которой обзор максимальный

    private Camera cam;
    private Rigidbody carRb;
    private float baseFov;

    void Start()
    {
        cam = GetComponent<Camera>();
        baseFov = cam.fieldOfView;

        if (target != null)
            carRb = target.GetComponent<Rigidbody>();
    }

    void LateUpdate()
    {
        if (target == null) return;

        // 1. ПОЗИЦИЯ: точка сзади и сверху от машины
        Vector3 desiredPosition = target.TransformPoint(offset);
        transform.position = Vector3.Lerp(transform.position, desiredPosition, Time.deltaTime * followSpeed);

        // 2. ПОВОРОТ: плавно смотрим на машину
        Vector3 lookDirection = target.position - transform.position;
        if (lookDirection != Vector3.zero)
        {
            Quaternion desiredRotation = Quaternion.LookRotation(lookDirection);
            transform.rotation = Quaternion.Slerp(transform.rotation, desiredRotation, Time.deltaTime * rotationSpeed);
        }

        // 3. ЭФФЕКТ СКОРОСТИ: на скорости обзор шире (чувство скорости!)
        if (carRb != null)
        {
            float speed = carRb.linearVelocity.magnitude;
            float t = Mathf.Clamp01(speed / maxSpeed);
            cam.fieldOfView = Mathf.Lerp(baseFov, baseFov + maxFovKick, t);
        }
    }
}