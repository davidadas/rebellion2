using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Rebellion.Game;
using Rebellion.Game.Encyclopedia;
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
    public class MessageFactoryTests
    {
        [Test]
        public void CreateMessages_FleetArrival_InterpolatesFleetAndDestination()
        {
            (GameRoot game, Faction alliance, _, Planet destination) = BuildMessageScene();
            Fleet fleet = new Fleet
            {
                DisplayName = "Fleet 1",
                OwnerInstanceID = alliance.InstanceID,
            };

            Message message = FirstMessageFor(
                CreateMessages(
                    game,
                    new[]
                    {
                        Definition(
                            MessageResultType.FleetArrived,
                            MessageType.Fleet,
                            "arrived:{fleet}:{system}",
                            "body:{fleet}:{system}",
                            FactionImages()
                        ),
                    },
                    new UnitArrivedResult { Unit = fleet, Destination = destination }
                ),
                alliance
            );

            Assert.AreEqual(MessageType.Fleet, message.Type);
            Assert.AreEqual("arrived:Fleet 1:Yavin", message.Title);
            Assert.AreEqual("body:Fleet 1:Yavin", message.Body);
            Assert.AreEqual("alliance-image", message.DisplayImagePath);
        }

        [Test]
        public void CreateMessages_ShipArrivals_GroupsShipsByOwnerAndDestination()
        {
            (GameRoot game, Faction alliance, _, Planet destination) = BuildMessageScene();
            CapitalShip firstShip = new CapitalShip
            {
                DisplayName = "Nebulon-B Frigate",
                OwnerInstanceID = alliance.InstanceID,
            };
            CapitalShip secondShip = new CapitalShip
            {
                DisplayName = "Corellian Corvette",
                OwnerInstanceID = alliance.InstanceID,
            };

            Message message = FirstMessageFor(
                CreateMessages(
                    game,
                    new[]
                    {
                        Definition(
                            MessageResultType.ShipsArrived,
                            MessageType.Fleet,
                            "ships:{system}",
                            "body:{ships}",
                            FactionImages()
                        ),
                    },
                    new UnitArrivedResult { Unit = firstShip, Destination = destination },
                    new UnitArrivedResult { Unit = secondShip, Destination = destination }
                ),
                alliance
            );

            Assert.AreEqual(MessageType.Fleet, message.Type);
            Assert.AreEqual("ships:Yavin", message.Title);
            Assert.AreEqual("body:Nebulon-B Frigate\nCorellian Corvette", message.Body);
            Assert.AreEqual("alliance-image", message.DisplayImagePath);
        }

        [Test]
        public void CreateMessages_CapitalShipRepaired_ReportsShipAndAttachment()
        {
            (GameRoot game, Faction alliance, Planet origin, _) = BuildMessageScene();
            Fleet fleet = new Fleet
            {
                DisplayName = "Fleet 1",
                OwnerInstanceID = alliance.InstanceID,
            };
            CapitalShip ship = new CapitalShip
            {
                DisplayName = "Corellian Corvette",
                OwnerInstanceID = alliance.InstanceID,
                MaxHullStrength = 100,
                CurrentHullStrength = 100,
                DisplayImagePath = "ship-card",
            };
            game.AttachNode(fleet, origin);
            game.AttachNode(ship, fleet);

            Message message = FirstMessageFor(
                CreateMessages(
                    game,
                    new[]
                    {
                        Definition(
                            MessageResultType.CapitalShipRepaired,
                            MessageType.Fleet,
                            "repaired",
                            "body:{item}:{attachment}"
                        ),
                    },
                    new ShipHullDamageResult
                    {
                        Ship = ship,
                        OldHull = 50,
                        NewHull = 100,
                    }
                ),
                alliance
            );

            Assert.AreEqual(MessageType.Fleet, message.Type);
            Assert.AreEqual("repaired", message.Title);
            Assert.AreEqual("body:Corellian Corvette:Fleet 1", message.Body);
            Assert.AreEqual("ship-card", message.DisplayImagePath);
            Assert.IsNull(message.OverlayImagePath);
        }

        [Test]
        public void CreateMessages_CapitalShipRepaired_PrefersEncyclopediaImage()
        {
            (GameRoot game, Faction alliance, Planet origin, _) = BuildMessageScene();
            Fleet fleet = new Fleet
            {
                DisplayName = "Fleet 1",
                OwnerInstanceID = alliance.InstanceID,
            };
            CapitalShip ship = new CapitalShip
            {
                TypeID = "SHIP_TYPE",
                DisplayName = "Corellian Corvette",
                OwnerInstanceID = alliance.InstanceID,
                MaxHullStrength = 100,
                CurrentHullStrength = 100,
                DisplayImagePath = "unit-card",
            };
            game.AttachNode(fleet, origin);
            game.AttachNode(ship, fleet);

            MessageFactory factory = new MessageFactory(
                new[]
                {
                    Definition(
                        MessageResultType.CapitalShipRepaired,
                        MessageType.Fleet,
                        "repaired",
                        "body:{item}:{attachment}"
                    ),
                },
                new EncyclopediaEntries
                {
                    new EncyclopediaEntry { TypeID = "SHIP_TYPE", ImagePath = "encyclopedia-card" },
                }
            );

            Message message = FirstMessageFor(
                factory.CreateMessages(
                    new GameResult[]
                    {
                        new ShipHullDamageResult
                        {
                            Ship = ship,
                            OldHull = 50,
                            NewHull = 100,
                        },
                    },
                    game
                ),
                alliance
            );

            Assert.AreEqual("encyclopedia-card", message.DisplayImagePath);
            Assert.IsNull(message.OverlayImagePath);
        }

        [Test]
        public void CreateMessages_StarfighterRepaired_ReportsSquadronAndAttachment()
        {
            (GameRoot game, Faction alliance, Planet origin, _) = BuildMessageScene();
            Fleet fleet = new Fleet
            {
                DisplayName = "Fleet 1",
                OwnerInstanceID = alliance.InstanceID,
            };
            CapitalShip carrier = new CapitalShip
            {
                DisplayName = "Carrier",
                OwnerInstanceID = alliance.InstanceID,
                StarfighterCapacity = 2,
            };
            Starfighter fighter = new Starfighter
            {
                DisplayName = "X-Wing Squadron",
                OwnerInstanceID = alliance.InstanceID,
                MaxSquadronSize = 12,
                CurrentSquadronSize = 12,
                DisplayImagePath = "fighter-card",
            };
            game.AttachNode(fleet, origin);
            game.AttachNode(carrier, fleet);
            game.AttachNode(fighter, carrier);

            Message message = FirstMessageFor(
                CreateMessages(
                    game,
                    new[]
                    {
                        Definition(
                            MessageResultType.StarfighterRepaired,
                            MessageType.Fleet,
                            "full",
                            "body:{item}:{attachment}"
                        ),
                    },
                    new FighterDamageResult
                    {
                        Fighter = fighter,
                        OldSize = 6,
                        NewSize = 12,
                    }
                ),
                alliance
            );

            Assert.AreEqual(MessageType.Fleet, message.Type);
            Assert.AreEqual("full", message.Title);
            Assert.AreEqual("body:X-Wing Squadron:Carrier", message.Body);
            Assert.AreEqual("fighter-card", message.DisplayImagePath);
            Assert.IsNull(message.OverlayImagePath);
        }

        [Test]
        public void CreateMessages_SeatOfPowerChanged_ReturnsSeatOfPowerReport()
        {
            (GameRoot game, Faction alliance, _, _) = BuildMessageScene();
            Officer officer = new Officer { OwnerInstanceID = alliance.InstanceID };

            Message message = FirstMessageFor(
                CreateMessages(
                    game,
                    new[]
                    {
                        Definition(
                            MessageResultType.EmperorSeatOfPower,
                            MessageType.Mission,
                            "seat",
                            "body",
                            DefaultImage("seat-image")
                        ),
                    },
                    new SeatOfPowerChangedResult { Officer = officer, IsAtSeat = true }
                ),
                alliance
            );

            Assert.AreEqual(MessageType.Mission, message.Type);
            Assert.AreEqual("seat", message.Title);
            Assert.AreEqual("body", message.Body);
            Assert.AreEqual("seat-image", message.DisplayImagePath);
        }

        [Test]
        public void CreateMessages_DeployedFacility_UsesBuildingSpecificDefinition()
        {
            (GameRoot game, Faction alliance, Planet origin, _) = BuildMessageScene();
            Building mine = new Building
            {
                DisplayName = "Mine",
                OwnerInstanceID = alliance.InstanceID,
                BuildingType = BuildingType.Mine,
                MessageImagePath = "mine-specific-image",
            };
            game.AttachNode(mine, origin);

            Message message = FirstMessageFor(
                CreateMessages(
                    game,
                    new[]
                    {
                        Definition(
                            MessageResultType.FacilityDeployed,
                            MessageType.Resource,
                            "mine:{item}:{system}",
                            "body:{item}:{system}",
                            DefaultImage("mine-image"),
                            buildingType: BuildingType.Mine
                        ),
                    },
                    new GameObjectDeployedResult { GameObject = mine }
                ),
                alliance
            );

            Assert.AreEqual(MessageType.Resource, message.Type);
            Assert.AreEqual("mine:Mine:Coruscant", message.Title);
            Assert.AreEqual("body:Mine:Coruscant", message.Body);
            Assert.AreEqual("mine-specific-image", message.DisplayImagePath);
        }

        [Test]
        public void CreateMessages_DeployedFacilityWithoutMatchingDefinition_ReturnsNoDelivery()
        {
            (GameRoot game, Faction alliance, Planet origin, _) = BuildMessageScene();
            Building shipyard = new Building
            {
                DisplayName = "Shipyard",
                OwnerInstanceID = alliance.InstanceID,
                BuildingType = BuildingType.Shipyard,
            };
            game.AttachNode(shipyard, origin);

            List<MessageDelivery> deliveries = CreateMessages(
                game,
                new[]
                {
                    Definition(
                        MessageResultType.FacilityDeployed,
                        MessageType.Resource,
                        "mine:{item}:{system}",
                        "body:{item}:{system}",
                        DefaultImage("mine-image"),
                        buildingType: BuildingType.Mine
                    ),
                },
                new GameObjectDeployedResult { GameObject = shipyard }
            );

            Assert.IsEmpty(deliveries);
        }

        [Test]
        public void CreateMessages_ManufacturingCompleted_UsesQueueTypeDefinition()
        {
            (GameRoot game, Faction alliance, Planet origin, _) = BuildMessageScene();

            Message message = FirstMessageFor(
                CreateMessages(
                    game,
                    new[]
                    {
                        Definition(
                            MessageResultType.ManufacturingIdle,
                            MessageType.Manufacturing,
                            "construction:{system}",
                            "body:{system}",
                            DefaultImage("construction-image"),
                            manufacturingType: ManufacturingType.Building
                        ),
                    },
                    new ManufacturingCompletedResult
                    {
                        Faction = alliance,
                        ProductType = ManufacturingType.Building,
                        ProductionPlanet = origin,
                    }
                ),
                alliance
            );

            Assert.AreEqual(MessageType.Manufacturing, message.Type);
            Assert.AreEqual("construction:Coruscant", message.Title);
            Assert.AreEqual("body:Coruscant", message.Body);
            Assert.AreEqual("construction-image", message.DisplayImagePath);
        }

        [Test]
        public void CreateMessages_MissionSuccess_UsesSuccessReportForActor()
        {
            (GameRoot game, Faction alliance, _, _, Planet target) = BuildTwoFactionMessageScene();
            SabotageMission mission = new SabotageMission
            {
                DisplayName = "Sabotage",
                OwnerInstanceID = alliance.InstanceID,
            };
            game.AttachNode(mission, target);

            Message message = FirstMessageFor(
                CreateMessages(
                    game,
                    new[]
                    {
                        Definition(
                            MessageResultType.MissionReport,
                            MessageType.Mission,
                            "success:{mission}:{system}",
                            "body:{mission}:{system}",
                            FactionImages(),
                            outcome: MessageResultOutcome.Success
                        ),
                    },
                    new MissionCompletedResult
                    {
                        Mission = mission,
                        MissionName = "Sabotage",
                        Outcome = MissionOutcome.Success,
                    }
                ),
                alliance
            );

            Assert.AreEqual(MessageType.Mission, message.Type);
            Assert.AreEqual("success:Sabotage:Yavin", message.Title);
            Assert.AreEqual("body:Sabotage:Yavin", message.Body);
            Assert.AreEqual("alliance-image", message.DisplayImagePath);
        }

        [Test]
        public void CreateMessages_MissionReport_UsesReportDetailSpecificDefinition()
        {
            (GameRoot game, Faction alliance, _, _, Planet target) = BuildTwoFactionMessageScene();
            SabotageMission mission = new SabotageMission
            {
                DisplayName = "Sabotage",
                OwnerInstanceID = alliance.InstanceID,
            };
            game.AttachNode(mission, target);

            Message message = FirstMessageFor(
                CreateMessages(
                    game,
                    new[]
                    {
                        Definition(
                            MessageResultType.MissionReport,
                            MessageType.Mission,
                            "failed:{mission}:{system}",
                            "body:{mission}:{system}",
                            FactionImages(),
                            outcome: MessageResultOutcome.Failed,
                            missionType: MissionType.Sabotage
                        ),
                        Definition(
                            MessageResultType.MissionReport,
                            MessageType.Mission,
                            "missing:{mission}:{system}",
                            "missing-body:{mission}:{system}",
                            FactionImages(),
                            outcome: MessageResultOutcome.Failed,
                            missionType: MissionType.Sabotage,
                            missionReportDetail: MissionReportDetail.TargetUnavailable
                        ),
                    },
                    new MissionCompletedResult
                    {
                        Mission = mission,
                        MissionName = "Sabotage",
                        Outcome = MissionOutcome.Failed,
                        ReportDetail = MissionReportDetail.TargetUnavailable,
                    }
                ),
                alliance
            );

            Assert.AreEqual("missing:Sabotage:Yavin", message.Title);
            Assert.AreEqual("missing-body:Sabotage:Yavin", message.Body);
        }

        [Test]
        public void CreateMessages_MissionReport_DoesNotFallbackForDetailOnlyReports()
        {
            (GameRoot game, Faction alliance, _, _, Planet target) = BuildTwoFactionMessageScene();
            SabotageMission mission = new SabotageMission
            {
                DisplayName = "Sabotage",
                OwnerInstanceID = alliance.InstanceID,
            };
            game.AttachNode(mission, target);

            List<MessageDelivery> deliveries = CreateMessages(
                game,
                new[]
                {
                    Definition(
                        MessageResultType.MissionReport,
                        MessageType.Mission,
                        "failed:{mission}:{system}",
                        "body:{mission}:{system}",
                        FactionImages(),
                        outcome: MessageResultOutcome.Failed,
                        missionType: MissionType.Sabotage
                    ),
                },
                new MissionCompletedResult
                {
                    Mission = mission,
                    MissionName = "Sabotage",
                    Outcome = MissionOutcome.Failed,
                    ReportDetail = MissionReportDetail.TargetUnavailable,
                }
            );

            Assert.IsFalse(deliveries.Any(delivery => delivery.Faction == alliance));
        }

        [Test]
        public void CreateMessages_MissionSuccess_UsesMissionSpecificImage()
        {
            (GameRoot game, Faction alliance, _, _, Planet target) = BuildTwoFactionMessageScene();
            DiplomacyMission mission = new DiplomacyMission
            {
                DisplayName = "Diplomacy",
                OwnerInstanceID = alliance.InstanceID,
            };
            game.AttachNode(mission, target);

            Message message = FirstMessageFor(
                CreateMessages(
                    game,
                    new[]
                    {
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
                            "success:{mission}:{system}",
                            "body:{mission}:{system}",
                            DefaultImage("diplomacy-image"),
                            outcome: MessageResultOutcome.Success,
                            missionType: MissionType.Diplomacy
                        ),
                    },
                    new MissionCompletedResult
                    {
                        Mission = mission,
                        MissionName = "Diplomacy",
                        Outcome = MissionOutcome.Success,
                    }
                ),
                alliance
            );

            Assert.AreEqual("diplomacy-image", message.DisplayImagePath);
        }

        [Test]
        public void CreateMessages_AssassinationReportWithKilledTarget_UsesKilledResultOfficerName()
        {
            (GameRoot game, Faction alliance, Faction empire, _, Planet target) =
                BuildTwoFactionMessageScene();
            Officer targetOfficer = new Officer
            {
                DisplayName = "Target Officer",
                InstanceID = "target-officer",
                OwnerInstanceID = empire.InstanceID,
            };
            AssassinationMission mission = new AssassinationMission
            {
                DisplayName = "Assassination",
                OwnerInstanceID = alliance.InstanceID,
                TargetOfficerInstanceID = targetOfficer.InstanceID,
            };
            game.AttachNode(targetOfficer, target);
            game.AttachNode(mission, target);
            game.DetachNode(targetOfficer);

            Message message = FirstMessageFor(
                CreateMessages(
                    game,
                    new[]
                    {
                        Definition(
                            MessageResultType.MissionReport,
                            MessageType.Mission,
                            "title:{officer}",
                            "body:{officer}:{assassination_result}",
                            DefaultImage("mission-image"),
                            outcome: MessageResultOutcome.Success,
                            missionType: MissionType.Assassination
                        ),
                    },
                    new MissionCompletedResult
                    {
                        Mission = mission,
                        MissionName = "Assassination",
                        Outcome = MissionOutcome.Success,
                    },
                    new OfficerKilledResult { TargetOfficer = targetOfficer, Context = target }
                ),
                alliance
            );

            Assert.AreEqual("title:Target Officer", message.Title);
            Assert.AreEqual("body:Target Officer:has been eliminated", message.Body);
        }

        [Test]
        public void CreateMessages_AssassinationReportWithInjuredTarget_UsesLiveOfficerName()
        {
            (GameRoot game, Faction alliance, Faction empire, _, Planet target) =
                BuildTwoFactionMessageScene();
            Officer targetOfficer = new Officer
            {
                DisplayName = "Target Officer",
                InstanceID = "target-officer",
                OwnerInstanceID = empire.InstanceID,
            };
            AssassinationMission mission = new AssassinationMission
            {
                DisplayName = "Assassination",
                OwnerInstanceID = alliance.InstanceID,
                TargetOfficerInstanceID = targetOfficer.InstanceID,
            };
            game.AttachNode(targetOfficer, target);
            game.AttachNode(mission, target);

            Message message = FirstMessageFor(
                CreateMessages(
                    game,
                    new[]
                    {
                        Definition(
                            MessageResultType.MissionReport,
                            MessageType.Mission,
                            "title:{officer}",
                            "body:{officer}:{assassination_result}",
                            DefaultImage("mission-image"),
                            outcome: MessageResultOutcome.Success,
                            missionType: MissionType.Assassination
                        ),
                    },
                    new MissionCompletedResult
                    {
                        Mission = mission,
                        MissionName = "Assassination",
                        Outcome = MissionOutcome.Success,
                    },
                    new OfficerInjuredResult { Officer = targetOfficer, Severity = 1 }
                ),
                alliance
            );

            Assert.AreEqual("title:Target Officer", message.Title);
            Assert.AreEqual("body:Target Officer:has been injured", message.Body);
        }

        [Test]
        public void CreateMessages_MissionReportWithParticipants_UsesFirstParticipantImageOverlay()
        {
            (GameRoot game, Faction alliance, _, _, Planet target) = BuildTwoFactionMessageScene();
            DiplomacyMission mission = new DiplomacyMission
            {
                DisplayName = "Diplomacy",
                OwnerInstanceID = alliance.InstanceID,
            };
            Officer firstParticipant = new Officer
            {
                DisplayName = "First",
                OwnerInstanceID = alliance.InstanceID,
                MessageImagePath = "first-card",
            };
            Officer secondParticipant = new Officer
            {
                DisplayName = "Second",
                OwnerInstanceID = alliance.InstanceID,
                MessageImagePath = "second-card",
            };
            game.AttachNode(mission, target);

            Message message = FirstMessageFor(
                CreateMessages(
                    game,
                    new[]
                    {
                        Definition(
                            MessageResultType.MissionReport,
                            MessageType.Mission,
                            "failed:{mission}:{system}",
                            "body:{mission}:{system}",
                            DefaultImage("diplomacy-image"),
                            outcome: MessageResultOutcome.Failed,
                            missionType: MissionType.Diplomacy
                        ),
                    },
                    new MissionCompletedResult
                    {
                        Mission = mission,
                        MissionName = "Diplomacy",
                        Outcome = MissionOutcome.Failed,
                        Participants = new List<IMissionParticipant>
                        {
                            firstParticipant,
                            secondParticipant,
                        },
                    }
                ),
                alliance
            );

            Assert.AreEqual("diplomacy-image", message.DisplayImagePath);
            Assert.AreEqual("first-card", message.OverlayImagePath);
        }

        [Test]
        public void CreateMessages_RecruitmentMissionSuccessUsesRecruitedOfficerImage()
        {
            (GameRoot game, Faction alliance, _, _, Planet target) = BuildTwoFactionMessageScene();
            RecruitmentMission mission = new RecruitmentMission
            {
                DisplayName = "Recruitment",
                OwnerInstanceID = alliance.InstanceID,
                TargetOfficerInstanceID = "target-officer",
            };
            Officer participant = new Officer
            {
                DisplayName = "Recruiter",
                OwnerInstanceID = alliance.InstanceID,
                MessageImagePath = "participant-card",
            };
            Officer targetOfficer = new Officer
            {
                InstanceID = "target-officer",
                DisplayName = "Target",
                OwnerInstanceID = alliance.InstanceID,
                MessageImagePath = "target-card",
            };
            game.AttachNode(mission, target);
            game.AttachNode(targetOfficer, target);

            Message message = FirstMessageFor(
                CreateMessages(
                    game,
                    new[]
                    {
                        Definition(
                            MessageResultType.MissionReport,
                            MessageType.Mission,
                            "recruited:{participant}:{officer}:{system}",
                            "body:{participant}:{officer}:{system}",
                            DefaultImage("recruitment-image"),
                            outcome: MessageResultOutcome.Success,
                            missionType: MissionType.Recruitment
                        ),
                    },
                    new MissionCompletedResult
                    {
                        Mission = mission,
                        MissionName = "Recruitment",
                        Outcome = MissionOutcome.Success,
                        Participants = new List<IMissionParticipant> { participant },
                    }
                ),
                alliance
            );

            Assert.AreEqual("recruitment-image", message.DisplayImagePath);
            Assert.AreEqual("target-card", message.OverlayImagePath);
        }

        [Test]
        public void CreateMessages_MissionReportWithoutParticipantImages_UsesMissionReportImage()
        {
            (GameRoot game, Faction alliance, _, _, Planet target) = BuildTwoFactionMessageScene();
            DiplomacyMission mission = new DiplomacyMission
            {
                DisplayName = "Diplomacy",
                OwnerInstanceID = alliance.InstanceID,
            };
            Officer participant = new Officer
            {
                DisplayName = "Agent",
                OwnerInstanceID = alliance.InstanceID,
            };
            game.AttachNode(mission, target);

            Message message = FirstMessageFor(
                CreateMessages(
                    game,
                    new[]
                    {
                        Definition(
                            MessageResultType.MissionReport,
                            MessageType.Mission,
                            "failed:{mission}:{system}",
                            "body:{mission}:{system}",
                            DefaultImage("diplomacy-image"),
                            outcome: MessageResultOutcome.Failed,
                            missionType: MissionType.Diplomacy
                        ),
                    },
                    new MissionCompletedResult
                    {
                        Mission = mission,
                        MissionName = "Diplomacy",
                        Outcome = MissionOutcome.Failed,
                        Participants = new List<IMissionParticipant> { participant },
                    }
                ),
                alliance
            );

            Assert.AreEqual("diplomacy-image", message.DisplayImagePath);
            Assert.IsNull(message.OverlayImagePath);
        }

        [Test]
        public void CreateMessages_FoiledMission_ReturnsFailedActorReportAndFoiledTargetReport()
        {
            (GameRoot game, Faction alliance, Faction empire, _, Planet target) =
                BuildTwoFactionMessageScene();
            SabotageMission mission = new SabotageMission
            {
                DisplayName = "Sabotage",
                OwnerInstanceID = alliance.InstanceID,
            };
            Officer participant = new Officer
            {
                DisplayName = "Agent",
                OwnerInstanceID = alliance.InstanceID,
                MessageImagePath = "agent-card",
            };
            game.AttachNode(mission, target);

            List<MessageDelivery> deliveries = CreateMessages(
                game,
                new[]
                {
                    Definition(
                        MessageResultType.MissionReport,
                        MessageType.Mission,
                        "failed:{mission}:{system}",
                        "body:{mission}:{system}",
                        FactionImages(),
                        outcome: MessageResultOutcome.Failed
                    ),
                    Definition(
                        MessageResultType.EnemyMissionFoiled,
                        MessageType.Mission,
                        "foiled:{mission}:{system}",
                        "body:{mission}:{system}",
                        FactionImages(),
                        outcome: MessageResultOutcome.Foiled
                    ),
                },
                new MissionCompletedResult
                {
                    Mission = mission,
                    MissionName = "Sabotage",
                    Outcome = MissionOutcome.Foiled,
                    Participants = new List<IMissionParticipant> { participant },
                }
            );

            Assert.AreEqual("failed:Sabotage:Yavin", FirstMessageFor(deliveries, alliance).Title);
            Assert.AreEqual("foiled:Sabotage:Yavin", FirstMessageFor(deliveries, empire).Title);
            Assert.AreEqual(
                "alliance-image",
                FirstMessageFor(deliveries, alliance).DisplayImagePath
            );
            Assert.AreEqual("empire-image", FirstMessageFor(deliveries, empire).DisplayImagePath);
            Assert.AreEqual("agent-card", FirstMessageFor(deliveries, alliance).OverlayImagePath);
            Assert.AreEqual("agent-card", FirstMessageFor(deliveries, empire).OverlayImagePath);
        }

        [Test]
        public void CreateMessages_OfficerRecruited_UsesOfficerImageOverride()
        {
            (GameRoot game, Faction alliance, Planet origin, _) = BuildMessageScene();
            Officer officer = new Officer
            {
                DisplayName = "Agent",
                OwnerInstanceID = alliance.InstanceID,
                MessageImagePath = "agent-card",
            };
            game.AttachNode(officer, origin);

            Message message = FirstMessageFor(
                CreateMessages(
                    game,
                    new[]
                    {
                        Definition(
                            MessageResultType.OfficerRecruited,
                            MessageType.Mission,
                            "recruited:{officer}:{system}",
                            "body:{officer}:{system}",
                            DefaultImage("fallback-card")
                        ),
                    },
                    new OfficerRecruitedResult
                    {
                        Officer = officer,
                        Faction = alliance,
                        Planet = origin,
                    }
                ),
                alliance
            );

            Assert.AreEqual(MessageType.Mission, message.Type);
            Assert.AreEqual("recruited:Agent:Coruscant", message.Title);
            Assert.AreEqual("body:Agent:Coruscant", message.Body);
            Assert.AreEqual("fallback-card", message.DisplayImagePath);
            Assert.AreEqual("agent-card", message.OverlayImagePath);
        }

        [Test]
        public void CreateMessages_OfficerCapture_UsesTargetOfficerImage()
        {
            (GameRoot game, Faction alliance, Planet origin, _) = BuildMessageScene();
            Officer target = new Officer
            {
                DisplayName = "Target",
                OwnerInstanceID = alliance.InstanceID,
                MessageImagePath = "target-card",
            };
            Officer linked = new Officer
            {
                DisplayName = "Linked",
                OwnerInstanceID = alliance.InstanceID,
                MessageImagePath = "linked-card",
            };
            game.AttachNode(target, origin);
            game.AttachNode(linked, origin);

            Message message = FirstMessageFor(
                CreateMessages(
                    game,
                    new[]
                    {
                        Definition(
                            MessageResultType.OfficerCaptured,
                            MessageType.Mission,
                            "captured:{officer}:{system}",
                            "body:{officer}:{system}",
                            DefaultImage("fallback-card")
                        ),
                    },
                    new OfficerCaptureStateResult
                    {
                        TargetOfficer = target,
                        LinkedOfficer = linked,
                        IsCaptured = true,
                        Context = origin,
                    }
                ),
                alliance
            );

            Assert.AreEqual("captured:Target:Coruscant", message.Title);
            Assert.AreEqual("fallback-card", message.DisplayImagePath);
            Assert.AreEqual("target-card", message.OverlayImagePath);
        }

        [Test]
        public void CreateMessages_OfficerRecovered_UsesRecoveredDefinition()
        {
            (GameRoot game, Faction alliance, Planet origin, _) = BuildMessageScene();
            Officer officer = new Officer
            {
                DisplayName = "Agent",
                OwnerInstanceID = alliance.InstanceID,
                MessageImagePath = "agent-card",
            };
            game.AttachNode(officer, origin);

            Message message = FirstMessageFor(
                CreateMessages(
                    game,
                    new[]
                    {
                        Definition(
                            MessageResultType.OfficerRecovered,
                            MessageType.Mission,
                            "recovered:{officer}:{system}",
                            "body:{officer}:{system}",
                            DefaultImage("fallback-card")
                        ),
                    },
                    new OfficerInjuredResult { Officer = officer, Severity = 0 }
                ),
                alliance
            );

            Assert.AreEqual("recovered:Agent:Coruscant", message.Title);
            Assert.AreEqual("fallback-card", message.DisplayImagePath);
            Assert.AreEqual("agent-card", message.OverlayImagePath);
        }

        [Test]
        public void CreateMessages_OfficerKilled_SuppressesSameBatchInjury()
        {
            (GameRoot game, Faction alliance, Planet origin, _) = BuildMessageScene();
            Officer officer = new Officer
            {
                DisplayName = "Agent",
                OwnerInstanceID = alliance.InstanceID,
                MessageImagePath = "agent-card",
            };
            game.AttachNode(officer, origin);

            List<MessageDelivery> deliveries = CreateMessages(
                game,
                new[]
                {
                    Definition(
                        MessageResultType.OfficerInjured,
                        MessageType.Mission,
                        "injured:{officer}:{system}",
                        "body:{officer}:{system}",
                        DefaultImage("injury-card")
                    ),
                    Definition(
                        MessageResultType.OfficerKilled,
                        MessageType.Mission,
                        "killed:{officer}:{system}",
                        "body:{officer}:{system}",
                        DefaultImage("killed-card")
                    ),
                },
                new OfficerInjuredResult { Officer = officer, Severity = 1 },
                new OfficerKilledResult { TargetOfficer = officer, Context = origin }
            );

            List<Message> messages = deliveries
                .Where(delivery => delivery.Faction == alliance)
                .Select(delivery => delivery.Message)
                .ToList();
            Assert.AreEqual(1, messages.Count);
            Assert.AreEqual("killed:Agent:Coruscant", messages[0].Title);
            Assert.AreEqual("killed-card", messages[0].DisplayImagePath);
            Assert.AreEqual("agent-card", messages[0].OverlayImagePath);
        }

        [Test]
        public void CreateMessages_ForceExperience_ReturnsForceGrowthMessage()
        {
            (GameRoot game, Faction alliance, Planet origin, _) = BuildMessageScene();
            Officer officer = new Officer
            {
                DisplayName = "Student",
                OwnerInstanceID = alliance.InstanceID,
                IsJedi = true,
                ForceValue = game.Config.Jedi.RankLabelForceKnight,
                MessageImagePath = "student-card",
            };
            game.AttachNode(officer, origin);

            Message message = FirstMessageFor(
                CreateMessages(
                    game,
                    new[]
                    {
                        Definition(
                            MessageResultType.ForceGrowth,
                            MessageType.Mission,
                            "force",
                            "body:{rank}"
                        ),
                    },
                    new ForceExperienceResult { Officer = officer, ExperienceGained = 5 }
                ),
                alliance
            );

            Assert.AreEqual(MessageType.Mission, message.Type);
            Assert.AreEqual("force", message.Title);
            Assert.AreEqual("body:Jedi Knight", message.Body);
            Assert.AreEqual("student-card", message.OverlayImagePath);
        }

        [Test]
        public void CreateMessages_ForceUserDiscovered_DoesNotUseForceGrowthMessage()
        {
            (GameRoot game, Faction alliance, Planet origin, _) = BuildMessageScene();
            Officer officer = new Officer
            {
                DisplayName = "Student",
                InstanceID = "student",
                OwnerInstanceID = alliance.InstanceID,
                IsJedi = true,
                ForceValue = game.Config.Jedi.RankLabelTrainee,
            };
            game.AttachNode(officer, origin);

            List<MessageDelivery> deliveries = CreateMessages(
                game,
                new[]
                {
                    Definition(MessageResultType.ForceGrowth, MessageType.Mission, "force", "body"),
                },
                new ForceExperienceResult { Officer = officer, ExperienceGained = 5 },
                new ForceDiscoveryResult
                {
                    EventType = ForceEventType.ForceUserDiscovered,
                    Officer = officer,
                }
            );

            Assert.IsEmpty(deliveries);
        }

        [Test]
        public void CreateMessages_SabotageResult_ReportsDestroyedObjectToOwner()
        {
            (GameRoot game, _, Faction empire, _, Planet target) = BuildTwoFactionMessageScene();
            Building shieldGenerator = new Building
            {
                DisplayName = "Shield Generator",
                OwnerInstanceID = empire.InstanceID,
            };

            Message message = FirstMessageFor(
                CreateMessages(
                    game,
                    new[]
                    {
                        Definition(
                            MessageResultType.SabotageStrike,
                            MessageType.Mission,
                            "sabotage:{item}:{system}",
                            "body:{item}:{system}",
                            FactionImages()
                        ),
                    },
                    new GameObjectSabotagedResult
                    {
                        SabotagedObject = shieldGenerator,
                        Context = target,
                    }
                ),
                empire
            );

            Assert.AreEqual(MessageType.Mission, message.Type);
            Assert.AreEqual("sabotage:Shield Generator:Yavin", message.Title);
            Assert.AreEqual("body:Shield Generator:Yavin", message.Body);
            Assert.AreEqual("empire-image", message.DisplayImagePath);
        }

        [Test]
        public void CreateMessages_ResearchCompleted_UsesDisciplineDefinition()
        {
            (GameRoot game, Faction alliance, _, _) = BuildMessageScene();

            Message message = FirstMessageFor(
                CreateMessages(
                    game,
                    new[]
                    {
                        Definition(
                            MessageResultType.ResearchComplete,
                            MessageType.Manufacturing,
                            "ship:{item}",
                            "body:{item}",
                            DefaultImage("research-image"),
                            researchDiscipline: ResearchDiscipline.ShipDesign
                        ),
                    },
                    new ResearchOrderedResult
                    {
                        Faction = alliance,
                        Discipline = ResearchDiscipline.ShipDesign,
                        Technology = new Technology(
                            new CapitalShip { DisplayName = "Nebulon-B Frigate" }
                        ),
                    }
                ),
                alliance
            );

            Assert.AreEqual(MessageType.Manufacturing, message.Type);
            Assert.AreEqual("ship:Nebulon-B Frigate", message.Title);
            Assert.AreEqual("body:Nebulon-B Frigate", message.Body);
            Assert.AreEqual("research-image", message.DisplayImagePath);
        }

        [Test]
        public void CreateMessages_ResearchExhausted_UsesDisciplineDefinition()
        {
            (GameRoot game, Faction alliance, _, _) = BuildMessageScene();

            Message message = FirstMessageFor(
                CreateMessages(
                    game,
                    new[]
                    {
                        Definition(
                            MessageResultType.ResearchExhausted,
                            MessageType.Manufacturing,
                            "exhausted",
                            "body",
                            DefaultImage("research-image"),
                            researchDiscipline: ResearchDiscipline.FacilityDesign
                        ),
                    },
                    new ResearchExhaustedResult
                    {
                        Faction = alliance,
                        Discipline = ResearchDiscipline.FacilityDesign,
                    }
                ),
                alliance
            );

            Assert.AreEqual(MessageType.Manufacturing, message.Type);
            Assert.AreEqual("exhausted", message.Title);
            Assert.AreEqual("body", message.Body);
            Assert.AreEqual("research-image", message.DisplayImagePath);
        }

        [Test]
        public void CreateMessages_UprisingStarted_ReturnsControllerAndInstigatorReports()
        {
            (GameRoot game, Faction alliance, Faction empire, _, Planet target) =
                BuildTwoFactionMessageScene();

            List<MessageDelivery> deliveries = CreateMessages(
                game,
                new[]
                {
                    Definition(
                        MessageResultType.UprisingStarted,
                        MessageType.PopularSupport,
                        "started:{faction}:{system}",
                        "body:{faction}:{system}",
                        FactionImages()
                    ),
                },
                new PlanetUprisingStartedResult { Planet = target, InstigatorFaction = alliance }
            );

            Assert.AreEqual("started:Empire:Yavin", FirstMessageFor(deliveries, empire).Title);
            Assert.AreEqual("started:Empire:Yavin", FirstMessageFor(deliveries, alliance).Title);
        }

        [Test]
        public void CreateMessages_UprisingEnded_UsesControllerImage()
        {
            (GameRoot game, _, Faction empire, _, Planet target) = BuildTwoFactionMessageScene();

            Message message = FirstMessageFor(
                CreateMessages(
                    game,
                    new[]
                    {
                        Definition(
                            MessageResultType.UprisingEnded,
                            MessageType.PopularSupport,
                            "ended:{faction}:{system}",
                            "body:{faction}:{system}",
                            FactionImages()
                        ),
                    },
                    new PlanetUprisingEndedResult { Planet = target }
                ),
                empire
            );

            Assert.AreEqual(MessageType.PopularSupport, message.Type);
            Assert.AreEqual("ended:Empire:Yavin", message.Title);
            Assert.AreEqual("body:Empire:Yavin", message.Body);
            Assert.AreEqual("empire-image", message.DisplayImagePath);
        }

        [Test]
        public void CreateMessages_PlanetJoinedBySupport_ReportsNewOwner()
        {
            (GameRoot game, Faction alliance, _, _, Planet target) = BuildTwoFactionMessageScene();
            target.OwnerInstanceID = null;

            Message message = FirstMessageFor(
                CreateMessages(
                    game,
                    new[]
                    {
                        Definition(
                            MessageResultType.PlanetJoinedBySupport,
                            MessageType.PopularSupport,
                            "{system} joins",
                            "support:{system}:{faction}",
                            imageKey: "mission_report"
                        ),
                    },
                    new PlanetOwnershipChangedResult
                    {
                        Planet = target,
                        PreviousOwner = null,
                        NewOwner = alliance,
                        Reason = PlanetOwnershipChangeReason.PopularSupport,
                    }
                ),
                alliance
            );

            Assert.AreEqual(MessageType.PopularSupport, message.Type);
            Assert.AreEqual("Yavin joins", message.Title);
            Assert.AreEqual("support:Yavin:Alliance", message.Body);
            Assert.AreEqual("mission_report", message.DisplayImageKey);
            Assert.IsNull(message.DisplayImagePath);
        }

        [Test]
        public void CreateMessages_PlanetOwnershipChangeWithoutSupportReason_DoesNotReportJoin()
        {
            (GameRoot game, Faction alliance, _, _, Planet target) = BuildTwoFactionMessageScene();
            target.OwnerInstanceID = null;

            List<MessageDelivery> deliveries = CreateMessages(
                game,
                new[]
                {
                    Definition(
                        MessageResultType.PlanetJoinedBySupport,
                        MessageType.PopularSupport,
                        "{system} joins",
                        "support:{system}:{faction}"
                    ),
                },
                new PlanetOwnershipChangedResult
                {
                    Planet = target,
                    PreviousOwner = null,
                    NewOwner = alliance,
                }
            );

            Assert.IsEmpty(deliveries);
        }

        [Test]
        public void CreateMessages_BlockadeStarted_UsesTargetImageForBlockaderReport()
        {
            (GameRoot game, Faction alliance, Faction empire, _, Planet target) =
                BuildTwoFactionMessageScene();
            Fleet fleet = new Fleet
            {
                DisplayName = "Fleet 1",
                OwnerInstanceID = alliance.InstanceID,
            };

            Message message = FirstMessageFor(
                CreateMessages(
                    game,
                    new[]
                    {
                        Definition(
                            MessageResultType.BlockadeInitiated,
                            MessageType.Fleet,
                            "initiated:{faction}:{target}:{fleet}:{system}",
                            "body:{faction}:{target}:{fleet}:{system}",
                            FactionImages()
                        ),
                        Definition(
                            MessageResultType.BlockadeDetected,
                            MessageType.Fleet,
                            "detected:{faction}:{fleet}:{system}",
                            "body:{faction}:{fleet}:{system}",
                            FactionImages()
                        ),
                    },
                    new BlockadeChangedResult
                    {
                        Planet = target,
                        BlockadingFleet = fleet,
                        Blockaded = true,
                    }
                ),
                alliance
            );

            Assert.AreEqual(MessageType.Fleet, message.Type);
            Assert.AreEqual("initiated:Alliance:Empire:Fleet 1:Yavin", message.Title);
            Assert.AreEqual("empire-image", message.DisplayImagePath);
        }

        [Test]
        public void CreateMessages_EvacuationLosses_JoinsLostUnitNames()
        {
            (GameRoot game, Faction alliance, Planet origin, _) = BuildMessageScene();

            Message message = FirstMessageFor(
                CreateMessages(
                    game,
                    new[]
                    {
                        Definition(
                            MessageResultType.EvacuationLosses,
                            MessageType.Fleet,
                            "losses:{system}",
                            "body:{units}",
                            FactionImages()
                        ),
                    },
                    new EvacuationLossesResult
                    {
                        Faction = alliance,
                        Location = origin,
                        LostShips = { new CapitalShip { DisplayName = "Nebulon-B Frigate" } },
                        LostStarfighters = { new Starfighter { DisplayName = "X-wing Squadron" } },
                        LostRegiments = { new Regiment { DisplayName = "Infantry Regiment" } },
                    }
                ),
                alliance
            );

            Assert.AreEqual(MessageType.Fleet, message.Type);
            Assert.AreEqual("losses:Coruscant", message.Title);
            Assert.AreEqual(
                "body:Nebulon-B Frigate\nX-wing Squadron\nInfantry Regiment",
                message.Body
            );
            Assert.AreEqual("alliance-image", message.DisplayImagePath);
        }

        [Test]
        public void CreateMessages_MaintenanceAutoscrap_ReportsDestroyedObject()
        {
            (GameRoot game, Faction alliance, Planet origin, _) = BuildMessageScene();
            Building shipyard = new Building
            {
                DisplayName = "Shipyard",
                OwnerInstanceID = alliance.InstanceID,
            };
            game.AttachNode(shipyard, origin);

            Message message = FirstMessageFor(
                CreateMessages(
                    game,
                    new[]
                    {
                        Definition(
                            MessageResultType.MaintenanceAutoscrap,
                            MessageType.Resource,
                            "maintenance:{item}:{system}",
                            "body:{item}:{system}",
                            FactionImages()
                        ),
                    },
                    new GameObjectAutoscrappedResult
                    {
                        DestroyedObject = shipyard,
                        Context = origin,
                    }
                ),
                alliance
            );

            Assert.AreEqual(MessageType.Resource, message.Type);
            Assert.AreEqual("maintenance:Shipyard:Coruscant", message.Title);
            Assert.AreEqual("body:Shipyard:Coruscant", message.Body);
            Assert.AreEqual("alliance-image", message.DisplayImagePath);
        }

        [Test]
        public void CreateMessages_SpaceBattle_UsesWinnerPerspective()
        {
            (GameRoot game, Faction alliance, Faction empire, _, Planet target) =
                BuildTwoFactionMessageScene();

            List<MessageDelivery> deliveries = CreateMessages(
                game,
                SpaceBattleDefinitions(),
                new SpaceCombatResult
                {
                    AttackerFleet = new Fleet { OwnerInstanceID = alliance.InstanceID },
                    DefenderFleet = new Fleet { OwnerInstanceID = empire.InstanceID },
                    Planet = target,
                    Winner = CombatSide.Attacker,
                }
            );

            Assert.AreEqual(
                "victory:Alliance:Empire:Yavin",
                FirstMessageFor(deliveries, alliance).Title
            );
            Assert.AreEqual(
                "defeat:Empire:Alliance:Yavin",
                FirstMessageFor(deliveries, empire).Title
            );
            Assert.AreEqual(
                "alliance-victory-image",
                FirstMessageFor(deliveries, alliance).DisplayImagePath
            );
            Assert.AreEqual(
                "empire-defeat-image",
                FirstMessageFor(deliveries, empire).DisplayImagePath
            );
        }

        [Test]
        public void CreateMessages_Bombardment_UsesOwnershipAndLossSelectors()
        {
            (GameRoot game, Faction alliance, Faction empire, _, Planet target) =
                BuildTwoFactionMessageScene();

            Message message = FirstMessageFor(
                CreateMessages(
                    game,
                    BombardmentDefinitions(),
                    new BombardmentResult
                    {
                        AttackingFaction = alliance,
                        Planet = target,
                        DestroyedBuildings = { new Building { DisplayName = "Shield Generator" } },
                    }
                ),
                alliance
            );

            Assert.AreEqual(MessageType.Conflict, message.Type);
            Assert.AreEqual("owned-target:Alliance:Empire:Yavin", message.Title);
            Assert.AreEqual("target-losses-image", message.DisplayImagePath);
        }

        [Test]
        public void CreateMessages_PlanetaryAssault_UsesOwnershipAndOutcomeSelectors()
        {
            (GameRoot game, Faction alliance, Faction empire, _, Planet target) =
                BuildTwoFactionMessageScene();

            Message message = FirstMessageFor(
                CreateMessages(
                    game,
                    AssaultDefinitions(),
                    new PlanetaryAssaultResult
                    {
                        AttackingFaction = alliance,
                        Planet = target,
                        Success = false,
                    }
                ),
                empire
            );

            Assert.AreEqual(MessageType.Conflict, message.Type);
            Assert.AreEqual("owned-failed:Alliance:Empire:Yavin", message.Title);
            Assert.AreEqual("alliance-image", message.DisplayImagePath);
        }

        private static List<MessageDelivery> CreateMessages(
            GameRoot game,
            MessageDefinition[] definitions,
            params GameResult[] results
        )
        {
            MessageFactory factory = new MessageFactory(definitions);
            return factory.CreateMessages(results, game);
        }

        private static Message FirstMessageFor(
            IEnumerable<MessageDelivery> deliveries,
            Faction faction
        )
        {
            return deliveries.First(delivery => delivery.Faction == faction).Message;
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
            ResearchDiscipline? researchDiscipline = null,
            MissionType missionType = MissionType.None,
            MissionReportDetail missionReportDetail = MissionReportDetail.None,
            string imageKey = null
        )
        {
            MessageDefinition definition = new MessageDefinition
            {
                ResultType = resultType,
                MessageType = messageType,
                Outcome = outcome,
                PlanetOwnership = planetOwnership,
                MissionType = missionType,
                MissionReportDetail = missionReportDetail,
                BuildingType = buildingType,
                ManufacturingType = manufacturingType,
                TitleTemplate = titleTemplate,
                BodyTemplate = bodyTemplate,
                ImageKey = imageKey,
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
                    new MessageImageMap
                    {
                        FNALL1 = "alliance-victory-image",
                        FNEMP1 = "empire-victory-image",
                    },
                    outcome: MessageResultOutcome.Victory
                ),
                Definition(
                    MessageResultType.SpaceBattle,
                    MessageType.Conflict,
                    "defeat:{faction}:{opponent}:{system}",
                    "body:{faction}:{opponent}:{system}",
                    new MessageImageMap
                    {
                        FNALL1 = "alliance-defeat-image",
                        FNEMP1 = "empire-defeat-image",
                    },
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
    }
}
