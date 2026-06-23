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
            Assert.AreEqual("mine-image", message.DisplayImagePath);
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
        public void CreateMessages_FoiledMission_ReturnsFailedActorReportAndFoiledTargetReport()
        {
            (GameRoot game, Faction alliance, Faction empire, _, Planet target) =
                BuildTwoFactionMessageScene();
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
                }
            );

            Assert.AreEqual("failed:Sabotage:Yavin", FirstMessageFor(deliveries, alliance).Title);
            Assert.AreEqual("foiled:Sabotage:Yavin", FirstMessageFor(deliveries, empire).Title);
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
            Assert.IsNull(message.DisplayImagePath);
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
