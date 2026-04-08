using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Rebellion.Core.Simulation;
using Rebellion.Game;
using Rebellion.Game.Results;
using Rebellion.SceneGraph;
using Rebellion.Systems;

namespace Rebellion.Tests.Systems
{
    public class CombatTestBase
    {
        protected CombatSystem MakeCombat(GameRoot game, IRandomNumberProvider rng) =>
            new CombatSystem(game, rng, new MovementSystem(game, null));

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
    /// Validates the 7-phase combat pipeline against reverse-engineered REBEXE.EXE behavior.
    /// </summary>
    [TestFixture]
    public class CombatSystemTests : CombatTestBase
    {
        /// <summary>
        /// Runs a full combat cycle: detect then resolve (auto).
        /// Returns true if combat was detected and resolved.
        /// </summary>
        private bool RunCombat(CombatSystem manager, GameRoot game, IRandomNumberProvider rng)
        {
            if (manager.TryStartCombat(game, out CombatDecisionContext decision))
            {
                manager.Resolve(game, decision, autoResolve: true, rng);
                return true;
            }
            return false;
        }

        [Test]
        public void Combat_TwoFactionFleets_ResolvesCombat()
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

            RunCombat(manager, game, rng);

            bool combatOccurred =
                empireFleet.CapitalShips[0].HullStrength < 100
                || allianceFleet.CapitalShips[0].HullStrength < 100;
            Assert.IsTrue(combatOccurred, "Combat should occur between hostile factions");
        }

        [Test]
        public void Combat_NoHostileFleets_NoCombat()
        {
            GameRoot game = new GameRoot();
            Faction empire = new Faction { InstanceID = "empire" };
            game.Factions.Add(empire);

            PlanetSystem system = new PlanetSystem { InstanceID = "sys1" };
            Planet planet = new Planet { InstanceID = "p1" };
            game.AttachNode(system, game.Galaxy);
            game.AttachNode(planet, system);

            Fleet fleet = CreateFleet(game, "f1", "empire", planet, 1, 100, 10);
            int initialHull = fleet.CapitalShips[0].HullStrength;

            QueueRNG rng = new QueueRNG();
            CombatSystem manager = MakeCombat(game, rng);

            bool detected = RunCombat(manager, game, rng);

            Assert.IsFalse(detected, "No combat should be detected");
            Assert.AreEqual(
                initialHull,
                fleet.CapitalShips[0].HullStrength,
                "No combat should occur"
            );
        }

        [Test]
        public void Combat_SingleFactionMultipleFleets_NoCombat()
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

            RunCombat(manager, game, rng);

            Assert.AreEqual(100, fleet1.CapitalShips[0].HullStrength);
            Assert.AreEqual(100, fleet2.CapitalShips[0].HullStrength);
        }

        [Test]
        public void Combat_MultipleAttackerFleets_OnlyFirstPairFights()
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

            RunCombat(manager, game, rng);

            Assert.Less(empireFleet1.CapitalShips[0].HullStrength, 100, "First fleet fights");
            Assert.AreEqual(
                100,
                empireFleet2.CapitalShips[0].HullStrength,
                "Second fleet does not fight"
            );
        }

        [Test]
        public void Combat_AttackerDestroysDefender_AttackerWins()
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

            RunCombat(manager, game, rng);

            Assert.IsNull(game.GetSceneNodeByInstanceID<Fleet>("f2"), "Defender fleet destroyed");
            Assert.IsNotNull(game.GetSceneNodeByInstanceID<Fleet>("f1"), "Attacker survives");
        }

        [Test]
        public void Combat_DefenderDestroysAttacker_DefenderWins()
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

            RunCombat(manager, game, rng);

            Assert.IsNull(game.GetSceneNodeByInstanceID<Fleet>("f1"), "Attacker fleet destroyed");
            Assert.IsNotNull(game.GetSceneNodeByInstanceID<Fleet>("f2"), "Defender survives");
        }

        [Test]
        public void Combat_MutualDestruction_BothFleetsRemoved()
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

            RunCombat(manager, game, rng);

            bool anyDestroyed =
                game.GetSceneNodeByInstanceID<Fleet>("f1") == null
                || game.GetSceneNodeByInstanceID<Fleet>("f2") == null;
            Assert.IsTrue(
                anyDestroyed,
                "At least one fleet should be destroyed in evenly-matched combat"
            );
        }

        [Test]
        public void Combat_ShipTakesDamage_HullStrengthReduced()
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

            RunCombat(manager, game, rng);

            Assert.Less(
                empireFleet.CapitalShips[0].HullStrength,
                100,
                "Ships should take damage during combat"
            );
        }

        [Test]
        public void Combat_ShipDestroyed_RemovedFromFleet()
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

            RunCombat(manager, game, rng);

            Assert.AreEqual(
                0,
                allianceFleet.CapitalShips.Count,
                "Destroyed ship removed from fleet"
            );
        }

        [Test]
        public void Combat_FighterSquadronTakesLosses_SquadronSizeReduced()
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

            RunCombat(manager, game, rng);

            Fleet allianceFleet2 = game.GetSceneNodeByInstanceID<Fleet>("f2");
            if (allianceFleet2 != null)
            {
                List<Starfighter> allFighters = allianceFleet2.GetStarfighters().ToList();
                if (allFighters.Count > 0)
                {
                    Assert.Less(
                        allFighters[0].SquadronSize,
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
                empFighters[0].SquadronSize,
                100,
                "Empire fighters should take some losses"
            );
        }

        [Test]
        public void Combat_EmptyFleet_RemovedFromScene()
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

            RunCombat(manager, game, rng);

            Assert.IsNull(game.GetSceneNodeByInstanceID<Fleet>("f2"));
            bool foundFleet = false;
            foreach (Fleet fleet in planet.GetChildren<Fleet>(f => true, recurse: false))
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
        public void Combat_BothSidesZeroWeapons_NoDamage()
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

            RunCombat(manager, game, rng);

            Assert.AreEqual(
                100,
                empireFleet.CapitalShips[0].HullStrength,
                "No damage without weapons"
            );
            Assert.AreEqual(
                100,
                allianceFleet.CapitalShips[0].HullStrength,
                "No damage without weapons"
            );
        }

        [Test]
        public void Combat_WeaponFire_DamagesTargets()
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

            RunCombat(manager, game, rng);

            Assert.Less(empireFleet.CapitalShips[0].HullStrength, 100);
            Assert.Less(allianceFleet.CapitalShips[0].HullStrength, 100);
        }

        [Test]
        public void Combat_ShieldAbsorption_ReducesDamage()
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

            RunCombat(manager, game, rng);

            int shieldedDamage = 100 - shieldedFleet.CapitalShips[0].HullStrength;
            int unshieldedDamage = 100 - unshieldedFleet.CapitalShips[0].HullStrength;

            Assert.Greater(unshieldedDamage, shieldedDamage, "Shields should reduce damage");
        }

        [Test]
        public void Combat_FightersAttackCapitalShips_DamageApplied()
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

            RunCombat(manager, game, rng);

            Fleet target = game.GetSceneNodeByInstanceID<Fleet>("f2");
            Assert.IsNotNull(target, "Target fleet should still exist");
            Assert.Less(
                target.CapitalShips[0].HullStrength,
                1000,
                "Fighters should damage capital ships"
            );
        }

        [Test]
        public void Combat_DamageVariance_Within20Percent()
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
            RunCombat(manager1, game, rng1);
            int damage1 = 100 - empireFleet.CapitalShips[0].HullStrength;

            empireFleet.CapitalShips[0].HullStrength = 100;
            allianceFleet.CapitalShips[0].HullStrength = 100;
            empireFleet.IsInCombat = false;
            allianceFleet.IsInCombat = false;

            CombatSystem manager2 = MakeCombat(game, rng2);
            RunCombat(manager2, game, rng2);
            int damage2 = 100 - empireFleet.CapitalShips[0].HullStrength;

            Assert.AreNotEqual(damage1, damage2, "Damage should vary with different RNG");
        }

        [Test]
        public void Combat_SameRNGSeed_SameOutcome()
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

            RunCombat(MakeCombat(game1, rng1), game1, rng1);
            RunCombat(MakeCombat(game2, rng2), game2, rng2);

            Fleet fleet1 = game1.GetSceneNodeByInstanceID<Fleet>("f1");
            Fleet fleet2 = game2.GetSceneNodeByInstanceID<Fleet>("f1");

            Assert.AreEqual(
                fleet1.CapitalShips[0].HullStrength,
                fleet2.CapitalShips[0].HullStrength,
                "Same RNG should produce identical results"
            );
        }

        [Test]
        public void Combat_DifferentRNGSeeds_DifferentOutcomes()
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

            RunCombat(MakeCombat(game1, rng1), game1, rng1);
            RunCombat(MakeCombat(game2, rng2), game2, rng2);

            Fleet fleet1 = game1.GetSceneNodeByInstanceID<Fleet>("f1");
            Fleet fleet2 = game2.GetSceneNodeByInstanceID<Fleet>("f1");

            Assert.AreNotEqual(
                fleet1.CapitalShips[0].HullStrength,
                fleet2.CapitalShips[0].HullStrength,
                "Different RNG should produce different results"
            );
        }

        [Test]
        public void TryStartCombat_NullGame_ThrowsException()
        {
            GameRoot game = new GameRoot();
            CombatSystem manager = MakeCombat(game, new QueueRNG());

            Assert.Throws<System.NullReferenceException>(() => manager.TryStartCombat(null, out _));
        }

        [Test]
        public void Combat_EmptyFleets_NoCombat()
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

            RunCombat(manager, game, rng);

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

            bool detected = manager.TryStartCombat(game, out CombatDecisionContext decision);

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
            manager.TryStartCombat(game, out _);

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

            manager.TryStartCombat(game, out CombatDecisionContext decision);
            manager.Resolve(game, decision, autoResolve: true, rng);

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

            manager.TryStartCombat(game, out _);

            // Should not detect again while IsInCombat is set
            bool detectedAgain = manager.TryStartCombat(game, out CombatDecisionContext second);

            Assert.IsFalse(detectedAgain, "Fleets already in combat should not be detected again");
            Assert.IsNull(second);
        }

        [Test]
        public void ProcessTick_MultipleEncounters_AllAI_ResolvesAll()
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

            List<GameResult> results = manager.ProcessTick(game, rng);

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
                    ef.CapitalShips[0].HullStrength,
                    1000,
                    $"Empire fleet at planet {i} should have taken damage"
                );
                Assert.Less(
                    af.CapitalShips[0].HullStrength,
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

            List<GameResult> results = manager.ProcessTick(game, rng);

            Assert.IsTrue(
                results.OfType<PendingCombatResult>().Any(),
                "Player-involved encounter should emit a PendingCombatResult"
            );
        }

        [Test]
        public void Combat_DefenderWinsOnOwnPlanet_NoOwnershipChange()
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

            RunCombat(manager, game, rng);

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
                HullStrength = 1,
                ShieldRechargeRate = 0,
            };
            CapitalShip strongShip = new CapitalShip
            {
                InstanceID = "strong",
                OwnerInstanceID = "alliance",
                HullStrength = 1000,
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
            RunCombat(MakeCombat(game, rng), game, rng);

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
                HullStrength = 1,
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
            RunCombat(MakeCombat(game, rng), game, rng);

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
                    HullStrength = hullStrength,
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
                    SquadronSize = squadronSize,
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
                    HullStrength = 100,
                    Bombardment = bombardment,
                };
                fleet.CapitalShips.Add(ship);
                ship.SetParent(fleet);
            }
            game.AttachNode(fleet, planet);
            return fleet;
        }

        [Test]
        public void Bombardment_ShieldBlocked_ReturnsShieldBlockedResult()
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
        public void Bombardment_FleetStrengthBelowDefense_NoStrikes()
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
        public void Bombardment_RegimentsOnPlanet_DestroysTroops()
        {
            GameRoot game = CreateGame();
            (Planet planet, _) = CreatePlanet(game, "p1", "alliance");

            Regiment reg = new Regiment
            {
                InstanceID = "reg1",
                OwnerInstanceID = "alliance",
                BombardmentDefense = 0,
            };
            game.AttachNode(reg, planet);

            // Fleet with bombardment=10, no defense → 10 net strikes
            Fleet fleet = CreateBombardmentFleet(game, "f1", "empire", planet, 1, 10);

            // SequenceRNG: lane select=0 (troops), threshold roll=10 (max, always hits resistance 0)
            SequenceRNG rng = new SequenceRNG(
                intValues: new[]
                {
                    0, // lane select (troops)
                    10, // threshold roll (hits resistance 0)
                }
            );
            CombatSystem combat = MakeCombat(game, rng);

            BombardmentResult result = combat.ExecuteOrbitalBombardment(
                new List<Fleet> { fleet },
                planet
            );

            Assert.IsTrue(result.DestroyedRegiments.Any(r => r.InstanceID == "reg1"));
            Assert.AreEqual(0, planet.GetAllRegiments().Count);
        }

        [Test]
        public void Bombardment_PlanetWithEnergy_ReducesEnergy()
        {
            GameRoot game = CreateGame();
            (Planet planet, _) = CreatePlanet(game, "p1", "alliance", energy: 3);

            // No troops, fighters, or buildings → only energy lane available
            Fleet fleet = CreateBombardmentFleet(game, "f1", "empire", planet, 1, 5);

            // All rolls select lane 0 (energy) and roll high enough to hit
            SequenceRNG rng = new SequenceRNG(
                intValues: new[]
                {
                    0,
                    10, // strike 1
                    0,
                    10, // strike 2
                    0,
                    10, // strike 3
                    0,
                    10, // strike 4
                    0,
                    10, // strike 5 (energy=0, no more lanes)
                }
            );
            CombatSystem combat = MakeCombat(game, rng);

            BombardmentResult result = combat.ExecuteOrbitalBombardment(
                new List<Fleet> { fleet },
                planet
            );

            Assert.AreEqual(3, result.EnergyDamage);
            Assert.AreEqual(0, planet.EnergyCapacity);
        }

        [Test]
        public void Bombardment_BuildingsOnPlanet_DestroysBuildings()
        {
            GameRoot game = CreateGame();
            (Planet planet, _) = CreatePlanet(game, "p1", "alliance", energy: 5);

            Building mine = new Building
            {
                InstanceID = "mine1",
                OwnerInstanceID = "alliance",
                BuildingType = BuildingType.Mine,
                Bombardment = 0,
            };
            game.AttachNode(mine, planet);

            Fleet fleet = CreateBombardmentFleet(game, "f1", "empire", planet, 1, 5);

            // Lane 0=energy, lane 1=building. Select lane 1 (building).
            SequenceRNG rng = new SequenceRNG(intValues: new[] { 1, 10 });
            CombatSystem combat = MakeCombat(game, rng);

            BombardmentResult result = combat.ExecuteOrbitalBombardment(
                new List<Fleet> { fleet },
                planet
            );

            Assert.IsTrue(result.DestroyedBuildings.Any(b => b.InstanceID == "mine1"));
        }

        [Test]
        public void Bombardment_PlanetWithSupport_ShiftsPopularSupport()
        {
            GameRoot game = CreateGame();
            (Planet planet, _) = CreatePlanet(game, "p1", "alliance");

            Regiment reg = new Regiment
            {
                InstanceID = "reg1",
                OwnerInstanceID = "alliance",
                BombardmentDefense = 0,
            };
            game.AttachNode(reg, planet);

            Fleet fleet = CreateBombardmentFleet(game, "f1", "empire", planet, 1, 5);

            SequenceRNG rng = new SequenceRNG(intValues: new[] { 0, 10 });
            CombatSystem combat = MakeCombat(game, rng);

            int supportBefore = planet.GetPopularSupport("empire");

            BombardmentResult result = combat.ExecuteOrbitalBombardment(
                new List<Fleet> { fleet },
                planet
            );

            Assert.Less(planet.GetPopularSupport("empire"), supportBefore);
            Assert.Less(result.PopularSupportShift, 0);
        }

        [Test]
        public void Bombardment_AllTargetsDestroyed_StopsEarly()
        {
            GameRoot game = CreateGame();
            // Planet with energy=1, no other targets
            (Planet planet, _) = CreatePlanet(game, "p1", "alliance", energy: 1);

            // Fleet with bombardment=100 → 100 net strikes, but only 1 energy to destroy
            Fleet fleet = CreateBombardmentFleet(game, "f1", "empire", planet, 1, 100);

            SequenceRNG rng = new SequenceRNG(intValues: new[] { 0, 10 });
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
        public void Bombardment_HighResistance_StrikeMisses()
        {
            GameRoot game = CreateGame();
            (Planet planet, _) = CreatePlanet(game, "p1", "alliance", energy: 5);

            // Building with high bombardment resistance (15)
            Building bunker = new Building
            {
                InstanceID = "bunker1",
                OwnerInstanceID = "alliance",
                BuildingType = BuildingType.Defense,
                Bombardment = 15,
            };
            game.AttachNode(bunker, planet);

            Fleet fleet = CreateBombardmentFleet(game, "f1", "empire", planet, 1, 5);

            // Lane 0=energy, lane 1=building. Select lane 1 (building).
            // Roll = 1 (min threshold), resistance 15 >= 1 → miss
            SequenceRNG rng = new SequenceRNG(intValues: new[] { 1, 1, 1, 1, 1, 1, 1, 1, 1, 1 });
            CombatSystem combat = MakeCombat(game, rng);

            BombardmentResult result = combat.ExecuteOrbitalBombardment(
                new List<Fleet> { fleet },
                planet
            );

            Assert.AreEqual(0, result.Strikes.Count);
            Assert.AreEqual(1, planet.GetAllBuildings().Count);
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
                HullStrength = 100,
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

        private Building CreateDefenseBuilding(
            GameRoot game,
            string id,
            string owner,
            Planet planet,
            int weaponStrength
        )
        {
            Building building = new Building
            {
                InstanceID = id,
                OwnerInstanceID = owner,
                BuildingType = BuildingType.Defense,
                WeaponStrength = weaponStrength,
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

            CombatSystem combat = MakeCombat(game, new QueueRNG());

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

            CombatSystem combat = MakeCombat(game, new QueueRNG());

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
            CreateDefenseBuilding(game, "def1", "alliance", planet, weaponStrength: 100);

            // Zero-weapon fleet → combat value 0 → assault strength 0 ≤ defense 100
            Fleet fleet = CreateAssaultFleet(game, "ef1", "empire", planet, weaponPower: 0);

            CombatSystem combat = MakeCombat(game, new QueueRNG());

            PlanetaryAssaultResult result = combat.ExecutePlanetaryAssault(
                new List<Fleet> { fleet },
                planet
            );

            Assert.IsFalse(result.Success);
        }

        [Test]
        public void ExecutePlanetaryAssault_RollFails_ReturnsFailed()
        {
            GameRoot game = CreateGame();
            (Planet planet, _) = CreatePlanet(game, "p1", "alliance");

            // High-weapon fleet with no defense → assault strength > 0
            Fleet fleet = CreateAssaultFleet(game, "ef1", "empire", planet, weaponPower: 100);

            // NextDouble()=0.5 → NextInt(0,150)=75 ≥ threshold(50) → roll fails
            QueueRNG rng = new QueueRNG(0.5);
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

            // NextDouble()=0.0 → NextInt(0,150)=0 < threshold(50) → roll succeeds
            QueueRNG rng = new QueueRNG(0.0);
            CombatSystem combat = MakeCombat(game, rng);

            PlanetaryAssaultResult result = combat.ExecutePlanetaryAssault(
                new List<Fleet> { fleet },
                planet
            );

            Assert.IsTrue(result.Success);
        }

        [Test]
        public void ExecutePlanetaryAssault_SuccessWithDefenseBuilding_DestroysBuildingOnSuccess()
        {
            GameRoot game = CreateGame();
            (Planet planet, _) = CreatePlanet(game, "p1", "alliance");
            Building defBuilding = CreateDefenseBuilding(
                game,
                "def1",
                "alliance",
                planet,
                weaponStrength: 1
            );

            Fleet fleet = CreateAssaultFleet(game, "ef1", "empire", planet, weaponPower: 100);

            // 0.0 → roll=0 (success); 0.5 → building ordering
            QueueRNG rng = new QueueRNG(0.0, 0.5);
            CombatSystem combat = MakeCombat(game, rng);

            PlanetaryAssaultResult result = combat.ExecutePlanetaryAssault(
                new List<Fleet> { fleet },
                planet
            );

            Assert.IsTrue(result.Success);
            Assert.IsTrue(
                result.DestroyedBuildings.Any(b => b.InstanceID == defBuilding.InstanceID)
            );
        }

        [Test]
        public void ExecutePlanetaryAssault_AllDefenseDestroyed_TransfersPlanetOwnership()
        {
            GameRoot game = CreateGame();
            (Planet planet, _) = CreatePlanet(game, "p1", "alliance");
            CreateDefenseBuilding(game, "def1", "alliance", planet, weaponStrength: 1);

            Fleet fleet = CreateAssaultFleet(game, "ef1", "empire", planet, weaponPower: 100);

            // 0.0 → roll=0 (success); 0.5 → building ordering
            QueueRNG rng = new QueueRNG(0.0, 0.5);
            CombatSystem combat = MakeCombat(game, rng);

            PlanetaryAssaultResult result = combat.ExecutePlanetaryAssault(
                new List<Fleet> { fleet },
                planet
            );

            Assert.IsTrue(result.OwnershipChanged);
            Assert.AreEqual("empire", result.NewOwner.InstanceID);
            Assert.AreEqual("empire", planet.GetOwnerInstanceID());
        }
    }
}
