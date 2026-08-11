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
    internal sealed class BlockadeMessageFactory
    {
        private readonly MessageDefinitionResolver _definitions;
        private readonly MessageTemplateBuilder _templates;
        private readonly MessageDeliveryBuilder _deliveries;

        public BlockadeMessageFactory(
            MessageDefinitionResolver definitions,
            MessageTemplateBuilder templates,
            MessageDeliveryBuilder deliveries
        )
        {
            _definitions = definitions;
            _templates = templates;
            _deliveries = deliveries;
        }

        public void AddMessages(
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
                Faction blockadingFaction = GetFaction(
                    game,
                    result.BlockadingFleet?.GetOwnerInstanceID()
                );
                Faction targetFaction = GetFaction(game, result.Planet?.OwnerInstanceID);
                Add(
                    deliveries,
                    blockadingFaction,
                    result,
                    targetFaction,
                    MessageResultType.BlockadeInitiated
                );
                if (targetFaction?.InstanceID != blockadingFaction?.InstanceID)
                {
                    Add(
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
                MessageDefinition definition = _definitions.GetDefinition(
                    MessageResultType.EvacuationLosses
                );
                Message message = Build(
                    definition,
                    result.Faction,
                    new Dictionary<string, string>
                    {
                        { "system", result.Location?.GetDisplayName() ?? string.Empty },
                        { "units", FormatLostUnits(result) },
                    }
                );
                SetLocation(message, result.Location, result.Location);
                _deliveries.Add(deliveries, result.Faction, message);
            }
        }

        private void Add(
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

            MessageDefinition definition = _definitions.GetDefinition(resultType);
            Message message = Build(
                definition,
                recipient,
                values,
                resultType == MessageResultType.BlockadeInitiated ? otherFaction : null
            );
            SetLocation(message, result.Planet, result.BlockadingFleet);
            _deliveries.Add(deliveries, recipient, message);
        }

        private Message Build(
            MessageDefinition definition,
            Faction recipient,
            Dictionary<string, string> values,
            Faction imageFaction = null
        )
        {
            Message message = _templates.Build(definition, recipient, values, imageFaction);
            return _deliveries.WithNotification(
                message,
                AdvisorNotificationPolicy.GetDefault(definition?.ResultType)
            );
        }

        private static void SetLocation(Message message, ISceneNode planet, ISceneNode target)
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

        private static Faction GetFaction(GameRoot game, string instanceID) =>
            string.IsNullOrEmpty(instanceID)
                ? null
                : game.GetFactions().FirstOrDefault(faction => faction.InstanceID == instanceID);
    }
}
