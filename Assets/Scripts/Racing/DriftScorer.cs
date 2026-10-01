using System;
using UnityEngine;
public class DriftScorer : MonoBehaviour
{
    [SerializeField] private RaceSessionManager session;
    [SerializeField] private VehicleDriftStateAdapter driftState;
    [SerializeField, Min(0)] private float basePoints = 100;
    [SerializeField, Min(0)] private float stopSpeedKmh = 3;
    [SerializeField, Min(0)] private float stopResetDelay = 0.25f;
    [SerializeField, Min(0)] private float collisionResetDeltaSpeedMps = 1.5f;
    [SerializeField, Range(0, 1)] private float collisionMinHorizontalNormal = 0.6f;
    [SerializeField, Min(0)] private float collisionResetCooldown = 0.15f;
    private DriftZone activeZone;
    private float driftTime;
    private double points;
    private IVehicleDriftState vehicleState;
    private float stoppedTime;
    private bool stoppedResetApplied;
    private float lastCollisionResetTime = float.NegativeInfinity;
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
        if (zone == null || session == null || !session.isActiveAndEnabled || !isActiveAndEnabled) return;
        if (session.State == RaceSessionManager.RaceState.Finished || zone.ZoneId < nextExpectedZoneId) return;
        if (activeZone != null) { Debug.LogWarning("Overlapping drift zones: separate their colliders.", zone); return; }
        if (zone.ZoneId > nextExpectedZoneId) ResetChain();
        nextExpectedZoneId = zone.ZoneId;
        activeZone = zone;
        driftTime = 0;
        // Entering a corner is not a drift. Show/apply the chain multiplier
        // only while the vehicle actually satisfies the drift conditions.
        SetMultiplier(1);
    }
    private void FixedUpdate()
    {
        if (!CanScore || vehicleState == null) return;
        float speed = vehicleState.SpeedKmh;
        CheckStopped(speed);
        if (activeZone == null) return;
        if (vehicleState == null || !vehicleState.IsGrounded || !vehicleState.IsDrifting)
        {
            SetMultiplier(1);
            return;
        }
        float slip = vehicleState.SlipAngle;
        if (float.IsNaN(speed) || float.IsInfinity(speed) || float.IsNaN(slip) || float.IsInfinity(slip)
            || speed < activeZone.MinSpeedKmh || slip <= 0)
        {
            SetMultiplier(1);
            return;
        }
        SetMultiplier(successfulChain + 1);
        float dt = Time.fixedDeltaTime;
        driftTime += dt;
        int before = Score;
        points += basePoints * Mathf.Clamp01(slip / 45) * Mathf.Clamp01(speed / 80) * currentMultiplier * dt;
        MaxMultiplier = Mathf.Max(MaxMultiplier, currentMultiplier);
        if (Score != before) OnScoreChanged?.Invoke(Score);
    }
    private bool CanScore => session != null && session.isActiveAndEnabled && isActiveAndEnabled
        && session.State == RaceSessionManager.RaceState.Racing && !session.IsPaused && Time.timeScale > 0;
    private void CheckStopped(float speed)
    {
        if (float.IsNaN(speed) || float.IsInfinity(speed)) return;
        if (speed > stopSpeedKmh + 1)
        {
            stoppedTime = 0;
            stoppedResetApplied = false;
            return;
        }
        if (speed > stopSpeedKmh)
        {
            stoppedTime = 0;
            return;
        }
        if (stoppedResetApplied) return;
        stoppedTime += Time.fixedDeltaTime;
        if (stoppedTime < stopResetDelay) return;
        stoppedResetApplied = true;
        BreakChain();
    }
    // The relay supplies impulse / mass, in m/s. Vertical road/landing
    // contacts and light touches do not count as a damaging side impact.
    public bool NotifyCollision(float deltaSpeedMps, Vector3 contactNormal)
    {
        if (!CanScore || float.IsNaN(deltaSpeedMps) || float.IsInfinity(deltaSpeedMps)
            || deltaSpeedMps < collisionResetDeltaSpeedMps || contactNormal.sqrMagnitude < 0.01f
            || Time.fixedTime - lastCollisionResetTime < collisionResetCooldown) return false;
        float horizontalNormal = Vector3.ProjectOnPlane(contactNormal.normalized, Vector3.up).magnitude;
        if (float.IsNaN(horizontalNormal) || float.IsInfinity(horizontalNormal) || horizontalNormal < collisionMinHorizontalNormal) return false;
        lastCollisionResetTime = Time.fixedTime;
        BreakChain();
        return true;
    }
    private void BreakChain()
    {
        // Keep the current zone: a fresh post-stop/post-impact drift can
        // still succeed at x1, but the preceding drift time cannot qualify it.
        driftTime = 0;
        ResetChain();
    }
    public void ExitZone(DriftZone zone)
    {
        if (activeZone != zone) return;
        if (session == null || !session.isActiveAndEnabled || !isActiveAndEnabled) { CancelZone(zone); return; }
        if (session.State != RaceSessionManager.RaceState.Racing) { activeZone = null; SetMultiplier(1); return; }
        CompleteActiveZone();
    }
    // Disabling/unloading a zone is cancellation, not crossing its exit.
    // A manually disabled active zone breaks the chain without awarding it;
    // teardown with disabled owners also avoids publishing UI callbacks.
    public void CancelZone(DriftZone zone)
    {
        if (zone == null || activeZone != zone) return;
        activeZone = null;
        driftTime = 0;
        nextExpectedZoneId = zone.ZoneId + 1;
        ResetChain(session != null && session.isActiveAndEnabled && isActiveAndEnabled);
    }
    public void CompleteActiveZone()
    {
        if (activeZone == null) return;
        if (session == null || !session.isActiveAndEnabled || !isActiveAndEnabled) { CancelZone(activeZone); return; }
        int id = activeZone.ZoneId;
        bool success = driftTime >= activeZone.MinDriftTime && driftTime > 0;
        activeZone = null;
        SetMultiplier(1);
        nextExpectedZoneId = id + 1;
        if (success) { successfulChain++; OnZoneSuccess?.Invoke(id); }
        else ResetChain();
    }
    private void ResetChain(bool notify = true) { successfulChain = 0; SetMultiplier(1, notify); if (notify) OnChainReset?.Invoke(); }
    private void SetMultiplier(int value, bool notify = true)
    {
        if (currentMultiplier == value) return;
        currentMultiplier = value;
        if (notify) OnMultiplierChanged?.Invoke(value);
    }
}
