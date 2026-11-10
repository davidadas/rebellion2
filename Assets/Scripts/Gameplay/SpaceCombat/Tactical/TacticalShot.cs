using System;
using Rebellion.Game.Results;

namespace Rebellion.Gameplay
{
    /// <summary>
    /// Describes one recently fired weapon lane for presentation: a bolt that flies from the
    /// muzzle to the target at a fixed speed.
    /// </summary>
    public sealed class TacticalShot
    {
        private const double _boltSpeed = 160;
        private const double _torpedoSpeed = 30;
        private const double _torpedoMinimumSeconds = 2;
        private const double _capitalBoltLength = 6;
        private const double _starfighterBoltLength = 3;
        private const double _torpedoLength = 2;

        public CombatSide Side { get; }

        public double StartX { get; }

        public double StartY { get; }

        public double EndX { get; }

        public double EndY { get; }

        public bool IsIon { get; }

        public bool IsStarfighterShot { get; }

        public bool IsTorpedo { get; }

        public int Strength { get; }

        public double TotalSeconds { get; }

        public double RemainingSeconds { get; internal set; }

        /// <summary>
        /// Gets how far along its flight the bolt is, from 0 at the muzzle to 1 at the target.
        /// </summary>
        public double Progress =>
            TotalSeconds > 0 ? Math.Clamp(1 - RemainingSeconds / TotalSeconds, 0, 1) : 1;

        /// <summary>
        /// Gets the bolt's length in battle units.
        /// </summary>
        public double BoltLength =>
            IsTorpedo ? _torpedoLength
            : IsStarfighterShot ? _starfighterBoltLength
            : _capitalBoltLength;

        /// <summary>
        /// Creates one weapon-fire record.
        /// </summary>
        /// <param name="side">The side that fired.</param>
        /// <param name="startX">The firing unit's horizontal coordinate.</param>
        /// <param name="startY">The firing unit's vertical coordinate.</param>
        /// <param name="endX">The target's horizontal coordinate.</param>
        /// <param name="endY">The target's vertical coordinate.</param>
        /// <param name="isIon">Whether the shot came from an ion weapon.</param>
        /// <param name="isStarfighterShot">Whether a fighter squadron fired the shot.</param>
        /// <param name="isTorpedo">Whether the shot is a torpedo, which flies slower than a bolt.</param>
        /// <param name="strength">The raw weapon strength behind the shot, for presentation.</param>
        /// <param name="remainingSeconds">
        /// The shortest time the shot remains visible; longer flights take longer.
        /// </param>
        internal TacticalShot(
            CombatSide side,
            double startX,
            double startY,
            double endX,
            double endY,
            bool isIon,
            bool isStarfighterShot,
            bool isTorpedo,
            int strength,
            double remainingSeconds
        )
        {
            Side = side;
            StartX = startX;
            StartY = startY;
            EndX = endX;
            EndY = endY;
            IsIon = isIon;
            IsStarfighterShot = isStarfighterShot;
            IsTorpedo = isTorpedo;
            Strength = Math.Max(strength, 0);
            double distance = TacticalBattleGeometry.GetDistance(startX, startY, endX, endY);
            // Torpedoes crawl: slow in flight and never a flash across the gap like a bolt.
            TotalSeconds = isTorpedo
                ? Math.Max(
                    Math.Max(remainingSeconds, _torpedoMinimumSeconds),
                    distance / _torpedoSpeed
                )
                : Math.Max(remainingSeconds, distance / _boltSpeed);
            RemainingSeconds = TotalSeconds;
        }
    }
}
