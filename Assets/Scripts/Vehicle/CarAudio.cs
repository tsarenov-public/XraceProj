using UnityEngine;

public class CarAudio : MonoBehaviour
{
    public AudioSource Engine;
    public AudioSource GearChangeSound;
    public WheelController Controller;

    [Header("Звук двигателя")]
    public float idlePitch = 0.75f;
    public float redlinePitch = 2.05f;
    public float idleVolume = 0.16f;
    public float throttleVolume = 0.82f;
    public float audioResponse = 7f;

    private int lastGear;

    private void Awake()
    {
        if (Controller == null) Controller = GetComponent<WheelController>();
        lastGear = Controller == null ? 0 : Controller.CurrentGear;
    }

    private void Update()
    {
        if (Controller == null || Engine == null) return;

        float rpmRange = Mathf.Max(1f, Controller.redlineRpm - Controller.idleRpm);
        float normalizedRpm = Mathf.Clamp01((Controller.EngineRpm - Controller.idleRpm) / rpmRange);
        float targetPitch = Mathf.Lerp(idlePitch, redlinePitch, normalizedRpm);
        float targetVolume = Mathf.Lerp(idleVolume, throttleVolume, Controller.ThrottleInput);
        float blend = 1f - Mathf.Exp(-audioResponse * Time.deltaTime);

        Engine.pitch = Mathf.Lerp(Engine.pitch, targetPitch, blend);
        Engine.volume = Mathf.Lerp(Engine.volume, targetVolume, blend);

        if (Controller.CurrentGear != lastGear)
        {
            if (GearChangeSound != null) GearChangeSound.Play();
            lastGear = Controller.CurrentGear;
        }
    }
}
