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
        private const float _chargePerTacticalSecond = 1f;
        private readonly HashSet<TacticalUnitState> participants;
        private readonly Dictionary<TacticalUnitState, float> chargeByDeathStar;

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
        }

        /// <summary>
        /// Fires a charged Death Star at one active opposing tactical object.
        /// </summary>
        /// <param name="deathStar">The firing Death Star.</param>
        /// <param name="target">The opposing tactical object selected as its target.</param>
        /// <returns>True when the shot fires and destroys the target.</returns>
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
            target.Hull = 0;
            return true;
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
