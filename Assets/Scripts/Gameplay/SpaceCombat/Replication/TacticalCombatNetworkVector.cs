using System;
using System.Numerics;

namespace Rebellion.Gameplay
{
    /// <summary>
    /// Stores a serializable encounter-local vector without engine object references.
    /// </summary>
    [Serializable]
    public struct TacticalCombatNetworkVector
    {
        public float X;
        public float Y;
        public float Z;

        /// <summary>
        /// Captures an encounter-local vector.
        /// </summary>
        /// <param name="value">The vector being captured.</param>
        public TacticalCombatNetworkVector(Vector3 value)
        {
            X = value.X;
            Y = value.Y;
            Z = value.Z;
        }

        /// <summary>
        /// Restores a vector for a local presentation consumer.
        /// </summary>
        /// <returns>The encounter-local vector.</returns>
        public Vector3 ToVector() => new(X, Y, Z);
    }
}
