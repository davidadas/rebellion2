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
                    MessageResultType.FleetArrived,
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
                    MessageResultType.ShipsArrived,
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
        public void CreateFacilityDeployed_Mine_UsesMatchingBuildingDefinition()
        {
            MessageFactory factory = CreateFactory(
                Definition(
                    MessageResultType.FacilityDeployed,
                    MessageType.Resource,
                    "mine:{item}:{system}",
                    "body:{item}:{system}",
                    DefaultImage("mine-image"),
                    buildingType: BuildingType.Mine
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
                    MessageResultType.FacilityDeployed,
                    MessageType.Resource,
                    "mine:{item}:{system}",
                    "body:{item}:{system}",
                    DefaultImage("mine-image"),
                    buildingType: BuildingType.Mine
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
                    MessageResultType.ManufacturingIdle,
                    MessageType.Manufacturing,
                    "construction:{system}",
                    "body:{system}",
                    DefaultImage("construction-image"),
                    manufacturingType: ManufacturingType.Building
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
                    MessageResultType.MissionReport,
                    MessageType.Mission,
                    "success:{mission}:{system}",
                    "body:{mission}:{system}",
                    FactionImages(),
                    outcome: MessageResultOutcome.Success
                ),
                Definition(
                    MessageResultType.MissionReport,
                    MessageType.Mission,
                    "failed:{mission}:{system}",
                    "body:{mission}:{system}",
                    FactionImages(),
                    outcome: MessageResultOutcome.Failed
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
        public void CreateMissionReport_Foiled_UsesFailedDefinitionForActor()
        {
            MessageFactory factory = CreateFactory(
                Definition(
                    MessageResultType.MissionReport,
                    MessageType.Mission,
                    "failed:{mission}:{system}",
                    "body:{mission}:{system}",
                    FactionImages(),
                    outcome: MessageResultOutcome.Failed
                )
            );
            MissionCompletedResult result = new MissionCompletedResult
            {
                MissionName = "Sabotage",
                Outcome = MissionOutcome.Foiled,
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
                    MessageResultType.EnemyMissionFoiled,
                    MessageType.Mission,
                    "foiled:{mission}:{system}",
                    "body:{mission}:{system}",
                    FactionImages(),
                    outcome: MessageResultOutcome.Foiled
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
                    MessageResultType.EnemyMissionFoiled,
                    MessageType.Mission,
                    "foiled:{mission}:{system}",
                    "body:{mission}:{system}",
                    FactionImages(),
                    outcome: MessageResultOutcome.Foiled
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
                    MessageResultType.SabotageStrike,
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
                    MessageResultType.ResearchComplete,
                    MessageType.Manufacturing,
                    "ship:{item}",
                    "body:{item}",
                    researchDiscipline: ResearchDiscipline.ShipDesign
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
        public void CreateResearchExhausted_TroopTraining_UsesTroopDefinition()
        {
            MessageFactory factory = CreateFactory(
                Definition(
                    MessageResultType.ResearchExhausted,
                    MessageType.Manufacturing,
                    "troop-exhausted",
                    "body",
                    DefaultImage("research-image"),
                    researchDiscipline: ResearchDiscipline.TroopTraining
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
        public void CreateUprisingEnded_ControllerDiffersFromRecipient_UsesControllerImage()
        {
            MessageFactory factory = CreateFactory(
                Definition(
                    MessageResultType.UprisingEnded,
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
                    MessageResultType.BlockadeInitiated,
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
        public void CreateEvacuationLosses_MultipleUnitTypes_JoinsUnitNames()
        {
            MessageFactory factory = CreateFactory(
                Definition(
                    MessageResultType.EvacuationLosses,
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
                    MessageResultType.MaintenanceAutoscrap,
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
        public void CreatePlanetaryAssault_OwnedSystemDefended_UsesOwnedFailedDefinitionAndAttackerImage()
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
            Assert.AreEqual("owned-failed:Alliance:Empire:Coruscant", message.Title);
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
            MessageResultType resultType,
            MessageType messageType,
            string titleTemplate,
            string bodyTemplate,
            MessageImageMap imageMap = null,
            MessageResultOutcome outcome = MessageResultOutcome.None,
            MessagePlanetOwnership planetOwnership = MessagePlanetOwnership.None,
            BuildingType buildingType = BuildingType.None,
            ManufacturingType manufacturingType = ManufacturingType.None,
            ResearchDiscipline? researchDiscipline = null
        )
        {
            MessageDefinition definition = new MessageDefinition
            {
                ResultType = resultType,
                MessageType = messageType,
                Outcome = outcome,
                PlanetOwnership = planetOwnership,
                BuildingType = buildingType,
                ManufacturingType = manufacturingType,
                TitleTemplate = titleTemplate,
                BodyTemplate = bodyTemplate,
                ImageMap = imageMap,
            };

            if (researchDiscipline.HasValue)
                definition.ResearchDiscipline = researchDiscipline.Value;

            return definition;
        }

        private static MessageDefinition[] SpaceBattleDefinitions()
        {
            return new[]
            {
                Definition(
                    MessageResultType.SpaceBattle,
                    MessageType.Conflict,
                    "victory:{faction}:{opponent}:{system}",
                    "body:{faction}:{opponent}:{system}",
                    FactionImages(),
                    outcome: MessageResultOutcome.Victory
                ),
                Definition(
                    MessageResultType.SpaceBattle,
                    MessageType.Conflict,
                    "defeat:{faction}:{opponent}:{system}",
                    "body:{faction}:{opponent}:{system}",
                    FactionImages(),
                    outcome: MessageResultOutcome.Defeat
                ),
                Definition(
                    MessageResultType.SpaceBattle,
                    MessageType.Conflict,
                    "draw:{faction}:{opponent}:{system}",
                    "body:{faction}:{opponent}:{system}",
                    FactionImages(),
                    outcome: MessageResultOutcome.Stalemate
                ),
            };
        }

        private static MessageDefinition[] BombardmentDefinitions()
        {
            return new[]
            {
                Definition(
                    MessageResultType.Bombardment,
                    MessageType.Conflict,
                    "owned-none:{faction}:{target}:{system}",
                    "body:{faction}:{target}:{system}",
                    DefaultImage("no-losses-image"),
                    outcome: MessageResultOutcome.NoLosses,
                    planetOwnership: MessagePlanetOwnership.Owned
                ),
                Definition(
                    MessageResultType.Bombardment,
                    MessageType.Conflict,
                    "owned-target:{faction}:{target}:{system}",
                    "body:{faction}:{target}:{system}",
                    DefaultImage("target-losses-image"),
                    outcome: MessageResultOutcome.TargetLosses,
                    planetOwnership: MessagePlanetOwnership.Owned
                ),
                Definition(
                    MessageResultType.Bombardment,
                    MessageType.Conflict,
                    "owned-attacker:{faction}:{target}:{system}",
                    "body:{faction}:{target}:{system}",
                    DefaultImage("attacker-losses-image"),
                    outcome: MessageResultOutcome.AttackerLosses,
                    planetOwnership: MessagePlanetOwnership.Owned
                ),
                Definition(
                    MessageResultType.Bombardment,
                    MessageType.Conflict,
                    "neutral-none:{faction}:{target}:{system}",
                    "body:{faction}:{target}:{system}",
                    DefaultImage("no-losses-image"),
                    outcome: MessageResultOutcome.NoLosses,
                    planetOwnership: MessagePlanetOwnership.Neutral
                ),
                Definition(
                    MessageResultType.Bombardment,
                    MessageType.Conflict,
                    "neutral-target:{faction}:{target}:{system}",
                    "body:{faction}:{target}:{system}",
                    DefaultImage("target-losses-image"),
                    outcome: MessageResultOutcome.TargetLosses,
                    planetOwnership: MessagePlanetOwnership.Neutral
                ),
                Definition(
                    MessageResultType.Bombardment,
                    MessageType.Conflict,
                    "neutral-attacker:{faction}:{target}:{system}",
                    "body:{faction}:{target}:{system}",
                    DefaultImage("attacker-losses-image"),
                    outcome: MessageResultOutcome.AttackerLosses,
                    planetOwnership: MessagePlanetOwnership.Neutral
                ),
            };
        }

        private static MessageDefinition[] AssaultDefinitions()
        {
            return new[]
            {
                Definition(
                    MessageResultType.PlanetaryAssault,
                    MessageType.Conflict,
                    "owned-success:{faction}:{target}:{system}",
                    "body:{faction}:{target}:{system}",
                    FactionImages(),
                    outcome: MessageResultOutcome.Success,
                    planetOwnership: MessagePlanetOwnership.Owned
                ),
                Definition(
                    MessageResultType.PlanetaryAssault,
                    MessageType.Conflict,
                    "owned-failed:{faction}:{target}:{system}",
                    "body:{faction}:{target}:{system}",
                    FactionImages(),
                    outcome: MessageResultOutcome.Failed,
                    planetOwnership: MessagePlanetOwnership.Owned
                ),
                Definition(
                    MessageResultType.PlanetaryAssault,
                    MessageType.Conflict,
                    "neutral-success:{faction}:{target}:{system}",
                    "body:{faction}:{target}:{system}",
                    FactionImages(),
                    outcome: MessageResultOutcome.Success,
                    planetOwnership: MessagePlanetOwnership.Neutral
                ),
                Definition(
                    MessageResultType.PlanetaryAssault,
                    MessageType.Conflict,
                    "neutral-failed:{faction}:{target}:{system}",
                    "body:{faction}:{target}:{system}",
                    FactionImages(),
                    outcome: MessageResultOutcome.Failed,
                    planetOwnership: MessagePlanetOwnership.Neutral
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
