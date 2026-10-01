using System;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
#endif

[InitializeOnLoad]
public static class PauseMenuSmokeCheck
{
    private const string Key = "Xrace.PauseSmoke";
    private static int frames;
    private static float pausedAt;
    private static PauseMenuController menu;
    private static RaceSessionManager session;
    private static RaceTimer timer;
    private static MusicManager music;
    private static AudioClip first;
    private static AudioClip second;
    static PauseMenuSmokeCheck() => EditorApplication.update += Tick;
    public static void WriteRestartVolume()
    {
        const string backup = "Logs/PauseVolumeBackup.txt";
        if (System.IO.File.Exists(backup)) throw new Exception("Pending volume backup exists; preserve it.");
        System.IO.File.WriteAllText(backup, (PlayerPrefs.HasKey("MusicVolume") ? "1" : "0") + "\n" + PlayerPrefs.GetFloat("MusicVolume", 0.8f).ToString(System.Globalization.CultureInfo.InvariantCulture));
        SettingsService.Set(0.37f); SettingsService.Save();
        Debug.Log("XRACE_VOLUME_WRITE_OK");
    }
    public static void ReadRestartVolume()
    {
        const string backup = "Logs/PauseVolumeBackup.txt";
        var original = System.IO.File.ReadAllLines(backup);
        bool correct = Mathf.Approximately(SettingsService.Get(), 0.37f);
        if (original[0] == "1") PlayerPrefs.SetFloat("MusicVolume", float.Parse(original[1], System.Globalization.CultureInfo.InvariantCulture)); else PlayerPrefs.DeleteKey("MusicVolume");
        PlayerPrefs.Save(); SettingsService.Load(); System.IO.File.Delete(backup);
        Require(correct, "Volume did not survive Unity process restart.");
        Debug.Log("XRACE_VOLUME_RESTART_OK");
    }
    public static void Run()
    {
        SessionState.SetBool(Key + "HadVolume", PlayerPrefs.HasKey("MusicVolume"));
        SessionState.SetFloat(Key + "Volume", PlayerPrefs.GetFloat("MusicVolume", 0.8f));
        SessionState.SetInt(Key, 1);
        SessionState.SetString(Key + "Deadline", DateTime.UtcNow.AddMinutes(3).ToString("O"));
        EditorSceneManager.OpenScene("Assets/Scenes/Touge01.unity");
        EditorApplication.EnterPlaymode();
    }
#if ENABLE_INPUT_SYSTEM
    private static void Press(UnityEngine.InputSystem.Key key, object target)
    {
        InputSystem.EnableDevice(Keyboard.current);
        InputSystem.QueueStateEvent(Keyboard.current, new KeyboardState(key));
        InputSystem.Update();
        target.GetType().GetMethod("Update", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(target, null);
        InputSystem.QueueStateEvent(Keyboard.current, new KeyboardState());
        InputSystem.Update();
    }
#endif
    private static T Field<T>(object target, string name) => (T)target.GetType().GetField(name, BindingFlags.NonPublic | BindingFlags.Instance).GetValue(target);
    private static void Require(bool condition, string message) { if (!condition) throw new Exception(message); }
    private static void Tick()
    {
        int step = SessionState.GetInt(Key, 0); if (step == 0) return;
        try
        {
            Require(DateTime.UtcNow < DateTime.Parse(SessionState.GetString(Key + "Deadline", "")), "Pause smoke timed out.");
            if (!EditorApplication.isPlaying || EditorApplication.isCompiling || ++frames < 20) return;
            frames = 0;
            if (step == 1)
            {
#if ENABLE_INPUT_SYSTEM
                InputSystem.settings = UnityEngine.Object.Instantiate(InputSystem.settings);
                InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
                InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
                if (Keyboard.current == null) InputSystem.AddDevice<Keyboard>();
#endif
                var roots = SceneManager.GetActiveScene().GetRootGameObjects();
                menu = roots.Select(r => r.GetComponent<PauseMenuController>()).First(m => m != null);
                session = roots.Select(r => r.GetComponent<RaceSessionManager>()).First(m => m != null);
                timer = session.GetComponent<RaceTimer>(); music = MusicManager.Instance;
                Require(music != null, "Direct Touge01 start has no music manager.");
                Require(!Field<GameObject>(menu, "pausePanel").activeSelf && !Field<GameObject>(menu, "settingsPanel").activeSelf, "Panels initially visible.");
                first = AudioClip.Create("Test Track A", 44100 * 60, 1, 44100, false);
                second = AudioClip.Create("Test Track B", 44100 * 60, 1, 44100, false);
                typeof(MusicManager).GetField("tracks", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(music, new[] { first, second });
                music.NextTrack(); Require(music.CurrentTrack == first, "First track selection failed.");
                var body = session.CarBody; body.GetComponent<WheelController>().enabled = false; body.useGravity = false; body.position += Vector3.up * 20; body.linearVelocity = body.transform.forward * 10;
            }
            else if (step == 2)
            {
                if (session.State != RaceSessionManager.RaceState.Racing) return;
#if ENABLE_INPUT_SYSTEM
                Press(UnityEngine.InputSystem.Key.Escape, menu);
#else
                menu.TogglePause();
#endif
            }
            else if (step == 3)
            {
#if ENABLE_INPUT_SYSTEM
                InputSystem.QueueStateEvent(Keyboard.current, new KeyboardState());
#endif
                Require(session.IsPaused && Time.timeScale == 0 && Field<GameObject>(menu, "pausePanel").activeSelf, "ESC did not pause.");
                Require(!Field<Canvas>(session.CarBody.GetComponent<VehicleHud>(), "canvas").gameObject.activeSelf, "Vehicle HUD overlays pause.");
                var wheels = session.CarBody.GetComponent<WheelController>();
                typeof(WheelController).GetField("pendingShift", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(wheels, 1);
                typeof(WheelController).GetMethod("Update", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(wheels, null);
                Require(Field<int>(wheels, "pendingShift") == 0, "Gear command survives pause.");
                pausedAt = timer.TimeSeconds;
                Require(!music.GetComponent<AudioSource>().isPlaying, "Music continued in pause.");
                SessionState.SetInt(Key + "Sample", music.GetComponent<AudioSource>().timeSamples);
            }
            else if (step == 4)
            {
                Require(timer.TimeSeconds == pausedAt, "Timer continued in pause.");
                Require(music.GetComponent<AudioSource>().timeSamples == SessionState.GetInt(Key + "Sample", 0), "Paused track advanced.");
                menu.Continue();
                Require(music.GetComponent<AudioSource>().isPlaying, "Music did not resume.");
                Require(music.GetComponent<AudioSource>().timeSamples == SessionState.GetInt(Key + "Sample", 0), "Continue restarted the track.");
                menu.TogglePause();
                menu.OpenSettings(); Require(Time.timeScale == 0 && Field<GameObject>(menu, "settingsPanel").activeSelf, "Settings unpaused race.");
                Field<Slider>(menu, "musicVolumeSlider").value = 0.37f;
                Require(Mathf.Approximately(music.GetComponent<AudioSource>().volume, 0.37f), "Music volume not applied.");
                Require(Mathf.Approximately(PlayerPrefs.GetFloat("MusicVolume"), 0.37f), "Volume not saved.");
                SettingsService.Set(0.12f); SettingsService.Load(); Require(Mathf.Approximately(SettingsService.Get(), 0.37f), "Volume reload failed.");
                menu.NextTrack(); Require(music.CurrentTrack == second, "Next track failed.");
                menu.NextTrack(); Require(music.CurrentTrack == first, "Track wrap failed.");
#if ENABLE_INPUT_SYSTEM
                Press(UnityEngine.InputSystem.Key.LeftArrow, music);
#else
                menu.PrevTrack();
#endif
            }
            else if (step == 5)
            {
#if ENABLE_INPUT_SYSTEM
                InputSystem.QueueStateEvent(Keyboard.current, new KeyboardState());
#endif
                Require(music.CurrentTrack == second, "Left arrow did not change track.");
                Require(!music.GetComponent<AudioSource>().isPlaying, "Track switching played music in pause.");
                Require(Mathf.Approximately(Field<Slider>(menu, "musicVolumeSlider").value, 0.37f), "Arrow also changed volume slider.");
                menu.SettingsBack(); Require(Field<GameObject>(menu, "pausePanel").activeSelf, "Back failed.");
                menu.Continue(); Require(Time.timeScale == 1 && !session.IsPaused && !AudioListener.pause, "Continue failed.");
            }
            else if (step == 6)
            {
                Require(timer.TimeSeconds > pausedAt, "Timer did not resume.");
                Require(Field<Canvas>(session.CarBody.GetComponent<VehicleHud>(), "canvas").gameObject.activeSelf, "Vehicle HUD did not resume.");
                menu.TogglePause();
                menu.enabled = false;
                Require(Time.timeScale == 1 && !session.IsPaused && !AudioListener.pause, "Disabling pause controller left the game frozen.");
                menu.enabled = true;
                menu.TogglePause();
                Require(session.IsPaused, "Re-enabled controller did not pause.");
                menu.Continue();
#if ENABLE_INPUT_SYSTEM
                Press(UnityEngine.InputSystem.Key.RightArrow, music);
#endif
            }
            else if (step == 7)
            {
#if ENABLE_INPUT_SYSTEM
                InputSystem.QueueStateEvent(Keyboard.current, new KeyboardState());
#endif
                Require(music.CurrentTrack == second, "Active driving arrow switched music.");
                menu.TogglePause(); menu.ExitGarage();
            }
            else if (step == 8)
            {
                if (SceneManager.GetActiveScene().name != "Garage") return;
                Require(Time.timeScale == 1 && !AudioListener.pause, "Garage remained paused.");
                Require(!music.GetComponent<AudioSource>().isPlaying, "Music plays in garage.");
#if ENABLE_INPUT_SYSTEM
                Press(UnityEngine.InputSystem.Key.Escape, music);
#endif
            }
            else
            {
                Require(SceneManager.GetActiveScene().name == "Garage" && Time.timeScale == 1, "ESC affected garage.");
                Cleanup(); Debug.Log("XRACE_PAUSE_SMOKE_OK: ESC, timer freeze/resume, settings, volume save/reload, tracks/wrap/arrows, active driving, garage silence."); EditorApplication.Exit(0); return;
            }
            SessionState.SetInt(Key, step + 1);
        }
        catch (Exception error) { Debug.LogException(error); Cleanup(); EditorApplication.Exit(1); }
    }
    private static void Cleanup()
    {
        SessionState.SetInt(Key, 0); Time.timeScale = 1; AudioListener.pause = false;
        if (SessionState.GetBool(Key + "HadVolume", false)) PlayerPrefs.SetFloat("MusicVolume", SessionState.GetFloat(Key + "Volume", 0.8f)); else PlayerPrefs.DeleteKey("MusicVolume");
        PlayerPrefs.Save(); SettingsService.Load();
    }
}
