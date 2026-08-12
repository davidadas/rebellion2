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
    public partial class MessageFactory
    {
        private void AddRepairMessages(
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
                AddRepairDelivery(
                    deliveries,
                    game,
                    result.Ship,
                    MessageResultType.CapitalShipRepaired,
                    result
                );
            }

            foreach (FighterDamageResult result in fighterResults)
            {
                if (result?.Fighter == null || result.Fighter.HasLosses())
                    continue;
                AddRepairDelivery(
                    deliveries,
                    game,
                    result.Fighter,
                    MessageResultType.StarfighterRepaired,
                    result
                );
            }
        }

        private void AddRepairDelivery(
            ICollection<MessageDelivery> deliveries,
            GameRoot game,
            ISceneNode unit,
            MessageResultType resultType,
            GameResult sourceResult
        )
        {
            Faction faction = game.GetFactions()
                .FirstOrDefault(candidate => candidate.InstanceID == unit.GetOwnerInstanceID());
            MessageDefinition definition = _definitionResolver.GetDefinition(resultType);
            Message message = _templateBuilder.Build(
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
            _deliveryBuilder.WithNotification(
                message,
                AdvisorNotificationPolicy.GetDefault(definition?.ResultType)
            );
            _deliveryBuilder.Add(deliveries, faction, message, sourceResult);
        }
    }
}
