public enum MovementStatus
{
    Stationary,
    InTransit,
}

/// <summary>
/// Represents an object that can be moved.
/// </summary>
public interface IMovable
{
    MovementStatus MovementStatus { get; set; }
}