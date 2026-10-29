using System;
using System.IO;
using Rebellion.App.PlayerActions;

namespace Rebellion.Multiplayer
{
    /// <summary>
    /// Selects an explicit player-request schema before reading peer-controlled parameters.
    /// </summary>
    internal static class MultiplayerPlayerActionCodec
    {
        /// <summary>
        /// Encodes one supported concrete request without serializing its actor or execution services.
        /// </summary>
        /// <param name="action">The request to encode.</param>
        /// <returns>The UTF-8 request parameters.</returns>
        public static byte[] Serialize(PlayerAction action)
        {
            if (action == null)
                throw new ArgumentNullException(nameof(action));
            Type expected = GetActionType(action.Kind);
            if (action.GetType() != expected)
                throw new InvalidDataException(
                    "The player request does not match its declared kind."
                );
            return MultiplayerSnapshotSerializer.SerializeValue(action, expected);
        }

        /// <summary>
        /// Decodes only the schema selected by the admitted request discriminator.
        /// </summary>
        /// <param name="kind">The supported request discriminator.</param>
        /// <param name="payload">The UTF-8 request parameters.</param>
        /// <returns>The decoded request.</returns>
        public static PlayerAction Deserialize(PlayerActionKind kind, byte[] payload)
        {
            return (PlayerAction)
                MultiplayerSnapshotSerializer.DeserializeValue(payload, GetActionType(kind));
        }

        /// <summary>
        /// Resolves the closed set of supported player request types without loading peer-supplied names.
        /// </summary>
        /// <param name="kind">The request discriminator.</param>
        /// <returns>The exact concrete request schema.</returns>
        private static Type GetActionType(PlayerActionKind kind)
        {
            return kind switch
            {
                PlayerActionKind.Move => typeof(MovePlayerAction),
                PlayerActionKind.RelocateHeadquarters => typeof(RelocateHeadquartersPlayerAction),
                PlayerActionKind.SetFleetRoute => typeof(SetFleetRoutePlayerAction),
                PlayerActionKind.ClearFleetRoute => typeof(ClearFleetRoutePlayerAction),
                PlayerActionKind.Scrap => typeof(ScrapPlayerAction),
                PlayerActionKind.CancelManufacturing => typeof(CancelManufacturingPlayerAction),
                PlayerActionKind.RetirePersonnel => typeof(RetirePersonnelPlayerAction),
                PlayerActionKind.AbortMissions => typeof(AbortMissionsPlayerAction),
                PlayerActionKind.StartMission => typeof(StartMissionPlayerAction),
                PlayerActionKind.CreateFleet => typeof(CreateFleetPlayerAction),
                PlayerActionKind.Bombard => typeof(BombardPlayerAction),
                PlayerActionKind.Assault => typeof(AssaultPlayerAction),
                PlayerActionKind.SetOfficerRank => typeof(SetOfficerRankPlayerAction),
                PlayerActionKind.Rename => typeof(RenamePlayerAction),
                PlayerActionKind.ToggleAdvisorOption => typeof(ToggleAdvisorOptionPlayerAction),
                PlayerActionKind.ReadMessages => typeof(ReadMessagesPlayerAction),
                PlayerActionKind.MarkAllMessagesRead => typeof(MarkAllMessagesReadPlayerAction),
                PlayerActionKind.RemoveMessages => typeof(RemoveMessagesPlayerAction),
                PlayerActionKind.AcknowledgeMessages => typeof(AcknowledgeMessagesPlayerAction),
                PlayerActionKind.ToggleMessageNotification =>
                    typeof(ToggleMessageNotificationPlayerAction),
                PlayerActionKind.ConsumeMissionContinuation =>
                    typeof(ConsumeMissionContinuationPlayerAction),
                PlayerActionKind.ResolveCombat => typeof(ResolveCombatPlayerAction),
                PlayerActionKind.SetSpeed => typeof(SetSpeedPlayerAction),
                PlayerActionKind.RespondToSpeed => typeof(RespondToSpeedPlayerAction),
                PlayerActionKind.SetManufacturingOrder => typeof(SetManufacturingOrderPlayerAction),
                PlayerActionKind.RetargetManufacturing => typeof(RetargetManufacturingPlayerAction),
                PlayerActionKind.ToggleManufacturingReservation =>
                    typeof(ToggleManufacturingReservationPlayerAction),
                PlayerActionKind.SendPeerChat => typeof(SendPeerChatPlayerAction),
                _ => throw new InvalidDataException("The player request kind is unsupported."),
            };
        }
    }
}
