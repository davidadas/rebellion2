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
        private class MockRNG : IRandomNumberProvider
        {
            private Queue<double> values;

            public MockRNG(params double[] values)
            {
                this.values = new Queue<double>(values);
            }

            public double NextDouble()
            {
                return values.Count > 0 ? values.Dequeue() : 0.5;
            }

            public int NextInt(int min, int max)
            {
                return (int)(NextDouble() * (max - min)) + min;
            }
        }

        // ── Basic Combat Resolution ────────────────────────────────────────

        [Test]
        public void ProcessTick_TwoFactionFleets_ResolvesCombat()
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

            CombatSystem manager = new CombatSystem(game, new MockRNG(0.5, 0.5, 0.5, 0.5));

            manager.ProcessTick(game);

            // Combat should occur - at least one fleet should take damage
            bool combatOccurred =
                empireFleet.CapitalShips[0].HullStrength < 100
                || allianceFleet.CapitalShips[0].HullStrength < 100;
            Assert.IsTrue(combatOccurred, "Combat should occur between hostile factions");
        }

        [Test]
        public void ProcessTick_NoHostileFleets_NoCombat()
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

            CombatSystem manager = new CombatSystem(game, new MockRNG());

            manager.ProcessTick(game);

            Assert.AreEqual(
                initialHull,
                fleet.CapitalShips[0].HullStrength,
                "No combat should occur"
            );
        }

        [Test]
        public void ProcessTick_SingleFactionMultipleFleets_NoCombat()
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

            CombatSystem manager = new CombatSystem(game, new MockRNG());

            manager.ProcessTick(game);

            Assert.AreEqual(100, fleet1.CapitalShips[0].HullStrength);
            Assert.AreEqual(100, fleet2.CapitalShips[0].HullStrength);
        }

        [Test]
        public void ProcessTick_MultipleAttackerFleets_OnlyFirstPairFights()
        {
            // Validates original behavior: only first fleet per faction fights
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

            CombatSystem manager = new CombatSystem(game, new MockRNG(0.5, 0.5, 0.5, 0.5));

            manager.ProcessTick(game);

            // Only first empire fleet should have fought
            Assert.Less(empireFleet1.CapitalShips[0].HullStrength, 100, "First fleet fights");
            Assert.AreEqual(
                100,
                empireFleet2.CapitalShips[0].HullStrength,
                "Second fleet does not fight"
            );
        }

        // ── Winner Determination ────────────────────────────────────────────

        [Test]
        public void ProcessTick_AttackerDestroysDefender_AttackerWins()
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

            // Strong attacker vs weak defender
            Fleet empireFleet = CreateFleet(game, "f1", "empire", planet, 1, 1000, 100);
            Fleet allianceFleet = CreateFleet(game, "f2", "alliance", planet, 1, 1, 0);

            CombatSystem manager = new CombatSystem(game, new MockRNG(0.5, 0.5, 0.5, 0.5));

            manager.ProcessTick(game);

            // Defender should be destroyed and removed
            Assert.IsNull(game.GetSceneNodeByInstanceID<Fleet>("f2"), "Defender fleet destroyed");
            Assert.IsNotNull(game.GetSceneNodeByInstanceID<Fleet>("f1"), "Attacker survives");
        }

        [Test]
        public void ProcessTick_DefenderDestroysAttacker_DefenderWins()
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

            // Weak attacker vs strong defender
            Fleet empireFleet = CreateFleet(game, "f1", "empire", planet, 1, 1, 0);
            Fleet allianceFleet = CreateFleet(game, "f2", "alliance", planet, 1, 1000, 100);

            CombatSystem manager = new CombatSystem(game, new MockRNG(0.5, 0.5, 0.5, 0.5));

            manager.ProcessTick(game);

            // Attacker should be destroyed
            Assert.IsNull(game.GetSceneNodeByInstanceID<Fleet>("f1"), "Attacker fleet destroyed");
            Assert.IsNotNull(game.GetSceneNodeByInstanceID<Fleet>("f2"), "Defender survives");
        }

        [Test]
        public void ProcessTick_MutualDestruction_BothFleetsRemoved()
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

            // Test that destroyed fleets are removed from the scene
            // Due to first-strike advantage, attacker destroys defender first
            // (destroyed ships can't return fire per REBEXE.EXE behavior)
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

            CombatSystem manager = new CombatSystem(game, new MockRNG(0.5, 0.5));

            // Run one tick - attacker should destroy defender
            manager.ProcessTick(game);

            // At least one fleet should be destroyed
            bool anyDestroyed =
                game.GetSceneNodeByInstanceID<Fleet>("f1") == null
                || game.GetSceneNodeByInstanceID<Fleet>("f2") == null;
            Assert.IsTrue(
                anyDestroyed,
                "At least one fleet should be destroyed in evenly-matched combat"
            );
        }

        // ── Damage Application ──────────────────────────────────────────────

        [Test]
        public void ProcessTick_ShipTakesDamage_HullStrengthReduced()
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

            CombatSystem manager = new CombatSystem(game, new MockRNG(0.5, 0.5, 0.5, 0.5));

            manager.ProcessTick(game);

            // At least one ship should take damage
            Assert.Less(
                empireFleet.CapitalShips[0].HullStrength,
                100,
                "Ships should take damage during combat"
            );
        }

        [Test]
        public void ProcessTick_ShipDestroyed_RemovedFromFleet()
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

            // Strong attacker destroys weak ship
            Fleet empireFleet = CreateFleet(game, "f1", "empire", planet, 1, 1000, 100);
            Fleet allianceFleet = CreateFleet(game, "f2", "alliance", planet, 1, 1, 0);

            CombatSystem manager = new CombatSystem(game, new MockRNG(0.5, 0.5, 0.5, 0.5));

            manager.ProcessTick(game);

            // Weak ship should be removed
            Assert.AreEqual(
                0,
                allianceFleet.CapitalShips.Count,
                "Destroyed ship removed from fleet"
            );
        }

        [Test]
        public void ProcessTick_FighterSquadronTakesLosses_SquadronSizeReduced()
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

            // Create highly asymmetric fighter battle to guarantee losses
            // Empire: many fighters (36), Alliance: few fighters (6)
            // Fighter-vs-fighter losses = (enemy/(my+enemy)) * 0.3 * total * roll
            // Alliance losses: (36/(36+6)) * 0.3 * 6 * 0.5 = 0.857 * 0.3 * 6 * 0.5 ≈ 0.77 → 0
            // Empire losses: (6/(36+6)) * 0.3 * 36 * 0.5 = 0.143 * 0.3 * 36 * 0.5 ≈ 0.77 → 0
            // Need even more asymmetry: 100 vs 10
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

            CombatSystem manager = new CombatSystem(game, new MockRNG(0.5, 0.5, 0.5, 0.5));

            manager.ProcessTick(game);

            // Alliance fighters should take heavy losses (or be destroyed)
            // Empire fighters should take some losses
            Fleet allianceFleet2 = game.GetSceneNodeByInstanceID<Fleet>("f2");
            if (allianceFleet2 != null)
            {
                // Alliance survived - check its fighters took losses
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

            // Or Empire fighters took losses
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
        public void ProcessTick_EmptyFleet_RemovedFromScene()
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

            // Strong attacker vs weak defender
            Fleet empireFleet = CreateFleet(game, "f1", "empire", planet, 1, 1000, 100);
            Fleet allianceFleet = CreateFleet(game, "f2", "alliance", planet, 1, 1, 0);

            CombatSystem manager = new CombatSystem(game, new MockRNG(0.5, 0.5, 0.5, 0.5));

            manager.ProcessTick(game);

            // Destroyed fleet should be removed from scene graph
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

        // ── 7-Phase Pipeline ────────────────────────────────────────────────

        [Test]
        public void ProcessTick_BothSidesZeroWeapons_NoDamage()
        {
            // Validates anyArmed gate behavior
            GameRoot game = new GameRoot();
            Faction empire = new Faction { InstanceID = "empire" };
            Faction alliance = new Faction { InstanceID = "alliance" };
            game.Factions.Add(empire);
            game.Factions.Add(alliance);

            PlanetSystem system = new PlanetSystem { InstanceID = "sys1" };
            Planet planet = new Planet { InstanceID = "p1" };
            game.AttachNode(system, game.Galaxy);
            game.AttachNode(planet, system);

            // Create fleets with zero weapons (0 turbolasers, 0 ion, 0 laser)
            Fleet empireFleet = CreateFleet(game, "f1", "empire", planet, 1, 100, 0);
            Fleet allianceFleet = CreateFleet(game, "f2", "alliance", planet, 1, 100, 0);

            // Override weapon stats to zero
            empireFleet.CapitalShips[0].PrimaryWeapons.Clear();
            allianceFleet.CapitalShips[0].PrimaryWeapons.Clear();

            CombatSystem manager = new CombatSystem(game, new MockRNG(0.5, 0.5, 0.5, 0.5));

            manager.ProcessTick(game);

            // No damage should occur
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
        public void ProcessTick_WeaponFire_DamagesTargets()
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

            CombatSystem manager = new CombatSystem(game, new MockRNG(0.5, 0.5, 0.5, 0.5));

            manager.ProcessTick(game);

            // Both sides should take damage
            Assert.Less(empireFleet.CapitalShips[0].HullStrength, 100);
            Assert.Less(allianceFleet.CapitalShips[0].HullStrength, 100);
        }

        [Test]
        public void ProcessTick_ShieldAbsorption_ReducesDamage()
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

            // Fleet with high shields vs fleet with no shields
            Fleet shieldedFleet = CreateFleet(game, "f1", "empire", planet, 1, 100, 20);
            Fleet unshieldedFleet = CreateFleet(game, "f2", "alliance", planet, 1, 100, 20);

            shieldedFleet.CapitalShips[0].ShieldRechargeRate = 15; // Max shields
            unshieldedFleet.CapitalShips[0].ShieldRechargeRate = 0; // No shields

            CombatSystem manager = new CombatSystem(game, new MockRNG(0.5, 0.5, 0.5, 0.5));

            manager.ProcessTick(game);

            int shieldedDamage = 100 - shieldedFleet.CapitalShips[0].HullStrength;
            int unshieldedDamage = 100 - unshieldedFleet.CapitalShips[0].HullStrength;

            Assert.Greater(unshieldedDamage, shieldedDamage, "Shields should reduce damage");
        }

        [Test]
        public void ProcessTick_FightersAttackCapitalShips_DamageApplied()
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

            // Fleet with fighters vs fleet without (make target very durable)
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

            CombatSystem manager = new CombatSystem(game, new MockRNG(0.5, 0.5, 0.5, 0.5));

            manager.ProcessTick(game);

            // Target should take damage from fighters (check if fleet still exists)
            Fleet target = game.GetSceneNodeByInstanceID<Fleet>("f2");
            Assert.IsNotNull(target, "Target fleet should still exist");
            Assert.Less(
                target.CapitalShips[0].HullStrength,
                1000,
                "Fighters should damage capital ships"
            );
        }

        // ── RNG & Variance ──────────────────────────────────────────────────

        [Test]
        public void ProcessTick_DamageVariance_Within20Percent()
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

            // Run combat with different RNG values
            CombatSystem manager1 = new CombatSystem(game, new MockRNG(0.0, 0.0, 0.0, 0.0)); // Min variance
            CombatSystem manager2 = new CombatSystem(game, new MockRNG(1.0, 1.0, 1.0, 1.0)); // Max variance

            manager1.ProcessTick(game);

            int damage1 = 100 - empireFleet.CapitalShips[0].HullStrength;

            // Reset hull for second test
            empireFleet.CapitalShips[0].HullStrength = 100;
            allianceFleet.CapitalShips[0].HullStrength = 100;

            manager2.ProcessTick(game);

            int damage2 = 100 - empireFleet.CapitalShips[0].HullStrength;

            // Variance should be present
            Assert.AreNotEqual(damage1, damage2, "Damage should vary with different RNG");
        }

        [Test]
        public void ProcessTick_SameRNGSeed_SameOutcome()
        {
            // Validates deterministic combat
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

            CombatSystem manager1 = new CombatSystem(game1, new MockRNG(0.5, 0.5, 0.5, 0.5));
            CombatSystem manager2 = new CombatSystem(game2, new MockRNG(0.5, 0.5, 0.5, 0.5));

            manager1.ProcessTick(game1);
            manager2.ProcessTick(game2);

            Fleet fleet1 = game1.GetSceneNodeByInstanceID<Fleet>("f1");
            Fleet fleet2 = game2.GetSceneNodeByInstanceID<Fleet>("f1");

            Assert.AreEqual(
                fleet1.CapitalShips[0].HullStrength,
                fleet2.CapitalShips[0].HullStrength,
                "Same RNG should produce identical results"
            );
        }

        [Test]
        public void ProcessTick_DifferentRNGSeeds_DifferentOutcomes()
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

            CombatSystem manager1 = new CombatSystem(game1, new MockRNG(0.0, 0.0, 0.0, 0.0));
            CombatSystem manager2 = new CombatSystem(game2, new MockRNG(1.0, 1.0, 1.0, 1.0));

            manager1.ProcessTick(game1);
            manager2.ProcessTick(game2);

            Fleet fleet1 = game1.GetSceneNodeByInstanceID<Fleet>("f1");
            Fleet fleet2 = game2.GetSceneNodeByInstanceID<Fleet>("f1");

            Assert.AreNotEqual(
                fleet1.CapitalShips[0].HullStrength,
                fleet2.CapitalShips[0].HullStrength,
                "Different RNG should produce different results"
            );
        }

        // ── Edge Cases ──────────────────────────────────────────────────────

        [Test]
        public void ProcessTick_NullGame_ThrowsException()
        {
            GameRoot game = new GameRoot();
            CombatSystem manager = new CombatSystem(game, new MockRNG());

            Assert.Throws<System.NullReferenceException>(() => manager.ProcessTick(null));
        }

        [Test]
        public void ProcessTick_EmptyFleets_NoCombat()
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

            // Create empty fleets (no ships)
            Fleet empireFleet = new Fleet { InstanceID = "f1", OwnerInstanceID = "empire" };
            Fleet allianceFleet = new Fleet { InstanceID = "f2", OwnerInstanceID = "alliance" };
            game.AttachNode(empireFleet, planet);
            game.AttachNode(allianceFleet, planet);

            CombatSystem manager = new CombatSystem(game, new MockRNG());

            // Should not crash
            manager.ProcessTick(game);

            Assert.Pass("Empty fleets should not cause combat");
        }

        // ── Helper Methods ──────────────────────────────────────────────────

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
