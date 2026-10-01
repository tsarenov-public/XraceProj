using System;
using UnityEngine;
public class RaceTimer : MonoBehaviour
{
    [SerializeField] private RaceSessionManager session;
    public float TimeSeconds { get; private set; }
    public event Action<float> OnTimeChanged;
    public string FormattedTime
    {
        get
        {
            long milliseconds = (long)Math.Floor(TimeSeconds * 1000.0);
            return string.Format("{0:00}:{1:00}.{2:000}", milliseconds / 60000, milliseconds / 1000 % 60, milliseconds % 1000);
        }
    }
    private void Update()
    {
        if (session.State == RaceSessionManager.RaceState.Racing && !session.IsPaused)
        {
            if (Time.deltaTime <= 0) return;
            TimeSeconds += Time.deltaTime;
            OnTimeChanged?.Invoke(TimeSeconds);
        }
    }
}
