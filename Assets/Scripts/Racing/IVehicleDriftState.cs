public interface IVehicleDriftState
{
    float SpeedKmh { get; }
    float SlipAngle { get; }
    bool IsDrifting { get; }
    bool IsGrounded { get; }
}
