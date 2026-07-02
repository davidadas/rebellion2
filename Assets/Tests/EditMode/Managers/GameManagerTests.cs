using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Rebellion.Game;
using Rebellion.Game.Events;
using Rebellion.Game.Factions;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Messages;
using Rebellion.Game.Research;
using Rebellion.Game.Results;
using Rebellion.Game.Units;
using Rebellion.Systems;

namespace Rebellion.Tests.Managers
{
    [TestFixture]
    public class GameManagerTests
    {
        [Test]
        public void Constructor_WithFactions_RebuildsResearchCatalogs()
        {
            GameRoot game = new GameRoot();
            Faction alliance = new Faction { InstanceID = "FNALL1", DisplayName = "Alliance" };
            Faction empire = new Faction { InstanceID = "FNEMP1", DisplayName = "Empire" };
            game.Factions.Add(alliance);
            game.Factions.Add(empire);

            Assume.That(
                alliance.ResearchCatalog,
                Is.Empty,
                "Catalog must start empty to prove the rebuild populates it"
            );
            Assume.That(empire.ResearchCatalog, Is.Empty);

            _ = new GameManager(game);

            Assert.IsNotEmpty(
                alliance.ResearchCatalog,
                "Alliance research catalog should be rebuilt after GameManager construction"
            );
            Assert.IsNotEmpty(
                empire.ResearchCatalog,
                "Empire research catalog should be rebuilt after GameManager construction"
            );
        }

        [Test]
        public void ProcessTick_EventResults_AddsMessages()
        {
            GameRoot game = new GameRoot();
            Faction faction = new Faction { InstanceID = "FNALL1", DisplayName = "Alliance" };
            game.Factions.Add(faction);
            game.EventPool.Add(
                new GameEvent
                {
                    InstanceID = "EVENT_RESEARCH_EXHAUSTED",
                    Actions = new List<GameAction>
                    {
                        new EmitResultAction(
                            new ResearchExhaustedResult
                            {
                                Faction = faction,
                                Discipline = ResearchDiscipline.ShipDesign,
                            }
                        ),
                    },
                }
            );

            GameManager manager = new GameManager(game);

            manager.ProcessTick();

            Assert.AreEqual(1, faction.Messages[MessageType.Manufacturing].Count);
        }

        [Test]
        public void ProcessTick_SabotageResult_RemovesDestroyedObjectFromActorSnapshot()
        {
            GameRoot game = new GameRoot(ResourceManager.GetConfig<GameConfig>());
            Faction alliance = new Faction
            {
                InstanceID = "FNALL1",
                DisplayName = "Alliance",
                PlayerID = "alliance_player",
            };
            Faction empire = new Faction
            {
                InstanceID = "FNEMP1",
                DisplayName = "Empire",
                PlayerID = "empire_player",
            };
            game.Factions.Add(alliance);
            game.Factions.Add(empire);

            PlanetSystem system = new PlanetSystem { InstanceID = "SYS1", DisplayName = "System" };
            game.AttachNode(system, game.GetGalaxyMap());

            Planet planet = new Planet
            {
                InstanceID = "PLANET1",
                DisplayName = "Coruscant",
                OwnerInstanceID = empire.InstanceID,
                IsColonized = true,
                EnergyCapacity = 10,
            };
            game.AttachNode(planet, system);

            Building mine = new Building
            {
                InstanceID = "MINE1",
                DisplayName = "Mine",
                OwnerInstanceID = empire.InstanceID,
                BuildingType = BuildingType.Mine,
                ManufacturingStatus = ManufacturingStatus.Complete,
            };
            game.AttachNode(mine, planet);

            Officer han = EntityFactory.CreateOfficer("HAN", alliance.InstanceID);
            FogOfWarSystem fog = new FogOfWarSystem(game);
            fog.CaptureSnapshot(alliance, planet, system, 0);
            Assert.IsTrue(
                alliance
                    .Fog.Snapshots["SYS1"]
                    .Planets["PLANET1"]
                    .Buildings.Any(b => b.InstanceID == "MINE1")
            );

            game.DetachNode(mine);

            game.EventPool.Add(
                new GameEvent
                {
                    InstanceID = "EVENT_SABOTAGE",
                    Actions = new List<GameAction>
                    {
                        new EmitResultAction(
                            new GameObjectSabotagedResult
                            {
                                SabotagedObject = mine,
                                Saboteur = han,
                                Context = planet,
                            }
                        ),
                    },
                }
            );

            GameManager manager = new GameManager(game);

            manager.ProcessTick();

            GalaxyMap view = manager.GetFogOfWarSystem().BuildFactionView(alliance);
            Planet viewedPlanet = view
                .PlanetSystems.Single(s => s.InstanceID == "SYS1")
                .Planets.Single(p => p.InstanceID == "PLANET1");
            Assert.IsFalse(viewedPlanet.Buildings.Any(b => b.InstanceID == "MINE1"));
        }

        private sealed class EmitResultAction : GameAction
        {
            private readonly GameResult _result;

            internal EmitResultAction(GameResult result)
            {
                _result = result;
            }

            public override List<GameResult> Execute(GameRoot game)
            {
                return new List<GameResult> { _result };
            }
        }
    }
}
