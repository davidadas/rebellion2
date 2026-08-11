using System.Collections.Generic;
using System.Linq;
using Rebellion.Game.Factions;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Results;
using Rebellion.Presentation.Advisor;
using Rebellion.SceneGraph;

namespace Rebellion.Game.Messages
{
    /// <summary>
    /// Translates completed ship repairs into faction message deliveries.
    /// </summary>
    internal sealed class RepairMessageFactory
    {
        private readonly MessageDefinitionResolver _definitions;
        private readonly MessageTemplateBuilder _templates;
        private readonly MessageDeliveryBuilder _deliveries;

        public RepairMessageFactory(
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
            IEnumerable<ShipHullDamageResult> shipResults,
            IEnumerable<FighterDamageResult> fighterResults,
            GameRoot game,
            ICollection<MessageDelivery> deliveries
        )
        {
            foreach (ShipHullDamageResult result in shipResults)
            {
                if (result?.Ship == null || result.Ship.IsDamaged())
                    continue;
                Add(deliveries, game, result.Ship, MessageResultType.CapitalShipRepaired);
            }

            foreach (FighterDamageResult result in fighterResults)
            {
                if (result?.Fighter == null || result.Fighter.HasLosses())
                    continue;
                Add(deliveries, game, result.Fighter, MessageResultType.StarfighterRepaired);
            }
        }

        private void Add(
            ICollection<MessageDelivery> deliveries,
            GameRoot game,
            ISceneNode unit,
            MessageResultType resultType
        )
        {
            Faction faction = game.GetFactions()
                .FirstOrDefault(candidate => candidate.InstanceID == unit.GetOwnerInstanceID());
            MessageDefinition definition = _definitions.GetDefinition(resultType);
            Message message = _templates.Build(
                definition,
                faction,
                new Dictionary<string, string>
                {
                    { "item", unit.GetDisplayName() ?? string.Empty },
                    { "attachment", unit.GetParent()?.GetDisplayName() ?? string.Empty },
                }
            );
            if (message != null)
            {
                message.EventLocationInstanceID = unit.GetParentOfType<Planet>()?.InstanceID;
                message.NavigationTargetInstanceID = unit.InstanceID;
            }
            _deliveries.WithNotification(
                message,
                AdvisorNotificationPolicy.GetDefault(definition?.ResultType)
            );
            _deliveries.Add(deliveries, faction, message);
        }
    }
}
