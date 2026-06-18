using System;
using System.Collections.Generic;
using System.Linq;
using Rebellion.Game.Factions;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Research;
using Rebellion.Game.Results;
using Rebellion.Game.Units;
using Rebellion.SceneGraph;
using Rebellion.Systems;

namespace Rebellion.Game.Messages
{
    public class MessageFactory
    {
        private readonly MessageDefinition[] _definitions;

        public MessageFactory(IEnumerable<MessageDefinition> definitions)
        {
            _definitions = definitions?.ToArray() ?? Array.Empty<MessageDefinition>();
        }

        public List<MessageDelivery> CreateMessages(IEnumerable<GameResult> results, GameRoot game)
        {
            GameResult[] resultArray = results?.ToArray() ?? Array.Empty<GameResult>();
            List<MessageDelivery> deliveries = new List<MessageDelivery>();

            AddArrivalMessages(resultArray.OfType<UnitArrivedResult>(), game, deliveries);
            AddMissionMessages(resultArray.OfType<MissionCompletedResult>(), game, deliveries);
            AddSabotageMessages(resultArray.OfType<GameObjectSabotagedResult>(), game, deliveries);
            AddResearchMessages(
                resultArray.OfType<ResearchOrderedResult>(),
                resultArray.OfType<ResearchExhaustedResult>(),
                deliveries
            );
            AddUprisingMessages(
                resultArray.OfType<PlanetUprisingStartedResult>(),
                resultArray.OfType<PlanetUprisingEndedResult>(),
                game,
                deliveries
            );
            AddBlockadeMessages(
                resultArray.OfType<BlockadeChangedResult>(),
                resultArray.OfType<EvacuationLossesResult>(),
                game,
                deliveries
            );
            AddMaintenanceMessages(
                resultArray.OfType<GameObjectAutoscrappedResult>(),
                game,
                deliveries
            );
            AddCombatMessages(
                resultArray.OfType<SpaceCombatResult>(),
                resultArray.OfType<BombardmentResult>(),
                resultArray.OfType<PlanetaryAssaultResult>(),
                game,
                deliveries
            );
            AddDeploymentMessages(resultArray.OfType<GameObjectDeployedResult>(), game, deliveries);
            AddManufacturingMessages(
                resultArray.OfType<ManufacturingCompletedResult>(),
                deliveries
            );
            AddSeatOfPowerMessages(
                resultArray.OfType<SeatOfPowerChangedResult>(),
                game,
                deliveries
            );

            return deliveries;
        }

        public Message CreateFleetArrived(Faction faction, Fleet fleet, Planet destination)
        {
            return CreateMessage(
                GetDefinition(MessageResultType.FleetArrived),
                faction,
                new Dictionary<string, string>
                {
                    { "fleet", fleet?.GetDisplayName() ?? string.Empty },
                    { "system", destination?.GetDisplayName() ?? string.Empty },
                }
            );
        }

        public Message CreateShipsArrived(
            Faction faction,
            IEnumerable<CapitalShip> ships,
            Planet destination
        )
        {
            string shipList = string.Join(
                "\n",
                (ships ?? Enumerable.Empty<CapitalShip>()).Select(ship => ship.GetDisplayName())
            );
            return CreateMessage(
                GetDefinition(MessageResultType.ShipsArrived),
                faction,
                new Dictionary<string, string>
                {
                    { "ships", shipList },
                    { "system", destination?.GetDisplayName() ?? string.Empty },
                }
            );
        }

        public Message CreateEmperorSeatOfPower(Faction faction)
        {
            return CreateMessage(
                GetDefinition(MessageResultType.EmperorSeatOfPower),
                faction,
                new Dictionary<string, string>()
            );
        }

        public Message CreateFacilityDeployed(
            Faction faction,
            Building building,
            Planet destination
        )
        {
            BuildingType buildingType = building?.BuildingType ?? BuildingType.None;
            if (buildingType == BuildingType.None)
                return null;

            return CreateMessage(
                GetDefinition(MessageResultType.FacilityDeployed, buildingType: buildingType),
                faction,
                new Dictionary<string, string>
                {
                    { "item", building?.GetDisplayName() ?? string.Empty },
                    { "system", destination?.GetDisplayName() ?? string.Empty },
                }
            );
        }

        public Message CreateManufacturingIdle(
            Faction faction,
            ManufacturingType manufacturingType,
            Planet planet
        )
        {
            if (manufacturingType == ManufacturingType.None)
                return null;

            return CreateMessage(
                GetDefinition(
                    MessageResultType.ManufacturingIdle,
                    manufacturingType: manufacturingType
                ),
                faction,
                new Dictionary<string, string>
                {
                    { "system", planet?.GetDisplayName() ?? string.Empty },
                }
            );
        }

        public Message CreateMissionReport(
            Faction faction,
            MissionCompletedResult result,
            Planet target
        )
        {
            if (result == null)
                return null;

            MessageResultOutcome outcome =
                result.Outcome == MissionOutcome.Success
                    ? MessageResultOutcome.Success
                    : MessageResultOutcome.Failed;

            return CreateMessage(
                GetDefinition(MessageResultType.MissionReport, outcome),
                faction,
                new Dictionary<string, string>
                {
                    { "mission", GetMissionName(result) },
                    { "system", GetTargetName(result, target) },
                }
            );
        }

        public Message CreateEnemyMissionFoiled(
            Faction faction,
            MissionCompletedResult result,
            Planet target
        )
        {
            if (result == null || result.Outcome != MissionOutcome.Foiled)
                return null;

            return CreateMessage(
                GetDefinition(MessageResultType.EnemyMissionFoiled, MessageResultOutcome.Foiled),
                faction,
                new Dictionary<string, string>
                {
                    { "mission", GetMissionName(result) },
                    { "system", GetTargetName(result, target) },
                }
            );
        }

        public Message CreateSabotageStrike(
            Faction faction,
            GameObjectSabotagedResult result,
            Planet target
        )
        {
            if (result == null)
                return null;

            return CreateMessage(
                GetDefinition(MessageResultType.SabotageStrike),
                faction,
                new Dictionary<string, string>
                {
                    { "item", GetDisplayName(result.SabotagedObject) },
                    { "system", target?.GetDisplayName() ?? string.Empty },
                }
            );
        }

        public Message CreateResearchComplete(Faction faction, ResearchOrderedResult result)
        {
            if (result?.Technology == null)
                return null;

            string item = GetDisplayName(result.Technology.GetReference());
            return CreateMessage(
                GetDefinition(MessageResultType.ResearchComplete, discipline: result.Discipline),
                faction,
                new Dictionary<string, string> { { "item", item } }
            );
        }

        public Message CreateResearchExhausted(Faction faction, ResearchExhaustedResult result)
        {
            if (result == null)
                return null;

            return CreateMessage(
                GetDefinition(MessageResultType.ResearchExhausted, discipline: result.Discipline),
                faction,
                new Dictionary<string, string>()
            );
        }

        public Message CreateUprisingStarted(
            Faction faction,
            PlanetUprisingStartedResult result,
            Faction controller
        )
        {
            if (result == null)
                return null;

            return CreateMessage(
                GetDefinition(MessageResultType.UprisingStarted),
                faction,
                new Dictionary<string, string>
                {
                    { "faction", controller?.GetDisplayName() ?? string.Empty },
                    { "system", result.Planet?.GetDisplayName() ?? string.Empty },
                }
            );
        }

        public Message CreateUprisingEnded(
            Faction faction,
            PlanetUprisingEndedResult result,
            Faction controller
        )
        {
            if (result == null)
                return null;

            return CreateMessage(
                GetDefinition(MessageResultType.UprisingEnded),
                faction,
                new Dictionary<string, string>
                {
                    { "faction", controller?.GetDisplayName() ?? string.Empty },
                    { "system", result.Planet?.GetDisplayName() ?? string.Empty },
                },
                controller
            );
        }

        public Message CreateBlockadeInitiated(
            Faction faction,
            BlockadeChangedResult result,
            Faction targetFaction
        )
        {
            if (result?.Blockaded != true)
                return null;

            return CreateMessage(
                GetDefinition(MessageResultType.BlockadeInitiated),
                faction,
                new Dictionary<string, string>
                {
                    { "faction", faction?.GetDisplayName() ?? string.Empty },
                    { "target", targetFaction?.GetDisplayName() ?? string.Empty },
                    { "fleet", result.BlockadingFleet?.GetDisplayName() ?? string.Empty },
                    { "system", result.Planet?.GetDisplayName() ?? string.Empty },
                },
                targetFaction
            );
        }

        public Message CreateBlockadeDetected(
            Faction faction,
            BlockadeChangedResult result,
            Faction blockadingFaction
        )
        {
            if (result?.Blockaded != true)
                return null;

            return CreateMessage(
                GetDefinition(MessageResultType.BlockadeDetected),
                faction,
                new Dictionary<string, string>
                {
                    { "faction", blockadingFaction?.GetDisplayName() ?? string.Empty },
                    { "fleet", result.BlockadingFleet?.GetDisplayName() ?? string.Empty },
                    { "system", result.Planet?.GetDisplayName() ?? string.Empty },
                }
            );
        }

        public Message CreateEvacuationLosses(Faction faction, EvacuationLossesResult result)
        {
            if (result == null)
                return null;

            return CreateMessage(
                GetDefinition(MessageResultType.EvacuationLosses),
                faction,
                new Dictionary<string, string>
                {
                    { "system", result.Location?.GetDisplayName() ?? string.Empty },
                    { "units", FormatLostUnits(result) },
                }
            );
        }

        public Message CreateMaintenanceAutoscrap(
            Faction faction,
            GameObjectAutoscrappedResult result,
            Planet location
        )
        {
            if (result == null)
                return null;

            return CreateMessage(
                GetDefinition(MessageResultType.MaintenanceAutoscrap),
                faction,
                new Dictionary<string, string>
                {
                    { "item", GetDisplayName(result.DestroyedObject) },
                    { "system", location?.GetDisplayName() ?? string.Empty },
                }
            );
        }

        public Message CreateSpaceBattle(
            Faction faction,
            SpaceCombatResult result,
            Faction opponent
        )
        {
            if (result == null)
                return null;

            MessageResultOutcome outcome = GetSpaceBattleOutcome(faction, result);
            if (outcome == MessageResultOutcome.None)
                return null;

            return CreateMessage(
                GetDefinition(MessageResultType.SpaceBattle, outcome),
                faction,
                new Dictionary<string, string>
                {
                    { "faction", faction?.GetDisplayName() ?? string.Empty },
                    { "opponent", opponent?.GetDisplayName() ?? string.Empty },
                    { "system", result.Planet?.GetDisplayName() ?? string.Empty },
                }
            );
        }

        public Message CreateBombardment(
            Faction faction,
            BombardmentResult result,
            Faction targetFaction
        )
        {
            if (result == null)
                return null;

            return CreateMessage(
                GetDefinition(
                    MessageResultType.Bombardment,
                    GetBombardmentOutcome(result),
                    GetPlanetOwnership(result.Planet)
                ),
                faction,
                new Dictionary<string, string>
                {
                    { "faction", result.AttackingFaction?.GetDisplayName() ?? string.Empty },
                    { "target", targetFaction?.GetDisplayName() ?? string.Empty },
                    { "system", result.Planet?.GetDisplayName() ?? string.Empty },
                }
            );
        }

        public Message CreatePlanetaryAssault(
            Faction faction,
            PlanetaryAssaultResult result,
            Faction targetFaction
        )
        {
            if (result == null)
                return null;

            return CreateMessage(
                GetDefinition(
                    MessageResultType.PlanetaryAssault,
                    result.Success ? MessageResultOutcome.Success : MessageResultOutcome.Failed,
                    GetAssaultPlanetOwnership(result)
                ),
                faction,
                new Dictionary<string, string>
                {
                    { "faction", result.AttackingFaction?.GetDisplayName() ?? string.Empty },
                    { "target", targetFaction?.GetDisplayName() ?? string.Empty },
                    { "system", result.Planet?.GetDisplayName() ?? string.Empty },
                },
                result.AttackingFaction
            );
        }

        private void AddArrivalMessages(
            IEnumerable<UnitArrivedResult> arrivals,
            GameRoot game,
            List<MessageDelivery> deliveries
        )
        {
            Dictionary<
                (string OwnerInstanceID, string DestinationInstanceID),
                List<CapitalShip>
            > shipGroups =
                new Dictionary<
                    (string OwnerInstanceID, string DestinationInstanceID),
                    List<CapitalShip>
                >();
            Dictionary<
                (string OwnerInstanceID, string DestinationInstanceID),
                Planet
            > shipDestinations =
                new Dictionary<(string OwnerInstanceID, string DestinationInstanceID), Planet>();

            foreach (UnitArrivedResult arrival in arrivals)
            {
                if (arrival.Unit is Fleet fleet)
                {
                    Faction faction = GetFaction(game, fleet.GetOwnerInstanceID());
                    AddDelivery(
                        deliveries,
                        faction,
                        CreateFleetArrived(faction, fleet, arrival.Destination)
                    );
                    continue;
                }

                if (arrival.Unit is CapitalShip ship)
                {
                    var key = (ship.GetOwnerInstanceID(), arrival.Destination?.GetInstanceID());
                    if (!shipGroups.TryGetValue(key, out List<CapitalShip> ships))
                    {
                        ships = new List<CapitalShip>();
                        shipGroups[key] = ships;
                        shipDestinations[key] = arrival.Destination;
                    }

                    ships.Add(ship);
                    continue;
                }

                if (arrival.Unit is Building building)
                {
                    Faction faction = GetFaction(game, building.GetOwnerInstanceID());
                    AddDelivery(
                        deliveries,
                        faction,
                        CreateFacilityDeployed(faction, building, arrival.Destination)
                    );
                }
            }

            foreach (
                KeyValuePair<
                    (string OwnerInstanceID, string DestinationInstanceID),
                    List<CapitalShip>
                > group in shipGroups
            )
            {
                Faction faction = GetFaction(game, group.Key.OwnerInstanceID);
                AddDelivery(
                    deliveries,
                    faction,
                    CreateShipsArrived(faction, group.Value, shipDestinations[group.Key])
                );
            }
        }

        private void AddMissionMessages(
            IEnumerable<MissionCompletedResult> results,
            GameRoot game,
            List<MessageDelivery> deliveries
        )
        {
            foreach (MissionCompletedResult result in results)
            {
                Planet target = GetMissionTarget(result);
                Faction actorFaction = GetFaction(game, result.Mission?.OwnerInstanceID);
                AddDelivery(
                    deliveries,
                    actorFaction,
                    CreateMissionReport(actorFaction, result, target)
                );

                Faction targetFaction = GetFaction(game, target?.OwnerInstanceID);
                if (targetFaction?.InstanceID == actorFaction?.InstanceID)
                    continue;

                AddDelivery(
                    deliveries,
                    targetFaction,
                    CreateEnemyMissionFoiled(targetFaction, result, target)
                );
            }
        }

        private void AddSabotageMessages(
            IEnumerable<GameObjectSabotagedResult> results,
            GameRoot game,
            List<MessageDelivery> deliveries
        )
        {
            foreach (GameObjectSabotagedResult result in results)
            {
                Planet target = GetSabotageTarget(result);
                string ownerInstanceID = GetOwnerInstanceID(result.SabotagedObject);
                if (string.IsNullOrEmpty(ownerInstanceID))
                    ownerInstanceID = target?.OwnerInstanceID;

                Faction faction = GetFaction(game, ownerInstanceID);
                AddDelivery(deliveries, faction, CreateSabotageStrike(faction, result, target));
            }
        }

        private void AddResearchMessages(
            IEnumerable<ResearchOrderedResult> orderedResults,
            IEnumerable<ResearchExhaustedResult> exhaustedResults,
            List<MessageDelivery> deliveries
        )
        {
            foreach (ResearchOrderedResult result in orderedResults)
                AddDelivery(
                    deliveries,
                    result.Faction,
                    CreateResearchComplete(result.Faction, result)
                );

            foreach (ResearchExhaustedResult result in exhaustedResults)
                AddDelivery(
                    deliveries,
                    result.Faction,
                    CreateResearchExhausted(result.Faction, result)
                );
        }

        private void AddUprisingMessages(
            IEnumerable<PlanetUprisingStartedResult> startedResults,
            IEnumerable<PlanetUprisingEndedResult> endedResults,
            GameRoot game,
            List<MessageDelivery> deliveries
        )
        {
            foreach (PlanetUprisingStartedResult result in startedResults)
            {
                Faction controller = GetFaction(game, result.Planet?.OwnerInstanceID);
                AddDelivery(
                    deliveries,
                    controller,
                    CreateUprisingStarted(controller, result, controller)
                );

                if (result.InstigatorFaction?.InstanceID == controller?.InstanceID)
                    continue;

                AddDelivery(
                    deliveries,
                    result.InstigatorFaction,
                    CreateUprisingStarted(result.InstigatorFaction, result, controller)
                );
            }

            foreach (PlanetUprisingEndedResult result in endedResults)
            {
                Faction controller =
                    GetFaction(game, result.Planet?.OwnerInstanceID) ?? result.Faction;
                AddDelivery(
                    deliveries,
                    controller,
                    CreateUprisingEnded(controller, result, controller)
                );
            }
        }

        private void AddBlockadeMessages(
            IEnumerable<BlockadeChangedResult> blockadeResults,
            IEnumerable<EvacuationLossesResult> evacuationResults,
            GameRoot game,
            List<MessageDelivery> deliveries
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
                AddDelivery(
                    deliveries,
                    blockadingFaction,
                    CreateBlockadeInitiated(blockadingFaction, result, targetFaction)
                );

                if (targetFaction?.InstanceID == blockadingFaction?.InstanceID)
                    continue;

                AddDelivery(
                    deliveries,
                    targetFaction,
                    CreateBlockadeDetected(targetFaction, result, blockadingFaction)
                );
            }

            foreach (EvacuationLossesResult result in evacuationResults)
                AddDelivery(
                    deliveries,
                    result.Faction,
                    CreateEvacuationLosses(result.Faction, result)
                );
        }

        private void AddMaintenanceMessages(
            IEnumerable<GameObjectAutoscrappedResult> autoscrapResults,
            GameRoot game,
            List<MessageDelivery> deliveries
        )
        {
            foreach (GameObjectAutoscrappedResult result in autoscrapResults)
            {
                Planet location = GetResultPlanet(
                    result.Context ?? result.Ref ?? result.DestroyedObject
                );
                Faction faction =
                    GetOwnerFaction(game, result.DestroyedObject)
                    ?? GetOwnerFaction(game, result.Ref)
                    ?? GetFaction(game, location?.OwnerInstanceID);
                AddDelivery(
                    deliveries,
                    faction,
                    CreateMaintenanceAutoscrap(faction, result, location)
                );
            }
        }

        private void AddCombatMessages(
            IEnumerable<SpaceCombatResult> battleResults,
            IEnumerable<BombardmentResult> bombardmentResults,
            IEnumerable<PlanetaryAssaultResult> assaultResults,
            GameRoot game,
            List<MessageDelivery> deliveries
        )
        {
            foreach (SpaceCombatResult result in battleResults)
            {
                Faction attacker = GetFaction(game, result.AttackerFleet?.GetOwnerInstanceID());
                Faction defender = GetFaction(game, result.DefenderFleet?.GetOwnerInstanceID());
                AddDelivery(deliveries, attacker, CreateSpaceBattle(attacker, result, defender));
                if (defender?.InstanceID != attacker?.InstanceID)
                    AddDelivery(
                        deliveries,
                        defender,
                        CreateSpaceBattle(defender, result, attacker)
                    );
            }

            foreach (BombardmentResult result in bombardmentResults)
            {
                Faction defender = GetFaction(game, result.Planet?.OwnerInstanceID);
                AddDelivery(
                    deliveries,
                    result.AttackingFaction,
                    CreateBombardment(result.AttackingFaction, result, defender)
                );
                if (defender?.InstanceID != result.AttackingFaction?.InstanceID)
                    AddDelivery(
                        deliveries,
                        defender,
                        CreateBombardment(defender, result, defender)
                    );
            }

            foreach (PlanetaryAssaultResult result in assaultResults)
            {
                Faction defender =
                    result.OwnershipChange?.PreviousOwner
                    ?? GetFaction(game, result.Planet?.OwnerInstanceID);
                AddDelivery(
                    deliveries,
                    result.AttackingFaction,
                    CreatePlanetaryAssault(result.AttackingFaction, result, defender)
                );
                if (defender?.InstanceID != result.AttackingFaction?.InstanceID)
                    AddDelivery(
                        deliveries,
                        defender,
                        CreatePlanetaryAssault(defender, result, defender)
                    );
            }
        }

        private void AddDeploymentMessages(
            IEnumerable<GameObjectDeployedResult> results,
            GameRoot game,
            List<MessageDelivery> deliveries
        )
        {
            foreach (GameObjectDeployedResult result in results)
            {
                if (result.GameObject is not Building building || building.Movement != null)
                    continue;

                Faction faction = GetFaction(game, building.GetOwnerInstanceID());
                AddDelivery(
                    deliveries,
                    faction,
                    CreateFacilityDeployed(faction, building, building.GetParentOfType<Planet>())
                );
            }
        }

        private void AddManufacturingMessages(
            IEnumerable<ManufacturingCompletedResult> results,
            List<MessageDelivery> deliveries
        )
        {
            foreach (ManufacturingCompletedResult result in results)
                AddDelivery(
                    deliveries,
                    result.Faction,
                    CreateManufacturingIdle(
                        result.Faction,
                        result.ProductType,
                        result.ProductionPlanet
                    )
                );
        }

        private void AddSeatOfPowerMessages(
            IEnumerable<SeatOfPowerChangedResult> results,
            GameRoot game,
            List<MessageDelivery> deliveries
        )
        {
            foreach (SeatOfPowerChangedResult result in results)
            {
                if (!result.IsAtSeat)
                    continue;

                Faction faction = GetFaction(game, result.Officer?.GetOwnerInstanceID());
                AddDelivery(deliveries, faction, CreateEmperorSeatOfPower(faction));
            }
        }

        private static void AddDelivery(
            List<MessageDelivery> deliveries,
            Faction faction,
            Message message
        )
        {
            if (faction == null || message == null)
                return;

            deliveries.Add(new MessageDelivery(faction, message));
        }

        private Message CreateMessage(
            MessageDefinition definition,
            Faction faction,
            Dictionary<string, string> values,
            Faction imageFaction = null
        )
        {
            if (definition == null)
                return null;

            string title = Interpolate(definition.TitleTemplate, values);
            string body = Interpolate(definition.BodyTemplate, values);

            return new Message(definition.MessageType, title, body)
            {
                InstanceID = Guid.NewGuid().ToString("N"),
                DisplayName = title,
                DisplayImagePath = definition.ImageMap?.GetForFaction(imageFaction ?? faction),
            };
        }

        private MessageDefinition GetDefinition(
            MessageResultType resultType,
            MessageResultOutcome outcome = MessageResultOutcome.None,
            MessagePlanetOwnership planetOwnership = MessagePlanetOwnership.None,
            BuildingType buildingType = BuildingType.None,
            ManufacturingType manufacturingType = ManufacturingType.None,
            ResearchDiscipline? discipline = null
        )
        {
            return _definitions.FirstOrDefault(definition =>
                definition.ResultType == resultType
                && definition.Outcome == outcome
                && definition.PlanetOwnership == planetOwnership
                && definition.BuildingType == buildingType
                && definition.ManufacturingType == manufacturingType
                && (!discipline.HasValue || definition.ResearchDiscipline == discipline.Value)
            );
        }

        private static string GetMissionName(MissionCompletedResult result)
        {
            return result.MissionName ?? result.Mission?.GetDisplayName() ?? string.Empty;
        }

        private static string GetTargetName(MissionCompletedResult result, Planet target)
        {
            return target?.GetDisplayName() ?? result.TargetName ?? string.Empty;
        }

        private static string GetDisplayName(IGameEntity entity)
        {
            return entity?.GetDisplayName() ?? string.Empty;
        }

        private static string FormatLostUnits(EvacuationLossesResult result)
        {
            IEnumerable<IGameEntity> units = result
                .LostShips.Cast<IGameEntity>()
                .Concat(result.LostStarfighters)
                .Concat(result.LostRegiments);

            return string.Join("\n", units.Select(GetDisplayName).Where(name => name.Length > 0));
        }

        private static string Interpolate(string template, Dictionary<string, string> values)
        {
            string result = template ?? string.Empty;
            foreach (KeyValuePair<string, string> value in values)
                result = result.Replace("{" + value.Key + "}", value.Value ?? string.Empty);

            return result;
        }

        private static MessageResultOutcome GetSpaceBattleOutcome(
            Faction faction,
            SpaceCombatResult result
        )
        {
            if (result.Winner == CombatSide.Draw)
                return MessageResultOutcome.Stalemate;

            if (faction?.InstanceID == result.AttackerFleet?.GetOwnerInstanceID())
                return result.Winner == CombatSide.Attacker
                    ? MessageResultOutcome.Victory
                    : MessageResultOutcome.Defeat;

            if (faction?.InstanceID == result.DefenderFleet?.GetOwnerInstanceID())
                return result.Winner == CombatSide.Defender
                    ? MessageResultOutcome.Victory
                    : MessageResultOutcome.Defeat;

            return MessageResultOutcome.None;
        }

        private static MessageResultOutcome GetBombardmentOutcome(BombardmentResult result)
        {
            if (HasBombardmentTargetLosses(result))
                return MessageResultOutcome.TargetLosses;

            if (HasBombardmentAttackerLosses(result))
                return MessageResultOutcome.AttackerLosses;

            return MessageResultOutcome.NoLosses;
        }

        private static bool HasBombardmentTargetLosses(BombardmentResult result)
        {
            if (result.DestroyedBuildings.Any() || result.DestroyedStarfighters.Any())
                return true;

            if (
                result.Strikes.Any(strike =>
                    strike.Lane == BombardmentLaneType.CapitalShip
                    || strike.Lane == BombardmentLaneType.Starfighter
                    || strike.Lane == BombardmentLaneType.Building
                )
            )
                return true;

            string attackerID = result.AttackingFaction?.InstanceID;
            return result.DestroyedRegiments.Any(regiment =>
                regiment.GetOwnerInstanceID() != attackerID
            );
        }

        private static bool HasBombardmentAttackerLosses(BombardmentResult result)
        {
            string attackerID = result.AttackingFaction?.InstanceID;
            return !string.IsNullOrEmpty(attackerID)
                && result.DestroyedRegiments.Any(regiment =>
                    regiment.GetOwnerInstanceID() == attackerID
                );
        }

        private static MessagePlanetOwnership GetAssaultPlanetOwnership(
            PlanetaryAssaultResult result
        )
        {
            if (result?.OwnershipChange != null)
                return result.OwnershipChange.PreviousOwner == null
                    ? MessagePlanetOwnership.Neutral
                    : MessagePlanetOwnership.Owned;

            return GetPlanetOwnership(result?.Planet);
        }

        private static MessagePlanetOwnership GetPlanetOwnership(Planet planet)
        {
            return string.IsNullOrEmpty(planet?.OwnerInstanceID)
                ? MessagePlanetOwnership.Neutral
                : MessagePlanetOwnership.Owned;
        }

        private static Planet GetMissionTarget(MissionCompletedResult result)
        {
            return result?.Mission?.GetParent() as Planet
                ?? result?.Mission?.GetLastParent() as Planet;
        }

        private static Planet GetSabotageTarget(GameObjectSabotagedResult result)
        {
            if (result?.Context is Planet contextPlanet)
                return contextPlanet;

            if (result?.SabotagedObject is ISceneNode sceneNode)
                return sceneNode.GetParentOfType<Planet>() ?? sceneNode.GetLastParent() as Planet;

            return null;
        }

        private static Planet GetResultPlanet(IGameEntity entity)
        {
            if (entity is Planet planet)
                return planet;

            if (entity is ISceneNode sceneNode)
                return sceneNode.GetParentOfType<Planet>() ?? sceneNode.GetLastParent() as Planet;

            return null;
        }

        private static string GetOwnerInstanceID(IGameEntity entity)
        {
            return entity is ISceneNode sceneNode ? sceneNode.GetOwnerInstanceID() : null;
        }

        private static Faction GetFaction(GameRoot game, string ownerInstanceID)
        {
            return string.IsNullOrEmpty(ownerInstanceID)
                ? null
                : game
                    ?.GetFactions()
                    .FirstOrDefault(faction => faction.InstanceID == ownerInstanceID);
        }

        private static Faction GetOwnerFaction(GameRoot game, IGameEntity entity)
        {
            return GetFaction(game, GetOwnerInstanceID(entity));
        }
    }
}
