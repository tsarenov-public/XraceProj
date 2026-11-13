using System;
using UnityEngine;

[DefaultExecutionOrder(-100)]
public class RaceSessionManager : MonoBehaviour
{
    public enum RaceState { Ready, Racing, Finished }
    [SerializeField, Min(0.1f)] private float startSpeedKmh = 5;
    [SerializeField] private VehicleDriftStateAdapter driftState;
    [SerializeField] private RaceTimer timer;
    [SerializeField] private DriftScorer scorer;
    [SerializeField] private CarStopOnFinish stopper;
    public RaceState State { get; private set; } = RaceState.Ready;
    public Rigidbody CarBody { get; private set; }
    public bool IsPaused { get; private set; }
    public event Action<RaceState> OnStateChanged;
    public void BindCar(Rigidbody body)
    {
        if (CarBody != null) return;
        CarBody = body;
        driftState.Bind(body);
        stopper.Bind(body);
    }
    private void Update()
    {
        if (State == RaceState.Ready && CarBody != null && !IsPaused && Time.timeScale > 0
            && Vector3.ProjectOnPlane(CarBody.linearVelocity, Vector3.up).magnitude * 3.6f > startSpeedKmh)
        { State = RaceState.Racing; OnStateChanged?.Invoke(State); }
    }
    public bool TryFinish()
    {
        if (State != RaceState.Racing || IsPaused || Time.timeScale <= 0) return false;
        scorer.CompleteActiveZone();
        State = RaceState.Finished;
        stopper.StopCar();
        OnStateChanged?.Invoke(State);
        Debug.Log("Race finished: " + timer.FormattedTime + ", drift " + scorer.Score);
        return true;
    }
    // The pause UI will also set Time.timeScale. This flag gates trigger/scoring logic.
    public void SetPaused(bool paused)
    {
        IsPaused = paused;
        if (MusicManager.Instance != null) MusicManager.Instance.SetRacePaused(paused);
    }
}
