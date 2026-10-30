using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Rebellion.Game;
using Rebellion.Game.Factions;
using Rebellion.Game.Galaxy;
using Rebellion.Game.PlayerRequests;
using Rebellion.Game.Results;
using Rebellion.Game.Units;
using Rebellion.SceneGraph;

namespace Rebellion.Gameplay.PlayerRequests
{
    /// <summary>
    /// Executes explicit player requests through the active session's existing domain operations.
    /// The caller owns ordering and tick boundaries and supplies the authenticated actor separately.
    /// </summary>
    public sealed partial class PlayerRequestExecutor
    {
        private readonly GameSession _session;
        private readonly GameManager _gameManager;

        /// <summary>
        /// Binds player request execution to one session and its clock.
        /// </summary>
        /// <param name="session">The authoritative session receiving requests.</param>
        /// <param name="gameManager">The clock controlling that session.</param>
        public PlayerRequestExecutor(GameSession session, GameManager gameManager)
        {
            _session = session ?? throw new ArgumentNullException(nameof(session));
            _gameManager = gameManager ?? throw new ArgumentNullException(nameof(gameManager));
        }

        /// <summary>
        /// Executes a request using the actor established by the caller's player binding.
        /// </summary>
        /// <param name="authenticatedActorFactionID">The faction authorized to issue the request.</param>
        /// <param name="playerRequest">The typed request to execute.</param>
        /// <returns>The completed domain outcome; delivering or queuing a request is not acceptance.</returns>
        public PlayerRequestOutcome Execute(
            string authenticatedActorFactionID,
            PlayerRequest playerRequest
        )
        {
            if (!_session.CanExecuteCommands)
                return Reject("The simulation is not at a writable command boundary.");
            return ExecuteAsync(authenticatedActorFactionID, playerRequest)
                .GetAwaiter()
                .GetResult();
        }

        /// <summary>
        /// Defers a local request until this session can execute it and report its real outcome.
        /// </summary>
        /// <param name="authenticatedActorFactionID">The faction authorized at submission.</param>
        /// <param name="playerRequest">The typed request to execute.</param>
        /// <returns>The completed result, cancellation on replacement, or execution failure.</returns>
        public Task<PlayerRequestOutcome> ExecuteAsync(
            string authenticatedActorFactionID,
            PlayerRequest playerRequest
        )
        {
            return _session.ExecuteCommandAsync(() =>
                ExecuteCore(authenticatedActorFactionID, playerRequest)
            );
        }

        /// <summary>
        /// Applies one request within the session-owned command boundary.
        /// </summary>
        /// <param name="authenticatedActorFactionID">The faction authorized to issue this request.</param>
        /// <param name="playerRequest">The typed request to apply.</param>
        /// <returns>The completed domain result.</returns>
        private PlayerRequestOutcome ExecuteCore(
            string authenticatedActorFactionID,
            PlayerRequest playerRequest
        )
        {
            Faction actor = _session
                .Game.GetFactions()
                .FirstOrDefault(faction =>
                    !string.IsNullOrEmpty(authenticatedActorFactionID)
                    && string.Equals(
                        faction.InstanceID,
                        authenticatedActorFactionID,
                        StringComparison.Ordinal
                    )
                );
            if (actor == null)
                return Reject("The issuing faction is unavailable.");

            return playerRequest switch
            {
                MoveRequest request => Move(actor, request),
                RelocateHeadquartersRequest request => RelocateHeadquarters(actor, request),
                SetFleetRouteRequest request => SetFleetRoute(actor, request),
                ClearFleetRouteRequest request => ClearFleetRoute(actor, request),
                ScrapRequest request => Scrap(actor, request),
                CancelManufacturingRequest request => CancelManufacturing(actor, request),
                RetirePersonnelRequest request => RetirePersonnel(actor, request),
                AbortMissionsRequest request => AbortMissions(actor, request),
                StartMissionRequest request => StartMission(actor, request),
                CreateFleetRequest request => CreateFleet(actor, request),
                BombardRequest request => Bombard(actor, request),
                AssaultRequest request => Assault(actor, request),
                SetOfficerRankRequest request => Complete(
                    _session
                        .GetService<PersonnelCommands>()
                        .TrySetRank(request.OfficerID, request.Rank, actor.InstanceID)
                ),
                RenameRequest request => Rename(actor, request),
                ToggleAdvisorOptionRequest request => ToggleAdvisorOption(actor, request),
                ReadMessagesRequest request => ReadMessages(actor, request),
                MarkAllMessagesReadRequest => MarkAllMessagesRead(actor),
                RemoveMessagesRequest request => RemoveMessages(actor, request),
                AcknowledgeMessagesRequest request => AcknowledgeMessages(actor, request),
                ToggleMessageNotificationRequest request => ToggleMessageNotification(
                    actor,
                    request
                ),
                ConsumeMissionContinuationRequest request => ConsumeMissionContinuation(
                    actor,
                    request
                ),
                ResolveCombatRequest request => ResolveCombat(actor, request),
                SetSpeedRequest request => SetSpeed(actor, request),
                RespondToSpeedRequest request => Complete(
                    _gameManager.TryRespondToGameSpeed(
                        actor.InstanceID,
                        request.RequestingFactionID,
                        request.Speed,
                        request.Revision,
                        request.Accept
                    )
                ),
                SendPeerChatRequest request => SendPeerChat(actor, request),
                SetManufacturingOrderRequest request => SetManufacturingOrder(actor, request),
                RetargetManufacturingRequest request => RetargetManufacturing(actor, request),
                ToggleManufacturingReservationRequest request => Complete(
                    IsManufacturingType(request.ManufacturingType)
                        && _session
                            .GetService<ManufacturingCommands>()
                            .ToggleManufacturingReservation(
                                FindNode<Planet>(request.ProducerPlanetID),
                                request.ManufacturingType,
                                actor.InstanceID
                            )
                ),
                _ => Reject("The player request is unsupported."),
            };
        }

        /// <summary>
        /// Resolves one active scene identity without accepting caller-supplied entity state.
        /// </summary>
        /// <typeparam name="T">The expected scene-node type.</typeparam>
        /// <param name="instanceID">The persistent scene identity.</param>
        /// <returns>The active registered node, or null when unavailable.</returns>
        private T FindNode<T>(string instanceID)
            where T : class, ISceneNode
        {
            return _session.Game.GetSceneNodeByInstanceID<T>(instanceID);
        }

        /// <summary>
        /// Resolves a complete unique selection before invoking its domain validation.
        /// </summary>
        /// <typeparam name="T">The required selected-node type.</typeparam>
        /// <param name="instanceIDs">The selected scene identities.</param>
        /// <param name="nodes">Receives the live selection in request order.</param>
        /// <returns>True when every identity names a distinct active node of the required type.</returns>
        private bool TryResolveSelection<T>(IReadOnlyList<string> instanceIDs, out List<T> nodes)
            where T : class, ISceneNode
        {
            nodes = new List<T>();
            if (instanceIDs == null || instanceIDs.Count == 0)
                return false;

            HashSet<string> seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (string instanceID in instanceIDs)
            {
                T node = FindNode<T>(instanceID);
                if (node == null || !seen.Add(node.InstanceID))
                    return false;
                nodes.Add(node);
            }
            return true;
        }

        /// <summary>
        /// Determines whether every selected entity belongs to the authenticated actor.
        /// </summary>
        /// <param name="actor">The issuing faction.</param>
        /// <param name="nodes">The live selection to verify.</param>
        /// <returns>True when the actor owns every selected node.</returns>
        private static bool OwnsSelection(Faction actor, IEnumerable<ISceneNode> nodes)
        {
            return nodes.All(node =>
                string.Equals(node.GetOwnerInstanceID(), actor.InstanceID, StringComparison.Ordinal)
            );
        }

        /// <summary>
        /// Creates the completed outcome for an existing domain operation's boolean return value.
        /// </summary>
        /// <param name="accepted">Whether the existing operation accepted the request.</param>
        /// <returns>The accepted outcome or an explicit rejection.</returns>
        private static PlayerRequestOutcome Complete(bool accepted)
        {
            return accepted
                ? new PlayerRequestOutcome(true)
                : Reject("The order is no longer available.");
        }

        /// <summary>
        /// Creates an explicit rejection without implying that a request executed successfully.
        /// </summary>
        /// <param name="reason">The reason for rejection.</param>
        /// <returns>The rejected outcome.</returns>
        private static PlayerRequestOutcome Reject(string reason)
        {
            return new PlayerRequestOutcome(false, rejectionReason: reason);
        }

        /// <summary>
        /// Determines whether a request identifies an existing production lane.
        /// </summary>
        /// <param name="type">The requested manufacturing category.</param>
        /// <returns>True for a defined production lane.</returns>
        private static bool IsManufacturingType(ManufacturingType type)
        {
            return type != ManufacturingType.None
                && Enum.IsDefined(typeof(ManufacturingType), type);
        }

        /// <summary>
        /// Renames an owned live fleet or ship while preserving explicit ship naming state.
        /// </summary>
        /// <param name="actor">The issuing faction.</param>
        /// <param name="request">The target identity and submitted name.</param>
        /// <returns>The completed rename outcome.</returns>
        private PlayerRequestOutcome Rename(Faction actor, RenameRequest request)
        {
            ISceneNode node = FindNode<ISceneNode>(request.EntityID);
            if (
                node is not Fleet and not CapitalShip
                || !OwnsSelection(actor, new[] { node })
                || string.IsNullOrEmpty(request.Name)
                || node is CapitalShip && string.IsNullOrWhiteSpace(request.Name)
            )
                return Reject("The selected entity cannot be renamed.");

            if (node is CapitalShip ship)
                ship.AssignName(request.Name);
            else
                node.DisplayName = request.Name;
            return new PlayerRequestOutcome(true);
        }

        /// <summary>
        /// Applies a supported simulation speed through the session clock.
        /// </summary>
        /// <param name="actor">The participant whose preference is changing.</param>
        /// <param name="request">The requested speed.</param>
        /// <returns>The completed speed-change outcome.</returns>
        private PlayerRequestOutcome SetSpeed(Faction actor, SetSpeedRequest request)
        {
            if (!Enum.IsDefined(typeof(TickSpeed), request.Speed))
                return Reject("The requested game speed is unsupported.");
            _gameManager.ApplyPlayerGameSpeed(actor.InstanceID, request.Speed);
            return new PlayerRequestOutcome(true);
        }

        /// <summary>
        /// Records a participating faction's response and resolves once every human side has responded.
        /// </summary>
        /// <param name="actor">The issuing faction.</param>
        /// <param name="request">The displayed encounter identity and decision.</param>
        /// <returns>An accepted response with an optional resolved battle, or an explicit rejection.</returns>
        private PlayerRequestOutcome ResolveCombat(Faction actor, ResolveCombatRequest request)
        {
            if (
                !_session
                    .GetService<SpaceCombatCommands>()
                    .TryGetPendingCombat(out PendingCombatResult pending)
                || pending.PlanetInstanceID != request.PlanetID
                || pending.Tick != request.Tick
                || pending.AttackerOwnerInstanceID != actor.InstanceID
                    && pending.DefenderOwnerInstanceID != actor.InstanceID
            )
                return Reject("The selected battle is no longer awaiting this decision.");

            return !_session.Tick.TrySubmitCombatResponse(
                actor.InstanceID,
                request.Retreat,
                out SpaceCombatResult result
            )
                ? Reject("The battle decision was unavailable.")
                : new PlayerRequestOutcome(true, spaceCombatResult: result);
        }
    }
}
