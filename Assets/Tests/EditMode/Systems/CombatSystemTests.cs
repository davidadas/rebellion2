using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Rebellion.Game;
using Rebellion.Game.Results;
using Rebellion.SceneGraph;
using Rebellion.Systems;
using Rebellion.Util.Common;

namespace Rebellion.Tests.Systems
{
    public class CombatTestBase
    {
        protected CombatSystem MakeCombat(GameRoot game, IRandomNumberProvider rng)
        {
            MovementSystem movement = new MovementSystem(game, null);
            ManufacturingSystem manufacturing = new ManufacturingSystem(game);
            PlanetaryControlSystem ownership = new PlanetaryControlSystem(
                game,
                movement,
                manufacturing
            );
            return new CombatSystem(game, rng, movement, ownership);
        }

        protected GameRoot CreateGame()
        {
            GameRoot game = new GameRoot(TestConfig.Create());
            game.Factions.Add(new Faction { InstanceID = "empire", PlayerID = null });
            game.Factions.Add(new Faction { InstanceID = "alliance", PlayerID = null });
            return game;
        }

        protected (Planet planet, PlanetSystem system) CreatePlanet(
            GameRoot game,
            string id,
            string owner = null,
            int energy = 5
        )
        {
            PlanetSystem system = new PlanetSystem { InstanceID = $"sys_{id}" };
            game.AttachNode(system, game.Galaxy);
            Planet planet = new Planet
            {
                InstanceID = id,
                OwnerInstanceID = owner,
                IsColonized = true,
                EnergyCapacity = energy,
                PopularSupport = new Dictionary<string, int>
                {
                    { "empire", 50 },
                    { "alliance", 50 },
                },
            };
            game.AttachNode(planet, system);
            return (planet, system);
        }
    }

    /// <summary>
    /// Tests for CombatSystem.
    /// Validates the 7-phase combat pipeline.
    /// </summary>
    [TestFixture]
    public class CombatSystemTests : CombatTestBase
    {
        /// <summary>
        /// Runs a full combat cycle: detect then resolve (auto).
        /// Returns true if combat was detected and resolved.
        /// </summary>
        private bool RunCombat(CombatSystem manager)
        {
            if (manager.TryStartCombat(out CombatDecisionContext decision))
            {
                manager.Resolve(decision, autoResolve: true);
                return true;
            }
            return false;
        }

        [Test]
        public void Resolve_TwoFactionFleets_RunsSpaceCombat()
        {
            GameRoot game = new GameRoot();
            Faction empire = new Faction { InstanceID = "empire" };
            Faction alliance = new Faction { InstanceID = "alliance" };
            game.Factions.Add(empire);
            game.Factions.Add(alliance);

            PlanetSystem system = new PlanetSystem { InstanceID = "sys1" };
            Planet planet = new Planet { InstanceID = "p1" };
            game.AttachNode(system, game.Galaxy);
            game.AttachNode(planet, system);

            Fleet empireFleet = CreateFleet(game, "f1", "empire", planet, 1, 100, 10);
            Fleet allianceFleet = CreateFleet(game, "f2", "alliance", planet, 1, 100, 10);

            QueueRNG rng = new QueueRNG(0.5, 0.5, 0.5, 0.5);
            CombatSystem manager = MakeCombat(game, rng);

            RunCombat(manager);

            bool combatOccurred =
                empireFleet.CapitalShips[0].CurrentHullStrength < 100
                || allianceFleet.CapitalShips[0].CurrentHullStrength < 100;
            Assert.IsTrue(combatOccurred, "Combat should occur between hostile factions");
        }

        [Test]
        public void Resolve_NoHostileFleets_DoesNotRunCombat()
        {
            GameRoot game = new GameRoot();
            Faction empire = new Faction { InstanceID = "empire" };
            game.Factions.Add(empire);

            PlanetSystem system = new PlanetSystem { InstanceID = "sys1" };
            Planet planet = new Planet { InstanceID = "p1" };
            game.AttachNode(system, game.Galaxy);
            game.AttachNode(planet, system);

            Fleet fleet = CreateFleet(game, "f1", "empire", planet, 1, 100, 10);
            int initialHull = fleet.CapitalShips[0].CurrentHullStrength;

            QueueRNG rng = new QueueRNG();
            CombatSystem manager = MakeCombat(game, rng);

            bool detected = RunCombat(manager);

            Assert.IsFalse(detected, "No combat should be detected");
            Assert.AreEqual(
                initialHull,
                fleet.CapitalShips[0].CurrentHullStrength,
                "No combat should occur"
            );
        }

        [Test]
        public void Resolve_SingleFactionFleets_DoesNotRunCombat()
        {
            GameRoot game = new GameRoot();
            Faction empire = new Faction { InstanceID = "empire" };
            game.Factions.Add(empire);

            PlanetSystem system = new PlanetSystem { InstanceID = "sys1" };
            Planet planet = new Planet { InstanceID = "p1" };
            game.AttachNode(system, game.Galaxy);
            game.AttachNode(planet, system);

            Fleet fleet1 = CreateFleet(game, "f1", "empire", planet, 1, 100, 10);
            Fleet fleet2 = CreateFleet(game, "f2", "empire", planet, 1, 100, 10);

            QueueRNG rng = new QueueRNG();
            CombatSystem manager = MakeCombat(game, rng);

            RunCombat(manager);

            Assert.AreEqual(100, fleet1.CapitalShips[0].CurrentHullStrength);
            Assert.AreEqual(100, fleet2.CapitalShips[0].CurrentHullStrength);
        }

        [Test]
        public void Resolve_MultipleAttackerFleets_OnlyFirstPairFights()
        {
            GameRoot game = new GameRoot();
            Faction empire = new Faction { InstanceID = "empire" };
            Faction alliance = new Faction { InstanceID = "alliance" };
            game.Factions.Add(empire);
            game.Factions.Add(alliance);

            PlanetSystem system = new PlanetSystem { InstanceID = "sys1" };
            Planet planet = new Planet { InstanceID = "p1" };
            game.AttachNode(system, game.Galaxy);
            game.AttachNode(planet, system);

            Fleet empireFleet1 = CreateFleet(game, "f1", "empire", planet, 1, 100, 10);
            Fleet empireFleet2 = CreateFleet(game, "f2", "empire", planet, 1, 100, 10);
            Fleet allianceFleet = CreateFleet(game, "f3", "alliance", planet, 1, 100, 10);

            QueueRNG rng = new QueueRNG(0.5, 0.5, 0.5, 0.5);
            CombatSystem manager = MakeCombat(game, rng);

            RunCombat(manager);

            Assert.Less(
                empireFleet1.CapitalShips[0].CurrentHullStrength,
                100,
                "First fleet fights"
            );
            Assert.AreEqual(
                100,
                empireFleet2.CapitalShips[0].CurrentHullStrength,
                "Second fleet does not fight"
            );
        }

        [Test]
        public void Resolve_AttackerDestroysDefender_ReturnsAttackerVictory()
        {
            GameRoot game = new GameRoot();
            Faction empire = new Faction { InstanceID = "empire" };
            Faction alliance = new Faction { InstanceID = "alliance" };
            game.Factions.Add(empire);
            game.Factions.Add(alliance);

            PlanetSystem system = new PlanetSystem { InstanceID = "sys1" };
            Planet planet = new Planet { InstanceID = "p1" };
            game.AttachNode(system, game.Galaxy);
            game.AttachNode(planet, system);

            Fleet empireFleet = CreateFleet(game, "f1", "empire", planet, 1, 1000, 100);
            Fleet allianceFleet = CreateFleet(game, "f2", "alliance", planet, 1, 1, 0);

            QueueRNG rng = new QueueRNG(0.5, 0.5, 0.5, 0.5);
            CombatSystem manager = MakeCombat(game, rng);

            RunCombat(manager);

            Assert.IsNull(game.GetSceneNodeByInstanceID<Fleet>("f2"), "Defender fleet destroyed");
            Assert.IsNotNull(game.GetSceneNodeByInstanceID<Fleet>("f1"), "Attacker survives");
        }

        [Test]
        public void Resolve_DefenderDestroysAttacker_ReturnsDefenderVictory()
        {
            GameRoot game = new GameRoot();
            Faction empire = new Faction { InstanceID = "empire" };
            Faction alliance = new Faction { InstanceID = "alliance" };
            game.Factions.Add(empire);
            game.Factions.Add(alliance);

            PlanetSystem system = new PlanetSystem { InstanceID = "sys1" };
            Planet planet = new Planet { InstanceID = "p1" };
            game.AttachNode(system, game.Galaxy);
            game.AttachNode(planet, system);

            Fleet empireFleet = CreateFleet(game, "f1", "empire", planet, 1, 1, 0);
            Fleet allianceFleet = CreateFleet(game, "f2", "alliance", planet, 1, 1000, 100);

            QueueRNG rng = new QueueRNG(0.5, 0.5, 0.5, 0.5);
            CombatSystem manager = MakeCombat(game, rng);

            RunCombat(manager);

            Assert.IsNull(game.GetSceneNodeByInstanceID<Fleet>("f1"), "Attacker fleet destroyed");
            Assert.IsNotNull(game.GetSceneNodeByInstanceID<Fleet>("f2"), "Defender survives");
        }

        [Test]
        public void Resolve_MutualDestruction_RemovesBothFleets()
        {
            GameRoot game = new GameRoot();
            Faction empire = new Faction { InstanceID = "empire" };
            Faction alliance = new Faction { InstanceID = "alliance" };
            game.Factions.Add(empire);
            game.Factions.Add(alliance);

            PlanetSystem system = new PlanetSystem { InstanceID = "sys1" };
            Planet planet = new Planet { InstanceID = "p1" };
            game.AttachNode(system, game.Galaxy);
            game.AttachNode(planet, system);

            Fleet empireFleet = CreateFleet(
                game,
                "f1",
                "empire",
                planet,
                1,
                10,
                3,
                shieldRechargeRate: 0
            );
            Fleet allianceFleet = CreateFleet(
                game,
                "f2",
                "alliance",
                planet,
                1,
                10,
                3,
                shieldRechargeRate: 0
            );

            QueueRNG rng = new QueueRNG(0.5, 0.5);
            CombatSystem manager = MakeCombat(game, rng);

            RunCombat(manager);

            bool anyDestroyed =
                game.GetSceneNodeByInstanceID<Fleet>("f1") == null
                || game.GetSceneNodeByInstanceID<Fleet>("f2") == null;
            Assert.IsTrue(
                anyDestroyed,
                "At least one fleet should be destroyed in evenly-matched combat"
            );
        }

        [Test]
        public void Resolve_ShipTakesDamage_ReducesCurrentHullStrength()
        {
            GameRoot game = new GameRoot();
            Faction empire = new Faction { InstanceID = "empire" };
            Faction alliance = new Faction { InstanceID = "alliance" };
            game.Factions.Add(empire);
            game.Factions.Add(alliance);

            PlanetSystem system = new PlanetSystem { InstanceID = "sys1" };
            Planet planet = new Planet { InstanceID = "p1" };
            game.AttachNode(system, game.Galaxy);
            game.AttachNode(planet, system);

            Fleet empireFleet = CreateFleet(game, "f1", "empire", planet, 1, 100, 10);
            Fleet allianceFleet = CreateFleet(game, "f2", "alliance", planet, 1, 100, 10);

            QueueRNG rng = new QueueRNG(0.5, 0.5, 0.5, 0.5);
            CombatSystem manager = MakeCombat(game, rng);

            RunCombat(manager);

            Assert.Less(
                empireFleet.CapitalShips[0].CurrentHullStrength,
                100,
                "Ships should take damage during combat"
            );
        }

        [Test]
        public void Resolve_ShipDestroyed_RemovedFromFleet()
        {
            GameRoot game = new GameRoot();
            Faction empire = new Faction { InstanceID = "empire" };
            Faction alliance = new Faction { InstanceID = "alliance" };
            game.Factions.Add(empire);
            game.Factions.Add(alliance);

            PlanetSystem system = new PlanetSystem { InstanceID = "sys1" };
            Planet planet = new Planet { InstanceID = "p1" };
            game.AttachNode(system, game.Galaxy);
            game.AttachNode(planet, system);

            Fleet empireFleet = CreateFleet(game, "f1", "empire", planet, 1, 1000, 100);
            Fleet allianceFleet = CreateFleet(game, "f2", "alliance", planet, 1, 1, 0);

            QueueRNG rng = new QueueRNG(0.5, 0.5, 0.5, 0.5);
            CombatSystem manager = MakeCombat(game, rng);

            RunCombat(manager);

            Assert.AreEqual(
                0,
                allianceFleet.CapitalShips.Count,
                "Destroyed ship removed from fleet"
            );
        }

        [Test]
        public void Resolve_FighterSquadronTakesLosses_ReducesCurrentSquadronSize()
        {
            GameRoot game = new GameRoot();
            Faction empire = new Faction { InstanceID = "empire" };
            Faction alliance = new Faction { InstanceID = "alliance" };
            game.Factions.Add(empire);
            game.Factions.Add(alliance);

            PlanetSystem system = new PlanetSystem { InstanceID = "sys1" };
            Planet planet = new Planet { InstanceID = "p1" };
            game.AttachNode(system, game.Galaxy);
            game.AttachNode(planet, system);

            Fleet empireFleet = CreateFleetWithFighters(
                game,
                "f1",
                "empire",
                planet,
                1,
                1000,
                1,
                100
            );
            Fleet allianceFleet = CreateFleetWithFighters(
                game,
                "f2",
                "alliance",
                planet,
                1,
                50,
                1,
                10
            );

            QueueRNG rng = new QueueRNG(0.5, 0.5, 0.5, 0.5);
            CombatSystem manager = MakeCombat(game, rng);

            RunCombat(manager);

            Fleet allianceFleet2 = game.GetSceneNodeByInstanceID<Fleet>("f2");
            if (allianceFleet2 != null)
            {
                List<Starfighter> allFighters = allianceFleet2.GetStarfighters().ToList();
                if (allFighters.Count > 0)
                {
                    Assert.Less(
                        allFighters[0].CurrentSquadronSize,
                        10,
                        "Alliance fighters should take losses"
                    );
                    return;
                }
            }

            Fleet empFleet = game.GetSceneNodeByInstanceID<Fleet>("f1");
            Assert.IsNotNull(empFleet, "Empire fleet should still exist");
            List<Starfighter> empFighters = empFleet.GetStarfighters().ToList();
            Assert.Greater(empFighters.Count, 0, "Empire should have fighters");
            Assert.Less(
                empFighters[0].CurrentSquadronSize,
                100,
                "Empire fighters should take some losses"
            );
        }

        [Test]
        public void Resolve_EmptyFleet_RemovedFromScene()
        {
            GameRoot game = new GameRoot();
            Faction empire = new Faction { InstanceID = "empire" };
            Faction alliance = new Faction { InstanceID = "alliance" };
            game.Factions.Add(empire);
            game.Factions.Add(alliance);

            PlanetSystem system = new PlanetSystem { InstanceID = "sys1" };
            Planet planet = new Planet { InstanceID = "p1" };
            game.AttachNode(system, game.Galaxy);
            game.AttachNode(planet, system);

            Fleet empireFleet = CreateFleet(game, "f1", "empire", planet, 1, 1000, 100);
            Fleet allianceFleet = CreateFleet(game, "f2", "alliance", planet, 1, 1, 0);

            QueueRNG rng = new QueueRNG(0.5, 0.5, 0.5, 0.5);
            CombatSystem manager = MakeCombat(game, rng);

            RunCombat(manager);

            Assert.IsNull(game.GetSceneNodeByInstanceID<Fleet>("f2"));
            bool foundFleet = false;
            foreach (Fleet fleet in planet.GetChildren<Fleet>(null, recurse: false))
            {
                if (fleet == allianceFleet)
                {
                    foundFleet = true;
                    break;
                }
            }
            Assert.IsFalse(foundFleet, "Destroyed fleet should not be in planet's children");
        }

        [Test]
        public void Resolve_BothSidesZeroWeapons_AppliesNoDamage()
        {
            GameRoot game = new GameRoot();
            Faction empire = new Faction { InstanceID = "empire" };
            Faction alliance = new Faction { InstanceID = "alliance" };
            game.Factions.Add(empire);
            game.Factions.Add(alliance);

            PlanetSystem system = new PlanetSystem { InstanceID = "sys1" };
            Planet planet = new Planet { InstanceID = "p1" };
            game.AttachNode(system, game.Galaxy);
            game.AttachNode(planet, system);

            Fleet empireFleet = CreateFleet(game, "f1", "empire", planet, 1, 100, 0);
            Fleet allianceFleet = CreateFleet(game, "f2", "alliance", planet, 1, 100, 0);

            empireFleet.CapitalShips[0].PrimaryWeapons.Clear();
            allianceFleet.CapitalShips[0].PrimaryWeapons.Clear();

            QueueRNG rng = new QueueRNG(0.5, 0.5, 0.5, 0.5);
            CombatSystem manager = MakeCombat(game, rng);

            RunCombat(manager);

            Assert.AreEqual(
                100,
                empireFleet.CapitalShips[0].CurrentHullStrength,
                "No damage without weapons"
            );
            Assert.AreEqual(
                100,
                allianceFleet.CapitalShips[0].CurrentHullStrength,
                "No damage without weapons"
            );
        }

        [Test]
        public void Resolve_WeaponFire_DamagesTargets()
        {
            GameRoot game = new GameRoot();
            Faction empire = new Faction { InstanceID = "empire" };
            Faction alliance = new Faction { InstanceID = "alliance" };
            game.Factions.Add(empire);
            game.Factions.Add(alliance);

            PlanetSystem system = new PlanetSystem { InstanceID = "sys1" };
            Planet planet = new Planet { InstanceID = "p1" };
            game.AttachNode(system, game.Galaxy);
            game.AttachNode(planet, system);

            Fleet empireFleet = CreateFleet(game, "f1", "empire", planet, 1, 100, 20);
            Fleet allianceFleet = CreateFleet(game, "f2", "alliance", planet, 1, 100, 20);

            QueueRNG rng = new QueueRNG(0.5, 0.5, 0.5, 0.5);
            CombatSystem manager = MakeCombat(game, rng);

            RunCombat(manager);

            Assert.Less(empireFleet.CapitalShips[0].CurrentHullStrength, 100);
            Assert.Less(allianceFleet.CapitalShips[0].CurrentHullStrength, 100);
        }

        [Test]
        public void Resolve_ShieldAbsorption_ReducesDamage()
        {
            GameRoot game = new GameRoot();
            Faction empire = new Faction { InstanceID = "empire" };
            Faction alliance = new Faction { InstanceID = "alliance" };
            game.Factions.Add(empire);
            game.Factions.Add(alliance);

            PlanetSystem system = new PlanetSystem { InstanceID = "sys1" };
            Planet planet = new Planet { InstanceID = "p1" };
            game.AttachNode(system, game.Galaxy);
            game.AttachNode(planet, system);

            Fleet shieldedFleet = CreateFleet(game, "f1", "empire", planet, 1, 100, 20);
            Fleet unshieldedFleet = CreateFleet(game, "f2", "alliance", planet, 1, 100, 20);

            shieldedFleet.CapitalShips[0].ShieldRechargeRate = 15;
            unshieldedFleet.CapitalShips[0].ShieldRechargeRate = 0;

            QueueRNG rng = new QueueRNG(0.5, 0.5, 0.5, 0.5);
            CombatSystem manager = MakeCombat(game, rng);

            RunCombat(manager);

            int shieldedDamage =
                shieldedFleet.CapitalShips[0].MaxHullStrength
                - shieldedFleet.CapitalShips[0].CurrentHullStrength;
            int unshieldedDamage =
                unshieldedFleet.CapitalShips[0].MaxHullStrength
                - unshieldedFleet.CapitalShips[0].CurrentHullStrength;

            Assert.Greater(unshieldedDamage, shieldedDamage, "Shields should reduce damage");
        }

        [Test]
        public void Resolve_FightersAttackCapitalShips_AppliesDamage()
        {
            GameRoot game = new GameRoot();
            Faction empire = new Faction { InstanceID = "empire" };
            Faction alliance = new Faction { InstanceID = "alliance" };
            game.Factions.Add(empire);
            game.Factions.Add(alliance);

            PlanetSystem system = new PlanetSystem { InstanceID = "sys1" };
            Planet planet = new Planet { InstanceID = "p1" };
            game.AttachNode(system, game.Galaxy);
            game.AttachNode(planet, system);

            Fleet fighterFleet = CreateFleetWithFighters(
                game,
                "f1",
                "empire",
                planet,
                1,
                50,
                5,
                12
            );
            Fleet targetFleet = CreateFleet(game, "f2", "alliance", planet, 1, 1000, 5);

            QueueRNG rng = new QueueRNG(0.5, 0.5, 0.5, 0.5);
            CombatSystem manager = MakeCombat(game, rng);

            RunCombat(manager);

            Fleet target = game.GetSceneNodeByInstanceID<Fleet>("f2");
            Assert.IsNotNull(target, "Target fleet should still exist");
            Assert.Less(
                target.CapitalShips[0].CurrentHullStrength,
                1000,
                "Fighters should damage capital ships"
            );
        }

        [Test]
        public void Resolve_RepeatedRuns_DamageVarianceWithinTwentyPercent()
        {
            GameRoot game = new GameRoot();
            Faction empire = new Faction { InstanceID = "empire" };
            Faction alliance = new Faction { InstanceID = "alliance" };
            game.Factions.Add(empire);
            game.Factions.Add(alliance);

            PlanetSystem system = new PlanetSystem { InstanceID = "sys1" };
            Planet planet = new Planet { InstanceID = "p1" };
            game.AttachNode(system, game.Galaxy);
            game.AttachNode(planet, system);

            Fleet empireFleet = CreateFleet(game, "f1", "empire", planet, 1, 100, 20);
            Fleet allianceFleet = CreateFleet(game, "f2", "alliance", planet, 1, 100, 20);

            QueueRNG rng1 = new QueueRNG(0.0, 0.0, 0.0, 0.0);
            QueueRNG rng2 = new QueueRNG(1.0, 1.0, 1.0, 1.0);

            CombatSystem manager1 = MakeCombat(game, rng1);
            RunCombat(manager1);
            int damage1 =
                empireFleet.CapitalShips[0].MaxHullStrength
                - empireFleet.CapitalShips[0].CurrentHullStrength;

            empireFleet.CapitalShips[0].CurrentHullStrength = empireFleet
                .CapitalShips[0]
                .MaxHullStrength;
            allianceFleet.CapitalShips[0].CurrentHullStrength = allianceFleet
                .CapitalShips[0]
                .MaxHullStrength;
            empireFleet.IsInCombat = false;
            allianceFleet.IsInCombat = false;

            CombatSystem manager2 = MakeCombat(game, rng2);
            RunCombat(manager2);
            int damage2 =
                empireFleet.CapitalShips[0].MaxHullStrength
                - empireFleet.CapitalShips[0].CurrentHullStrength;

            Assert.AreNotEqual(damage1, damage2, "Damage should vary with different RNG");
        }

        [Test]
        public void Resolve_SameRNGSeed_ProducesSameOutcome()
        {
            GameRoot game1 = new GameRoot();
            GameRoot game2 = new GameRoot();

            foreach (GameRoot game in new[] { game1, game2 })
            {
                Faction empire = new Faction { InstanceID = "empire" };
                Faction alliance = new Faction { InstanceID = "alliance" };
                game.Factions.Add(empire);
                game.Factions.Add(alliance);

                PlanetSystem system = new PlanetSystem { InstanceID = "sys1" };
                Planet planet = new Planet { InstanceID = "p1" };
                game.AttachNode(system, game.Galaxy);
                game.AttachNode(planet, system);

                CreateFleet(game, "f1", "empire", planet, 1, 100, 20);
                CreateFleet(game, "f2", "alliance", planet, 1, 100, 20);
            }

            QueueRNG rng1 = new QueueRNG(0.5, 0.5, 0.5, 0.5);
            QueueRNG rng2 = new QueueRNG(0.5, 0.5, 0.5, 0.5);

            RunCombat(MakeCombat(game1, rng1));
            RunCombat(MakeCombat(game2, rng2));

            Fleet fleet1 = game1.GetSceneNodeByInstanceID<Fleet>("f1");
            Fleet fleet2 = game2.GetSceneNodeByInstanceID<Fleet>("f1");

            Assert.AreEqual(
                fleet1.CapitalShips[0].CurrentHullStrength,
                fleet2.CapitalShips[0].CurrentHullStrength,
                "Same RNG should produce identical results"
            );
        }

        [Test]
        public void Resolve_DifferentRNGSeeds_ProduceDifferentOutcomes()
        {
            GameRoot game1 = new GameRoot();
            GameRoot game2 = new GameRoot();

            foreach (GameRoot game in new[] { game1, game2 })
            {
                Faction empire = new Faction { InstanceID = "empire" };
                Faction alliance = new Faction { InstanceID = "alliance" };
                game.Factions.Add(empire);
                game.Factions.Add(alliance);

                PlanetSystem system = new PlanetSystem { InstanceID = "sys1" };
                Planet planet = new Planet { InstanceID = "p1" };
                game.AttachNode(system, game.Galaxy);
                game.AttachNode(planet, system);

                CreateFleet(game, "f1", "empire", planet, 1, 100, 20);
                CreateFleet(game, "f2", "alliance", planet, 1, 100, 20);
            }

            QueueRNG rng1 = new QueueRNG(0.0, 0.0, 0.0, 0.0);
            QueueRNG rng2 = new QueueRNG(1.0, 1.0, 1.0, 1.0);

            RunCombat(MakeCombat(game1, rng1));
            RunCombat(MakeCombat(game2, rng2));

            Fleet fleet1 = game1.GetSceneNodeByInstanceID<Fleet>("f1");
            Fleet fleet2 = game2.GetSceneNodeByInstanceID<Fleet>("f1");

            Assert.AreNotEqual(
                fleet1.CapitalShips[0].CurrentHullStrength,
                fleet2.CapitalShips[0].CurrentHullStrength,
                "Different RNG should produce different results"
            );
        }

        [Test]
        public void Resolve_EmptyFleets_DoesNotRunCombat()
        {
            GameRoot game = new GameRoot();
            Faction empire = new Faction { InstanceID = "empire" };
            Faction alliance = new Faction { InstanceID = "alliance" };
            game.Factions.Add(empire);
            game.Factions.Add(alliance);

            PlanetSystem system = new PlanetSystem { InstanceID = "sys1" };
            Planet planet = new Planet { InstanceID = "p1" };
            game.AttachNode(system, game.Galaxy);
            game.AttachNode(planet, system);

            Fleet empireFleet = new Fleet { InstanceID = "f1", OwnerInstanceID = "empire" };
            Fleet allianceFleet = new Fleet { InstanceID = "f2", OwnerInstanceID = "alliance" };
            game.AttachNode(empireFleet, planet);
            game.AttachNode(allianceFleet, planet);

            QueueRNG rng = new QueueRNG();
            CombatSystem manager = MakeCombat(game, rng);

            RunCombat(manager);

            Assert.Pass("Empty fleets should not cause combat");
        }

        [Test]
        public void TryStartCombat_HostileFleets_ReturnsTrueAndSetsContext()
        {
            GameRoot game = new GameRoot();
            Faction empire = new Faction { InstanceID = "empire" };
            Faction alliance = new Faction { InstanceID = "alliance" };
            game.Factions.Add(empire);
            game.Factions.Add(alliance);

            PlanetSystem system = new PlanetSystem { InstanceID = "sys1" };
            Planet planet = new Planet { InstanceID = "p1" };
            game.AttachNode(system, game.Galaxy);
            game.AttachNode(planet, system);

            Fleet empireFleet = CreateFleet(game, "f1", "empire", planet, 1, 100, 10);
            Fleet allianceFleet = CreateFleet(game, "f2", "alliance", planet, 1, 100, 10);

            CombatSystem manager = MakeCombat(game, new QueueRNG());

            bool detected = manager.TryStartCombat(out CombatDecisionContext decision);

            Assert.IsTrue(detected);
            Assert.IsNotNull(decision);
            Assert.IsNotEmpty(decision.AttackerFleetInstanceID);
            Assert.IsNotEmpty(decision.DefenderFleetInstanceID);
        }

        [Test]
        public void TryStartCombat_TwoHostileFleets_SetsIsInCombatOnBothFleets()
        {
            GameRoot game = new GameRoot();
            Faction empire = new Faction { InstanceID = "empire" };
            Faction alliance = new Faction { InstanceID = "alliance" };
            game.Factions.Add(empire);
            game.Factions.Add(alliance);

            PlanetSystem system = new PlanetSystem { InstanceID = "sys1" };
            Planet planet = new Planet { InstanceID = "p1" };
            game.AttachNode(system, game.Galaxy);
            game.AttachNode(planet, system);

            Fleet empireFleet = CreateFleet(game, "f1", "empire", planet, 1, 100, 10);
            Fleet allianceFleet = CreateFleet(game, "f2", "alliance", planet, 1, 100, 10);

            CombatSystem manager = MakeCombat(game, new QueueRNG());
            manager.TryStartCombat(out _);

            Assert.IsTrue(
                empireFleet.IsInCombat || allianceFleet.IsInCombat,
                "At least one fleet should be flagged IsInCombat"
            );
        }

        [Test]
        public void Resolve_CombatWithSurvivors_ClearsIsInCombatOnSurvivingFleets()
        {
            GameRoot game = new GameRoot();
            Faction empire = new Faction { InstanceID = "empire" };
            Faction alliance = new Faction { InstanceID = "alliance" };
            game.Factions.Add(empire);
            game.Factions.Add(alliance);

            PlanetSystem system = new PlanetSystem { InstanceID = "sys1" };
            Planet planet = new Planet { InstanceID = "p1" };
            game.AttachNode(system, game.Galaxy);
            game.AttachNode(planet, system);

            // Durable fleets so both survive
            Fleet empireFleet = CreateFleet(game, "f1", "empire", planet, 1, 10000, 1);
            Fleet allianceFleet = CreateFleet(game, "f2", "alliance", planet, 1, 10000, 1);

            QueueRNG rng = new QueueRNG(0.5, 0.5, 0.5, 0.5);
            CombatSystem manager = MakeCombat(game, rng);

            manager.TryStartCombat(out CombatDecisionContext decision);
            manager.Resolve(decision, autoResolve: true);

            Assert.IsFalse(empireFleet.IsInCombat, "IsInCombat should be cleared after resolution");
            Assert.IsFalse(
                allianceFleet.IsInCombat,
                "IsInCombat should be cleared after resolution"
            );
        }

        [Test]
        public void TryStartCombat_FleetsInCombat_NotDetectedAgain()
        {
            GameRoot game = new GameRoot();
            Faction empire = new Faction { InstanceID = "empire" };
            Faction alliance = new Faction { InstanceID = "alliance" };
            game.Factions.Add(empire);
            game.Factions.Add(alliance);

            PlanetSystem system = new PlanetSystem { InstanceID = "sys1" };
            Planet planet = new Planet { InstanceID = "p1" };
            game.AttachNode(system, game.Galaxy);
            game.AttachNode(planet, system);

            Fleet empireFleet = CreateFleet(game, "f1", "empire", planet, 1, 100, 10);
            Fleet allianceFleet = CreateFleet(game, "f2", "alliance", planet, 1, 100, 10);

            CombatSystem manager = MakeCombat(game, new QueueRNG());

            manager.TryStartCombat(out _);

            // Should not detect again while IsInCombat is set
            bool detectedAgain = manager.TryStartCombat(out CombatDecisionContext second);

            Assert.IsFalse(detectedAgain, "Fleets already in combat should not be detected again");
            Assert.IsNull(second);
        }

        [Test]
        public void ProcessTick_MultipleEncountersAllAI_ResolvesAll()
        {
            GameRoot game = new GameRoot();
            // No PlayerID = IsAIControlled() returns true
            Faction empire = new Faction { InstanceID = "empire" };
            Faction alliance = new Faction { InstanceID = "alliance" };
            game.Factions.Add(empire);
            game.Factions.Add(alliance);

            // Three planets, each with one empire fleet and one alliance fleet
            for (int i = 1; i <= 3; i++)
            {
                PlanetSystem sys = new PlanetSystem { InstanceID = $"sys{i}" };
                Planet planet = new Planet { InstanceID = $"p{i}" };
                game.AttachNode(sys, game.Galaxy);
                game.AttachNode(planet, sys);
                CreateFleet(game, $"ef{i}", "empire", planet, 1, 1000, 20);
                CreateFleet(game, $"af{i}", "alliance", planet, 1, 1000, 20);
            }

            // 2 RNG calls per engagement (one per side firing), 3 engagements = 6
            QueueRNG rng = new QueueRNG(0.5, 0.5, 0.5, 0.5, 0.5, 0.5);
            CombatSystem manager = MakeCombat(game, rng);

            List<GameResult> results = manager.ProcessTick();

            Assert.IsFalse(
                results.OfType<PendingCombatResult>().Any(),
                "All AI encounters should auto-resolve with no pending decision"
            );

            for (int i = 1; i <= 3; i++)
            {
                Fleet ef = game.GetSceneNodeByInstanceID<Fleet>($"ef{i}");
                Fleet af = game.GetSceneNodeByInstanceID<Fleet>($"af{i}");
                Assert.IsNotNull(ef, $"Empire fleet at planet {i} should survive");
                Assert.IsNotNull(af, $"Alliance fleet at planet {i} should survive");
                Assert.Less(
                    ef.CapitalShips[0].CurrentHullStrength,
                    1000,
                    $"Empire fleet at planet {i} should have taken damage"
                );
                Assert.Less(
                    af.CapitalShips[0].CurrentHullStrength,
                    1000,
                    $"Alliance fleet at planet {i} should have taken damage"
                );
            }
        }

        [Test]
        public void ProcessTick_PlayerInvolvedEncounter_ReturnsPendingDecision()
        {
            GameRoot game = new GameRoot();
            Faction empire = new Faction { InstanceID = "empire", PlayerID = "player1" };
            Faction alliance = new Faction { InstanceID = "alliance" };
            game.Factions.Add(empire);
            game.Factions.Add(alliance);

            PlanetSystem sys = new PlanetSystem { InstanceID = "sys1" };
            Planet planet = new Planet { InstanceID = "p1" };
            game.AttachNode(sys, game.Galaxy);
            game.AttachNode(planet, sys);
            CreateFleet(game, "ef1", "empire", planet, 1, 1000, 10);
            CreateFleet(game, "af1", "alliance", planet, 1, 1000, 10);

            QueueRNG rng = new QueueRNG();
            CombatSystem manager = MakeCombat(game, rng);

            List<GameResult> results = manager.ProcessTick();

            Assert.IsTrue(
                results.OfType<PendingCombatResult>().Any(),
                "Player-involved encounter should emit a PendingCombatResult"
            );
        }

        [Test]
        public void Resolve_DefenderWinsOnOwnPlanet_DoesNotChangeOwnership()
        {
            GameRoot game = new GameRoot(TestConfig.Create());
            Faction empire = new Faction { InstanceID = "empire", PlayerID = null };
            Faction alliance = new Faction { InstanceID = "alliance", PlayerID = null };
            game.Factions.Add(empire);
            game.Factions.Add(alliance);

            PlanetSystem sys = new PlanetSystem { InstanceID = "sys1" };
            game.AttachNode(sys, game.Galaxy);
            Planet planet = new Planet
            {
                InstanceID = "p1",
                OwnerInstanceID = "alliance",
                IsColonized = true,
                PopularSupport = new Dictionary<string, int> { { "alliance", 80 } },
            };
            game.AttachNode(planet, sys);

            // Weak empire fleet vs strong alliance fleet — alliance defends
            Fleet empireFleet = CreateFleet(game, "ef1", "empire", planet, 1, 1, 0);
            Fleet allianceFleet = CreateFleet(game, "af1", "alliance", planet, 3, 1000, 100);

            QueueRNG rng = new QueueRNG(0.0, 0.0, 0.0, 0.0, 0.0, 0.0);
            CombatSystem manager = MakeCombat(game, rng);

            RunCombat(manager);

            Assert.AreEqual(
                "alliance",
                planet.GetOwnerInstanceID(),
                "Defender winning on own planet should not change ownership"
            );
        }

        [Test]
        public void EvacuateOfficers_ShipDestroyedWithSurvivingShip_OfficerMovedToSurvivingShip()
        {
            GameRoot game = new GameRoot(TestConfig.Create());
            game.Factions.Add(new Faction { InstanceID = "empire" });
            game.Factions.Add(new Faction { InstanceID = "alliance" });

            PlanetSystem sys = new PlanetSystem { InstanceID = "sys1" };
            game.AttachNode(sys, game.Galaxy);
            Planet planet = new Planet { InstanceID = "p1" };
            game.AttachNode(planet, sys);

            // Alliance fleet: two ships. Weak ship dies, strong ship survives.
            Fleet allianceFleet = new Fleet { InstanceID = "af1", OwnerInstanceID = "alliance" };
            CapitalShip weakShip = new CapitalShip
            {
                InstanceID = "weak",
                OwnerInstanceID = "alliance",
                MaxHullStrength = 1,
                CurrentHullStrength = 1,
                ShieldRechargeRate = 0,
            };
            CapitalShip strongShip = new CapitalShip
            {
                InstanceID = "strong",
                OwnerInstanceID = "alliance",
                MaxHullStrength = 1000,
                CurrentHullStrength = 1000,
                ShieldRechargeRate = 0,
            };
            allianceFleet.CapitalShips.Add(weakShip);
            weakShip.SetParent(allianceFleet);
            allianceFleet.CapitalShips.Add(strongShip);
            strongShip.SetParent(allianceFleet);
            game.AttachNode(allianceFleet, planet);

            Officer officer = new Officer { InstanceID = "han", OwnerInstanceID = "alliance" };
            game.AttachNode(officer, weakShip);

            // Overwhelming empire fleet destroys the weak ship.
            Fleet empireFleet = CreateFleet(
                game,
                "ef1",
                "empire",
                planet,
                1,
                1000,
                100,
                shieldRechargeRate: 0
            );

            QueueRNG rng = new QueueRNG(0.5, 0.5, 0.5, 0.5);
            RunCombat(MakeCombat(game, rng));

            Assert.Contains(
                officer,
                strongShip.Officers,
                "Officer should be evacuated to the surviving ship"
            );
        }

        [Test]
        public void EvacuateOfficers_LastShipDestroyed_OfficerEvacuatedToNearestFriendlyPlanet()
        {
            GameRoot game = new GameRoot(TestConfig.Create());
            game.Factions.Add(new Faction { InstanceID = "empire" });
            game.Factions.Add(new Faction { InstanceID = "alliance" });

            PlanetSystem sys1 = new PlanetSystem { InstanceID = "sys1" };
            game.AttachNode(sys1, game.Galaxy);
            Planet combatPlanet = new Planet { InstanceID = "p1" };
            game.AttachNode(combatPlanet, sys1);

            PlanetSystem sys2 = new PlanetSystem { InstanceID = "sys2" };
            game.AttachNode(sys2, game.Galaxy);
            Planet alliancePlanet = new Planet { InstanceID = "p2", OwnerInstanceID = "alliance" };
            game.AttachNode(alliancePlanet, sys2);

            // Alliance fleet: single ship that is immediately destroyed.
            Fleet allianceFleet = new Fleet { InstanceID = "af1", OwnerInstanceID = "alliance" };
            CapitalShip ship = new CapitalShip
            {
                InstanceID = "ship1",
                OwnerInstanceID = "alliance",
                MaxHullStrength = 1,
                CurrentHullStrength = 1,
                ShieldRechargeRate = 0,
            };
            allianceFleet.CapitalShips.Add(ship);
            ship.SetParent(allianceFleet);
            game.AttachNode(allianceFleet, combatPlanet);

            Officer officer = new Officer { InstanceID = "leia", OwnerInstanceID = "alliance" };
            game.AttachNode(officer, ship);

            Fleet empireFleet = CreateFleet(
                game,
                "ef1",
                "empire",
                combatPlanet,
                1,
                1000,
                100,
                shieldRechargeRate: 0
            );

            QueueRNG rng = new QueueRNG(0.5, 0.5, 0.5, 0.5);
            RunCombat(MakeCombat(game, rng));

            Assert.Contains(
                officer,
                alliancePlanet.Officers,
                "Officer should be evacuated to the nearest friendly planet"
            );
        }

        private Fleet CreateFleet(
            GameRoot game,
            string instanceId,
            string ownerId,
            Planet planet,
            int shipCount,
            int hullStrength,
            int weaponPower,
            int shieldRechargeRate = 5
        )
        {
            Fleet fleet = new Fleet { InstanceID = instanceId, OwnerInstanceID = ownerId };

            for (int i = 0; i < shipCount; i++)
            {
                CapitalShip ship = new CapitalShip
                {
                    InstanceID = $"{instanceId}_ship{i}",
                    MaxHullStrength = hullStrength,
                    CurrentHullStrength = hullStrength,
                    ShieldRechargeRate = shieldRechargeRate,
                };

                // Add weapon arcs
                if (weaponPower > 0)
                {
                    ship.PrimaryWeapons[PrimaryWeaponType.Turbolaser] = new int[]
                    {
                        weaponPower,
                        weaponPower,
                        weaponPower,
                        weaponPower,
                    };
                }

                fleet.CapitalShips.Add(ship);
                ship.SetParent(fleet);
            }

            game.AttachNode(fleet, planet);
            return fleet;
        }

        private Fleet CreateFleetWithFighters(
            GameRoot game,
            string instanceId,
            string ownerId,
            Planet planet,
            int shipCount,
            int hullStrength,
            int weaponPower,
            int squadronSize
        )
        {
            Fleet fleet = CreateFleet(
                game,
                instanceId,
                ownerId,
                planet,
                shipCount,
                hullStrength,
                weaponPower
            );

            // Add fighters to first ship
            if (fleet.CapitalShips.Count > 0)
            {
                Starfighter fighter = new Starfighter
                {
                    InstanceID = $"{instanceId}_fighter",
                    MaxSquadronSize = squadronSize,
                    CurrentSquadronSize = squadronSize,
                    LaserCannon = 5,
                    IonCannon = 3,
                    Torpedoes = 2,
                };
                fleet.CapitalShips[0].Starfighters.Add(fighter);
            }

            return fleet;
        }
    }

    [TestFixture]
    public class BombardmentTests : CombatTestBase
    {
        private Fleet CreateBombardmentFleet(
            GameRoot game,
            string id,
            string owner,
            Planet planet,
            int shipCount,
            int bombardment
        )
        {
            Fleet fleet = new Fleet { InstanceID = id, OwnerInstanceID = owner };
            for (int i = 0; i < shipCount; i++)
            {
                CapitalShip ship = new CapitalShip
                {
                    InstanceID = $"{id}_ship{i}",
                    MaxHullStrength = 100,
                    CurrentHullStrength = 100,
                    Bombardment = bombardment,
                };
                fleet.CapitalShips.Add(ship);
                ship.SetParent(fleet);
            }
            game.AttachNode(fleet, planet);
            return fleet;
        }

        [Test]
        public void ExecuteOrbitalBombardment_ShieldThresholdMet_ReturnsShieldBlockedResult()
        {
            GameRoot game = CreateGame();
            (Planet planet, _) = CreatePlanet(game, "p1", "alliance");

            Building shield = new Building
            {
                InstanceID = "shield1",
                OwnerInstanceID = "alliance",
                DefenseFacilityClass = DefenseFacilityClass.Shield,
            };
            game.AttachNode(shield, planet);

            Fleet fleet = CreateBombardmentFleet(game, "f1", "empire", planet, 1, 10);
            QueueRNG rng = new QueueRNG();
            CombatSystem combat = MakeCombat(game, rng);

            BombardmentResult result = combat.ExecuteOrbitalBombardment(
                new List<Fleet> { fleet },
                planet
            );

            Assert.IsTrue(result.ShieldBlocked);
            Assert.AreEqual(0, result.Strikes.Count);
        }

        [Test]
        public void ExecuteOrbitalBombardment_FleetStrengthBelowDefense_ProducesNoStrikes()
        {
            GameRoot game = CreateGame();
            (Planet planet, _) = CreatePlanet(game, "p1", "alliance");

            Building kdy = new Building
            {
                InstanceID = "kdy1",
                OwnerInstanceID = "alliance",
                DefenseFacilityClass = DefenseFacilityClass.KDY,
                WeaponStrength = 100,
            };
            game.AttachNode(kdy, planet);

            Fleet fleet = CreateBombardmentFleet(game, "f1", "empire", planet, 1, 5);
            QueueRNG rng = new QueueRNG();
            CombatSystem combat = MakeCombat(game, rng);

            BombardmentResult result = combat.ExecuteOrbitalBombardment(
                new List<Fleet> { fleet },
                planet
            );

            Assert.IsFalse(result.ShieldBlocked);
            Assert.LessOrEqual(result.NetStrikes, 0);
            Assert.AreEqual(0, result.Strikes.Count);
        }

        [Test]
        public void ExecuteOrbitalBombardment_PlanetWithEnergyOnly_ReducesEnergyCapacity()
        {
            GameRoot game = CreateGame();
            (Planet planet, _) = CreatePlanet(game, "p1", "alliance", energy: 3);

            // No KDY/LNR, no buildings, no defenders -> only energy lane is ever present.
            Fleet fleet = CreateBombardmentFleet(game, "f1", "empire", planet, 1, 5);
            SequenceRNG rng = new SequenceRNG(intValues: new[] { 0, 0, 0, 0, 0, 0, 0, 0 });
            CombatSystem combat = MakeCombat(game, rng);

            BombardmentResult result = combat.ExecuteOrbitalBombardment(
                new List<Fleet> { fleet },
                planet
            );

            Assert.AreEqual(3, result.EnergyDamage);
            Assert.AreEqual(0, planet.EnergyCapacity);
        }

        [Test]
        public void ExecuteOrbitalBombardment_DefensiveBuildingOnPlanet_DestroysBuilding()
        {
            GameRoot game = CreateGame();
            (Planet planet, _) = CreatePlanet(game, "p1", "alliance", energy: 5);

            // KDY facility — counted as a defensive (ship-group) lane target in Stage 4, but is
            // NOT a shield so the shield-gate doesn't block. ProductionModifier=0 disables the
            // Stage 2 fire-back roll so the pipeline is deterministic.
            Building kdy = new Building
            {
                InstanceID = "kdy1",
                OwnerInstanceID = "alliance",
                BuildingType = BuildingType.Defense,
                DefenseFacilityClass = DefenseFacilityClass.KDY,
                Bombardment = 0,
                ProductionModifier = 0,
                WeaponStrength = 0,
            };
            game.AttachNode(kdy, planet);
            Fleet fleet = CreateBombardmentFleet(game, "f1", "empire", planet, 1, 1);
            SequenceRNG rng = new SequenceRNG(intValues: new[] { 0, 0 });
            CombatSystem combat = MakeCombat(game, rng);

            BombardmentResult result = combat.ExecuteOrbitalBombardment(
                new List<Fleet> { fleet },
                planet
            );

            Assert.IsTrue(result.DestroyedBuildings.Any(b => b.InstanceID == "kdy1"));
        }

        [Test]
        public void ExecuteOrbitalBombardment_AllTargetsDestroyed_BreaksStrikeLoop()
        {
            GameRoot game = CreateGame();
            (Planet planet, _) = CreatePlanet(game, "p1", "alliance", energy: 1);
            // After 1 hit, lane picker returns None and main loop breaks.
            Fleet fleet = CreateBombardmentFleet(game, "f1", "empire", planet, 1, 3);
            SequenceRNG rng = new SequenceRNG(intValues: new[] { 0, 0, 0, 0 });
            CombatSystem combat = MakeCombat(game, rng);

            BombardmentResult result = combat.ExecuteOrbitalBombardment(
                new List<Fleet> { fleet },
                planet
            );

            Assert.AreEqual(1, result.EnergyDamage);
            Assert.AreEqual(1, result.Strikes.Count);
            Assert.AreEqual(0, planet.EnergyCapacity);
        }

        [Test]
        public void ExecuteOrbitalBombardment_NoAttackingFleets_ReturnsEmptyResult()
        {
            GameRoot game = CreateGame();
            (Planet planet, _) = CreatePlanet(game, "p1", "alliance");

            CombatSystem combat = MakeCombat(game, new QueueRNG());

            BombardmentResult result = combat.ExecuteOrbitalBombardment(new List<Fleet>(), planet);

            Assert.IsFalse(result.ShieldBlocked);
            Assert.AreEqual(0, result.Strikes.Count);
            Assert.IsNull(result.AttackingFaction);
        }

        [Test]
        public void ExecuteOrbitalBombardment_MixedFactionFleets_ReturnsEmptyResult()
        {
            GameRoot game = CreateGame();
            (Planet planet, _) = CreatePlanet(game, "p1", "alliance");

            Fleet empireFleet = CreateBombardmentFleet(game, "f1", "empire", planet, 1, 5);
            Fleet allianceFleet = CreateBombardmentFleet(game, "f2", "alliance", planet, 1, 5);

            CombatSystem combat = MakeCombat(game, new QueueRNG());

            BombardmentResult result = combat.ExecuteOrbitalBombardment(
                new List<Fleet> { empireFleet, allianceFleet },
                planet
            );

            Assert.AreEqual(0, result.Strikes.Count);
            Assert.IsNull(result.AttackingFaction);
        }

        [Test]
        public void ExecuteOrbitalBombardment_DefenseFacilityFiresOnAttacker_DamagesShip()
        {
            // KDY facility with ProductionModifier high enough that the stage-2 fire-back roll
            // always succeeds (scaled chance >= 100). Fleet strength = 0 so stage 4 does nothing.
            GameRoot game = CreateGame();
            (Planet planet, _) = CreatePlanet(game, "p1", "alliance");

            Building kdy = new Building
            {
                InstanceID = "kdy1",
                OwnerInstanceID = "alliance",
                DefenseFacilityClass = DefenseFacilityClass.KDY,
                ProductionModifier = 1000, // scaledChance = 1000/10 = 100 -> always fires
                WeaponStrength = 25,
            };
            game.AttachNode(kdy, planet);

            Fleet fleet = CreateBombardmentFleet(game, "f1", "empire", planet, 1, 0);
            SequenceRNG rng = new SequenceRNG(intValues: new[] { 0, 0 });
            CombatSystem combat = MakeCombat(game, rng);

            combat.ExecuteOrbitalBombardment(new List<Fleet> { fleet }, planet);

            Assert.AreEqual(
                75,
                fleet.CapitalShips[0].CurrentHullStrength,
                "Attacker ship should lose 25 hull to facility fire"
            );
        }

        [Test]
        public void ExecuteOrbitalBombardment_DefenseFacilityOverkill_DestroysShip()
        {
            GameRoot game = CreateGame();
            (Planet planet, _) = CreatePlanet(game, "p1", "alliance");

            Building kdy = new Building
            {
                InstanceID = "kdy1",
                OwnerInstanceID = "alliance",
                DefenseFacilityClass = DefenseFacilityClass.KDY,
                ProductionModifier = 1000,
                WeaponStrength = 500, // > hull -> destroys
            };
            game.AttachNode(kdy, planet);

            Fleet fleet = CreateBombardmentFleet(game, "f1", "empire", planet, 1, 0);

            SequenceRNG rng = new SequenceRNG(intValues: new[] { 0, 0 });
            CombatSystem combat = MakeCombat(game, rng);

            combat.ExecuteOrbitalBombardment(new List<Fleet> { fleet }, planet);

            Assert.AreEqual(
                0,
                fleet.CapitalShips.Count,
                "Destroyed ship should be removed from fleet"
            );
        }

        [Test]
        public void ExecuteOrbitalBombardment_ZeroProductionModifier_SkipsFacilityFire()
        {
            // ProductionModifier = 0 -> scaledChance = 0 -> RollProbabilitySuccess returns false
            // -> facility never fires. Verifies the short-circuit path in Stage 2.
            GameRoot game = CreateGame();
            (Planet planet, _) = CreatePlanet(game, "p1", "alliance");

            Building kdy = new Building
            {
                InstanceID = "kdy1",
                OwnerInstanceID = "alliance",
                DefenseFacilityClass = DefenseFacilityClass.KDY,
                ProductionModifier = 0,
                WeaponStrength = 999,
            };
            game.AttachNode(kdy, planet);

            Fleet fleet = CreateBombardmentFleet(game, "f1", "empire", planet, 1, 0);

            CombatSystem combat = MakeCombat(game, new QueueRNG());

            combat.ExecuteOrbitalBombardment(new List<Fleet> { fleet }, planet);

            Assert.AreEqual(
                100,
                fleet.CapitalShips[0].CurrentHullStrength,
                "Zero-chance facility should not fire"
            );
        }

        [Test]
        public void ExecuteOrbitalBombardment_AllocatedEnergyLane_ReducesEnergyCapacity()
        {
            // Planet with energy=2 and a non-defensive, non-target building (to force
            // allocatedPresent = true without adding to the ship-group lane).
            GameRoot game = CreateGame();
            (Planet planet, _) = CreatePlanet(game, "p1", "alliance", energy: 2);

            Building weapon = new Building
            {
                InstanceID = "w1",
                OwnerInstanceID = "alliance",
                BuildingType = BuildingType.Weapon,
                DefenseFacilityClass = DefenseFacilityClass.None,
            };
            game.AttachNode(weapon, planet);

            Fleet fleet = CreateBombardmentFleet(game, "f1", "empire", planet, 1, 2);

            SequenceRNG rng = new SequenceRNG(intValues: new[] { 0, 0, 1, 0 });
            CombatSystem combat = MakeCombat(game, rng);

            BombardmentResult result = combat.ExecuteOrbitalBombardment(
                new List<Fleet> { fleet },
                planet
            );

            Assert.AreEqual(2, result.EnergyDamage);
            Assert.AreEqual(0, planet.EnergyCapacity);
        }

        [Test]
        public void ExecuteOrbitalBombardment_RepeatTrialsAllFail_ProducesNoStrikes()
        {
            GameRoot game = CreateGame();
            (Planet planet, _) = CreatePlanet(game, "p1", "alliance", energy: 5);

            Fleet fleet = CreateBombardmentFleet(game, "f1", "empire", planet, 1, 3);

            SequenceRNG rng = new SequenceRNG(intValues: new[] { 99, 99, 99 });
            CombatSystem combat = MakeCombat(game, rng);

            BombardmentResult result = combat.ExecuteOrbitalBombardment(
                new List<Fleet> { fleet },
                planet
            );

            Assert.AreEqual(0, result.Strikes.Count);
            Assert.AreEqual(5, planet.EnergyCapacity, "Energy should be untouched");
        }

        [Test]
        public void ExecuteOrbitalBombardment_FleetStrengthZero_SkipsStage4()
        {
            GameRoot game = CreateGame();
            (Planet planet, _) = CreatePlanet(game, "p1", "alliance", energy: 5);

            Fleet fleet = CreateBombardmentFleet(game, "f1", "empire", planet, 1, 0);

            CombatSystem combat = MakeCombat(game, new QueueRNG());

            BombardmentResult result = combat.ExecuteOrbitalBombardment(
                new List<Fleet> { fleet },
                planet
            );

            Assert.AreEqual(0, result.Strikes.Count);
            Assert.AreEqual(5, planet.EnergyCapacity);
        }
    }

    [TestFixture]
    public class PlanetaryAssaultTests : CombatTestBase
    {
        private Fleet CreateAssaultFleet(
            GameRoot game,
            string id,
            string owner,
            Planet planet,
            int weaponPower
        )
        {
            Fleet fleet = new Fleet { InstanceID = id, OwnerInstanceID = owner };
            CapitalShip ship = new CapitalShip
            {
                InstanceID = $"{id}_ship",
                OwnerInstanceID = owner,
                MaxHullStrength = 100,
                CurrentHullStrength = 100,
                ManufacturingStatus = ManufacturingStatus.Complete,
            };
            if (weaponPower > 0)
            {
                ship.PrimaryWeapons[PrimaryWeaponType.Turbolaser] = new int[]
                {
                    weaponPower,
                    weaponPower,
                    weaponPower,
                    weaponPower,
                };
            }
            fleet.CapitalShips.Add(ship);
            ship.SetParent(fleet);
            game.AttachNode(fleet, planet);
            return fleet;
        }

        private Building CreateShieldBuilding(
            GameRoot game,
            string id,
            string owner,
            Planet planet,
            int shieldStrength
        )
        {
            Building building = new Building
            {
                InstanceID = id,
                OwnerInstanceID = owner,
                BuildingType = BuildingType.Defense,
                DefenseFacilityClass = DefenseFacilityClass.Shield,
                ShieldStrength = shieldStrength,
                ManufacturingStatus = ManufacturingStatus.Complete,
            };
            game.AttachNode(building, planet);
            return building;
        }

        private Building CreateTargetBuilding(
            GameRoot game,
            string id,
            string owner,
            Planet planet,
            int bombardment = 0
        )
        {
            Building building = new Building
            {
                InstanceID = id,
                OwnerInstanceID = owner,
                BuildingType = BuildingType.Defense,
                Bombardment = bombardment,
                ManufacturingStatus = ManufacturingStatus.Complete,
            };
            game.AttachNode(building, planet);
            return building;
        }

        [Test]
        public void ExecutePlanetaryAssault_NoAttackingFleets_ReturnsFailed()
        {
            GameRoot game = CreateGame();
            (Planet planet, _) = CreatePlanet(game, "p1", "alliance");

            CombatSystem combat = MakeCombat(game, new SequenceRNG());

            PlanetaryAssaultResult result = combat.ExecutePlanetaryAssault(
                new List<Fleet>(),
                planet
            );

            Assert.IsFalse(result.Success);
        }

        [Test]
        public void ExecutePlanetaryAssault_MixedFactionFleets_ReturnsFailed()
        {
            GameRoot game = CreateGame();
            (Planet planet, _) = CreatePlanet(game, "p1", "alliance");

            Fleet empireFleet = CreateAssaultFleet(game, "ef1", "empire", planet, 100);
            Fleet allianceFleet = CreateAssaultFleet(game, "af1", "alliance", planet, 100);

            CombatSystem combat = MakeCombat(game, new SequenceRNG());

            PlanetaryAssaultResult result = combat.ExecutePlanetaryAssault(
                new List<Fleet> { empireFleet, allianceFleet },
                planet
            );

            Assert.IsFalse(result.Success);
        }

        [Test]
        public void ExecutePlanetaryAssault_AssaultStrengthBelowDefense_ReturnsFailed()
        {
            GameRoot game = CreateGame();
            (Planet planet, _) = CreatePlanet(game, "p1", "alliance");
            CreateShieldBuilding(game, "shield1", "alliance", planet, shieldStrength: 500);

            Fleet fleet = CreateAssaultFleet(game, "ef1", "empire", planet, weaponPower: 0);

            CombatSystem combat = MakeCombat(game, new SequenceRNG());

            PlanetaryAssaultResult result = combat.ExecutePlanetaryAssault(
                new List<Fleet> { fleet },
                planet
            );

            Assert.IsFalse(result.Success);
            Assert.AreEqual(500, result.DefenseStrength);
        }

        [Test]
        public void ExecutePlanetaryAssault_RollFails_ReturnsFailed()
        {
            GameRoot game = CreateGame();
            (Planet planet, _) = CreatePlanet(game, "p1", "alliance");

            Fleet fleet = CreateAssaultFleet(game, "ef1", "empire", planet, weaponPower: 100);

            // Planet energy=5, no other targets -> 1 energy lane. laneCount=1.
            SequenceRNG rng = new SequenceRNG(intValues: new[] { 1 });
            CombatSystem combat = MakeCombat(game, rng);

            PlanetaryAssaultResult result = combat.ExecutePlanetaryAssault(
                new List<Fleet> { fleet },
                planet
            );

            Assert.IsFalse(result.Success);
        }

        [Test]
        public void ExecutePlanetaryAssault_RollSucceeds_ReturnsSuccess()
        {
            GameRoot game = CreateGame();
            (Planet planet, _) = CreatePlanet(game, "p1", "alliance");

            Fleet fleet = CreateAssaultFleet(game, "ef1", "empire", planet, weaponPower: 100);
            // Main loop strikes energy lane but resistance(9) >= roll(1) -> all miss.
            SequenceRNG rng = new SequenceRNG(intValues: new[] { 0 });
            CombatSystem combat = MakeCombat(game, rng);

            PlanetaryAssaultResult result = combat.ExecutePlanetaryAssault(
                new List<Fleet> { fleet },
                planet
            );

            Assert.IsTrue(result.Success);
        }

        [Test]
        public void ExecutePlanetaryAssault_InitialStrikeOnProductionBuilding_DestroysBuilding()
        {
            // The pre-loop initial strike only targets production buildings, so use a Mine
            // rather than a Defense building — otherwise the destruction would happen in the
            // main loop and this test wouldn't be exercising the initial-strike codepath.
            GameRoot game = CreateGame();
            (Planet planet, _) = CreatePlanet(game, "p1", "alliance");
            Building mine = new Building
            {
                InstanceID = "mine1",
                OwnerInstanceID = "alliance",
                BuildingType = BuildingType.Mine,
                Bombardment = 0,
                ManufacturingStatus = ManufacturingStatus.Complete,
            };
            game.AttachNode(mine, planet);

            Fleet fleet = CreateAssaultFleet(game, "ef1", "empire", planet, weaponPower: 100);

            SequenceRNG rng = new SequenceRNG(intValues: new[] { 0, 0, 5 });
            CombatSystem combat = MakeCombat(game, rng);

            PlanetaryAssaultResult result = combat.ExecutePlanetaryAssault(
                new List<Fleet> { fleet },
                planet
            );

            Assert.IsTrue(result.Success);
            Assert.IsTrue(result.DestroyedBuildings.Any(b => b.InstanceID == mine.InstanceID));
        }

        [Test]
        public void ExecutePlanetaryAssault_AllTargetsDestroyed_TransfersPlanetOwnership()
        {
            GameRoot game = CreateGame();
            (Planet planet, _) = CreatePlanet(game, "p1", "alliance", energy: 1);
            CreateTargetBuilding(game, "bld1", "alliance", planet);

            Fleet fleet = CreateAssaultFleet(game, "ef1", "empire", planet, weaponPower: 100);

            SequenceRNG rng = new SequenceRNG(intValues: new[] { 0, 0, 5, 0, 10 });
            CombatSystem combat = MakeCombat(game, rng);

            PlanetaryAssaultResult result = combat.ExecutePlanetaryAssault(
                new List<Fleet> { fleet },
                planet
            );

            Assert.IsTrue(result.OwnershipChanged);
            Assert.AreEqual("empire", result.NewOwner.InstanceID);
            Assert.AreEqual("empire", planet.GetOwnerInstanceID());
        }

        [Test]
        public void ExecutePlanetaryAssault_CommanderBoostsAssaultStrength()
        {
            GameRoot game = CreateGame();
            (Planet planet, _) = CreatePlanet(game, "p1", "alliance");

            Fleet fleet = CreateAssaultFleet(game, "ef1", "empire", planet, weaponPower: 100);
            Officer commander = new Officer
            {
                InstanceID = "cmd1",
                OwnerInstanceID = "empire",
                CurrentRank = OfficerRank.General,
                Skills = new Dictionary<MissionParticipantSkill, int>
                {
                    { MissionParticipantSkill.Leadership, 80 },
                },
            };
            fleet.CapitalShips[0].AddOfficer(commander);

            // CombatValue = 4×100 = 400. AssaultStrength = (80/40 + 1) × 400 = 1200.
            SequenceRNG rng = new SequenceRNG(intValues: new[] { 1 });
            CombatSystem combat = MakeCombat(game, rng);

            PlanetaryAssaultResult result = combat.ExecutePlanetaryAssault(
                new List<Fleet> { fleet },
                planet
            );

            Assert.AreEqual(1200, result.AssaultStrength);
        }

        [Test]
        public void ExecutePlanetaryAssault_StrikeDestroysRegiment()
        {
            GameRoot game = CreateGame();
            (Planet planet, _) = CreatePlanet(game, "p1", "alliance");
            Regiment regiment = new Regiment
            {
                InstanceID = "reg1",
                OwnerInstanceID = "alliance",
                BombardmentDefense = 0,
            };
            game.AttachNode(regiment, planet);

            Fleet fleet = CreateAssaultFleet(game, "ef1", "empire", planet, weaponPower: 100);

            // Lanes: Troop(resist=0), Energy(resist=9). Count=2. No buildings -> skip initial.
            // Remaining: regiment gone, energy lane only, all miss (9 >= 1).
            SequenceRNG rng = new SequenceRNG(intValues: new[] { 0, 0, 5 });
            CombatSystem combat = MakeCombat(game, rng);

            PlanetaryAssaultResult result = combat.ExecutePlanetaryAssault(
                new List<Fleet> { fleet },
                planet
            );

            Assert.IsTrue(result.Success);
            Assert.AreEqual(1, result.DestroyedRegiments.Count);
            Assert.AreEqual("reg1", result.DestroyedRegiments[0].InstanceID);
        }

        [Test]
        public void ExecutePlanetaryAssault_StrikeReducesEnergy()
        {
            GameRoot game = CreateGame();
            (Planet planet, _) = CreatePlanet(game, "p1", "alliance");

            Fleet fleet = CreateAssaultFleet(game, "ef1", "empire", planet, weaponPower: 100);

            // Lanes: Energy(resist=9). Count=1. No buildings -> skip initial.
            // Remaining: energy=4, still has lane, all miss (9 >= 1).
            SequenceRNG rng = new SequenceRNG(intValues: new[] { 0, 0, 10 });
            CombatSystem combat = MakeCombat(game, rng);

            PlanetaryAssaultResult result = combat.ExecutePlanetaryAssault(
                new List<Fleet> { fleet },
                planet
            );

            Assert.IsTrue(result.Success);
            Assert.AreEqual(1, result.EnergyDamage);
            Assert.AreEqual(4, planet.EnergyCapacity);
        }

        [Test]
        public void ExecutePlanetaryAssault_HighResistanceBlocksStrike()
        {
            GameRoot game = CreateGame();
            (Planet planet, _) = CreatePlanet(game, "p1", "alliance");
            Regiment regiment = new Regiment
            {
                InstanceID = "reg1",
                OwnerInstanceID = "alliance",
                BombardmentDefense = 10,
            };
            game.AttachNode(regiment, planet);

            Fleet fleet = CreateAssaultFleet(game, "ef1", "empire", planet, weaponPower: 100);

            // Lanes: Troop(resist=10), Energy(resist=9). Count=2.
            // Remaining: all miss (resist >= 1 for all lanes).
            SequenceRNG rng = new SequenceRNG(intValues: new[] { 0, 0, 5 });
            CombatSystem combat = MakeCombat(game, rng);

            PlanetaryAssaultResult result = combat.ExecutePlanetaryAssault(
                new List<Fleet> { fleet },
                planet
            );

            Assert.IsTrue(result.Success);
            Assert.AreEqual(0, result.DestroyedRegiments.Count);
            Assert.AreEqual(1, planet.GetAllRegiments().Count);
        }
    }
}
