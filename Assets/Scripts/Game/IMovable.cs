using System.Drawing;

public enum MovementStatus
{
    Idle,
    InTransit,
}

/// <summary>
///
/// </summary>
public interface IMovable
{
    int PositionX { get; set; }
    int PositionY { get; set; }
    MovementStatus MovementStatus { get; set; }

    /// <summary>
    /// Checks if the unit is allowed to move.
    /// </summary>
    bool IsMovable();

    /// <summary>
    /// Moves the unit to the specified target location.
    /// </summary>
    /// <param name="target">The target scene node to move to.</param>
    void MoveTo(SceneNode target)
    {
        // Ensure the unit is allowed to move.
        if (!IsMovable())
        {
            throw new InvalidSceneOperationException($"Unit is not movable.");
        }

        if (this is SceneNode sceneNode)
        {
            // Ensure the unit has a parent before moving.
            if (sceneNode.GetParent() == null)
            {
                throw new InvalidSceneOperationException(
                    $"Unit {sceneNode.DisplayName} must have a parent to move."
                );
            }

            // Get the closest parent planet of the target.
            Planet targetPlanet = target.GetParentOfType<Planet>();
            Point targetPosition = targetPlanet.GetPosition();

            // If already at the target location, adjust parent-child relationships.
            if (GetPosition() == targetPosition)
            {
                sceneNode.SetParent(target);
                target.AddChild(sceneNode);
                return;
            }

            // Move the unit to the target, update its position, and set it as InTransit.
            SetPosition(targetPosition);
            sceneNode.SetParent(target);
            target.AddChild(sceneNode);
            MovementStatus = MovementStatus.InTransit;

            // Move any children that also implement IMovable.
            sceneNode.Traverse(child =>
            {
                if (child is IMovable movable)
                {
                    movable.MoveTo(target);
                }
            });
        }
    }

    /// <summary>
    /// Gets the unit's current position. If not in transit, returns the position of its closest parent planet.
    /// </summary>
    /// <returns>The current position of the unit.</returns>
    Point GetPosition()
    {
        if (this is SceneNode sceneNode && MovementStatus != MovementStatus.InTransit)
        {
            return sceneNode.GetParentOfType<Planet>().GetPosition();
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
}
