using System;
using System.Drawing;
using Rebellion.Game;
using Rebellion.SceneGraph;

namespace Rebellion.Util.Extensions
{
    /// <summary>
    /// Extension methods for IMovable interface.
    /// Provides shared GetPosition/SetPosition behavior without duplication.
    /// </summary>
    public static class IMovableExtensions
    {
        /// <summary>
        /// Gets the current position of the movable unit.
        /// Returns parent planet position if not moving, or current transit position if in-transit.
        /// </summary>
        /// <param name="movable">The movable unit.</param>
        /// <returns>The current position.</returns>
        public static Point GetPosition(this IMovable movable)
        {
            // Movement == null means not moving (at parent planet)
            if (movable.Movement == null)
            {
                Planet parent = movable.GetParentOfType<Planet>();
                return parent != null ? parent.GetPosition() : new Point(0, 0);
            }
            // Movement != null means in transit
            return movable.Movement.CurrentPosition;
        }

        /// <summary>
        /// Sets the current transit position.
        /// Should only be called by MovementSystem during active movement.
        /// </summary>
        /// <param name="movable">The movable unit.</param>
        /// <param name="position">The new position.</param>
        public static void SetPosition(this IMovable movable, Point position)
        {
            if (movable.Movement == null)
            {
                throw new InvalidOperationException(
                    $"Cannot set position on {movable.GetDisplayName()} without active movement. "
                        + "MovementSystem must create MovementState first."
                );
            }
            movable.Movement.CurrentPosition = position;
        }

        /// <summary>
        /// Sets the current transit position using X/Y coordinates.
        /// </summary>
        /// <param name="movable">The movable unit.</param>
        /// <param name="x">The X coordinate.</param>
        /// <param name="y">The Y coordinate.</param>
        public static void SetPosition(this IMovable movable, int x, int y)
        {
            movable.SetPosition(new Point(x, y));
        }
    }
}
