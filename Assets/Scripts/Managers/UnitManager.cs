using System;
using System.Drawing;

/// <summary>
///
/// </summary>
public class UnitManager
{
    private readonly Game game;

    /// <summary>
    /// Initializes a new instance of the UnitManager class.
    /// </summary>
    /// <param name="game">The game instance this manager is associated with.</param>
    public UnitManager(Game game)
    {
        this.game = game;
    }

    /// <summary>
    /// Updates the movement of a movable unit.
    /// </summary>
    /// <param name="movable">The movable unit to update.</param>
    public void UpdateMovement(IMovable movable)
    {
        // Early returns for units that shouldn't move.
        if (ShouldSkipMovement(movable))
        {
            return;
        }

        Planet destination = movable.GetParentOfType<Planet>();
        if (destination == null)
        {
            throw new GameStateException(
                $"Unit {movable.GetDisplayName()} in transit without destination."
            );
        }

        // Calculate and apply movement.
        Point newPosition = CalculateNewPosition(movable, destination);
        ApplyMovement(movable, newPosition);

        // Check if the unit has arrived at its destination.
        // If so, update its movement status accordingly.
        CheckArrival(movable, destination, newPosition);
    }

    /// <summary>
    /// Determines if movement should be skipped for the given movable.
    /// </summary>
    private bool ShouldSkipMovement(IMovable movable)
    {
        if (movable.MovementStatus != MovementStatus.InTransit)
        {
            return true;
        }

        if (
            movable is IManufacturable manufacturable
            && manufacturable.GetManufacturingStatus() == ManufacturingStatus.Building
        )
        {
            return true;
        }

        return false;
    }

    /// <summary>
    /// Calculates the new position for the movable unit.
    /// </summary>
    private Point CalculateNewPosition(IMovable movable, Planet destination)
    {
        Point targetPosition = destination.GetPosition();
        Point currentPosition = movable.GetPosition();

        int deltaX = targetPosition.X - currentPosition.X;
        int deltaY = targetPosition.Y - currentPosition.Y;

        int newX = currentPosition.X + Math.Sign(deltaX);
        int newY = currentPosition.Y + Math.Sign(deltaY);

        return new Point(newX, newY);
    }

    /// <summary>
    /// Applies the movement to the movable unit.
    /// </summary>
    private void ApplyMovement(IMovable movable, Point newPosition)
    {
        GameLogger.Log($"Incrementing movement for {movable.GetDisplayName()}.");
        movable.SetPosition(newPosition);
    }

    /// <summary>
    /// Checks if the unit has arrived at its destination and updates its status if so.
    /// </summary>
    private void CheckArrival(IMovable movable, Planet destination, Point newPosition)
    {
        if (newPosition == destination.GetPosition())
        {
            movable.SetMovementStatus(MovementStatus.Idle);
            GameLogger.Log($"Unit {movable.GetDisplayName()} has arrived at its destination.");
        }
    }
}
