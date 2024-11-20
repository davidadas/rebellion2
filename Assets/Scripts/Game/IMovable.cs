using System.Drawing;

public enum MovementStatus
{
    Idle,
    InTransit,
}

/// <summary>
///
/// </summary>
public interface IMovable : ISceneNode
{
    public int PositionX { get; set; }
    public int PositionY { get; set; }
    MovementStatus MovementStatus { get; set; }

    /// <summary>
    /// Called when determining whether an IMovable can be moved.
    /// </summary>
    /// <returns>True if the IMovable can be moved, false otherwise.</returns>
    bool IsMovable();

    /// <summary>
    /// Called when moving an IMovable from one location to another.
    /// Provides a default implementation which adds the unit to the location and
    /// sets its ManufacturingStatus to "InTransit".
    /// </summary>
    /// <param name="target">The target scene node to move to.</param>
    void MoveTo(ISceneNode target)
    {
        if (GetParent() == null)
        {
            throw new InvalidSceneOperationException(
                $"Unit {GetDisplayName()} must have a parent to move."
            );
        }

        // Get the closest parent planet of the target.
        Planet targetPlanet = target.GetParentOfType<Planet>();
        Point targetPosition = targetPlanet.GetPosition();

        // If already at the target location, adjust parent-child relationships.
        if (GetPosition() == targetPosition)
        {
            SetParent(target);
            target.AddChild(this);
            return;
        }

        // Move the unit to the target, update its position, and set it as InTransit.
        SetPosition(targetPosition);
        SetParent(target);
        target.AddChild(this);
        MovementStatus = MovementStatus.InTransit;

        // Move any children that also implement IMovable.
        Traverse(child =>
        {
            if (child is IMovable movable)
            {
                movable.MoveTo(target);
            }
        });
    }

    /// <summary>
    /// Called when moving an IMovable to determine its position in relation to
    /// its final destination. A default implementation is provided which uses the
    /// current IMovable's X,Y location when it hs otherwise not been set.
    /// </summary>
    /// <returns>The current position of the unit.</returns>
    Point GetPosition()
    {
        if (MovementStatus != MovementStatus.InTransit)
        {
            return GetParentOfType<Planet>().GetPosition();
        }
        return new Point(PositionX, PositionY);
    }

    /// <summary>
    /// Sets the unit's position to a new location.
    /// </summary>
    /// <param name="position">The new position as a Point.</param>
    void SetPosition(Point position)
    {
        PositionX = position.X;
        PositionY = position.Y;
    }

    /// <summary>
    ///
    /// </summary>
    /// <param name="movementStatus"></param>
    void SetMovementStatus(MovementStatus movementStatus)
    {
        MovementStatus = movementStatus;
    }

    /// <summary>
    ///
    /// </summary>
    /// <returns></returns>
    MovementStatus GetMovementStatus()
    {
        return MovementStatus;
    }

    /// <summary>
    ///
    /// </summary>
    /// <returns></returns>
    bool CanBlockade()
    {
        return false;
    }

    /// <summary>
    ///
    /// </summary>
    /// <returns></returns>
    bool IgnoresBlockade()
    {
        return false;
    }
}
