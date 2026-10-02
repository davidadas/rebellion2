using System.Collections.Generic;
using NUnit.Framework;
using Rebellion.Game;
using Rebellion.Game.Factions;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Results;
using Rebellion.Game.Units;
using Rebellion.Simulation;

namespace Rebellion.Tests.Simulation
{
    [TestFixture]
    public class OfficerLoyaltyObserverTests
    {
        [Test]
        public void HandleResults_SpaceCombatWithExclusiveController_UsesDestroyedEligibleUnitValues()
        {
            BattleScene scene = CreateScene();
            SpaceCombatResult result = new SpaceCombatResult
            {
                AttackerOwnerInstanceID = scene.Alliance.InstanceID,
                DefenderOwnerInstanceID = scene.Empire.InstanceID,
                AttackerOutcome = SpaceCombatSideOutcome.Active,
                DefenderOutcome = SpaceCombatSideOutcome.Destroyed,
                AttackingUnits = new List<CombatUnitSnapshot>
                {
                    DestroyedSnapshot(
                        new CapitalShip
                        {
                            InstanceID = "alliance-ship",
                            OwnerInstanceID = scene.Alliance.InstanceID,
                            UprisingDefense = 40,
                        }
                    ),
                    SurvivingSnapshot(
                        new Starfighter
                        {
                            InstanceID = "partial-fighter",
                            OwnerInstanceID = scene.Alliance.InstanceID,
                            UprisingDefense = 999,
                        }
                    ),
                    DestroyedSnapshot(
                        new SpecialForces
                        {
                            InstanceID = "excluded-special-forces",
                            OwnerInstanceID = scene.Alliance.InstanceID,
                        }
                    ),
                },
                DefendingUnits = new List<CombatUnitSnapshot>
                {
                    DestroyedSnapshot(
                        new CapitalShip
                        {
                            InstanceID = "empire-ship",
                            OwnerInstanceID = scene.Empire.InstanceID,
                            UprisingDefense = 75,
                        }
                    ),
                    DestroyedSnapshot(
                        new Starfighter
                        {
                            InstanceID = "empire-fighter",
                            OwnerInstanceID = scene.Empire.InstanceID,
                            UprisingDefense = 5,
                        }
                    ),
                },
            };

            scene.Observer.HandleResults(new[] { result });

            // The two divisions happen independently: (80 - 40) / 80 + 80 / 80 = 1.
            Assert.AreEqual(51, scene.AllianceOfficer.Loyalty);
            Assert.AreEqual(49, scene.EmpireOfficer.Loyalty);
        }

        [Test]
        public void HandleResults_SpaceCombatWithBothSidesActive_DoesNotChangeLoyalty()
        {
            BattleScene scene = CreateScene();
            SpaceCombatResult result = new SpaceCombatResult
            {
                AttackerOwnerInstanceID = scene.Alliance.InstanceID,
                DefenderOwnerInstanceID = scene.Empire.InstanceID,
                AttackerOutcome = SpaceCombatSideOutcome.Active,
                DefenderOutcome = SpaceCombatSideOutcome.Active,
                AttackingUnits = new List<CombatUnitSnapshot>
                {
                    DestroyedSnapshot(new CapitalShip { InstanceID = "a", UprisingDefense = 1000 }),
                },
                DefendingUnits = new List<CombatUnitSnapshot>
                {
                    DestroyedSnapshot(new CapitalShip { InstanceID = "d", UprisingDefense = 1000 }),
                },
            };

            scene.Observer.HandleResults(new[] { result });

            Assert.AreEqual(50, scene.AllianceOfficer.Loyalty);
            Assert.AreEqual(50, scene.EmpireOfficer.Loyalty);
        }

        [Test]
        public void HandleResults_PlanetaryAssaultWithFinalController_UsesDestroyedRegimentValues()
        {
            BattleScene scene = CreateScene();
            scene.Planet.OwnerInstanceID = scene.Alliance.InstanceID;
            PlanetaryAssaultResult result = new PlanetaryAssaultResult
            {
                Planet = scene.Planet,
                AttackerOwnerInstanceID = scene.Alliance.InstanceID,
                DefenderOwnerInstanceID = scene.Empire.InstanceID,
                Success = true,
                DestroyedAttackerRegiments = new List<Regiment>
                {
                    new Regiment { UprisingDefense = 40 },
                },
                DestroyedDefenderRegiments = new List<Regiment>
                {
                    new Regiment { UprisingDefense = 80 },
                },
                CollateralDestroyedBuildings = new List<Building>
                {
                    new Building { ConstructionCost = 10000 },
                },
            };

            scene.Observer.HandleResults(new[] { result });

            Assert.AreEqual(51, scene.AllianceOfficer.Loyalty);
            Assert.AreEqual(49, scene.EmpireOfficer.Loyalty);
        }

        [Test]
        public void HandleResults_BombardmentWithDestroyedFacilities_ExcludesFacilities()
        {
            BattleScene scene = CreateScene();
            BombardmentResult result = new BombardmentResult
            {
                Planet = scene.Planet,
                AttackerOwnerInstanceID = scene.Alliance.InstanceID,
                DefenderOwnerInstanceID = scene.Empire.InstanceID,
                DestroyedCapitalShips = new List<CapitalShip>
                {
                    new CapitalShip { UprisingDefense = 80 },
                },
                DestroyedRegiments = new List<Regiment> { new Regiment { UprisingDefense = 40 } },
                DestroyedBuildings = new List<Building>
                {
                    new Building { ConstructionCost = 10000 },
                },
            };

            scene.Observer.HandleResults(new[] { result });

            Assert.AreEqual(49, scene.AllianceOfficer.Loyalty);
            Assert.AreEqual(51, scene.EmpireOfficer.Loyalty);
        }

        [Test]
        public void HandleResults_BombardmentWithDestroyedPlanet_FavorsAttackingFaction()
        {
            BattleScene scene = CreateScene();
            BombardmentResult result = new BombardmentResult
            {
                Planet = scene.Planet,
                PlanetDestroyed = true,
                AttackerOwnerInstanceID = scene.Alliance.InstanceID,
                DefenderOwnerInstanceID = scene.Empire.InstanceID,
                DestroyedRegiments = new List<Regiment> { new Regiment { UprisingDefense = 80 } },
            };

            scene.Observer.HandleResults(new[] { result });

            Assert.AreEqual(52, scene.AllianceOfficer.Loyalty);
            Assert.AreEqual(48, scene.EmpireOfficer.Loyalty);
        }

        [Test]
        public void HandleResults_FavoredFactionDivisor_ControlsShift()
        {
            BattleScene scene = CreateScene();
            scene.Alliance.Settings.BattleLossLoyaltyDivisor = 40;
            SpaceCombatResult result = new SpaceCombatResult
            {
                AttackerOwnerInstanceID = scene.Alliance.InstanceID,
                DefenderOwnerInstanceID = scene.Empire.InstanceID,
                AttackerOutcome = SpaceCombatSideOutcome.Active,
                DefenderOutcome = SpaceCombatSideOutcome.Destroyed,
                AttackingUnits = new List<CombatUnitSnapshot>
                {
                    DestroyedSnapshot(new CapitalShip { UprisingDefense = 40 }),
                },
                DefendingUnits = new List<CombatUnitSnapshot>
                {
                    DestroyedSnapshot(new CapitalShip { UprisingDefense = 80 }),
                },
            };

            scene.Observer.HandleResults(new[] { result });

            Assert.AreEqual(53, scene.AllianceOfficer.Loyalty);
            Assert.AreEqual(47, scene.EmpireOfficer.Loyalty);
        }

        [Test]
        public void HandleResults_NonPositiveDivisorWithEligibleLoss_RejectsConfiguration()
        {
            BattleScene scene = CreateScene();
            scene.Empire.Settings.BattleLossLoyaltyDivisor = 0;
            PlanetaryAssaultResult result = new PlanetaryAssaultResult
            {
                Planet = scene.Planet,
                AttackerOwnerInstanceID = scene.Alliance.InstanceID,
                DefenderOwnerInstanceID = scene.Empire.InstanceID,
                DestroyedAttackerRegiments = new List<Regiment>
                {
                    new Regiment { UprisingDefense = 1 },
                },
            };

            Assert.Throws<System.InvalidOperationException>(() =>
                scene.Observer.HandleResults(new[] { result })
            );
        }

        [Test]
        public void HandleResults_NonPositiveDivisorWithoutEligibleLoss_DoesNotReject()
        {
            BattleScene scene = CreateScene();
            scene.Empire.Settings.BattleLossLoyaltyDivisor = 0;
            PlanetaryAssaultResult result = new PlanetaryAssaultResult
            {
                Planet = scene.Planet,
                AttackerOwnerInstanceID = scene.Alliance.InstanceID,
                DefenderOwnerInstanceID = scene.Empire.InstanceID,
                DestroyedAttackerRegiments = new List<Regiment> { new Regiment() },
            };

            Assert.DoesNotThrow(() => scene.Observer.HandleResults(new[] { result }));
            Assert.AreEqual(50, scene.AllianceOfficer.Loyalty);
            Assert.AreEqual(50, scene.EmpireOfficer.Loyalty);
        }

        /// <summary>
        /// Creates a destroyed combat-unit snapshot.
        /// </summary>
        /// <param name="unit">The unit represented by the snapshot.</param>
        /// <returns>The destroyed snapshot.</returns>
        private static CombatUnitSnapshot DestroyedSnapshot(Rebellion.SceneGraph.ISceneNode unit)
        {
            return new CombatUnitSnapshot(unit) { Destroyed = true };
        }

        /// <summary>
        /// Creates a surviving combat-unit snapshot.
        /// </summary>
        /// <param name="unit">The unit represented by the snapshot.</param>
        /// <returns>The surviving snapshot.</returns>
        private static CombatUnitSnapshot SurvivingSnapshot(Rebellion.SceneGraph.ISceneNode unit)
        {
            return new CombatUnitSnapshot(unit) { Destroyed = false };
        }

        /// <summary>
        /// Creates a two-faction battle scene with one mutable officer per faction.
        /// </summary>
        /// <returns>The prepared battle scene.</returns>
        private static BattleScene CreateScene()
        {
            GameConfig config = new GameConfig();
            GameRoot game = TestGame.Create(config);
            Faction alliance = new Faction
            {
                InstanceID = "alliance",
                Settings = new FactionSettings { BattleLossLoyaltyDivisor = 80 },
            };
            Faction empire = new Faction
            {
                InstanceID = "empire",
                Settings = new FactionSettings { BattleLossLoyaltyDivisor = 80 },
            };
            game.GetFactions().Add(alliance);
            game.GetFactions().Add(empire);

            PlanetSector sector = new PlanetSector { InstanceID = "sector" };
            game.AttachNode(sector, game.Galaxy);
            Planet planet = new Planet
            {
                InstanceID = "battle-planet",
                OwnerInstanceID = empire.InstanceID,
                IsColonized = true,
            };
            game.AttachNode(planet, sector);
            Planet alliancePlanet = new Planet
            {
                InstanceID = "alliance-planet",
                OwnerInstanceID = alliance.InstanceID,
                IsColonized = true,
            };
            game.AttachNode(alliancePlanet, sector);

            Officer allianceOfficer = EntityFactory.CreateOfficer(
                "alliance-officer",
                alliance.InstanceID,
                canBetray: true,
                loyalty: 50
            );
            Officer empireOfficer = EntityFactory.CreateOfficer(
                "empire-officer",
                empire.InstanceID,
                canBetray: true,
                loyalty: 50
            );
            game.AttachNode(allianceOfficer, alliancePlanet);
            game.AttachNode(empireOfficer, planet);

            OfficerLoyaltyCommands commands = new OfficerLoyaltyCommands(game);
            return new BattleScene
            {
                Game = game,
                Alliance = alliance,
                Empire = empire,
                Planet = planet,
                AllianceOfficer = allianceOfficer,
                EmpireOfficer = empireOfficer,
                Observer = new OfficerLoyaltyObserver(game, commands),
            };
        }

        private sealed class BattleScene
        {
            public GameRoot Game { get; set; }
            public Faction Alliance { get; set; }
            public Faction Empire { get; set; }
            public Planet Planet { get; set; }
            public Officer AllianceOfficer { get; set; }
            public Officer EmpireOfficer { get; set; }
            public OfficerLoyaltyObserver Observer { get; set; }
        }
    }
}
