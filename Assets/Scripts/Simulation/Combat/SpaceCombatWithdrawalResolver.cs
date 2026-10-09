using System;
using System.Collections.Generic;
using System.Linq;
using Rebellion.Game;
using Rebellion.Game.Factions;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Results;
using Rebellion.Game.Units;
using Rebellion.SceneGraph;

namespace Rebellion.Simulation
{
    /// <summary>Resolves coordinated withdrawal for every unit on one combat side.</summary>
    internal sealed class SpaceCombatWithdrawalResolver
    {
        private readonly GameRoot _game;
        private readonly SpaceCombatQueries _spaceCombatQueries;

        /// <summary>Creates withdrawal resolution.</summary>
        /// <param name="game">The active game graph.</param>
        /// <param name="spaceCombatQueries">Space-combat withdrawal rules.</param>
        internal SpaceCombatWithdrawalResolver(GameRoot game, SpaceCombatQueries spaceCombatQueries)
        {
            _game = game ?? throw new ArgumentNullException(nameof(game));
            _spaceCombatQueries =
                spaceCombatQueries ?? throw new ArgumentNullException(nameof(spaceCombatQueries));
        }

        /// <summary>Determines whether a combat side can withdraw during automatic combat.</summary>
        /// <param name="fleets">The fleets on the withdrawing side.</param>
        /// <param name="opponents">The opposing fleets.</param>
        /// <param name="planet">The combat planet.</param>
        /// <param name="ownerInstanceId">The withdrawing owner identifier.</param>
        /// <returns>True when the side can withdraw.</returns>
        internal bool CanAutomaticallyWithdraw(
            IReadOnlyList<Fleet> fleets,
            IReadOnlyList<Fleet> opponents,
            Planet planet,
            string ownerInstanceId
        )
        {
            Faction faction =
                planet == null || string.IsNullOrEmpty(ownerInstanceId)
                    ? null
                    : _game.GetFactionByOwnerInstanceID(ownerInstanceId);
            if (
                (
                    faction != null
                    && _game.IsFactionAIControlled(faction)
                    && planet.GetOwnerInstanceID() == faction.InstanceID
                    && planet.GetInstanceID() == faction.HQInstanceID
                )
                || !_spaceCombatQueries.CanRetreatForces(fleets, opponents, planet, ownerInstanceId)
            )
                return false;

            return true;
        }

        /// <summary>Plans and records the units that leave or are lost after a manual order.</summary>
        /// <param name="result">The combat result receiving withdrawal outcomes.</param>
        /// <param name="retreatingFleets">Every participating fleet on the withdrawing side.</param>
        /// <param name="opposingFleets">The fleets capable of blocking withdrawal.</param>
        /// <param name="ownerInstanceId">The withdrawing faction identifier.</param>
        /// <param name="attackerRetreated">Whether the withdrawing side initiated the encounter.</param>
        /// <param name="origin">The planet where withdrawal began.</param>
        /// <returns>The reserved plan, or null when the side cannot withdraw.</returns>
        internal SpaceCombatWithdrawalPlan ResolveManualWithdrawal(
            SpaceCombatResult result,
            IReadOnlyList<Fleet> retreatingFleets,
            IReadOnlyList<Fleet> opposingFleets,
            string ownerInstanceId,
            bool attackerRetreated,
            Planet origin
        )
        {
            if (result == null)
                throw new ArgumentNullException(nameof(result));

            if (SpaceCombatQueries.IsRetreatBlockedByGravityWell(origin, opposingFleets))
                return null;

            SpaceCombatWithdrawalPlan plan = _spaceCombatQueries.GetWithdrawalPlan(
                retreatingFleets,
                origin,
                ownerInstanceId
            );
            if (!plan.CanWithdraw)
                return null;

            RecordWithdrawalOutcomes(
                result,
                plan,
                retreatingFleets,
                ownerInstanceId,
                attackerRetreated,
                origin,
                eligibleUnits: null
            );
            return plan;
        }

        /// <summary>Reserves and records strategic destinations after tactical withdrawal.</summary>
        /// <param name="result">The combat result receiving corrected withdrawal outcomes.</param>
        /// <param name="fleets">Every participating fleet on the withdrawing side.</param>
        /// <param name="ownerInstanceId">The withdrawing faction identifier.</param>
        /// <param name="attackerRetreated">Whether the withdrawing side initiated the encounter.</param>
        /// <param name="origin">The planet where withdrawal began.</param>
        /// <param name="tacticallyWithdrawnUnits">The units that escaped tactical combat.</param>
        /// <returns>The reserved strategic withdrawal plan.</returns>
        internal SpaceCombatWithdrawalPlan ResolveAutomaticWithdrawal(
            SpaceCombatResult result,
            IReadOnlyList<Fleet> fleets,
            string ownerInstanceId,
            bool attackerRetreated,
            Planet origin,
            ISet<ISceneNode> tacticallyWithdrawnUnits
        )
        {
            if (result == null)
                throw new ArgumentNullException(nameof(result));

            SpaceCombatWithdrawalPlan plan = _spaceCombatQueries.GetWithdrawalPlan(
                fleets,
                origin,
                ownerInstanceId,
                tacticallyWithdrawnUnits
            );
            RecordWithdrawalOutcomes(
                result,
                plan,
                fleets,
                ownerInstanceId,
                attackerRetreated,
                origin,
                tacticallyWithdrawnUnits
            );
            return plan;
        }

        /// <summary>Places withdrawn dependent fighters aboard their reserved carriers.</summary>
        /// <param name="plan">The applied withdrawal plan.</param>
        internal void ApplyFighterRecovery(SpaceCombatWithdrawalPlan plan)
        {
            foreach (
                KeyValuePair<Starfighter, CapitalShip> assignment in (
                    plan?.FighterRecoveryAssignments ?? new Dictionary<Starfighter, CapitalShip>()
                ).Where(assignment =>
                    !ReferenceEquals(
                        assignment.Key.GetParentOfType<CapitalShip>(),
                        assignment.Value
                    )
                )
            )
            {
                _game.MoveNode(assignment.Key, assignment.Value);
            }
        }

        /// <summary>Reconciles tactical withdrawal candidates with reserved strategic movement.</summary>
        /// <param name="result">The combat result receiving reconciled outcomes.</param>
        /// <param name="plan">The reserved withdrawal plan.</param>
        /// <param name="fleets">Every participating fleet on the withdrawing side.</param>
        /// <param name="ownerInstanceId">The withdrawing faction identifier.</param>
        /// <param name="attackerRetreated">Whether the withdrawing side initiated the encounter.</param>
        /// <param name="origin">The combat planet.</param>
        /// <param name="eligibleUnits">The tactical candidates, or null for a manual order.</param>
        private static void RecordWithdrawalOutcomes(
            SpaceCombatResult result,
            SpaceCombatWithdrawalPlan plan,
            IReadOnlyList<Fleet> fleets,
            string ownerInstanceId,
            bool attackerRetreated,
            Planet origin,
            ISet<ISceneNode> eligibleUnits
        )
        {
            List<CapitalShip> ships = SpaceCombatWithdrawalPlanner.GetParticipatingShips(fleets);
            List<Starfighter> fighters = SpaceCombatWithdrawalPlanner.GetParticipatingFighters(
                ships,
                origin,
                ownerInstanceId
            );
            List<CapitalShip> evaluatedShips = FilterEvaluated(ships, eligibleUnits);
            List<Starfighter> evaluatedFighters = FilterEvaluated(fighters, eligibleUnits);
            List<CapitalShip> lostShips = evaluatedShips
                .Where(ship => !plan.Contains(ship))
                .ToList();
            List<Starfighter> lostFighters = evaluatedFighters
                .Where(fighter => !plan.Contains(fighter))
                .ToList();
            HashSet<ISceneNode> evaluatedUnits = evaluatedShips
                .Cast<ISceneNode>()
                .Concat(evaluatedFighters)
                .ToHashSet();

            result.WithdrawnUnits.RemoveAll(evaluatedUnits.Contains);
            result.WithdrawnUnits.AddRange(
                plan.WithdrawnUnits.Where(unit => !result.WithdrawnUnits.Contains(unit))
            );
            foreach (CapitalShip ship in lostShips)
                RecordDestroyedShip(result, ship);
            foreach (Starfighter fighter in lostFighters)
                RecordDestroyedFighter(result, fighter);
            CombatUnitSnapshot.RecordOutcomes(
                attackerRetreated ? result.AttackingUnits : result.DefendingUnits,
                Enumerable.Empty<ISceneNode>(),
                lostShips.Cast<ISceneNode>().Concat(lostFighters)
            );

            if (attackerRetreated)
                result.AttackerOutcome = plan.CanWithdraw
                    ? SpaceCombatSideOutcome.Withdrawn
                    : SpaceCombatSideOutcome.Destroyed;
            else
                result.DefenderOutcome = plan.CanWithdraw
                    ? SpaceCombatSideOutcome.Withdrawn
                    : SpaceCombatSideOutcome.Destroyed;
        }

        /// <summary>Filters participants to the tactical units being reconciled.</summary>
        /// <typeparam name="TUnit">The scene-node type being filtered.</typeparam>
        /// <param name="units">The participating units.</param>
        /// <param name="eligibleUnits">The allowed units, or null to include every participant.</param>
        /// <returns>The evaluated participants in stable order.</returns>
        private static List<TUnit> FilterEvaluated<TUnit>(
            IEnumerable<TUnit> units,
            ISet<ISceneNode> eligibleUnits
        )
            where TUnit : class, ISceneNode
        {
            return (units ?? Enumerable.Empty<TUnit>())
                .Where(unit => eligibleUnits?.Contains(unit) != false)
                .ToList();
        }

        /// <summary>Records a participating capital ship as destroyed.</summary>
        /// <param name="result">The result receiving the loss.</param>
        /// <param name="ship">The ship unable to complete withdrawal.</param>
        private static void RecordDestroyedShip(SpaceCombatResult result, CapitalShip ship)
        {
            ShipDamageResult damage = result.ShipDamage.FirstOrDefault(candidate =>
                candidate.Ship == ship
            );
            if (damage == null)
            {
                result.ShipDamage.Add(
                    new ShipDamageResult
                    {
                        Ship = ship,
                        HullBefore = ship.CurrentHullStrength,
                        HullAfter = 0,
                    }
                );
                return;
            }

            damage.HullAfter = 0;
        }

        /// <summary>Records a participating fighter squadron as destroyed.</summary>
        /// <param name="result">The result receiving the loss.</param>
        /// <param name="fighter">The fighter unable to complete withdrawal.</param>
        private static void RecordDestroyedFighter(SpaceCombatResult result, Starfighter fighter)
        {
            FighterLossResult loss = result.FighterLosses.FirstOrDefault(candidate =>
                candidate.Fighter == fighter
            );
            if (loss == null)
            {
                result.FighterLosses.Add(
                    new FighterLossResult
                    {
                        Fighter = fighter,
                        SquadsBefore = fighter.CurrentSquadronSize,
                        SquadsAfter = 0,
                    }
                );
                return;
            }

            loss.SquadsAfter = 0;
        }
    }
}
