using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using System.Collections.Generic;
#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
using UnityEngine.InputSystem;
#endif

public class GarageManager : MonoBehaviour
{
    [SerializeField] private CarDefinition[] cars;
    [SerializeField] private Transform carSpot;
    [SerializeField] private TMP_Text carNameText;
    [SerializeField] private Button raceButton;
    [SerializeField] private MusicManager musicManagerPrefab;
    [Header("Preview rotation (local Y axis)")]
    [SerializeField] private bool allowPreviewRotation = true;
    [SerializeField, Min(0.01f)] private float previewRotationDegreesPerPixel = 0.35f;
    private GameObject preview;
    private int selectedIndex;
    private bool loading;
    private bool draggingPreview;
    private Vector2 previousPointerPosition;
    private float previewAngleDegrees;
    private PointerEventData pointerEvent;
    private EventSystem pointerEventSystem;
    private readonly List<RaycastResult> uiHits = new List<RaycastResult>(8);

    private void Start()
    {
        if (raceButton != null) raceButton.interactable = false;
        Time.timeScale = 1f;
        AudioListener.pause = false;
        if (MusicManager.Instance == null)
        {
            if (musicManagerPrefab != null) Instantiate(musicManagerPrefab);
            else new GameObject("MusicManager").AddComponent<MusicManager>();
        }
        MusicManager.Instance.SetGarageCarSelectionActive(true);
        CarDefinition selected = SelectedCarService.Get();
        if (cars != null)
            for (int i = 0; i < cars.Length; i++)
                if (cars[i] == selected && IsValid(i)) { selectedIndex = i; break; }
        if (!IsValid(selectedIndex)) SelectNextValid(1);
        ShowPreview();
    }

    private void Update()
    {
        if (loading) return;
#if ENABLE_LEGACY_INPUT_MANAGER
        if (Input.GetKeyDown(KeyCode.LeftArrow)) PrevCar();
        else if (Input.GetKeyDown(KeyCode.RightArrow)) NextCar();
#elif ENABLE_INPUT_SYSTEM
        Keyboard keyboard = Keyboard.current;
        if (keyboard != null)
        {
            if (keyboard.leftArrowKey.wasPressedThisFrame) PrevCar();
            else if (keyboard.rightArrowKey.wasPressedThisFrame) NextCar();
        }
#endif
        UpdatePreviewRotation();
    }

    private void UpdatePreviewRotation()
    {
        if (!allowPreviewRotation || !HasReadyPreview()) { draggingPreview = false; return; }
        Vector2 position;
        bool pressed;
        bool held;
#if ENABLE_LEGACY_INPUT_MANAGER
        position = Input.mousePosition;
        pressed = Input.GetMouseButtonDown(0);
        held = Input.GetMouseButton(0);
#elif ENABLE_INPUT_SYSTEM
        Mouse mouse = Mouse.current;
        if (mouse == null) { draggingPreview = false; return; }
        position = mouse.position.ReadValue();
        pressed = mouse.leftButton.wasPressedThisFrame;
        held = mouse.leftButton.isPressed;
#else
        return;
#endif
#if ENABLE_LEGACY_INPUT_MANAGER || ENABLE_INPUT_SYSTEM
        if (pressed)
        {
            draggingPreview = !IsPointerOverUi(position);
            previousPointerPosition = position;
        }
        if (!held) { draggingPreview = false; return; }
        if (!draggingPreview) return;
        ApplyPreviewRotation(position.x - previousPointerPosition.x);
        previousPointerPosition = position;
#endif
    }

    private bool IsPointerOverUi(Vector2 position)
    {
        EventSystem events = EventSystem.current;
        if (events == null) return false;
        if (pointerEvent == null || pointerEventSystem != events)
        {
            pointerEventSystem = events;
            pointerEvent = new PointerEventData(events);
        }
        pointerEvent.Reset();
        pointerEvent.position = position;
        uiHits.Clear();
        events.RaycastAll(pointerEvent, uiHits);
        return uiHits.Count > 0;
    }

    private void ApplyPreviewRotation(float horizontalPixels)
    {
        if (!allowPreviewRotation || loading || !HasReadyPreview()) return;
        previewAngleDegrees = Mathf.Repeat(previewAngleDegrees + horizontalPixels * previewRotationDegreesPerPixel, 360f);
        // Rotate the preview pivot only: CarSpot, position, scale and the source prefab stay unchanged.
        preview.transform.localRotation = Quaternion.AngleAxis(previewAngleDegrees, Vector3.up);
    }

    private void OnDisable() => draggingPreview = false;

    public void PrevCar() => ChangeCar(-1);
    public void NextCar() => ChangeCar(1);

    private void ChangeCar(int direction)
    {
        if (loading) return;
        SelectNextValid(direction);
        ShowPreview();
    }

    private bool IsValid(int index) => cars != null && index >= 0 && index < cars.Length
        && cars[index] != null && cars[index].carPrefab != null;

    private void SelectNextValid(int direction)
    {
        if (cars == null || cars.Length == 0) return;
        for (int i = 0; i < cars.Length; i++)
        {
            selectedIndex = (selectedIndex + direction + cars.Length) % cars.Length;
            if (IsValid(selectedIndex)) return;
        }
    }

    private void ShowPreview()
    {
        if (preview != null) { preview.SetActive(false); Destroy(preview); }
        preview = null;
        draggingPreview = false;
        previewAngleDegrees = 0f;
        UpdateRaceButton();
        if (!IsValid(selectedIndex) || carSpot == null || carNameText == null || raceButton == null)
        {
            if (carNameText != null) carNameText.text = "НЕТ МАШИН";
            Debug.LogError("GarageManager: assign CarSpot, CarName_Text, Btn_Race and at least one CarDefinition with a prefab.", this);
            return;
        }
        CarDefinition car = cars[selectedIndex];
        if (carNameText != null) carNameText.text = car.displayName;
        // Copy visuals only. No vehicle Awake/OnDestroy, native wheel physics or particle playOnAwake
        // is involved, even when returning to the garage after finishing a race.
        preview = GaragePreviewBuilder.Create(car.carPrefab, carSpot, "CarPreview");
        UpdateRaceButton();
    }

    private bool HasReadyPreview() => IsValid(selectedIndex) && carSpot != null && carNameText != null &&
        raceButton != null && preview != null && preview.activeInHierarchy;

    private void UpdateRaceButton()
    {
        if (raceButton != null)
            raceButton.interactable = !loading && HasReadyPreview() && Application.CanStreamedLevelBeLoaded("Touge01");
    }

    public void Race()
    {
        if (loading || !HasReadyPreview()) return;
        if (!Application.CanStreamedLevelBeLoaded("Touge01"))
        { Debug.LogError("Add Touge01 to the active Build Profile scene list.", this); return; }
        SelectedCarService.Set(cars[selectedIndex]);
        loading = true;
        UpdateRaceButton();
        SceneManager.LoadSceneAsync("Touge01");
    }
}
