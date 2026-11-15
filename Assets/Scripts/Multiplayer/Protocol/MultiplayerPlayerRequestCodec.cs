using System;
using System.IO;
using Rebellion.Game.PlayerRequests;

namespace Rebellion.Multiplayer
{
    /// <summary>
    /// Selects an explicit player-request schema before reading peer-controlled parameters.
    /// </summary>
    internal static class MultiplayerPlayerRequestCodec
    {
        /// <summary>
        /// Encodes one supported concrete request without serializing its actor or execution services.
        /// </summary>
        /// <param name="request">The request to encode.</param>
        /// <returns>The UTF-8 request parameters.</returns>
        public static byte[] Serialize(PlayerRequest request)
        {
            if (request == null)
                throw new ArgumentNullException(nameof(request));
            Type expected = GetRequestType(request.Kind);
            if (request.GetType() != expected)
                throw new InvalidDataException(
                    "The player request does not match its declared kind."
                );
            return MultiplayerSnapshotCodec.Serialize(expected, request);
        }

        /// <summary>
        /// Decodes only the schema selected by the admitted request discriminator.
        /// </summary>
        /// <param name="kind">The supported request discriminator.</param>
        /// <param name="payload">The UTF-8 request parameters.</param>
        /// <returns>The decoded request.</returns>
        public static PlayerRequest Deserialize(PlayerRequestKind kind, byte[] payload)
        {
            return (PlayerRequest)
                MultiplayerSnapshotCodec.Deserialize(GetRequestType(kind), payload);
        }

        /// <summary>
        /// Resolves the closed set of supported player request types without loading peer-supplied names.
        /// </summary>
        /// <param name="kind">The request discriminator.</param>
        /// <returns>The exact concrete request schema.</returns>
        private static Type GetRequestType(PlayerRequestKind kind)
        {
            return kind switch
            {
                PlayerRequestKind.Move => typeof(MoveRequest),
                PlayerRequestKind.RelocateHeadquarters => typeof(RelocateHeadquartersRequest),
                PlayerRequestKind.SetFleetRoute => typeof(SetFleetRouteRequest),
                PlayerRequestKind.ClearFleetRoute => typeof(ClearFleetRouteRequest),
                PlayerRequestKind.Scrap => typeof(ScrapRequest),
                PlayerRequestKind.CancelManufacturing => typeof(CancelManufacturingRequest),
                PlayerRequestKind.RetirePersonnel => typeof(RetirePersonnelRequest),
                PlayerRequestKind.AbortMissions => typeof(AbortMissionsRequest),
                PlayerRequestKind.StartMission => typeof(StartMissionRequest),
                PlayerRequestKind.CreateFleet => typeof(CreateFleetRequest),
                PlayerRequestKind.Bombard => typeof(BombardRequest),
                PlayerRequestKind.Assault => typeof(AssaultRequest),
                PlayerRequestKind.SetOfficerRank => typeof(SetOfficerRankRequest),
                PlayerRequestKind.Rename => typeof(RenameRequest),
                PlayerRequestKind.ToggleAdvisorOption => typeof(ToggleAdvisorOptionRequest),
                PlayerRequestKind.ReadMessages => typeof(ReadMessagesRequest),
                PlayerRequestKind.MarkAllMessagesRead => typeof(MarkAllMessagesReadRequest),
                PlayerRequestKind.RemoveMessages => typeof(RemoveMessagesRequest),
                PlayerRequestKind.AcknowledgeMessages => typeof(AcknowledgeMessagesRequest),
                PlayerRequestKind.ToggleMessageNotification =>
                    typeof(ToggleMessageNotificationRequest),
                PlayerRequestKind.ConsumeMissionContinuation =>
                    typeof(ConsumeMissionContinuationRequest),
                PlayerRequestKind.ResolveCombat => typeof(ResolveCombatRequest),
                PlayerRequestKind.SetSpeed => typeof(SetSpeedRequest),
                PlayerRequestKind.RespondToSpeed => typeof(RespondToSpeedRequest),
                PlayerRequestKind.SetManufacturingOrder => typeof(SetManufacturingOrderRequest),
                PlayerRequestKind.RetargetManufacturing => typeof(RetargetManufacturingRequest),
                PlayerRequestKind.ToggleManufacturingReservation =>
                    typeof(ToggleManufacturingReservationRequest),
                PlayerRequestKind.SendPeerChat => typeof(SendPeerChatRequest),
                _ => throw new InvalidDataException("The player request kind is unsupported."),
            };
        }
    }
}
