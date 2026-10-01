using System;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
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

    private static void CheckPointerSlider(Slider slider)
    {
        Canvas.ForceUpdateCanvases();
        var eventSystem = EventSystem.current;
        Require(eventSystem != null, "No EventSystem for pointer volume check.");
        var handleArea = slider.handleRect.parent as RectTransform;
        Require(handleArea != null, "Missing handle slide area.");
        var pointer = new PointerEventData(eventSystem) { button = PointerEventData.InputButton.Left, pointerId = -1 };
        var results = new System.Collections.Generic.List<RaycastResult>();
        float savedBeforeDrag = PlayerPrefs.GetFloat("MusicVolume", 0.8f);
        // Click above the narrow painted track: the full slider rectangle must accept it.
        pointer.position = SliderPoint(handleArea, 0, 0.3f);
        pointer.pressPosition = pointer.position;
        eventSystem.RaycastAll(pointer, results);
        var rootImage = slider.GetComponent<Image>();
        Debug.Log("XRACE_VOLUME_RAYCAST: screen=" + Screen.width + "x" + Screen.height + " position=" + pointer.position +
            " rootRect=" + ((RectTransform)slider.transform).rect + " handleArea=" + handleArea.rect +
            " handleRect=" + slider.handleRect.rect + " rootRaycast=" + (rootImage != null && rootImage.raycastTarget) +
            " rootCull=" + (rootImage != null && rootImage.canvasRenderer.cull) + " rootDepth=" + (rootImage != null ? rootImage.depth : -2) +
            " hits=" + string.Join(",", results.Select(hit => hit.gameObject.name)));
        Require(Mathf.Abs(slider.handleRect.rect.height - handleArea.rect.height) < 0.05f && Mathf.Abs(slider.handleRect.sizeDelta.y) < 0.05f,
            "Handle height contains a second size offset.");
        Require(results.Count != 0 && results[0].gameObject.transform.IsChildOf(slider.transform), "Full-height slider hit area is blocked.");
        pointer.pointerPressRaycast = pointer.pointerCurrentRaycast = results[0];
        pointer.pointerPress = pointer.pointerDrag = slider.gameObject;
        ExecuteEvents.Execute(slider.gameObject, pointer, ExecuteEvents.pointerDownHandler);
        ExecuteEvents.Execute(slider.gameObject, pointer, ExecuteEvents.initializePotentialDrag);
        pointer.dragging = true;
        ExecuteEvents.Execute(slider.gameObject, pointer, ExecuteEvents.beginDragHandler);
        Require(eventSystem.currentSelectedGameObject != slider.gameObject, "Volume slider would consume music arrow keys.");
        Require(Mathf.Abs(slider.value) < 0.005f, "Click on slider start did not select zero volume.");
        float previous = slider.value;
        foreach (float value in new[] { 0.25f, 0.5f, 0.75f, 1f })
        {
            pointer.position = SliderPoint(handleArea, value, 0.3f);
            ExecuteEvents.Execute(slider.gameObject, pointer, ExecuteEvents.dragHandler);
            Require(slider.value > previous && Mathf.Abs(slider.value - value) < 0.005f, "Volume drag is non-monotonic or offset.");
            Require(Mathf.Abs(slider.fillRect.rect.width - ((RectTransform)slider.fillRect.parent).rect.width * slider.value) < 0.05f, "Volume fill extends beyond its normalized track.");
            Require(Mathf.Approximately(music.GetComponent<AudioSource>().volume, slider.value), "Dragging did not immediately apply audio volume.");
            Require(Time.timeScale == 0 && !music.GetComponent<AudioSource>().isPlaying, "Volume dragging resumed the paused race or music.");
            previous = slider.value;
        }
        pointer.position = SliderPoint(handleArea, 0, 0.3f);
        ExecuteEvents.Execute(slider.gameObject, pointer, ExecuteEvents.dragHandler);
        Require(Mathf.Abs(slider.value) < 0.005f && music.GetComponent<AudioSource>().volume < 0.005f, "Volume slider cannot mute at zero.");
        pointer.position = SliderPoint(handleArea, 0.37f, 0.3f);
        ExecuteEvents.Execute(slider.gameObject, pointer, ExecuteEvents.dragHandler);
        Require(PlayerPrefs.GetFloat("MusicVolume", 0.8f) == savedBeforeDrag, "Pointer drag writes PlayerPrefs before release.");
        ExecuteEvents.Execute(slider.gameObject, pointer, ExecuteEvents.pointerUpHandler);
        ExecuteEvents.Execute(slider.gameObject, pointer, ExecuteEvents.endDragHandler);
        Require(Mathf.Approximately(PlayerPrefs.GetFloat("MusicVolume"), 0.37f), "Pointer release did not save volume.");
        Debug.Log("XRACE_VOLUME_POINTER_OK: full-height click, real pointer drag 0/25/50/75/100%, correct fill, immediate paused audio, release persistence.");
    }

    private static Vector2 SliderPoint(RectTransform area, float value, float verticalOffset)
    {
        var canvas = area.GetComponentInParent<Canvas>();
        var camera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
        return RectTransformUtility.WorldToScreenPoint(camera, area.TransformPoint(new Vector3(Mathf.Lerp(area.rect.xMin, area.rect.xMax, value), area.rect.height * verticalOffset, 0)));
    }

    private static void CaptureSettings()
    {
        var canvas = Field<Slider>(menu, "musicVolumeSlider").GetComponentInParent<Canvas>();
        var camera = Camera.allCameras.First(value => value.isActiveAndEnabled);
        var renderTexture = new RenderTexture(1280, 720, 24);
        var texture = new Texture2D(1280, 720, TextureFormat.RGB24, false);
        var previousActive = RenderTexture.active;
        var previousTarget = camera.targetTexture;
        var previousMode = canvas.renderMode;
        var previousCamera = canvas.worldCamera;
        float previousDistance = canvas.planeDistance;
        try
        {
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = camera;
            canvas.planeDistance = Mathf.Max(camera.nearClipPlane + 0.1f, 1f);
            camera.targetTexture = renderTexture;
            Canvas.ForceUpdateCanvases();
            RenderPipeline.SubmitRenderRequest(camera, new UniversalRenderPipeline.SingleCameraRequest { destination = renderTexture });
            RenderTexture.active = renderTexture;
            texture.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0);
            texture.Apply();
            System.IO.File.WriteAllBytes("Logs/SettingsPreview.png", texture.EncodeToPNG());
        }
        finally
        {
            camera.targetTexture = previousTarget;
            canvas.renderMode = previousMode;
            canvas.worldCamera = previousCamera;
            canvas.planeDistance = previousDistance;
            RenderTexture.active = previousActive;
            UnityEngine.Object.DestroyImmediate(texture);
            UnityEngine.Object.DestroyImmediate(renderTexture);
            Canvas.ForceUpdateCanvases();
        }
    }
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
            }
            else if (step == 5)
            {
                // Let the newly activated panel render before sending real pointer events.
                CheckPointerSlider(Field<Slider>(menu, "musicVolumeSlider"));
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
#if ENABLE_INPUT_SYSTEM
                InputSystem.QueueStateEvent(Keyboard.current, new KeyboardState());
#endif
                Require(music.CurrentTrack == second, "Left arrow did not change track.");
                Require(!music.GetComponent<AudioSource>().isPlaying, "Track switching played music in pause.");
                Require(Mathf.Approximately(Field<Slider>(menu, "musicVolumeSlider").value, 0.37f), "Arrow also changed volume slider.");
                if (Environment.GetCommandLineArgs().Contains("-xrace-capture-settings")) CaptureSettings();
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
                Cleanup(); Debug.Log("XRACE_PAUSE_SMOKE_OK: ESC, timer freeze/resume, pointer volume/fill/save/reload, tracks/wrap/arrows, active driving, garage silence."); EditorApplication.Exit(0); return;
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
