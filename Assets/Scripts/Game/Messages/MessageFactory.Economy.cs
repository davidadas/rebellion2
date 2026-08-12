using System.Collections.Generic;
using Rebellion.Game.Factions;
using Rebellion.Game.Results;
using Rebellion.Game.Units;
using Rebellion.Presentation.Advisor;

namespace Rebellion.Game.Messages
{
    /// <summary>
    /// Translates economic simulation results into faction message deliveries.
    /// </summary>
    public partial class MessageFactory
    {
        private void AddSmugglingMessages(
            IEnumerable<SmugglingChangedResult> results,
            ICollection<MessageDelivery> deliveries
        )
        {
            foreach (SmugglingChangedResult result in results)
            {
                AddSmugglingDelivery(deliveries, result.Controller, result, false);
                AddSmugglingDelivery(deliveries, result.Beneficiary, result, true);
            }
        }

        private void AddManufacturingMessages(
            IEnumerable<ManufacturingIdleResult> results,
            ICollection<MessageDelivery> deliveries
        )
        {
            foreach (ManufacturingIdleResult result in results)
            {
                if (result.ManufacturingType == ManufacturingType.None)
                    continue;

                Message message = BuildEconomyMessage(
                    _definitionResolver.GetDefinition(
                        MessageResultType.ManufacturingIdle,
                        manufacturingType: result.ManufacturingType
                    ),
                    result.Faction,
                    new Dictionary<string, string>
                    {
                        { "system", result.ProductionPlanet?.GetDisplayName() ?? string.Empty },
                    }
                );
                if (message != null)
                {
                    message.EventLocationInstanceID = result.ProductionPlanet?.InstanceID;
                    message.NavigationTargetInstanceID = result.ProductionPlanet?.InstanceID;
                }
                _deliveryBuilder.WithNotification(message, AdvisorNotificationType.Manufacturing);
                _deliveryBuilder.Add(deliveries, result.Faction, message, result);
            }
        }

        private void AddSmugglingDelivery(
            ICollection<MessageDelivery> deliveries,
            Faction recipient,
            SmugglingChangedResult result,
            bool receivesBenefits
        )
        {
            bool active = result.NewPercent != 0;
            MessageResultType resultType = (receivesBenefits, active) switch
            {
                (false, true) => MessageResultType.SmugglingLosses,
                (false, false) => MessageResultType.SmugglingLossesEnded,
                (true, true) => MessageResultType.SmugglingBenefits,
                _ => MessageResultType.SmugglingBenefitsEnded,
            };
            Message message = BuildEconomyMessage(
                _definitionResolver.GetDefinition(resultType),
                recipient,
                new Dictionary<string, string>
                {
                    { "system", result.Planet?.GetDisplayName() ?? string.Empty },
                }
            );
            if (message != null)
            {
                message.EventLocationInstanceID = result.Planet?.InstanceID;
                message.NavigationTargetInstanceID = result.Planet?.InstanceID;
            }
            _deliveryBuilder.Add(deliveries, recipient, message, result);
        }

        private Message BuildEconomyMessage(
            MessageDefinition definition,
            Faction faction,
            Dictionary<string, string> values
        )
        {
            Message message = _templateBuilder.Build(definition, faction, values);
            return _deliveryBuilder.WithNotification(
                message,
                AdvisorNotificationPolicy.GetDefault(definition?.ResultType)
            );
        }
    }
}
