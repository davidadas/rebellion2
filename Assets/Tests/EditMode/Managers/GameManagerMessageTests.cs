using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using Rebellion.Game;
using Rebellion.Game.Factions;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Missions;
using Rebellion.Game.Research;
using Rebellion.Game.Results;
using Rebellion.Game.Units;

namespace Rebellion.Tests.Managers
{
    [TestFixture]
    public class GameManagerMessageTests
    {
        [Test]
        public void ProcessResults_FleetArrived_AddsFleetMessageToOwner()
        {
            (GameRoot game, Faction alliance, _, Planet destination, GameManager manager) =
                BuildMessageScene();
            Fleet fleet = new Fleet
            {
                InstanceID = "FLEET1",
                DisplayName = "Fleet 1",
                OwnerInstanceID = alliance.InstanceID,
            };
            game.AttachNode(fleet, destination);

            ProcessResults(
                manager,
                new UnitArrivedResult { Unit = fleet, Destination = destination }
            );

            AssertOnlyMessages(alliance, MessageType.Fleet, 1);
        }

        [Test]
        public void ProcessResults_BuildingDeployed_AddsResourceMessageToOwner()
        {
            (GameRoot game, Faction alliance, Planet origin, _, GameManager manager) =
                BuildMessageScene();
            Building mine = new Building
            {
                InstanceID = "MINE1",
                DisplayName = "Mine",
                OwnerInstanceID = alliance.InstanceID,
                BuildingType = BuildingType.Mine,
            };
            game.AttachNode(mine, origin);

            ProcessResults(manager, new GameObjectDeployedResult { GameObject = mine });

            AssertOnlyMessages(alliance, MessageType.Resource, 1);
        }

        [Test]
        public void ProcessResults_ManufacturingCompleted_AddsManufacturingMessageToFaction()
        {
            (_, Faction alliance, Planet origin, _, GameManager manager) = BuildMessageScene();

            ProcessResults(
                manager,
                new ManufacturingCompletedResult
                {
                    Faction = alliance,
                    ProductionPlanet = origin,
                    ProductType = ManufacturingType.Building,
                }
            );

            AssertOnlyMessages(alliance, MessageType.Manufacturing, 1);
        }

        [Test]
        public void ProcessResults_SabotageSucceeded_AddsMissionMessagesToActorAndTarget()
        {
            (
                GameRoot game,
                Faction alliance,
                Faction empire,
                _,
                Planet target,
                GameManager manager
            ) = BuildTwoFactionMessageScene();
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

            ProcessResults(
                manager,
                new GameObjectSabotagedResult { SabotagedObject = building, Context = target },
                new MissionCompletedResult
                {
                    Mission = mission,
                    MissionName = "Sabotage",
                    TargetName = target.DisplayName,
                    Outcome = MissionOutcome.Success,
                }
            );

            AssertOnlyMessages(alliance, MessageType.Mission, 1);
            AssertOnlyMessages(empire, MessageType.Mission, 1);
        }

        [Test]
        public void ProcessResults_MissionFoiled_AddsMissionMessagesToActorAndTarget()
        {
            (
                GameRoot game,
                Faction alliance,
                Faction empire,
                _,
                Planet target,
                GameManager manager
            ) = BuildTwoFactionMessageScene();
            SabotageMission mission = new SabotageMission
            {
                OwnerInstanceID = alliance.InstanceID,
                DisplayName = "Sabotage",
            };
            game.AttachNode(mission, target);

            ProcessResults(
                manager,
                new MissionCompletedResult
                {
                    Mission = mission,
                    MissionName = "Sabotage",
                    TargetName = target.DisplayName,
                    Outcome = MissionOutcome.Foiled,
                }
            );

            AssertOnlyMessages(alliance, MessageType.Mission, 1);
            AssertOnlyMessages(empire, MessageType.Mission, 1);
        }

        [Test]
        public void ProcessResults_ResearchCompletedAndExhausted_AddsManufacturingMessagesToFaction()
        {
            (_, Faction alliance, _, _, GameManager manager) = BuildMessageScene();

            ProcessResults(
                manager,
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

            AssertOnlyMessages(alliance, MessageType.Manufacturing, 2);
        }

        [Test]
        public void ProcessResults_UprisingStarted_AddsPopularSupportMessagesToOwnerAndInstigator()
        {
            (_, Faction alliance, Faction empire, _, Planet target, GameManager manager) =
                BuildTwoFactionMessageScene();

            ProcessResults(
                manager,
                new PlanetUprisingStartedResult { Planet = target, InstigatorFaction = alliance }
            );

            AssertOnlyMessages(empire, MessageType.PopularSupport, 1);
            AssertOnlyMessages(alliance, MessageType.PopularSupport, 1);
        }

        [Test]
        public void ProcessResults_BlockadeStarted_AddsFleetMessagesToBlockaderAndOwner()
        {
            (_, Faction alliance, Faction empire, _, Planet target, GameManager manager) =
                BuildTwoFactionMessageScene();
            Fleet fleet = new Fleet
            {
                DisplayName = "Fleet 1",
                OwnerInstanceID = alliance.InstanceID,
            };

            ProcessResults(
                manager,
                new BlockadeChangedResult
                {
                    Planet = target,
                    BlockadingFleet = fleet,
                    Blockaded = true,
                }
            );

            AssertOnlyMessages(alliance, MessageType.Fleet, 1);
            AssertOnlyMessages(empire, MessageType.Fleet, 1);
        }

        [Test]
        public void ProcessResults_EvacuationLosses_AddsFleetMessageToFaction()
        {
            (_, Faction alliance, Planet origin, _, GameManager manager) = BuildMessageScene();

            ProcessResults(
                manager,
                new EvacuationLossesResult
                {
                    Faction = alliance,
                    Location = origin,
                    LostRegiments = { new Regiment { DisplayName = "Infantry Regiment" } },
                }
            );

            AssertOnlyMessages(alliance, MessageType.Fleet, 1);
        }

        [Test]
        public void ProcessResults_MaintenanceAutoscrap_AddsResourceMessageToFaction()
        {
            (GameRoot game, Faction alliance, Planet origin, _, GameManager manager) =
                BuildMessageScene();
            Building shipyard = new Building
            {
                InstanceID = "SHIPYARD1",
                DisplayName = "Shipyard",
                OwnerInstanceID = alliance.InstanceID,
            };
            game.AttachNode(shipyard, origin);

            ProcessResults(
                manager,
                new MaintenanceRequiredResult { Faction = alliance, Amount = 12 },
                new GameObjectAutoscrappedResult { DestroyedObject = shipyard, Context = origin }
            );

            AssertOnlyMessages(alliance, MessageType.Resource, 1);
        }

        [Test]
        public void ProcessResults_SpaceCombat_AddsConflictMessagesToBothFactions()
        {
            (_, Faction alliance, Faction empire, _, Planet target, GameManager manager) =
                BuildTwoFactionMessageScene();

            ProcessResults(
                manager,
                new SpaceCombatResult
                {
                    AttackerFleet = new Fleet { OwnerInstanceID = alliance.InstanceID },
                    DefenderFleet = new Fleet { OwnerInstanceID = empire.InstanceID },
                    Planet = target,
                    Winner = CombatSide.Attacker,
                }
            );

            AssertOnlyMessages(alliance, MessageType.Conflict, 1);
            AssertOnlyMessages(empire, MessageType.Conflict, 1);
        }

        [Test]
        public void ProcessResults_Bombardment_AddsConflictMessagesToAttackerAndDefender()
        {
            (_, Faction alliance, Faction empire, _, Planet target, GameManager manager) =
                BuildTwoFactionMessageScene();

            ProcessResults(
                manager,
                new BombardmentResult { AttackingFaction = alliance, Planet = target }
            );

            AssertOnlyMessages(alliance, MessageType.Conflict, 1);
            AssertOnlyMessages(empire, MessageType.Conflict, 1);
        }

        [Test]
        public void ProcessResults_PlanetaryAssault_AddsConflictMessagesToAttackerAndDefender()
        {
            (_, Faction alliance, Faction empire, _, Planet target, GameManager manager) =
                BuildTwoFactionMessageScene();

            ProcessResults(
                manager,
                new PlanetaryAssaultResult
                {
                    AttackingFaction = alliance,
                    Planet = target,
                    Success = false,
                }
            );

            AssertOnlyMessages(alliance, MessageType.Conflict, 1);
            AssertOnlyMessages(empire, MessageType.Conflict, 1);
        }

        private static (
            GameRoot game,
            Faction alliance,
            Planet origin,
            Planet destination,
            GameManager manager
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

            return (game, alliance, origin, destination, new GameManager(game));
        }

        private static (
            GameRoot game,
            Faction alliance,
            Faction empire,
            Planet origin,
            Planet target,
            GameManager manager
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

            return (game, alliance, empire, origin, target, new GameManager(game));
        }

        private static void AssertOnlyMessages(
            Faction faction,
            MessageType messageType,
            int expectedCount
        )
        {
            foreach (KeyValuePair<MessageType, List<Message>> entry in faction.Messages)
            {
                int expected = entry.Key == messageType ? expectedCount : 0;
                Assert.AreEqual(expected, entry.Value.Count, entry.Key.ToString());
            }

            foreach (Message message in faction.Messages[messageType])
                Assert.AreEqual(messageType, message.Type);
        }

        private static void ProcessResults(GameManager manager, params GameResult[] results)
        {
            typeof(GameManager)
                .GetMethod("ProcessResults", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(manager, new object[] { results.ToList() });
        }
    }
}
