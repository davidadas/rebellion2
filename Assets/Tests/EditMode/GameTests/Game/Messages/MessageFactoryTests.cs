using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Rebellion.Game;
using Rebellion.Game.Advisor;
using Rebellion.Game.Factions;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Messages;
using Rebellion.Game.Missions;
using Rebellion.Game.Requests;
using Rebellion.Game.Research;
using Rebellion.Game.Results;
using Rebellion.Game.Units;
using Rebellion.SceneGraph;

namespace Rebellion.Tests.Game.Messages
{
    [TestFixture]
    public class MessageFactoryTests
    {
        private static readonly Dictionary<Message, MessageDeliveryRequest> _deliveriesByMessage =
            new Dictionary<Message, MessageDeliveryRequest>();
        private static readonly Dictionary<MessageDeliveryRequest, Message> _messagesByDelivery =
            new Dictionary<MessageDeliveryRequest, Message>();

        [Test]
        public void CreateMessages_RequestedMessage_UsesDataDefinedPresentation()
        {
            (GameRoot game, Faction alliance, _, Planet destination) = BuildMessageScene();
            Officer luke = EntityFactory.CreateOfficer("luke", alliance.InstanceID);
            luke.DisplayName = "Luke Skywalker";
            Officer vader = EntityFactory.CreateOfficer("vader", "empire");
            vader.DisplayName = "Darth Vader";
            game.AttachNode(luke, destination);

            MessageFactory factory = new MessageFactory(new MessageDefinition[0]);
            MessageDeliveryRequest delivery = factory
                .CreateAuthoredMessages(
                    new[]
                    {
                        new MessageDeliveryRequest
                        {
                            Recipient = alliance,
                            SubjectNode = luke,
                            RelatedSubjectNode = vader,
                            Location = destination,
                            MessageType = MessageType.Advice,
                            Subject = "{subject} at {location}",
                            Body = "{subject} confronts {relatedSubject} for {faction}",
                            BackgroundImagePath = "Story/image",
                            OverlayImagePath = "Officers/luke",
                            BackgroundAudioPath = "Story/dialogue",
                            OfficerVoicePath = "Officers/luke/dialogue",
                            SourceEventInstanceID = "STORY_EVENT",
                            AdvisorNotification = new AdvisorNotification
                            {
                                Preset = AdvisorNotificationPreset.SubjectReport,
                            },
                        },
                    }
                )
                .Single();
            Message message = AsMessage(delivery);

            Assert.AreEqual("Luke Skywalker at Yavin", message.Title);
            Assert.AreEqual("Luke Skywalker confronts Darth Vader for Alliance", message.Body);
            Assert.AreEqual("Story/image", message.DisplayImagePath);
            Assert.IsNull(message.BackgroundImageKey);
            Assert.AreEqual("Officers/luke", message.OverlayImagePath);
            Assert.AreEqual("Story/dialogue", message.BackgroundAudioPath);
            Assert.AreEqual("Officers/luke/dialogue", message.OfficerVoicePath);
            Assert.AreEqual(luke.InstanceID, message.NavigationTargetInstanceID);
            Assert.AreEqual(destination.InstanceID, message.EventLocationInstanceID);
            Assert.AreEqual(AdvisorSubjectNotification.Report, delivery.AdvisorSubjectNotification);
        }

        [Test]
        public void CreateMessages_GameplayResult_ProducesAutomaticMessage()
        {
            (GameRoot game, Faction alliance, Planet origin, _) = BuildMessageScene();
            Officer officer = new Officer { OwnerInstanceID = alliance.InstanceID };
            game.AttachNode(officer, origin);

            List<MessageDeliveryRequest> deliveries = CreateMessages(
                game,
                new[]
                {
                    Definition(
                        MessageResultType.OfficerCaptured,
                        MessageType.Mission,
                        "Captured",
                        "Captured"
                    ),
                },
                new OfficerCaptureStateResult
                {
                    TargetOfficer = officer,
                    IsCaptured = true,
                    Context = origin,
                }
            );

            Assert.AreEqual("Captured", AsMessage(deliveries.Single()).Title);
        }

        [Test]
        public void CreateMessages_AuthoredEventResult_DoesNotApplyDeliveryPolicy()
        {
            (GameRoot game, Faction alliance, Planet origin, _) = BuildMessageScene();
            Officer officer = new Officer { OwnerInstanceID = alliance.InstanceID };
            game.AttachNode(officer, origin);

            List<MessageDeliveryRequest> deliveries = CreateMessages(
                game,
                new[]
                {
                    Definition(
                        MessageResultType.OfficerCaptured,
                        MessageType.Mission,
                        "Captured",
                        "Captured"
                    ),
                },
                new OfficerCaptureStateResult
                {
                    TargetOfficer = officer,
                    IsCaptured = true,
                    Context = origin,
                    SourceEventInstanceID = "CAPTURE_EVENT",
                }
            );

            Assert.AreEqual("Captured", AsMessage(deliveries.Single()).Title);
        }

        [Test]
        public void CreateMessages_FleetArrival_InterpolatesFleetAndDestination()
        {
            (GameRoot game, Faction alliance, _, Planet destination) = BuildMessageScene();
            Fleet fleet = new Fleet
            {
                DisplayName = "Fleet 1",
                OwnerInstanceID = alliance.InstanceID,
            };
            game.AttachNode(fleet, destination);

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
                            imagePaths: FactionImages()
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
            Assert.AreEqual(fleet.InstanceID, message.NavigationTargetInstanceID);
            Assert.AreEqual(destination.InstanceID, message.EventLocationInstanceID);
        }

        [Test]
        public void CreateMessages_WithDefinitionVoicePath_StoresMessageAudioData()
        {
            (GameRoot game, Faction alliance, _, Planet destination) = BuildMessageScene();
            Fleet fleet = new Fleet
            {
                DisplayName = "Fleet 1",
                OwnerInstanceID = alliance.InstanceID,
            };
            game.AttachNode(fleet, destination);

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
                            voicePath: "Audio/SFX/StrategyView/Messages/test_voice"
                        ),
                    },
                    new UnitArrivedResult { Unit = fleet, Destination = destination }
                ),
                alliance
            );

            Assert.AreEqual(
                "Audio/SFX/StrategyView/Messages/test_voice",
                message.BackgroundAudioPath
            );
        }

        [Test]
        public void CreateMessages_WithDefinitionVoicePaths_UsesFactionAudioData()
        {
            (GameRoot game, _, Faction empire, _, Planet destination) =
                BuildTwoFactionMessageScene();
            Fleet fleet = new Fleet
            {
                DisplayName = "Fleet 1",
                OwnerInstanceID = empire.InstanceID,
            };
            game.AttachNode(fleet, destination);

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
                            voicePaths: new Dictionary<string, string>
                            {
                                { "FNALL1", "alliance-voice" },
                                { "FNEMP1", "empire-voice" },
                            }
                        ),
                    },
                    new UnitArrivedResult { Unit = fleet, Destination = destination }
                ),
                empire
            );

            Assert.AreEqual("empire-voice", message.BackgroundAudioPath);
        }

        [Test]
        public void CreateMessages_DetachedFleetArrival_CreatesArrivalDelivery()
        {
            (GameRoot game, Faction alliance, _, Planet destination) = BuildMessageScene();
            Fleet fleet = new Fleet
            {
                DisplayName = "Fleet 1",
                OwnerInstanceID = alliance.InstanceID,
            };
            game.AttachNode(fleet, destination);
            game.DetachNode(fleet);

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
                            imagePaths: FactionImages()
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
        public void CreateMessages_ShipArrivalsWithSameMovementGroup_GroupsShips()
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
            Fleet fleet = new Fleet
            {
                InstanceID = "FLEET1",
                OwnerInstanceID = alliance.InstanceID,
            };
            game.AttachNode(fleet, destination);
            game.AttachNode(firstShip, fleet);
            game.AttachNode(secondShip, fleet);

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
                            imagePaths: FactionImages()
                        ),
                    },
                    new UnitArrivedResult
                    {
                        Unit = firstShip,
                        Destination = destination,
                        MovementGroupID = "group-1",
                    },
                    new UnitArrivedResult
                    {
                        Unit = secondShip,
                        Destination = destination,
                        MovementGroupID = "group-1",
                    }
                ),
                alliance
            );

            Assert.AreEqual(MessageType.Fleet, message.Type);
            Assert.AreEqual("ships:Yavin", message.Title);
            Assert.AreEqual("body:Nebulon-B Frigate\nCorellian Corvette", message.Body);
            Assert.AreEqual("alliance-image", message.DisplayImagePath);
            Assert.AreEqual(firstShip.InstanceID, message.NavigationTargetInstanceID);
        }

        [Test]
        public void CreateMessages_PersonnelArrival_UsesReportingOfficerVoiceAndGroupsPersonnel()
        {
            (GameRoot game, Faction alliance, _, Planet destination) = BuildMessageScene();
            Officer reporter = new Officer
            {
                TypeID = "OFAL003",
                DisplayName = "Reporter",
                OwnerInstanceID = alliance.InstanceID,
                MessageImagePath = "reporter-image",
                VoiceSet = new OfficerVoiceSet
                {
                    PersonnelArrivedPaths = new List<string> { "arrival-voice" },
                },
            };
            Officer passenger = new Officer
            {
                DisplayName = "Passenger",
                OwnerInstanceID = alliance.InstanceID,
            };

            Message message = FirstMessageFor(
                CreateMessages(
                    game,
                    new[]
                    {
                        Definition(
                            MessageResultType.PersonnelArrivedByOfficerWithCompany,
                            MessageType.Mission,
                            "{officer}:{system}",
                            "{personnel}"
                        ),
                    },
                    new UnitArrivedResult
                    {
                        Unit = reporter,
                        Destination = destination,
                        MovementGroupID = "group-1",
                    },
                    new UnitArrivedResult
                    {
                        Unit = passenger,
                        Destination = destination,
                        MovementGroupID = "group-1",
                    }
                ),
                alliance
            );

            Assert.AreEqual("Reporter:Yavin", message.Title);
            Assert.AreEqual("Passenger", message.Body);
            Assert.AreEqual("reporter-image", message.OverlayImagePath);
            Assert.AreEqual("arrival-voice", message.OfficerVoicePath);
            Assert.AreEqual(
                AdvisorSubjectNotification.Report,
                DeliveryFor(message).AdvisorSubjectNotification
            );
            Assert.AreEqual(reporter.TypeID, DeliveryFor(message).AdvisorSubjectTypeID);
            Assert.AreEqual(reporter.InstanceID, message.NavigationTargetInstanceID);
        }

        [Test]
        public void CreateMessages_PersonnelArrivalsWithSameMovementGroup_UsesPersonnelReport()
        {
            (GameRoot game, Faction alliance, _, Planet destination) = BuildMessageScene();
            Officer firstOfficer = new Officer
            {
                InstanceID = "officer-1",
                DisplayName = "Luke Skywalker",
                OwnerInstanceID = alliance.InstanceID,
                MessageImagePath = "luke-card",
            };
            Officer secondOfficer = new Officer
            {
                InstanceID = "officer-2",
                DisplayName = "Han Solo",
                OwnerInstanceID = alliance.InstanceID,
                MessageImagePath = "han-card",
            };
            game.AttachNode(firstOfficer, destination);
            game.AttachNode(secondOfficer, destination);

            List<MessageDeliveryRequest> deliveries = CreateMessages(
                game,
                new[]
                {
                    Definition(
                        MessageResultType.PersonnelArrived,
                        MessageType.Mission,
                        "personnel:{system}",
                        "body:{personnel}",
                        imageKey: "mission_report"
                    ),
                },
                new UnitArrivedResult
                {
                    Unit = firstOfficer,
                    Destination = destination,
                    MovementGroupID = "group-1",
                },
                new UnitArrivedResult
                {
                    Unit = secondOfficer,
                    Destination = destination,
                    MovementGroupID = "group-1",
                }
            );

            Message message = FirstMessageFor(deliveries, alliance);
            Assert.AreEqual(1, deliveries.Count);
            Assert.AreEqual(MessageType.Mission, message.Type);
            Assert.AreEqual("personnel:Yavin", message.Title);
            Assert.AreEqual("body:Luke Skywalker\nHan Solo", message.Body);
            Assert.AreEqual("mission_report", message.BackgroundImageKey);
            Assert.AreEqual("luke-card", message.OverlayImagePath);
        }

        [Test]
        public void CreateMessages_SpecialForcesArrival_GroupsWithReportingOfficer()
        {
            (GameRoot game, Faction alliance, _, Planet destination) = BuildMessageScene();
            Officer reporter = new Officer
            {
                TypeID = "OFAL003",
                DisplayName = "Luke Skywalker",
                OwnerInstanceID = alliance.InstanceID,
                VoiceSet = new OfficerVoiceSet
                {
                    PersonnelArrivedPaths = new List<string> { "arrival-voice" },
                },
            };
            SpecialForces infiltrators = new SpecialForces
            {
                DisplayName = "Infiltrators",
                OwnerInstanceID = alliance.InstanceID,
            };

            Message message = FirstMessageFor(
                CreateMessages(
                    game,
                    new[]
                    {
                        Definition(
                            MessageResultType.PersonnelArrivedByOfficerWithCompany,
                            MessageType.Mission,
                            "{officer} Arrives",
                            "Personnel:\n{personnel}"
                        ),
                    },
                    new UnitArrivedResult
                    {
                        Unit = reporter,
                        Destination = destination,
                        MovementGroupID = "group-1",
                    },
                    new UnitArrivedResult
                    {
                        Unit = infiltrators,
                        Destination = destination,
                        MovementGroupID = "group-1",
                    }
                ),
                alliance
            );

            Assert.AreEqual("Luke Skywalker Arrives", message.Title);
            Assert.AreEqual("Personnel:\nInfiltrators", message.Body);
            Assert.AreEqual("arrival-voice", message.OfficerVoicePath);
        }

        [Test]
        public void CreateMessages_CombatUnitArrivalsWithSameMovementGroup_UseGroupedUnitsReport()
        {
            (GameRoot game, Faction alliance, _, Planet destination) = BuildMessageScene();
            Regiment regiment = new Regiment
            {
                DisplayName = "Infantry Regiment",
                OwnerInstanceID = alliance.InstanceID,
            };
            Starfighter fighters = new Starfighter
            {
                DisplayName = "X-wing Squadron",
                OwnerInstanceID = alliance.InstanceID,
            };

            List<MessageDeliveryRequest> deliveries = CreateMessages(
                game,
                new[]
                {
                    Definition(
                        MessageResultType.UnitsArrived,
                        MessageType.Fleet,
                        "Units Arrive at {system}",
                        "Units:\n{units}"
                    ),
                },
                new UnitArrivedResult
                {
                    Unit = regiment,
                    Destination = destination,
                    MovementGroupID = "group-1",
                },
                new UnitArrivedResult
                {
                    Unit = fighters,
                    Destination = destination,
                    MovementGroupID = "group-1",
                }
            );

            Assert.AreEqual(1, deliveries.Count);
            Assert.AreEqual("Units Arrive at Yavin", AsMessage(deliveries[0]).Title);
            Assert.AreEqual(
                "Units:\nInfantry Regiment\nX-wing Squadron",
                AsMessage(deliveries[0]).Body
            );
            Assert.AreEqual(AdvisorNotificationType.UnitsArrived, deliveries[0].NotificationType);
        }

        [Test]
        public void CreateMessages_HeadquartersArrival_UsesSpecialReport()
        {
            (GameRoot game, Faction alliance, _, Planet destination) = BuildMessageScene();
            Building headquarters = new Building
            {
                InstanceID = "headquarters-1",
                DisplayName = "Alliance Headquarters",
                OwnerInstanceID = alliance.InstanceID,
                BuildingType = BuildingType.Headquarters,
            };

            Message message = FirstMessageFor(
                CreateMessages(
                    game,
                    new[]
                    {
                        Definition(
                            MessageResultType.HeadquartersArrived,
                            MessageType.Fleet,
                            "Headquarters Arrives",
                            "The Alliance Headquarters has arrived at {system}"
                        ),
                    },
                    new UnitArrivedResult { Unit = headquarters, Destination = destination }
                ),
                alliance
            );

            Assert.AreEqual("Headquarters Arrives", message.Title);
            Assert.AreEqual("The Alliance Headquarters has arrived at Yavin", message.Body);
            Assert.AreEqual(headquarters.InstanceID, message.NavigationTargetInstanceID);
        }

        [Test]
        public void CreateMessages_HeadquartersArrival_RequiresMatchingFactionDefinition()
        {
            (GameRoot game, Faction alliance, Faction empire, _, Planet destination) =
                BuildTwoFactionMessageScene();
            MessageDefinition allianceHeadquarters = Definition(
                MessageResultType.HeadquartersArrived,
                MessageType.Fleet,
                "Headquarters Arrives",
                "The Alliance Headquarters has arrived at {system}"
            );
            allianceHeadquarters.FactionInstanceID = alliance.InstanceID;
            Building imperialHeadquarters = new Building
            {
                InstanceID = "imperial-headquarters",
                DisplayName = "Imperial Headquarters",
                OwnerInstanceID = empire.InstanceID,
                BuildingType = BuildingType.Headquarters,
            };

            List<MessageDeliveryRequest> deliveries = CreateMessages(
                game,
                new[] { allianceHeadquarters },
                new UnitArrivedResult { Unit = imperialHeadquarters, Destination = destination }
            );

            Assert.IsEmpty(deliveries);
        }

        [Test]
        public void CreateMessages_DetachedShipArrival_CreatesArrivalDelivery()
        {
            (GameRoot game, Faction alliance, _, Planet destination) = BuildMessageScene();
            Fleet fleet = new Fleet
            {
                InstanceID = "FLEET1",
                OwnerInstanceID = alliance.InstanceID,
            };
            CapitalShip ship = new CapitalShip
            {
                DisplayName = "Nebulon-B Frigate",
                OwnerInstanceID = alliance.InstanceID,
                EncyclopediaImagePath = "ship-encyclopedia-image",
            };
            game.AttachNode(fleet, destination);
            game.AttachNode(ship, fleet);
            game.DetachNode(ship);

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
                            imagePaths: FactionImages()
                        ),
                    },
                    new UnitArrivedResult
                    {
                        Unit = ship,
                        Destination = destination,
                        MovementGroupID = "group-1",
                    }
                ),
                alliance
            );

            Assert.AreEqual(MessageType.Fleet, message.Type);
            Assert.AreEqual("ships:Yavin", message.Title);
            Assert.AreEqual("body:Nebulon-B Frigate", message.Body);
            Assert.AreEqual("alliance-image", message.DisplayImagePath);
        }

        [Test]
        public void CreateMessages_ShipArrivalsWithDifferentMovementGroups_ReturnsSeparateMessages()
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
            Fleet fleet = new Fleet
            {
                InstanceID = "FLEET1",
                OwnerInstanceID = alliance.InstanceID,
            };
            game.AttachNode(fleet, destination);
            game.AttachNode(firstShip, fleet);
            game.AttachNode(secondShip, fleet);

            List<MessageDeliveryRequest> deliveries = CreateMessages(
                game,
                new[]
                {
                    Definition(
                        MessageResultType.ShipsArrived,
                        MessageType.Fleet,
                        "ships:{system}",
                        "body:{ships}",
                        imagePaths: FactionImages()
                    ),
                },
                new UnitArrivedResult
                {
                    Unit = firstShip,
                    Destination = destination,
                    MovementGroupID = "group-1",
                },
                new UnitArrivedResult
                {
                    Unit = secondShip,
                    Destination = destination,
                    MovementGroupID = "group-2",
                }
            );

            List<Message> messages = deliveries.ConvertAll(AsMessage);

            Assert.AreEqual(2, messages.Count);
            Assert.IsTrue(messages.Any(message => message.Body == "body:Nebulon-B Frigate"));
            Assert.IsTrue(messages.Any(message => message.Body == "body:Corellian Corvette"));
        }

        [Test]
        public void CreateMessages_ShipArrivalsWithoutMovementGroup_ReturnsSeparateMessages()
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
            Fleet fleet = new Fleet
            {
                InstanceID = "FLEET1",
                OwnerInstanceID = alliance.InstanceID,
            };
            game.AttachNode(fleet, destination);
            game.AttachNode(firstShip, fleet);
            game.AttachNode(secondShip, fleet);

            List<MessageDeliveryRequest> deliveries = CreateMessages(
                game,
                new[]
                {
                    Definition(
                        MessageResultType.ShipsArrived,
                        MessageType.Fleet,
                        "ships:{system}",
                        "body:{ships}",
                        imagePaths: FactionImages()
                    ),
                },
                new UnitArrivedResult { Unit = firstShip, Destination = destination },
                new UnitArrivedResult { Unit = secondShip, Destination = destination }
            );

            Assert.AreEqual(2, deliveries.Count);
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
                            "body:{item}:{attachment}",
                            imageKey: "capital_ship_repaired"
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
            Assert.AreEqual("capital_ship_repaired", message.BackgroundImageKey);
            Assert.IsNull(message.DisplayImagePath);
            Assert.IsNull(message.OverlayImagePath);
        }

        [Test]
        public void CreateMessages_CapitalShipRepaired_UsesDefinitionImageInsteadOfUnitCard()
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
                        "body:{item}:{attachment}",
                        imagePath: DefaultImage("repair-background")
                    ),
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

            Assert.AreEqual("repair-background", message.DisplayImagePath);
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
                            "body:{item}:{attachment}",
                            imageKey: "starfighter_repaired"
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
            Assert.AreEqual("starfighter_repaired", message.BackgroundImageKey);
            Assert.IsNull(message.DisplayImagePath);
            Assert.IsNull(message.OverlayImagePath);
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

            List<MessageDeliveryRequest> deliveries = CreateMessages(
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
        public void CreateMessages_DeployedCombatUnits_UseUnitSpecificReports()
        {
            (GameRoot game, Faction alliance, Planet origin, _) = BuildMessageScene();
            Fleet fleet = new Fleet { InstanceID = "fleet", OwnerInstanceID = alliance.InstanceID };
            game.AttachNode(fleet, origin);
            CapitalShip ship = new CapitalShip
            {
                DisplayName = "Nebulon-B Frigate",
                OwnerInstanceID = alliance.InstanceID,
                EncyclopediaImagePath = "ship-encyclopedia-image",
            };
            CapitalShip deathStar = new CapitalShip
            {
                DisplayName = "Death Star Alpha",
                TypeID = "DEATH_STAR",
                CanDestroyPlanets = true,
                OwnerInstanceID = alliance.InstanceID,
                EncyclopediaImagePath = "death-star-encyclopedia-image",
            };
            Starfighter fighter = new Starfighter
            {
                DisplayName = "X-wing",
                OwnerInstanceID = alliance.InstanceID,
                MessageImagePath = "fighter-message-image",
                EncyclopediaImagePath = "fighter-encyclopedia-image",
            };
            Regiment regiment = new Regiment
            {
                DisplayName = "Mon Calamari Regiment",
                OwnerInstanceID = alliance.InstanceID,
                EncyclopediaImagePath = "regiment-encyclopedia-image",
            };
            Regiment secondRegiment = new Regiment
            {
                DisplayName = "Wookiee Regiment",
                OwnerInstanceID = alliance.InstanceID,
            };
            game.AttachNode(ship, fleet);
            game.AttachNode(deathStar, fleet);
            game.AttachNode(fighter, origin);
            game.AttachNode(regiment, origin);
            game.AttachNode(secondRegiment, origin);

            List<MessageDeliveryRequest> deliveries = CreateMessages(
                game,
                new[]
                {
                    Definition(
                        MessageResultType.CapitalShipDeployed,
                        MessageType.Manufacturing,
                        "{type} Deployed at {system}",
                        "The new {type}, {item}, has been deployed at {system}."
                    ),
                    Definition(
                        MessageResultType.DeathStarDeployed,
                        MessageType.Manufacturing,
                        "New Death Star Deployed at {system}",
                        "The new Death Star, {item}, has been deployed at {system}."
                    ),
                    Definition(
                        MessageResultType.StarfighterDeployed,
                        MessageType.Manufacturing,
                        "{item} Squadron Deployed at {system}",
                        "A new {item} squadron has been deployed to {system}.",
                        voicePaths: new Dictionary<string, string>
                        {
                            { alliance.InstanceID, "fighter-voice" },
                        }
                    ),
                    Definition(
                        MessageResultType.RegimentDeployed,
                        MessageType.Manufacturing,
                        "{item} Deployed to {system}",
                        "The following units have been deployed to {system}:\n{items}"
                    ),
                },
                new GameObjectDeployedResult { GameObject = ship },
                new GameObjectDeployedResult { GameObject = deathStar },
                new GameObjectDeployedResult { GameObject = fighter },
                new GameObjectDeployedResult { GameObject = regiment },
                new GameObjectDeployedResult { GameObject = secondRegiment }
            );

            Message[] messages = deliveries.Select(AsMessage).ToArray();
            Assert.AreEqual(4, messages.Length);
            Assert.AreEqual("Nebulon-B Frigate Deployed at Coruscant", messages[0].Title);
            Assert.AreEqual(
                "The new Nebulon-B Frigate, Nebulon-B Frigate, has been deployed at Coruscant.",
                messages[0].Body
            );
            Assert.AreEqual("ship-encyclopedia-image", messages[0].DisplayImagePath);
            Assert.AreEqual("New Death Star Deployed at Coruscant", messages[1].Title);
            Assert.AreEqual(
                "The new Death Star, Death Star Alpha, has been deployed at Coruscant.",
                messages[1].Body
            );
            Assert.AreEqual("death-star-encyclopedia-image", messages[1].DisplayImagePath);
            Assert.AreEqual("X-wing Squadron Deployed at Coruscant", messages[2].Title);
            Assert.AreEqual("fighter-voice", messages[2].BackgroundAudioPath);
            Assert.AreEqual("fighter-encyclopedia-image", messages[2].DisplayImagePath);
            Assert.AreEqual("Mon Calamari Regiment Deployed to Coruscant", messages[3].Title);
            Assert.AreEqual(
                "The following units have been deployed to Coruscant:\n"
                    + "Mon Calamari Regiment\nWookiee Regiment",
                messages[3].Body
            );
            Assert.AreEqual("regiment-encyclopedia-image", messages[3].DisplayImagePath);
        }

        [Test]
        public void CreateMessages_FacilityDestroyedOnArrival_ReturnsFacilityLostReport()
        {
            (GameRoot game, Faction alliance, _, Planet destination) = BuildMessageScene();
            Building shipyard = new Building
            {
                DisplayName = "Shipyard",
                OwnerInstanceID = alliance.InstanceID,
                BuildingType = BuildingType.Shipyard,
            };

            Message message = FirstMessageFor(
                CreateMessages(
                    game,
                    new[]
                    {
                        Definition(
                            MessageResultType.FacilityLost,
                            MessageType.Manufacturing,
                            "Facility Lost",
                            "New {item} could not be deployed to {system}.  The facility has been scrapped.",
                            imagePaths: FactionImages()
                        ),
                    },
                    new GameObjectDestroyedOnArrivalResult
                    {
                        DestroyedObject = shipyard,
                        Context = destination,
                    }
                ),
                alliance
            );

            Assert.AreEqual(MessageType.Manufacturing, message.Type);
            Assert.AreEqual("Facility Lost", message.Title);
            Assert.AreEqual(
                "New Shipyard could not be deployed to Yavin.  The facility has been scrapped.",
                message.Body
            );
            Assert.AreEqual("alliance-image", message.DisplayImagePath);
            Assert.AreEqual(destination.InstanceID, message.NavigationTargetInstanceID);
            Assert.AreEqual(destination.InstanceID, message.EventLocationInstanceID);
            Assert.AreEqual(
                AdvisorNotificationType.Maintenance,
                DeliveryFor(message).NotificationType
            );
        }

        [Test]
        public void CreateMessages_RegimentDestroyedOnArrival_DoesNotReturnFacilityLostReport()
        {
            (GameRoot game, Faction alliance, _, Planet destination) = BuildMessageScene();
            Regiment regiment = new Regiment
            {
                DisplayName = "Army Regiment",
                OwnerInstanceID = alliance.InstanceID,
            };

            List<MessageDeliveryRequest> deliveries = CreateMessages(
                game,
                new[]
                {
                    Definition(
                        MessageResultType.FacilityLost,
                        MessageType.Manufacturing,
                        "Facility Lost",
                        "lost"
                    ),
                },
                new GameObjectDestroyedOnArrivalResult
                {
                    DestroyedObject = regiment,
                    Context = destination,
                }
            );

            Assert.IsEmpty(deliveries);
        }

        [Test]
        public void CreateMessages_SmugglingStarted_ReturnsLossAndBenefitReports()
        {
            (GameRoot game, Faction alliance, Faction empire, _, Planet target) =
                BuildTwoFactionMessageScene();

            List<MessageDeliveryRequest> deliveries = CreateMessages(
                game,
                SmugglingDefinitions(),
                new SmugglingChangedResult
                {
                    Planet = target,
                    Controller = empire,
                    Beneficiary = alliance,
                    OldPercent = 0,
                    NewPercent = 50,
                }
            );

            Message loss = FirstMessageFor(deliveries, empire);
            Assert.AreEqual(MessageType.Resource, loss.Type);
            Assert.AreEqual("Smuggling Losses", loss.Title);
            Assert.AreEqual(
                "Dissention among the population has allowed smugglers to begin operations on Yavin.  As a result, valuable resources are being lost.",
                loss.Body
            );
            Assert.AreEqual("empire-smuggling-voice", loss.BackgroundAudioPath);
            Assert.AreEqual(target.InstanceID, loss.NavigationTargetInstanceID);

            Message benefit = FirstMessageFor(deliveries, alliance);
            Assert.AreEqual("Smuggling Benefits", benefit.Title);
            Assert.AreEqual(
                "Smugglers from Yavin are providing us with additional resources.",
                benefit.Body
            );
            Assert.AreEqual("alliance-smuggling-voice", benefit.BackgroundAudioPath);
            Assert.AreEqual(target.InstanceID, benefit.EventLocationInstanceID);
        }

        [Test]
        public void CreateMessages_SmugglingEnded_ReturnsLossAndBenefitEndReports()
        {
            (GameRoot game, Faction alliance, Faction empire, _, Planet target) =
                BuildTwoFactionMessageScene();

            List<MessageDeliveryRequest> deliveries = CreateMessages(
                game,
                SmugglingDefinitions(),
                new SmugglingChangedResult
                {
                    Planet = target,
                    Controller = empire,
                    Beneficiary = alliance,
                    OldPercent = 50,
                    NewPercent = 0,
                }
            );

            Message lossEnd = FirstMessageFor(deliveries, empire);
            Assert.AreEqual("Smuggling Losses End", lossEnd.Title);
            Assert.AreEqual(
                "Increasing support on Yavin has put an end to the smuggling losses there.",
                lossEnd.Body
            );
            Assert.IsNull(lossEnd.BackgroundAudioPath);

            Message benefitEnd = FirstMessageFor(deliveries, alliance);
            Assert.AreEqual("Smuggling Benefits End", benefitEnd.Title);
            Assert.AreEqual(
                "Popular opinion on Yavin has caused smugglers from that system to withdraw their support.",
                benefitEnd.Body
            );
            Assert.IsNull(benefitEnd.BackgroundAudioPath);
        }

        [Test]
        public void CreateMessages_ManufacturingIdle_UsesQueueTypeDefinition()
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
                    new ManufacturingIdleResult
                    {
                        Faction = alliance,
                        ManufacturingType = ManufacturingType.Building,
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
            Mission mission = new SabotageMission
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
                            imagePaths: FactionImages(),
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
        public void CreateMessages_EspionageSuccess_AppendsConfiguredAdditionalSectors()
        {
            (GameRoot game, Faction alliance, _, _, Planet target) = BuildTwoFactionMessageScene();
            Mission mission = new EspionageMission
            {
                InstanceID = "espionage-mission",
                DisplayName = "Espionage",
                ConfigKey = MissionTypeIDs.Espionage,
                OwnerInstanceID = alliance.InstanceID,
            };
            game.AttachNode(mission, target);
            PlanetSector corellia = new PlanetSector { DisplayName = "Corellia" };
            PlanetSector sullust = new PlanetSector { DisplayName = "Sullust" };
            game.AttachNode(corellia, game.Galaxy);
            game.AttachNode(sullust, game.Galaxy);
            MessageDefinition definition = Definition(
                MessageResultType.MissionReport,
                MessageType.Mission,
                "title",
                "Successful.  {details}",
                outcome: MessageResultOutcome.Success,
                missionTypeId: MissionTypeIDs.Espionage
            );
            definition.DetailListHeaderTemplate = "Additional sectors:";
            definition.DetailListItemTemplate = "\n     {sector}";

            Message message = FirstMessageFor(
                CreateMessages(
                    game,
                    new[] { definition },
                    new PlanetSectorsRevealedResult
                    {
                        MissionInstanceID = mission.InstanceID,
                        AdditionalSectors = new List<PlanetSector> { corellia, sullust },
                    },
                    new MissionCompletedResult
                    {
                        Mission = mission,
                        MissionInstanceID = mission.InstanceID,
                        MissionName = "Espionage",
                        MissionTypeID = MissionTypeIDs.Espionage,
                        Outcome = MissionOutcome.Success,
                    }
                ),
                alliance
            );

            Assert.AreEqual(
                "Successful.  Additional sectors:\n     Corellia\n     Sullust",
                message.Body
            );
        }

        [Test]
        public void CreateMessages_EspionageSuccessWithoutAdditionalSectors_OmitsDetails()
        {
            (GameRoot game, Faction alliance, _, _, Planet target) = BuildTwoFactionMessageScene();
            Mission mission = new EspionageMission
            {
                InstanceID = "espionage-mission",
                DisplayName = "Espionage",
                ConfigKey = MissionTypeIDs.Espionage,
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
                            "title",
                            "Successful.  {details}",
                            outcome: MessageResultOutcome.Success,
                            missionTypeId: MissionTypeIDs.Espionage
                        ),
                    },
                    new MissionCompletedResult
                    {
                        Mission = mission,
                        MissionInstanceID = mission.InstanceID,
                        MissionName = "Espionage",
                        MissionTypeID = MissionTypeIDs.Espionage,
                        Outcome = MissionOutcome.Success,
                    }
                ),
                alliance
            );

            Assert.AreEqual("Successful.  ", message.Body);
        }

        [Test]
        public void CreateMessages_RecruitmentSuccess_UsesRecruiterVoiceAndAdvisorSubject()
        {
            (GameRoot game, Faction alliance, Planet origin, _) = BuildMessageScene();
            Officer recruiter = new Officer
            {
                TypeID = "OFAL004",
                DisplayName = "Recruiter",
                OwnerInstanceID = alliance.InstanceID,
                VoiceSet = new OfficerVoiceSet
                {
                    MissionSuccessPaths = new List<string> { "success-voice" },
                },
            };
            Officer recruit = new Officer
            {
                InstanceID = "RECRUIT",
                DisplayName = "Recruit",
                OwnerInstanceID = alliance.InstanceID,
            };
            RecruitmentMission mission = new RecruitmentMission
            {
                OwnerInstanceID = alliance.InstanceID,
                RecruitedOfficerInstanceID = recruit.InstanceID,
            };
            game.AttachNode(recruit, origin);
            game.AttachNode(mission, origin);

            Message message = FirstMessageFor(
                CreateMessages(
                    game,
                    new[]
                    {
                        Definition(
                            MessageResultType.MissionReport,
                            MessageType.Mission,
                            "{participant} recruits {officer}",
                            "body",
                            outcome: MessageResultOutcome.Success,
                            missionTypeId: MissionTypeIDs.Recruitment
                        ),
                    },
                    new MissionCompletedResult
                    {
                        Mission = mission,
                        Outcome = MissionOutcome.Success,
                        Participants = new List<IMissionParticipant> { recruiter },
                    }
                ),
                alliance
            );

            Assert.AreEqual("Recruiter recruits Recruit", message.Title);
            Assert.AreEqual("success-voice", message.OfficerVoicePath);
            Assert.AreEqual(
                AdvisorSubjectNotification.Report,
                DeliveryFor(message).AdvisorSubjectNotification
            );
            Assert.AreEqual(recruiter.TypeID, DeliveryFor(message).AdvisorSubjectTypeID);
        }

        [Test]
        public void CreateMessages_MissionFailure_UsesReporterVoiceAndAdvisorSubject()
        {
            (GameRoot game, Faction alliance, _, _, Planet target) = BuildTwoFactionMessageScene();
            Officer reporter = new Officer
            {
                TypeID = "OFAL003",
                OwnerInstanceID = alliance.InstanceID,
                VoiceSet = new OfficerVoiceSet
                {
                    MissionFailurePaths = new List<string> { "failure-voice" },
                },
            };
            Mission mission = new SabotageMission
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
                            "failed",
                            "body",
                            outcome: MessageResultOutcome.Failed
                        ),
                    },
                    new MissionCompletedResult
                    {
                        Mission = mission,
                        Outcome = MissionOutcome.Failed,
                        Participants = new List<IMissionParticipant> { reporter },
                    }
                ),
                alliance
            );

            Assert.AreEqual("failure-voice", message.OfficerVoicePath);
            Assert.AreEqual(
                AdvisorSubjectNotification.Report,
                DeliveryFor(message).AdvisorSubjectNotification
            );
            Assert.AreEqual(reporter.TypeID, DeliveryFor(message).AdvisorSubjectTypeID);
        }

        [Test]
        public void CreateMessages_TargetUnavailableMission_UsesAbortVoice()
        {
            (GameRoot game, Faction alliance, _, _, Planet target) = BuildTwoFactionMessageScene();
            Officer reporter = new Officer
            {
                OwnerInstanceID = alliance.InstanceID,
                VoiceSet = new OfficerVoiceSet
                {
                    MissionFailurePaths = new List<string> { "failure-voice" },
                    MissionAbortPaths = new List<string> { "abort-voice" },
                },
            };
            Mission mission = new SabotageMission
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
                            "aborted",
                            "body",
                            outcome: MessageResultOutcome.Failed,
                            missionCompletionReason: MissionCompletionReason.TargetUnavailable
                        ),
                    },
                    new MissionCompletedResult
                    {
                        Mission = mission,
                        Outcome = MissionOutcome.Failed,
                        CompletionReason = MissionCompletionReason.TargetUnavailable,
                        Participants = new List<IMissionParticipant> { reporter },
                    }
                ),
                alliance
            );

            Assert.AreEqual("abort-voice", message.OfficerVoicePath);
        }

        [Test]
        public void CreateMessages_ContinuingMissionReport_CarriesMissionInstanceID()
        {
            (GameRoot game, Faction alliance, _, _, Planet target) = BuildTwoFactionMessageScene();
            Mission mission = new SabotageMission
            {
                InstanceID = "mission-1",
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
                            "success",
                            "body",
                            outcome: MessageResultOutcome.Success
                        ),
                    },
                    new MissionCompletedResult
                    {
                        Mission = mission,
                        MissionInstanceID = mission.InstanceID,
                        MissionName = "Sabotage",
                        Outcome = MissionOutcome.Success,
                        CanContinue = true,
                    }
                ),
                alliance
            );

            Assert.AreEqual(mission.InstanceID, message.MissionInstanceID);
        }

        [Test]
        public void CreateMessages_MissionReport_UsesCompletionReasonSpecificDefinition()
        {
            (GameRoot game, Faction alliance, _, _, Planet target) = BuildTwoFactionMessageScene();
            Mission mission = new SabotageMission
            {
                ConfigKey = MissionTypeIDs.Sabotage,
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
                            imagePaths: FactionImages(),
                            outcome: MessageResultOutcome.Failed,
                            missionTypeId: MissionTypeIDs.Sabotage
                        ),
                        Definition(
                            MessageResultType.MissionReport,
                            MessageType.Mission,
                            "missing:{mission}:{system}",
                            "missing-body:{mission}:{system}",
                            imagePaths: FactionImages(),
                            outcome: MessageResultOutcome.Failed,
                            missionTypeId: MissionTypeIDs.Sabotage,
                            missionCompletionReason: MissionCompletionReason.TargetUnavailable
                        ),
                    },
                    new MissionCompletedResult
                    {
                        Mission = mission,
                        MissionName = "Sabotage",
                        Outcome = MissionOutcome.Failed,
                        CompletionReason = MissionCompletionReason.TargetUnavailable,
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
            Mission mission = new SabotageMission
            {
                ConfigKey = MissionTypeIDs.Sabotage,
                DisplayName = "Sabotage",
                OwnerInstanceID = alliance.InstanceID,
            };
            game.AttachNode(mission, target);

            List<MessageDeliveryRequest> deliveries = CreateMessages(
                game,
                new[]
                {
                    Definition(
                        MessageResultType.MissionReport,
                        MessageType.Mission,
                        "failed:{mission}:{system}",
                        "body:{mission}:{system}",
                        imagePaths: FactionImages(),
                        outcome: MessageResultOutcome.Failed,
                        missionTypeId: MissionTypeIDs.Sabotage
                    ),
                },
                new MissionCompletedResult
                {
                    Mission = mission,
                    MissionName = "Sabotage",
                    Outcome = MissionOutcome.Failed,
                    CompletionReason = MissionCompletionReason.TargetUnavailable,
                }
            );

            Assert.IsFalse(deliveries.Any(delivery => delivery.Recipient == alliance));
        }

        [Test]
        public void CreateMessages_MissionSuccess_UsesMissionSpecificImage()
        {
            (GameRoot game, Faction alliance, _, _, Planet target) = BuildTwoFactionMessageScene();
            Mission mission = new DiplomacyMission
            {
                ConfigKey = MissionTypeIDs.Diplomacy,
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
                            imagePaths: FactionImages(),
                            outcome: MessageResultOutcome.Success
                        ),
                        Definition(
                            MessageResultType.MissionReport,
                            MessageType.Mission,
                            "success:{mission}:{system}",
                            "body:{mission}:{system}",
                            DefaultImage("diplomacy-image"),
                            outcome: MessageResultOutcome.Success,
                            missionTypeId: MissionTypeIDs.Diplomacy
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
            Mission mission = new AssassinationMission
            {
                ConfigKey = MissionTypeIDs.Assassination,
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
                            missionTypeId: MissionTypeIDs.Assassination
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
            Mission mission = new AssassinationMission
            {
                ConfigKey = MissionTypeIDs.Assassination,
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
                            missionTypeId: MissionTypeIDs.Assassination
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
        public void CreateMessages_ReconnaissanceReportWithSpecialForces_UsesReconUnitImageOverlay()
        {
            (GameRoot game, Faction alliance, _, _, Planet target) = BuildTwoFactionMessageScene();
            Mission mission = new ReconnaissanceMission
            {
                ConfigKey = MissionTypeIDs.Reconnaissance,
                DisplayName = "Reconnaissance",
                OwnerInstanceID = alliance.InstanceID,
            };
            SpecialForces reconUnit = new SpecialForces
            {
                DisplayName = "Longprobe Y-wing Recon Team",
                OwnerInstanceID = alliance.InstanceID,
                MessageImagePath = "recon-unit-image",
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
                            outcome: MessageResultOutcome.Success,
                            missionTypeId: MissionTypeIDs.Reconnaissance,
                            imageKey: "mission_report"
                        ),
                    },
                    new MissionCompletedResult
                    {
                        Mission = mission,
                        MissionName = "Reconnaissance",
                        MissionTypeID = MissionTypeIDs.Reconnaissance,
                        Outcome = MissionOutcome.Success,
                        Participants = new List<IMissionParticipant> { reconUnit },
                    }
                ),
                alliance
            );

            Assert.AreEqual("mission_report", message.BackgroundImageKey);
            Assert.AreEqual("recon-unit-image", message.OverlayImagePath);
        }

        [Test]
        public void CreateMessages_JediTrainingReport_UsesTrainerAsReporter()
        {
            (GameRoot game, Faction alliance, _, _, Planet target) = BuildTwoFactionMessageScene();
            Officer student = new Officer
            {
                InstanceID = "student",
                DisplayName = "Student",
                OwnerInstanceID = alliance.InstanceID,
                MessageImagePath = "student-card",
                VoiceSet = new OfficerVoiceSet
                {
                    MissionSuccessPaths = new List<string> { "student-success" },
                },
            };
            Officer trainer = new Officer
            {
                InstanceID = "trainer",
                DisplayName = "Trainer",
                OwnerInstanceID = alliance.InstanceID,
                MessageImagePath = "trainer-card",
                VoiceSet = new OfficerVoiceSet
                {
                    MissionSuccessPaths = new List<string> { "trainer-success" },
                },
            };
            JediTrainingMission mission = new JediTrainingMission
            {
                ConfigKey = MissionTypeIDs.JediTraining,
                DisplayName = "Jedi Training",
                OwnerInstanceID = alliance.InstanceID,
                TrainerInstanceID = trainer.InstanceID,
            };
            mission.AddChildren(new IMissionParticipant[] { student, trainer });
            game.AttachNode(mission, target);

            Message message = FirstMessageFor(
                CreateMessages(
                    game,
                    new[]
                    {
                        Definition(
                            MessageResultType.MissionReport,
                            MessageType.Mission,
                            "report:{participant}",
                            "body:{participant}",
                            outcome: MessageResultOutcome.Success,
                            missionTypeId: MissionTypeIDs.JediTraining,
                            imageKey: "mission_report"
                        ),
                    },
                    new MissionCompletedResult
                    {
                        Mission = mission,
                        MissionName = "Jedi Training",
                        MissionTypeID = MissionTypeIDs.JediTraining,
                        Outcome = MissionOutcome.Success,
                        Participants = new List<IMissionParticipant> { student, trainer },
                    }
                ),
                alliance
            );

            Assert.AreEqual("report:Trainer", message.Title);
            Assert.AreEqual("body:Trainer", message.Body);
            Assert.AreEqual("mission_report", message.BackgroundImageKey);
            Assert.AreEqual("trainer-card", message.OverlayImagePath);
            Assert.AreEqual("trainer-success", message.OfficerVoicePath);
        }

        [Test]
        public void CreateMessages_RecruitmentMissionSuccess_UsesParticipantImageOverlay()
        {
            (GameRoot game, Faction alliance, _, Planet origin, _) = BuildTwoFactionMessageScene();
            Mission mission = new RecruitmentMission
            {
                ConfigKey = MissionTypeIDs.Recruitment,
                DisplayName = "Recruitment",
                OwnerInstanceID = alliance.InstanceID,
                RecruitedOfficerInstanceID = "target-officer",
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
            game.AttachNode(mission, origin);
            game.AttachNode(targetOfficer, origin);

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
                            missionTypeId: MissionTypeIDs.Recruitment
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
            Assert.AreEqual("participant-card", message.OverlayImagePath);
        }

        [Test]
        public void CreateMessages_RecruitmentMissionExhausted_ReturnsRecruitmentDoneReport()
        {
            (GameRoot game, Faction alliance, _, Planet origin, _) = BuildTwoFactionMessageScene();

            List<Message> messages = CreateMessages(
                    game,
                    new[]
                    {
                        Definition(
                            MessageResultType.MissionReport,
                            MessageType.Mission,
                            "mission-report",
                            "mission-body",
                            DefaultImage("recruitment-image"),
                            outcome: MessageResultOutcome.Success,
                            missionTypeId: MissionTypeIDs.Recruitment
                        ),
                        Definition(
                            MessageResultType.RecruitmentExhausted,
                            MessageType.Mission,
                            "recruitment-done",
                            "recruitment-exhausted",
                            DefaultImage("recruitment-done-image")
                        ),
                    },
                    new RecruitmentExhaustedResult { Faction = alliance, Planet = origin }
                )
                .Where(delivery => delivery.Recipient == alliance)
                .Select(AsMessage)
                .ToList();

            Assert.AreEqual(1, messages.Count);
            Assert.AreEqual("recruitment-done", messages[0].Title);
            Assert.AreEqual("recruitment-exhausted", messages[0].Body);
            Assert.IsNull(messages[0].OverlayImagePath);
        }

        [Test]
        public void CreateMessages_MissionReportWithoutParticipantImages_UsesMissionReportImage()
        {
            (GameRoot game, Faction alliance, _, _, Planet target) = BuildTwoFactionMessageScene();
            Mission mission = new DiplomacyMission
            {
                ConfigKey = MissionTypeIDs.Diplomacy,
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
                            missionTypeId: MissionTypeIDs.Diplomacy
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
        public void CreateMessages_FoiledMission_ReturnsFoiledActorReportAndFoiledTargetReport()
        {
            (GameRoot game, Faction alliance, Faction empire, _, Planet target) =
                BuildTwoFactionMessageScene();
            Mission mission = new SabotageMission
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

            List<MessageDeliveryRequest> deliveries = CreateMessages(
                game,
                new[]
                {
                    Definition(
                        MessageResultType.MissionReport,
                        MessageType.Mission,
                        "actor-foiled:{mission}:{system}",
                        "body:{mission}:{system}",
                        imagePaths: FactionImages(),
                        outcome: MessageResultOutcome.Foiled
                    ),
                    Definition(
                        MessageResultType.EnemyMissionFoiled,
                        MessageType.Mission,
                        "foiled:{mission}:{system}",
                        "body:{mission}:{system}",
                        imagePaths: FactionImages(),
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

            Assert.AreEqual(
                "actor-foiled:Sabotage:Yavin",
                FirstMessageFor(deliveries, alliance).Title
            );
            Assert.AreEqual("foiled:Sabotage:Yavin", FirstMessageFor(deliveries, empire).Title);
            Assert.AreEqual(
                "alliance-image",
                FirstMessageFor(deliveries, alliance).DisplayImagePath
            );
            Assert.AreEqual("empire-image", FirstMessageFor(deliveries, empire).DisplayImagePath);
            Assert.AreEqual("agent-card", FirstMessageFor(deliveries, alliance).OverlayImagePath);
            Assert.IsNull(FirstMessageFor(deliveries, empire).OverlayImagePath);
            Assert.AreEqual(
                AdvisorNotificationType.AgentReport,
                FirstDeliveryFor(deliveries, empire).NotificationType
            );
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
                            DefaultImage("fallback-card"),
                            showOfficerOverlay: true
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
        public void CreateMessages_OfficerCapture_DoesNotOverlayTargetOfficerImage()
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
            Assert.IsNull(message.OverlayImagePath);
        }

        [Test]
        public void CreateMessages_OfficerCapture_NotifiesOwnerAndCaptor()
        {
            (GameRoot game, Faction alliance, Faction empire, Planet origin, _) =
                BuildTwoFactionMessageScene();
            Officer target = new Officer
            {
                TypeID = "OFAL001",
                DisplayName = "Target",
                OwnerInstanceID = alliance.InstanceID,
                CaptorInstanceID = empire.InstanceID,
                MessageImagePath = "target-card",
            };
            game.AttachNode(target, origin);

            List<MessageDeliveryRequest> deliveries = CreateMessages(
                game,
                new[]
                {
                    Definition(
                        MessageResultType.OfficerCaptured,
                        MessageType.Mission,
                        "owner:{officer}",
                        "owner:{officer}:{captor}:{system}",
                        imagePaths: FactionImages()
                    ),
                    Definition(
                        MessageResultType.EnemyOfficerCaptured,
                        MessageType.Mission,
                        "captor:{officer}",
                        "captor:{officer}:{system}",
                        imagePaths: FactionImages()
                    ),
                },
                new OfficerCaptureStateResult
                {
                    TargetOfficer = target,
                    IsCaptured = true,
                    Context = origin,
                }
            );

            Message ownerMessage = FirstMessageFor(deliveries, alliance);
            Message captorMessage = FirstMessageFor(deliveries, empire);

            Assert.AreEqual(2, deliveries.Count);
            Assert.AreEqual("owner:Target:Empire:Coruscant", ownerMessage.Body);
            Assert.AreEqual("captor:Target:Coruscant", captorMessage.Body);
            Assert.AreEqual("alliance-image", ownerMessage.DisplayImagePath);
            Assert.AreEqual("empire-image", captorMessage.DisplayImagePath);
            Assert.IsNull(ownerMessage.OverlayImagePath);
            Assert.IsNull(captorMessage.OverlayImagePath);
            Assert.AreEqual(
                AdvisorSubjectNotification.Captured,
                DeliveryFor(captorMessage).AdvisorSubjectNotification
            );
            Assert.AreEqual(target.TypeID, DeliveryFor(captorMessage).AdvisorSubjectTypeID);
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
                VoiceSet = new OfficerVoiceSet
                {
                    RecoveredPaths = new List<string> { "recovered-voice" },
                },
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
                            DefaultImage("fallback-card"),
                            showOfficerOverlay: true
                        ),
                    },
                    new OfficerInjuredResult { Officer = officer, Severity = 0 }
                ),
                alliance
            );

            Assert.AreEqual("recovered:Agent:Coruscant", message.Title);
            Assert.AreEqual("fallback-card", message.DisplayImagePath);
            Assert.AreEqual("agent-card", message.OverlayImagePath);
            Assert.AreEqual("recovered-voice", message.OfficerVoicePath);
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

            List<MessageDeliveryRequest> deliveries = CreateMessages(
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
                .Where(delivery => delivery.Recipient == alliance)
                .Select(AsMessage)
                .ToList();
            Assert.AreEqual(1, messages.Count);
            Assert.AreEqual("killed:Agent:Coruscant", messages[0].Title);
            Assert.AreEqual("killed-card", messages[0].DisplayImagePath);
            Assert.IsNull(messages[0].OverlayImagePath);
        }

        [Test]
        public void CreateMessages_AssassinatedOfficer_ReturnsImperialAssassinsReport()
        {
            (GameRoot game, Faction alliance, Faction empire, _, Planet target) =
                BuildTwoFactionMessageScene();
            Officer victim = new Officer
            {
                InstanceID = "victim",
                DisplayName = "Mon Mothma",
                OwnerInstanceID = alliance.InstanceID,
            };
            Officer assassin = new Officer
            {
                InstanceID = "assassin",
                DisplayName = "Menndo",
                OwnerInstanceID = empire.InstanceID,
            };

            Message message = FirstMessageFor(
                CreateMessages(
                    game,
                    new[]
                    {
                        Definition(
                            MessageResultType.OfficerAssassinated,
                            MessageType.Mission,
                            "{officer} Killed",
                            "{officer} was killed by Imperial Assassins at {system}.",
                            imagePaths: FactionImages()
                        ),
                    },
                    new OfficerKilledResult
                    {
                        TargetOfficer = victim,
                        Assassin = assassin,
                        Context = target,
                    }
                ),
                alliance
            );

            Assert.AreEqual("Mon Mothma Killed", message.Title);
            Assert.AreEqual("Mon Mothma was killed by Imperial Assassins at Yavin.", message.Body);
            Assert.AreEqual(target.InstanceID, message.EventLocationInstanceID);
            Assert.AreEqual(victim.InstanceID, message.NavigationTargetInstanceID);
        }

        [Test]
        public void CreateMessages_ForceExperience_ReturnsForceGrowthMessage()
        {
            (GameRoot game, Faction alliance, Planet origin, _) = BuildMessageScene();
            Officer officer = new Officer
            {
                DisplayName = "Student",
                OwnerInstanceID = alliance.InstanceID,
                IsForceSensitive = true,
                ForceValue = game.Config.Jedi.GetMinimumRank(ForceRankLabel.ForceKnight),
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
                    new ForceExperienceResult
                    {
                        Officer = officer,
                        ExperienceGained = 1,
                        PreviousForceRank =
                            game.Config.Jedi.GetMinimumRank(ForceRankLabel.ForceKnight) - 1,
                        CurrentForceRank = game.Config.Jedi.GetMinimumRank(
                            ForceRankLabel.ForceKnight
                        ),
                    }
                ),
                alliance
            );

            Assert.AreEqual(MessageType.Mission, message.Type);
            Assert.AreEqual("force", message.Title);
            Assert.AreEqual("body:Jedi Knight", message.Body);
            Assert.AreEqual("student-card", message.OverlayImagePath);
        }

        [Test]
        public void CreateMessages_ForceExperience_WithoutRankLabelChange_DoesNotReturnForceGrowthMessage()
        {
            (GameRoot game, Faction alliance, Planet origin, _) = BuildMessageScene();
            Officer officer = new Officer
            {
                DisplayName = "Student",
                OwnerInstanceID = alliance.InstanceID,
                IsForceSensitive = true,
                ForceValue = game.Config.Jedi.GetMinimumRank(ForceRankLabel.ForceKnight) + 1,
            };
            game.AttachNode(officer, origin);

            List<MessageDeliveryRequest> deliveries = CreateMessages(
                game,
                new[]
                {
                    Definition(MessageResultType.ForceGrowth, MessageType.Mission, "force", "body"),
                },
                new ForceExperienceResult
                {
                    Officer = officer,
                    ExperienceGained = 1,
                    PreviousForceRank = game.Config.Jedi.GetMinimumRank(ForceRankLabel.ForceKnight),
                    CurrentForceRank =
                        game.Config.Jedi.GetMinimumRank(ForceRankLabel.ForceKnight) + 1,
                }
            );

            Assert.IsEmpty(deliveries);
        }

        [Test]
        public void CreateMessages_ForceExperience_ReachesRankThreshold_ReturnsForceGrowthMessage()
        {
            (GameRoot game, Faction alliance, Planet origin, _) = BuildMessageScene();
            Officer officer = new Officer
            {
                OwnerInstanceID = alliance.InstanceID,
                IsForceSensitive = true,
                ForceValue = game.Config.Jedi.GetMinimumRank(ForceRankLabel.ForceKnight),
            };
            game.AttachNode(officer, origin);

            List<MessageDeliveryRequest> deliveries = CreateMessages(
                game,
                new[]
                {
                    Definition(MessageResultType.ForceGrowth, MessageType.Mission, "force", "body"),
                },
                new ForceExperienceResult
                {
                    Officer = officer,
                    PreviousForceRank =
                        game.Config.Jedi.GetMinimumRank(ForceRankLabel.ForceKnight) - 1,
                    CurrentForceRank = game.Config.Jedi.GetMinimumRank(ForceRankLabel.ForceKnight),
                }
            );

            Assert.AreEqual(1, deliveries.Count);
        }

        [TestCase(true, 0, "qualified")]
        [TestCase(true, -1, "student")]
        [TestCase(false, 0, "student")]
        public void CreateMessages_ForceUserDiscovered_SelectsReportByTrainerQualification(
            bool isJediTrainer,
            int rankOffset,
            string expectedTitle
        )
        {
            (GameRoot game, Faction alliance, Planet origin, _) = BuildMessageScene();
            Officer discoverer = new Officer
            {
                DisplayName = "Discoverer",
                InstanceID = "discoverer",
                OwnerInstanceID = alliance.InstanceID,
                IsForceSensitive = true,
                IsForceEligible = true,
                IsJediTrainer = isJediTrainer,
                ForceValue = game.Config.Jedi.ForceQualifiedThreshold + rankOffset,
                MessageImagePath = "discoverer-card",
            };
            Officer candidate = new Officer
            {
                DisplayName = "Student",
                InstanceID = "student",
                OwnerInstanceID = alliance.InstanceID,
                IsForceSensitive = true,
                ForceValue = game.Config.Jedi.GetMinimumRank(ForceRankLabel.Trainee),
                MessageImagePath = "student-card",
            };
            game.AttachNode(discoverer, origin);
            game.AttachNode(candidate, origin);

            Message message = FirstMessageFor(
                CreateMessages(
                    game,
                    new[]
                    {
                        Definition(
                            MessageResultType.ForceGrowth,
                            MessageType.Mission,
                            "force",
                            "growth"
                        ),
                        Definition(
                            MessageResultType.ForceUserDiscovered,
                            MessageType.Mission,
                            "qualified",
                            "discovered:{officer}"
                        ),
                        Definition(
                            MessageResultType.ForceUserDiscoveredByStudent,
                            MessageType.Mission,
                            "student",
                            "discovered:{officer}"
                        ),
                    },
                    new ForceExperienceResult { Officer = candidate, ExperienceGained = 5 },
                    new ForceDiscoveryResult
                    {
                        EventType = ForceEventType.ForceUserDiscovered,
                        Officer = candidate,
                        Discoverer = discoverer,
                    }
                ),
                alliance
            );

            Assert.AreEqual(expectedTitle, message.Title);
            Assert.AreEqual("discovered:Student", message.Body);
            Assert.AreEqual("discoverer-card", message.OverlayImagePath);
        }

        [Test]
        public void CreateMessages_TraitorDiscovered_UsesReportAndDiscovererPresentation()
        {
            (GameRoot game, Faction alliance, _, Planet origin, _) = BuildTwoFactionMessageScene();
            Officer discoverer = new Officer
            {
                DisplayName = "Luke Skywalker",
                InstanceID = "discoverer",
                OwnerInstanceID = alliance.InstanceID,
                MessageImagePath = "luke-card",
                VoiceSet = new OfficerVoiceSet
                {
                    TraitorDiscoveredPaths = new List<string> { "luke-discovers-traitor" },
                },
            };
            Officer traitor = new Officer
            {
                DisplayName = "Lando Calrissian",
                InstanceID = "traitor",
                OwnerInstanceID = alliance.InstanceID,
            };
            game.AttachNode(discoverer, origin);
            game.AttachNode(traitor, origin);

            List<MessageDeliveryRequest> deliveries = CreateMessages(
                game,
                new[]
                {
                    Definition(
                        MessageResultType.TraitorDiscovered,
                        MessageType.Mission,
                        "{discoverer} Discovers Traitor",
                        "Through the use of the Force, I have discovered that {traitor} has betrayed us to the {enemy}.",
                        showOfficerOverlay: true
                    ),
                },
                new TraitorDiscoveredResult
                {
                    Officer = traitor,
                    DiscoveredBy = discoverer,
                    Context = origin,
                }
            );

            Assert.AreEqual(1, deliveries.Count);
            Assert.AreSame(alliance, deliveries[0].Recipient);
            Message message = AsMessage(deliveries[0]);
            Assert.AreEqual("Luke Skywalker Discovers Traitor", message.Title);
            Assert.AreEqual(
                "Through the use of the Force, I have discovered that Lando Calrissian has betrayed us to the Empire.",
                message.Body
            );
            Assert.AreEqual("luke-card", message.OverlayImagePath);
            Assert.AreEqual("luke-discovers-traitor", message.OfficerVoicePath);
            Assert.AreEqual(origin.InstanceID, message.EventLocationInstanceID);
            Assert.AreEqual(traitor.InstanceID, message.NavigationTargetInstanceID);
            Assert.AreEqual(discoverer.InstanceID, message.NavigationSecondaryTargetInstanceID);
            Assert.AreEqual(discoverer.TypeID, DeliveryFor(message).AdvisorSubjectTypeID);
            Assert.AreEqual(
                AdvisorSubjectNotification.Report,
                DeliveryFor(message).AdvisorSubjectNotification
            );
        }

        [Test]
        public void CreateMessages_ForceUserDiscovered_DoesNotUseDialog()
        {
            (GameRoot game, Faction alliance, Planet origin, _) = BuildMessageScene();
            Officer discoverer = new Officer
            {
                TypeID = "OFAL003",
                OwnerInstanceID = alliance.InstanceID,
                IsForceSensitive = true,
                IsForceEligible = true,
                IsJediTrainer = true,
                ForceValue = game.Config.Jedi.ForceQualifiedThreshold,
                MessageImagePath = "discoverer-image",
                VoiceSet = new OfficerVoiceSet
                {
                    ForceUserDiscoveredPaths = new List<string> { "discovery-voice" },
                },
            };
            Officer candidate = new Officer
            {
                DisplayName = "Candidate",
                OwnerInstanceID = alliance.InstanceID,
                MessageImagePath = "candidate-image",
            };
            game.AttachNode(discoverer, origin);
            game.AttachNode(candidate, origin);

            Message message = FirstMessageFor(
                CreateMessages(
                    game,
                    new[]
                    {
                        Definition(
                            MessageResultType.ForceUserDiscovered,
                            MessageType.Mission,
                            "future jedi",
                            "body:{officer}"
                        ),
                    },
                    new ForceDiscoveryResult
                    {
                        EventType = ForceEventType.ForceUserDiscovered,
                        Officer = candidate,
                        Discoverer = discoverer,
                    }
                ),
                alliance
            );

            Assert.AreEqual("body:Candidate", message.Body);
            Assert.AreEqual("discoverer-image", message.OverlayImagePath);
            Assert.IsNull(message.OfficerVoicePath);
            Assert.AreEqual(AdvisorNotificationType.None, DeliveryFor(message).NotificationType);
            Assert.AreEqual(
                AdvisorSubjectNotification.None,
                DeliveryFor(message).AdvisorSubjectNotification
            );
            Assert.IsNull(DeliveryFor(message).AdvisorSubjectTypeID);
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
                            imagePaths: FactionImages()
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
        public void CreateMessages_SabotageResultsAtSameSector_UseOneCombinedReport()
        {
            (GameRoot game, _, Faction empire, _, Planet target) = BuildTwoFactionMessageScene();
            Building shield = new Building
            {
                DisplayName = "Shield Generator",
                OwnerInstanceID = empire.InstanceID,
            };
            Regiment regiment = new Regiment
            {
                DisplayName = "Stormtrooper Regiment",
                OwnerInstanceID = empire.InstanceID,
            };

            List<MessageDeliveryRequest> deliveries = CreateMessages(
                game,
                new[]
                {
                    Definition(
                        MessageResultType.SabotageStrike,
                        MessageType.Mission,
                        "Saboteurs Strike at {system}",
                        "Destroyed at {system}:\n{item}"
                    ),
                },
                new GameObjectSabotagedResult { SabotagedObject = shield, Context = target },
                new GameObjectSabotagedResult { SabotagedObject = regiment, Context = target }
            );

            Assert.AreEqual(1, deliveries.Count);
            Assert.AreEqual("Saboteurs Strike at Yavin", AsMessage(deliveries[0]).Title);
            Assert.AreEqual(
                "Destroyed at Yavin:\nShield Generator\nStormtrooper Regiment",
                AsMessage(deliveries[0]).Body
            );
        }

        [Test]
        public void CreateMessages_SabotageResultsWithSpecificPresentation_StayInSeparateReports()
        {
            (GameRoot game, _, Faction empire, _, Planet target) = BuildTwoFactionMessageScene();
            CapitalShip deathStar = new CapitalShip
            {
                TypeID = "CSEM015",
                DisplayName = "Death Star",
                OwnerInstanceID = empire.InstanceID,
            };
            Building shield = new Building
            {
                DisplayName = "Shield Generator",
                OwnerInstanceID = empire.InstanceID,
            };
            MessageDefinition generic = Definition(
                MessageResultType.SabotageStrike,
                MessageType.Mission,
                "generic",
                "generic:{item}"
            );
            MessageDefinition specific = Definition(
                MessageResultType.SabotageStrike,
                MessageType.Mission,
                "Death Star Sabotaged",
                "death-star"
            );
            specific.GameObjectTypeID = deathStar.TypeID;

            List<MessageDeliveryRequest> deliveries = CreateMessages(
                game,
                new[] { generic, specific },
                new GameObjectSabotagedResult { SabotagedObject = deathStar, Context = target },
                new GameObjectSabotagedResult { SabotagedObject = shield, Context = target }
            );

            Assert.AreEqual(2, deliveries.Count);
            Assert.IsTrue(
                deliveries.Any(delivery => AsMessage(delivery).Title == "Death Star Sabotaged")
            );
            Assert.IsTrue(
                deliveries.Any(delivery => AsMessage(delivery).Body == "generic:Shield Generator")
            );
        }

        [Test]
        public void CreateMessages_SabotagedConfiguredUnitType_UsesSpecificDefinition()
        {
            (GameRoot game, _, Faction empire, _, Planet target) = BuildTwoFactionMessageScene();
            CapitalShip deathStar = new CapitalShip
            {
                TypeID = "CSEM015",
                DisplayName = "Death Star",
                OwnerInstanceID = empire.InstanceID,
            };
            MessageDefinition generic = Definition(
                MessageResultType.SabotageStrike,
                MessageType.Mission,
                "generic",
                "generic"
            );
            MessageDefinition specific = Definition(
                MessageResultType.SabotageStrike,
                MessageType.Mission,
                "Death Star Sabotaged",
                "The Rebel Alliance has sabotaged the Death Star at {system}."
            );
            specific.GameObjectTypeID = deathStar.TypeID;

            Message message = FirstMessageFor(
                CreateMessages(
                    game,
                    new[] { generic, specific },
                    new GameObjectSabotagedResult { SabotagedObject = deathStar, Context = target }
                ),
                empire
            );

            Assert.AreEqual("Death Star Sabotaged", message.Title);
            Assert.AreEqual(
                "The Rebel Alliance has sabotaged the Death Star at Yavin.",
                message.Body
            );
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

            List<MessageDeliveryRequest> deliveries = CreateMessages(
                game,
                new[]
                {
                    Definition(
                        MessageResultType.UprisingStarted,
                        MessageType.PopularSupport,
                        "started:{faction}:{system}",
                        "body:{faction}:{system}",
                        imagePaths: FactionImages()
                    ),
                },
                new PlanetUprisingStartedResult { Planet = target, InstigatorFaction = alliance }
            );

            Assert.AreEqual("started:Empire:Yavin", FirstMessageFor(deliveries, empire).Title);
            Assert.AreEqual("started:Empire:Yavin", FirstMessageFor(deliveries, alliance).Title);
        }

        [Test]
        public void CreateMessages_NearUprising_ReturnsControllerPopularSupportReport()
        {
            (GameRoot game, Faction alliance, Faction empire, _, Planet target) =
                BuildTwoFactionMessageScene();

            List<MessageDeliveryRequest> deliveries = CreateMessages(
                game,
                new[]
                {
                    Definition(
                        MessageResultType.NearUprising,
                        MessageType.PopularSupport,
                        "{system} Near Uprising",
                        "Unrest has pushed {system} close to uprising.",
                        imagePaths: FactionImages(),
                        voicePaths: new Dictionary<string, string>
                        {
                            { "FNALL1", "alliance-unrest" },
                            { "FNEMP1", "empire-unrest" },
                        }
                    ),
                },
                new PlanetNearUprisingResult { Planet = target }
            );

            Assert.AreEqual(1, deliveries.Count);
            Assert.AreEqual(empire, deliveries[0].Recipient);
            Assert.AreNotEqual(alliance, deliveries[0].Recipient);
            Assert.AreEqual(MessageType.PopularSupport, AsMessage(deliveries[0]).Type);
            Assert.AreEqual("Yavin Near Uprising", AsMessage(deliveries[0]).Title);
            Assert.AreEqual(
                "Unrest has pushed Yavin close to uprising.",
                AsMessage(deliveries[0]).Body
            );
            Assert.AreEqual("empire-image", AsMessage(deliveries[0]).DisplayImagePath);
            Assert.AreEqual("empire-unrest", AsMessage(deliveries[0]).BackgroundAudioPath);
            Assert.AreEqual(
                AdvisorNotificationType.NegativePopularSupport,
                deliveries[0].NotificationType
            );
            Assert.AreEqual(target.InstanceID, AsMessage(deliveries[0]).EventLocationInstanceID);
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
                            imagePaths: FactionImages()
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
                            imagePaths: FactionImages()
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
            Assert.AreEqual("alliance-image", message.DisplayImagePath);
        }

        [Test]
        public void CreateMessages_PlanetJoinedEnemyBySupport_ReportsPreviousOwner()
        {
            (GameRoot game, Faction alliance, Faction empire, _, Planet target) =
                BuildTwoFactionMessageScene();

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
                            imagePaths: FactionImages()
                        ),
                        Definition(
                            MessageResultType.PlanetJoinedEnemyBySupport,
                            MessageType.PopularSupport,
                            "{system} joins enemy",
                            "dissent:{system}:{faction}",
                            imagePaths: FactionImages()
                        ),
                    },
                    new PlanetOwnershipChangedResult
                    {
                        Planet = target,
                        PreviousOwner = empire,
                        NewOwner = alliance,
                        Reason = PlanetOwnershipChangeReason.PopularSupport,
                    }
                ),
                empire
            );

            Assert.AreEqual(MessageType.PopularSupport, message.Type);
            Assert.AreEqual("Yavin joins enemy", message.Title);
            Assert.AreEqual("dissent:Yavin:Alliance", message.Body);
            Assert.AreEqual("alliance-image", message.DisplayImagePath);
        }

        [Test]
        public void CreateMessages_PlanetJoinedEnemyBySupport_ReportsObserver()
        {
            (GameRoot game, Faction alliance, Faction empire, _, Planet target) =
                BuildTwoFactionMessageScene();

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
                            imagePaths: FactionImages()
                        ),
                        Definition(
                            MessageResultType.PlanetJoinedEnemyBySupport,
                            MessageType.PopularSupport,
                            "{system} joins enemy",
                            "dissent:{system}:{faction}",
                            imagePaths: FactionImages()
                        ),
                    },
                    new PlanetOwnershipChangedResult
                    {
                        Planet = target,
                        PreviousOwner = null,
                        NewOwner = empire,
                        Reason = PlanetOwnershipChangeReason.PopularSupport,
                        ObserverFactionInstanceIDs = new List<string>
                        {
                            alliance.InstanceID,
                            empire.InstanceID,
                        },
                    }
                ),
                alliance
            );

            Assert.AreEqual(MessageType.PopularSupport, message.Type);
            Assert.AreEqual("Yavin joins enemy", message.Title);
            Assert.AreEqual("dissent:Yavin:Empire", message.Body);
            Assert.AreEqual("empire-image", message.DisplayImagePath);
        }

        [Test]
        public void CreateMessages_PlanetDeclaredNeutralityBySupport_ReportsPreviousOwner()
        {
            (GameRoot game, _, Faction empire, _, Planet target) = BuildTwoFactionMessageScene();

            Message message = FirstMessageFor(
                CreateMessages(
                    game,
                    new[]
                    {
                        Definition(
                            MessageResultType.PlanetDeclaredNeutralityBySupport,
                            MessageType.PopularSupport,
                            "{system} neutral",
                            "neutral:{system}:{faction}",
                            imagePath: "support-image",
                            voicePath: "neutral-audio"
                        ),
                    },
                    new PlanetOwnershipChangedResult
                    {
                        Planet = target,
                        PreviousOwner = empire,
                        NewOwner = null,
                        Reason = PlanetOwnershipChangeReason.PopularSupport,
                    }
                ),
                empire
            );

            Assert.AreEqual(MessageType.PopularSupport, message.Type);
            Assert.AreEqual("Yavin neutral", message.Title);
            Assert.AreEqual("neutral:Yavin:Empire", message.Body);
            Assert.AreEqual("support-image", message.DisplayImagePath);
            Assert.AreEqual("neutral-audio", message.BackgroundAudioPath);
        }

        [Test]
        public void CreateMessages_PlanetOwnershipChangeWithoutSupportReason_DoesNotReportJoin()
        {
            (GameRoot game, Faction alliance, _, _, Planet target) = BuildTwoFactionMessageScene();
            target.OwnerInstanceID = null;

            List<MessageDeliveryRequest> deliveries = CreateMessages(
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
        public void CreateMessages_SelectedStrategicPlanetCapture_ReportsToBothFactions()
        {
            (GameRoot game, Faction alliance, Faction empire, Planet coruscant, _) =
                BuildTwoFactionMessageScene();
            MessageDefinition definition = Definition(
                MessageResultType.PlanetCaptured,
                MessageType.Mission,
                "captured:{system}",
                "{newFaction} seized {system} from {previousFaction}",
                imagePaths: FactionImages()
            );
            definition.PlanetInstanceID = coruscant.InstanceID;
            definition.PreviousOwnerInstanceID = empire.InstanceID;
            definition.NewOwnerInstanceID = alliance.InstanceID;

            List<MessageDeliveryRequest> deliveries = CreateMessages(
                game,
                new[] { definition },
                new PlanetOwnershipChangedResult
                {
                    Planet = coruscant,
                    PreviousOwner = empire,
                    NewOwner = alliance,
                }
            );

            Assert.AreEqual("captured:Coruscant", FirstMessageFor(deliveries, alliance).Title);
            Assert.AreEqual(
                "Alliance seized Coruscant from Empire",
                FirstMessageFor(deliveries, empire).Body
            );
        }

        [Test]
        public void CreateMessages_SelectedHeadquartersLoss_ReportsToBothFactions()
        {
            (GameRoot game, Faction alliance, Faction empire, _, Planet target) =
                BuildTwoFactionMessageScene();
            Building headquarters = new Building
            {
                InstanceID = "ALLIANCE_HQ",
                DisplayName = "Alliance Headquarters",
                OwnerInstanceID = alliance.InstanceID,
            };
            MessageDefinition definition = Definition(
                MessageResultType.HeadquartersDestroyed,
                MessageType.Mission,
                "headquarters destroyed",
                "{attacker} destroyed the {defender} headquarters at {system}"
            );
            definition.FactionInstanceID = alliance.InstanceID;

            List<MessageDeliveryRequest> deliveries = CreateMessages(
                game,
                new[] { definition },
                new HeadquartersDestroyedResult
                {
                    Headquarters = headquarters,
                    Planet = target,
                    Defender = alliance,
                    Attacker = empire,
                }
            );

            Assert.AreEqual(
                "Empire destroyed the Alliance headquarters at Yavin",
                FirstMessageFor(deliveries, alliance).Body
            );
            Assert.AreEqual("headquarters destroyed", FirstMessageFor(deliveries, empire).Title);
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
                            imagePaths: FactionImages()
                        ),
                        Definition(
                            MessageResultType.BlockadeDetected,
                            MessageType.Fleet,
                            "detected:{faction}:{fleet}:{system}",
                            "body:{faction}:{fleet}:{system}",
                            imagePaths: FactionImages()
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
                            imagePaths: FactionImages()
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
            Regiment regiment = new Regiment
            {
                DisplayName = "Infantry Regiment",
                OwnerInstanceID = alliance.InstanceID,
            };
            game.AttachNode(shipyard, origin);
            game.AttachNode(regiment, origin);

            List<MessageDeliveryRequest> deliveries = CreateMessages(
                game,
                new[]
                {
                    Definition(
                        MessageResultType.MaintenanceAutoscrap,
                        MessageType.Resource,
                        "maintenance:{item}:{system}",
                        "body:{items}:{system}",
                        imagePaths: FactionImages()
                    ),
                },
                new GameObjectAutoscrappedResult { DestroyedObject = shipyard, Context = origin },
                new GameObjectAutoscrappedResult { DestroyedObject = regiment, Context = origin }
            );
            Message message = FirstMessageFor(deliveries, alliance);

            Assert.AreEqual(1, deliveries.Count);
            Assert.AreEqual(MessageType.Resource, message.Type);
            Assert.AreEqual("maintenance:Shipyard:Coruscant", message.Title);
            Assert.AreEqual("body:Shipyard\nInfantry Regiment:Coruscant", message.Body);
            Assert.AreEqual("alliance-image", message.DisplayImagePath);
        }

        [Test]
        public void CreateMessages_MaintenanceAutoscrapAtDifferentSystems_UsesSeparateReports()
        {
            (GameRoot game, Faction alliance, Planet origin, _) = BuildMessageScene();
            Planet second = new Planet
            {
                InstanceID = "second",
                DisplayName = "Corellia",
                OwnerInstanceID = alliance.InstanceID,
                EnergyCapacity = 10,
            };
            game.AttachNode(second, origin.GetParent());
            Building firstShipyard = new Building
            {
                DisplayName = "First Shipyard",
                OwnerInstanceID = alliance.InstanceID,
            };
            Building secondShipyard = new Building
            {
                DisplayName = "Second Shipyard",
                OwnerInstanceID = alliance.InstanceID,
            };
            game.AttachNode(firstShipyard, origin);
            game.AttachNode(secondShipyard, second);

            List<MessageDeliveryRequest> deliveries = CreateMessages(
                game,
                new[]
                {
                    Definition(
                        MessageResultType.MaintenanceAutoscrap,
                        MessageType.Resource,
                        "maintenance:{system}",
                        "body:{items}"
                    ),
                },
                new GameObjectAutoscrappedResult
                {
                    DestroyedObject = firstShipyard,
                    Context = origin,
                },
                new GameObjectAutoscrappedResult
                {
                    DestroyedObject = secondShipyard,
                    Context = second,
                }
            );

            Assert.AreEqual(2, deliveries.Count);
            CollectionAssert.AreEquivalent(
                new[] { "maintenance:Coruscant", "maintenance:Corellia" },
                deliveries.Select(delivery => AsMessage(delivery).Title)
            );
        }

        [Test]
        public void CreateMessages_SpaceBattle_UsesWinnerPerspective()
        {
            (GameRoot game, Faction alliance, Faction empire, _, Planet target) =
                BuildTwoFactionMessageScene();

            List<MessageDeliveryRequest> deliveries = CreateMessages(
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
        public void CreateMessages_SpaceBattle_RendersRecordedFleetOutcomes()
        {
            (GameRoot game, Faction alliance, Faction empire, _, Planet target) =
                BuildTwoFactionMessageScene();

            List<MessageDeliveryRequest> deliveries = CreateMessages(
                game,
                SpaceBattleOutcomeDefinitions(),
                new SpaceCombatResult
                {
                    AttackerFleet = new Fleet { OwnerInstanceID = alliance.InstanceID },
                    DefenderFleet = new Fleet { OwnerInstanceID = empire.InstanceID },
                    AttackerOwnerInstanceID = alliance.InstanceID,
                    DefenderOwnerInstanceID = empire.InstanceID,
                    Planet = target,
                    Winner = CombatSide.Attacker,
                    AttackerOutcome = SpaceCombatSideOutcome.Active,
                    DefenderOutcome = SpaceCombatSideOutcome.Destroyed,
                }
            );

            Assert.AreEqual("Active|Destroyed||", FirstMessageFor(deliveries, alliance).Body);
            Assert.AreEqual("Destroyed|Active||", FirstMessageFor(deliveries, empire).Body);
        }

        [Test]
        public void CreateMessages_SpaceBattle_RendersRecordedRetreatDestination()
        {
            (GameRoot game, Faction alliance, Faction empire, Planet retreat, Planet target) =
                BuildTwoFactionMessageScene();
            Fleet attacker = new Fleet { OwnerInstanceID = alliance.InstanceID };
            Fleet defender = new Fleet { OwnerInstanceID = empire.InstanceID };
            game.AttachNode(attacker, retreat);
            game.AttachNode(defender, target);

            List<MessageDeliveryRequest> deliveries = CreateMessages(
                game,
                SpaceBattleOutcomeDefinitions(),
                new SpaceCombatResult
                {
                    AttackerFleet = attacker,
                    DefenderFleet = defender,
                    AttackerOwnerInstanceID = alliance.InstanceID,
                    DefenderOwnerInstanceID = empire.InstanceID,
                    Planet = target,
                    Winner = CombatSide.Defender,
                    AttackerOutcome = SpaceCombatSideOutcome.Withdrawn,
                    DefenderOutcome = SpaceCombatSideOutcome.Active,
                    AttackerRetreatPlanetInstanceID = retreat.InstanceID,
                }
            );

            Assert.AreEqual(
                "Withdrawn|Active|Coruscant|",
                FirstMessageFor(deliveries, alliance).Body
            );
            Assert.AreEqual(
                "Active|Withdrawn||Coruscant",
                FirstMessageFor(deliveries, empire).Body
            );
        }

        [Test]
        public void CreateMessages_SpaceBattleWithPlanetaryStarfighters_DeliversToDefender()
        {
            (GameRoot game, Faction alliance, Faction empire, _, Planet target) =
                BuildTwoFactionMessageScene();
            game.AttachNode(
                new Starfighter
                {
                    InstanceID = "planetary-defender",
                    OwnerInstanceID = empire.InstanceID,
                    ManufacturingStatus = ManufacturingStatus.Complete,
                    CurrentSquadronSize = 12,
                },
                target
            );

            List<MessageDeliveryRequest> deliveries = CreateMessages(
                game,
                SpaceBattleDefinitions(),
                new SpaceCombatResult
                {
                    AttackerFleet = new Fleet { OwnerInstanceID = alliance.InstanceID },
                    AttackerOwnerInstanceID = alliance.InstanceID,
                    DefenderOwnerInstanceID = empire.InstanceID,
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
        }

        [Test]
        public void CreateMessages_Bombardment_UsesOwnershipAndLossSelectors()
        {
            (GameRoot game, Faction alliance, Faction empire, _, Planet target) =
                BuildTwoFactionMessageScene();

            List<MessageDeliveryRequest> deliveries = CreateMessages(
                game,
                BombardmentDefinitions(),
                new BombardmentResult
                {
                    AttackingFaction = alliance,
                    Planet = target,
                    DestroyedBuildings = { new Building { DisplayName = "Shield Generator" } },
                }
            );
            Message message = FirstMessageFor(deliveries, alliance);
            Message defendingMessage = FirstMessageFor(deliveries, empire);

            Assert.AreEqual(MessageType.Conflict, message.Type);
            Assert.AreEqual(MessageResultType.Bombardment, message.ResultType);
            Assert.AreEqual("owned-target:Alliance:Empire:Yavin", message.Title);
            Assert.AreEqual("target-losses-image", message.DisplayImagePath);
            Assert.AreEqual(AdvisorNotificationType.None, DeliveryFor(message).NotificationType);
            Assert.AreEqual(
                AdvisorNotificationType.Bombardment,
                DeliveryFor(defendingMessage).NotificationType
            );
        }

        [Test]
        public void CreateMessages_DestroyedPlanet_UsesPlanetDestructionDefinition()
        {
            (GameRoot game, Faction alliance, Faction empire, _, Planet target) =
                BuildTwoFactionMessageScene();
            MessageDefinition generic = Definition(
                MessageResultType.Bombardment,
                MessageType.Conflict,
                "generic",
                "generic",
                outcome: MessageResultOutcome.TargetLosses,
                planetOwnership: MessagePlanetOwnership.Owned
            );
            MessageDefinition planetDestroyed = Definition(
                MessageResultType.Bombardment,
                MessageType.Conflict,
                "{system} Destroyed!",
                "The Death Star has destroyed the {target} system {system}.",
                outcome: MessageResultOutcome.TargetLosses,
                planetOwnership: MessagePlanetOwnership.Owned,
                planetDestroyed: true
            );

            Message message = FirstMessageFor(
                CreateMessages(
                    game,
                    new[] { generic, planetDestroyed },
                    new BombardmentResult
                    {
                        AttackingFaction = alliance,
                        Planet = target,
                        PlanetDestroyed = true,
                    }
                ),
                empire
            );

            Assert.AreEqual("Yavin Destroyed!", message.Title);
            Assert.AreEqual("The Death Star has destroyed the Empire system Yavin.", message.Body);
        }

        [Test]
        public void CreateMessages_PlanetaryAssault_UsesOwnershipAndOutcomeSelectors()
        {
            (GameRoot game, Faction alliance, Faction empire, _, Planet target) =
                BuildTwoFactionMessageScene();

            List<MessageDeliveryRequest> deliveries = CreateMessages(
                game,
                AssaultDefinitions(),
                new PlanetaryAssaultResult
                {
                    AttackingFaction = alliance,
                    Planet = target,
                    Success = false,
                }
            );
            Message attackerMessage = FirstMessageFor(deliveries, alliance);
            Message defenderMessage = FirstMessageFor(deliveries, empire);

            Assert.AreEqual(MessageType.Conflict, defenderMessage.Type);
            Assert.AreEqual(MessageResultType.PlanetaryAssault, defenderMessage.ResultType);
            Assert.AreEqual("owned-failed:Alliance:Empire:Yavin", defenderMessage.Title);
            Assert.AreEqual("alliance-image", defenderMessage.DisplayImagePath);
            Assert.AreEqual(
                AdvisorNotificationType.None,
                DeliveryFor(attackerMessage).NotificationType
            );
            Assert.AreEqual(
                AdvisorNotificationType.PlanetaryAssault,
                DeliveryFor(defenderMessage).NotificationType
            );
        }

        [Test]
        public void CreateMessages_InvalidPlanetaryCombatResults_DoNotCreateMessages()
        {
            (GameRoot game, _, _, _, Planet target) = BuildTwoFactionMessageScene();

            List<MessageDeliveryRequest> deliveries = CreateMessages(
                game,
                BombardmentDefinitions().Concat(AssaultDefinitions()).ToArray(),
                new BombardmentResult { Planet = target },
                new BombardmentResult(),
                new PlanetaryAssaultResult { Planet = target },
                new PlanetaryAssaultResult()
            );

            Assert.IsEmpty(deliveries);
        }

        private static List<MessageDeliveryRequest> CreateMessages(
            GameRoot game,
            MessageDefinition[] definitions,
            params GameResult[] results
        )
        {
            MessageFactory factory = new MessageFactory(definitions);
            List<MessageDeliveryRequest> deliveries = factory.CreateMessages(results, game);
            foreach (MessageDeliveryRequest delivery in deliveries)
                _deliveriesByMessage[AsMessage(delivery)] = delivery;
            return deliveries;
        }

        private static Message FirstMessageFor(
            IEnumerable<MessageDeliveryRequest> deliveries,
            Faction faction
        )
        {
            return AsMessage(deliveries.First(delivery => delivery.Recipient == faction));
        }

        private static MessageDeliveryRequest FirstDeliveryFor(
            IEnumerable<MessageDeliveryRequest> deliveries,
            Faction faction
        ) => deliveries.First(delivery => delivery.Recipient == faction);

        private static MessageDeliveryRequest DeliveryFor(Message message) =>
            _deliveriesByMessage[message];

        private static Message AsMessage(MessageDeliveryRequest request)
        {
            if (_messagesByDelivery.TryGetValue(request, out Message existing))
                return existing;

            Message message = new Message(request.MessageType, request.Subject, request.Body)
            {
                ResultType = request.ResultType,
                DisplayName = request.Subject,
                BackgroundImageKey = request.BackgroundImageKey,
                DisplayImagePath = request.BackgroundImagePath,
                OverlayImagePath = request.OverlayImagePath,
                BackgroundAudioPath = request.BackgroundAudioPath,
                OfficerVoicePath = request.OfficerVoicePath,
                EventLocationInstanceID = request.EventLocationInstanceID,
                NavigationTargetInstanceID = request.NavigationTargetInstanceID,
                NavigationSecondaryTargetInstanceID = request.NavigationSecondaryTargetInstanceID,
                MissionInstanceID = request.MissionInstanceID,
            };
            _messagesByDelivery.Add(request, message);
            return message;
        }

        private static MessageDefinition Definition(
            MessageResultType resultType,
            MessageType messageType,
            string titleTemplate,
            string bodyTemplate,
            string imagePath = null,
            MessageResultOutcome outcome = MessageResultOutcome.None,
            MessagePlanetOwnership planetOwnership = MessagePlanetOwnership.None,
            BuildingType buildingType = BuildingType.None,
            ManufacturingType manufacturingType = ManufacturingType.None,
            ResearchDiscipline? researchDiscipline = null,
            string missionTypeId = null,
            MissionCompletionReason missionCompletionReason = MissionCompletionReason.None,
            string imageKey = null,
            string voicePath = null,
            Dictionary<string, string> imagePaths = null,
            Dictionary<string, string> voicePaths = null,
            bool showOfficerOverlay = false,
            bool planetDestroyed = false
        )
        {
            MessageDefinition definition = new MessageDefinition
            {
                ResultType = resultType,
                MessageType = messageType,
                Outcome = outcome,
                PlanetOwnership = planetOwnership,
                MissionTypeID = missionTypeId,
                MissionCompletionReason = missionCompletionReason,
                BuildingType = buildingType,
                ManufacturingType = manufacturingType,
                Subject = titleTemplate,
                Body = bodyTemplate,
                ShowOfficerOverlay = showOfficerOverlay,
                BackgroundImage =
                    string.IsNullOrWhiteSpace(imageKey) && string.IsNullOrWhiteSpace(imagePath)
                        ? null
                        : new MessageBackgroundImage { Key = imageKey, Path = imagePath },
                ImagePaths = imagePaths ?? new Dictionary<string, string>(),
                BackgroundAudioPath = voicePath,
                BackgroundAudioPaths = voicePaths ?? new Dictionary<string, string>(),
                PlanetDestroyed = planetDestroyed,
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
                    imagePaths: new Dictionary<string, string>
                    {
                        { "FNALL1", "alliance-victory-image" },
                        { "FNEMP1", "empire-victory-image" },
                    },
                    outcome: MessageResultOutcome.Victory
                ),
                Definition(
                    MessageResultType.SpaceBattle,
                    MessageType.Conflict,
                    "defeat:{faction}:{opponent}:{system}",
                    "body:{faction}:{opponent}:{system}",
                    imagePaths: new Dictionary<string, string>
                    {
                        { "FNALL1", "alliance-defeat-image" },
                        { "FNEMP1", "empire-defeat-image" },
                    },
                    outcome: MessageResultOutcome.Defeat
                ),
                Definition(
                    MessageResultType.SpaceBattle,
                    MessageType.Conflict,
                    "draw:{faction}:{opponent}:{system}",
                    "body:{faction}:{opponent}:{system}",
                    imagePaths: FactionImages(),
                    outcome: MessageResultOutcome.Stalemate
                ),
            };
        }

        private static MessageDefinition[] SpaceBattleOutcomeDefinitions()
        {
            MessageDefinition[] definitions = SpaceBattleDefinitions();
            foreach (MessageDefinition definition in definitions)
                definition.Body =
                    "{factionOutcome}|{opponentOutcome}|{retreatSystem}|{opponentRetreatSystem}";
            return definitions;
        }

        private static MessageDefinition[] SmugglingDefinitions()
        {
            Dictionary<string, string> voices = new Dictionary<string, string>
            {
                { "FNALL1", "alliance-smuggling-voice" },
                { "FNEMP1", "empire-smuggling-voice" },
            };
            return new[]
            {
                Definition(
                    MessageResultType.SmugglingLosses,
                    MessageType.Resource,
                    "Smuggling Losses",
                    "Dissention among the population has allowed smugglers to begin operations on {system}.  As a result, valuable resources are being lost.",
                    voicePaths: voices
                ),
                Definition(
                    MessageResultType.SmugglingLossesEnded,
                    MessageType.Resource,
                    "Smuggling Losses End",
                    "Increasing support on {system} has put an end to the smuggling losses there."
                ),
                Definition(
                    MessageResultType.SmugglingBenefits,
                    MessageType.Resource,
                    "Smuggling Benefits",
                    "Smugglers from {system} are providing us with additional resources.",
                    voicePaths: voices
                ),
                Definition(
                    MessageResultType.SmugglingBenefitsEnded,
                    MessageType.Resource,
                    "Smuggling Benefits End",
                    "Popular opinion on {system} has caused smugglers from that system to withdraw their support."
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
                    imagePaths: FactionImages(),
                    outcome: MessageResultOutcome.Success,
                    planetOwnership: MessagePlanetOwnership.Owned
                ),
                Definition(
                    MessageResultType.PlanetaryAssault,
                    MessageType.Conflict,
                    "owned-failed:{faction}:{target}:{system}",
                    "body:{faction}:{target}:{system}",
                    imagePaths: FactionImages(),
                    outcome: MessageResultOutcome.Failed,
                    planetOwnership: MessagePlanetOwnership.Owned
                ),
                Definition(
                    MessageResultType.PlanetaryAssault,
                    MessageType.Conflict,
                    "neutral-success:{faction}:{target}:{system}",
                    "body:{faction}:{target}:{system}",
                    imagePaths: FactionImages(),
                    outcome: MessageResultOutcome.Success,
                    planetOwnership: MessagePlanetOwnership.Neutral
                ),
                Definition(
                    MessageResultType.PlanetaryAssault,
                    MessageType.Conflict,
                    "neutral-failed:{faction}:{target}:{system}",
                    "body:{faction}:{target}:{system}",
                    imagePaths: FactionImages(),
                    outcome: MessageResultOutcome.Failed,
                    planetOwnership: MessagePlanetOwnership.Neutral
                ),
            };
        }

        private static Dictionary<string, string> FactionImages()
        {
            return new Dictionary<string, string>
            {
                { "FNALL1", "alliance-image" },
                { "FNEMP1", "empire-image" },
            };
        }

        private static string DefaultImage(string path)
        {
            return path;
        }

        private static (
            GameRoot game,
            Faction alliance,
            Planet origin,
            Planet destination
        ) BuildMessageScene()
        {
            GameRoot game = new GameRoot(TestContent.Data.GameConfig);
            Faction alliance = new Faction { InstanceID = "FNALL1", DisplayName = "Alliance" };
            game.GetFactions().Add(alliance);
            PlanetSector sector = new PlanetSector { InstanceID = "CORE", DisplayName = "Core" };
            game.AttachNode(sector, game.Galaxy);
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
            game.AttachNode(origin, sector);
            game.AttachNode(destination, sector);

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
            GameRoot game = new GameRoot(TestContent.Data.GameConfig);
            Faction alliance = new Faction { InstanceID = "FNALL1", DisplayName = "Alliance" };
            Faction empire = new Faction { InstanceID = "FNEMP1", DisplayName = "Empire" };
            game.GetFactions().Add(alliance);
            game.GetFactions().Add(empire);
            PlanetSector sector = new PlanetSector { InstanceID = "CORE", DisplayName = "Core" };
            game.AttachNode(sector, game.Galaxy);
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
            game.AttachNode(origin, sector);
            game.AttachNode(target, sector);

            return (game, alliance, empire, origin, target);
        }
    }
}
