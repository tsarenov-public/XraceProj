using System;
using System.Globalization;
using System.IO;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class FinishPanelController : MonoBehaviour
{
    [SerializeField] private RaceSessionManager session;
    [SerializeField] private RaceTimer timer;
    [SerializeField] private DriftScorer scorer;
    [SerializeField] private CarDefinition fallbackCar;
    [SerializeField] private GameObject finishPanel;
    [SerializeField] private TMP_Text finishTimeText;
    [SerializeField] private TMP_Text finishScoreText;
    [SerializeField] private TMP_Text recordText;
    [SerializeField] private TMP_Text saveStatusText;
    [SerializeField] private Button garageButton;
    [SerializeField] private Button retryButton;
    [SerializeField] private Button resultsButton;
    [SerializeField] private ResultsPanelController resultsController;
    private bool finishHandled;
    private bool loading;
    public RaceResultEntry LastResult { get; private set; }
    public SaveResult SavedResult { get; private set; }
    public bool SaveSucceeded { get; private set; }

    private void OnEnable()
    {
        if (session == null || timer == null || scorer == null || finishPanel == null ||
            finishTimeText == null || finishScoreText == null || recordText == null || garageButton == null || retryButton == null)
        { Debug.LogError("FinishPanelController: fill all scene references.", this); enabled = false; return; }
        session.OnStateChanged += OnStateChanged;
        if (session.State == RaceSessionManager.RaceState.Finished)
        {
            if (!finishHandled) ShowFinish();
            else finishPanel.SetActive(true);
        }
        else { finishPanel.SetActive(false); recordText.gameObject.SetActive(false); }
    }
    private void OnDisable()
    {
        if (session != null) session.OnStateChanged -= OnStateChanged;
    }
    private void OnStateChanged(RaceSessionManager.RaceState state)
    {
        if (state == RaceSessionManager.RaceState.Finished) ShowFinish();
    }
    private void ShowFinish()
    {
        if (finishHandled) return;
        finishHandled = true; // Latch before IO: re-enabling UI cannot duplicate this race.
        finishTimeText.text = timer.FormattedTime;
        finishScoreText.text = "Дрифт: " + scorer.Score;
        recordText.gameObject.SetActive(false);
        if (saveStatusText != null) saveStatusText.text = string.Empty;
        finishPanel.SetActive(true);
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        CarDefinition car = SelectedCarService.Get();
        if (car == null) car = fallbackCar;
        LastResult = new RaceResultEntry
        {
            id = Guid.NewGuid().ToString("N"),
            carId = car != null ? car.carId : "scene-car",
            carName = car != null ? car.displayName : session.CarBody != null ? session.CarBody.name : "Unknown car",
            timeSeconds = timer.TimeSeconds,
            driftScore = scorer.Score,
            maxMultiplier = scorer.MaxMultiplier,
            createdAtUtc = DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture)
        };
        try
        {
            SavedResult = ResultsRepository.SaveNewEntry(LastResult);
            SaveSucceeded = true;
            recordText.gameObject.SetActive(SavedResult.isNewRecord);
        }
        catch (Exception error) when (error is IOException || error is UnauthorizedAccessException || error is ArgumentException || error is NotSupportedException)
        {
            if (saveStatusText != null) saveStatusText.text = "Не удалось сохранить заезд";
            Debug.LogError("Race result could not be saved: " + error.Message, this);
        }
    }
    public void GoGarage() => LoadScene("Garage");
    public void Retry() => LoadScene("Touge01");
    public void OpenResults()
    {
        if (!finishHandled || loading || resultsController == null) return;
        resultsController.OpenResults();
    }
    private void LoadScene(string sceneName)
    {
        if (loading) return;
        if (!Application.CanStreamedLevelBeLoaded(sceneName))
        { if (saveStatusText != null) saveStatusText.text = "Сцена не добавлена в сборку: " + sceneName; return; }
        loading = true;
        garageButton.interactable = retryButton.interactable = false;
        if (resultsButton != null) resultsButton.interactable = false;
        Time.timeScale = 1;
        AudioListener.pause = false;
        session.SetPaused(false);
        SceneManager.LoadSceneAsync(sceneName);
    }
}
