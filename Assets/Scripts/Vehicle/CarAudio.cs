using UnityEngine;

[DisallowMultipleComponent]
public class CarAudio : MonoBehaviour
{
    [Header("Источники")]
    [Tooltip("Существующий источник используется как верхний слой двигателя и как шаблон пространственного звука")]
    public AudioSource Engine;
    public AudioSource GearChangeSound;
    public WheelController Controller;

    [Header("Слои двигателя")]
    public AudioClip idleClip;
    public AudioClip lowOnClip;
    public AudioClip mediumOnClip;
    public AudioClip highOnClip;
    public AudioClip lowOffClip;
    public AudioClip mediumOffClip;
    public AudioClip highOffClip;
    public AudioClip limiterClip;

    [Header("Баланс")]
    [Range(0f, 1f)] public float masterVolume = 0.82f;
    [Range(0f, 1f)] public float idleVolume = 0.34f;
    [Range(0f, 1f)] public float onThrottleVolume = 0.9f;
    [Range(0f, 1f)] public float offThrottleVolume = 0.58f;
    [Range(0f, 1f)] public float limiterVolume = 0.38f;
    [Range(0f, 0.1f)] public float idleRoughness = 0.025f;

    [Header("Отклик")]
    [Min(0.1f)] public float rpmResponse = 11f;
    [Min(0.1f)] public float throttleResponse = 8f;
    [Min(0.1f)] public float volumeResponse = 12f;
    [Range(0f, 0.8f)] public float gearShiftVolumeDip = 0.38f;
    [Min(0.01f)] public float gearShiftRecovery = 0.16f;

    private AudioSource idleSource;
    private AudioSource lowOnSource;
    private AudioSource mediumOnSource;
    private AudioSource highOnSource;
    private AudioSource lowOffSource;
    private AudioSource mediumOffSource;
    private AudioSource highOffSource;
    private AudioSource limiterSource;

    private float smoothedRpm;
    private float smoothedThrottle;
    private float shiftDip;
    private int lastGear;

    private void Awake()
    {
        if (Controller == null)
            Controller = GetComponent<WheelController>();

        if (Engine == null)
            Engine = GetComponentInChildren<AudioSource>();

        lastGear = Controller == null ? 0 : Controller.CurrentGear;
        smoothedRpm = Controller == null ? 0f : Controller.EngineRpm;

        if (GearChangeSound != null)
        {
            GearChangeSound.playOnAwake = false;
            GearChangeSound.loop = false;
            GearChangeSound.Stop();
        }

        BuildEngineLayers();
    }

    private void Update()
    {
        if (Controller == null)
            return;

        float dt = Mathf.Min(Time.deltaTime, 0.05f);
        if (dt <= 0f)
            return;

        smoothedRpm = Mathf.Lerp(
            smoothedRpm,
            Controller.EngineRpm,
            Damp(rpmResponse, dt));
        smoothedThrottle = Mathf.Lerp(
            smoothedThrottle,
            Controller.ThrottleInput,
            Damp(throttleResponse, dt));
        shiftDip = Mathf.MoveTowards(shiftDip, 0f, dt / Mathf.Max(0.01f, gearShiftRecovery));

        float rpmRange = Mathf.Max(1f, Controller.redlineRpm - Controller.idleRpm);
        float normalizedRpm = Mathf.Clamp01((smoothedRpm - Controller.idleRpm) / rpmRange);
        float onLoad = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.04f, 0.72f, smoothedThrottle));
        float offLoad = 1f - onLoad;
        float shiftGain = 1f - shiftDip * gearShiftVolumeDip;

        float idleWeight = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.03f, 0.2f, normalizedRpm));
        float lowWeight = BandWeight(normalizedRpm, 0.02f, 0.22f, 0.5f);
        float mediumWeight = BandWeight(normalizedRpm, 0.24f, 0.53f, 0.82f);
        float highWeight = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.58f, 0.88f, normalizedRpm));
        float limiterWeight = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.94f, 0.995f, normalizedRpm))
            * onLoad;

        float masterGain = masterVolume * shiftGain;
        SetVolume(idleSource, idleWeight * idleVolume * masterGain, dt);
        SetVolume(lowOnSource, lowWeight * onLoad * onThrottleVolume * masterGain, dt);
        SetVolume(mediumOnSource, mediumWeight * onLoad * onThrottleVolume * masterGain, dt);
        SetVolume(highOnSource, highWeight * onLoad * onThrottleVolume * masterGain, dt);
        SetVolume(lowOffSource, lowWeight * offLoad * offThrottleVolume * masterGain, dt);
        SetVolume(mediumOffSource, mediumWeight * offLoad * offThrottleVolume * masterGain, dt);
        SetVolume(highOffSource, highWeight * offLoad * offThrottleVolume * masterGain, dt);
        SetVolume(limiterSource, limiterWeight * limiterVolume * masterGain, dt);

        float idleNoise = (Mathf.PerlinNoise(Time.time * 8f, 0.37f) - 0.5f)
            * idleRoughness
            * idleWeight
            * (1f - smoothedThrottle);
        SetPitch(idleSource, Mathf.Lerp(0.92f, 1.08f, Mathf.InverseLerp(0f, 0.2f, normalizedRpm)) + idleNoise, dt);

        float lowPitch = Mathf.Lerp(0.8f, 1.28f, Mathf.InverseLerp(0.02f, 0.5f, normalizedRpm));
        float mediumPitch = Mathf.Lerp(0.82f, 1.25f, Mathf.InverseLerp(0.24f, 0.82f, normalizedRpm));
        float highPitch = Mathf.Lerp(0.84f, 1.2f, Mathf.InverseLerp(0.58f, 1f, normalizedRpm));
        SetPitch(lowOnSource, lowPitch, dt);
        SetPitch(lowOffSource, lowPitch * 0.985f, dt);
        SetPitch(mediumOnSource, mediumPitch, dt);
        SetPitch(mediumOffSource, mediumPitch * 0.985f, dt);
        SetPitch(highOnSource, highPitch, dt);
        SetPitch(highOffSource, highPitch * 0.99f, dt);
        SetPitch(limiterSource, Mathf.Lerp(0.96f, 1.04f, limiterWeight), dt);

        HandleGearChange();
    }

    private void HandleGearChange()
    {
        if (Controller.CurrentGear == lastGear)
            return;

        shiftDip = 1f;
        if (GearChangeSound != null && GearChangeSound.clip != null)
        {
            GearChangeSound.pitch = Random.Range(0.96f, 1.04f);
            GearChangeSound.Play();
        }

        lastGear = Controller.CurrentGear;
    }

    private void BuildEngineLayers()
    {
        if (Engine == null)
            return;

        AudioClip fallbackHighOn = highOnClip != null ? highOnClip : Engine.clip;
        idleSource = CreateLoopSource(idleClip);
        lowOnSource = CreateLoopSource(lowOnClip);
        mediumOnSource = CreateLoopSource(mediumOnClip);
        highOnSource = ConfigureLoopSource(Engine, fallbackHighOn);
        lowOffSource = CreateLoopSource(lowOffClip);
        mediumOffSource = CreateLoopSource(mediumOffClip);
        highOffSource = CreateLoopSource(highOffClip);
        limiterSource = CreateLoopSource(limiterClip);
    }

    private AudioSource CreateLoopSource(AudioClip clip)
    {
        if (clip == null || Engine == null)
            return null;

        AudioSource source = gameObject.AddComponent<AudioSource>();
        source.outputAudioMixerGroup = Engine.outputAudioMixerGroup;
        source.spatialBlend = Engine.spatialBlend;
        source.rolloffMode = Engine.rolloffMode;
        source.minDistance = Engine.minDistance;
        source.maxDistance = Engine.maxDistance;
        source.priority = Engine.priority;
        source.spread = Engine.spread;
        source.panStereo = Engine.panStereo;
        source.dopplerLevel = 0f;
        return ConfigureLoopSource(source, clip);
    }

    private static AudioSource ConfigureLoopSource(AudioSource source, AudioClip clip)
    {
        if (source == null || clip == null)
            return null;

        source.Stop();
        source.clip = clip;
        source.loop = true;
        source.playOnAwake = false;
        source.volume = 0f;
        source.pitch = 1f;
        source.dopplerLevel = 0f;
        source.Play();
        return source;
    }

    private void SetVolume(AudioSource source, float targetVolume, float dt)
    {
        if (source == null)
            return;

        source.volume = Mathf.Lerp(
            source.volume,
            Mathf.Clamp01(targetVolume),
            Damp(volumeResponse, dt));
    }

    private void SetPitch(AudioSource source, float targetPitch, float dt)
    {
        if (source == null)
            return;

        source.pitch = Mathf.Lerp(
            source.pitch,
            Mathf.Clamp(targetPitch, 0.5f, 2f),
            Damp(rpmResponse, dt));
    }

    private static float BandWeight(float value, float start, float peak, float end)
    {
        if (value <= peak)
            return Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(start, peak, value));

        return 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(peak, end, value));
    }

    private static float Damp(float sharpness, float dt)
    {
        return 1f - Mathf.Exp(-Mathf.Max(0.01f, sharpness) * dt);
    }
}
