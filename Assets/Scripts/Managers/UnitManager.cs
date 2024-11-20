using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;

public class UnitManager
{
    private Game game;

    /// <summary>
    ///
    /// </summary>
    /// <param name="game"></param>
    public UnitManager(Game game)
    {
        this.game = game;
    }

    /// <summary>
    ///
    /// </summary>
    /// <param name="movable"></param>
    public void UpdateMovement(IMovable movable)
    {
        Planet destination = movable.GetParentOfType<Planet>();

        // Retrieve target and current positions.
        Point targetPosition = destination.GetPosition();
        Point movablePosition = movable.GetPosition();

        if (movable.MovementStatus == MovementStatus.InTransit && destination != null)
        {
            // Calculate movement direction.
            int deltaX = targetPosition.X - movablePosition.X;
            int deltaY = targetPosition.Y - movablePosition.Y;

            // Update position by moving one step in both X and Y directions as needed.
            int newX = movablePosition.X + Math.Sign(deltaX);
            int newY = movablePosition.Y + Math.Sign(deltaY);

            movable.SetPosition(new Point(newX, newY));
        }

        // Check if unit has arrived at its destination.
        // If so, set its status accordingly.
        if (movablePosition.X == targetPosition.X && movablePosition.Y == targetPosition.Y)
        {
            movable.SetMovementStatus(MovementStatus.Idle);
        }
    }
}
