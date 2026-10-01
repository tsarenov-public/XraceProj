using System;
using TMPro;
using UnityEngine;

[DisallowMultipleComponent]
public class RaceHUDController : MonoBehaviour
{
    [SerializeField] private RaceSessionManager session;
    [SerializeField] private RaceTimer timer;
    [SerializeField] private DriftScorer scorer;
    [SerializeField] private GameObject hudPanel;
    [SerializeField] private TMP_Text timeText;
    [SerializeField] private TMP_Text scoreText;
    [SerializeField] private TMP_Text multiplierText;
    [SerializeField] private string scorePrefix = "Дрифт: ";
    private readonly char[] timeBuffer = new char[32];
    private readonly char[] multiplierBuffer = new char[16];
    private char[] scoreBuffer;
    private int prefixLength;
    private long displayedMilliseconds = -1;

    private void OnEnable()
    {
        if (session == null || timer == null || scorer == null || hudPanel == null ||
            timeText == null || scoreText == null || multiplierText == null)
        {
            Debug.LogError("RaceHUDController: fill all scene references.", this);
            enabled = false;
            return;
        }
        // Controller lives on Canvas, outside HUD_Panel, so hiding HUD keeps subscriptions alive.
        scorePrefix = scorePrefix ?? string.Empty;
        prefixLength = scorePrefix.Length;
        scoreBuffer = new char[prefixLength + 11];
        scorePrefix.CopyTo(0, scoreBuffer, 0, prefixLength);
        WarmText(timeText, timeBuffer, 0);
        WarmText(scoreText, scoreBuffer, prefixLength);
        multiplierBuffer[0] = 'x';
        WarmText(multiplierText, multiplierBuffer, 1);
        displayedMilliseconds = -1;
        timer.OnTimeChanged += DisplayTime;
        scorer.OnScoreChanged += DisplayScore;
        scorer.OnMultiplierChanged += DisplayMultiplier;
        session.OnStateChanged += DisplayState;
        DisplayTime(timer.TimeSeconds);
        DisplayScore(scorer.Score);
        DisplayMultiplier(scorer.currentMultiplier);
        DisplayState(session.State);
    }

    private void OnDisable()
    {
        if (timer != null) timer.OnTimeChanged -= DisplayTime;
        if (scorer != null)
        {
            scorer.OnScoreChanged -= DisplayScore;
            scorer.OnMultiplierChanged -= DisplayMultiplier;
        }
        if (session != null) session.OnStateChanged -= DisplayState;
    }

    private static void WarmText(TMP_Text text, char[] buffer, int prefix)
    {
        // Prime TMP's character/mesh capacity and numeric glyphs before the first racing frame.
        for (int i = prefix; i < buffer.Length; i++) buffer[i] = (char)('0' + (i - prefix) % 10);
        text.SetCharArray(buffer, 0, buffer.Length);
        text.ForceMeshUpdate(true);
    }

    private void DisplayTime(float seconds)
    {
        // Scene objects can be destroyed in a different order than their publishers.
        if (timeText == null) return;
        long milliseconds = (long)Math.Floor(Math.Max(0, seconds) * 1000.0);
        if (milliseconds == displayedMilliseconds) return;
        displayedMilliseconds = milliseconds;
        int length = AppendNumber(timeBuffer, 0, milliseconds / 60000, 2);
        timeBuffer[length++] = ':';
        length = AppendNumber(timeBuffer, length, milliseconds / 1000 % 60, 2);
        timeBuffer[length++] = '.';
        length = AppendNumber(timeBuffer, length, milliseconds % 1000, 3);
        timeText.SetCharArray(timeBuffer, 0, length);
    }

    private void DisplayScore(int score)
    {
        if (scoreText == null || scoreBuffer == null) return;
        int length = AppendNumber(scoreBuffer, prefixLength, Math.Max(0, score), 1);
        scoreText.SetCharArray(scoreBuffer, 0, length);
    }

    private void DisplayMultiplier(int multiplier)
    {
        if (multiplierText == null) return;
        multiplierBuffer[0] = 'x';
        int length = AppendNumber(multiplierBuffer, 1, Math.Max(1, multiplier), 1);
        multiplierText.SetCharArray(multiplierBuffer, 0, length);
    }

    private void DisplayState(RaceSessionManager.RaceState state)
    {
        if (hudPanel != null) hudPanel.SetActive(state != RaceSessionManager.RaceState.Finished);
    }

    private static int AppendNumber(char[] buffer, int offset, long value, int minimumDigits)
    {
        int digits = 1;
        for (long remaining = value; remaining >= 10; remaining /= 10) digits++;
        digits = Math.Max(digits, minimumDigits);
        int end = offset + digits;
        for (int i = end - 1; i >= offset; i--)
        {
            buffer[i] = (char)('0' + value % 10);
            value /= 10;
        }
        return end;
    }
}
