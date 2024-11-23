using System.Drawing;

public enum MovementStatus
{
    Idle,
    InTransit,
}

/// <summary>
/// An interface for scene nodes/units that can be moved within the GalaxyMap.
/// </summary>
public interface IMovable : ISceneNode
{
    int PositionX { get; set; }
    int PositionY { get; set; }
    MovementStatus MovementStatus { get; set; }

    /// <summary>
    /// Used to determine whether this IMovable can be moved.
    /// </summary>
    /// <returns>True if the IMovable can be moved, false otherwise.</returns>
    bool IsMovable();

    /// <summary>
    /// Used to move an IMovable from one location to another.
    /// Provides a default implementation which adds the unit to the location and
    /// sets its ManufacturingStatus to "InTransit".
    /// </summary>
    /// <param name="target">This target scene node to move to.</param>
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

        // Set child-parent relationships.
        SetParent(target);
        target.AddChild(this);

        // If already at the target location, adjust parent-child relationships.
        if (GetPosition() == targetPosition)
        {
            return;
        }

        // Move the unit to the target, update its position, and set it as InTransit.
        SetPosition(targetPosition);
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
    /// Used to get this IMovable current position within the GalaxyMap.
    /// A default implementation is provided which uses the current IMovable's
    /// X,Y location when it hs otherwise not been set.
    /// </summary>
    /// <returns>This current position of the unit.</returns>
    Point GetPosition()
    {
        if (MovementStatus != MovementStatus.InTransit)
        {
            return GetParentOfType<Planet>().GetPosition();
        }
        return new Point(PositionX, PositionY);
    }

    /// <summary>
    /// Used to set this IMovable's next location. Provides a
    /// default implementation which performs a simple set operation.
    /// </summary>
    /// <param name="position">This new position as a Point object.</param>
    void SetPosition(Point position)
    {
        PositionX = position.X;
        PositionY = position.Y;
    }

    /// <summary>
    /// Used to set this IMovable's next location. Provides a
    /// default implementation which performs a simple set operation.
    /// </summary>
    /// <param name="x">This IMovable's new X position.</param>
    /// <param name="y">This IMovable's new Y position.</param>
    void SetPosition(int x, int y)
    {
        PositionX = x;
        PositionY = y;
    }

    /// <summary>
    /// Used to change this IMovable's MovementStatus. Provides a default
    /// implementation which performs a simple set operation.
    /// </summary>
    /// <param name="movementStatus">This IMovable's new movement status.</param>
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
