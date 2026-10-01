using System;
using UnityEngine;
public class DriftScorer : MonoBehaviour
{
    [SerializeField] private RaceSessionManager session;
    [SerializeField] private VehicleDriftStateAdapter driftState;
    [SerializeField, Min(0)] private float basePoints = 100;
    private DriftZone activeZone;
    private float driftTime;
    private double points;
    private IVehicleDriftState vehicleState;
    private void Awake() => vehicleState = driftState;
    public int successfulChain { get; private set; }
    public int nextExpectedZoneId { get; private set; } = 1;
    public int currentMultiplier { get; private set; } = 1;
    public int MaxMultiplier { get; private set; } = 1;
    public int Score => (int)Math.Min(int.MaxValue, Math.Floor(points));
    public event Action<int> OnScoreChanged;
    public event Action<int> OnMultiplierChanged;
    public event Action<int> OnZoneSuccess;
    public event Action OnChainReset;
    public void EnterZone(DriftZone zone)
    {
        if (session.State == RaceSessionManager.RaceState.Finished || zone.ZoneId < nextExpectedZoneId) return;
        if (activeZone != null) { Debug.LogWarning("Overlapping drift zones: separate their colliders.", zone); return; }
        if (zone.ZoneId > nextExpectedZoneId) ResetChain();
        nextExpectedZoneId = zone.ZoneId;
        activeZone = zone;
        driftTime = 0;
        SetMultiplier(successfulChain + 1);
    }
    private void FixedUpdate()
    {
        if (activeZone == null || session.State != RaceSessionManager.RaceState.Racing || session.IsPaused || !vehicleState.IsDrifting || vehicleState.SpeedKmh < activeZone.MinSpeedKmh) return;
        float dt = Time.fixedDeltaTime;
        driftTime += dt;
        int before = Score;
        points += basePoints * Mathf.Clamp01(vehicleState.SlipAngle / 45) * Mathf.Clamp01(vehicleState.SpeedKmh / 80) * currentMultiplier * dt;
        MaxMultiplier = Mathf.Max(MaxMultiplier, currentMultiplier);
        if (Score != before) OnScoreChanged?.Invoke(Score);
    }
    public void ExitZone(DriftZone zone)
    {
        if (activeZone != zone) return;
        if (session.State != RaceSessionManager.RaceState.Racing) { activeZone = null; return; }
        CompleteActiveZone();
    }
    public void CompleteActiveZone()
    {
        if (activeZone == null) return;
        int id = activeZone.ZoneId;
        bool success = driftTime >= activeZone.MinDriftTime && driftTime > 0;
        activeZone = null;
        nextExpectedZoneId = id + 1;
        if (success) { successfulChain++; OnZoneSuccess?.Invoke(id); }
        else ResetChain();
    }
    private void ResetChain() { successfulChain = 0; SetMultiplier(1); OnChainReset?.Invoke(); }
    private void SetMultiplier(int value)
    {
        if (currentMultiplier == value) return;
        currentMultiplier = value;
        OnMultiplierChanged?.Invoke(value);
    }
}
