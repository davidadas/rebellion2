using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Rebellion.Game;
using Rebellion.Game.Factions;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Results;
using Rebellion.Game.Units;
using Rebellion.SceneGraph;
using Rebellion.Simulation;

namespace Rebellion.App.PlayerActions
{
    /// <summary>
    /// Executes explicit player requests through the active session's existing domain operations.
    /// The caller owns ordering and tick boundaries and supplies the authenticated actor separately.
    /// </summary>
    public sealed partial class PlayerActionExecutor
    {
        private readonly GameSession _session;
        private readonly GameManager _gameManager;

        /// <summary>
        /// Binds player action execution to one session and its clock.
        /// </summary>
        /// <param name="session">The authoritative session receiving requests.</param>
        /// <param name="gameManager">The clock controlling that session.</param>
        public PlayerActionExecutor(GameSession session, GameManager gameManager)
        {
            _session = session ?? throw new ArgumentNullException(nameof(session));
            _gameManager = gameManager ?? throw new ArgumentNullException(nameof(gameManager));
        }

        /// <summary>
        /// Executes a request using the actor established by the caller's player binding.
        /// </summary>
        /// <param name="authenticatedActorFactionID">The faction authorized to issue the request.</param>
        /// <param name="action">The typed request to execute.</param>
        /// <returns>The completed domain outcome; delivering or queuing a request is not acceptance.</returns>
        public PlayerActionResult Execute(string authenticatedActorFactionID, PlayerAction action)
        {
            if (!_session.CanExecuteCommands)
                return Reject("The simulation is not at a writable command boundary.");
            return ExecuteAsync(authenticatedActorFactionID, action).GetAwaiter().GetResult();
        }

        /// <summary>
        /// Defers a local request until this session can execute it and report its real outcome.
        /// </summary>
        /// <param name="authenticatedActorFactionID">The faction authorized at submission.</param>
        /// <param name="action">The typed request to execute.</param>
        /// <returns>The completed result, cancellation on replacement, or execution failure.</returns>
        public Task<PlayerActionResult> ExecuteAsync(
            string authenticatedActorFactionID,
            PlayerAction action
        )
        {
            return _session.ExecuteCommandAsync(() =>
                ExecuteCore(authenticatedActorFactionID, action)
            );
        }

        /// <summary>
        /// Applies one request within the session-owned command boundary.
        /// </summary>
        /// <param name="authenticatedActorFactionID">The faction authorized to issue this request.</param>
        /// <param name="action">The typed request to apply.</param>
        /// <returns>The completed domain result.</returns>
        private PlayerActionResult ExecuteCore(
            string authenticatedActorFactionID,
            PlayerAction action
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

            return action switch
            {
                MovePlayerAction request => Move(actor, request),
                RelocateHeadquartersPlayerAction request => RelocateHeadquarters(actor, request),
                SetFleetRoutePlayerAction request => SetFleetRoute(actor, request),
                ClearFleetRoutePlayerAction request => ClearFleetRoute(actor, request),
                ScrapPlayerAction request => Scrap(actor, request),
                CancelManufacturingPlayerAction request => CancelManufacturing(actor, request),
                RetirePersonnelPlayerAction request => RetirePersonnel(actor, request),
                AbortMissionsPlayerAction request => AbortMissions(actor, request),
                StartMissionPlayerAction request => StartMission(actor, request),
                CreateFleetPlayerAction request => CreateFleet(actor, request),
                BombardPlayerAction request => Bombard(actor, request),
                AssaultPlayerAction request => Assault(actor, request),
                SetOfficerRankPlayerAction request => Complete(
                    _session
                        .GetService<OfficerCommandCommands>()
                        .TrySetRank(request.OfficerID, request.Rank, actor.InstanceID)
                ),
                RenamePlayerAction request => Rename(actor, request),
                ToggleAdvisorOptionPlayerAction request => ToggleAdvisorOption(actor, request),
                ReadMessagesPlayerAction request => ReadMessages(actor, request),
                MarkAllMessagesReadPlayerAction => MarkAllMessagesRead(actor),
                RemoveMessagesPlayerAction request => RemoveMessages(actor, request),
                AcknowledgeMessagesPlayerAction request => AcknowledgeMessages(actor, request),
                ToggleMessageNotificationPlayerAction request => ToggleMessageNotification(
                    actor,
                    request
                ),
                ConsumeMissionContinuationPlayerAction request => ConsumeMissionContinuation(
                    actor,
                    request
                ),
                ResolveCombatPlayerAction request => ResolveCombat(actor, request),
                SetSpeedPlayerAction request => SetSpeed(actor, request),
                RespondToSpeedPlayerAction request => Complete(
                    _gameManager.TryRespondToGameSpeed(
                        actor.InstanceID,
                        request.RequestingFactionID,
                        request.Speed,
                        request.Revision,
                        request.Accept
                    )
                ),
                SendPeerChatPlayerAction request => SendPeerChat(actor, request),
                SetManufacturingOrderPlayerAction request => SetManufacturingOrder(actor, request),
                RetargetManufacturingPlayerAction request => RetargetManufacturing(actor, request),
                ToggleManufacturingReservationPlayerAction request => Complete(
                    IsManufacturingType(request.ManufacturingType)
                        && _session
                            .GetService<ManufacturingCommands>()
                            .ToggleManufacturingReservation(
                                FindNode<Planet>(request.ProducerPlanetID),
                                request.ManufacturingType,
                                actor.InstanceID
                            )
                ),
                _ => Reject("The player action is unsupported."),
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
        private static PlayerActionResult Complete(bool accepted)
        {
            return accepted
                ? new PlayerActionResult(true)
                : Reject("The order is no longer available.");
        }

        /// <summary>
        /// Creates an explicit rejection without implying that a request executed successfully.
        /// </summary>
        /// <param name="reason">The reason for rejection.</param>
        /// <returns>The rejected outcome.</returns>
        private static PlayerActionResult Reject(string reason)
        {
            return new PlayerActionResult(false, rejectionReason: reason);
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
        private PlayerActionResult Rename(Faction actor, RenamePlayerAction request)
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
            return new PlayerActionResult(true);
        }

        /// <summary>
        /// Applies a supported simulation speed through the session clock.
        /// </summary>
        /// <param name="actor">The participant whose preference is changing.</param>
        /// <param name="request">The requested speed.</param>
        /// <returns>The completed speed-change outcome.</returns>
        private PlayerActionResult SetSpeed(Faction actor, SetSpeedPlayerAction request)
        {
            if (!Enum.IsDefined(typeof(TickSpeed), request.Speed))
                return Reject("The requested game speed is unsupported.");
            _gameManager.ApplyPlayerGameSpeed(actor.InstanceID, request.Speed);
            return new PlayerActionResult(true);
        }

        /// <summary>
        /// Records a participating faction's response and resolves once every human side has responded.
        /// </summary>
        /// <param name="actor">The issuing faction.</param>
        /// <param name="request">The displayed encounter identity and decision.</param>
        /// <returns>An accepted response with an optional resolved battle, or an explicit rejection.</returns>
        private PlayerActionResult ResolveCombat(Faction actor, ResolveCombatPlayerAction request)
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
                : new PlayerActionResult(true, spaceCombatResult: result);
        }
    }
}
