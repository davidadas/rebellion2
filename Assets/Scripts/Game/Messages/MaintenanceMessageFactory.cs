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
    /// Groups maintenance losses and translates them into faction reports.
    /// </summary>
    internal sealed class MaintenanceMessageFactory
    {
        private readonly MessageDefinitionResolver _definitions;
        private readonly MessageTemplateBuilder _templates;
        private readonly MessageDeliveryBuilder _deliveries;

        public MaintenanceMessageFactory(
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
            IEnumerable<GameObjectAutoscrappedResult> results,
            GameRoot game,
            ICollection<MessageDelivery> deliveries
        )
        {
            MessageDefinition definition = _definitions.GetDefinition(
                MessageResultType.MaintenanceAutoscrap
            );
            var reportItems = (results ?? Enumerable.Empty<GameObjectAutoscrappedResult>())
                .Where(result => result != null)
                .Select(result =>
                {
                    Planet location = GetPlanet(
                        result.Context ?? result.Ref ?? result.DestroyedObject
                    );
                    Faction faction =
                        GetOwner(game, result.DestroyedObject)
                        ?? GetOwner(game, result.Ref)
                        ?? GetFaction(game, location?.OwnerInstanceID);
                    return new
                    {
                        Result = result,
                        Location = location,
                        Faction = faction,
                    };
                })
                .Where(item => item.Faction != null && definition != null);

            foreach (
                var group in reportItems.GroupBy(item =>
                    (item.Faction.InstanceID, LocationInstanceID: item.Location?.InstanceID)
                )
            )
            {
                var first = group.First();
                GameObjectAutoscrappedResult[] groupedResults = group
                    .Select(item => item.Result)
                    .ToArray();
                Message message = _templates.Build(
                    definition,
                    first.Faction,
                    new Dictionary<string, string>
                    {
                        {
                            "item",
                            groupedResults[0].DestroyedObject?.GetDisplayName() ?? string.Empty
                        },
                        {
                            "items",
                            string.Join(
                                "\n",
                                groupedResults.Select(result =>
                                    result.DestroyedObject?.GetDisplayName() ?? string.Empty
                                )
                            )
                        },
                        { "system", first.Location?.GetDisplayName() ?? string.Empty },
                    }
                );
                if (message != null)
                {
                    message.EventLocationInstanceID = first.Location?.InstanceID;
                    message.NavigationTargetInstanceID =
                        (groupedResults[0].DestroyedObject as ISceneNode)?.InstanceID
                        ?? first.Location?.InstanceID;
                }
                _deliveries.WithNotification(
                    message,
                    AdvisorNotificationPolicy.GetDefault(definition.ResultType)
                );
                _deliveries.Add(deliveries, first.Faction, message);
            }
        }

        private static Planet GetPlanet(IGameEntity entity)
        {
            if (entity is Planet planet)
                return planet;
            return entity is ISceneNode node
                ? node.GetParentOfType<Planet>() ?? node.GetLastParent() as Planet
                : null;
        }

        private static Faction GetOwner(GameRoot game, IGameEntity entity) =>
            entity is ISceneNode node ? GetFaction(game, node.GetOwnerInstanceID()) : null;

        private static Faction GetFaction(GameRoot game, string instanceID) =>
            string.IsNullOrEmpty(instanceID)
                ? null
                : game.GetFactions().FirstOrDefault(faction => faction.InstanceID == instanceID);
    }
}
