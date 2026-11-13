using TMPro;
using UnityEngine;
public class RaceHud : MonoBehaviour
{
    [SerializeField] private RaceTimer timer;
    [SerializeField] private DriftScorer scorer;
    [SerializeField] private TMP_Text timeText;
    [SerializeField] private TMP_Text scoreText;
    [SerializeField] private TMP_Text multiplierText;
    private float refresh;
    private void Update()
    {
        refresh -= Time.unscaledDeltaTime;
        if (refresh > 0) return;
        refresh = 0.05f;
        timeText.text = timer.FormattedTime;
        scoreText.text = scorer.Score.ToString();
        multiplierText.text = "x" + scorer.currentMultiplier;
    }
}
