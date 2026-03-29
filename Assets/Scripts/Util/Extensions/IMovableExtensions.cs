using System;
using System.Drawing;
using Rebellion.Game;

namespace Rebellion.Util.Extensions
{
    /// <summary>
    /// Extension methods for IMovable.
    /// Provides shared GetPosition/SetPosition behavior across concrete movable types.
    /// </summary>
    public static class IMovableExtensions
    {
        /// <summary>
        /// Returns the planet position if idle, or the current transit position if in transit.
        /// </summary>
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
