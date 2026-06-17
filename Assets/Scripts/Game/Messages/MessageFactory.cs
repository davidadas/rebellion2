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
        private const string _fleetArrivedDefinitionID = "MESSAGE_FLEET_ARRIVED";
        private const string _shipsArrivedDefinitionID = "MESSAGE_SHIPS_ARRIVED";
        private const string _emperorSeatOfPowerDefinitionID = "MESSAGE_EMPEROR_SEAT_OF_POWER";
        private const string _shipyardIdleDefinitionID = "MESSAGE_SHIPYARD_IDLE";
        private const string _trainingFacilityIdleDefinitionID = "MESSAGE_TRAINING_FACILITY_IDLE";
        private const string _constructionYardIdleDefinitionID = "MESSAGE_CONSTRUCTION_YARD_IDLE";
        private const string _missionReportSuccessDefinitionID = "MESSAGE_MISSION_REPORT_SUCCESS";
        private const string _missionReportFailedDefinitionID = "MESSAGE_MISSION_REPORT_FAILED";
        private const string _enemyMissionFoiledDefinitionID = "MESSAGE_ENEMY_MISSION_FOILED";
        private const string _sabotageStrikeDefinitionID = "MESSAGE_SABOTAGE_STRIKE";
        private const string _researchCompleteShipDefinitionID = "MESSAGE_RESEARCH_COMPLETE_SHIP";
        private const string _researchCompleteTroopDefinitionID = "MESSAGE_RESEARCH_COMPLETE_TROOP";
        private const string _researchCompleteFacilityDefinitionID =
            "MESSAGE_RESEARCH_COMPLETE_FACILITY";
        private const string _researchExhaustedShipDefinitionID = "MESSAGE_RESEARCH_EXHAUSTED_SHIP";
        private const string _researchExhaustedTroopDefinitionID =
            "MESSAGE_RESEARCH_EXHAUSTED_TROOP";
        private const string _researchExhaustedFacilityDefinitionID =
            "MESSAGE_RESEARCH_EXHAUSTED_FACILITY";
        private const string _uprisingStartedDefinitionID = "MESSAGE_UPRISING_STARTED";
        private const string _uprisingEndedDefinitionID = "MESSAGE_UPRISING_ENDED";
        private const string _blockadeInitiatedDefinitionID = "MESSAGE_BLOCKADE_INITIATED";
        private const string _blockadeDetectedDefinitionID = "MESSAGE_BLOCKADE_DETECTED";
        private const string _evacuationLossesDefinitionID = "MESSAGE_EVACUATION_LOSSES";
        private const string _maintenanceAutoscrapDefinitionID = "MESSAGE_MAINTENANCE_AUTOSCRAP";
        private const string _spaceBattleVictoryDefinitionID = "MESSAGE_SPACE_BATTLE_VICTORY";
        private const string _spaceBattleDefeatDefinitionID = "MESSAGE_SPACE_BATTLE_DEFEAT";
        private const string _spaceBattleStalemateDefinitionID = "MESSAGE_SPACE_BATTLE_STALEMATE";
        private const string _bombardmentOwnedNoLossesDefinitionID =
            "MESSAGE_BOMBARDMENT_OWNED_NO_LOSSES";
        private const string _bombardmentOwnedTargetLossesDefinitionID =
            "MESSAGE_BOMBARDMENT_OWNED_TARGET_LOSSES";
        private const string _bombardmentOwnedAttackerLossesDefinitionID =
            "MESSAGE_BOMBARDMENT_OWNED_ATTACKER_LOSSES";
        private const string _bombardmentNeutralNoLossesDefinitionID =
            "MESSAGE_BOMBARDMENT_NEUTRAL_NO_LOSSES";
        private const string _bombardmentNeutralTargetLossesDefinitionID =
            "MESSAGE_BOMBARDMENT_NEUTRAL_TARGET_LOSSES";
        private const string _bombardmentNeutralAttackerLossesDefinitionID =
            "MESSAGE_BOMBARDMENT_NEUTRAL_ATTACKER_LOSSES";
        private const string _assaultCapturedOwnedSystemDefinitionID =
            "MESSAGE_ASSAULT_CAPTURED_OWNED_SYSTEM";
        private const string _assaultDefendedOwnedSystemDefinitionID =
            "MESSAGE_ASSAULT_DEFENDED_OWNED_SYSTEM";
        private const string _assaultNeutralSuccessDefinitionID = "MESSAGE_ASSAULT_NEUTRAL_SUCCESS";
        private const string _assaultNeutralFailedDefinitionID = "MESSAGE_ASSAULT_NEUTRAL_FAILED";

        private readonly Dictionary<string, MessageDefinition> _definitionsByID;
        private readonly Dictionary<
            BuildingType,
            MessageDefinition
        > _deployedFacilityDefinitionsByBuildingType;

        public MessageFactory(IEnumerable<MessageDefinition> definitions)
        {
            MessageDefinition[] definitionArray =
                definitions?.ToArray() ?? Array.Empty<MessageDefinition>();
            _definitionsByID = definitionArray
                .Where(definition => !string.IsNullOrEmpty(definition.InstanceID))
                .ToDictionary(definition => definition.InstanceID);
            _deployedFacilityDefinitionsByBuildingType = definitionArray
                .Where(definition => definition.BuildingType != BuildingType.None)
                .ToDictionary(definition => definition.BuildingType);
        }

        public Message CreateFleetArrived(Faction faction, Fleet fleet, Planet destination)
        {
            return CreateMessage(
                _fleetArrivedDefinitionID,
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
                _shipsArrivedDefinitionID,
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
                _emperorSeatOfPowerDefinitionID,
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
            if (
                buildingType == BuildingType.None
                || !_deployedFacilityDefinitionsByBuildingType.TryGetValue(
                    buildingType,
                    out MessageDefinition definition
                )
            )
            {
                return null;
            }

            return CreateMessage(
                definition,
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
            string definitionID = GetIdleDefinitionID(manufacturingType);
            if (string.IsNullOrEmpty(definitionID))
                return null;

            return CreateMessage(
                definitionID,
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

            string definitionID =
                result.Outcome == MissionOutcome.Success
                    ? _missionReportSuccessDefinitionID
                    : _missionReportFailedDefinitionID;

            return CreateMessage(
                definitionID,
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
                _enemyMissionFoiledDefinitionID,
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
                _sabotageStrikeDefinitionID,
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

            string definitionID = GetResearchCompleteDefinitionID(result.Discipline);
            if (string.IsNullOrEmpty(definitionID))
                return null;

            string item = GetDisplayName(result.Technology.GetReference());
            return CreateMessage(
                definitionID,
                faction,
                new Dictionary<string, string> { { "item", item } }
            );
        }

        public Message CreateResearchExhausted(Faction faction, ResearchExhaustedResult result)
        {
            if (result == null)
                return null;

            string definitionID = GetResearchExhaustedDefinitionID(result.Discipline);
            if (string.IsNullOrEmpty(definitionID))
                return null;

            return CreateMessage(definitionID, faction, new Dictionary<string, string>());
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
                _uprisingStartedDefinitionID,
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
                _uprisingEndedDefinitionID,
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
                _blockadeInitiatedDefinitionID,
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
                _blockadeDetectedDefinitionID,
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
                _evacuationLossesDefinitionID,
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
                _maintenanceAutoscrapDefinitionID,
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

            string definitionID = GetSpaceBattleDefinitionID(faction, result);
            if (string.IsNullOrEmpty(definitionID))
                return null;

            return CreateMessage(
                definitionID,
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

            string definitionID = GetBombardmentDefinitionID(result);

            return CreateMessage(
                definitionID,
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

            string definitionID = GetPlanetaryAssaultDefinitionID(
                result.Success,
                IsNeutralAssaultSystem(result)
            );
            return CreateMessage(
                definitionID,
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

        private Message CreateMessage(
            string definitionID,
            Faction faction,
            Dictionary<string, string> values,
            Faction imageFaction = null
        )
        {
            if (!_definitionsByID.TryGetValue(definitionID, out MessageDefinition definition))
                return null;

            return CreateMessage(definition, faction, values, imageFaction ?? faction);
        }

        private Message CreateMessage(
            MessageDefinition definition,
            Faction faction,
            Dictionary<string, string> values,
            Faction imageFaction = null
        )
        {
            string title = Interpolate(definition.TitleTemplate, values);
            string body = Interpolate(definition.BodyTemplate, values);

            return new Message(definition.MessageType, title, body)
            {
                InstanceID = Guid.NewGuid().ToString("N"),
                DisplayName = title,
                DisplayImagePath = definition.ImageMap?.GetForFaction(imageFaction ?? faction),
            };
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

        private static string GetIdleDefinitionID(ManufacturingType manufacturingType)
        {
            return manufacturingType switch
            {
                ManufacturingType.Ship => _shipyardIdleDefinitionID,
                ManufacturingType.Troop => _trainingFacilityIdleDefinitionID,
                ManufacturingType.Building => _constructionYardIdleDefinitionID,
                _ => null,
            };
        }

        private static string GetResearchCompleteDefinitionID(ResearchDiscipline discipline)
        {
            return discipline switch
            {
                ResearchDiscipline.ShipDesign => _researchCompleteShipDefinitionID,
                ResearchDiscipline.TroopTraining => _researchCompleteTroopDefinitionID,
                ResearchDiscipline.FacilityDesign => _researchCompleteFacilityDefinitionID,
                _ => null,
            };
        }

        private static string GetResearchExhaustedDefinitionID(ResearchDiscipline discipline)
        {
            return discipline switch
            {
                ResearchDiscipline.ShipDesign => _researchExhaustedShipDefinitionID,
                ResearchDiscipline.TroopTraining => _researchExhaustedTroopDefinitionID,
                ResearchDiscipline.FacilityDesign => _researchExhaustedFacilityDefinitionID,
                _ => null,
            };
        }

        private static string GetSpaceBattleDefinitionID(Faction faction, SpaceCombatResult result)
        {
            if (result.Winner == CombatSide.Draw)
                return _spaceBattleStalemateDefinitionID;

            if (faction?.InstanceID == result.AttackerFleet?.GetOwnerInstanceID())
                return result.Winner == CombatSide.Attacker
                    ? _spaceBattleVictoryDefinitionID
                    : _spaceBattleDefeatDefinitionID;

            if (faction?.InstanceID == result.DefenderFleet?.GetOwnerInstanceID())
                return result.Winner == CombatSide.Defender
                    ? _spaceBattleVictoryDefinitionID
                    : _spaceBattleDefeatDefinitionID;

            return null;
        }

        private static string GetBombardmentDefinitionID(BombardmentResult result)
        {
            bool isNeutralSystem = IsNeutralSystem(result.Planet);
            if (HasBombardmentTargetLosses(result))
            {
                return isNeutralSystem
                    ? _bombardmentNeutralTargetLossesDefinitionID
                    : _bombardmentOwnedTargetLossesDefinitionID;
            }

            if (HasBombardmentAttackerLosses(result))
            {
                return isNeutralSystem
                    ? _bombardmentNeutralAttackerLossesDefinitionID
                    : _bombardmentOwnedAttackerLossesDefinitionID;
            }

            return isNeutralSystem
                ? _bombardmentNeutralNoLossesDefinitionID
                : _bombardmentOwnedNoLossesDefinitionID;
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

        private static string GetPlanetaryAssaultDefinitionID(bool success, bool isNeutralSystem)
        {
            if (isNeutralSystem)
                return success
                    ? _assaultNeutralSuccessDefinitionID
                    : _assaultNeutralFailedDefinitionID;

            return success
                ? _assaultCapturedOwnedSystemDefinitionID
                : _assaultDefendedOwnedSystemDefinitionID;
        }

        private static bool IsNeutralSystem(Planet planet)
        {
            return string.IsNullOrEmpty(planet?.OwnerInstanceID);
        }

        private static bool IsNeutralAssaultSystem(PlanetaryAssaultResult result)
        {
            if (result?.OwnershipChange != null)
                return result.OwnershipChange.PreviousOwner == null;

            return IsNeutralSystem(result?.Planet);
        }
    }
}
