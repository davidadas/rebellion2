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
using Rebellion.Util.Extensions;

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

            List<(Faction faction, Message message)> deliveries = CreateMessages(
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

            List<(Faction faction, Message message)> deliveries = CreateMessages(
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

            List<(Faction faction, Message message)> deliveries = CreateMessages(
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
                MessageImagePath = "Art/UI/Units/ui_message_character_alliance_ackbar",
            };
            game.AttachNode(officer, origin);

            List<(Faction faction, Message message)> deliveries = CreateMessages(
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
                MessageImagePath = "Art/UI/Units/ui_message_character_alliance_ackbar",
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
                null
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
                    Mission = new JediTrainingMission
                    {
                        ConfigKey = MissionTypeIDs.JediTraining,
                        OwnerInstanceID = alliance.InstanceID,
                    },
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
                        ConfigKey = MissionTypeIDs.Research,
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
                        ConfigKey = MissionTypeIDs.Sabotage,
                        OwnerInstanceID = alliance.InstanceID,
                        DisplayName = "Sabotage",
                    },
                    TargetName = destination.DisplayName,
                    Outcome = MissionOutcome.Failed,
                }
            );

            Assert.AreEqual("mission_report", jediTraining.DisplayImageKey);
            Assert.IsNull(jediTraining.DisplayImagePath);
            Assert.IsNull(research.DisplayImageKey);
            Assert.AreEqual(
                "Art/UI/Messages/ui_message_research_report",
                research.DisplayImagePath
            );
            Assert.IsNull(sabotageFailed.DisplayImageKey);
            Assert.AreEqual(
                "Art/UI/Messages/ui_message_mission_report_espionage",
                sabotageFailed.DisplayImagePath
            );
        }

        [Test]
        public void CreateMessages_WithConfiguredActorMissionReportDefinition_UsesParticipantTitle()
        {
            (GameRoot game, Faction alliance, _, _, Planet target) = BuildTwoFactionMessageScene();
            Mission mission = new DiplomacyMission
            {
                ConfigKey = MissionTypeIDs.Diplomacy,
                OwnerInstanceID = alliance.InstanceID,
                DisplayName = "Diplomacy",
            };
            Officer participant = new Officer
            {
                DisplayName = "Han Solo",
                OwnerInstanceID = alliance.InstanceID,
            };
            game.AttachNode(mission, target);

            Message message = CreateSingleMessage(
                game,
                alliance,
                new MissionCompletedResult
                {
                    Mission = mission,
                    MissionName = "Diplomacy",
                    TargetName = target.DisplayName,
                    Participants = new List<IMissionParticipant> { participant },
                    Outcome = MissionOutcome.Success,
                }
            );

            Assert.AreEqual("Han Solo Mission Report", message.Title);
            Assert.AreEqual(
                "My diplomacy mission to Yavin has increased popular support on that system.  ",
                message.Body
            );
        }

        [Test]
        public void CreateMessages_WithConfiguredSpecialForcesMissionReportDefinition_UsesParticipantOverlay()
        {
            (GameRoot game, Faction alliance, _, _, Planet target) = BuildTwoFactionMessageScene();
            Mission mission = new ReconnaissanceMission
            {
                ConfigKey = MissionTypeIDs.Reconnaissance,
                OwnerInstanceID = alliance.InstanceID,
                DisplayName = "Reconnaissance",
            };
            SpecialForces participant = ResourceManager
                .GetEntityData<SpecialForces>()
                .Single(specialForces => specialForces.TypeID == "SPAL003")
                .GetDeepCopy();
            participant.OwnerInstanceID = alliance.InstanceID;
            game.AttachNode(mission, target);

            Message message = CreateSingleMessage(
                game,
                alliance,
                new MissionCompletedResult
                {
                    Mission = mission,
                    MissionName = "Reconnaissance",
                    TargetName = target.DisplayName,
                    Participants = new List<IMissionParticipant> { participant },
                    Outcome = MissionOutcome.Success,
                }
            );

            Assert.AreEqual(
                "Art/UI/Messages/ui_message_overlay_special_force_longprobe_y_wing_recon_team",
                message.OverlayImagePath
            );
        }

        [Test]
        public void CreateMessages_WithConfiguredRecruitmentExhaustedDefinition_ReturnsRecruitmentDone()
        {
            (GameRoot game, Faction alliance, Planet origin, _) = BuildMessageScene();
            Mission mission = new RecruitmentMission
            {
                ConfigKey = MissionTypeIDs.Recruitment,
                OwnerInstanceID = alliance.InstanceID,
                DisplayName = "Recruitment",
            };
            Officer participant = new Officer
            {
                DisplayName = "Han Solo",
                OwnerInstanceID = alliance.InstanceID,
            };
            game.AttachNode(mission, origin);

            List<Message> messages = CreateMessages(
                    game,
                    new MissionCompletedResult
                    {
                        Mission = mission,
                        MissionName = "Recruitment",
                        Participants = new List<IMissionParticipant> { participant },
                        Outcome = MissionOutcome.Success,
                        CanContinue = false,
                    }
                )
                .Where(delivery => delivery.faction == alliance)
                .Select(delivery => delivery.message)
                .ToList();

            Assert.AreEqual(2, messages.Count);
            Assert.AreEqual("Recruitment Done", messages[1].Title);
            Assert.AreEqual(
                "We regret to report that there are no more candidates to be recruited.",
                messages[1].Body
            );
            Assert.AreEqual(
                "Art/UI/Messages/ui_message_mission_report_recruitment_alliance",
                messages[1].DisplayImagePath
            );
        }

        [Test]
        public void CreateMessages_WithConfiguredFoiledMissionDefinitions_UsesActorAndEnemyTitles()
        {
            (GameRoot game, Faction alliance, Faction empire, _, Planet target) =
                BuildTwoFactionMessageScene();
            Mission mission = new SabotageMission
            {
                ConfigKey = MissionTypeIDs.Sabotage,
                OwnerInstanceID = alliance.InstanceID,
                DisplayName = "Sabotage",
            };
            Officer participant = new Officer
            {
                DisplayName = "Han Solo",
                OwnerInstanceID = alliance.InstanceID,
            };
            game.AttachNode(mission, target);

            List<(Faction faction, Message message)> deliveries = CreateMessages(
                game,
                new MissionCompletedResult
                {
                    Mission = mission,
                    MissionName = "Sabotage",
                    TargetName = target.DisplayName,
                    Participants = new List<IMissionParticipant> { participant },
                    Outcome = MissionOutcome.Foiled,
                }
            );

            Message actorMessage = deliveries
                .Single(delivery => delivery.faction == alliance)
                .message;
            Message enemyMessage = deliveries
                .Single(delivery => delivery.faction == empire)
                .message;
            Assert.AreEqual("Han Solo Mission Foiled", actorMessage.Title);
            Assert.AreEqual(
                "My Sabotage mission to Yavin has been foiled by opposing forces.  ",
                actorMessage.Body
            );
            Assert.AreEqual("Enemy Mission Foiled", enemyMessage.Title);
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
                MessageImagePath = "Art/UI/Units/ui_message_character_alliance_luke_skywalker",
            };
            game.AttachNode(officer, origin);

            Message message = CreateSingleMessage(
                game,
                alliance,
                new ForceExperienceResult
                {
                    Officer = officer,
                    ExperienceGained = 1,
                    PreviousForceRank = game.Config.Jedi.RankLabelForceKnight - 1,
                    CurrentForceRank = game.Config.Jedi.RankLabelForceKnight,
                }
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

            List<(Faction faction, Message message)> deliveries = CreateMessages(
                game,
                new GameObjectDeployedResult { GameObject = mine }
            );

            AssertOnlyDeliveries(deliveries, alliance, MessageType.Resource, 1);
        }

        [Test]
        public void CreateMessages_WithConfiguredManufacturingDefinition_ReturnsManufacturingDelivery()
        {
            (GameRoot game, Faction alliance, Planet origin, _) = BuildMessageScene();

            List<(Faction faction, Message message)> deliveries = CreateMessages(
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
            Mission mission = new SabotageMission
            {
                ConfigKey = MissionTypeIDs.Sabotage,
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

            List<(Faction faction, Message message)> deliveries = CreateMessages(
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
            Mission mission = new SabotageMission
            {
                ConfigKey = MissionTypeIDs.Sabotage,
                OwnerInstanceID = alliance.InstanceID,
                DisplayName = "Sabotage",
            };
            game.AttachNode(mission, target);

            List<(Faction faction, Message message)> deliveries = CreateMessages(
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

            List<(Faction faction, Message message)> deliveries = CreateMessages(
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

            List<(Faction faction, Message message)> deliveries = CreateMessages(
                game,
                new PlanetUprisingStartedResult { Planet = target, InstigatorFaction = alliance }
            );

            AssertOnlyDeliveries(deliveries, empire, MessageType.PopularSupport, 1);
            AssertOnlyDeliveries(deliveries, alliance, MessageType.PopularSupport, 1);
            Message message = deliveries.First(delivery => delivery.faction == alliance).message;
            Assert.IsNull(message.DisplayImageKey);
            Assert.AreEqual(
                "Art/UI/Messages/ui_message_uprising_started",
                message.DisplayImagePath
            );
        }

        [Test]
        public void CreateMessages_WithConfiguredUprisingEndedDefinition_ReturnsControllerDelivery()
        {
            (GameRoot game, _, Faction empire, _, Planet target) = BuildTwoFactionMessageScene();

            List<(Faction faction, Message message)> deliveries = CreateMessages(
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

            List<(Faction faction, Message message)> deliveries = CreateMessages(
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
            Message message = deliveries.Single().message;
            Assert.IsNull(message.DisplayImageKey);
            Assert.AreEqual(
                "Art/UI/Messages/ui_message_planet_allegiance_evolves_alliance",
                message.DisplayImagePath
            );
        }

        [Test]
        public void CreateMessages_WithConfiguredPlanetJoinedEnemyDefinition_ReturnsPreviousOwnerDelivery()
        {
            (GameRoot game, Faction alliance, Faction empire, _, Planet target) =
                BuildTwoFactionMessageScene();

            List<(Faction faction, Message message)> deliveries = CreateMessages(
                game,
                new PlanetOwnershipChangedResult
                {
                    Planet = target,
                    PreviousOwner = empire,
                    NewOwner = alliance,
                    Reason = PlanetOwnershipChangeReason.PopularSupport,
                }
            );

            AssertOnlyDeliveries(deliveries, alliance, MessageType.PopularSupport, 1);
            AssertOnlyDeliveries(deliveries, empire, MessageType.PopularSupport, 1);
            Message message = deliveries.Single(delivery => delivery.faction == empire).message;
            Assert.AreEqual("Yavin Joins Enemy", message.Title);
            Assert.AreEqual(
                "Popular dissent on Yavin has caused that world to join the Alliance.",
                message.Body
            );
            Assert.IsNull(message.DisplayImageKey);
            Assert.AreEqual(
                "Art/UI/Messages/ui_message_planet_allegiance_evolves_alliance",
                message.DisplayImagePath
            );
        }

        [Test]
        public void CreateMessages_WithConfiguredPlanetNeutralityDefinition_ReturnsPreviousOwnerDelivery()
        {
            (GameRoot game, _, Faction empire, _, Planet target) = BuildTwoFactionMessageScene();

            List<(Faction faction, Message message)> deliveries = CreateMessages(
                game,
                new PlanetOwnershipChangedResult
                {
                    Planet = target,
                    PreviousOwner = empire,
                    NewOwner = null,
                    Reason = PlanetOwnershipChangeReason.PopularSupport,
                }
            );

            AssertOnlyDeliveries(deliveries, empire, MessageType.PopularSupport, 1);
            Message message = deliveries.Single().message;
            Assert.AreEqual("Yavin Declares Neutrality", message.Title);
            Assert.AreEqual(
                "Divided loyalty on Yavin has caused that world to abandon the Empire cause.",
                message.Body
            );
            Assert.IsNull(message.DisplayImageKey);
            Assert.AreEqual(
                "Art/UI/Messages/ui_message_planet_allegiance_evolves_empire",
                message.DisplayImagePath
            );
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

            List<(Faction faction, Message message)> deliveries = CreateMessages(
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
            Assert.AreEqual(
                "Fleet Initiates Blockade of Yavin",
                deliveries.Single(delivery => delivery.faction == alliance).message.Title
            );
            Assert.AreEqual(
                "Fleet 1 has initiated a blockade of Yavin.",
                deliveries.Single(delivery => delivery.faction == alliance).message.Body
            );
            Assert.AreEqual(
                "Yavin Under Blockade",
                deliveries.Single(delivery => delivery.faction == empire).message.Title
            );
            Assert.AreEqual(
                "Alliance ships have been detected at Yavin.  The world is under enemy blockade.",
                deliveries.Single(delivery => delivery.faction == empire).message.Body
            );
        }

        [Test]
        public void CreateMessages_WithConfiguredEvacuationDefinition_ReturnsFleetDelivery()
        {
            (GameRoot game, Faction alliance, Planet origin, _) = BuildMessageScene();

            List<(Faction faction, Message message)> deliveries = CreateMessages(
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

            List<(Faction faction, Message message)> deliveries = CreateMessages(
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

            List<(Faction faction, Message message)> deliveries = CreateMessages(
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

            List<(Faction faction, Message message)> deliveries = CreateMessages(
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

            List<(Faction faction, Message message)> deliveries = CreateMessages(
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

        private static List<(Faction faction, Message message)> CreateMessages(
            GameRoot game,
            params GameResult[] results
        )
        {
            MessageFactory factory = new MessageFactory(
                ResourceManager.GetEntityData<MessageDefinition>()
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
                .Single(delivery => delivery.faction == faction)
                .message;
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
            List<(Faction faction, Message message)> deliveries,
            Faction faction,
            MessageType messageType,
            int expectedCount
        )
        {
            List<(Faction faction, Message message)> factionDeliveries = deliveries
                .Where(delivery => delivery.faction == faction)
                .ToList();
            Assert.AreEqual(expectedCount, factionDeliveries.Count);

            foreach ((Faction faction, Message message) delivery in factionDeliveries)
                Assert.AreEqual(messageType, delivery.message.Type);
        }
    }
}
