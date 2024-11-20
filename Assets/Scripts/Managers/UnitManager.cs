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
    public void Update()
    {
        // Update the movement for each unit.
        foreach (IMovable movable in game.GetSceneNodesByType<IMovable>())
        {
            IncrementMovement(movable);
        }
    }

    /// <summary>
    ///
    /// </summary>
    /// <param name="movable"></param>
    private void IncrementMovement(IMovable movable)
    {
        Planet destination = movable.GetParentOfType<Planet>();

        if (movable.MovementStatus == MovementStatus.InTransit && destination != null)
        {
            // Retrieve target and current positions.
            Point targetPosition = destination.GetPosition();
            Point movablePosition = movable.GetPosition();

            // Calculate movement direction.
            int deltaX = targetPosition.X - movablePosition.X;
            int deltaY = targetPosition.Y - movablePosition.Y;

            // Update position by moving one step in both X and Y directions as needed.
            int newX = movablePosition.X + Math.Sign(deltaX);
            int newY = movablePosition.Y + Math.Sign(deltaY);

            movable.SetPosition(new Point(newX, newY));
        }
    }
}
