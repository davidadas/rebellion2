using System;
using System.Drawing;
using Rebellion.Game.Galaxy;
using Rebellion.SceneGraph;

namespace Rebellion.Game.Units
{
    /// <summary>
    /// An interface for scene nodes and units that can move within the galaxy map.
    /// </summary>
    public interface IMovable : ISceneNode
    {
        MovementState Movement { get; set; }

        /// <summary>
        /// Returns the movement state physically carrying this unit, including movement inherited
        /// from its capital ship or fleet.
        /// </summary>
        /// <returns>The active movement state, or null when the unit is stationary.</returns>
        MovementState GetTransitMovement()
        {
            if (Movement != null)
                return Movement;

            MovementState capitalShipMovement = GetParentOfType<CapitalShip>()?.Movement;
            return capitalShipMovement ?? GetParentOfType<Fleet>()?.Movement;
        }

        /// <summary>
        /// Returns the planet position while stationary or the current position while in transit.
        /// </summary>
        /// <returns>The unit's current position.</returns>
        Point GetPosition()
        {
            if (Movement != null)
                return Movement.CurrentPosition;

            Planet planet = GetParentOfType<Planet>();
            return planet?.GetPosition() ?? Point.Empty;
        }

        /// <summary>
        /// Updates the unit's current in-transit position.
        /// </summary>
        /// <param name="position">The new in-transit position.</param>
        /// <exception cref="InvalidOperationException">
        /// Thrown when the unit has no active movement.
        /// </exception>
        void SetPosition(Point position)
        {
            if (Movement == null)
            {
                throw new InvalidOperationException(
                    $"Cannot set position on {GetDisplayName()} without active movement."
                );
            }

            Movement.CurrentPosition = position;
        }

        /// <summary>
        /// Gets whether this unit is currently eligible to move.
        /// </summary>
        /// <returns>True when this unit can move; otherwise false.</returns>
        bool IsMovable();

        /// <summary>
        /// Gets whether this unit can impose a blockade.
        /// </summary>
        /// <returns>True when this unit can impose a blockade; otherwise false.</returns>
        bool CanBlockade() => false;

        /// <summary>
        /// Gets whether this unit can move despite a blockade.
        /// </summary>
        /// <returns>True when blockades do not prevent this unit from moving; otherwise false.</returns>
        bool IgnoresBlockade() => false;
    }
}
