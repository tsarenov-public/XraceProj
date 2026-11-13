using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Keep this controller on the active Canvas, outside the panels it opens/closes.
[DisallowMultipleComponent]
public class ResultsPanelController : MonoBehaviour
{
    [SerializeField] private FinishPanelController finishController;
    [SerializeField] private GameObject finishPanel;
    [SerializeField] private GameObject resultsPanel;
    [SerializeField] private GameObject currentResultPanel;
    [SerializeField] private TMP_Text currentResultText;
    [SerializeField] private TMP_Text currentRecordText;
    [SerializeField] private TMP_Text statusText;
    [SerializeField] private ScrollRect historyScroll;
    [SerializeField] private RectTransform historyContent;
    [SerializeField] private GameObject rowTemplate;
    private readonly List<GameObject> rows = new List<GameObject>();

    private void Awake()
    {
        if (finishController == null || finishPanel == null || resultsPanel == null || currentResultPanel == null ||
            currentResultText == null || currentRecordText == null || statusText == null || historyScroll == null ||
            historyContent == null || rowTemplate == null || rowTemplate.GetComponentInChildren<TMP_Text>(true) == null ||
            transform.IsChildOf(resultsPanel.transform) || transform.IsChildOf(finishPanel.transform))
        {
            Debug.LogError("ResultsPanelController: assign all references on the active Canvas, outside both panels.", this);
            enabled = false;
            return;
        }
        resultsPanel.SetActive(false);
        rowTemplate.SetActive(false);
    }

    public void OpenResults()
    {
        if (!isActiveAndEnabled || finishController.LastResult == null) return;
        ClearRows();
        statusText.text = string.Empty;
        RaceResultEntry current = finishController.LastResult;
        int currentPlace = finishController.SaveSucceeded ? finishController.SavedResult.place : 0;
        currentResultPanel.SetActive(true);
        currentRecordText.gameObject.SetActive(finishController.SaveSucceeded && finishController.SavedResult.isNewRecord);
        currentRecordText.text = "НОВЫЙ РЕКОРД";
        // The current run remains visible even if persistent storage cannot be read.
        currentResultText.text = CurrentLabel(current, currentPlace);
        finishPanel.SetActive(false);
        resultsPanel.SetActive(true);
        resultsPanel.transform.SetAsLastSibling();
        try
        {
            ResultsData data = ResultsRepository.Load();
            bool foundCurrent = false;
            for (int i = 0; i < data.results.Count; i++)
            {
                RaceResultEntry entry = data.results[i];
                if (entry.id == current.id)
                {
                    currentPlace = i + 1;
                    foundCurrent = true;
                    continue; // Pinned above the scroll area, never repeated in history.
                }
                GameObject row = Instantiate(rowTemplate, historyContent, false);
                row.name = "ResultRow_" + entry.id;
                TMP_Text label = row.GetComponentInChildren<TMP_Text>(true);
                label.richText = false;
                label.text = EntryLabel(entry, i + 1);
                row.SetActive(true);
                rows.Add(row);
            }
            currentResultText.text = CurrentLabel(current, foundCurrent ? currentPlace : 0);
            if (!finishController.SaveSucceeded || !foundCurrent)
                statusText.text = "Текущий заезд не сохранён. Остальная история показана ниже.";
            else if (rows.Count == 0)
                statusText.text = "Это ваш первый заезд.";
        }
        catch (Exception error) when (error is IOException || error is UnauthorizedAccessException || error is ArgumentException || error is NotSupportedException)
        {
            statusText.text = "Не удалось загрузить историю заездов. Текущий результат показан сверху.";
            Debug.LogWarning("Race history could not be displayed: " + error.Message, this);
        }
        Canvas.ForceUpdateCanvases();
        LayoutRebuilder.ForceRebuildLayoutImmediate(historyContent);
        historyScroll.StopMovement();
        historyScroll.verticalNormalizedPosition = 1;
    }

    public void Back()
    {
        if (resultsPanel != null) resultsPanel.SetActive(false);
        if (finishPanel != null) finishPanel.SetActive(true);
    }

    private void ClearRows()
    {
        foreach (GameObject row in rows)
        {
            if (row == null) continue;
            row.SetActive(false);
            Destroy(row);
        }
        rows.Clear();
    }

    private static string CurrentLabel(RaceResultEntry entry, int place)
    {
        string rank = place > 0 ? "Место: " + place : "Заезд не сохранён";
        return "ТЕКУЩИЙ ЗАЕЗД · " + rank + "\n" + EntryLabel(entry, 0);
    }

    private static string EntryLabel(RaceResultEntry entry, int place)
    {
        string name = string.IsNullOrWhiteSpace(entry.carName) ? "Без названия" : entry.carName.Replace('\n', ' ').Replace('\r', ' ');
        string rank = place > 0 ? place + ". " : string.Empty;
        return rank + name + "\n" + FormatTime(entry.timeSeconds) + "  ·  Дрифт: " + entry.driftScore;
    }

    private static string FormatTime(float seconds)
    {
        long milliseconds = (long)Math.Floor(seconds * 1000.0);
        return string.Format(CultureInfo.InvariantCulture, "{0:00}:{1:00}.{2:000}",
            milliseconds / 60000, milliseconds / 1000 % 60, milliseconds % 1000);
    }
}
