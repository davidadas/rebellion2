using System;
using System.Collections.Generic;
using System.Linq;
using Rebellion.Game;
using Rebellion.Game.Results;
using Rebellion.Game.Units;

namespace Rebellion.Gameplay
{
    /// <summary>
    /// Represents one side's mutable state during a tactical space battle.
    /// </summary>
    internal sealed class TacticalBattleForce
    {
        private const double _orbitRadiusSpread = 0.5;
        private const double _goldenRatioConjugate = 0.6180339887;

        private readonly List<TacticalBattleUnit> _targetableUnits = new List<TacticalBattleUnit>();

        internal CombatSide Side { get; }

        internal List<TacticalCapitalShipUnit> Ships { get; }

        internal List<TacticalStarfighterUnit> Fighters { get; }

        internal List<TacticalBattleUnit> Units { get; }

        internal bool CanWithdraw { get; }

        internal bool IsAutomatic { get; set; }

        internal SpaceCombatSideOutcome Outcome { get; set; }

        internal bool WithdrawalOrdered { get; private set; }

        internal double InitialDurability { get; }

        internal bool HasCombatants => Units.Any(unit => unit.IsTargetable);

        internal bool HasUnresolvedUnits => Units.Any(unit => unit.IsAlive && !unit.HasWithdrawn);

        internal bool HasPendingWithdrawal => Units.Any(unit => unit.IsAlive && unit.IsWithdrawing);

        internal bool HasTargetableShips => Ships.Any(ship => ship.IsTargetable);

        internal bool HasTargetableFighters => Fighters.Any(fighter => fighter.IsTargetable);

        internal bool HasWithdrawnUnits => Units.Any(unit => unit.HasWithdrawn);

        internal bool HasWithdrawingIndependentUnit =>
            Units.Any(unit => unit.IsAlive && unit.IsWithdrawing && unit.CanWithdrawIndependently);

        /// <summary>
        /// Creates tactical state for one combat force.
        /// </summary>
        /// <param name="side">The side this force fights for.</param>
        /// <param name="ships">The force's capital ships.</param>
        /// <param name="fighters">The force's fighter squadrons.</param>
        /// <param name="canWithdraw">Whether the force can withdraw.</param>
        /// <param name="isAutomatic">Whether the simulation controls the force.</param>
        /// <param name="config">The space-combat parameters.</param>
        /// <param name="command">The force's tactical command bonuses.</param>
        internal TacticalBattleForce(
            CombatSide side,
            IReadOnlyList<CapitalShip> ships,
            IReadOnlyList<Starfighter> fighters,
            bool canWithdraw,
            bool isAutomatic,
            GameConfig.SpaceCombatConfig config,
            SpaceCombatCommandModifiers command
        )
        {
            Side = side;
            double capitalShipCommandBonus = GetCommandBonus(
                command.AdmiralLeadership,
                config.AdmiralLeadershipDivisor
            );
            double starfighterCommandBonus = GetCommandBonus(
                command.CommanderCombat,
                config.CommanderCombatDivisor
            );
            Ships = (ships ?? Array.Empty<CapitalShip>())
                .Where(ship => ship != null)
                .Select(ship => new TacticalCapitalShipUnit(
                    ship,
                    side,
                    config,
                    capitalShipCommandBonus
                ))
                .ToList();
            Fighters = (fighters ?? Array.Empty<Starfighter>())
                .Where(fighter => fighter != null)
                .Select(fighter => new TacticalStarfighterUnit(
                    fighter,
                    side,
                    config,
                    starfighterCommandBonus
                ))
                .ToList();
            for (int index = 0; index < Fighters.Count; index++)
            {
                Fighters[index].OrbitDirection = index % 2 == 0 ? 1 : -1;
                // Spread the squadrons over different rings so they do not fly stacked; the
                // golden-ratio stride keeps any number of them apart.
                double stride = index * _goldenRatioConjugate;
                Fighters[index].OrbitRadiusScale =
                    1 + _orbitRadiusSpread * (stride - Math.Floor(stride));
            }
            Units = new List<TacticalBattleUnit>(Ships.Count + Fighters.Count);
            Units.AddRange(Ships);
            Units.AddRange(Fighters);
            CanWithdraw = canWithdraw;
            IsAutomatic = isAutomatic;
            Outcome = SpaceCombatSideOutcome.Active;
            InitialDurability = GetDurability();
        }

        /// <summary>
        /// Returns the force's currently targetable units in stable combat order.
        /// </summary>
        /// <returns>The reusable target collection.</returns>
        internal IReadOnlyList<TacticalBattleUnit> GetTargetableUnits()
        {
            _targetableUnits.Clear();
            foreach (TacticalBattleUnit unit in Units)
            {
                if (unit.IsTargetable)
                    _targetableUnits.Add(unit);
            }

            return _targetableUnits;
        }

        /// <summary>
        /// Calculates the remaining hull, shields, and fighter durability.
        /// </summary>
        /// <returns>The force's remaining tactical durability.</returns>
        internal double GetDurability()
        {
            double durability = 0;
            foreach (TacticalBattleUnit unit in Units)
            {
                if (unit.IsTargetable)
                    durability += unit.RemainingDurability;
            }

            return durability;
        }

        /// <summary>
        /// Calculates the remaining strength used by the withdrawal threshold check.
        /// </summary>
        /// <returns>The force's remaining tactical strength against capital ships.</returns>
        internal double GetRetreatStrength()
        {
            double strength = 0;
            foreach (TacticalBattleUnit unit in Units)
            {
                if (unit.IsTargetable)
                    strength += unit.GetEffectiveness(targetsFighters: false);
            }

            return strength;
        }

        /// <summary>
        /// Calculates the remaining strength against the target types an opponent offers.
        /// </summary>
        /// <param name="opposingForce">The force providing the available target types.</param>
        /// <returns>The force's remaining tactical strength.</returns>
        internal double GetTacticalStrength(TacticalBattleForce opposingForce)
        {
            bool canTargetCapitalShips = opposingForce.HasTargetableShips;
            bool canTargetFighters = opposingForce.HasTargetableFighters;
            double strength = 0;
            foreach (TacticalBattleUnit unit in Units)
            {
                if (!unit.IsTargetable)
                    continue;

                strength += Math.Max(
                    canTargetCapitalShips ? unit.GetEffectiveness(targetsFighters: false) : 0,
                    canTargetFighters ? unit.GetEffectiveness(targetsFighters: true) : 0
                );
            }

            return strength;
        }

        /// <summary>
        /// Calculates the escape progress of units still leaving the battle under their own power.
        /// </summary>
        /// <returns>The combined distance from the battle center of independently withdrawing units.</returns>
        internal double GetWithdrawalProgress()
        {
            double progress = 0;
            foreach (TacticalBattleUnit unit in Units)
            {
                if (unit.IsAlive && unit.IsWithdrawing && unit.CanWithdrawIndependently)
                    progress += Math.Abs(unit.X);
            }

            return progress;
        }

        /// <summary>
        /// Starts withdrawal for every surviving unit in the force.
        /// </summary>
        internal void BeginWithdrawal()
        {
            if (!CanWithdraw || WithdrawalOrdered)
                return;

            WithdrawalOrdered = true;
            foreach (TacticalCapitalShipUnit ship in Ships)
                ship.ClearOrder();
            foreach (TacticalBattleUnit unit in Units.Where(unit => unit.IsTargetable))
                unit.StartWithdrawal();
        }

        /// <summary>
        /// Immediately resolves withdrawal for every surviving unit.
        /// </summary>
        internal void CompleteWithdrawal()
        {
            foreach (
                TacticalBattleUnit unit in Units.Where(unit =>
                    unit.IsAlive && !unit.HasWithdrawn && unit.CanWithdrawIndependently
                )
            )
            {
                unit.FinishWithdrawal();
            }

            CompleteDependentWithdrawal();
        }

        /// <summary>
        /// Resolves stranded units after every mobile survivor has escaped.
        /// </summary>
        internal void CompleteDependentWithdrawal()
        {
            HashSet<Starfighter> carrierDependentFighters = Fighters
                .Where(fighter =>
                    fighter.IsAlive && !fighter.HasWithdrawn && !fighter.CanWithdrawIndependently
                )
                .Select(fighter => fighter.Fighter)
                .ToHashSet();
            Dictionary<Starfighter, CapitalShip> recoveryAssignments =
                SpaceCombatWithdrawalPlanner.GetRecoveryAssignments(
                    Ships
                        .Where(ship =>
                            ship.IsAlive && ship.HasWithdrawn && ship.CanWithdrawIndependently
                        )
                        .Select(ship => ship.Ship),
                    Fighters.ConvertAll(fighter => fighter.Fighter),
                    carrierDependentFighters
                );

            foreach (
                TacticalBattleUnit unit in Units.Where(unit => unit.IsAlive && !unit.HasWithdrawn)
            )
            {
                if (
                    unit is TacticalStarfighterUnit fighter
                    && recoveryAssignments.ContainsKey(fighter.Fighter)
                )
                    unit.FinishWithdrawal();
                else
                    unit.Destroy();
            }
        }

        /// <summary>
        /// Returns every capital ship to automatic engagement.
        /// </summary>
        internal void ClearOrders()
        {
            foreach (TacticalCapitalShipUnit ship in Ships)
                ship.ClearOrder();
        }

        /// <summary>
        /// Scales an officer rating with its configured tactical divisor.
        /// </summary>
        /// <param name="rating">The selected officer's effective rating.</param>
        /// <param name="divisor">The configured tactical divisor.</param>
        /// <returns>The tactical command bonus, or zero for an invalid divisor.</returns>
        private static double GetCommandBonus(int rating, int divisor)
        {
            return divisor > 0 ? (double)Math.Max(rating, 0) / divisor : 0;
        }
    }
}
