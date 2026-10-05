using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using Rebellion.Game;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Results;
using Rebellion.Game.Units;
using Rebellion.SceneGraph;
using Rebellion.Util.Logging;

namespace Rebellion.Simulation
{
    /// <summary>
    /// Selects newly blockaded destinations and requests their inbound-unit reactions.
    /// </summary>
    public sealed class MovementObserver : IResultObserver, IDisposable
    {
        private readonly MovementCommands _commands;
        private readonly GameRoot _game;
        private readonly MovementQueries _queries;
        private IDisposable[] _subscriptions;

        /// <summary>
        /// Creates the blockade result observer.
        /// </summary>
        /// <param name="game">The active game graph.</param>
        /// <param name="commands">The general movement operations.</param>
        /// <param name="queries">The movement routing and destination rules.</param>
        public MovementObserver(GameRoot game, MovementCommands commands, MovementQueries queries)
        {
            _game = game ?? throw new ArgumentNullException(nameof(game));
            _commands = commands ?? throw new ArgumentNullException(nameof(commands));
            _queries = queries ?? throw new ArgumentNullException(nameof(queries));
        }

        /// <summary>Registers the blockade callback with the result bus.</summary>
        /// <param name="results">The bus that delivers blockade changes.</param>
        public void Connect(GameResultBus results)
        {
            if (_subscriptions != null)
                throw new InvalidOperationException("Movement observer is already connected.");
            if (results == null)
                throw new ArgumentNullException(nameof(results));

            _subscriptions = new IDisposable[]
            {
                results.Subscribe<BlockadeChangedResult>(HandleResults),
                results.Subscribe<GameObjectDestroyedResult>(HandleResults),
                results.Subscribe<GameObjectScrappedResult>(HandleResults),
            };
        }

        /// <summary>Stops receiving blockade changes.</summary>
        public void Dispose()
        {
            foreach (IDisposable subscription in _subscriptions ?? Array.Empty<IDisposable>())
                subscription.Dispose();
        }

        /// <summary>
        /// Applies movement reactions to newly started blockades.
        /// </summary>
        /// <param name="results">The blockade changes to inspect.</param>
        /// <returns>The movement and destruction results caused by blockade starts.</returns>
        public List<GameResult> HandleResults(IReadOnlyList<BlockadeChangedResult> results)
        {
            List<GameResult> reactions = new List<GameResult>();
            if (results == null)
                return reactions;

            HashSet<string> handledPlanets = new HashSet<string>(StringComparer.Ordinal);
            foreach (BlockadeChangedResult result in results)
            {
                if (
                    result?.Blockaded != true
                    || result.Planet == null
                    || result.BlockadingFleet == null
                    || !result.Planet.IsBlockaded()
                    || !handledPlanets.Add(result.Planet.InstanceID)
                )
                    continue;

                HandleBlockadeStarted(result, reactions);
            }

            return reactions;
        }

        /// <summary>Redirects or destroys inbound units after a blockade begins.</summary>
        /// <param name="result">The completed blockade change.</param>
        /// <param name="reactions">The collection receiving movement and destruction facts.</param>
        private void HandleBlockadeStarted(
            BlockadeChangedResult result,
            ICollection<GameResult> reactions
        )
        {
            string blockadingOwner = result.BlockadingFleet.GetOwnerInstanceID();
            if (string.IsNullOrEmpty(blockadingOwner))
                return;

            List<IMovable> inboundUnits = result
                .Planet.GetChildren<IMovable>(recursive: true)
                .Where(unit => unit.Movement != null)
                .Where(unit => unit.GetOwnerInstanceID() != blockadingOwner)
                .ToList();
            foreach (IMovable unit in inboundUnits)
            {
                if (unit is Building)
                {
                    DestroyBlockadeInboundUnit(unit, result.Planet, reactions);
                    continue;
                }

                if (!ShouldAutorouteFromBlockade(unit))
                    continue;

                ContainerNode destination = _queries.FindBlockadeAutorouteDestination(
                    unit,
                    result.Planet
                );
                Planet destinationPlanet =
                    destination as Planet ?? destination?.GetParentOfType<Planet>();
                if (destinationPlanet == null)
                {
                    DestroyBlockadeInboundUnit(unit, result.Planet, reactions);
                    continue;
                }

                _game.MoveNode(unit, destination);
                RetargetMovement(unit, destinationPlanet);
                reactions.Add(
                    new GameObjectEnrouteResult { GameObject = unit, Tick = _game.CurrentTick }
                );
            }
        }

        /// <summary>Returns whether an inbound unit must seek another destination.</summary>
        /// <param name="unit">The inbound unit.</param>
        /// <returns>True when the blockade forces autorouting.</returns>
        private static bool ShouldAutorouteFromBlockade(IMovable unit)
        {
            return unit is Starfighter
                || unit is Regiment
                || unit is SpecialForces specialForces && !specialForces.IsOnMission();
        }

        /// <summary>Destroys an inbound unit that cannot enter or route around a blockade.</summary>
        /// <param name="unit">The unit to destroy.</param>
        /// <param name="planet">The blockaded destination.</param>
        /// <param name="reactions">The collection receiving the destruction fact.</param>
        private void DestroyBlockadeInboundUnit(
            IMovable unit,
            Planet planet,
            ICollection<GameResult> reactions
        )
        {
            _game.DeleteNode(unit);
            reactions.Add(
                new GameObjectDestroyedResult
                {
                    DestroyedObject = unit,
                    Context = planet,
                    Tick = _game.CurrentTick,
                }
            );
        }

        /// <summary>Retargets a moving unit from its current position.</summary>
        /// <param name="unit">The unit being rerouted.</param>
        /// <param name="destination">The new destination planet.</param>
        private void RetargetMovement(IMovable unit, Planet destination)
        {
            MovementState movement = unit.Movement;
            Point currentPosition = movement.CurrentPosition;
            unit.Movement = new MovementState
            {
                TransitTicks = _queries.CalculateTransitTicks(
                    unit,
                    currentPosition,
                    destination,
                    sameSector: false
                ),
                TicksElapsed = 0,
                MovementGroupID = movement.MovementGroupID,
                SourceEventInstanceID = movement.SourceEventInstanceID,
                OriginPosition = currentPosition,
                CurrentPosition = currentPosition,
            };
        }

        /// <summary>
        /// Relocates eligible occupants after a destruction result removes their capital ship.
        /// </summary>
        /// <param name="results">The destruction results to inspect.</param>
        /// <returns>Movement and follow-up destruction results.</returns>
        public List<GameResult> HandleResults(IReadOnlyList<GameObjectDestroyedResult> results)
        {
            List<GameResult> reactions = new List<GameResult>();
            if (results == null)
                return reactions;

            HashSet<string> destroyedInstanceIds = results
                .Select(result => result?.DestroyedObject?.InstanceID)
                .Where(instanceId => !string.IsNullOrEmpty(instanceId))
                .ToHashSet(StringComparer.Ordinal);
            foreach (
                GameObjectDestroyedResult result in results.Where(result =>
                    result?.DestroyedObject is CapitalShip
                )
            )
            {
                RelocateRemovedCapitalShipOccupants(
                    (CapitalShip)result.DestroyedObject,
                    result.Context,
                    destroyedInstanceIds,
                    recoverStarfighters: result.Reason == UnitDestructionReason.Combat,
                    results: reactions
                );
            }

            return reactions;
        }

        /// <summary>
        /// Relocates eligible occupants after intentional scrapping removes their capital ship.
        /// </summary>
        /// <param name="results">The scrapping results to inspect.</param>
        /// <returns>Movement and follow-up destruction results.</returns>
        public List<GameResult> HandleResults(IReadOnlyList<GameObjectScrappedResult> results)
        {
            List<GameResult> reactions = new List<GameResult>();
            if (results == null)
                return reactions;

            HashSet<string> scrappedInstanceIds = results
                .Select(result => result?.ScrappedObject?.InstanceID)
                .Where(instanceId => !string.IsNullOrEmpty(instanceId))
                .ToHashSet(StringComparer.Ordinal);
            foreach (
                GameObjectScrappedResult result in results.Where(result =>
                    result?.ScrappedObject is CapitalShip
                )
            )
            {
                RelocateRemovedCapitalShipOccupants(
                    (CapitalShip)result.ScrappedObject,
                    result.Context,
                    scrappedInstanceIds,
                    recoverStarfighters: false,
                    results: reactions
                );
            }

            return reactions;
        }

        /// <summary>Recovers surviving occupants retained by a removed capital ship.</summary>
        /// <param name="removedShip">The removed ship retaining its former children.</param>
        /// <param name="context">The removal location.</param>
        /// <param name="removedInstanceIds">Units removed by the same result batch.</param>
        /// <param name="recoverStarfighters">Whether completed starfighters survived.</param>
        /// <param name="results">The collection receiving recovery facts.</param>
        private void RelocateRemovedCapitalShipOccupants(
            CapitalShip removedShip,
            IGameEntity context,
            ISet<string> removedInstanceIds,
            bool recoverStarfighters,
            ICollection<GameResult> results
        )
        {
            Fleet previousFleet =
                removedShip.GetParent() as Fleet ?? removedShip.GetLastParent() as Fleet;
            Fleet liveFleet = string.IsNullOrEmpty(previousFleet?.InstanceID)
                ? null
                : _game.GetSceneNodeByInstanceID<Fleet>(
                    previousFleet.InstanceID,
                    includeDisabled: true
                );
            Planet origin =
                context as Planet
                ?? liveFleet?.GetParentOfType<Planet>()
                ?? previousFleet?.GetLastParent() as Planet;
            IEnumerable<IMovable> occupants = removedShip
                .GetChildren<Officer>(includeDisabled: true)
                .Cast<IMovable>();
            if (recoverStarfighters)
            {
                occupants = occupants.Concat(
                    removedShip
                        .GetChildren<Starfighter>(includeDisabled: true)
                        .Where(fighter =>
                            fighter.ManufacturingStatus == ManufacturingStatus.Complete
                        )
                );
            }

            foreach (
                IMovable occupant in occupants
                    .Where(occupant =>
                        occupant != null
                        && !(removedInstanceIds?.Contains(occupant.InstanceID) ?? false)
                    )
                    .OrderBy(occupant =>
                        occupant is Starfighter fighter && fighter.Hyperdrive <= 0 ? 0 : 1
                    )
                    .ToList()
            )
            {
                RelocateRemovedCapitalShipOccupant(occupant, liveFleet, origin, results);
            }
        }

        /// <summary>Moves one removed-ship occupant to the nearest safe container.</summary>
        /// <param name="occupant">The surviving occupant.</param>
        /// <param name="fleet">The former fleet when it remains active.</param>
        /// <param name="origin">The planet where removal occurred.</param>
        /// <param name="results">The collection receiving recovery facts.</param>
        private void RelocateRemovedCapitalShipOccupant(
            IMovable occupant,
            Fleet fleet,
            Planet origin,
            ICollection<GameResult> results
        )
        {
            if (origin == null)
                return;

            ContainerNode destination = FindFleetRecoveryCarrier(occupant, fleet);
            if (destination == null && occupant is Starfighter { Hyperdrive: <= 0 })
            {
                FreeFleetRecoveryCapacity(fleet, origin, results);
                destination = FindFleetRecoveryCarrier(occupant, fleet);
            }

            destination ??= _queries
                .FindSafeRelocationDestinations(occupant, origin, allowOriginPlanet: true)
                .FirstOrDefault();
            if (destination == null)
            {
                ResolveStrandedOccupant(occupant, origin, results);
                return;
            }

            RestoreOccupant((ISceneNode)occupant, destination);
            Planet destinationPlanet = MovementQueries.RequireDestinationPlanet(destination);
            bool remainsWithFleet =
                destination is CapitalShip && destination.GetParentOfType<Fleet>() == fleet;
            if (!remainsWithFleet && destinationPlanet != origin)
                StartTransit(occupant, origin, destination, results);
        }

        /// <summary>Captures a stranded officer when no friendly destination exists.</summary>
        /// <param name="occupant">The stranded occupant.</param>
        /// <param name="origin">The removal planet.</param>
        /// <param name="results">The collection receiving capture facts.</param>
        private void ResolveStrandedOccupant(
            IMovable occupant,
            Planet origin,
            ICollection<GameResult> results
        )
        {
            if (occupant is not Officer officer)
                return;

            string captorInstanceId = origin.GetOwnerInstanceID();
            if (captorInstanceId == officer.GetOwnerInstanceID())
            {
                throw new InvalidOperationException(
                    $"Officer '{officer.InstanceID}' has no valid destination or captor after carrier removal."
                );
            }

            officer.Movement = null;
            if (officer.TryCapture(captorInstanceId))
            {
                results.Add(
                    new OfficerCaptureStateResult
                    {
                        TargetOfficer = officer,
                        IsCaptured = true,
                        ParentAtCapture = officer.GetParent(),
                        Context = origin,
                        Tick = _game.CurrentTick,
                    }
                );
                GameLogger.Log(
                    $"{officer.GetDisplayName()} was captured when its carrier was removed at {origin.GetDisplayName()}."
                );
            }

            RestoreOccupant(officer, origin);
        }

        /// <summary>Finds a compatible surviving carrier in the removed ship's fleet.</summary>
        /// <param name="occupant">The occupant requiring recovery.</param>
        /// <param name="fleet">The surviving fleet.</param>
        /// <returns>The first compatible carrier, or null.</returns>
        private static CapitalShip FindFleetRecoveryCarrier(IMovable occupant, Fleet fleet)
        {
            return fleet
                ?.GetChildren<CapitalShip>()
                .Where(ship =>
                    ship.ManufacturingStatus == ManufacturingStatus.Complete
                    && ship.Movement == null
                )
                .OrderBy(ship => ship.InstanceID, StringComparer.Ordinal)
                .FirstOrDefault(ship => ship.CanAcceptChild(occupant));
        }

        /// <summary>Moves a mobile fighter out of a carrier to free recovery capacity.</summary>
        /// <param name="fleet">The surviving fleet.</param>
        /// <param name="origin">The carrier-removal planet.</param>
        /// <param name="results">The collection receiving movement facts.</param>
        private void FreeFleetRecoveryCapacity(
            Fleet fleet,
            Planet origin,
            ICollection<GameResult> results
        )
        {
            if (fleet == null || origin == null)
                return;

            Starfighter mobileFighter = fleet
                .GetChildren<CapitalShip>()
                .Where(ship =>
                    ship.ManufacturingStatus == ManufacturingStatus.Complete
                    && ship.Movement == null
                )
                .OrderBy(ship => ship.InstanceID, StringComparer.Ordinal)
                .SelectMany(ship => ship.GetChildren<Starfighter>())
                .FirstOrDefault(fighter =>
                    fighter.ManufacturingStatus == ManufacturingStatus.Complete
                    && fighter.Movement == null
                    && fighter.Hyperdrive > 0
                    && CanVacateRecoveryCarrier(fighter, origin)
                );
            if (mobileFighter == null)
                return;

            Planet fleetDestination = mobileFighter.GetParentOfType<Planet>();
            if (
                fleetDestination != origin
                && _queries.CanUseSafeRelocationDestination(mobileFighter, fleetDestination, origin)
            )
            {
                _game.MoveNode(mobileFighter, fleetDestination);
                StartTransit(mobileFighter, origin, fleetDestination, results);
                return;
            }

            _commands.TryEvacuateToNearestFriendlyPlanet(mobileFighter, results);
        }

        /// <summary>Returns whether a mobile fighter can safely vacate its carrier.</summary>
        /// <param name="fighter">The fighter occupying recovery capacity.</param>
        /// <param name="origin">The carrier-removal planet.</param>
        /// <returns>True when the fighter has a safe destination.</returns>
        private bool CanVacateRecoveryCarrier(Starfighter fighter, Planet origin)
        {
            Planet fleetDestination = fighter.GetParentOfType<Planet>();
            return fleetDestination != origin
                    && _queries.CanUseSafeRelocationDestination(fighter, fleetDestination, origin)
                || _queries.CanEvacuateToNearestFriendlyPlanet(fighter);
        }

        /// <summary>Starts transit for a recovered unit already attached to its destination.</summary>
        /// <param name="occupant">The recovered unit.</param>
        /// <param name="origin">The removal planet.</param>
        /// <param name="destination">The receiving container.</param>
        /// <param name="results">The collection receiving movement facts.</param>
        private void StartTransit(
            IMovable occupant,
            Planet origin,
            ContainerNode destination,
            ICollection<GameResult> results
        )
        {
            Planet destinationPlanet = MovementQueries.RequireDestinationPlanet(destination);
            occupant.Movement = new MovementState
            {
                TransitTicks = _queries.CalculateTransitTicks(occupant, origin, destinationPlanet),
                TicksElapsed = 0,
                MovementGroupID = Guid.NewGuid().ToString("N"),
                OriginPosition = origin.GetPosition(),
                CurrentPosition = origin.GetPosition(),
            };
            results.Add(
                new GameObjectEnrouteResult { GameObject = occupant, Tick = _game.CurrentTick }
            );
            results.Add(
                new GameObjectEnrouteActiveResult
                {
                    GameObject = occupant,
                    IsActive = true,
                    Tick = _game.CurrentTick,
                }
            );
        }

        /// <summary>Registers a retained child beneath a live destination.</summary>
        /// <param name="occupant">The retained scene node.</param>
        /// <param name="destination">The receiving container.</param>
        private void RestoreOccupant(ISceneNode occupant, ContainerNode destination)
        {
            _game.DetachNode(occupant);
            _game.AttachNode(occupant, destination);
        }
    }
}
