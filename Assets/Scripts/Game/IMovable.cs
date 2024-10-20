/// <summary>
/// 
/// </summary>
public enum MovementStatus
{
    InTransit,
    Idle,
}

/// <summary>
/// 
/// </summary>
public interface IMovable
{
    public MovementStatus MovementStatus { get; set; }
}
