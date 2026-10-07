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
    /// <summary>Resolves losses incurred when a combat force withdraws.</summary>
    internal sealed class SpaceCombatWithdrawalResolver
    {
        private readonly GameRoot _game;
        private readonly MovementQueries _movementQueries;
        private readonly SpaceCombatQueries _spaceCombatQueries;

        /// <summary>Creates withdrawal resolution.</summary>
        /// <param name="game">The active game graph.</param>
        /// <param name="movementQueries">Movement destination rules.</param>
        /// <param name="spaceCombatQueries">Space-combat withdrawal rules.</param>
        internal SpaceCombatWithdrawalResolver(
            GameRoot game,
            MovementQueries movementQueries,
            SpaceCombatQueries spaceCombatQueries
        )
        {
            _game = game ?? throw new ArgumentNullException(nameof(game));
            _movementQueries =
                movementQueries ?? throw new ArgumentNullException(nameof(movementQueries));
            _spaceCombatQueries =
                spaceCombatQueries ?? throw new ArgumentNullException(nameof(spaceCombatQueries));
        }

        /// <summary>Builds coordinated tactical withdrawal groups for one combat side.</summary>
        /// <param name="fleets">The fleets on the withdrawing side.</param>
        /// <param name="opponents">The opposing fleets.</param>
        /// <param name="planet">The combat planet.</param>
        /// <param name="ownerInstanceId">The withdrawing owner identifier.</param>
        /// <returns>The coordinated fleet force and independent fighter groups.</returns>
        internal List<IReadOnlyCollection<ISceneNode>> BuildAutomaticGroups(
            IReadOnlyList<Fleet> fleets,
            IReadOnlyList<Fleet> opponents,
            Planet planet,
            string ownerInstanceId
        )
        {
            List<IReadOnlyCollection<ISceneNode>> groups =
                new List<IReadOnlyCollection<ISceneNode>>();
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
                ) || SpaceCombatQueries.IsRetreatBlockedByGravityWell(planet, opponents)
            )
                return groups;

            List<Fleet> withdrawingFleets = (fleets ?? Array.Empty<Fleet>())
                .Where(_spaceCombatQueries.CanRetreatFleet)
                .ToList();
            if (withdrawingFleets.Count > 0)
            {
                List<CapitalShip> fleetShips = withdrawingFleets
                    .SelectMany(SpaceCombatQueries.GetActiveCapitalShips)
                    .Distinct()
                    .ToList();
                List<ISceneNode> fleetUnits = fleetShips
                    .Cast<ISceneNode>()
                    .Concat(
                        fleetShips
                            .SelectMany(ship => ship.GetChildren<Starfighter>())
                            .Where(SpaceCombatQueries.IsActiveStarfighter)
                    )
                    .Distinct()
                    .ToList();
                if (fleetUnits.Count > 0)
                    groups.Add(fleetUnits);
            }

            foreach (
                Starfighter fighter in SpaceCombatQueries
                    .GetActivePlanetStarfighters(planet, ownerInstanceId)
                    .Where(_spaceCombatQueries.CanRetreatFighter)
            )
            {
                groups.Add(new ISceneNode[] { fighter });
            }

            return groups;
        }

        /// <summary>Records units unable to accompany the withdrawing force.</summary>
        /// <param name="result">The combat result receiving withdrawal losses.</param>
        /// <param name="retreatingFleets">Every fleet on the withdrawing side.</param>
        /// <param name="retreatableFleets">The fleets that began evacuation.</param>
        /// <param name="retreatingFighters">Directly deployed fighters on the withdrawing side.</param>
        /// <param name="retreatableFighters">Directly deployed fighters that began evacuation.</param>
        /// <param name="attackerRetreated">Whether the withdrawing side initiated the encounter.</param>
        /// <param name="origin">The planet where withdrawal began.</param>
        internal void Resolve(
            SpaceCombatResult result,
            IReadOnlyList<Fleet> retreatingFleets,
            IReadOnlyList<Fleet> retreatableFleets,
            IReadOnlyList<Starfighter> retreatingFighters,
            IReadOnlyList<Starfighter> retreatableFighters,
            bool attackerRetreated,
            Planet origin
        )
        {
            if (result == null)
                throw new ArgumentNullException(nameof(result));

            HashSet<Fleet> retreatableFleetSet = (retreatableFleets ?? Array.Empty<Fleet>())
                .Where(fleet => fleet != null)
                .ToHashSet();
            List<CapitalShip> strandedShips = (retreatingFleets ?? Array.Empty<Fleet>())
                .Where(fleet => fleet != null)
                .SelectMany(fleet =>
                    SpaceCombatQueries
                        .GetActiveCapitalShips(fleet)
                        .Where(ship => !retreatableFleetSet.Contains(fleet) || ship.Hyperdrive <= 0)
                )
                .Distinct()
                .ToList();
            HashSet<Starfighter> retreatableFighterSet = (
                retreatableFighters ?? Array.Empty<Starfighter>()
            )
                .Where(fighter => fighter != null)
                .ToHashSet();
            List<Starfighter> strandedFighters = (retreatingFighters ?? Array.Empty<Starfighter>())
                .Where(fighter => fighter != null && !retreatableFighterSet.Contains(fighter))
                .Distinct()
                .ToList();
            strandedFighters.AddRange(
                GetUnrecoverableFighters(
                    retreatingFleets,
                    retreatableFleetSet,
                    strandedShips,
                    origin
                )
            );
            strandedFighters = strandedFighters.Distinct().ToList();

            result.ShipDamage.AddRange(
                strandedShips.Select(ship => new ShipDamageResult
                {
                    Ship = ship,
                    HullBefore = ship.CurrentHullStrength,
                    HullAfter = 0,
                })
            );
            result.FighterLosses.AddRange(
                strandedFighters.Select(fighter => new FighterLossResult
                {
                    Fighter = fighter,
                    SquadsBefore = fighter.CurrentSquadronSize,
                    SquadsAfter = 0,
                })
            );
            CombatUnitSnapshot.RecordOutcomes(
                attackerRetreated ? result.AttackingUnits : result.DefendingUnits,
                Enumerable.Empty<ISceneNode>(),
                strandedShips.Cast<ISceneNode>().Concat(strandedFighters)
            );
        }

        /// <summary>Finds carried fighters that cannot escape or board a withdrawing carrier.</summary>
        /// <param name="retreatingFleets">Every fleet on the withdrawing side.</param>
        /// <param name="retreatableFleets">The fleets that began evacuation.</param>
        /// <param name="strandedShips">The capital ships unable to withdraw.</param>
        /// <param name="origin">The planet where withdrawal began.</param>
        /// <returns>The carried fighters lost during withdrawal.</returns>
        private List<Starfighter> GetUnrecoverableFighters(
            IReadOnlyList<Fleet> retreatingFleets,
            ISet<Fleet> retreatableFleets,
            IReadOnlyCollection<CapitalShip> strandedShips,
            Planet origin
        )
        {
            HashSet<CapitalShip> strandedShipSet = (strandedShips ?? Array.Empty<CapitalShip>())
                .Where(ship => ship != null)
                .ToHashSet();
            List<Starfighter> displacedFighters = (retreatingFleets ?? Array.Empty<Fleet>())
                .Where(fleet => fleet != null)
                .SelectMany(SpaceCombatQueries.GetActiveCapitalShips)
                .Where(strandedShipSet.Contains)
                .SelectMany(ship => ship.GetChildren<Starfighter>())
                .Where(SpaceCombatQueries.IsActiveStarfighter)
                .OrderBy(fighter => fighter.Hyperdrive > 0 ? 1 : 0)
                .ToList();
            List<CapitalShip> recoveryCarriers = (retreatingFleets ?? Array.Empty<Fleet>())
                .Where(fleet => fleet != null && retreatableFleets?.Contains(fleet) == true)
                .SelectMany(SpaceCombatQueries.GetActiveCapitalShips)
                .Where(ship =>
                    !strandedShipSet.Contains(ship)
                    && ship.Hyperdrive > 0
                    && ship.StarfighterCapacity > 0
                )
                .ToList();
            Dictionary<CapitalShip, int> remainingCapacity = recoveryCarriers.ToDictionary(
                carrier => carrier,
                carrier => GetAvailableRecoveryCapacity(carrier, origin)
            );
            List<Starfighter> losses = new List<Starfighter>();

            foreach (Starfighter fighter in displacedFighters)
            {
                if (_spaceCombatQueries.CanRetreatFighter(fighter))
                    continue;

                CapitalShip recoveryCarrier = recoveryCarriers.FirstOrDefault(carrier =>
                    remainingCapacity[carrier] > 0
                );
                if (recoveryCarrier == null)
                {
                    losses.Add(fighter);
                    continue;
                }

                remainingCapacity[recoveryCarrier]--;
            }

            return losses;
        }

        /// <summary>Calculates carrier bays available to recover stranded fighters.</summary>
        /// <param name="carrier">The carrier receiving displaced fighters.</param>
        /// <param name="origin">The planet where withdrawal began.</param>
        /// <returns>The number of available recovery bays.</returns>
        private int GetAvailableRecoveryCapacity(CapitalShip carrier, Planet origin)
        {
            int releasableCapacity = carrier
                .GetChildren<Starfighter>()
                .Where(SpaceCombatQueries.IsActiveStarfighter)
                .Count(fighter => CanVacateCarrier(fighter, origin));
            return Math.Max(carrier.GetExcessStarfighterCapacity() + releasableCapacity, 0);
        }

        /// <summary>Returns whether a fighter can safely vacate a recovery carrier.</summary>
        /// <param name="fighter">The fighter occupying recovery capacity.</param>
        /// <param name="origin">The planet where withdrawal began.</param>
        /// <returns>True when the fighter can travel independently.</returns>
        private bool CanVacateCarrier(Starfighter fighter, Planet origin)
        {
            if (fighter?.Hyperdrive <= 0)
                return false;

            Planet fleetDestination = fighter.GetParentOfType<Planet>();
            return fleetDestination != origin
                    && _movementQueries.CanUseSafeRelocationDestination(
                        fighter,
                        fleetDestination,
                        origin
                    )
                || _movementQueries.CanEvacuateToNearestFriendlyPlanet(fighter);
        }
    }
}
