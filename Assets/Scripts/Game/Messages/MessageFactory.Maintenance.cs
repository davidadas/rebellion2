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
    public partial class MessageFactory
    {
        private void AddMaintenanceMessages(
            IEnumerable<GameObjectAutoscrappedResult> results,
            GameRoot game,
            ICollection<MessageDelivery> deliveries
        )
        {
            MessageDefinition definition = _definitionResolver.GetDefinition(
                MessageResultType.MaintenanceAutoscrap
            );
            var reportItems = (results ?? Enumerable.Empty<GameObjectAutoscrappedResult>())
                .Where(result => result != null)
                .Select(result =>
                {
                    Planet location = GetMaintenancePlanet(
                        result.Context ?? result.Ref ?? result.DestroyedObject
                    );
                    Faction faction =
                        GetOwner(game, result.DestroyedObject)
                        ?? GetOwner(game, result.Ref)
                        ?? GetMaintenanceFaction(game, location?.OwnerInstanceID);
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
                Message message = _templateBuilder.Build(
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
                _deliveryBuilder.WithNotification(
                    message,
                    AdvisorNotificationPolicy.GetDefault(definition.ResultType)
                );
                _deliveryBuilder.Add(
                    deliveries,
                    first.Faction,
                    message,
                    groupedResults.Cast<GameResult>().ToArray()
                );
            }
        }

        private static Planet GetMaintenancePlanet(IGameEntity entity)
        {
            if (entity is Planet planet)
                return planet;
            return entity is ISceneNode node
                ? node.GetParentOfType<Planet>() ?? node.GetLastParent() as Planet
                : null;
        }

        private static Faction GetOwner(GameRoot game, IGameEntity entity) =>
            entity is ISceneNode node
                ? GetMaintenanceFaction(game, node.GetOwnerInstanceID())
                : null;

        private static Faction GetMaintenanceFaction(GameRoot game, string instanceID) =>
            string.IsNullOrEmpty(instanceID)
                ? null
                : game.GetFactions().FirstOrDefault(faction => faction.InstanceID == instanceID);
    }
}
