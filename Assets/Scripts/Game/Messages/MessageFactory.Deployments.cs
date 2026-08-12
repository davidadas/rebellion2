using System.Collections.Generic;
using System.Linq;
using Rebellion.Game.Factions;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Results;
using Rebellion.Game.Units;
using Rebellion.Presentation.Advisor;
using Rebellion.SceneGraph;

namespace Rebellion.Game.Messages
{
    /// <summary>
    /// Translates completed deployments and failed facility arrivals into unit reports.
    /// </summary>
    public partial class MessageFactory
    {
        private void AddDeploymentMessages(
            IEnumerable<GameObjectDeployedResult> results,
            GameRoot game,
            ICollection<MessageDelivery> deliveries
        )
        {
            GameObjectDeployedResult[] deploymentResults = (
                results ?? Enumerable.Empty<GameObjectDeployedResult>()
            )
                .Where(result => result?.GameObject is IManufacturable)
                .ToArray();

            foreach (
                GameObjectDeployedResult result in deploymentResults.Where(result =>
                    result.GameObject is not Regiment
                )
            )
            {
                IManufacturable unit = (IManufacturable)result.GameObject;
                ISceneNode node = unit as ISceneNode;
                Planet destination = node?.GetParentOfType<Planet>();
                Faction faction = GetDeploymentFaction(game, unit.GetOwnerInstanceID());
                Message message = unit is Building building
                    ? building.Movement == null
                        ? CreateFacilityMessage(faction, building, destination)
                        : null
                    : CreateUnit(faction, unit as IGameEntity, destination, game);
                _deliveryBuilder.Add(deliveries, faction, message, result);
            }

            var regimentItems = deploymentResults
                .Where(result => result.GameObject is Regiment)
                .Select(result =>
                {
                    Regiment regiment = (Regiment)result.GameObject;
                    Planet destination = regiment.GetParentOfType<Planet>();
                    Faction faction = GetDeploymentFaction(game, regiment.GetOwnerInstanceID());
                    return new
                    {
                        Regiment = regiment,
                        Result = result,
                        Destination = destination,
                        Faction = faction,
                        Definition = _definitionResolver.GetDefinition(
                            MessageResultType.RegimentDeployed,
                            gameObjectTypeId: regiment.TypeID
                        ),
                    };
                })
                .Where(item => item.Faction != null && item.Definition != null);
            foreach (
                var group in regimentItems.GroupBy(item =>
                    (
                        item.Faction.InstanceID,
                        DestinationInstanceID: item.Destination?.InstanceID,
                        item.Definition
                    )
                )
            )
            {
                var first = group.First();
                _deliveryBuilder.Add(
                    deliveries,
                    first.Faction,
                    CreateRegiments(
                        first.Faction,
                        group.Select(item => item.Regiment),
                        first.Destination,
                        first.Definition
                    ),
                    group.Select(item => (GameResult)item.Result).ToArray()
                );
            }
        }

        private void AddFacilityLossMessages(
            IEnumerable<GameObjectDestroyedOnArrivalResult> results,
            GameRoot game,
            ICollection<MessageDelivery> deliveries
        )
        {
            foreach (GameObjectDestroyedOnArrivalResult result in results)
            {
                if (result.DestroyedObject is not Building building)
                    continue;
                Planet destination = GetDeploymentPlanet(result.Context ?? result.Ref);
                Faction faction = GetDeploymentFaction(game, building.GetOwnerInstanceID());
                MessageDefinition definition = _definitionResolver.GetDefinition(
                    MessageResultType.FacilityLost
                );
                Message message = BuildDeploymentMessage(
                    definition,
                    faction,
                    new Dictionary<string, string>
                    {
                        { "item", building.GetDisplayName() ?? string.Empty },
                        { "system", destination?.GetDisplayName() ?? string.Empty },
                    }
                );
                SetDeploymentLocation(message, destination, destination);
                _deliveryBuilder.Add(deliveries, faction, message, result);
            }
        }

        public Message CreateFacilityMessage(Faction faction, Building building, Planet destination)
        {
            BuildingType buildingType = building?.BuildingType ?? BuildingType.None;
            if (buildingType == BuildingType.None)
                return null;
            MessageDefinition definition = _definitionResolver.GetDefinition(
                MessageResultType.FacilityDeployed,
                buildingType: buildingType
            );
            Message message = BuildDeploymentMessage(
                definition,
                faction,
                new Dictionary<string, string>
                {
                    { "item", building.GetDisplayName() ?? string.Empty },
                    { "system", destination?.GetDisplayName() ?? string.Empty },
                },
                imageOverride: building.MessageImagePath
            );
            SetDeploymentLocation(message, destination, building);
            return message;
        }

        private Message CreateUnit(
            Faction faction,
            IGameEntity unit,
            Planet destination,
            GameRoot game
        )
        {
            MessageResultType resultType = unit switch
            {
                CapitalShip ship when IsPlanetDestroying(ship, game) =>
                    MessageResultType.DeathStarDeployed,
                CapitalShip => MessageResultType.CapitalShipDeployed,
                Starfighter => MessageResultType.StarfighterDeployed,
                Regiment => MessageResultType.RegimentDeployed,
                _ => MessageResultType.None,
            };
            if (resultType == MessageResultType.None)
                return null;
            string itemName = unit.GetDisplayName() ?? string.Empty;
            MessageDefinition definition = _definitionResolver.GetDefinition(
                resultType,
                gameObjectTypeId: unit.TypeID
            );
            Message message = BuildDeploymentMessage(
                definition,
                faction,
                new Dictionary<string, string>
                {
                    { "item", itemName },
                    { "type", itemName },
                    { "system", destination?.GetDisplayName() ?? string.Empty },
                },
                imageOverride: unit.EncyclopediaImagePath
            );
            SetDeploymentLocation(message, destination, unit as ISceneNode);
            return message;
        }

        private Message CreateRegiments(
            Faction faction,
            IEnumerable<Regiment> regiments,
            Planet destination,
            MessageDefinition definition
        )
        {
            Regiment[] regimentArray = regiments?.Where(regiment => regiment != null).ToArray();
            if (regimentArray == null || regimentArray.Length == 0)
                return null;
            string firstName = regimentArray[0].GetDisplayName() ?? string.Empty;
            Message message = BuildDeploymentMessage(
                definition,
                faction,
                new Dictionary<string, string>
                {
                    { "item", firstName },
                    {
                        "items",
                        string.Join(
                            "\n",
                            regimentArray.Select(regiment => regiment.GetDisplayName())
                        )
                    },
                    { "system", destination?.GetDisplayName() ?? string.Empty },
                },
                imageOverride: regimentArray[0].EncyclopediaImagePath
            );
            SetDeploymentLocation(message, destination, regimentArray[0]);
            return message;
        }

        private Message BuildDeploymentMessage(
            MessageDefinition definition,
            Faction faction,
            Dictionary<string, string> values,
            string imageOverride = null
        )
        {
            Message message = _templateBuilder.Build(
                definition,
                faction,
                values,
                imageOverride: imageOverride
            );
            return _deliveryBuilder.WithNotification(
                message,
                AdvisorNotificationPolicy.GetDefault(definition?.ResultType)
            );
        }

        private static bool IsPlanetDestroying(CapitalShip ship, GameRoot game) =>
            ship != null
            && game?.Config?.Combat?.Bombardment?.PlanetDestroyingCapitalShipTypeIDs?.Contains(
                ship.TypeID
            ) == true;

        private static Planet GetDeploymentPlanet(IGameEntity entity) =>
            entity is Planet planet ? planet
            : entity is ISceneNode node
                ? node.GetParentOfType<Planet>() ?? node.GetLastParent() as Planet
            : null;

        private static void SetDeploymentLocation(Message message, Planet planet, ISceneNode target)
        {
            if (message == null)
                return;
            message.EventLocationInstanceID = planet?.InstanceID;
            message.NavigationTargetInstanceID = (target ?? planet)?.InstanceID;
        }

        private static Faction GetDeploymentFaction(GameRoot game, string instanceID) =>
            string.IsNullOrEmpty(instanceID)
                ? null
                : game.GetFactions().FirstOrDefault(faction => faction.InstanceID == instanceID);
    }
}
