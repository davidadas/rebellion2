using System;
using System.Collections.Generic;
using Rebellion.Game;
using Rebellion.Game.Results;
using Rebellion.SceneGraph;
using Rebellion.Util.Random;

namespace Rebellion.Gameplay
{
    /// <summary>
    /// Receives one weapon lane's damage against a tactical target.
    /// </summary>
    /// <param name="target">The unit receiving the hit.</param>
    /// <param name="damage">The positive damage dealt.</param>
    /// <param name="isIonDamage">Whether the hit came from an ion weapon.</param>
    internal delegate void TacticalHitSink(
        TacticalBattleUnit target,
        double damage,
        bool isIonDamage
    );

    /// <summary>
    /// Provides shared position, withdrawal, and attack state for a unit in a tactical battle.
    /// </summary>
    public abstract class TacticalBattleUnit
    {
        // Capital hulls are presented several times larger than their simulation radius; fighters
        // range and circle against this footprint so they engage outside the visible hull.
        internal const double HullFootprintScale = 3.0;

        // A missed shot flies on past its target and lands somewhere beside it.
        private const double _missOvershoot = 8;
        private const double _missSpread = 4;

        private readonly double _minimumManeuverRatio;
        private bool _hasWithdrawn;
        private bool _isWithdrawing;

        public CombatSide Side { get; }

        public double X { get; internal set; }

        public double Y { get; internal set; }

        public double Heading { get; internal set; }

        public abstract ISceneNode Node { get; }

        public abstract bool IsAlive { get; }

        public abstract bool IsStarfighter { get; }

        public abstract double Radius { get; }

        public bool HasWithdrawn => _hasWithdrawn;

        public bool IsWithdrawing => _isWithdrawing;

        /// <summary>
        /// Gets whether enemies can still engage this unit: a ship running for hyperspace stays
        /// under fire until it is gone.
        /// </summary>
        public bool IsTargetable => IsAlive && !_hasWithdrawn;

        public string DisplayName => Node?.GetDisplayName();

        /// <summary>
        /// Gets the radius other units keep clear of: the presented hull size for capital ships,
        /// nothing for fighter squadrons.
        /// </summary>
        public double HullFootprint => IsStarfighter ? 0 : Radius * HullFootprintScale;

        /// <summary>
        /// Gets which class of enemy this unit engages first when choosing its own targets.
        /// </summary>
        public TacticalTargetPriority TargetPriority { get; internal set; }

        internal abstract bool CanWithdrawIndependently { get; }

        internal abstract double ManeuverRate { get; }

        internal abstract double Speed { get; }

        internal abstract double RemainingDurability { get; }

        internal virtual bool IsAttackDelayed => false;

        internal bool CanFire => IsTargetable && !IsAttackDelayed;

        protected double MinimumManeuverRatio => _minimumManeuverRatio;

        /// <summary>
        /// Creates tactical state for one unit.
        /// </summary>
        /// <param name="side">The side the unit fights for.</param>
        /// <param name="minimumManeuverRatio">The minimum maneuver value and multiplier.</param>
        protected TacticalBattleUnit(CombatSide side, double minimumManeuverRatio)
        {
            Side = side;
            _minimumManeuverRatio = minimumManeuverRatio;
        }

        /// <summary>
        /// Calculates this unit's remaining tactical strength against a target type.
        /// </summary>
        /// <param name="targetsFighters">Whether the opposing target is a fighter squadron.</param>
        /// <returns>The remaining tactical strength.</returns>
        internal abstract double GetEffectiveness(bool targetsFighters);

        /// <summary>
        /// Queues attacks from every charged weapon against the available targets.
        /// </summary>
        /// <param name="targets">The targetable opposing units.</param>
        /// <param name="hitSink">Receives each queued hit.</param>
        /// <param name="shots">Receives presentation records for each fired lane.</param>
        /// <param name="shotDisplaySeconds">How long each fired lane remains visible.</param>
        /// <param name="random">The combat random-number stream used for hit rolls.</param>
        internal abstract void QueueAttacks(
            IReadOnlyList<TacticalBattleUnit> targets,
            TacticalHitSink hitSink,
            ICollection<TacticalShot> shots,
            double shotDisplaySeconds,
            IRandomNumberProvider random
        );

        /// <summary>
        /// Applies simultaneous tactical damage.
        /// </summary>
        /// <param name="conventionalDamage">The non-negative hull-damaging attack strength.</param>
        /// <param name="ionDamage">The non-negative shield and subsystem attack strength.</param>
        /// <param name="config">The space-combat parameters.</param>
        /// <param name="random">The combat random-number stream.</param>
        internal abstract void ApplyDamage(
            double conventionalDamage,
            double ionDamage,
            GameConfig.SpaceCombatConfig config,
            IRandomNumberProvider random
        );

        /// <summary>
        /// Advances recharge and temporary damage state.
        /// </summary>
        /// <param name="rounds">The elapsed combat rounds.</param>
        /// <param name="config">The space-combat parameters.</param>
        internal abstract void AdvanceState(double rounds, GameConfig.SpaceCombatConfig config);

        /// <summary>
        /// Destroys the unit.
        /// </summary>
        internal abstract void Destroy();

        /// <summary>
        /// Starts this unit's movement toward the escape edge.
        /// </summary>
        internal void StartWithdrawal()
        {
            if (IsTargetable)
                _isWithdrawing = true;
        }

        /// <summary>
        /// Marks this surviving unit as withdrawn from the battle.
        /// </summary>
        internal void FinishWithdrawal()
        {
            if (!IsAlive)
                return;

            _hasWithdrawn = true;
            _isWithdrawing = false;
        }

        /// <summary>
        /// Returns the distance between this unit and another unit.
        /// </summary>
        /// <param name="other">The other unit.</param>
        /// <returns>The planar distance between unit centers.</returns>
        internal double GetDistanceTo(TacticalBattleUnit other)
        {
            return TacticalBattleGeometry.GetDistance(X, Y, other.X, other.Y);
        }

        /// <summary>
        /// Returns the direction from this unit to another unit.
        /// </summary>
        /// <param name="other">The other unit.</param>
        /// <returns>The direction in radians.</returns>
        internal double GetAngleTo(TacticalBattleUnit other)
        {
            return TacticalBattleGeometry.GetAngle(X, Y, other.X, other.Y);
        }

        /// <summary>
        /// Returns whether a candidate belongs to the class this unit engages first.
        /// </summary>
        /// <param name="candidate">The potential target.</param>
        /// <returns>True when the candidate matches the unit's target priority.</returns>
        internal bool PrefersTarget(TacticalBattleUnit candidate)
        {
            return candidate.IsStarfighter
                == (TargetPriority == TacticalTargetPriority.Starfighters);
        }

        /// <summary>
        /// Returns the distance from this unit to the edge of another unit's hull footprint.
        /// </summary>
        /// <param name="other">The unit being engaged.</param>
        /// <returns>The distance to the other unit's hull footprint, never negative.</returns>
        internal double GetEngagementDistanceTo(TacticalBattleUnit other)
        {
            return Math.Max(0, GetDistanceTo(other) - other.HullFootprint);
        }

        /// <summary>
        /// Returns the bearing of another unit relative to this unit's bow.
        /// </summary>
        /// <param name="other">The other unit.</param>
        /// <returns>The relative bearing in radians.</returns>
        internal double GetBearingTo(TacticalBattleUnit other)
        {
            return TacticalBattleGeometry.NormalizeAngle(GetAngleTo(other) - Heading);
        }

        /// <summary>
        /// Adds one weapon lane's damage to the pending hits and records its shot.
        /// </summary>
        /// <param name="hitSink">Receives the queued hit.</param>
        /// <param name="shots">Receives the shot presentation record.</param>
        /// <param name="target">The tactical target.</param>
        /// <param name="damage">The positive damage to add.</param>
        /// <param name="isIonDamage">Whether the damage comes from an ion weapon.</param>
        /// <param name="shotDisplaySeconds">How long the shot remains visible.</param>
        /// <param name="isTorpedo">Whether the shot is a torpedo rather than an energy bolt.</param>
        private protected void QueueHit(
            TacticalHitSink hitSink,
            ICollection<TacticalShot> shots,
            TacticalBattleUnit target,
            double damage,
            bool isIonDamage,
            double shotDisplaySeconds,
            bool isTorpedo = false
        )
        {
            hitSink(target, Math.Max(damage, 1), isIonDamage);
            shots.Add(
                new TacticalShot(
                    Side,
                    X,
                    Y,
                    target.X,
                    target.Y,
                    isIonDamage,
                    IsStarfighter,
                    isTorpedo,
                    shotDisplaySeconds
                )
            );
        }

        /// <summary>
        /// Records a shot that misses: the bolt flies past the target and does no damage.
        /// </summary>
        /// <param name="shots">Receives the shot presentation record.</param>
        /// <param name="target">The unit the shot was aimed at.</param>
        /// <param name="isIonDamage">Whether the shot came from an ion weapon.</param>
        /// <param name="shotDisplaySeconds">How long the shot remains visible.</param>
        /// <param name="random">The combat random-number stream that scatters the shot.</param>
        private protected void QueueMiss(
            ICollection<TacticalShot> shots,
            TacticalBattleUnit target,
            bool isIonDamage,
            double shotDisplaySeconds,
            IRandomNumberProvider random
        )
        {
            double angle = GetAngleTo(target);
            double overshoot = target.Radius + _missOvershoot * (0.5 + random.NextDouble());
            double drift = (random.NextDouble() * 2 - 1) * (target.Radius + _missSpread);
            shots.Add(
                new TacticalShot(
                    Side,
                    X,
                    Y,
                    target.X + Math.Cos(angle) * overshoot - Math.Sin(angle) * drift,
                    target.Y + Math.Sin(angle) * overshoot + Math.Cos(angle) * drift,
                    isIonDamage,
                    IsStarfighter,
                    false,
                    shotDisplaySeconds
                )
            );
        }

        /// <summary>
        /// Calculates the maneuver-rate adjustment used against fighter targets.
        /// </summary>
        /// <param name="target">The target unit.</param>
        /// <returns>The attack multiplier.</returns>
        protected double GetManeuverMultiplier(TacticalBattleUnit target)
        {
            if (!target.IsStarfighter)
                return 1;

            double targetRate = Math.Max(target.ManeuverRate, _minimumManeuverRatio);
            return Math.Max(Math.Min(ManeuverRate / targetRate, 1), _minimumManeuverRatio);
        }
    }
}
