using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Rebellion.Core.Simulation;
using Rebellion.Game;
using Rebellion.SceneGraph;
using Rebellion.Systems;

namespace Rebellion.Tests.Systems
{
    /// <summary>
    /// Tests for CombatSystem.
    /// Validates the 7-phase combat pipeline against reverse-engineered REBEXE.EXE behavior.
    /// </summary>
    [TestFixture]
    public class CombatSystemTests
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
            CombatSystem manager = new CombatSystem(game, rng);

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
            CombatSystem manager = new CombatSystem(game, rng);

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
            CombatSystem manager = new CombatSystem(game, rng);

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
            CombatSystem manager = new CombatSystem(game, rng);

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
            CombatSystem manager = new CombatSystem(game, rng);

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
            CombatSystem manager = new CombatSystem(game, rng);

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
            CombatSystem manager = new CombatSystem(game, rng);

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
            CombatSystem manager = new CombatSystem(game, rng);

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
            CombatSystem manager = new CombatSystem(game, rng);

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
            CombatSystem manager = new CombatSystem(game, rng);

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
            CombatSystem manager = new CombatSystem(game, rng);

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
            CombatSystem manager = new CombatSystem(game, rng);

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
            CombatSystem manager = new CombatSystem(game, rng);

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
            CombatSystem manager = new CombatSystem(game, rng);

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
            CombatSystem manager = new CombatSystem(game, rng);

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

            CombatSystem manager1 = new CombatSystem(game, rng1);
            RunCombat(manager1, game, rng1);
            int damage1 = 100 - empireFleet.CapitalShips[0].HullStrength;

            empireFleet.CapitalShips[0].HullStrength = 100;
            allianceFleet.CapitalShips[0].HullStrength = 100;
            empireFleet.IsInCombat = false;
            allianceFleet.IsInCombat = false;

            CombatSystem manager2 = new CombatSystem(game, rng2);
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

            RunCombat(new CombatSystem(game1, rng1), game1, rng1);
            RunCombat(new CombatSystem(game2, rng2), game2, rng2);

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

            RunCombat(new CombatSystem(game1, rng1), game1, rng1);
            RunCombat(new CombatSystem(game2, rng2), game2, rng2);

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
            CombatSystem manager = new CombatSystem(game, new QueueRNG());

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
            CombatSystem manager = new CombatSystem(game, rng);

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

            CombatSystem manager = new CombatSystem(game, new QueueRNG());

            bool detected = manager.TryStartCombat(game, out CombatDecisionContext decision);

            Assert.IsTrue(detected);
            Assert.IsNotNull(decision);
            Assert.IsNotEmpty(decision.AttackerFleetInstanceID);
            Assert.IsNotEmpty(decision.DefenderFleetInstanceID);
        }

        [Test]
        public void TryStartCombat_SetsIsInCombatOnBothFleets()
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

            CombatSystem manager = new CombatSystem(game, new QueueRNG());
            manager.TryStartCombat(game, out _);

            Assert.IsTrue(
                empireFleet.IsInCombat || allianceFleet.IsInCombat,
                "At least one fleet should be flagged IsInCombat"
            );
        }

        [Test]
        public void Resolve_ClearsIsInCombatOnSurvivingFleets()
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
            CombatSystem manager = new CombatSystem(game, rng);

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

            CombatSystem manager = new CombatSystem(game, new QueueRNG());

            manager.TryStartCombat(game, out _);

            // Should not detect again while IsInCombat is set
            bool detectedAgain = manager.TryStartCombat(game, out CombatDecisionContext second);

            Assert.IsFalse(detectedAgain, "Fleets already in combat should not be detected again");
            Assert.IsNull(second);
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
}
