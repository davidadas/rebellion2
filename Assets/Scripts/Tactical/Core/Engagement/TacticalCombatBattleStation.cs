using System;
using Rebellion.Game.Combat;

namespace Rebellion.Tactical
{
    /// <summary>
    /// Owns a battle station's superlaser charge and planetary protection state.
    /// </summary>
    public sealed class TacticalCombatBattleStation
    {
        public CombatUnit Unit { get; }
        public bool HasPlanetaryProtection { get; }
        public bool IsConstructionComplete { get; }
        public float MaximumHull { get; }
        public bool HasDeparted { get; private set; }
        public float Charge { get; private set; } = 100;
        public bool IsOperational =>
            IsConstructionComplete && Unit.Hull != 0 && !float.IsNaN(Unit.Hull) && !HasDeparted;
        public bool CanBeginTargetSelection =>
            MaximumHull > 0 && (Charge == 100 || float.IsNaN(Charge));
        public bool CanReceiveAttackRun => IsOperational && !HasPlanetaryProtection;
        public event Action ChargeCompleted;

        /// <summary>
        /// Captures station and fighter availability for assessing a new attack run.
        /// Fighter counts describe the active tactical root, independently of selection and withdrawal.
        /// </summary>
        internal readonly struct AttackReadiness
        {
            public bool StationExists { get; }
            public bool StationOperational { get; }
            public bool StationProtected { get; }
            public bool AttackRunActive { get; }
            public bool AllianceFighterChainPopulated { get; }
            public bool EmpireFighterChainPopulated { get; }
            public bool AllianceAttackRunOrdered { get; }
            public int AllianceActiveFighters { get; }
            public int EmpireActiveFighters { get; }
            public int AllianceActiveFightersOutsideAttackRuns { get; }

            public bool CanAttackBattleStation =>
                CanStartAttackRun(
                    StationExists && StationOperational && !StationProtected,
                    AttackRunActive || AllianceAttackRunOrdered,
                    AllianceActiveFighters,
                    EmpireActiveFighters
                );

            /// <summary>
            /// Captures the explicit station, root membership, and group-order facts for one frame.
            /// </summary>
            /// <param name="stationExists">Whether a battle-station record exists.</param>
            /// <param name="stationOperational">Whether that station is operational.</param>
            /// <param name="stationProtected">Whether that station's protection gate is set.</param>
            /// <param name="attackRunActive">Whether an attack-run helper currently exists.</param>
            /// <param name="allianceFighterChainPopulated">Whether the Alliance fighter chain is nonempty.</param>
            /// <param name="empireFighterChainPopulated">Whether the Empire fighter chain is nonempty.</param>
            /// <param name="allianceAttackRunOrdered">Whether an Alliance group requests or performs an attack run.</param>
            /// <param name="allianceActiveFighters">Every Alliance fighter in the active root.</param>
            /// <param name="empireActiveFighters">Every Empire fighter in the active root.</param>
            /// <param name="allianceActiveFightersOutsideAttackRuns">Alliance active fighters excluding attack-run members.</param>
            public AttackReadiness(
                bool stationExists,
                bool stationOperational,
                bool stationProtected,
                bool attackRunActive,
                bool allianceFighterChainPopulated,
                bool empireFighterChainPopulated,
                bool allianceAttackRunOrdered,
                int allianceActiveFighters,
                int empireActiveFighters,
                int allianceActiveFightersOutsideAttackRuns
            )
            {
                StationExists = stationExists;
                StationOperational = stationOperational;
                StationProtected = stationProtected;
                AttackRunActive = attackRunActive;
                AllianceFighterChainPopulated = allianceFighterChainPopulated;
                EmpireFighterChainPopulated = empireFighterChainPopulated;
                AllianceAttackRunOrdered = allianceAttackRunOrdered;
                AllianceActiveFighters = allianceActiveFighters;
                EmpireActiveFighters = empireActiveFighters;
                AllianceActiveFightersOutsideAttackRuns = allianceActiveFightersOutsideAttackRuns;
            }
        }

        /// <summary>
        /// Evaluates the shared station and squadron gates for a new attack run.
        /// </summary>
        /// <param name="canReceiveAttackRun">Whether the station is operational and unprotected.</param>
        /// <param name="hasAttackRun">Whether an attack run is already requested or active.</param>
        /// <param name="friendlySquadrons">The number of eligible squadrons on the attacking side.</param>
        /// <param name="opposingSquadrons">The number of eligible squadrons on the opposing side.</param>
        /// <returns>Whether the station can be attacked by the available squadrons.</returns>
        internal static bool CanStartAttackRun(
            bool canReceiveAttackRun,
            bool hasAttackRun,
            int friendlySquadrons,
            int opposingSquadrons
        ) => canReceiveAttackRun && !hasAttackRun && friendlySquadrons > opposingSquadrons;

        /// <summary>
        /// Restores the authoritative charge and departure facts without firing or withdrawing locally.
        /// </summary>
        /// <param name="charge">The stored charge.</param>
        /// <param name="departed">Whether departure has completed.</param>
        internal void ApplySnapshot(float charge, bool departed)
        {
            Charge = charge;
            HasDeparted = departed;
        }

        /// <summary>
        /// Captures the station and its protecting planetary facilities at battle entry.
        /// </summary>
        /// <param name="unit">The station's captured tactical unit.</param>
        /// <param name="hasPlanetaryProtection">Whether an operational planetary facility protects it.</param>
        public TacticalCombatBattleStation(CombatUnit unit, bool hasPlanetaryProtection)
        {
            Unit = unit ?? throw new ArgumentNullException(nameof(unit));
            if (!unit.IsBattleStation)
                throw new ArgumentException("A battle station unit is required.", nameof(unit));
            HasPlanetaryProtection = hasPlanetaryProtection;
            IsConstructionComplete = unit.Status != CombatUnitStatus.UnderConstruction;
            MaximumHull = unit.MaximumHull;
        }

        /// <summary>
        /// Advances charging after construction completes while hull remains positive.
        /// </summary>
        /// <param name="elapsedMilliseconds">The elapsed system interval.</param>
        public void Advance(float elapsedMilliseconds)
        {
            if (!IsConstructionComplete || !(Unit.Hull > 0))
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
            Unit.Destroy();
        }

        /// <summary>
        /// Resets the superlaser charge when an admitted target command is dispatched.
        /// Target-context and automatic-control admission remain with their respective owners.
        /// </summary>
        public void Fire() => Charge = 0;
    }
}
