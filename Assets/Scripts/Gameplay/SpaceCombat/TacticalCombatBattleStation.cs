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
        public bool IsConstructionComplete { get; }
        public float MaximumHull { get; }
        public bool HasDeparted { get; private set; }
        public float Charge { get; private set; } = 100;
        public bool IsOperational =>
            IsConstructionComplete
            && Unit.Systems.Hull != 0
            && !float.IsNaN(Unit.Systems.Hull)
            && !HasDeparted;
        public bool CanBeginTargetSelection =>
            MaximumHull > 0 && (Charge == 100 || float.IsNaN(Charge));
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
            IsConstructionComplete = unit.Status != TacticalCombatUnitStatus.UnderConstruction;
            MaximumHull = ((Rebellion.Game.Units.CapitalShip)unit.Source).MaxHullStrength;
        }

        /// <summary>
        /// Advances charging after construction completes while hull remains positive.
        /// </summary>
        /// <param name="elapsedMilliseconds">The elapsed system interval.</param>
        public void Advance(float elapsedMilliseconds)
        {
            if (!IsConstructionComplete || !(Unit.Systems.Hull > 0))
                return;
            float increment = (float)((double)elapsedMilliseconds * 0.33333334f * 0.001f);
            if (Charge < 100 && (double)Charge + increment >= 100)
                ChargeCompleted?.Invoke();
            double advanced = (double)Charge + increment;
            Charge = (float)advanced;
            if (advanced >= 100)
                Charge = 100;
        }

        /// <summary>
        /// Checks whether an existing target-selection context still has command authority and a valid station.
        /// </summary>
        /// <param name="withdrawing">Whether the station's faction has begun withdrawing.</param>
        /// <param name="directControl">Whether the player has direct command authority.</param>
        /// <returns>Whether the target-selection context remains valid after queued input.</returns>
        public bool CanKeepTargetSelection(bool withdrawing, bool directControl) =>
            MaximumHull > 0 && Charge >= 100 && IsOperational && !withdrawing && directControl;

        /// <summary>
        /// Deactivates an available station while retaining its current hull and charge.
        /// </summary>
        /// <returns>Whether the departure flag was set.</returns>
        public bool TryWithdraw()
        {
            if (!IsOperational)
                return false;
            HasDeparted = true;
            return true;
        }

        /// <summary>
        /// Clears the station's charge and hull while retaining any completed departure.
        /// </summary>
        internal void Destroy()
        {
            Charge = 0;
            Unit.Systems.Destroy();
        }

        /// <summary>
        /// Resets the superlaser charge when an admitted target command is dispatched.
        /// Target-context and automatic-control admission remain with their respective owners.
        /// </summary>
        public void Fire() => Charge = 0;
    }
}
