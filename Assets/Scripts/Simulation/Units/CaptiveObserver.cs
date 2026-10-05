using System;
using System.Collections.Generic;
using System.Linq;
using Rebellion.Game;
using Rebellion.Game.Factions;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Results;
using Rebellion.Game.Units;
using Rebellion.SceneGraph;
using Rebellion.Util.Logging;
using Rebellion.Util.Random;

namespace Rebellion.Simulation
{
    /// <summary>Routes capture and ownership changes to custody operations in batch order.</summary>
    public sealed class CaptiveObserver : IResultObserver, IDisposable
    {
        private readonly GameRoot _game;
        private readonly MovementCommands _movement;
        private readonly MovementQueries _movementQueries;
        private readonly IRandomNumberProvider _random;
        private readonly GameConfig.TickRangeConfig _escapeAttemptInterval;
        private IDisposable[] _subscriptions;

        /// <summary>Creates the custody listener for the active game.</summary>
        /// <param name="game">The authoritative game used to resolve officer ownership.</param>
        /// <param name="movement">The movement operations used to transfer captives.</param>
        /// <param name="movementQueries">The movement destination rules.</param>
        /// <param name="random">The random provider used to schedule escape attempts.</param>
        public CaptiveObserver(
            GameRoot game,
            MovementCommands movement,
            MovementQueries movementQueries,
            IRandomNumberProvider random
        )
        {
            _game = game ?? throw new ArgumentNullException(nameof(game));
            _movement = movement ?? throw new ArgumentNullException(nameof(movement));
            _movementQueries =
                movementQueries ?? throw new ArgumentNullException(nameof(movementQueries));
            _random = random ?? throw new ArgumentNullException(nameof(random));
            _escapeAttemptInterval = game.Config.Captive.EscapeAttemptInterval;
        }

        /// <summary>Registers capture and ownership callbacks with the result bus.</summary>
        /// <param name="results">The bus that delivers capture and ownership changes.</param>
        public void Connect(GameResultBus results)
        {
            if (_subscriptions != null)
                throw new InvalidOperationException("Captive observer is already connected.");
            if (results == null)
                throw new ArgumentNullException(nameof(results));

            _subscriptions = new IDisposable[]
            {
                results.Subscribe<PlanetOwnershipChangedResult>(HandleResults),
                results.Subscribe<OfficerCaptureStateResult>(HandleResults),
            };
        }

        /// <summary>Stops receiving capture and ownership changes.</summary>
        public void Dispose()
        {
            foreach (IDisposable subscription in _subscriptions ?? Array.Empty<IDisposable>())
                subscription.Dispose();
        }

        /// <summary>
        /// Establishes custody for newly captured officers and records the location revealed to
        /// their original factions.
        /// </summary>
        /// <param name="results">The capture-state changes to process.</param>
        /// <returns>Movement results produced while transferring captives.</returns>
        public List<GameResult> HandleResults(IReadOnlyList<OfficerCaptureStateResult> results)
        {
            List<GameResult> reactions = new List<GameResult>();
            if (results == null)
                return reactions;

            HashSet<string> handledOfficerIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (OfficerCaptureStateResult result in results)
            {
                Officer officer = result?.TargetOfficer;
                if (officer == null)
                    continue;

                if (result.IsCaptured == false)
                {
                    officer.NextEscapeAttemptTick = 0;
                    continue;
                }

                if (
                    officer.IsCaptured != true
                    || string.IsNullOrEmpty(officer.CaptorInstanceID)
                    || !handledOfficerIds.Add(officer.InstanceID)
                )
                    continue;

                EstablishCustody(
                    officer,
                    result.Context,
                    result.CapturingUnit,
                    result.Tick,
                    reactions
                );
            }

            return reactions;
        }

        /// <summary>
        /// Releases captured officers when their faction takes control of the planet holding them.
        /// </summary>
        /// <param name="results">The planet ownership changes to process.</param>
        /// <returns>Capture-state changes for the officers released by the ownership changes.</returns>
        public List<GameResult> HandleResults(IReadOnlyList<PlanetOwnershipChangedResult> results)
        {
            List<GameResult> reactions = new List<GameResult>();
            foreach (
                PlanetOwnershipChangedResult result in results
                    ?? Array.Empty<PlanetOwnershipChangedResult>()
            )
            {
                Planet planet = result?.Planet;
                string newOwnerInstanceID = result?.NewOwner?.InstanceID;
                if (planet == null || string.IsNullOrEmpty(newOwnerInstanceID))
                    continue;

                foreach (
                    Officer officer in planet
                        .GetAllOfficers()
                        .Where(officer =>
                            officer.IsCaptured
                            && !officer.IsKilled
                            && officer.GetOwnerInstanceID() == newOwnerInstanceID
                        )
                )
                {
                    reactions.Add(
                        CaptiveStateTransition.Release(
                            officer,
                            planet,
                            result.Tick,
                            officer.CaptorInstanceID
                        )
                    );
                }
            }

            return reactions;
        }

        /// <summary>Transfers a newly captured officer into captor-controlled custody.</summary>
        /// <param name="officer">The captured officer.</param>
        /// <param name="context">The capture location.</param>
        /// <param name="capturingUnit">The unit responsible for the capture, if present.</param>
        /// <param name="tick">The tick when the capture occurred.</param>
        /// <param name="reactions">The collection receiving movement results.</param>
        private void EstablishCustody(
            Officer officer,
            IGameEntity context,
            ISceneNode capturingUnit,
            int tick,
            List<GameResult> reactions
        )
        {
            ContainerNode destination = ResolveCustodyDestination(context, capturingUnit, officer);
            if (
                destination == null
                || !TransferToCustody(
                    officer,
                    destination,
                    GetCustodyEscort(capturingUnit),
                    reactions
                )
            )
            {
                GameLogger.Log(
                    $"Captured officer {officer.GetDisplayName()} has no valid custody destination for {officer.CaptorInstanceID}.",
                    GameLogger.LogLevel.Error
                );
                return;
            }

            AddCustodyObservations(officer, tick, reactions);
            if (officer.CanEscape && officer.NextEscapeAttemptTick <= 0)
                ScheduleEscapeAttempt(officer);
        }

        /// <summary>Reveals established custody to the officer's owner and captor.</summary>
        /// <param name="officer">The officer whose custody was established.</param>
        /// <param name="tick">The tick when custody was established.</param>
        /// <param name="reactions">The collection receiving intelligence results.</param>
        private void AddCustodyObservations(
            Officer officer,
            int tick,
            ICollection<GameResult> reactions
        )
        {
            Faction owner = _game.GetFactionByOwnerInstanceID(officer.OwnerInstanceID);
            Faction captor = _game.GetFactionByOwnerInstanceID(officer.CaptorInstanceID);
            if (owner != null)
            {
                reactions.Add(
                    new IntelligenceRevealedResult
                    {
                        Recipient = owner,
                        Observations = new List<ISceneNode> { officer },
                        Tick = tick,
                    }
                );
            }

            if (captor != null && captor != owner)
            {
                reactions.Add(
                    new IntelligenceRevealedResult
                    {
                        Recipient = captor,
                        Observations = new List<ISceneNode> { officer },
                        Tick = tick,
                    }
                );
            }
        }

        /// <summary>Transfers a captured officer into a validated captor-controlled container.</summary>
        /// <param name="officer">The captured officer.</param>
        /// <param name="destination">The requested custody destination.</param>
        /// <param name="escort">The capturing unit accompanying a remote transfer.</param>
        /// <param name="results">The collection receiving movement facts.</param>
        /// <returns>True when custody is established or already correct.</returns>
        private bool TransferToCustody(
            Officer officer,
            ContainerNode destination,
            IMovable escort,
            List<GameResult> results
        )
        {
            destination = _movementQueries.ResolveLiveContainer(destination);
            if (
                !_movementQueries.TryResolveAcceptedDestination(
                    officer,
                    destination,
                    out ContainerNode resolvedDestination
                )
                || resolvedDestination.GetOwnerInstanceID() != officer.CaptorInstanceID
            )
                return false;

            if (ReferenceEquals(officer.GetParent(), resolvedDestination))
                return true;

            Planet origin = officer.GetParentOfType<Planet>();
            Planet destinationPlanet = MovementQueries.RequireDestinationPlanet(
                resolvedDestination
            );
            if (!officer.IsActive() || ReferenceEquals(origin, destinationPlanet) || escort == null)
            {
                officer.Movement = null;
                _game.MoveNode(officer, resolvedDestination);
                return true;
            }

            return _movement.TryRequestMove(
                new IMovable[] { escort, officer },
                new[] { resolvedDestination },
                sourceEventInstanceID: null,
                results
            );
        }

        /// <summary>Schedules the next escape attempt for a newly established captive.</summary>
        /// <param name="officer">The captive officer.</param>
        private void ScheduleEscapeAttempt(Officer officer)
        {
            int minimum = Math.Max(0, _escapeAttemptInterval?.Minimum ?? 0);
            int maximum = Math.Max(minimum, _escapeAttemptInterval?.Maximum ?? minimum);
            officer.NextEscapeAttemptTick = checked(
                _game.CurrentTick + _random.NextInt(minimum, maximum + 1)
            );
        }

        /// <summary>Finds the captor-controlled container that should receive an officer.</summary>
        /// <param name="context">The capture location.</param>
        /// <param name="capturingUnit">The capturing unit, if present.</param>
        /// <param name="officer">The captured officer.</param>
        /// <returns>The selected custody destination, or null.</returns>
        private ContainerNode ResolveCustodyDestination(
            IGameEntity context,
            ISceneNode capturingUnit,
            Officer officer
        )
        {
            string captorInstanceId = officer.CaptorInstanceID;
            ContainerNode destination = GetCaptorControlledContainer(
                officer.GetParent() as ContainerNode,
                captorInstanceId,
                officer
            );
            if (destination != null)
                return destination;

            destination = GetCaptorControlledContainer(
                GetCapturePlanet(context),
                captorInstanceId,
                officer
            );
            if (destination != null)
                return destination;

            destination = GetCapturingUnitCustody(capturingUnit, captorInstanceId, officer);
            if (destination != null)
                return destination;

            Faction captor = _game.GetFactionByOwnerInstanceID(captorInstanceId);
            return captor
                ?.GetOwnedColonizedPlanets()
                .Where(planet =>
                    !planet.IsDestroyed
                    && string.Equals(
                        planet.GetOwnerInstanceID(),
                        captorInstanceId,
                        StringComparison.Ordinal
                    )
                    && planet.CanAcceptChild(officer)
                )
                .OrderBy(planet => planet.GetRawDistanceTo(((IMovable)officer).GetPosition()))
                .ThenBy(planet => planet.InstanceID, StringComparer.Ordinal)
                .FirstOrDefault();
        }

        /// <summary>Gets the planet containing a capture context.</summary>
        /// <param name="context">The capture context.</param>
        /// <returns>The capture planet, or null.</returns>
        private static Planet GetCapturePlanet(IGameEntity context) =>
            context as Planet ?? (context as ISceneNode)?.GetParentOfType<Planet>();

        /// <summary>Finds captor-controlled custody associated with the capturing unit.</summary>
        /// <param name="capturingUnit">The capturing unit.</param>
        /// <param name="captorInstanceId">The captor faction identifier.</param>
        /// <param name="officer">The captured officer.</param>
        /// <returns>The custody container, or null.</returns>
        private static ContainerNode GetCapturingUnitCustody(
            ISceneNode capturingUnit,
            string captorInstanceId,
            Officer officer
        )
        {
            if (capturingUnit == null)
                return null;

            CapitalShip ship =
                capturingUnit as CapitalShip ?? capturingUnit.GetParentOfType<CapitalShip>();
            return GetCaptorControlledContainer(ship, captorInstanceId, officer)
                ?? GetCaptorControlledContainer(
                    capturingUnit.GetParent() as ContainerNode,
                    captorInstanceId,
                    officer
                );
        }

        /// <summary>Gets the movable escort represented by a capturing unit.</summary>
        /// <param name="capturingUnit">The capturing unit.</param>
        /// <returns>The escort, or null.</returns>
        private static IMovable GetCustodyEscort(ISceneNode capturingUnit) =>
            capturingUnit switch
            {
                Officer officer => officer,
                SpecialForces specialForces => specialForces,
                _ => null,
            };

        /// <summary>Returns an eligible captor-controlled custody container.</summary>
        /// <param name="container">The possible custody container.</param>
        /// <param name="captorInstanceId">The captor faction identifier.</param>
        /// <param name="officer">The captured officer.</param>
        /// <returns>The container when eligible; otherwise null.</returns>
        private static ContainerNode GetCaptorControlledContainer(
            ContainerNode container,
            string captorInstanceId,
            Officer officer
        )
        {
            return
                container is Planet or CapitalShip
                && IsControlledBy(container, captorInstanceId)
                && container.CanAcceptChild(officer)
                ? container
                : null;
        }

        /// <summary>Determines whether a custody container is usable by a faction.</summary>
        /// <param name="node">The container to inspect.</param>
        /// <param name="captorInstanceId">The captor faction identifier.</param>
        /// <returns>True when the container is controlled and operational.</returns>
        private static bool IsControlledBy(ISceneNode node, string captorInstanceId)
        {
            if (node == null || string.IsNullOrEmpty(captorInstanceId))
                return false;
            if (node is Planet { IsDestroyed: true })
                return false;
            if (
                node is CapitalShip capitalShip
                && capitalShip.ManufacturingStatus != ManufacturingStatus.Complete
            )
                return false;

            return string.Equals(
                node.GetOwnerInstanceID(),
                captorInstanceId,
                StringComparison.Ordinal
            );
        }
    }
}
