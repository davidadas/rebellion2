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
    /// <summary>
    /// Builds faction message deliveries from game results and configured message definitions.
    /// </summary>
    public class MessageFactory
    {
        private readonly MessageDefinition[] _definitions;

        /// <summary>
        /// Creates a message factory backed by the supplied message definitions.
        /// </summary>
        /// <param name="definitions">The message definitions used to select templates and images.</param>
        public MessageFactory(IEnumerable<MessageDefinition> definitions)
        {
            _definitions = definitions?.ToArray() ?? Array.Empty<MessageDefinition>();
        }

        /// <summary>
        /// Creates messages for the factions affected by the supplied game results.
        /// </summary>
        /// <param name="results">The game results to translate into message deliveries.</param>
        /// <param name="game">The game state used to resolve affected factions and display names.</param>
        /// <returns>The messages to add to each recipient faction.</returns>
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

        /// <summary>
        /// Creates a fleet arrival message.
        /// </summary>
        /// <param name="faction">The faction that owns the arriving fleet.</param>
        /// <param name="fleet">The fleet that arrived.</param>
        /// <param name="destination">The planet where the fleet arrived.</param>
        /// <returns>The fleet arrival message, or null when no matching definition exists.</returns>
        private Message CreateFleetArrived(Faction faction, Fleet fleet, Planet destination)
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

        /// <summary>
        /// Creates a grouped capital ship arrival message.
        /// </summary>
        /// <param name="faction">The faction that owns the arriving ships.</param>
        /// <param name="ships">The ships that arrived together.</param>
        /// <param name="destination">The planet where the ships arrived.</param>
        /// <returns>The ship arrival message, or null when no matching definition exists.</returns>
        private Message CreateShipsArrived(
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

        /// <summary>
        /// Creates the emperor seat-of-power message.
        /// </summary>
        /// <param name="faction">The faction that owns the emperor.</param>
        /// <returns>The seat-of-power message, or null when no matching definition exists.</returns>
        private Message CreateEmperorSeatOfPower(Faction faction)
        {
            return CreateMessage(
                GetDefinition(MessageResultType.EmperorSeatOfPower),
                faction,
                new Dictionary<string, string>()
            );
        }

        /// <summary>
        /// Creates a facility deployment message.
        /// </summary>
        /// <param name="faction">The faction that owns the deployed building.</param>
        /// <param name="building">The deployed building.</param>
        /// <param name="destination">The planet where the building deployed.</param>
        /// <returns>The facility deployment message, or null when no matching definition exists.</returns>
        private Message CreateFacilityDeployed(
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

        /// <summary>
        /// Creates a manufacturing idle message.
        /// </summary>
        /// <param name="faction">The faction whose manufacturing queue became idle.</param>
        /// <param name="manufacturingType">The idle manufacturing queue type.</param>
        /// <param name="planet">The planet where the queue became idle.</param>
        /// <returns>The manufacturing idle message, or null when no matching definition exists.</returns>
        private Message CreateManufacturingIdle(
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

        /// <summary>
        /// Creates the mission report for the acting faction.
        /// </summary>
        /// <param name="faction">The faction that launched the mission.</param>
        /// <param name="result">The completed mission result.</param>
        /// <param name="target">The mission target planet.</param>
        /// <returns>The mission report message, or null when no matching definition exists.</returns>
        private Message CreateMissionReport(
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

        /// <summary>
        /// Creates the target-faction report for a foiled enemy mission.
        /// </summary>
        /// <param name="faction">The faction that owned the mission target.</param>
        /// <param name="result">The completed mission result.</param>
        /// <param name="target">The mission target planet.</param>
        /// <returns>The enemy mission foiled message, or null when the result is not foiled or no definition exists.</returns>
        private Message CreateEnemyMissionFoiled(
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

        /// <summary>
        /// Creates a sabotage strike message for the owner of the destroyed object.
        /// </summary>
        /// <param name="faction">The faction that owned the sabotaged object.</param>
        /// <param name="result">The sabotage result.</param>
        /// <param name="target">The planet where sabotage occurred.</param>
        /// <returns>The sabotage strike message, or null when no matching definition exists.</returns>
        private Message CreateSabotageStrike(
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

        /// <summary>
        /// Creates a research completion message.
        /// </summary>
        /// <param name="faction">The faction that completed research.</param>
        /// <param name="result">The completed research result.</param>
        /// <returns>The research completion message, or null when no matching definition exists.</returns>
        private Message CreateResearchComplete(Faction faction, ResearchOrderedResult result)
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

        /// <summary>
        /// Creates a research exhausted message.
        /// </summary>
        /// <param name="faction">The faction whose research discipline is exhausted.</param>
        /// <param name="result">The exhausted research result.</param>
        /// <returns>The research exhausted message, or null when no matching definition exists.</returns>
        private Message CreateResearchExhausted(Faction faction, ResearchExhaustedResult result)
        {
            if (result == null)
                return null;

            return CreateMessage(
                GetDefinition(MessageResultType.ResearchExhausted, discipline: result.Discipline),
                faction,
                new Dictionary<string, string>()
            );
        }

        /// <summary>
        /// Creates an uprising started message.
        /// </summary>
        /// <param name="faction">The faction that should receive the message.</param>
        /// <param name="result">The uprising started result.</param>
        /// <param name="controller">The faction that controls the planet.</param>
        /// <returns>The uprising started message, or null when no matching definition exists.</returns>
        private Message CreateUprisingStarted(
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

        /// <summary>
        /// Creates an uprising ended message.
        /// </summary>
        /// <param name="faction">The faction that should receive the message.</param>
        /// <param name="result">The uprising ended result.</param>
        /// <param name="controller">The faction that controls the planet.</param>
        /// <returns>The uprising ended message, or null when no matching definition exists.</returns>
        private Message CreateUprisingEnded(
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

        /// <summary>
        /// Creates the blockading faction's blockade started message.
        /// </summary>
        /// <param name="faction">The faction that started the blockade.</param>
        /// <param name="result">The blockade changed result.</param>
        /// <param name="targetFaction">The faction that owns the blockaded planet.</param>
        /// <returns>The blockade initiated message, or null when no matching definition exists.</returns>
        private Message CreateBlockadeInitiated(
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

        /// <summary>
        /// Creates the target faction's blockade detected message.
        /// </summary>
        /// <param name="faction">The faction that owns the blockaded planet.</param>
        /// <param name="result">The blockade changed result.</param>
        /// <param name="blockadingFaction">The faction that started the blockade.</param>
        /// <returns>The blockade detected message, or null when no matching definition exists.</returns>
        private Message CreateBlockadeDetected(
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

        /// <summary>
        /// Creates an evacuation losses message.
        /// </summary>
        /// <param name="faction">The faction that lost units during evacuation.</param>
        /// <param name="result">The evacuation losses result.</param>
        /// <returns>The evacuation losses message, or null when no matching definition exists.</returns>
        private Message CreateEvacuationLosses(Faction faction, EvacuationLossesResult result)
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

        /// <summary>
        /// Creates an autoscrap message for maintenance losses.
        /// </summary>
        /// <param name="faction">The faction that owned the destroyed object.</param>
        /// <param name="result">The autoscrap result.</param>
        /// <param name="location">The planet where the object was destroyed.</param>
        /// <returns>The autoscrap message, or null when no matching definition exists.</returns>
        private Message CreateMaintenanceAutoscrap(
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

        /// <summary>
        /// Creates a space battle result message for one faction.
        /// </summary>
        /// <param name="faction">The faction that should receive the message.</param>
        /// <param name="result">The space combat result.</param>
        /// <param name="opponent">The opposing faction.</param>
        /// <returns>The space battle message, or null when no matching definition exists.</returns>
        private Message CreateSpaceBattle(
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

        /// <summary>
        /// Creates a bombardment result message for one faction.
        /// </summary>
        /// <param name="faction">The faction that should receive the message.</param>
        /// <param name="result">The bombardment result.</param>
        /// <param name="targetFaction">The faction that owned the target planet.</param>
        /// <returns>The bombardment message, or null when no matching definition exists.</returns>
        private Message CreateBombardment(
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

        /// <summary>
        /// Creates a planetary assault result message for one faction.
        /// </summary>
        /// <param name="faction">The faction that should receive the message.</param>
        /// <param name="result">The planetary assault result.</param>
        /// <param name="targetFaction">The faction that owned the assaulted planet.</param>
        /// <returns>The planetary assault message, or null when no matching definition exists.</returns>
        private Message CreatePlanetaryAssault(
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

        /// <summary>
        /// Adds messages for arriving fleets, ships, and buildings.
        /// </summary>
        /// <param name="arrivals">The arrival results to process.</param>
        /// <param name="game">The game state used to resolve owning factions.</param>
        /// <param name="deliveries">The delivery list to append messages to.</param>
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

        /// <summary>
        /// Adds messages for completed missions.
        /// </summary>
        /// <param name="results">The completed mission results to process.</param>
        /// <param name="game">The game state used to resolve recipient factions.</param>
        /// <param name="deliveries">The delivery list to append messages to.</param>
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

        /// <summary>
        /// Adds messages for sabotaged game objects.
        /// </summary>
        /// <param name="results">The sabotage results to process.</param>
        /// <param name="game">The game state used to resolve recipient factions.</param>
        /// <param name="deliveries">The delivery list to append messages to.</param>
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

        /// <summary>
        /// Adds messages for completed and exhausted research.
        /// </summary>
        /// <param name="orderedResults">The completed research results to process.</param>
        /// <param name="exhaustedResults">The exhausted research results to process.</param>
        /// <param name="deliveries">The delivery list to append messages to.</param>
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

        /// <summary>
        /// Adds messages for uprising start and end results.
        /// </summary>
        /// <param name="startedResults">The uprising started results to process.</param>
        /// <param name="endedResults">The uprising ended results to process.</param>
        /// <param name="game">The game state used to resolve recipient factions.</param>
        /// <param name="deliveries">The delivery list to append messages to.</param>
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

        /// <summary>
        /// Adds messages for blockades and evacuation losses.
        /// </summary>
        /// <param name="blockadeResults">The blockade changed results to process.</param>
        /// <param name="evacuationResults">The evacuation loss results to process.</param>
        /// <param name="game">The game state used to resolve recipient factions.</param>
        /// <param name="deliveries">The delivery list to append messages to.</param>
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

        /// <summary>
        /// Adds messages for autoscrapped objects.
        /// </summary>
        /// <param name="autoscrapResults">The autoscrap results to process.</param>
        /// <param name="game">The game state used to resolve recipient factions.</param>
        /// <param name="deliveries">The delivery list to append messages to.</param>
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

        /// <summary>
        /// Adds messages for space combat, bombardment, and planetary assault results.
        /// </summary>
        /// <param name="battleResults">The space combat results to process.</param>
        /// <param name="bombardmentResults">The bombardment results to process.</param>
        /// <param name="assaultResults">The planetary assault results to process.</param>
        /// <param name="game">The game state used to resolve recipient factions.</param>
        /// <param name="deliveries">The delivery list to append messages to.</param>
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

        /// <summary>
        /// Adds messages for deployed buildings.
        /// </summary>
        /// <param name="results">The deployment results to process.</param>
        /// <param name="game">The game state used to resolve recipient factions.</param>
        /// <param name="deliveries">The delivery list to append messages to.</param>
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

        /// <summary>
        /// Adds messages for completed manufacturing queues.
        /// </summary>
        /// <param name="results">The manufacturing completed results to process.</param>
        /// <param name="deliveries">The delivery list to append messages to.</param>
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

        /// <summary>
        /// Adds messages for seat-of-power changes.
        /// </summary>
        /// <param name="results">The seat-of-power changed results to process.</param>
        /// <param name="game">The game state used to resolve recipient factions.</param>
        /// <param name="deliveries">The delivery list to append messages to.</param>
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

        /// <summary>
        /// Adds a non-null message delivery for a non-null faction.
        /// </summary>
        /// <param name="deliveries">The delivery list to append to.</param>
        /// <param name="faction">The faction that should receive the message.</param>
        /// <param name="message">The message to deliver.</param>
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

        /// <summary>
        /// Creates a message from a definition and interpolation values.
        /// </summary>
        /// <param name="definition">The message definition that supplies the template and image map.</param>
        /// <param name="faction">The faction that should receive the message.</param>
        /// <param name="values">The values to substitute into the templates.</param>
        /// <param name="imageFaction">The faction used for faction-specific image selection.</param>
        /// <returns>The created message, or null when the definition is missing.</returns>
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
                DisplayName = title,
                DisplayImagePath = definition.ImageMap?.GetForFaction(imageFaction ?? faction),
            };
        }

        /// <summary>
        /// Finds the configured message definition for a result selector.
        /// </summary>
        /// <param name="resultType">The message result type to match.</param>
        /// <param name="outcome">The result outcome to match.</param>
        /// <param name="planetOwnership">The planet ownership selector to match.</param>
        /// <param name="buildingType">The building type selector to match.</param>
        /// <param name="manufacturingType">The manufacturing type selector to match.</param>
        /// <param name="discipline">The research discipline selector to match.</param>
        /// <returns>The matching message definition, or null when none exists.</returns>
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

        /// <summary>
        /// Gets the display name for a completed mission result.
        /// </summary>
        /// <param name="result">The completed mission result.</param>
        /// <returns>The mission display name, or an empty string when none is available.</returns>
        private static string GetMissionName(MissionCompletedResult result)
        {
            return result.MissionName ?? result.Mission?.GetDisplayName() ?? string.Empty;
        }

        /// <summary>
        /// Gets the display name for a mission target.
        /// </summary>
        /// <param name="result">The completed mission result.</param>
        /// <param name="target">The resolved target planet.</param>
        /// <returns>The target display name, or an empty string when none is available.</returns>
        private static string GetTargetName(MissionCompletedResult result, Planet target)
        {
            return target?.GetDisplayName() ?? result.TargetName ?? string.Empty;
        }

        /// <summary>
        /// Gets the display name for a game entity.
        /// </summary>
        /// <param name="entity">The entity whose display name should be returned.</param>
        /// <returns>The entity display name, or an empty string when none is available.</returns>
        private static string GetDisplayName(IGameEntity entity)
        {
            return entity?.GetDisplayName() ?? string.Empty;
        }

        /// <summary>
        /// Formats evacuation losses as a newline-separated unit list.
        /// </summary>
        /// <param name="result">The evacuation losses result.</param>
        /// <returns>The formatted unit list.</returns>
        private static string FormatLostUnits(EvacuationLossesResult result)
        {
            IEnumerable<IGameEntity> units = result
                .LostShips.Cast<IGameEntity>()
                .Concat(result.LostStarfighters)
                .Concat(result.LostRegiments);

            return string.Join("\n", units.Select(GetDisplayName).Where(name => name.Length > 0));
        }

        /// <summary>
        /// Replaces template tokens with provided values.
        /// </summary>
        /// <param name="template">The message template text.</param>
        /// <param name="values">The values to substitute into the template.</param>
        /// <returns>The interpolated message text.</returns>
        private static string Interpolate(string template, Dictionary<string, string> values)
        {
            string result = template ?? string.Empty;
            foreach (KeyValuePair<string, string> value in values)
                result = result.Replace("{" + value.Key + "}", value.Value ?? string.Empty);

            return result;
        }

        /// <summary>
        /// Gets the message outcome for a space battle from one faction's perspective.
        /// </summary>
        /// <param name="faction">The faction whose perspective should be evaluated.</param>
        /// <param name="result">The space combat result.</param>
        /// <returns>The message outcome for the faction.</returns>
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

        /// <summary>
        /// Gets the message outcome for a bombardment result.
        /// </summary>
        /// <param name="result">The bombardment result.</param>
        /// <returns>The bombardment message outcome.</returns>
        private static MessageResultOutcome GetBombardmentOutcome(BombardmentResult result)
        {
            if (HasBombardmentTargetLosses(result))
                return MessageResultOutcome.TargetLosses;

            if (HasBombardmentAttackerLosses(result))
                return MessageResultOutcome.AttackerLosses;

            return MessageResultOutcome.NoLosses;
        }

        /// <summary>
        /// Checks whether bombardment destroyed target-side assets.
        /// </summary>
        /// <param name="result">The bombardment result.</param>
        /// <returns>True when target-side assets were destroyed; otherwise false.</returns>
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

        /// <summary>
        /// Checks whether bombardment destroyed attacker-side regiments.
        /// </summary>
        /// <param name="result">The bombardment result.</param>
        /// <returns>True when attacker-side regiments were destroyed; otherwise false.</returns>
        private static bool HasBombardmentAttackerLosses(BombardmentResult result)
        {
            string attackerID = result.AttackingFaction?.InstanceID;
            return !string.IsNullOrEmpty(attackerID)
                && result.DestroyedRegiments.Any(regiment =>
                    regiment.GetOwnerInstanceID() == attackerID
                );
        }

        /// <summary>
        /// Gets the planet ownership selector for a planetary assault result.
        /// </summary>
        /// <param name="result">The planetary assault result.</param>
        /// <returns>The planet ownership selector for the assault message.</returns>
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

        /// <summary>
        /// Gets the message ownership selector for a planet.
        /// </summary>
        /// <param name="planet">The planet to evaluate.</param>
        /// <returns>The planet ownership selector.</returns>
        private static MessagePlanetOwnership GetPlanetOwnership(Planet planet)
        {
            return string.IsNullOrEmpty(planet?.OwnerInstanceID)
                ? MessagePlanetOwnership.Neutral
                : MessagePlanetOwnership.Owned;
        }

        /// <summary>
        /// Gets the planet targeted by a completed mission.
        /// </summary>
        /// <param name="result">The completed mission result.</param>
        /// <returns>The target planet, or null when the target cannot be resolved.</returns>
        private static Planet GetMissionTarget(MissionCompletedResult result)
        {
            return result?.Mission?.GetParent() as Planet
                ?? result?.Mission?.GetLastParent() as Planet;
        }

        /// <summary>
        /// Gets the planet where sabotage occurred.
        /// </summary>
        /// <param name="result">The sabotage result.</param>
        /// <returns>The sabotage target planet, or null when the target cannot be resolved.</returns>
        private static Planet GetSabotageTarget(GameObjectSabotagedResult result)
        {
            if (result?.Context is Planet contextPlanet)
                return contextPlanet;

            if (result?.SabotagedObject is ISceneNode sceneNode)
                return sceneNode.GetParentOfType<Planet>() ?? sceneNode.GetLastParent() as Planet;

            return null;
        }

        /// <summary>
        /// Gets the planet associated with a result entity.
        /// </summary>
        /// <param name="entity">The entity to resolve to a planet.</param>
        /// <returns>The associated planet, or null when none can be resolved.</returns>
        private static Planet GetResultPlanet(IGameEntity entity)
        {
            if (entity is Planet planet)
                return planet;

            if (entity is ISceneNode sceneNode)
                return sceneNode.GetParentOfType<Planet>() ?? sceneNode.GetLastParent() as Planet;

            return null;
        }

        /// <summary>
        /// Gets the owner instance ID for a game entity.
        /// </summary>
        /// <param name="entity">The entity whose owner should be returned.</param>
        /// <returns>The owner instance ID, or null when none is available.</returns>
        private static string GetOwnerInstanceID(IGameEntity entity)
        {
            return entity is ISceneNode sceneNode ? sceneNode.GetOwnerInstanceID() : null;
        }

        /// <summary>
        /// Finds a faction by owner instance ID.
        /// </summary>
        /// <param name="game">The game state containing factions.</param>
        /// <param name="ownerInstanceID">The owner instance ID to match.</param>
        /// <returns>The matching faction, or null when none is found.</returns>
        private static Faction GetFaction(GameRoot game, string ownerInstanceID)
        {
            return string.IsNullOrEmpty(ownerInstanceID)
                ? null
                : game
                    ?.GetFactions()
                    .FirstOrDefault(faction => faction.InstanceID == ownerInstanceID);
        }

        /// <summary>
        /// Finds the owning faction for a game entity.
        /// </summary>
        /// <param name="game">The game state containing factions.</param>
        /// <param name="entity">The entity whose owner should be resolved.</param>
        /// <returns>The owning faction, or null when none is found.</returns>
        private static Faction GetOwnerFaction(GameRoot game, IGameEntity entity)
        {
            return GetFaction(game, GetOwnerInstanceID(entity));
        }
    }
}
