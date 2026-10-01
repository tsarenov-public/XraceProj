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
    private CursorLockMode previousCursorLock;
    private bool previousCursorVisible;

    private void Start()
    {
        Time.timeScale = 1;
        AudioListener.pause = false;
        pausePanel.SetActive(false);
        settingsPanel.SetActive(false);
        if (MusicManager.Instance == null && musicManagerPrefab != null) Instantiate(musicManagerPrefab);
        music = MusicManager.Instance;
        if (music != null) music.OnTrackChanged += RefreshTrack;
        session.SetPaused(false);
        session.OnStateChanged += OnRaceStateChanged;
        musicVolumeSlider.SetValueWithoutNotify(SettingsService.Get());
        RefreshTrack();
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
        if (leaving || session.State == RaceSessionManager.RaceState.Finished) return;
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
        bool wasPaused = paused;
        paused = false;
        pausePanel.SetActive(false);
        settingsPanel.SetActive(false);
        session.SetPaused(false);
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
        settingsPanel.SetActive(false);
        pausePanel.SetActive(true);
    }
    public void SetMusicVolume(float volume)
    {
        if (music != null) music.SetVolume(volume);
        else { SettingsService.Set(volume); SettingsService.Save(); }
    }
    public void NextTrack() { if (music != null) music.NextTrack(); }
    public void PrevTrack() { if (music != null) music.PrevTrack(); }
    private void RefreshTrack()
    {
        if (trackNameText != null) trackNameText.text = music != null && music.CurrentTrack != null ? music.CurrentTrack.name : "Добавьте музыкальные треки";
    }
    public void ExitGarage()
    {
        if (leaving) return;
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
    private void OnDestroy()
    {
        if (session != null) session.OnStateChanged -= OnRaceStateChanged;
        if (music != null) music.OnTrackChanged -= RefreshTrack;
        if (paused)
        {
            Time.timeScale = 1;
            AudioListener.pause = false;
            if (MusicManager.Instance != null) MusicManager.Instance.SetRacePaused(false);
        }
    }
}
