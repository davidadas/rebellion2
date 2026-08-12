using System.Collections.Generic;
using System.Linq;
using Rebellion.Game.Factions;
using Rebellion.Game.Results;
using Rebellion.Presentation.Advisor;
using Rebellion.SceneGraph;

namespace Rebellion.Game.Messages
{
    /// <summary>
    /// Translates blockade and evacuation results into faction reports.
    /// </summary>
    public partial class MessageFactory
    {
        private void AddBlockadeMessages(
            IEnumerable<BlockadeChangedResult> blockadeResults,
            IEnumerable<EvacuationLossesResult> evacuationResults,
            GameRoot game,
            ICollection<MessageDelivery> deliveries
        )
        {
            foreach (BlockadeChangedResult result in blockadeResults)
            {
                if (!result.Blockaded)
                    continue;
                Faction blockadingFaction = GetBlockadeFaction(
                    game,
                    result.BlockadingFleet?.GetOwnerInstanceID()
                );
                Faction targetFaction = GetBlockadeFaction(game, result.Planet?.OwnerInstanceID);
                AddBlockadeDelivery(
                    deliveries,
                    blockadingFaction,
                    result,
                    targetFaction,
                    MessageResultType.BlockadeInitiated
                );
                if (targetFaction?.InstanceID != blockadingFaction?.InstanceID)
                {
                    AddBlockadeDelivery(
                        deliveries,
                        targetFaction,
                        result,
                        blockadingFaction,
                        MessageResultType.BlockadeDetected
                    );
                }
            }

            foreach (EvacuationLossesResult result in evacuationResults)
            {
                if (result == null)
                    continue;
                MessageDefinition definition = _definitionResolver.GetDefinition(
                    MessageResultType.EvacuationLosses
                );
                Message message = BuildBlockadeMessage(
                    definition,
                    result.Faction,
                    new Dictionary<string, string>
                    {
                        { "system", result.Location?.GetDisplayName() ?? string.Empty },
                        { "units", FormatLostUnits(result) },
                    }
                );
                SetBlockadeLocation(message, result.Location, result.Location);
                _deliveryBuilder.Add(deliveries, result.Faction, message, result);
            }
        }

        private void AddBlockadeDelivery(
            ICollection<MessageDelivery> deliveries,
            Faction recipient,
            BlockadeChangedResult result,
            Faction otherFaction,
            MessageResultType resultType
        )
        {
            Dictionary<string, string> values = new Dictionary<string, string>
            {
                {
                    "faction",
                    (
                        resultType == MessageResultType.BlockadeInitiated ? recipient : otherFaction
                    )?.GetDisplayName() ?? string.Empty
                },
                { "fleet", result.BlockadingFleet?.GetDisplayName() ?? string.Empty },
                { "system", result.Planet?.GetDisplayName() ?? string.Empty },
            };
            if (resultType == MessageResultType.BlockadeInitiated)
                values["target"] = otherFaction?.GetDisplayName() ?? string.Empty;

            MessageDefinition definition = _definitionResolver.GetDefinition(resultType);
            Message message = BuildBlockadeMessage(
                definition,
                recipient,
                values,
                resultType == MessageResultType.BlockadeInitiated ? otherFaction : null
            );
            SetBlockadeLocation(message, result.Planet, result.BlockadingFleet);
            _deliveryBuilder.Add(deliveries, recipient, message, result);
        }

        private Message BuildBlockadeMessage(
            MessageDefinition definition,
            Faction recipient,
            Dictionary<string, string> values,
            Faction imageFaction = null
        )
        {
            Message message = _templateBuilder.Build(definition, recipient, values, imageFaction);
            return _deliveryBuilder.WithNotification(
                message,
                AdvisorNotificationPolicy.GetDefault(definition?.ResultType)
            );
        }

        private static void SetBlockadeLocation(
            Message message,
            ISceneNode planet,
            ISceneNode target
        )
        {
            if (message == null)
                return;
            message.EventLocationInstanceID = planet?.InstanceID;
            message.NavigationTargetInstanceID = (target ?? planet)?.InstanceID;
        }

        private static string FormatLostUnits(EvacuationLossesResult result)
        {
            IEnumerable<IGameEntity> units = result
                .LostShips.Cast<IGameEntity>()
                .Concat(result.LostStarfighters)
                .Concat(result.LostRegiments);
            return string.Join(
                "\n",
                units
                    .Select(unit => unit?.GetDisplayName() ?? string.Empty)
                    .Where(name => name.Length > 0)
            );
        }

        private static Faction GetBlockadeFaction(GameRoot game, string instanceID) =>
            string.IsNullOrEmpty(instanceID)
                ? null
                : game.GetFactions().FirstOrDefault(faction => faction.InstanceID == instanceID);
    }
}
