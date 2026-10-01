using UnityEngine;
using UnityEngine.SceneManagement;
#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
using UnityEngine.InputSystem;
#endif

[RequireComponent(typeof(AudioSource))]
[DisallowMultipleComponent]
public class MusicManager : MonoBehaviour
{
    public static MusicManager Instance { get; private set; }
    [SerializeField] private AudioClip[] tracks;
    [SerializeField] private AudioSource audioSource;
    private int currentTrack = -1;
    private bool inGarage;
    private bool inRace;
    private bool racePaused;
    private bool garageCarSelectionActive;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => Instance = null;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        // Put this component on a dedicated root object, not GarageRoot.
        transform.SetParent(null);
        DontDestroyOnLoad(gameObject);
        if (audioSource == null) audioSource = GetComponent<AudioSource>();
        audioSource.playOnAwake = false;
        audioSource.loop = true;
        audioSource.spatialBlend = 0f;
        audioSource.ignoreListenerPause = true;
        audioSource.volume = SettingsService.Get();
        SettingsService.MusicVolumeChanged += ApplyVolume;
        SceneManager.activeSceneChanged += OnSceneChanged;
        OnSceneChanged(default(Scene), SceneManager.GetActiveScene());
    }

    private void Start()
    {
        if (Instance == this && currentTrack < 0 && (inGarage || inRace)) NextTrack();
    }

    private void OnSceneChanged(Scene previous, Scene current)
    {
        inGarage = current.name == "Garage";
        inRace = current.name == "Touge01";
        racePaused = false;
        garageCarSelectionActive = false;
        if (!inGarage && !inRace) audioSource.Pause();
        else if (currentTrack >= 0) audioSource.UnPause();
        else NextTrack();
    }

    // Pause controller calls true on opening pause, false on resume/finish/exit.
    public void SetRacePaused(bool paused) => racePaused = paused;
    public void SetGarageCarSelectionActive(bool active) => garageCarSelectionActive = active;

    private void Update()
    {
        if (!(inGarage && !garageCarSelectionActive) && !(inRace && racePaused)) return;
#if ENABLE_LEGACY_INPUT_MANAGER
        if (Input.GetKeyDown(KeyCode.RightArrow)) NextTrack();
        else if (Input.GetKeyDown(KeyCode.LeftArrow)) PrevTrack();
#elif ENABLE_INPUT_SYSTEM
        Keyboard keyboard = Keyboard.current;
        if (keyboard == null) return;
        if (keyboard.rightArrowKey.wasPressedThisFrame) NextTrack();
        else if (keyboard.leftArrowKey.wasPressedThisFrame) PrevTrack();
#endif
    }

    public void NextTrack() => SwitchTrack(1);
    public void PrevTrack() => SwitchTrack(-1);

    private void SwitchTrack(int direction)
    {
        if (tracks == null || tracks.Length == 0) return;
        int candidate = currentTrack < 0 ? (direction > 0 ? tracks.Length - 1 : 0) : currentTrack;
        for (int i = 0; i < tracks.Length; i++)
        {
            candidate = (candidate + direction + tracks.Length) % tracks.Length;
            if (tracks[candidate] == null) continue;
            currentTrack = candidate;
            audioSource.clip = tracks[currentTrack];
            audioSource.Play();
            return;
        }
    }

    public void SetVolume(float volume)
    {
        SettingsService.Set(volume);
        SettingsService.Save();
    }

    private void ApplyVolume(float volume) => audioSource.volume = volume;

    private void OnDestroy()
    {
        if (Instance != this) return;
        SettingsService.MusicVolumeChanged -= ApplyVolume;
        SceneManager.activeSceneChanged -= OnSceneChanged;
        Instance = null;
    }
}
