using System;
using System.Drawing;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Movement;
using Rebellion.Game.Units;

namespace Rebellion.Util.Extensions
{
    /// <summary>
    /// Extension methods for IMovable.
    /// Provides shared GetPosition/SetPosition behavior across concrete movable types.
    /// </summary>
    public static class IMovableExtensions
    {
        /// <summary>
        /// Returns the movement state that physically carries a unit, including movement inherited from its capital ship or fleet.
        /// </summary>
        /// <param name="movable">The movable entity to inspect.</param>
        /// <returns>The active movement state, or null when the entity is stationary.</returns>
        public static MovementState GetTransitMovement(this IMovable movable)
        {
            if (movable?.Movement != null)
                return movable.Movement;

            MovementState capitalShipMovement = movable?.GetParentOfType<CapitalShip>()?.Movement;
            return capitalShipMovement ?? movable?.GetParentOfType<Fleet>()?.Movement;
        }

        /// <summary>
        /// Returns the planet position if idle, or the current transit position if in transit.
        /// </summary>
        /// <param name="movable">The movable entity to get the position of.</param>
        /// <returns>The current position as a Point.</returns>
        public static Point GetPosition(this IMovable movable)
        {
            if (movable.Movement == null)
            {
                Planet parent = movable.GetParentOfType<Planet>();
                return parent != null ? parent.GetPosition() : new Point(0, 0);
            }
            return movable.Movement.CurrentPosition;
        }

        /// <summary>
        /// Sets the current transit position. Only valid while a MovementState is active.
        /// </summary>
        /// <param name="movable">The movable entity to update.</param>
        /// <param name="position">The new transit position to assign.</param>
        public static void SetPosition(this IMovable movable, Point position)
        {
            if (movable.Movement == null)
            {
                throw new InvalidOperationException(
                    $"Cannot set position on {movable.GetDisplayName()} without active movement."
                );
            }
            movable.Movement.CurrentPosition = position;
        }
    }
}
