using System;

namespace Rebellion.Gameplay
{
    /// <summary>
    /// Owns a battle station's superlaser charge and planetary protection state.
    /// </summary>
    public sealed class TacticalCombatBattleStation
    {
        public TacticalCombatUnit Unit { get; }
        public bool HasPlanetaryProtection { get; }
        public float Charge { get; private set; } = 100;
        public bool IsOperational =>
            Unit.Systems.IsAlive && Unit.Status == TacticalCombatUnitStatus.Active;
        public bool CanReceiveAttackRun => IsOperational && !HasPlanetaryProtection;
        public event Action ChargeCompleted;

        /// <summary>
        /// Captures the station and its protecting planetary facilities at battle entry.
        /// </summary>
        /// <param name="unit">The station's campaign-backed tactical unit.</param>
        /// <param name="hasPlanetaryProtection">Whether an operational planetary facility protects it.</param>
        public TacticalCombatBattleStation(TacticalCombatUnit unit, bool hasPlanetaryProtection)
        {
            Unit = unit ?? throw new ArgumentNullException(nameof(unit));
            if (!unit.IsBattleStation)
                throw new ArgumentException("A battle station unit is required.", nameof(unit));
            HasPlanetaryProtection = hasPlanetaryProtection;
        }

        /// <summary>
        /// Advances superlaser charging while the station remains operational.
        /// </summary>
        /// <param name="elapsedMilliseconds">The elapsed system interval.</param>
        public void Advance(float elapsedMilliseconds)
        {
            if (!IsOperational)
                return;
            float increment = (float)((double)elapsedMilliseconds * 0.33333334f * 0.001f);
            if (Charge < 100 && (double)Charge + increment >= 100)
                ChargeCompleted?.Invoke();
            Charge = Math.Min((float)((double)Charge + increment), 100);
        }

        /// <summary>
        /// Consumes a completed charge when a superlaser target is selected.
        /// </summary>
        /// <returns>Whether a charged operational station fired.</returns>
        public bool TryFire()
        {
            if (!IsOperational || Charge < 100)
                return false;
            Charge = 0;
            return true;
        }
    }
}
