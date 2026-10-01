using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
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
    private GameObject preview;
    private int selectedIndex;
    private bool loading;

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
        if (keyboard == null) return;
        if (keyboard.leftArrowKey.wasPressedThisFrame) PrevCar();
        else if (keyboard.rightArrowKey.wasPressedThisFrame) NextCar();
#endif
    }

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
        UpdateRaceButton();
        if (!IsValid(selectedIndex) || carSpot == null || carNameText == null || raceButton == null)
        {
            if (carNameText != null) carNameText.text = "НЕТ МАШИН";
            Debug.LogError("GarageManager: assign CarSpot, CarName_Text, Btn_Race and at least one CarDefinition with a prefab.", this);
            return;
        }
        CarDefinition car = cars[selectedIndex];
        if (carNameText != null) carNameText.text = car.displayName;
        // Inactive parent prevents vehicle Awake/OnEnable from starting engine, cameras or input.
        preview = new GameObject("CarPreview");
        preview.SetActive(false);
        preview.transform.SetParent(carSpot, false);
        GameObject model = Instantiate(car.carPrefab, preview.transform, false);
        model.transform.localPosition = Vector3.zero;
        model.transform.localRotation = Quaternion.identity;
        foreach (MonoBehaviour behaviour in model.GetComponentsInChildren<MonoBehaviour>(true))
            Destroy(behaviour);
        foreach (Camera camera in model.GetComponentsInChildren<Camera>(true)) camera.enabled = false;
        foreach (AudioListener listener in model.GetComponentsInChildren<AudioListener>(true)) listener.enabled = false;
        foreach (AudioSource source in model.GetComponentsInChildren<AudioSource>(true))
        { source.playOnAwake = false; source.enabled = false; }
        foreach (Rigidbody body in model.GetComponentsInChildren<Rigidbody>(true))
        { body.isKinematic = true; body.detectCollisions = false; }
        foreach (Collider collider in model.GetComponentsInChildren<Collider>(true)) collider.enabled = false;
        // Destroy completes at the end of this frame, before activating the visual preview.
        StartCoroutine(ActivatePreview(preview));
    }

    private System.Collections.IEnumerator ActivatePreview(GameObject target)
    {
        yield return null;
        if (target != null && target == preview)
        {
            target.SetActive(true);
            UpdateRaceButton();
        }
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
