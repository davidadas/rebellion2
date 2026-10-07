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

        /// <summary>Records the units that leave or are lost when a side orders withdrawal.</summary>
        /// <param name="result">The combat result receiving withdrawal outcomes.</param>
        /// <param name="retreatingFleets">Every participating fleet on the withdrawing side.</param>
        /// <param name="ownerInstanceId">The withdrawing faction identifier.</param>
        /// <param name="attackerRetreated">Whether the withdrawing side initiated the encounter.</param>
        /// <param name="origin">The planet where withdrawal began.</param>
        internal void ResolveManualWithdrawal(
            SpaceCombatResult result,
            IReadOnlyList<Fleet> retreatingFleets,
            string ownerInstanceId,
            bool attackerRetreated,
            Planet origin
        )
        {
            if (result == null)
                throw new ArgumentNullException(nameof(result));

            List<CapitalShip> ships = GetParticipatingShips(retreatingFleets);
            List<Starfighter> fighters = GetParticipatingFighters(ships, origin, ownerInstanceId);
            HashSet<CapitalShip> independentlyWithdrawingShips = ships
                .Where(ship =>
                    ship.Hyperdrive > 0
                    && _spaceCombatQueries.CanRetreatFleet(ship.GetParentOfType<Fleet>())
                )
                .ToHashSet();
            HashSet<Starfighter> independentlyWithdrawingFighters = fighters
                .Where(_spaceCombatQueries.CanRetreatFighter)
                .ToHashSet();
            HashSet<Starfighter> carrierDependentFighters = fighters
                .Where(fighter => !independentlyWithdrawingFighters.Contains(fighter))
                .ToHashSet();
            Dictionary<Starfighter, CapitalShip> recoveryAssignments = GetRecoveryAssignments(
                independentlyWithdrawingShips,
                fighters,
                carrierDependentFighters
            );
            HashSet<ISceneNode> withdrawnUnits = independentlyWithdrawingShips
                .Cast<ISceneNode>()
                .Concat(independentlyWithdrawingFighters)
                .Concat(recoveryAssignments.Keys)
                .ToHashSet();
            List<CapitalShip> lostShips = ships
                .Where(ship => !withdrawnUnits.Contains(ship))
                .ToList();
            List<Starfighter> lostFighters = fighters
                .Where(fighter => !withdrawnUnits.Contains(fighter))
                .ToList();

            result.WithdrawnUnits.AddRange(withdrawnUnits);
            result.ShipDamage.AddRange(
                lostShips.Select(ship => new ShipDamageResult
                {
                    Ship = ship,
                    HullBefore = ship.CurrentHullStrength,
                    HullAfter = 0,
                })
            );
            result.FighterLosses.AddRange(
                lostFighters.Select(fighter => new FighterLossResult
                {
                    Fighter = fighter,
                    SquadsBefore = fighter.CurrentSquadronSize,
                    SquadsAfter = 0,
                })
            );
            CombatUnitSnapshot.RecordOutcomes(
                attackerRetreated ? result.AttackingUnits : result.DefendingUnits,
                Enumerable.Empty<ISceneNode>(),
                lostShips.Cast<ISceneNode>().Concat(lostFighters)
            );
        }

        /// <summary>Places withdrawn carrier-dependent fighters aboard participating carriers.</summary>
        /// <param name="fleets">The fleets that participated on the withdrawing side.</param>
        /// <param name="planet">The combat planet.</param>
        /// <param name="ownerInstanceId">The withdrawing faction identifier.</param>
        /// <param name="withdrawnUnits">The units resolved as withdrawn.</param>
        internal void ApplyFighterRecovery(
            IReadOnlyList<Fleet> fleets,
            Planet planet,
            string ownerInstanceId,
            ISet<ISceneNode> withdrawnUnits
        )
        {
            if (withdrawnUnits == null || withdrawnUnits.Count == 0)
                return;

            List<CapitalShip> ships = GetParticipatingShips(fleets);
            List<Starfighter> fighters = GetParticipatingFighters(ships, planet, ownerInstanceId);
            HashSet<CapitalShip> withdrawingCarriers = ships
                .Where(ship => withdrawnUnits.Contains(ship))
                .ToHashSet();
            HashSet<Starfighter> carrierDependentFighters = fighters
                .Where(fighter => withdrawnUnits.Contains(fighter) && fighter.Hyperdrive <= 0)
                .ToHashSet();
            Dictionary<Starfighter, CapitalShip> recoveryAssignments = GetRecoveryAssignments(
                withdrawingCarriers,
                fighters,
                carrierDependentFighters
            );

            foreach (
                KeyValuePair<Starfighter, CapitalShip> assignment in recoveryAssignments.Where(
                    assignment =>
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

        /// <summary>Returns active capital ships in stable participating-fleet order.</summary>
        /// <param name="fleets">The participating fleets.</param>
        /// <returns>The active capital ships.</returns>
        private static List<CapitalShip> GetParticipatingShips(IReadOnlyList<Fleet> fleets)
        {
            return (fleets ?? Array.Empty<Fleet>())
                .Where(fleet => fleet != null)
                .SelectMany(SpaceCombatQueries.GetActiveCapitalShips)
                .Distinct()
                .ToList();
        }

        /// <summary>Returns active carried and planetary fighters in stable combat order.</summary>
        /// <param name="ships">The participating capital ships.</param>
        /// <param name="planet">The combat planet.</param>
        /// <param name="ownerInstanceId">The participating faction identifier.</param>
        /// <returns>The active fighter squadrons.</returns>
        private static List<Starfighter> GetParticipatingFighters(
            IReadOnlyList<CapitalShip> ships,
            Planet planet,
            string ownerInstanceId
        )
        {
            return (ships ?? Array.Empty<CapitalShip>())
                .SelectMany(ship => ship.GetChildren<Starfighter>())
                .Concat(SpaceCombatQueries.GetActivePlanetStarfighters(planet, ownerInstanceId))
                .Where(SpaceCombatQueries.IsActiveStarfighter)
                .Distinct()
                .ToList();
        }

        /// <summary>Assigns surviving carrier-dependent fighters to withdrawing carriers.</summary>
        /// <param name="carriers">The capital ships that successfully withdraw.</param>
        /// <param name="participatingFighters">Every active fighter that entered combat.</param>
        /// <param name="carrierDependentFighters">Fighters eligible to leave aboard a carrier.</param>
        /// <returns>Carrier assignments for every recoverable dependent fighter.</returns>
        internal static Dictionary<Starfighter, CapitalShip> GetRecoveryAssignments(
            IEnumerable<CapitalShip> carriers,
            IReadOnlyList<Starfighter> participatingFighters,
            ISet<Starfighter> carrierDependentFighters
        )
        {
            List<CapitalShip> availableCarriers = (carriers ?? Enumerable.Empty<CapitalShip>())
                .Where(carrier => carrier?.StarfighterCapacity > 0)
                .Distinct()
                .ToList();
            Dictionary<CapitalShip, int> remainingCapacity = availableCarriers.ToDictionary(
                carrier => carrier,
                carrier =>
                    GetAvailableRecoveryCapacity(
                        carrier,
                        participatingFighters,
                        carrierDependentFighters
                    )
            );
            Dictionary<Starfighter, CapitalShip> assignments =
                new Dictionary<Starfighter, CapitalShip>();

            foreach (CapitalShip carrier in availableCarriers)
            {
                foreach (
                    Starfighter fighter in carrier
                        .GetChildren<Starfighter>()
                        .Where(fighter => carrierDependentFighters?.Contains(fighter) == true)
                )
                {
                    assignments[fighter] = carrier;
                }
            }

            foreach (
                Starfighter fighter in (participatingFighters ?? Array.Empty<Starfighter>()).Where(
                    fighter =>
                        fighter != null
                        && carrierDependentFighters?.Contains(fighter) == true
                        && !assignments.ContainsKey(fighter)
                )
            )
            {
                CapitalShip carrier = availableCarriers.FirstOrDefault(candidate =>
                    remainingCapacity[candidate] > 0
                );
                if (carrier == null)
                    continue;

                remainingCapacity[carrier]--;
                assignments[fighter] = carrier;
            }

            return assignments;
        }

        /// <summary>Calculates bays available after non-travelling combat participants are removed.</summary>
        /// <param name="carrier">The carrier receiving displaced fighters.</param>
        /// <param name="participatingFighters">Every active fighter that entered combat.</param>
        /// <param name="carrierDependentFighters">Fighters resolved to remain aboard a carrier.</param>
        /// <returns>The number of bays available for displaced fighters.</returns>
        private static int GetAvailableRecoveryCapacity(
            CapitalShip carrier,
            IReadOnlyList<Starfighter> participatingFighters,
            ISet<Starfighter> carrierDependentFighters
        )
        {
            int releasedCapacity = (participatingFighters ?? Array.Empty<Starfighter>()).Count(
                fighter =>
                    ReferenceEquals(fighter?.GetParentOfType<CapitalShip>(), carrier)
                    && !(carrierDependentFighters?.Contains(fighter) ?? false)
            );
            return Math.Max(carrier.GetExcessStarfighterCapacity() + releasedCapacity, 0);
        }
    }
}
