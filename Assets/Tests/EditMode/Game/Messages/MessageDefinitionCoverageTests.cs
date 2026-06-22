using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Rebellion.Game;
using Rebellion.Game.Factions;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Messages;
using Rebellion.Game.Missions;
using Rebellion.Game.Research;
using Rebellion.Game.Results;
using Rebellion.Game.Units;

namespace Rebellion.Tests.Game.Messages
{
    [TestFixture]
    public class MessageDefinitionCoverageTests
    {
        [Test]
        public void CreateMessages_WithConfiguredFleetArrivalDefinition_ReturnsFleetDelivery()
        {
            (GameRoot game, Faction alliance, _, Planet destination) = BuildMessageScene();
            Fleet fleet = new Fleet
            {
                InstanceID = "FLEET1",
                DisplayName = "Fleet 1",
                OwnerInstanceID = alliance.InstanceID,
            };
            game.AttachNode(fleet, destination);

            List<MessageDelivery> deliveries = CreateMessages(
                game,
                new UnitArrivedResult { Unit = fleet, Destination = destination }
            );

            AssertOnlyDeliveries(deliveries, alliance, MessageType.Fleet, 1);
        }

        [Test]
        public void CreateMessages_WithConfiguredShipArrivalDefinition_ReturnsFleetDelivery()
        {
            (GameRoot game, Faction alliance, _, Planet destination) = BuildMessageScene();
            CapitalShip firstShip = new CapitalShip
            {
                InstanceID = "SHIP1",
                DisplayName = "Nebulon-B Frigate",
                OwnerInstanceID = alliance.InstanceID,
            };
            CapitalShip secondShip = new CapitalShip
            {
                InstanceID = "SHIP2",
                DisplayName = "Corellian Corvette",
                OwnerInstanceID = alliance.InstanceID,
            };

            List<MessageDelivery> deliveries = CreateMessages(
                game,
                new UnitArrivedResult { Unit = firstShip, Destination = destination },
                new UnitArrivedResult { Unit = secondShip, Destination = destination }
            );

            AssertOnlyDeliveries(deliveries, alliance, MessageType.Fleet, 1);
        }

        [Test]
        public void CreateMessages_WithConfiguredSeatOfPowerDefinition_ReturnsMissionDelivery()
        {
            (GameRoot game, Faction alliance, _, _) = BuildMessageScene();
            Officer officer = new Officer { OwnerInstanceID = alliance.InstanceID };

            List<MessageDelivery> deliveries = CreateMessages(
                game,
                new SeatOfPowerChangedResult { Officer = officer, IsAtSeat = true }
            );

            AssertOnlyDeliveries(deliveries, alliance, MessageType.Mission, 1);
        }

        [Test]
        public void CreateMessages_WithConfiguredOfficerDefinitions_ReturnsMissionDelivery()
        {
            (GameRoot game, Faction alliance, Planet origin, _) = BuildMessageScene();
            Officer officer = new Officer
            {
                DisplayName = "Agent",
                OwnerInstanceID = alliance.InstanceID,
                MessageImagePath =
                    "Art/UI/Messages/Characters/ui_message_character_alliance_ackbar",
            };
            game.AttachNode(officer, origin);

            List<MessageDelivery> deliveries = CreateMessages(
                game,
                new OfficerRecruitedResult
                {
                    Officer = officer,
                    Faction = alliance,
                    Planet = origin,
                },
                new OfficerInjuredResult { Officer = officer, Severity = 1 },
                new OfficerKilledResult { TargetOfficer = officer, Context = origin }
            );

            AssertOnlyDeliveries(deliveries, alliance, MessageType.Mission, 2);
        }

        [Test]
        public void CreateMessages_WithConfiguredOfficerStatusDefinitions_AssignsDetailImages()
        {
            (GameRoot game, Faction alliance, Planet origin, _) = BuildMessageScene();
            Officer officer = new Officer
            {
                DisplayName = "Agent",
                OwnerInstanceID = alliance.InstanceID,
                MessageImagePath =
                    "Art/UI/Messages/Characters/ui_message_character_alliance_ackbar",
            };
            game.AttachNode(officer, origin);

            AssertMessageImage(
                CreateSingleMessage(
                    game,
                    alliance,
                    new OfficerCaptureStateResult
                    {
                        TargetOfficer = officer,
                        IsCaptured = true,
                        Context = origin,
                    }
                ),
                "Art/UI/Messages/ui_message_character_captured_alliance",
                officer.MessageImagePath
            );
            AssertMessageImage(
                CreateSingleMessage(
                    game,
                    alliance,
                    new OfficerCaptureStateResult
                    {
                        TargetOfficer = officer,
                        IsCaptured = false,
                        Context = origin,
                    }
                ),
                "Art/UI/Messages/ui_message_prisoner_escape",
                officer.MessageImagePath
            );
            AssertMessageImage(
                CreateSingleMessage(
                    game,
                    alliance,
                    new OfficerInjuredResult { Officer = officer, Severity = 1 }
                ),
                "Art/UI/Messages/ui_message_character_injured",
                officer.MessageImagePath
            );
            AssertMessageImage(
                CreateSingleMessage(
                    game,
                    alliance,
                    new OfficerInjuredResult { Officer = officer, Severity = 0 }
                ),
                "Art/UI/Messages/ui_message_character_recovered",
                officer.MessageImagePath
            );
            AssertMessageImage(
                CreateSingleMessage(
                    game,
                    alliance,
                    new OfficerKilledResult { TargetOfficer = officer, Context = origin }
                ),
                "Art/UI/Messages/ui_message_character_killed_alliance",
                officer.MessageImagePath
            );
        }

        [Test]
        public void CreateMessages_WithConfiguredMissionFallbackDefinitions_AssignsDetailImages()
        {
            (GameRoot game, Faction alliance, _, Planet destination) = BuildMessageScene();

            Message jediTraining = CreateSingleMessage(
                game,
                alliance,
                new MissionCompletedResult
                {
                    Mission = new JediTrainingMission { OwnerInstanceID = alliance.InstanceID },
                    TargetName = destination.DisplayName,
                    Outcome = MissionOutcome.Success,
                }
            );
            Message research = CreateSingleMessage(
                game,
                alliance,
                new MissionCompletedResult
                {
                    Mission = new ResearchMission
                    {
                        OwnerInstanceID = alliance.InstanceID,
                        DisplayName = "Ship Design",
                    },
                    TargetName = destination.DisplayName,
                    Outcome = MissionOutcome.Failed,
                }
            );
            Message sabotageFailed = CreateSingleMessage(
                game,
                alliance,
                new MissionCompletedResult
                {
                    Mission = new SabotageMission
                    {
                        OwnerInstanceID = alliance.InstanceID,
                        DisplayName = "Sabotage",
                    },
                    TargetName = destination.DisplayName,
                    Outcome = MissionOutcome.Failed,
                }
            );

            Assert.AreEqual("mission_report", jediTraining.DisplayImageKey);
            Assert.IsNull(jediTraining.DisplayImagePath);
            Assert.AreEqual("mission_report", research.DisplayImageKey);
            Assert.IsNull(research.DisplayImagePath);
            Assert.IsNull(sabotageFailed.DisplayImageKey);
            Assert.AreEqual(
                "Art/UI/Messages/ui_message_mission_report_espionage",
                sabotageFailed.DisplayImagePath
            );
        }

        [Test]
        public void CreateMessages_WithConfiguredForceGrowthDefinition_UsesMissionReportKey()
        {
            (GameRoot game, Faction alliance, Planet origin, _) = BuildMessageScene();
            Officer officer = new Officer
            {
                OwnerInstanceID = alliance.InstanceID,
                IsJedi = true,
                ForceValue = game.Config.Jedi.RankLabelForceKnight,
                MessageImagePath =
                    "Art/UI/Messages/Characters/ui_message_character_alliance_luke_skywalker",
            };
            game.AttachNode(officer, origin);

            Message message = CreateSingleMessage(
                game,
                alliance,
                new ForceExperienceResult { Officer = officer, ExperienceGained = 1 }
            );

            Assert.AreEqual("mission_report", message.DisplayImageKey);
            Assert.IsNull(message.DisplayImagePath);
            Assert.AreEqual(officer.MessageImagePath, message.OverlayImagePath);
        }

        [Test]
        public void CreateMessages_WithConfiguredFacilityDefinition_ReturnsResourceDelivery()
        {
            (GameRoot game, Faction alliance, Planet origin, _) = BuildMessageScene();
            Building mine = new Building
            {
                InstanceID = "MINE1",
                DisplayName = "Mine",
                OwnerInstanceID = alliance.InstanceID,
                BuildingType = BuildingType.Mine,
            };
            game.AttachNode(mine, origin);

            List<MessageDelivery> deliveries = CreateMessages(
                game,
                new GameObjectDeployedResult { GameObject = mine }
            );

            AssertOnlyDeliveries(deliveries, alliance, MessageType.Resource, 1);
        }

        [Test]
        public void CreateMessages_WithConfiguredManufacturingDefinition_ReturnsManufacturingDelivery()
        {
            (GameRoot game, Faction alliance, Planet origin, _) = BuildMessageScene();

            List<MessageDelivery> deliveries = CreateMessages(
                game,
                new ManufacturingCompletedResult
                {
                    Faction = alliance,
                    ProductionPlanet = origin,
                    ProductType = ManufacturingType.Building,
                }
            );

            AssertOnlyDeliveries(deliveries, alliance, MessageType.Manufacturing, 1);
        }

        [Test]
        public void CreateMessages_WithConfiguredSabotageDefinitions_ReturnsActorAndTargetDeliveries()
        {
            (GameRoot game, Faction alliance, Faction empire, _, Planet target) =
                BuildTwoFactionMessageScene();
            SabotageMission mission = new SabotageMission
            {
                OwnerInstanceID = alliance.InstanceID,
                DisplayName = "Sabotage",
            };
            Building building = new Building
            {
                InstanceID = "SHIELD1",
                DisplayName = "Shield Generator",
                OwnerInstanceID = empire.InstanceID,
            };
            game.AttachNode(mission, target);

            List<MessageDelivery> deliveries = CreateMessages(
                game,
                new GameObjectSabotagedResult { SabotagedObject = building, Context = target },
                new MissionCompletedResult
                {
                    Mission = mission,
                    MissionName = "Sabotage",
                    TargetName = target.DisplayName,
                    Outcome = MissionOutcome.Success,
                }
            );

            AssertOnlyDeliveries(deliveries, alliance, MessageType.Mission, 1);
            AssertOnlyDeliveries(deliveries, empire, MessageType.Mission, 1);
        }

        [Test]
        public void CreateMessages_WithConfiguredFoiledMissionDefinitions_ReturnsActorAndTargetDeliveries()
        {
            (GameRoot game, Faction alliance, Faction empire, _, Planet target) =
                BuildTwoFactionMessageScene();
            SabotageMission mission = new SabotageMission
            {
                OwnerInstanceID = alliance.InstanceID,
                DisplayName = "Sabotage",
            };
            game.AttachNode(mission, target);

            List<MessageDelivery> deliveries = CreateMessages(
                game,
                new MissionCompletedResult
                {
                    Mission = mission,
                    MissionName = "Sabotage",
                    TargetName = target.DisplayName,
                    Outcome = MissionOutcome.Foiled,
                }
            );

            AssertOnlyDeliveries(deliveries, alliance, MessageType.Mission, 1);
            AssertOnlyDeliveries(deliveries, empire, MessageType.Mission, 1);
        }

        [Test]
        public void CreateMessages_WithConfiguredResearchDefinitions_ReturnsManufacturingDeliveries()
        {
            (GameRoot game, Faction alliance, _, _) = BuildMessageScene();

            List<MessageDelivery> deliveries = CreateMessages(
                game,
                new ResearchOrderedResult
                {
                    Faction = alliance,
                    Technology = new Technology(
                        new CapitalShip { DisplayName = "Nebulon-B Frigate" }
                    ),
                },
                new ResearchExhaustedResult
                {
                    Faction = alliance,
                    Discipline = ResearchDiscipline.ShipDesign,
                }
            );

            AssertOnlyDeliveries(deliveries, alliance, MessageType.Manufacturing, 2);
        }

        [Test]
        public void CreateMessages_WithConfiguredUprisingDefinition_ReturnsControllerAndInstigatorDeliveries()
        {
            (GameRoot game, Faction alliance, Faction empire, _, Planet target) =
                BuildTwoFactionMessageScene();

            List<MessageDelivery> deliveries = CreateMessages(
                game,
                new PlanetUprisingStartedResult { Planet = target, InstigatorFaction = alliance }
            );

            AssertOnlyDeliveries(deliveries, empire, MessageType.PopularSupport, 1);
            AssertOnlyDeliveries(deliveries, alliance, MessageType.PopularSupport, 1);
            Message message = deliveries.First(delivery => delivery.Faction == alliance).Message;
            Assert.AreEqual("mission_report", message.DisplayImageKey);
            Assert.IsNull(message.DisplayImagePath);
        }

        [Test]
        public void CreateMessages_WithConfiguredUprisingEndedDefinition_ReturnsControllerDelivery()
        {
            (GameRoot game, _, Faction empire, _, Planet target) = BuildTwoFactionMessageScene();

            List<MessageDelivery> deliveries = CreateMessages(
                game,
                new PlanetUprisingEndedResult { Planet = target }
            );

            AssertOnlyDeliveries(deliveries, empire, MessageType.PopularSupport, 1);
        }

        [Test]
        public void CreateMessages_WithConfiguredPlanetJoinedBySupportDefinition_ReturnsNewOwnerDelivery()
        {
            (GameRoot game, Faction alliance, _, _, Planet target) = BuildTwoFactionMessageScene();
            target.OwnerInstanceID = null;

            List<MessageDelivery> deliveries = CreateMessages(
                game,
                new PlanetOwnershipChangedResult
                {
                    Planet = target,
                    PreviousOwner = null,
                    NewOwner = alliance,
                    Reason = PlanetOwnershipChangeReason.PopularSupport,
                }
            );

            AssertOnlyDeliveries(deliveries, alliance, MessageType.PopularSupport, 1);
        }

        [Test]
        public void CreateMessages_WithConfiguredBlockadeDefinitions_ReturnsBlockaderAndOwnerDeliveries()
        {
            (GameRoot game, Faction alliance, Faction empire, _, Planet target) =
                BuildTwoFactionMessageScene();
            Fleet fleet = new Fleet
            {
                DisplayName = "Fleet 1",
                OwnerInstanceID = alliance.InstanceID,
            };

            List<MessageDelivery> deliveries = CreateMessages(
                game,
                new BlockadeChangedResult
                {
                    Planet = target,
                    BlockadingFleet = fleet,
                    Blockaded = true,
                }
            );

            AssertOnlyDeliveries(deliveries, alliance, MessageType.Fleet, 1);
            AssertOnlyDeliveries(deliveries, empire, MessageType.Fleet, 1);
        }

        [Test]
        public void CreateMessages_WithConfiguredEvacuationDefinition_ReturnsFleetDelivery()
        {
            (GameRoot game, Faction alliance, Planet origin, _) = BuildMessageScene();

            List<MessageDelivery> deliveries = CreateMessages(
                game,
                new EvacuationLossesResult
                {
                    Faction = alliance,
                    Location = origin,
                    LostRegiments = { new Regiment { DisplayName = "Infantry Regiment" } },
                }
            );

            AssertOnlyDeliveries(deliveries, alliance, MessageType.Fleet, 1);
        }

        [Test]
        public void CreateMessages_WithConfiguredAutoscrapDefinition_ReturnsResourceDelivery()
        {
            (GameRoot game, Faction alliance, Planet origin, _) = BuildMessageScene();
            Building shipyard = new Building
            {
                InstanceID = "SHIPYARD1",
                DisplayName = "Shipyard",
                OwnerInstanceID = alliance.InstanceID,
            };
            game.AttachNode(shipyard, origin);

            List<MessageDelivery> deliveries = CreateMessages(
                game,
                new MaintenanceRequiredResult { Faction = alliance, Amount = 12 },
                new GameObjectAutoscrappedResult { DestroyedObject = shipyard, Context = origin }
            );

            AssertOnlyDeliveries(deliveries, alliance, MessageType.Resource, 1);
        }

        [Test]
        public void CreateMessages_WithConfiguredSpaceCombatDefinitions_ReturnsConflictDeliveries()
        {
            (GameRoot game, Faction alliance, Faction empire, _, Planet target) =
                BuildTwoFactionMessageScene();

            List<MessageDelivery> deliveries = CreateMessages(
                game,
                new SpaceCombatResult
                {
                    AttackerFleet = new Fleet { OwnerInstanceID = alliance.InstanceID },
                    DefenderFleet = new Fleet { OwnerInstanceID = empire.InstanceID },
                    Planet = target,
                    Winner = CombatSide.Attacker,
                }
            );

            AssertOnlyDeliveries(deliveries, alliance, MessageType.Conflict, 1);
            AssertOnlyDeliveries(deliveries, empire, MessageType.Conflict, 1);
        }

        [Test]
        public void CreateMessages_WithConfiguredBombardmentDefinitions_ReturnsConflictDeliveries()
        {
            (GameRoot game, Faction alliance, Faction empire, _, Planet target) =
                BuildTwoFactionMessageScene();

            List<MessageDelivery> deliveries = CreateMessages(
                game,
                new BombardmentResult { AttackingFaction = alliance, Planet = target }
            );

            AssertOnlyDeliveries(deliveries, alliance, MessageType.Conflict, 1);
            AssertOnlyDeliveries(deliveries, empire, MessageType.Conflict, 1);
        }

        [Test]
        public void CreateMessages_WithConfiguredAssaultDefinitions_ReturnsConflictDeliveries()
        {
            (GameRoot game, Faction alliance, Faction empire, _, Planet target) =
                BuildTwoFactionMessageScene();

            List<MessageDelivery> deliveries = CreateMessages(
                game,
                new PlanetaryAssaultResult
                {
                    AttackingFaction = alliance,
                    Planet = target,
                    Success = false,
                }
            );

            AssertOnlyDeliveries(deliveries, alliance, MessageType.Conflict, 1);
            AssertOnlyDeliveries(deliveries, empire, MessageType.Conflict, 1);
        }

        private static (
            GameRoot game,
            Faction alliance,
            Planet origin,
            Planet destination
        ) BuildMessageScene()
        {
            GameRoot game = new GameRoot(ResourceManager.GetConfig<GameConfig>());
            Faction alliance = new Faction { InstanceID = "FNALL1", DisplayName = "Alliance" };
            game.Factions.Add(alliance);
            PlanetSystem system = new PlanetSystem { InstanceID = "CORE", DisplayName = "Core" };
            game.AttachNode(system, game.Galaxy);
            Planet origin = new Planet
            {
                InstanceID = "CORUSCANT",
                DisplayName = "Coruscant",
                OwnerInstanceID = alliance.InstanceID,
                IsColonized = true,
                EnergyCapacity = 10,
            };
            Planet destination = new Planet
            {
                InstanceID = "YAVIN",
                DisplayName = "Yavin",
                OwnerInstanceID = alliance.InstanceID,
                IsColonized = true,
                EnergyCapacity = 10,
            };
            game.AttachNode(origin, system);
            game.AttachNode(destination, system);

            return (game, alliance, origin, destination);
        }

        private static (
            GameRoot game,
            Faction alliance,
            Faction empire,
            Planet origin,
            Planet target
        ) BuildTwoFactionMessageScene()
        {
            GameRoot game = new GameRoot(ResourceManager.GetConfig<GameConfig>());
            Faction alliance = new Faction { InstanceID = "FNALL1", DisplayName = "Alliance" };
            Faction empire = new Faction { InstanceID = "FNEMP1", DisplayName = "Empire" };
            game.Factions.Add(alliance);
            game.Factions.Add(empire);
            PlanetSystem system = new PlanetSystem { InstanceID = "CORE", DisplayName = "Core" };
            game.AttachNode(system, game.Galaxy);
            Planet origin = new Planet
            {
                InstanceID = "CORUSCANT",
                DisplayName = "Coruscant",
                OwnerInstanceID = alliance.InstanceID,
                IsColonized = true,
                EnergyCapacity = 10,
            };
            Planet target = new Planet
            {
                InstanceID = "YAVIN",
                DisplayName = "Yavin",
                OwnerInstanceID = empire.InstanceID,
                IsColonized = true,
                EnergyCapacity = 10,
            };
            game.AttachNode(origin, system);
            game.AttachNode(target, system);

            return (game, alliance, empire, origin, target);
        }

        private static List<MessageDelivery> CreateMessages(
            GameRoot game,
            params GameResult[] results
        )
        {
            MessageFactory factory = new MessageFactory(
                ResourceManager.GetGameData<MessageDefinition>()
            );
            return factory.CreateMessages(results, game).ToList();
        }

        private static Message CreateSingleMessage(
            GameRoot game,
            Faction faction,
            params GameResult[] results
        )
        {
            return CreateMessages(game, results)
                .Single(delivery => delivery.Faction == faction)
                .Message;
        }

        private static void AssertMessageImage(
            Message message,
            string displayImagePath,
            string overlayImagePath
        )
        {
            Assert.AreEqual(displayImagePath, message.DisplayImagePath);
            Assert.AreEqual(overlayImagePath, message.OverlayImagePath);
        }

        private static void AssertOnlyDeliveries(
            List<MessageDelivery> deliveries,
            Faction faction,
            MessageType messageType,
            int expectedCount
        )
        {
            List<MessageDelivery> factionDeliveries = deliveries
                .Where(delivery => delivery.Faction == faction)
                .ToList();
            Assert.AreEqual(expectedCount, factionDeliveries.Count);

            foreach (MessageDelivery delivery in factionDeliveries)
                Assert.AreEqual(messageType, delivery.Message.Type);
        }
    }
}
