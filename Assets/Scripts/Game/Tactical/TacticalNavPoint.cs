namespace Rebellion.Game.Tactical
{
    /// <summary>
    /// Identifies a persistent waypoint in tactical battle space.
    /// </summary>
    public sealed class TacticalNavPoint
    {
        /// <summary>
        /// Gets the waypoint's horizontal coordinate.
        /// </summary>
        public float X { get; }

        /// <summary>
        /// Gets the waypoint's vertical coordinate.
        /// </summary>
        public float Y { get; }

        /// <summary>
        /// Gets the waypoint's depth coordinate.
        /// </summary>
        public float Z { get; }

        /// <summary>
        /// Initializes a waypoint at a tactical-space position.
        /// </summary>
        /// <param name="x">The horizontal coordinate.</param>
        /// <param name="y">The vertical coordinate.</param>
        /// <param name="z">The depth coordinate.</param>
        public TacticalNavPoint(float x, float y, float z)
        {
            X = x;
            Y = y;
            Z = z;
        }
    }
}
