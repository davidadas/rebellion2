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
    internal sealed class DeploymentMessageFactory
    {
        private readonly MessageDefinitionResolver _definitions;
        private readonly MessageTemplateBuilder _templates;
        private readonly MessageDeliveryBuilder _deliveries;

        public DeploymentMessageFactory(
            MessageDefinitionResolver definitions,
            MessageTemplateBuilder templates,
            MessageDeliveryBuilder deliveries
        )
        {
            _definitions = definitions;
            _templates = templates;
            _deliveries = deliveries;
        }

        public void AddDeploymentMessages(
            IEnumerable<GameObjectDeployedResult> results,
            GameRoot game,
            ICollection<MessageDelivery> deliveries
        )
        {
            IManufacturable[] units = (results ?? Enumerable.Empty<GameObjectDeployedResult>())
                .Select(result => result?.GameObject)
                .OfType<IManufacturable>()
                .ToArray();

            foreach (IManufacturable unit in units.Where(unit => unit is not Regiment))
            {
                ISceneNode node = unit as ISceneNode;
                Planet destination = node?.GetParentOfType<Planet>();
                Faction faction = GetFaction(game, unit.GetOwnerInstanceID());
                Message message = unit is Building building
                    ? building.Movement == null
                        ? CreateFacilityMessage(faction, building, destination)
                        : null
                    : CreateUnit(faction, unit as IGameEntity, destination, game);
                _deliveries.Add(deliveries, faction, message);
            }

            var regimentItems = units
                .OfType<Regiment>()
                .Select(regiment =>
                {
                    Planet destination = regiment.GetParentOfType<Planet>();
                    Faction faction = GetFaction(game, regiment.GetOwnerInstanceID());
                    return new
                    {
                        Regiment = regiment,
                        Destination = destination,
                        Faction = faction,
                        Definition = _definitions.GetDefinition(
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
                _deliveries.Add(
                    deliveries,
                    first.Faction,
                    CreateRegiments(
                        first.Faction,
                        group.Select(item => item.Regiment),
                        first.Destination,
                        first.Definition
                    )
                );
            }
        }

        public void AddFacilityLossMessages(
            IEnumerable<GameObjectDestroyedOnArrivalResult> results,
            GameRoot game,
            ICollection<MessageDelivery> deliveries
        )
        {
            foreach (GameObjectDestroyedOnArrivalResult result in results)
            {
                if (result.DestroyedObject is not Building building)
                    continue;
                Planet destination = GetPlanet(result.Context ?? result.Ref);
                Faction faction = GetFaction(game, building.GetOwnerInstanceID());
                MessageDefinition definition = _definitions.GetDefinition(
                    MessageResultType.FacilityLost
                );
                Message message = Build(
                    definition,
                    faction,
                    new Dictionary<string, string>
                    {
                        { "item", building.GetDisplayName() ?? string.Empty },
                        { "system", destination?.GetDisplayName() ?? string.Empty },
                    }
                );
                SetLocation(message, destination, destination);
                _deliveries.Add(deliveries, faction, message);
            }
        }

        public Message CreateFacilityMessage(Faction faction, Building building, Planet destination)
        {
            BuildingType buildingType = building?.BuildingType ?? BuildingType.None;
            if (buildingType == BuildingType.None)
                return null;
            MessageDefinition definition = _definitions.GetDefinition(
                MessageResultType.FacilityDeployed,
                buildingType: buildingType
            );
            Message message = Build(
                definition,
                faction,
                new Dictionary<string, string>
                {
                    { "item", building.GetDisplayName() ?? string.Empty },
                    { "system", destination?.GetDisplayName() ?? string.Empty },
                },
                imageOverride: building.MessageImagePath
            );
            SetLocation(message, destination, building);
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
            MessageDefinition definition = _definitions.GetDefinition(
                resultType,
                gameObjectTypeId: unit.TypeID
            );
            Message message = Build(
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
            SetLocation(message, destination, unit as ISceneNode);
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
            Message message = Build(
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
            SetLocation(message, destination, regimentArray[0]);
            return message;
        }

        private Message Build(
            MessageDefinition definition,
            Faction faction,
            Dictionary<string, string> values,
            string imageOverride = null
        )
        {
            Message message = _templates.Build(
                definition,
                faction,
                values,
                imageOverride: imageOverride
            );
            return _deliveries.WithNotification(
                message,
                AdvisorNotificationPolicy.GetDefault(definition?.ResultType)
            );
        }

        private static bool IsPlanetDestroying(CapitalShip ship, GameRoot game) =>
            ship != null
            && game?.Config?.Combat?.Bombardment?.PlanetDestroyingCapitalShipTypeIDs?.Contains(
                ship.TypeID
            ) == true;

        private static Planet GetPlanet(IGameEntity entity) =>
            entity is Planet planet ? planet
            : entity is ISceneNode node
                ? node.GetParentOfType<Planet>() ?? node.GetLastParent() as Planet
            : null;

        private static void SetLocation(Message message, Planet planet, ISceneNode target)
        {
            if (message == null)
                return;
            message.EventLocationInstanceID = planet?.InstanceID;
            message.NavigationTargetInstanceID = (target ?? planet)?.InstanceID;
        }

        private static Faction GetFaction(GameRoot game, string instanceID) =>
            string.IsNullOrEmpty(instanceID)
                ? null
                : game.GetFactions().FirstOrDefault(faction => faction.InstanceID == instanceID);
    }
}
