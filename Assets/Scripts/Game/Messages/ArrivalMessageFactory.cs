using System;
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
    /// Groups completed movement arrivals and translates them into faction reports.
    /// </summary>
    internal sealed class ArrivalMessageFactory
    {
        private readonly MessageDefinitionResolver _definitions;
        private readonly MessageTemplateBuilder _templates;
        private readonly MessageDeliveryBuilder _deliveries;
        private readonly DeploymentMessageFactory _deployments;

        public ArrivalMessageFactory(
            MessageDefinitionResolver definitions,
            MessageTemplateBuilder templates,
            MessageDeliveryBuilder deliveries,
            DeploymentMessageFactory deployments
        )
        {
            _definitions = definitions;
            _templates = templates;
            _deliveries = deliveries;
            _deployments = deployments;
        }

        public void AddMessages(
            IEnumerable<UnitArrivedResult> arrivals,
            GameRoot game,
            ICollection<MessageDelivery> deliveries
        )
        {
            var shipGroups =
                new Dictionary<
                    (string Owner, string Destination, string Group),
                    List<CapitalShip>
                >();
            var shipDestinations =
                new Dictionary<(string Owner, string Destination, string Group), Planet>();
            var personnelGroups =
                new Dictionary<
                    (string Owner, string Destination, string Group),
                    List<IGameEntity>
                >();
            var personnelDestinations =
                new Dictionary<(string Owner, string Destination, string Group), Planet>();
            var unitGroups =
                new Dictionary<
                    (string Owner, string Destination, string Group),
                    List<IGameEntity>
                >();
            var unitDestinations =
                new Dictionary<(string Owner, string Destination, string Group), Planet>();

            foreach (UnitArrivedResult arrival in arrivals)
            {
                if (arrival.Unit is Fleet fleet)
                {
                    Faction faction = GetFaction(game, fleet.GetOwnerInstanceID());
                    Add(deliveries, faction, CreateFleet(faction, fleet, arrival.Destination));
                    continue;
                }
                if (arrival.Unit is CapitalShip ship)
                {
                    var key = Key(ship, arrival);
                    AddGroup(shipGroups, shipDestinations, key, ship, arrival.Destination);
                    continue;
                }
                if (arrival.Unit is Officer or SpecialForces)
                {
                    IGameEntity personnel = arrival.Unit;
                    var key = Key(personnel, arrival);
                    AddGroup(
                        personnelGroups,
                        personnelDestinations,
                        key,
                        personnel,
                        arrival.Destination
                    );
                    continue;
                }
                if (arrival.Unit is Regiment or Starfighter)
                {
                    IGameEntity unit = arrival.Unit;
                    var key = Key(unit, arrival);
                    AddGroup(unitGroups, unitDestinations, key, unit, arrival.Destination);
                    continue;
                }
                if (arrival.Unit is Building building)
                {
                    Faction faction = GetFaction(game, building.GetOwnerInstanceID());
                    Message message =
                        building.BuildingType == BuildingType.Headquarters
                            ? CreateHeadquarters(faction, building, arrival.Destination)
                            : _deployments.CreateFacilityMessage(
                                faction,
                                building,
                                arrival.Destination
                            );
                    Add(deliveries, faction, message);
                }
            }

            foreach (var group in shipGroups)
            {
                Faction faction = GetFaction(game, group.Key.Owner);
                Add(
                    deliveries,
                    faction,
                    CreateShips(faction, group.Value, shipDestinations[group.Key])
                );
            }
            foreach (var group in personnelGroups)
            {
                Faction faction = GetFaction(game, group.Key.Owner);
                Add(
                    deliveries,
                    faction,
                    CreatePersonnel(faction, group.Value, personnelDestinations[group.Key], game)
                );
            }
            foreach (var group in unitGroups)
            {
                Faction faction = GetFaction(game, group.Key.Owner);
                Add(
                    deliveries,
                    faction,
                    CreateUnits(faction, group.Value, unitDestinations[group.Key])
                );
            }
        }

        private Message CreateFleet(Faction faction, Fleet fleet, Planet destination)
        {
            Message message = Build(
                MessageResultType.FleetArrived,
                faction,
                new Dictionary<string, string>
                {
                    { "fleet", fleet?.GetDisplayName() ?? string.Empty },
                    { "system", destination?.GetDisplayName() ?? string.Empty },
                }
            );
            SetLocation(message, destination, fleet);
            return _deliveries.WithNotification(message, AdvisorNotificationType.FleetArrived);
        }

        private Message CreateShips(
            Faction faction,
            IEnumerable<CapitalShip> ships,
            Planet destination
        )
        {
            CapitalShip[] array =
                ships?.Where(ship => ship != null).ToArray() ?? Array.Empty<CapitalShip>();
            Message message = Build(
                MessageResultType.ShipsArrived,
                faction,
                new Dictionary<string, string>
                {
                    { "ships", string.Join("\n", array.Select(ship => ship.GetDisplayName())) },
                    { "system", destination?.GetDisplayName() ?? string.Empty },
                }
            );
            SetLocation(message, destination, array.FirstOrDefault());
            return _deliveries.WithNotification(message, AdvisorNotificationType.UnitsArrived);
        }

        private Message CreateUnits(
            Faction faction,
            IEnumerable<IGameEntity> units,
            Planet destination
        )
        {
            IGameEntity[] array = units?.Where(unit => unit != null).ToArray();
            if (array == null || array.Length == 0)
                return null;
            Message message = Build(
                MessageResultType.UnitsArrived,
                faction,
                new Dictionary<string, string>
                {
                    { "units", string.Join("\n", array.Select(unit => unit.GetDisplayName())) },
                    { "system", destination?.GetDisplayName() ?? string.Empty },
                }
            );
            SetLocation(message, destination, array[0] as ISceneNode);
            return _deliveries.WithNotification(message, AdvisorNotificationType.UnitsArrived);
        }

        private Message CreatePersonnel(
            Faction faction,
            IEnumerable<IGameEntity> personnel,
            Planet destination,
            GameRoot game
        )
        {
            IGameEntity[] array =
                personnel?.Where(unit => unit != null).ToArray() ?? Array.Empty<IGameEntity>();
            if (array.Length == 0)
                return null;
            Officer reporter = array
                .OfType<Officer>()
                .FirstOrDefault(officer =>
                    officer.HasVoicePath(OfficerVoiceLineType.PersonnelArrived)
                );
            IGameEntity[] listed =
                reporter == null ? array : array.Where(unit => unit != reporter).ToArray();
            MessageResultType resultType =
                reporter == null ? MessageResultType.PersonnelArrived
                : listed.Length == 0 ? MessageResultType.PersonnelArrivedByOfficer
                : MessageResultType.PersonnelArrivedByOfficerWithCompany;
            Message message = Build(
                resultType,
                faction,
                new Dictionary<string, string>
                {
                    { "officer", reporter?.GetDisplayName() ?? string.Empty },
                    { "system", destination?.GetDisplayName() ?? string.Empty },
                    {
                        "personnel",
                        string.Join("\n", listed.Select(unit => unit.GetDisplayName()))
                    },
                },
                overlayImagePath: (reporter ?? array[0]).MessageImagePath,
                officerVoicePath: reporter?.GetVoicePath(
                    OfficerVoiceLineType.PersonnelArrived,
                    game.Random
                )
            );
            SetLocation(message, destination, reporter ?? array[0] as ISceneNode);
            return reporter == null
                ? _deliveries.WithNotification(message, AdvisorNotificationType.FieldPersonnel)
                : _deliveries.WithSubject(message, AdvisorSubjectNotification.Report, reporter);
        }

        private Message CreateHeadquarters(
            Faction faction,
            Building headquarters,
            Planet destination
        )
        {
            MessageDefinition definition = _definitions.GetDefinition(
                MessageResultType.HeadquartersArrived,
                factionInstanceId: faction?.InstanceID
            );
            Message message = Build(
                definition,
                faction,
                new Dictionary<string, string>
                {
                    { "system", destination?.GetDisplayName() ?? string.Empty },
                },
                imageOverride: headquarters?.MessageImagePath
            );
            SetLocation(message, destination, headquarters);
            return _deliveries.WithNotification(message, AdvisorNotificationType.UnitsArrived);
        }

        private Message Build(
            MessageResultType resultType,
            Faction faction,
            Dictionary<string, string> values,
            string overlayImagePath = null,
            string officerVoicePath = null
        ) =>
            Build(
                _definitions.GetDefinition(resultType),
                faction,
                values,
                overlayImagePath: overlayImagePath,
                officerVoicePath: officerVoicePath
            );

        private Message Build(
            MessageDefinition definition,
            Faction faction,
            Dictionary<string, string> values,
            string imageOverride = null,
            string overlayImagePath = null,
            string officerVoicePath = null
        )
        {
            Message message = _templates.Build(
                definition,
                faction,
                values,
                imageOverride: imageOverride,
                overlayImagePath: overlayImagePath,
                officerVoicePath: officerVoicePath
            );
            return _deliveries.WithNotification(
                message,
                AdvisorNotificationPolicy.GetDefault(definition?.ResultType)
            );
        }

        private static (string Owner, string Destination, string Group) Key(
            IGameEntity unit,
            UnitArrivedResult arrival
        ) =>
            (
                (unit as ISceneNode)?.GetOwnerInstanceID(),
                arrival.Destination?.InstanceID,
                string.IsNullOrEmpty(arrival.MovementGroupID)
                    ? unit.InstanceID
                    : arrival.MovementGroupID
            );

        private static void AddGroup<T>(
            IDictionary<(string Owner, string Destination, string Group), List<T>> groups,
            IDictionary<(string Owner, string Destination, string Group), Planet> destinations,
            (string Owner, string Destination, string Group) key,
            T item,
            Planet destination
        )
        {
            if (!groups.TryGetValue(key, out List<T> items))
            {
                items = new List<T>();
                groups.Add(key, items);
                destinations.Add(key, destination);
            }
            items.Add(item);
        }

        private static void SetLocation(Message message, Planet planet, ISceneNode target)
        {
            if (message == null)
                return;
            message.EventLocationInstanceID = planet?.InstanceID;
            message.NavigationTargetInstanceID = (target ?? planet)?.InstanceID;
        }

        private void Add(
            ICollection<MessageDelivery> deliveries,
            Faction faction,
            Message message
        ) => _deliveries.Add(deliveries, faction, message);

        private static Faction GetFaction(GameRoot game, string ownerID) =>
            string.IsNullOrEmpty(ownerID)
                ? null
                : game.GetFactions().FirstOrDefault(faction => faction.InstanceID == ownerID);
    }
}
