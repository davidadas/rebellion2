using System;
using System.Collections.Generic;
using System.Linq;
using Rebellion.Game.Units;

namespace Rebellion.Game.Tactical
{
    /// <summary>
    /// Owns Death Star superlaser charge and resolves its dedicated tactical attack.
    /// </summary>
    internal sealed class TacticalSuperlaserSystem
    {
        internal const float MaximumCharge = 100f;
        internal const float ResolutionDelay = 1f;
        private const float _chargePerTacticalSecond = 1f / 3f;
        private readonly HashSet<TacticalUnitState> participants;
        private readonly Dictionary<TacticalUnitState, float> chargeByDeathStar;
        private readonly List<PendingShot> pendingShots = new List<PendingShot>();
        private readonly List<TacticalUnitState> resolvedTargets = new List<TacticalUnitState>();

        private sealed class PendingShot
        {
            /// <summary>
            /// Gets the opposing tactical object selected for destruction.
            /// </summary>
            public TacticalUnitState Target { get; }

            /// <summary>
            /// Gets or sets the tactical time remaining before the shot resolves.
            /// </summary>
            public float RemainingTime { get; set; } = ResolutionDelay;

            /// <summary>
            /// Initializes one delayed superlaser result.
            /// </summary>
            /// <param name="target">The opposing tactical object selected for destruction.</param>
            public PendingShot(TacticalUnitState target)
            {
                Target = target;
            }
        }

        /// <summary>
        /// Initializes every participating Death Star with a fully charged superlaser.
        /// </summary>
        /// <param name="units">The tactical units participating in the battle.</param>
        public TacticalSuperlaserSystem(IReadOnlyList<TacticalUnitState> units)
        {
            if (units == null)
                throw new ArgumentNullException(nameof(units));

            participants = new HashSet<TacticalUnitState>(units);
            chargeByDeathStar = units
                .Where(IsDeathStar)
                .ToDictionary(unit => unit, _ => MaximumCharge);
        }

        /// <summary>
        /// Gets one Death Star's current charge percentage.
        /// </summary>
        /// <param name="deathStar">The Death Star whose charge is requested.</param>
        /// <returns>The current charge from zero through one hundred.</returns>
        public float GetCharge(TacticalUnitState deathStar)
        {
            ValidateDeathStar(deathStar);
            return chargeByDeathStar[deathStar];
        }

        /// <summary>
        /// Advances the dedicated charge meter for every operational Death Star.
        /// </summary>
        /// <param name="elapsedTime">The elapsed tactical time.</param>
        public void Advance(float elapsedTime)
        {
            if (elapsedTime < 0f)
                throw new ArgumentOutOfRangeException(nameof(elapsedTime));

            foreach (TacticalUnitState deathStar in chargeByDeathStar.Keys.ToArray())
            {
                if (!CanOperate(deathStar))
                    continue;

                chargeByDeathStar[deathStar] = Math.Min(
                    MaximumCharge,
                    chargeByDeathStar[deathStar] + elapsedTime * _chargePerTacticalSecond
                );
            }

            foreach (PendingShot shot in pendingShots.ToArray())
            {
                shot.RemainingTime -= elapsedTime;
                if (shot.RemainingTime > 0f)
                    continue;

                pendingShots.Remove(shot);
                if (shot.Target.IsActive)
                    resolvedTargets.Add(shot.Target);
            }
        }

        /// <summary>
        /// Fires a charged Death Star at one active opposing tactical object.
        /// </summary>
        /// <param name="deathStar">The firing Death Star.</param>
        /// <param name="target">The opposing tactical object selected as its target.</param>
        /// <returns>True when the shot fires and schedules the target's destruction.</returns>
        public bool TryFire(TacticalUnitState deathStar, TacticalUnitState target)
        {
            ValidateDeathStar(deathStar);
            if (
                !CanOperate(deathStar)
                || target is not { IsActive: true }
                || !participants.Contains(target)
                || target.Side == deathStar.Side
                || chargeByDeathStar[deathStar] < MaximumCharge
            )
            {
                return false;
            }

            chargeByDeathStar[deathStar] = 0f;
            pendingShots.Add(new PendingShot(target));
            return true;
        }

        /// <summary>
        /// Removes and returns targets reached by delayed superlaser shots.
        /// </summary>
        /// <returns>The active targets whose superlaser delay elapsed.</returns>
        public IReadOnlyList<TacticalUnitState> DrainResolvedTargets()
        {
            TacticalUnitState[] result = resolvedTargets.ToArray();
            resolvedTargets.Clear();
            return result;
        }

        /// <summary>
        /// Tests whether a tactical unit represents a Death Star.
        /// </summary>
        /// <param name="unit">The tactical unit to inspect.</param>
        /// <returns>True when the unit is a Death Star.</returns>
        private static bool IsDeathStar(TacticalUnitState unit)
        {
            return unit?.Unit is CapitalShip { IsDeathStar: true };
        }

        /// <summary>
        /// Tests whether a Death Star remains active and has not begun withdrawing.
        /// </summary>
        /// <param name="deathStar">The Death Star to inspect.</param>
        /// <returns>True when its superlaser can charge or fire.</returns>
        private static bool CanOperate(TacticalUnitState deathStar)
        {
            return deathStar.IsActive && !deathStar.IsWithdrawing;
        }

        /// <summary>
        /// Rejects objects that do not belong to this system's Death Star set.
        /// </summary>
        /// <param name="deathStar">The proposed firing object.</param>
        private void ValidateDeathStar(TacticalUnitState deathStar)
        {
            if (deathStar == null)
                throw new ArgumentNullException(nameof(deathStar));
            if (!chargeByDeathStar.ContainsKey(deathStar))
                throw new ArgumentException(
                    "The tactical unit is not a participating Death Star.",
                    nameof(deathStar)
                );
        }
    }
}
