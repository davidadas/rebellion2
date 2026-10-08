using System;
using System.Collections.Generic;
using System.Linq;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Units;
using Rebellion.SceneGraph;

namespace Rebellion.Simulation
{
    /// <summary>
    /// Stores the destinations and carrier assignments reserved for one combat side's withdrawal.
    /// </summary>
    internal sealed class SpaceCombatWithdrawalPlan
    {
        private readonly Dictionary<Fleet, Planet> _fleetDestinations =
            new Dictionary<Fleet, Planet>();
        private readonly Dictionary<Starfighter, ContainerNode> _fighterDestinations =
            new Dictionary<Starfighter, ContainerNode>();
        private readonly Dictionary<Starfighter, CapitalShip> _fighterRecoveryAssignments =
            new Dictionary<Starfighter, CapitalShip>();
        private readonly HashSet<ISceneNode> _withdrawnUnits = new HashSet<ISceneNode>();

        /// <summary>Gets the reserved destination for each withdrawing fleet.</summary>
        internal IReadOnlyDictionary<Fleet, Planet> FleetDestinations => _fleetDestinations;

        /// <summary>Gets the reserved independent destination for each withdrawing fighter.</summary>
        internal IReadOnlyDictionary<Starfighter, ContainerNode> FighterDestinations =>
            _fighterDestinations;

        /// <summary>Gets the withdrawing carrier reserved for each dependent fighter.</summary>
        internal IReadOnlyDictionary<Starfighter, CapitalShip> FighterRecoveryAssignments =>
            _fighterRecoveryAssignments;

        /// <summary>Gets every unit that has a reserved way out of combat.</summary>
        internal IReadOnlyCollection<ISceneNode> WithdrawnUnits => _withdrawnUnits;

        /// <summary>Gets whether at least one participating unit can withdraw.</summary>
        internal bool CanWithdraw => _withdrawnUnits.Count > 0;

        /// <summary>Adds a fleet and its independently mobile ships to the plan.</summary>
        /// <param name="fleet">The fleet receiving the destination.</param>
        /// <param name="destination">The planet receiving the fleet.</param>
        /// <param name="ships">The ships capable of leaving with the fleet.</param>
        internal void AddFleet(Fleet fleet, Planet destination, IEnumerable<CapitalShip> ships)
        {
            if (fleet == null || destination == null)
                return;

            List<CapitalShip> withdrawingShips = (ships ?? Enumerable.Empty<CapitalShip>())
                .Where(ship => ship != null)
                .Distinct()
                .ToList();
            if (withdrawingShips.Count == 0)
                return;

            _fleetDestinations[fleet] = destination;
            _withdrawnUnits.UnionWith(withdrawingShips);
        }

        /// <summary>Adds an independent fighter and its reserved destination.</summary>
        /// <param name="fighter">The independently withdrawing fighter.</param>
        /// <param name="destination">The container reserved for the fighter.</param>
        internal void AddFighterDestination(Starfighter fighter, ContainerNode destination)
        {
            if (fighter == null || destination == null)
                return;

            _fighterDestinations[fighter] = destination;
            _withdrawnUnits.Add(fighter);
        }

        /// <summary>Adds a dependent fighter and the withdrawing carrier reserved for it.</summary>
        /// <param name="fighter">The fighter travelling aboard a carrier.</param>
        /// <param name="carrier">The carrier receiving the fighter.</param>
        internal void AddFighterRecovery(Starfighter fighter, CapitalShip carrier)
        {
            if (fighter == null || carrier == null)
                return;

            _fighterRecoveryAssignments[fighter] = carrier;
            _withdrawnUnits.Add(fighter);
        }

        /// <summary>Returns whether the unit has a reserved withdrawal path.</summary>
        /// <param name="unit">The participating unit to inspect.</param>
        /// <returns>True when the unit belongs to this plan.</returns>
        internal bool Contains(ISceneNode unit)
        {
            return unit != null && _withdrawnUnits.Contains(unit);
        }

        /// <summary>Returns the first planet receiving this combat side.</summary>
        /// <returns>The destination planet identifier, or null when the plan is empty.</returns>
        internal string GetRetreatPlanetInstanceID()
        {
            Planet destination = _fleetDestinations
                .Values.Concat(
                    _fighterDestinations.Values.Select(MovementQueries.RequireDestinationPlanet)
                )
                .FirstOrDefault();
            return destination?.InstanceID;
        }
    }

    /// <summary>Builds capacity-aware withdrawal plans without mutating game state.</summary>
    internal sealed class SpaceCombatWithdrawalPlanner
    {
        private readonly MovementQueries _movement;

        /// <summary>Creates a withdrawal planner from the shared movement rules.</summary>
        /// <param name="movement">The movement rules used to resolve safe destinations.</param>
        internal SpaceCombatWithdrawalPlanner(MovementQueries movement)
        {
            _movement = movement ?? throw new ArgumentNullException(nameof(movement));
        }

        /// <summary>Builds one coordinated withdrawal plan for a combat side.</summary>
        /// <param name="fleets">The side's participating fleets.</param>
        /// <param name="planet">The combat planet.</param>
        /// <param name="ownerInstanceId">The withdrawing faction identifier.</param>
        /// <param name="eligibleUnits">
        /// The units that completed tactical withdrawal, or null when every active participant is
        /// eligible to attempt withdrawal.
        /// </param>
        /// <returns>The reserved withdrawal plan.</returns>
        internal SpaceCombatWithdrawalPlan CreatePlan(
            IReadOnlyList<Fleet> fleets,
            Planet planet,
            string ownerInstanceId,
            ISet<ISceneNode> eligibleUnits = null
        )
        {
            SpaceCombatWithdrawalPlan plan = new SpaceCombatWithdrawalPlan();
            List<CapitalShip> participatingShips = GetParticipatingShips(fleets);
            List<Starfighter> participatingFighters = GetParticipatingFighters(
                participatingShips,
                planet,
                ownerInstanceId
            );
            List<CapitalShip> eligibleShips = FilterEligible(participatingShips, eligibleUnits);
            List<Starfighter> eligibleFighters = FilterEligible(
                participatingFighters,
                eligibleUnits
            );

            AddFleetDestinations(plan, fleets, planet, eligibleShips);
            AddFighterDestinations(plan, planet, participatingFighters, eligibleFighters);
            return plan;
        }

        /// <summary>Reserves a shared safe planet for every fleet with a mobile ship.</summary>
        /// <param name="plan">The plan receiving fleet destinations.</param>
        /// <param name="fleets">The participating fleets in stable encounter order.</param>
        /// <param name="planet">The combat planet.</param>
        /// <param name="eligibleShips">The ships eligible to complete withdrawal.</param>
        private void AddFleetDestinations(
            SpaceCombatWithdrawalPlan plan,
            IReadOnlyList<Fleet> fleets,
            Planet planet,
            IReadOnlyList<CapitalShip> eligibleShips
        )
        {
            List<CapitalShip> mobileShips = (eligibleShips ?? Array.Empty<CapitalShip>())
                .Where(ship => ship.Hyperdrive > 0)
                .ToList();
            List<Fleet> mobileFleets = (fleets ?? Array.Empty<Fleet>())
                .Where(fleet =>
                    fleet != null
                    && mobileShips.Any(ship =>
                        ReferenceEquals(ship.GetParentOfType<Fleet>(), fleet)
                    )
                )
                .ToList();
            Fleet representativeFleet = mobileFleets.FirstOrDefault();
            if (representativeFleet == null)
                return;

            Planet destination = _movement
                .FindSafeRelocationDestinations(representativeFleet, planet)
                .OfType<Planet>()
                .FirstOrDefault();
            if (destination == null)
                return;

            foreach (Fleet fleet in mobileFleets)
            {
                plan.AddFleet(
                    fleet,
                    destination,
                    mobileShips.Where(ship => ReferenceEquals(ship.GetParentOfType<Fleet>(), fleet))
                );
            }
        }

        /// <summary>Reserves independent fighter destinations and remaining carrier bays.</summary>
        /// <param name="plan">The plan receiving fighter assignments.</param>
        /// <param name="planet">The combat planet.</param>
        /// <param name="participatingFighters">Every active fighter participating in combat.</param>
        /// <param name="eligibleFighters">The fighters eligible to complete withdrawal.</param>
        private void AddFighterDestinations(
            SpaceCombatWithdrawalPlan plan,
            Planet planet,
            IReadOnlyList<Starfighter> participatingFighters,
            IReadOnlyList<Starfighter> eligibleFighters
        )
        {
            List<Starfighter> mobileFighters = (eligibleFighters ?? Array.Empty<Starfighter>())
                .Where(fighter => fighter.Hyperdrive > 0)
                .ToList();
            IReadOnlyList<ContainerNode> destinations =
                mobileFighters.Count == 0
                    ? Array.Empty<ContainerNode>()
                    : _movement.FindSafeRelocationDestinations(
                        mobileFighters[0],
                        planet,
                        leaveOriginPlanet: true
                    );
            Dictionary<ContainerNode, List<ISceneNode>> reservations =
                new Dictionary<ContainerNode, List<ISceneNode>>();

            foreach (Starfighter fighter in mobileFighters)
            {
                ContainerNode destination = destinations.FirstOrDefault(candidate =>
                    CanReserve(candidate, fighter, reservations)
                );
                if (destination == null)
                    continue;

                Reserve(destination, fighter, reservations);
                plan.AddFighterDestination(fighter, destination);
            }

            HashSet<Starfighter> carrierDependentFighters = (
                eligibleFighters ?? Array.Empty<Starfighter>()
            )
                .Where(fighter => !plan.Contains(fighter))
                .ToHashSet();
            List<Starfighter> recoveryOrder = (participatingFighters ?? Array.Empty<Starfighter>())
                .OrderBy(fighter => fighter.Hyperdrive > 0 ? 1 : 0)
                .ToList();
            Dictionary<Starfighter, CapitalShip> recoveryAssignments = GetRecoveryAssignments(
                plan.WithdrawnUnits.OfType<CapitalShip>(),
                recoveryOrder,
                carrierDependentFighters
            );
            foreach (KeyValuePair<Starfighter, CapitalShip> assignment in recoveryAssignments)
                plan.AddFighterRecovery(assignment.Key, assignment.Value);
        }

        /// <summary>Returns active capital ships in stable participating-fleet order.</summary>
        /// <param name="fleets">The participating fleets.</param>
        /// <returns>The active capital ships.</returns>
        internal static List<CapitalShip> GetParticipatingShips(IReadOnlyList<Fleet> fleets)
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
        internal static List<Starfighter> GetParticipatingFighters(
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
                    assignments[fighter] = carrier;
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

        /// <summary>Filters participating units to the supplied tactical candidates.</summary>
        /// <typeparam name="TUnit">The scene-node type being filtered.</typeparam>
        /// <param name="units">The participating units.</param>
        /// <param name="eligibleUnits">The allowed units, or null to allow every participant.</param>
        /// <returns>The eligible units in stable participation order.</returns>
        private static List<TUnit> FilterEligible<TUnit>(
            IEnumerable<TUnit> units,
            ISet<ISceneNode> eligibleUnits
        )
            where TUnit : class, ISceneNode
        {
            return (units ?? Enumerable.Empty<TUnit>())
                .Where(unit => eligibleUnits?.Contains(unit) != false)
                .ToList();
        }

        /// <summary>Returns whether a destination has room after existing reservations.</summary>
        /// <param name="destination">The candidate destination.</param>
        /// <param name="fighter">The fighter requesting a reservation.</param>
        /// <param name="reservations">The reservations already made by this plan.</param>
        /// <returns>True when the fighter can reserve the destination.</returns>
        private static bool CanReserve(
            ContainerNode destination,
            Starfighter fighter,
            IReadOnlyDictionary<ContainerNode, List<ISceneNode>> reservations
        )
        {
            IReadOnlyCollection<ISceneNode> plannedChildren =
                destination != null
                && reservations.TryGetValue(
                    destination,
                    out List<ISceneNode> destinationReservations
                )
                    ? destinationReservations
                    : Array.Empty<ISceneNode>();
            return destination?.CanAcceptChild(fighter, plannedChildren) == true;
        }

        /// <summary>Adds a fighter reservation to a destination.</summary>
        /// <param name="destination">The reserved destination.</param>
        /// <param name="fighter">The fighter consuming destination capacity.</param>
        /// <param name="reservations">The reservations being built.</param>
        private static void Reserve(
            ContainerNode destination,
            Starfighter fighter,
            IDictionary<ContainerNode, List<ISceneNode>> reservations
        )
        {
            if (!reservations.TryGetValue(destination, out List<ISceneNode> plannedChildren))
            {
                plannedChildren = new List<ISceneNode>();
                reservations[destination] = plannedChildren;
            }

            plannedChildren.Add(fighter);
        }

        /// <summary>Calculates bays available after non-travelling participants are removed.</summary>
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
