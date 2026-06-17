using NUnit.Framework;
using Rebellion.Game.Factions;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Messages;
using Rebellion.Game.Missions;
using Rebellion.Game.Research;
using Rebellion.Game.Results;
using Rebellion.Game.Units;
using Rebellion.Systems;

namespace Rebellion.Tests.Game.Messages
{
    [TestFixture]
    public class MessageFactoryTests
    {
        [Test]
        public void CreateFleetArrived_FleetAndDestination_InterpolatesTemplate()
        {
            MessageFactory factory = CreateFactory(
                Definition(
                    "MESSAGE_FLEET_ARRIVED",
                    MessageType.Fleet,
                    "arrived:{fleet}:{system}",
                    "body:{fleet}:{system}",
                    FactionImages()
                )
            );
            Faction alliance = Alliance();
            Fleet fleet = new Fleet { DisplayName = "Fleet 1" };
            Planet destination = new Planet { DisplayName = "Coruscant" };

            Message message = factory.CreateFleetArrived(alliance, fleet, destination);

            Assert.AreEqual(MessageType.Fleet, message.Type);
            Assert.AreEqual("arrived:Fleet 1:Coruscant", message.Title);
            Assert.AreEqual("body:Fleet 1:Coruscant", message.Body);
            Assert.AreEqual("alliance-image", message.DisplayImagePath);
        }

        [Test]
        public void CreateShipsArrived_MultipleShips_JoinsShipNames()
        {
            MessageFactory factory = CreateFactory(
                Definition(
                    "MESSAGE_SHIPS_ARRIVED",
                    MessageType.Fleet,
                    "ships:{system}",
                    "body:{ships}",
                    FactionImages()
                )
            );
            Faction empire = Empire();
            Planet destination = new Planet { DisplayName = "Kuat" };
            CapitalShip[] ships =
            {
                new CapitalShip { DisplayName = "Imperial I" },
                new CapitalShip { DisplayName = "Imperial II" },
            };

            Message message = factory.CreateShipsArrived(empire, ships, destination);

            Assert.AreEqual(MessageType.Fleet, message.Type);
            Assert.AreEqual("ships:Kuat", message.Title);
            Assert.AreEqual("body:Imperial I\nImperial II", message.Body);
            Assert.AreEqual("empire-image", message.DisplayImagePath);
        }

        [Test]
        public void CreateEmperorSeatOfPower_NoInputs_UsesStaticDefinition()
        {
            MessageFactory factory = CreateFactory(
                Definition(
                    "MESSAGE_EMPEROR_SEAT_OF_POWER",
                    MessageType.PopularSupport,
                    "seat",
                    "body",
                    DefaultImage("seat-image")
                )
            );

            Message message = factory.CreateEmperorSeatOfPower(Empire());

            Assert.AreEqual(MessageType.PopularSupport, message.Type);
            Assert.AreEqual("seat", message.Title);
            Assert.AreEqual("body", message.Body);
            Assert.AreEqual("seat-image", message.DisplayImagePath);
        }

        [Test]
        public void CreateFacilityDeployed_Mine_UsesMatchingBuildingDefinition()
        {
            MessageFactory factory = CreateFactory(
                Definition(
                    "MESSAGE_MINE_DEPLOYED",
                    MessageType.Resource,
                    "mine:{item}:{system}",
                    "body:{item}:{system}",
                    DefaultImage("mine-image"),
                    BuildingType.Mine
                )
            );
            Building mine = new Building { DisplayName = "Mine", BuildingType = BuildingType.Mine };
            Planet destination = new Planet { DisplayName = "Yavin" };

            Message message = factory.CreateFacilityDeployed(Alliance(), mine, destination);

            Assert.AreEqual(MessageType.Resource, message.Type);
            Assert.AreEqual("mine:Mine:Yavin", message.Title);
            Assert.AreEqual("body:Mine:Yavin", message.Body);
            Assert.AreEqual("mine-image", message.DisplayImagePath);
        }

        [Test]
        public void CreateFacilityDeployed_NoDefinitionForBuildingType_ReturnsNull()
        {
            MessageFactory factory = CreateFactory(
                Definition(
                    "MESSAGE_MINE_DEPLOYED",
                    MessageType.Resource,
                    "mine:{item}:{system}",
                    "body:{item}:{system}",
                    DefaultImage("mine-image"),
                    BuildingType.Mine
                )
            );
            Building shipyard = new Building
            {
                DisplayName = "Shipyard",
                BuildingType = BuildingType.Shipyard,
            };

            Message message = factory.CreateFacilityDeployed(
                Alliance(),
                shipyard,
                new Planet { DisplayName = "Yavin" }
            );

            Assert.IsNull(message);
        }

        [Test]
        public void CreateManufacturingIdle_BuildingQueue_UsesConstructionYardDefinition()
        {
            MessageFactory factory = CreateFactory(
                Definition(
                    "MESSAGE_CONSTRUCTION_YARD_IDLE",
                    MessageType.Manufacturing,
                    "construction:{system}",
                    "body:{system}",
                    DefaultImage("construction-image")
                )
            );

            Message message = factory.CreateManufacturingIdle(
                Alliance(),
                ManufacturingType.Building,
                new Planet { DisplayName = "Yavin" }
            );

            Assert.AreEqual(MessageType.Manufacturing, message.Type);
            Assert.AreEqual("construction:Yavin", message.Title);
            Assert.AreEqual("body:Yavin", message.Body);
            Assert.AreEqual("construction-image", message.DisplayImagePath);
        }

        [Test]
        public void CreateMissionReport_Success_UsesSuccessDefinition()
        {
            MessageFactory factory = CreateFactory(
                Definition(
                    "MESSAGE_MISSION_REPORT_SUCCESS",
                    MessageType.Mission,
                    "success:{mission}:{system}",
                    "body:{mission}:{system}",
                    FactionImages()
                ),
                Definition(
                    "MESSAGE_MISSION_REPORT_FAILED",
                    MessageType.Mission,
                    "failed:{mission}:{system}",
                    "body:{mission}:{system}",
                    FactionImages()
                )
            );
            MissionCompletedResult result = new MissionCompletedResult
            {
                MissionName = "Sabotage",
                Outcome = MissionOutcome.Success,
            };

            Message message = factory.CreateMissionReport(
                Alliance(),
                result,
                new Planet { DisplayName = "Yavin" }
            );

            Assert.AreEqual(MessageType.Mission, message.Type);
            Assert.AreEqual("success:Sabotage:Yavin", message.Title);
            Assert.AreEqual("body:Sabotage:Yavin", message.Body);
            Assert.AreEqual("alliance-image", message.DisplayImagePath);
        }

        [Test]
        public void CreateMissionReport_Failure_UsesFailedDefinition()
        {
            MessageFactory factory = CreateFactory(
                Definition(
                    "MESSAGE_MISSION_REPORT_SUCCESS",
                    MessageType.Mission,
                    "success:{mission}:{system}",
                    "body:{mission}:{system}",
                    FactionImages()
                ),
                Definition(
                    "MESSAGE_MISSION_REPORT_FAILED",
                    MessageType.Mission,
                    "failed:{mission}:{system}",
                    "body:{mission}:{system}",
                    FactionImages()
                )
            );
            MissionCompletedResult result = new MissionCompletedResult
            {
                MissionName = "Sabotage",
                Outcome = MissionOutcome.Failed,
            };

            Message message = factory.CreateMissionReport(Alliance(), result, null);

            Assert.AreEqual(MessageType.Mission, message.Type);
            Assert.AreEqual("failed:Sabotage:", message.Title);
            Assert.AreEqual("body:Sabotage:", message.Body);
            Assert.AreEqual("alliance-image", message.DisplayImagePath);
        }

        [Test]
        public void CreateEnemyMissionFoiled_FoiledMission_UsesFoiledDefinition()
        {
            MessageFactory factory = CreateFactory(
                Definition(
                    "MESSAGE_ENEMY_MISSION_FOILED",
                    MessageType.Mission,
                    "foiled:{mission}:{system}",
                    "body:{mission}:{system}",
                    FactionImages()
                )
            );
            MissionCompletedResult result = new MissionCompletedResult
            {
                MissionName = "Sabotage",
                TargetName = "Coruscant",
                Outcome = MissionOutcome.Foiled,
            };

            Message message = factory.CreateEnemyMissionFoiled(Empire(), result, null);

            Assert.AreEqual(MessageType.Mission, message.Type);
            Assert.AreEqual("foiled:Sabotage:Coruscant", message.Title);
            Assert.AreEqual("body:Sabotage:Coruscant", message.Body);
            Assert.AreEqual("empire-image", message.DisplayImagePath);
        }

        [Test]
        public void CreateEnemyMissionFoiled_SuccessfulMission_ReturnsNull()
        {
            MessageFactory factory = CreateFactory(
                Definition(
                    "MESSAGE_ENEMY_MISSION_FOILED",
                    MessageType.Mission,
                    "foiled:{mission}:{system}",
                    "body:{mission}:{system}",
                    FactionImages()
                )
            );
            MissionCompletedResult result = new MissionCompletedResult
            {
                MissionName = "Sabotage",
                Outcome = MissionOutcome.Success,
            };

            Message message = factory.CreateEnemyMissionFoiled(Empire(), result, null);

            Assert.IsNull(message);
        }

        [Test]
        public void CreateSabotageStrike_SabotagedObject_InterpolatesItemAndSystem()
        {
            MessageFactory factory = CreateFactory(
                Definition(
                    "MESSAGE_SABOTAGE_STRIKE",
                    MessageType.Mission,
                    "sabotage:{item}:{system}",
                    "body:{item}:{system}",
                    FactionImages()
                )
            );
            GameObjectSabotagedResult result = new GameObjectSabotagedResult
            {
                SabotagedObject = new Building { DisplayName = "Shield Generator" },
            };

            Message message = factory.CreateSabotageStrike(
                Empire(),
                result,
                new Planet { DisplayName = "Coruscant" }
            );

            Assert.AreEqual(MessageType.Mission, message.Type);
            Assert.AreEqual("sabotage:Shield Generator:Coruscant", message.Title);
            Assert.AreEqual("body:Shield Generator:Coruscant", message.Body);
            Assert.AreEqual("empire-image", message.DisplayImagePath);
        }

        [Test]
        public void CreateResearchComplete_ShipDesign_UsesShipDefinitionAndNoImage()
        {
            MessageFactory factory = CreateFactory(
                Definition(
                    "MESSAGE_RESEARCH_COMPLETE_SHIP",
                    MessageType.Manufacturing,
                    "ship:{item}",
                    "body:{item}"
                )
            );
            ResearchOrderedResult result = new ResearchOrderedResult
            {
                Discipline = ResearchDiscipline.ShipDesign,
                Technology = new Technology(new CapitalShip { DisplayName = "Nebulon-B Frigate" }),
            };

            Message message = factory.CreateResearchComplete(Alliance(), result);

            Assert.AreEqual(MessageType.Manufacturing, message.Type);
            Assert.AreEqual("ship:Nebulon-B Frigate", message.Title);
            Assert.AreEqual("body:Nebulon-B Frigate", message.Body);
            Assert.IsNull(message.DisplayImagePath);
        }

        [Test]
        public void CreateResearchComplete_TroopTraining_UsesTroopDefinition()
        {
            MessageFactory factory = CreateFactory(
                Definition(
                    "MESSAGE_RESEARCH_COMPLETE_TROOP",
                    MessageType.Manufacturing,
                    "troop:{item}",
                    "body:{item}"
                )
            );
            ResearchOrderedResult result = new ResearchOrderedResult
            {
                Discipline = ResearchDiscipline.TroopTraining,
                Technology = new Technology(new Regiment { DisplayName = "SpecForce Regiment" }),
            };

            Message message = factory.CreateResearchComplete(Alliance(), result);

            Assert.AreEqual(MessageType.Manufacturing, message.Type);
            Assert.AreEqual("troop:SpecForce Regiment", message.Title);
            Assert.AreEqual("body:SpecForce Regiment", message.Body);
            Assert.IsNull(message.DisplayImagePath);
        }

        [Test]
        public void CreateResearchExhausted_TroopTraining_UsesTroopDefinition()
        {
            MessageFactory factory = CreateFactory(
                Definition(
                    "MESSAGE_RESEARCH_EXHAUSTED_TROOP",
                    MessageType.Manufacturing,
                    "troop-exhausted",
                    "body",
                    DefaultImage("research-image")
                )
            );
            ResearchExhaustedResult result = new ResearchExhaustedResult
            {
                Discipline = ResearchDiscipline.TroopTraining,
            };

            Message message = factory.CreateResearchExhausted(Alliance(), result);

            Assert.AreEqual(MessageType.Manufacturing, message.Type);
            Assert.AreEqual("troop-exhausted", message.Title);
            Assert.AreEqual("body", message.Body);
            Assert.AreEqual("research-image", message.DisplayImagePath);
        }

        [Test]
        public void CreateUprisingStarted_Controller_InterpolatesControllerAndSystem()
        {
            MessageFactory factory = CreateFactory(
                Definition(
                    "MESSAGE_UPRISING_STARTED",
                    MessageType.PopularSupport,
                    "started:{faction}:{system}",
                    "body:{faction}:{system}",
                    DefaultImage("uprising-image")
                )
            );
            PlanetUprisingStartedResult result = new PlanetUprisingStartedResult
            {
                Planet = new Planet { DisplayName = "Yavin" },
            };

            Message message = factory.CreateUprisingStarted(Alliance(), result, Empire());

            Assert.AreEqual(MessageType.PopularSupport, message.Type);
            Assert.AreEqual("started:Empire:Yavin", message.Title);
            Assert.AreEqual("body:Empire:Yavin", message.Body);
            Assert.AreEqual("uprising-image", message.DisplayImagePath);
        }

        [Test]
        public void CreateUprisingEnded_ControllerDiffersFromRecipient_UsesControllerImage()
        {
            MessageFactory factory = CreateFactory(
                Definition(
                    "MESSAGE_UPRISING_ENDED",
                    MessageType.PopularSupport,
                    "ended:{faction}:{system}",
                    "body:{faction}:{system}",
                    FactionImages()
                )
            );
            PlanetUprisingEndedResult result = new PlanetUprisingEndedResult
            {
                Planet = new Planet { DisplayName = "Coruscant" },
            };

            Message message = factory.CreateUprisingEnded(Alliance(), result, Empire());

            Assert.AreEqual(MessageType.PopularSupport, message.Type);
            Assert.AreEqual("ended:Empire:Coruscant", message.Title);
            Assert.AreEqual("body:Empire:Coruscant", message.Body);
            Assert.AreEqual("empire-image", message.DisplayImagePath);
        }

        [Test]
        public void CreateBlockadeInitiated_TargetFactionDiffersFromActor_UsesTargetImage()
        {
            MessageFactory factory = CreateFactory(
                Definition(
                    "MESSAGE_BLOCKADE_INITIATED",
                    MessageType.Fleet,
                    "initiated:{faction}:{target}:{fleet}:{system}",
                    "body:{faction}:{target}:{fleet}:{system}",
                    FactionImages()
                )
            );
            BlockadeChangedResult result = new BlockadeChangedResult
            {
                Planet = new Planet { DisplayName = "Coruscant" },
                BlockadingFleet = new Fleet { DisplayName = "Fleet 1" },
                Blockaded = true,
            };

            Message message = factory.CreateBlockadeInitiated(Alliance(), result, Empire());

            Assert.AreEqual(MessageType.Fleet, message.Type);
            Assert.AreEqual("initiated:Alliance:Empire:Fleet 1:Coruscant", message.Title);
            Assert.AreEqual("body:Alliance:Empire:Fleet 1:Coruscant", message.Body);
            Assert.AreEqual("empire-image", message.DisplayImagePath);
        }

        [Test]
        public void CreateBlockadeDetected_BlockadingFaction_UsesRecipientImage()
        {
            MessageFactory factory = CreateFactory(
                Definition(
                    "MESSAGE_BLOCKADE_DETECTED",
                    MessageType.Fleet,
                    "detected:{faction}:{fleet}:{system}",
                    "body:{faction}:{fleet}:{system}",
                    FactionImages()
                )
            );
            BlockadeChangedResult result = new BlockadeChangedResult
            {
                Planet = new Planet { DisplayName = "Coruscant" },
                BlockadingFleet = new Fleet { DisplayName = "Fleet 1" },
                Blockaded = true,
            };

            Message message = factory.CreateBlockadeDetected(Empire(), result, Alliance());

            Assert.AreEqual(MessageType.Fleet, message.Type);
            Assert.AreEqual("detected:Alliance:Fleet 1:Coruscant", message.Title);
            Assert.AreEqual("body:Alliance:Fleet 1:Coruscant", message.Body);
            Assert.AreEqual("empire-image", message.DisplayImagePath);
        }

        [Test]
        public void CreateEvacuationLosses_MultipleUnitTypes_JoinsUnitNames()
        {
            MessageFactory factory = CreateFactory(
                Definition(
                    "MESSAGE_EVACUATION_LOSSES",
                    MessageType.Fleet,
                    "losses:{system}",
                    "body:{units}",
                    FactionImages()
                )
            );
            EvacuationLossesResult result = new EvacuationLossesResult
            {
                Location = new Planet { DisplayName = "Coruscant" },
                LostShips = { new CapitalShip { DisplayName = "Nebulon-B Frigate" } },
                LostStarfighters = { new Starfighter { DisplayName = "X-wing Squadron" } },
                LostRegiments = { new Regiment { DisplayName = "Infantry Regiment" } },
            };

            Message message = factory.CreateEvacuationLosses(Alliance(), result);

            Assert.AreEqual(MessageType.Fleet, message.Type);
            Assert.AreEqual("losses:Coruscant", message.Title);
            Assert.AreEqual(
                "body:Nebulon-B Frigate\nX-wing Squadron\nInfantry Regiment",
                message.Body
            );
            Assert.AreEqual("alliance-image", message.DisplayImagePath);
        }

        [Test]
        public void CreateMaintenanceAutoscrap_DestroyedObject_InterpolatesItemAndSystem()
        {
            MessageFactory factory = CreateFactory(
                Definition(
                    "MESSAGE_MAINTENANCE_AUTOSCRAP",
                    MessageType.Resource,
                    "maintenance:{item}:{system}",
                    "body:{item}:{system}",
                    FactionImages()
                )
            );
            GameObjectAutoscrappedResult result = new GameObjectAutoscrappedResult
            {
                DestroyedObject = new Building { DisplayName = "Shipyard" },
            };

            Message message = factory.CreateMaintenanceAutoscrap(
                Alliance(),
                result,
                new Planet { DisplayName = "Coruscant" }
            );

            Assert.AreEqual(MessageType.Resource, message.Type);
            Assert.AreEqual("maintenance:Shipyard:Coruscant", message.Title);
            Assert.AreEqual("body:Shipyard:Coruscant", message.Body);
            Assert.AreEqual("alliance-image", message.DisplayImagePath);
        }

        [Test]
        public void CreateSpaceBattle_AttackerVictoryForAttacker_UsesVictoryDefinition()
        {
            MessageFactory factory = CreateFactory(SpaceBattleDefinitions());
            SpaceCombatResult result = new SpaceCombatResult
            {
                AttackerFleet = new Fleet { OwnerInstanceID = Alliance().InstanceID },
                DefenderFleet = new Fleet { OwnerInstanceID = Empire().InstanceID },
                Planet = new Planet { DisplayName = "Yavin" },
                Winner = CombatSide.Attacker,
            };

            Message message = factory.CreateSpaceBattle(Alliance(), result, Empire());

            Assert.AreEqual(MessageType.Conflict, message.Type);
            Assert.AreEqual("victory:Alliance:Empire:Yavin", message.Title);
            Assert.AreEqual("body:Alliance:Empire:Yavin", message.Body);
            Assert.AreEqual("alliance-image", message.DisplayImagePath);
        }

        [Test]
        public void CreateSpaceBattle_AttackerVictoryForDefender_UsesDefeatDefinition()
        {
            MessageFactory factory = CreateFactory(SpaceBattleDefinitions());
            SpaceCombatResult result = new SpaceCombatResult
            {
                AttackerFleet = new Fleet { OwnerInstanceID = Alliance().InstanceID },
                DefenderFleet = new Fleet { OwnerInstanceID = Empire().InstanceID },
                Planet = new Planet { DisplayName = "Yavin" },
                Winner = CombatSide.Attacker,
            };

            Message message = factory.CreateSpaceBattle(Empire(), result, Alliance());

            Assert.AreEqual(MessageType.Conflict, message.Type);
            Assert.AreEqual("defeat:Empire:Alliance:Yavin", message.Title);
            Assert.AreEqual("body:Empire:Alliance:Yavin", message.Body);
            Assert.AreEqual("empire-image", message.DisplayImagePath);
        }

        [Test]
        public void CreateBombardment_OwnedSystemWithTargetLosses_UsesOwnedTargetLossesDefinition()
        {
            MessageFactory factory = CreateFactory(BombardmentDefinitions());
            BombardmentResult result = new BombardmentResult
            {
                AttackingFaction = Alliance(),
                Planet = new Planet
                {
                    DisplayName = "Coruscant",
                    OwnerInstanceID = Empire().InstanceID,
                },
                DestroyedBuildings = { new Building { DisplayName = "Shield Generator" } },
            };

            Message message = factory.CreateBombardment(Alliance(), result, Empire());

            Assert.AreEqual(MessageType.Conflict, message.Type);
            Assert.AreEqual("owned-target:Alliance:Empire:Coruscant", message.Title);
            Assert.AreEqual("body:Alliance:Empire:Coruscant", message.Body);
            Assert.AreEqual("target-losses-image", message.DisplayImagePath);
        }

        [Test]
        public void CreateBombardment_OwnedSystemWithAttackerLosses_UsesOwnedAttackerLossesDefinition()
        {
            MessageFactory factory = CreateFactory(BombardmentDefinitions());
            BombardmentResult result = new BombardmentResult
            {
                AttackingFaction = Alliance(),
                Planet = new Planet
                {
                    DisplayName = "Coruscant",
                    OwnerInstanceID = Empire().InstanceID,
                },
                DestroyedRegiments =
                {
                    new Regiment
                    {
                        DisplayName = "Alliance Regiment",
                        OwnerInstanceID = Alliance().InstanceID,
                    },
                },
            };

            Message message = factory.CreateBombardment(Alliance(), result, Empire());

            Assert.AreEqual(MessageType.Conflict, message.Type);
            Assert.AreEqual("owned-attacker:Alliance:Empire:Coruscant", message.Title);
            Assert.AreEqual("body:Alliance:Empire:Coruscant", message.Body);
            Assert.AreEqual("attacker-losses-image", message.DisplayImagePath);
        }

        [Test]
        public void CreateBombardment_NeutralSystemWithoutLosses_UsesNeutralNoLossesDefinition()
        {
            MessageFactory factory = CreateFactory(BombardmentDefinitions());
            BombardmentResult result = new BombardmentResult
            {
                AttackingFaction = Alliance(),
                Planet = new Planet { DisplayName = "Ord Mantell" },
            };

            Message message = factory.CreateBombardment(Alliance(), result, null);

            Assert.AreEqual(MessageType.Conflict, message.Type);
            Assert.AreEqual("neutral-none:Alliance::Ord Mantell", message.Title);
            Assert.AreEqual("body:Alliance::Ord Mantell", message.Body);
            Assert.AreEqual("no-losses-image", message.DisplayImagePath);
        }

        [Test]
        public void CreatePlanetaryAssault_OwnedSystemDefended_UsesDefendedDefinitionAndAttackerImage()
        {
            MessageFactory factory = CreateFactory(AssaultDefinitions());
            PlanetaryAssaultResult result = new PlanetaryAssaultResult
            {
                AttackingFaction = Alliance(),
                Planet = new Planet
                {
                    DisplayName = "Coruscant",
                    OwnerInstanceID = Empire().InstanceID,
                },
                Success = false,
            };

            Message message = factory.CreatePlanetaryAssault(Empire(), result, Empire());

            Assert.AreEqual(MessageType.Conflict, message.Type);
            Assert.AreEqual("owned-defended:Alliance:Empire:Coruscant", message.Title);
            Assert.AreEqual("body:Alliance:Empire:Coruscant", message.Body);
            Assert.AreEqual("alliance-image", message.DisplayImagePath);
        }

        [Test]
        public void CreatePlanetaryAssault_NeutralSuccess_UsesNeutralSuccessDefinition()
        {
            MessageFactory factory = CreateFactory(AssaultDefinitions());
            PlanetaryAssaultResult result = new PlanetaryAssaultResult
            {
                AttackingFaction = Alliance(),
                Planet = new Planet { DisplayName = "Ord Mantell" },
                Success = true,
            };

            Message message = factory.CreatePlanetaryAssault(Alliance(), result, null);

            Assert.AreEqual(MessageType.Conflict, message.Type);
            Assert.AreEqual("neutral-success:Alliance::Ord Mantell", message.Title);
            Assert.AreEqual("body:Alliance::Ord Mantell", message.Body);
            Assert.AreEqual("alliance-image", message.DisplayImagePath);
        }

        private static MessageFactory CreateFactory(params MessageDefinition[] definitions)
        {
            return new MessageFactory(definitions);
        }

        private static MessageDefinition Definition(
            string instanceID,
            MessageType messageType,
            string titleTemplate,
            string bodyTemplate,
            MessageImageMap imageMap = null,
            BuildingType buildingType = BuildingType.None
        )
        {
            return new MessageDefinition
            {
                InstanceID = instanceID,
                MessageType = messageType,
                BuildingType = buildingType,
                TitleTemplate = titleTemplate,
                BodyTemplate = bodyTemplate,
                ImageMap = imageMap,
            };
        }

        private static MessageDefinition[] SpaceBattleDefinitions()
        {
            return new[]
            {
                Definition(
                    "MESSAGE_SPACE_BATTLE_VICTORY",
                    MessageType.Conflict,
                    "victory:{faction}:{opponent}:{system}",
                    "body:{faction}:{opponent}:{system}",
                    FactionImages()
                ),
                Definition(
                    "MESSAGE_SPACE_BATTLE_DEFEAT",
                    MessageType.Conflict,
                    "defeat:{faction}:{opponent}:{system}",
                    "body:{faction}:{opponent}:{system}",
                    FactionImages()
                ),
                Definition(
                    "MESSAGE_SPACE_BATTLE_STALEMATE",
                    MessageType.Conflict,
                    "draw:{faction}:{opponent}:{system}",
                    "body:{faction}:{opponent}:{system}",
                    FactionImages()
                ),
            };
        }

        private static MessageDefinition[] BombardmentDefinitions()
        {
            return new[]
            {
                Definition(
                    "MESSAGE_BOMBARDMENT_OWNED_NO_LOSSES",
                    MessageType.Conflict,
                    "owned-none:{faction}:{target}:{system}",
                    "body:{faction}:{target}:{system}",
                    DefaultImage("no-losses-image")
                ),
                Definition(
                    "MESSAGE_BOMBARDMENT_OWNED_TARGET_LOSSES",
                    MessageType.Conflict,
                    "owned-target:{faction}:{target}:{system}",
                    "body:{faction}:{target}:{system}",
                    DefaultImage("target-losses-image")
                ),
                Definition(
                    "MESSAGE_BOMBARDMENT_OWNED_ATTACKER_LOSSES",
                    MessageType.Conflict,
                    "owned-attacker:{faction}:{target}:{system}",
                    "body:{faction}:{target}:{system}",
                    DefaultImage("attacker-losses-image")
                ),
                Definition(
                    "MESSAGE_BOMBARDMENT_NEUTRAL_NO_LOSSES",
                    MessageType.Conflict,
                    "neutral-none:{faction}:{target}:{system}",
                    "body:{faction}:{target}:{system}",
                    DefaultImage("no-losses-image")
                ),
                Definition(
                    "MESSAGE_BOMBARDMENT_NEUTRAL_TARGET_LOSSES",
                    MessageType.Conflict,
                    "neutral-target:{faction}:{target}:{system}",
                    "body:{faction}:{target}:{system}",
                    DefaultImage("target-losses-image")
                ),
                Definition(
                    "MESSAGE_BOMBARDMENT_NEUTRAL_ATTACKER_LOSSES",
                    MessageType.Conflict,
                    "neutral-attacker:{faction}:{target}:{system}",
                    "body:{faction}:{target}:{system}",
                    DefaultImage("attacker-losses-image")
                ),
            };
        }

        private static MessageDefinition[] AssaultDefinitions()
        {
            return new[]
            {
                Definition(
                    "MESSAGE_ASSAULT_CAPTURED_OWNED_SYSTEM",
                    MessageType.Conflict,
                    "owned-captured:{faction}:{target}:{system}",
                    "body:{faction}:{target}:{system}",
                    FactionImages()
                ),
                Definition(
                    "MESSAGE_ASSAULT_DEFENDED_OWNED_SYSTEM",
                    MessageType.Conflict,
                    "owned-defended:{faction}:{target}:{system}",
                    "body:{faction}:{target}:{system}",
                    FactionImages()
                ),
                Definition(
                    "MESSAGE_ASSAULT_NEUTRAL_SUCCESS",
                    MessageType.Conflict,
                    "neutral-success:{faction}:{target}:{system}",
                    "body:{faction}:{target}:{system}",
                    FactionImages()
                ),
                Definition(
                    "MESSAGE_ASSAULT_NEUTRAL_FAILED",
                    MessageType.Conflict,
                    "neutral-failed:{faction}:{target}:{system}",
                    "body:{faction}:{target}:{system}",
                    FactionImages()
                ),
            };
        }

        private static MessageImageMap FactionImages()
        {
            return new MessageImageMap
            {
                Default = "default-image",
                FNALL1 = "alliance-image",
                FNEMP1 = "empire-image",
            };
        }

        private static MessageImageMap DefaultImage(string path)
        {
            return new MessageImageMap { Default = path };
        }

        private static Faction Alliance()
        {
            return new Faction { InstanceID = "FNALL1", DisplayName = "Alliance" };
        }

        private static Faction Empire()
        {
            return new Faction { InstanceID = "FNEMP1", DisplayName = "Empire" };
        }
    }
}
