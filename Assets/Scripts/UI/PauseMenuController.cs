using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

[DisallowMultipleComponent]
public class PauseMenuController : MonoBehaviour
{
    [SerializeField] private RaceSessionManager session;
    [SerializeField] private GameObject pausePanel;
    [SerializeField] private GameObject settingsPanel;
    [SerializeField] private Slider musicVolumeSlider;
    [SerializeField] private TMP_Text trackNameText;
    [SerializeField] private MusicManager musicManagerPrefab;
    private MusicManager music;
    private bool paused;
    private bool leaving;
    private bool initialized;
    private bool subscribed;
    private CursorLockMode previousCursorLock;
    private bool previousCursorVisible;

    private void Awake()
    {
        if (session == null || pausePanel == null || settingsPanel == null || musicVolumeSlider == null)
        {
            Debug.LogError("PauseMenuController: assign session, both panels and volume slider.", this);
            enabled = false;
            return;
        }
        if (transform.IsChildOf(pausePanel.transform) || transform.IsChildOf(settingsPanel.transform))
        {
            Debug.LogError("PauseMenuController must be on the active Canvas, outside its panels.", this);
            enabled = false;
        }
        if (musicVolumeSlider.GetComponent<MusicVolumeSliderCommitter>() == null)
            musicVolumeSlider.gameObject.AddComponent<MusicVolumeSliderCommitter>();
    }
    private void Start()
    {
        Time.timeScale = 1;
        AudioListener.pause = false;
        pausePanel.SetActive(false);
        settingsPanel.SetActive(false);
        if (MusicManager.Instance == null && musicManagerPrefab != null) Instantiate(musicManagerPrefab);
        music = MusicManager.Instance;
        session.SetPaused(false);
        initialized = true;
        Subscribe();
        musicVolumeSlider.SetValueWithoutNotify(SettingsService.Get());
        RefreshTrack();
    }
    private void OnEnable()
    {
        if (initialized) { Subscribe(); RefreshTrack(); }
    }
    private void Subscribe()
    {
        if (subscribed) return;
        session.OnStateChanged += OnRaceStateChanged;
        if (music != null) music.OnTrackChanged += RefreshTrack;
        subscribed = true;
    }
    private void Update()
    {
        if (leaving || gameObject.scene.name != "Touge01" || session.State == RaceSessionManager.RaceState.Finished) return;
#if ENABLE_INPUT_SYSTEM
        if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame) TogglePause();
#elif ENABLE_LEGACY_INPUT_MANAGER
        if (Input.GetKeyDown(KeyCode.Escape)) TogglePause();
#endif
    }
    public void TogglePause()
    {
        if (!initialized || !isActiveAndEnabled || gameObject.scene.name != "Touge01" || leaving || session.State == RaceSessionManager.RaceState.Finished) return;
        if (paused) Continue();
        else
        {
            previousCursorLock = Cursor.lockState;
            previousCursorVisible = Cursor.visible;
            paused = true;
            session.SetPaused(true);
            Time.timeScale = 0;
            AudioListener.pause = true;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            settingsPanel.SetActive(false);
            pausePanel.SetActive(true);
        }
    }
    public void Continue()
    {
        if (!initialized) return;
        SettingsService.Save();
        bool wasPaused = paused;
        paused = false;
        if (pausePanel != null) pausePanel.SetActive(false);
        if (settingsPanel != null) settingsPanel.SetActive(false);
        if (session != null) session.SetPaused(false);
        else if (MusicManager.Instance != null) MusicManager.Instance.SetRacePaused(false);
        Time.timeScale = 1;
        AudioListener.pause = false;
        if (wasPaused)
        {
            Cursor.lockState = previousCursorLock;
            Cursor.visible = previousCursorVisible;
        }
    }
    public void OpenSettings()
    {
        if (!paused || leaving) return;
        musicVolumeSlider.SetValueWithoutNotify(SettingsService.Get());
        RefreshTrack();
        pausePanel.SetActive(false);
        settingsPanel.SetActive(true);
    }
    public void SettingsBack()
    {
        if (!paused || leaving) return;
        SettingsService.Save();
        settingsPanel.SetActive(false);
        pausePanel.SetActive(true);
    }
    public void SetMusicVolume(float volume)
    {
        if (!initialized || leaving) return;
        if (music != null) music.SetVolume(volume);
        else SettingsService.Set(volume);
    }
    public void NextTrack() { if (paused && !leaving && music != null) music.NextTrack(); }
    public void PrevTrack() { if (paused && !leaving && music != null) music.PrevTrack(); }
    private void RefreshTrack()
    {
        if (trackNameText != null) trackNameText.text = music != null && music.CurrentTrack != null ? music.CurrentTrack.name : "Нет треков";
    }
    public void ExitGarage()
    {
        if (!initialized || leaving) return;
        if (!Application.CanStreamedLevelBeLoaded("Garage"))
        { Debug.LogError("Garage is missing from Build Settings.", this); return; }
        leaving = true;
        Continue();
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        SceneManager.LoadSceneAsync("Garage");
    }
    private void OnRaceStateChanged(RaceSessionManager.RaceState state)
    {
        if (state == RaceSessionManager.RaceState.Finished) Continue();
    }
    private void OnDisable()
    {
        if (!initialized) return;
        SettingsService.Save();
        if (session != null) session.OnStateChanged -= OnRaceStateChanged;
        if (music != null) music.OnTrackChanged -= RefreshTrack;
        subscribed = false;
        // Disabling the controller in Inspector must not leave a frozen race.
        if (paused) Continue();
    }
}
