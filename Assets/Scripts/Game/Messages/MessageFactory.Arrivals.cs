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
    public partial class MessageFactory
    {
        private void AddArrivalMessages(
            IEnumerable<UnitArrivedResult> arrivals,
            GameRoot game,
            ICollection<MessageDelivery> deliveries
        )
        {
            UnitArrivedResult[] arrivalResults = arrivals.ToArray();
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

            foreach (UnitArrivedResult arrival in arrivalResults)
            {
                if (arrival.Unit is Fleet fleet)
                {
                    Faction faction = GetArrivalFaction(game, fleet.GetOwnerInstanceID());
                    AddArrivalDelivery(
                        deliveries,
                        faction,
                        CreateFleet(faction, fleet, arrival.Destination),
                        arrival
                    );
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
                    Faction faction = GetArrivalFaction(game, building.GetOwnerInstanceID());
                    Message message =
                        building.BuildingType == BuildingType.Headquarters
                            ? CreateHeadquarters(faction, building, arrival.Destination)
                            : this.CreateFacilityMessage(faction, building, arrival.Destination);
                    AddArrivalDelivery(deliveries, faction, message, arrival);
                }
            }

            foreach (var group in shipGroups)
            {
                Faction faction = GetArrivalFaction(game, group.Key.Owner);
                AddArrivalDelivery(
                    deliveries,
                    faction,
                    CreateShips(faction, group.Value, shipDestinations[group.Key]),
                    arrivalResults
                        .Where(result => group.Value.Contains(result.Unit as CapitalShip))
                        .Cast<GameResult>()
                        .ToArray()
                );
            }
            foreach (var group in personnelGroups)
            {
                Faction faction = GetArrivalFaction(game, group.Key.Owner);
                AddArrivalDelivery(
                    deliveries,
                    faction,
                    CreatePersonnel(faction, group.Value, personnelDestinations[group.Key], game),
                    arrivalResults
                        .Where(result => group.Value.Contains(result.Unit))
                        .Cast<GameResult>()
                        .ToArray()
                );
            }
            foreach (var group in unitGroups)
            {
                Faction faction = GetArrivalFaction(game, group.Key.Owner);
                AddArrivalDelivery(
                    deliveries,
                    faction,
                    CreateUnits(faction, group.Value, unitDestinations[group.Key]),
                    arrivalResults
                        .Where(result => group.Value.Contains(result.Unit))
                        .Cast<GameResult>()
                        .ToArray()
                );
            }
        }

        private Message CreateFleet(Faction faction, Fleet fleet, Planet destination)
        {
            Message message = BuildArrivalMessage(
                MessageResultType.FleetArrived,
                faction,
                new Dictionary<string, string>
                {
                    { "fleet", fleet?.GetDisplayName() ?? string.Empty },
                    { "system", destination?.GetDisplayName() ?? string.Empty },
                }
            );
            SetArrivalLocation(message, destination, fleet);
            return _deliveryBuilder.WithNotification(message, AdvisorNotificationType.FleetArrived);
        }

        private Message CreateShips(
            Faction faction,
            IEnumerable<CapitalShip> ships,
            Planet destination
        )
        {
            CapitalShip[] array =
                ships?.Where(ship => ship != null).ToArray() ?? Array.Empty<CapitalShip>();
            Message message = BuildArrivalMessage(
                MessageResultType.ShipsArrived,
                faction,
                new Dictionary<string, string>
                {
                    { "ships", string.Join("\n", array.Select(ship => ship.GetDisplayName())) },
                    { "system", destination?.GetDisplayName() ?? string.Empty },
                }
            );
            SetArrivalLocation(message, destination, array.FirstOrDefault());
            return _deliveryBuilder.WithNotification(message, AdvisorNotificationType.UnitsArrived);
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
            Message message = BuildArrivalMessage(
                MessageResultType.UnitsArrived,
                faction,
                new Dictionary<string, string>
                {
                    { "units", string.Join("\n", array.Select(unit => unit.GetDisplayName())) },
                    { "system", destination?.GetDisplayName() ?? string.Empty },
                }
            );
            SetArrivalLocation(message, destination, array[0] as ISceneNode);
            return _deliveryBuilder.WithNotification(message, AdvisorNotificationType.UnitsArrived);
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
            Message message = BuildArrivalMessage(
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
            SetArrivalLocation(message, destination, reporter ?? array[0] as ISceneNode);
            return reporter == null
                ? _deliveryBuilder.WithNotification(message, AdvisorNotificationType.FieldPersonnel)
                : _deliveryBuilder.WithSubject(
                    message,
                    AdvisorSubjectNotification.Report,
                    reporter
                );
        }

        private Message CreateHeadquarters(
            Faction faction,
            Building headquarters,
            Planet destination
        )
        {
            MessageDefinition definition = _definitionResolver.GetDefinition(
                MessageResultType.HeadquartersArrived,
                factionInstanceId: faction?.InstanceID
            );
            Message message = BuildArrivalMessage(
                definition,
                faction,
                new Dictionary<string, string>
                {
                    { "system", destination?.GetDisplayName() ?? string.Empty },
                },
                imageOverride: headquarters?.MessageImagePath
            );
            SetArrivalLocation(message, destination, headquarters);
            return _deliveryBuilder.WithNotification(message, AdvisorNotificationType.UnitsArrived);
        }

        private Message BuildArrivalMessage(
            MessageResultType resultType,
            Faction faction,
            Dictionary<string, string> values,
            string overlayImagePath = null,
            string officerVoicePath = null
        ) =>
            BuildArrivalMessage(
                _definitionResolver.GetDefinition(resultType),
                faction,
                values,
                overlayImagePath: overlayImagePath,
                officerVoicePath: officerVoicePath
            );

        private Message BuildArrivalMessage(
            MessageDefinition definition,
            Faction faction,
            Dictionary<string, string> values,
            string imageOverride = null,
            string overlayImagePath = null,
            string officerVoicePath = null
        )
        {
            Message message = _templateBuilder.Build(
                definition,
                faction,
                values,
                imageOverride: imageOverride,
                overlayImagePath: overlayImagePath,
                officerVoicePath: officerVoicePath
            );
            return _deliveryBuilder.WithNotification(
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

        private static void SetArrivalLocation(Message message, Planet planet, ISceneNode target)
        {
            if (message == null)
                return;
            message.EventLocationInstanceID = planet?.InstanceID;
            message.NavigationTargetInstanceID = (target ?? planet)?.InstanceID;
        }

        private void AddArrivalDelivery(
            ICollection<MessageDelivery> deliveries,
            Faction faction,
            Message message,
            params GameResult[] sourceResults
        ) => _deliveryBuilder.Add(deliveries, faction, message, sourceResults);

        private static Faction GetArrivalFaction(GameRoot game, string ownerID) =>
            string.IsNullOrEmpty(ownerID)
                ? null
                : game.GetFactions().FirstOrDefault(faction => faction.InstanceID == ownerID);
    }
}
