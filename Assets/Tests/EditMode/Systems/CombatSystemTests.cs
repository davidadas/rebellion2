using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Rebellion.Game;
using Rebellion.Game.Factions;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Missions;
using Rebellion.Game.Movement;
using Rebellion.Game.Results;
using Rebellion.Game.Units;
using Rebellion.Systems;
using Rebellion.Util.Common;

namespace Rebellion.Tests.Systems
{
    public class CombatTestBase
    {
        protected CombatSystem MakeCombat(GameRoot game, IRandomNumberProvider rng)
        {
            FogOfWarSystem fogOfWar = new FogOfWarSystem(game);
            MovementSystem movement = new MovementSystem(game, fogOfWar);
            ManufacturingSystem manufacturing = new ManufacturingSystem(game);
            PlanetaryControlSystem ownership = new PlanetaryControlSystem(
                game,
                movement,
                manufacturing,
                fogOfWar
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

        /// <summary>
        /// Random-number provider that throws from every roll.
        /// </summary>
        protected class ThrowingRNG : IRandomNumberProvider
        {
            /// <summary>
            /// Throws when a double roll is requested.
            /// </summary>
            /// <returns>This method always throws.</returns>
            public double NextDouble()
            {
                throw new InvalidOperationException("RNG failure");
            }

            /// <summary>
            /// Throws when an integer roll is requested.
            /// </summary>
            /// <param name="min">Minimum roll value.</param>
            /// <param name="max">Maximum roll value.</param>
            /// <returns>This method always throws.</returns>
            public int NextInt(int min, int max)
            {
                throw new InvalidOperationException("RNG failure");
            }
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
            return TryRunCombat(manager, out _);
        }

        private bool TryRunCombat(CombatSystem manager, out List<GameResult> results)
        {
            results = manager.ProcessTick();
            return results.Count > 0;
        }

        private bool TryResolveCombat(
            CombatSystem manager,
            Fleet attacker,
            Fleet defender,
            out List<GameResult> results
        )
        {
            Planet planet =
                attacker.GetParentOfType<Planet>() ?? defender.GetParentOfType<Planet>();
            results = manager.Resolve(
                new CombatDecisionContext
                {
                    AttackerFleetInstanceID = attacker.InstanceID,
                    DefenderFleetInstanceID = defender.InstanceID,
                    PlanetInstanceID = planet?.InstanceID,
                },
                true
            );
            return results.Count > 0;
        }

        private static bool HasDamageFor(List<GameResult> results, CapitalShip ship)
        {
            return GetDamageResults(results)
                .Any(result => result.GameObject == ship && result.DamageValue > 0);
        }

        private static int GetDamageFor(List<GameResult> results, CapitalShip ship)
        {
            GameObjectDamagedResult damageResult = GetDamageResults(results)
                .FirstOrDefault(result => result.GameObject == ship);

            return damageResult?.DamageValue ?? 0;
        }

        private static IEnumerable<GameObjectDamagedResult> GetDamageResults(
            List<GameResult> results
        )
        {
            return results
                .OfType<SpaceCombatResult>()
                .SelectMany(result => result.Events)
                .OfType<GameObjectDamagedResult>();
        }

        private static SpaceCombatResult GetCombatResult(List<GameResult> results)
        {
            return results.OfType<SpaceCombatResult>().Single();
        }

        private List<int> ResolveDamageValues(double randomValue)
        {
            GameRoot game = new GameRoot(TestConfig.Create());
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

            QueueRNG rng = new QueueRNG(
                randomValue,
                randomValue,
                randomValue,
                randomValue,
                randomValue,
                randomValue
            );
            TryRunCombat(MakeCombat(game, rng), out List<GameResult> results);

            List<int> damageValues = GetDamageResults(results)
                .Select(result => result.DamageValue)
                .ToList();

            CollectionAssert.IsNotEmpty(damageValues, "Combat should emit damage results.");
            return damageValues;
        }

        private bool HasOpposingReadyFleets(Planet planet)
        {
            return planet
                    .GetFleets()
                    .Where(fleet => fleet.Movement == null)
                    .Select(fleet => fleet.GetOwnerInstanceID())
                    .Where(ownerInstanceId => !string.IsNullOrEmpty(ownerInstanceId))
                    .Distinct()
                    .Count() > 1;
        }

        [Test]
        public void Resolve_TwoFactionFleets_RunsSpaceCombat()
        {
            GameRoot game = new GameRoot(TestConfig.Create());
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
            CapitalShip empireShip = empireFleet.CapitalShips[0];
            CapitalShip allianceShip = allianceFleet.CapitalShips[0];

            QueueRNG rng = new QueueRNG(0.5, 0.5, 0.5, 0.5);
            CombatSystem manager = MakeCombat(game, rng);

            TryResolveCombat(manager, empireFleet, allianceFleet, out List<GameResult> results);

            bool combatOccurred =
                HasDamageFor(results, empireShip) || HasDamageFor(results, allianceShip);
            SpaceCombatResult combatResult = GetCombatResult(results);
            Assert.IsTrue(combatOccurred, "Combat should occur between hostile factions");
            Assert.IsNotEmpty(combatResult.ShipDamage);
            foreach (ShipDamageResult damage in combatResult.ShipDamage)
            {
                GameObjectDamagedResult lastDamageEvent = combatResult
                    .Events.OfType<GameObjectDamagedResult>()
                    .Last(result => result.GameObject == damage.Ship);
                Assert.AreEqual(damage.HullBefore - damage.HullAfter, lastDamageEvent.DamageValue);
            }
            Assert.IsFalse(results.OfType<GameObjectDamagedResult>().Any());
        }

        [Test]
        public void Resolve_MultipleCombatRounds_ReturnsAggregateDamage()
        {
            GameRoot game = new GameRoot(TestConfig.Create());
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
                100,
                10,
                shieldRechargeRate: 0
            );
            Fleet allianceFleet = CreateFleet(
                game,
                "f2",
                "alliance",
                planet,
                1,
                100,
                10,
                shieldRechargeRate: 0
            );
            CombatSystem manager = MakeCombat(game, new QueueRNG(0.5, 0.5, 0.5, 0.5));

            TryResolveCombat(manager, empireFleet, allianceFleet, out List<GameResult> results);

            SpaceCombatResult combatResult = GetCombatResult(results);
            var repeatedDamage = combatResult
                .ShipDamage.Select(damage => new
                {
                    Damage = damage,
                    Events = combatResult
                        .Events.OfType<GameObjectDamagedResult>()
                        .Where(result => result.GameObject == damage.Ship)
                        .ToList(),
                })
                .FirstOrDefault(entry => entry.Events.Count > 1);

            Assert.IsNotNull(repeatedDamage);
            Assert.AreEqual(100, repeatedDamage.Damage.HullBefore);
            Assert.AreEqual(
                repeatedDamage.Damage.Ship.CurrentHullStrength,
                repeatedDamage.Damage.HullAfter
            );
            Assert.AreEqual(
                repeatedDamage.Damage.HullBefore - repeatedDamage.Damage.HullAfter,
                repeatedDamage.Events.Last().DamageValue
            );
        }

        [Test]
        public void Resolve_NoHostileFleets_DoesNotRunCombat()
        {
            GameRoot game = new GameRoot(TestConfig.Create());
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
        public void ProcessTick_WithInTransitFleet_IgnoresInTransitFleet()
        {
            GameRoot game = CreateGame();
            (Planet planet, _) = CreatePlanet(game, "p1", owner: "alliance");

            Fleet empireFleet = CreateFleet(game, "f1", "empire", planet, 1, 100, 10);
            Fleet allianceFleet = CreateFleet(game, "f2", "alliance", planet, 1, 100, 10);
            empireFleet.Movement = new MovementState
            {
                TransitTicks = 5,
                TicksElapsed = 1,
                OriginPosition = planet.GetPosition(),
                CurrentPosition = planet.GetPosition(),
            };

            CombatSystem manager = MakeCombat(game, new QueueRNG());

            List<GameResult> results = manager.ProcessTick();

            Assert.IsEmpty(results);
            Assert.IsFalse(empireFleet.IsInCombat);
            Assert.IsFalse(allianceFleet.IsInCombat);
        }

        [Test]
        public void ProcessTick_FleetsWithOnlyInTransitShips_DoesNotRunCombat()
        {
            GameRoot game = CreateGame();
            (Planet planet, _) = CreatePlanet(game, "p1", owner: "alliance");

            Fleet empireFleet = CreateFleet(game, "f1", "empire", planet, 1, 100, 10);
            Fleet allianceFleet = CreateFleet(game, "f2", "alliance", planet, 1, 100, 10);
            empireFleet.CapitalShips[0].Movement = new MovementState
            {
                TransitTicks = 5,
                TicksElapsed = 1,
                OriginPosition = planet.GetPosition(),
                CurrentPosition = planet.GetPosition(),
            };
            allianceFleet.CapitalShips[0].Movement = new MovementState
            {
                TransitTicks = 5,
                TicksElapsed = 1,
                OriginPosition = planet.GetPosition(),
                CurrentPosition = planet.GetPosition(),
            };

            CombatSystem manager = MakeCombat(game, new QueueRNG());

            List<GameResult> results = manager.ProcessTick();

            Assert.IsEmpty(results);
            Assert.IsFalse(empireFleet.IsInCombat);
            Assert.IsFalse(allianceFleet.IsInCombat);
        }

        [Test]
        public void Resolve_InTransitCapitalShipAttachedToFleet_DoesNotTakeCombatDamage()
        {
            GameRoot game = CreateGame();
            (Planet planet, _) = CreatePlanet(game, "p1", owner: "alliance");

            Fleet empireFleet = CreateFleet(game, "f1", "empire", planet, 1, 1, 0);
            CapitalShip inTransitShip = new CapitalShip
            {
                InstanceID = "f1_ship_moving",
                OwnerInstanceID = "empire",
                MaxHullStrength = 1000,
                CurrentHullStrength = 1000,
                ShieldRechargeRate = 0,
                ManufacturingStatus = ManufacturingStatus.Complete,
                Movement = new MovementState
                {
                    TransitTicks = 5,
                    TicksElapsed = 1,
                    OriginPosition = planet.GetPosition(),
                    CurrentPosition = planet.GetPosition(),
                },
            };
            game.AttachNode(inTransitShip, empireFleet);

            Fleet allianceFleet = CreateFleet(
                game,
                "f2",
                "alliance",
                planet,
                1,
                1000,
                100,
                shieldRechargeRate: 0
            );
            allianceFleet.CapitalShips[0].HasGravityWell = true;

            CombatSystem manager = MakeCombat(game, new QueueRNG(0.5, 0.5, 0.5, 0.5));

            TryResolveCombat(manager, empireFleet, allianceFleet, out _);

            Assert.AreEqual(1000, inTransitShip.CurrentHullStrength);
        }

        [Test]
        public void Resolve_InTransitStarfighterAttachedToFleet_DoesNotTakeCombatLosses()
        {
            GameRoot game = CreateGame();
            (Planet planet, _) = CreatePlanet(game, "p1", owner: "alliance");

            Fleet empireFleet = CreateFleet(game, "f1", "empire", planet, 1, 1, 0);
            CapitalShip empireCarrier = empireFleet.CapitalShips[0];
            empireCarrier.StarfighterCapacity = 1;
            Starfighter inTransitFighter = new Starfighter
            {
                InstanceID = "f1_fighter_moving",
                OwnerInstanceID = "empire",
                MaxSquadronSize = 12,
                CurrentSquadronSize = 12,
                LaserCannon = 100,
                ManufacturingStatus = ManufacturingStatus.Complete,
                Movement = new MovementState
                {
                    TransitTicks = 5,
                    TicksElapsed = 1,
                    OriginPosition = planet.GetPosition(),
                    CurrentPosition = planet.GetPosition(),
                },
            };
            game.AttachNode(inTransitFighter, empireCarrier);

            Fleet allianceFleet = CreateFleetWithFighters(
                game,
                "f2",
                "alliance",
                planet,
                1,
                1000,
                0,
                100
            );
            allianceFleet.CapitalShips[0].HasGravityWell = true;

            CombatSystem manager = MakeCombat(game, new QueueRNG(0.5, 0.5, 0.5, 0.5));

            TryResolveCombat(manager, empireFleet, allianceFleet, out _);

            Assert.AreEqual(12, inTransitFighter.CurrentSquadronSize);
        }

        [Test]
        public void Resolve_SingleFactionFleets_DoesNotRunCombat()
        {
            GameRoot game = new GameRoot(TestConfig.Create());
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
            GameRoot game = new GameRoot(TestConfig.Create());
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
            CapitalShip empireShip1 = empireFleet1.CapitalShips[0];
            CapitalShip empireShip2 = empireFleet2.CapitalShips[0];
            CapitalShip allianceShip = allianceFleet.CapitalShips[0];

            QueueRNG rng = new QueueRNG(0.5, 0.5, 0.5, 0.5);
            CombatSystem manager = MakeCombat(game, rng);

            TryResolveCombat(manager, empireFleet1, allianceFleet, out List<GameResult> results);

            bool firstPairFought =
                HasDamageFor(results, empireShip1) || HasDamageFor(results, allianceShip);
            Assert.IsTrue(firstPairFought, "First fleet fights");
            Assert.IsFalse(HasDamageFor(results, empireShip2), "Second fleet does not fight");
            Assert.AreEqual(
                100,
                empireFleet2.CapitalShips[0].CurrentHullStrength,
                "Second fleet does not fight"
            );
        }

        [Test]
        public void Resolve_AttackerDestroysDefender_ReturnsAttackerVictory()
        {
            GameRoot game = new GameRoot(TestConfig.Create());
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
            GameRoot game = new GameRoot(TestConfig.Create());
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
            GameRoot game = new GameRoot(TestConfig.Create());
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
            GameRoot game = new GameRoot(TestConfig.Create());
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
            CapitalShip empireShip = empireFleet.CapitalShips[0];

            QueueRNG rng = new QueueRNG(0.5, 0.5, 0.5, 0.5);
            CombatSystem manager = MakeCombat(game, rng);

            TryRunCombat(manager, out List<GameResult> results);

            Assert.IsTrue(
                HasDamageFor(results, empireShip),
                "Ships should take damage during combat"
            );
        }

        [Test]
        public void Resolve_ShipDestroyed_RemovedFromFleet()
        {
            GameRoot game = new GameRoot(TestConfig.Create());
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
            GameRoot game = new GameRoot(TestConfig.Create());
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
            GameRoot game = new GameRoot(TestConfig.Create());
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
        public void Resolve_BothSidesZeroWeapons_AppliesNoDamageAndSeparatesFleets()
        {
            GameRoot game = new GameRoot(TestConfig.Create());
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
            Assert.IsFalse(
                HasOpposingReadyFleets(planet),
                "Opposing fleets should not remain ready at the same planet"
            );
        }

        [Test]
        public void Resolve_WeaponFire_DamagesTargets()
        {
            GameRoot game = new GameRoot(TestConfig.Create());
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
            CapitalShip empireShip = empireFleet.CapitalShips[0];
            CapitalShip allianceShip = allianceFleet.CapitalShips[0];

            QueueRNG rng = new QueueRNG(0.5, 0.5, 0.5, 0.5);
            CombatSystem manager = MakeCombat(game, rng);

            TryRunCombat(manager, out List<GameResult> results);

            Assert.IsTrue(HasDamageFor(results, empireShip));
            Assert.IsTrue(HasDamageFor(results, allianceShip));
        }

        [Test]
        public void Resolve_ShieldAbsorption_ReducesDamage()
        {
            GameRoot game = new GameRoot(TestConfig.Create());
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
            CapitalShip shieldedShip = shieldedFleet.CapitalShips[0];
            CapitalShip unshieldedShip = unshieldedFleet.CapitalShips[0];

            shieldedFleet.CapitalShips[0].ShieldRechargeRate = 15;
            unshieldedFleet.CapitalShips[0].ShieldRechargeRate = 0;

            QueueRNG rng = new QueueRNG(0.5, 0.5, 0.5, 0.5);
            CombatSystem manager = MakeCombat(game, rng);

            TryRunCombat(manager, out List<GameResult> results);

            int shieldedDamage = GetDamageFor(results, shieldedShip);
            int unshieldedDamage = GetDamageFor(results, unshieldedShip);

            Assert.Greater(unshieldedDamage, shieldedDamage, "Shields should reduce damage");
        }

        [Test]
        public void Resolve_FightersAttackCapitalShips_AppliesDamage()
        {
            GameRoot game = new GameRoot(TestConfig.Create());
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
        public void Resolve_DifferentRNGValues_ProducesDifferentDamage()
        {
            int damage1 = ResolveDamageValues(0.0).First();
            int damage2 = ResolveDamageValues(1.0).First();

            Assert.AreNotEqual(damage1, damage2, "Damage should vary with different RNG");
        }

        [Test]
        public void Resolve_SameRNGSeed_ProducesSameOutcome()
        {
            CollectionAssert.AreEqual(
                ResolveDamageValues(0.5),
                ResolveDamageValues(0.5),
                "Same RNG should produce identical results"
            );
        }

        [Test]
        public void Resolve_DifferentRNGSeeds_ProduceDifferentOutcomes()
        {
            CollectionAssert.AreNotEqual(
                ResolveDamageValues(0.0),
                ResolveDamageValues(1.0),
                "Different RNG should produce different results"
            );
        }

        [Test]
        public void Resolve_EmptyFleets_DoesNotRunCombat()
        {
            GameRoot game = new GameRoot(TestConfig.Create());
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
        public void Resolve_CombatWithSurvivors_ClearsIsInCombatOnSurvivingFleets()
        {
            GameRoot game = new GameRoot(TestConfig.Create());
            Faction empire = new Faction { InstanceID = "empire" };
            Faction alliance = new Faction { InstanceID = "alliance" };
            game.Factions.Add(empire);
            game.Factions.Add(alliance);

            PlanetSystem system = new PlanetSystem { InstanceID = "sys1" };
            Planet planet = new Planet { InstanceID = "p1" };
            game.AttachNode(system, game.Galaxy);
            game.AttachNode(planet, system);

            Fleet empireFleet = CreateFleet(game, "f1", "empire", planet, 1, 10000, 1);
            Fleet allianceFleet = CreateFleet(game, "f2", "alliance", planet, 1, 10000, 1);

            QueueRNG rng = new QueueRNG(0.5, 0.5, 0.5, 0.5);
            CombatSystem manager = MakeCombat(game, rng);

            manager.ProcessTick();

            Fleet survivingEmpireFleet = game.GetSceneNodeByInstanceID<Fleet>("f1");
            Fleet survivingAllianceFleet = game.GetSceneNodeByInstanceID<Fleet>("f2");

            if (survivingEmpireFleet != null)
                Assert.IsFalse(
                    survivingEmpireFleet.IsInCombat,
                    "IsInCombat should be cleared after resolution"
                );
            if (survivingAllianceFleet != null)
                Assert.IsFalse(
                    survivingAllianceFleet.IsInCombat,
                    "IsInCombat should be cleared after resolution"
                );
        }

        [Test]
        public void ProcessTick_MultipleEncountersAllAI_ResolvesAll()
        {
            GameRoot game = new GameRoot(TestConfig.Create());
            Faction empire = new Faction { InstanceID = "empire" };
            Faction alliance = new Faction { InstanceID = "alliance" };
            game.Factions.Add(empire);
            game.Factions.Add(alliance);

            CreatePlanet(game, "empireHome", owner: "empire");
            CreatePlanet(game, "allianceHome", owner: "alliance");

            for (int i = 1; i <= 3; i++)
            {
                PlanetSystem sys = new PlanetSystem { InstanceID = $"sys{i}" };
                Planet planet = new Planet { InstanceID = $"p{i}" };
                game.AttachNode(sys, game.Galaxy);
                game.AttachNode(planet, sys);
                CreateFleet(game, $"ef{i}", "empire", planet, 1, 1000, 20);
                CreateFleet(game, $"af{i}", "alliance", planet, 1, 1000, 20);
            }

            QueueRNG rng = new QueueRNG(0.5, 0.5, 0.5, 0.5, 0.5, 0.5);
            CombatSystem manager = MakeCombat(game, rng);

            List<GameResult> results = manager.ProcessTick();

            Assert.IsFalse(
                results.OfType<PendingCombatResult>().Any(),
                "All AI encounters should auto-resolve with no pending decision"
            );

            for (int i = 1; i <= 3; i++)
            {
                Planet planet = game.GetSceneNodeByInstanceID<Planet>($"p{i}");
                Assert.IsFalse(HasHostileFleets(planet));
            }
        }

        [Test]
        public void ProcessTick_WeakerAIFleetCanRetreat_MovesToFriendlyPlanet()
        {
            GameRoot game = CreateGame();
            (Planet combatPlanet, _) = CreatePlanet(game, "combat");
            (Planet empireHome, _) = CreatePlanet(game, "empireHome", owner: "empire");
            CreatePlanet(game, "allianceHome", owner: "alliance");

            Fleet empireFleet = CreateFleet(game, "ef1", "empire", combatPlanet, 1, 100, 1);
            Fleet allianceFleet = CreateFleet(game, "af1", "alliance", combatPlanet, 1, 1000, 100);

            CombatSystem manager = MakeCombat(game, new QueueRNG());

            manager.ProcessTick();

            Assert.AreSame(empireHome, empireFleet.GetParentOfType<Planet>());
            Assert.IsNotNull(empireFleet.Movement);
            Assert.AreSame(combatPlanet, allianceFleet.GetParentOfType<Planet>());
            Assert.IsFalse(HasHostileFleets(combatPlanet));
        }

        [Test]
        public void ProcessTick_WeakerAIFleetBlockedByGravityWell_Fights()
        {
            GameRoot game = CreateGame();
            (Planet combatPlanet, _) = CreatePlanet(game, "combat");
            CreatePlanet(game, "empireHome", owner: "empire");
            CreatePlanet(game, "allianceHome", owner: "alliance");

            CreateFleet(game, "ef1", "empire", combatPlanet, 1, 1, 1, shieldRechargeRate: 0);
            Fleet allianceFleet = CreateFleet(
                game,
                "af1",
                "alliance",
                combatPlanet,
                1,
                1000,
                100,
                shieldRechargeRate: 0
            );
            allianceFleet.CapitalShips[0].HasGravityWell = true;

            CombatSystem manager = MakeCombat(game, new QueueRNG(0.5, 0.5, 0.5, 0.5));

            manager.ProcessTick();

            Assert.IsNull(game.GetSceneNodeByInstanceID<Fleet>("ef1"));
            Assert.AreSame(combatPlanet, allianceFleet.GetParentOfType<Planet>());
            Assert.IsFalse(HasHostileFleets(combatPlanet));
        }

        [Test]
        public void ProcessTick_UnarmedAIFleets_RetreatsBoth()
        {
            GameRoot game = CreateGame();
            (Planet combatPlanet, _) = CreatePlanet(game, "combat");
            (Planet empireHome, _) = CreatePlanet(game, "empireHome", owner: "empire");
            (Planet allianceHome, _) = CreatePlanet(game, "allianceHome", owner: "alliance");

            Fleet empireFleet = CreateFleet(game, "ef1", "empire", combatPlanet, 1, 100, 0);
            Fleet allianceFleet = CreateFleet(game, "af1", "alliance", combatPlanet, 1, 100, 0);
            empireFleet.CapitalShips[0].PrimaryWeapons.Clear();
            allianceFleet.CapitalShips[0].PrimaryWeapons.Clear();

            CombatSystem manager = MakeCombat(game, new QueueRNG());

            manager.ProcessTick();

            Assert.AreSame(empireHome, empireFleet.GetParentOfType<Planet>());
            Assert.AreSame(allianceHome, allianceFleet.GetParentOfType<Planet>());
            Assert.IsFalse(HasHostileFleets(combatPlanet));
        }

        [Test]
        public void ProcessTick_PlayerInvolvedEncounter_ReturnsPendingDecision()
        {
            GameRoot game = new GameRoot(TestConfig.Create());
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
            PendingCombatResult pending = results.OfType<PendingCombatResult>().SingleOrDefault();

            Assert.IsNotNull(
                pending,
                "Player-involved encounter should emit a PendingCombatResult"
            );
            Assert.AreSame(planet, pending.Planet);
            Assert.IsTrue(manager.HasPendingDecision);
            Assert.IsEmpty(manager.ProcessTick());

            List<GameResult> resolvedResults = manager.ResolvePendingCombat(autoResolve: true);

            Assert.IsFalse(manager.HasPendingDecision);
            Assert.IsNotEmpty(resolvedResults);
        }

        [Test]
        public void ProcessTick_PlayerInvolvedEncounter_SetsRetreatAvailability()
        {
            GameRoot game = new GameRoot(TestConfig.Create());
            Faction empire = new Faction { InstanceID = "empire", PlayerID = "player1" };
            Faction alliance = new Faction { InstanceID = "alliance" };
            game.Factions.Add(empire);
            game.Factions.Add(alliance);

            PlanetSystem sys = new PlanetSystem { InstanceID = "sys1" };
            Planet planet = new Planet { InstanceID = "p1" };
            game.AttachNode(sys, game.Galaxy);
            game.AttachNode(planet, sys);
            Fleet empireFleet = CreateFleet(game, "ef1", "empire", planet, 1, 1000, 10);
            Fleet allianceFleet = CreateFleet(game, "af1", "alliance", planet, 1, 1000, 10);
            allianceFleet.CapitalShips[0].HasGravityWell = true;

            CombatSystem manager = MakeCombat(game, new QueueRNG());

            PendingCombatResult pending = manager
                .ProcessTick()
                .OfType<PendingCombatResult>()
                .Single();

            bool empireCanRetreat = ReferenceEquals(pending.AttackerFleet, empireFleet)
                ? pending.AttackerCanRetreat
                : pending.DefenderCanRetreat;
            bool allianceCanRetreat = ReferenceEquals(pending.AttackerFleet, allianceFleet)
                ? pending.AttackerCanRetreat
                : pending.DefenderCanRetreat;

            Assert.IsFalse(empireCanRetreat);
            Assert.IsTrue(allianceCanRetreat);
        }

        [Test]
        public void ResolvePendingCombatRetreat_PlayerFleet_MovesToFriendlyPlanet()
        {
            GameRoot game = CreateGame();
            game.Factions.First(faction => faction.InstanceID == "empire").PlayerID = "player1";
            (Planet combatPlanet, _) = CreatePlanet(game, "combat");
            (Planet empireHome, _) = CreatePlanet(game, "empireHome", owner: "empire");
            empireHome.PositionX = 100;
            CreatePlanet(game, "allianceHome", owner: "alliance");

            Fleet empireFleet = CreateFleet(game, "ef1", "empire", combatPlanet, 1, 100, 1);
            Fleet allianceFleet = CreateFleet(game, "af1", "alliance", combatPlanet, 1, 1000, 100);
            CombatSystem manager = MakeCombat(game, new QueueRNG());

            manager.ProcessTick();
            List<GameResult> results = manager.ResolvePendingCombatRetreat("empire");

            Assert.IsNotNull(results);
            Assert.AreSame(empireHome, empireFleet.GetParentOfType<Planet>());
            Assert.IsNotNull(empireFleet.Movement);
            SpaceCombatResult combatResult = results.OfType<SpaceCombatResult>().Single();
            Assert.AreSame(combatPlanet, combatResult.Planet);
            bool empireWasAttacker = ReferenceEquals(combatResult.AttackerFleet, empireFleet);
            Assert.AreSame(
                allianceFleet,
                empireWasAttacker ? combatResult.DefenderFleet : combatResult.AttackerFleet
            );
            Assert.AreEqual(
                empireWasAttacker ? CombatSide.Defender : CombatSide.Attacker,
                combatResult.Winner
            );
            Assert.AreEqual(
                SpaceCombatSideOutcome.Withdrawn,
                empireWasAttacker ? combatResult.AttackerOutcome : combatResult.DefenderOutcome
            );
            Assert.AreEqual(
                SpaceCombatSideOutcome.Active,
                empireWasAttacker ? combatResult.DefenderOutcome : combatResult.AttackerOutcome
            );
        }

        [Test]
        public void ResolvePendingCombat_WhenResolveThrows_KeepsPendingDecision()
        {
            GameRoot game = new GameRoot(TestConfig.Create());
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

            CombatSystem manager = MakeCombat(game, new ThrowingRNG());

            manager.ProcessTick();

            Assert.Throws<InvalidOperationException>(() =>
                manager.ResolvePendingCombat(autoResolve: true)
            );
            Assert.IsTrue(manager.HasPendingDecision);
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
                ManufacturingStatus = ManufacturingStatus.Complete,
            };
            CapitalShip strongShip = new CapitalShip
            {
                InstanceID = "strong",
                OwnerInstanceID = "alliance",
                MaxHullStrength = 1000,
                CurrentHullStrength = 1000,
                ShieldRechargeRate = 0,
                ManufacturingStatus = ManufacturingStatus.Complete,
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
            TryResolveCombat(MakeCombat(game, rng), empireFleet, allianceFleet, out _);

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
            Planet alliancePlanet = new Planet
            {
                InstanceID = "p2",
                OwnerInstanceID = "alliance",
                IsColonized = true,
            };
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
                ManufacturingStatus = ManufacturingStatus.Complete,
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
            TryResolveCombat(MakeCombat(game, rng), empireFleet, allianceFleet, out _);

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
                    OwnerInstanceID = ownerId,
                    MaxHullStrength = hullStrength,
                    CurrentHullStrength = hullStrength,
                    ShieldRechargeRate = shieldRechargeRate,
                    ManufacturingStatus = ManufacturingStatus.Complete,
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

        private static bool HasHostileFleets(Planet planet)
        {
            List<string> owners = planet
                .GetFleets()
                .Where(fleet => fleet.Movement == null)
                .Select(fleet => fleet.GetOwnerInstanceID())
                .Where(owner => !string.IsNullOrEmpty(owner))
                .Distinct()
                .ToList();

            return owners.Count > 1;
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
                    OwnerInstanceID = ownerId,
                    MaxSquadronSize = squadronSize,
                    CurrentSquadronSize = squadronSize,
                    LaserCannon = 5,
                    IonCannon = 3,
                    Torpedoes = 2,
                    ManufacturingStatus = ManufacturingStatus.Complete,
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

            for (int i = 0; i < game.Config.Combat.BombardmentShieldBlockThreshold; i++)
            {
                Building shield = new Building
                {
                    InstanceID = $"shield{i}",
                    OwnerInstanceID = "alliance",
                    DefenseFacilityClass = DefenseFacilityClass.Shield,
                };
                game.AttachNode(shield, planet);
            }

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
        public void ExecuteOrbitalBombardment_DefenseFacilityFires_DestroysAttackerTroop()
        {
            GameRoot game = CreateGame();
            (Planet planet, _) = CreatePlanet(game, "p1", "alliance");

            Building kdy = new Building
            {
                InstanceID = "kdy1",
                OwnerInstanceID = "alliance",
                DefenseFacilityClass = DefenseFacilityClass.KDY,
                ProductionModifier = 1000,
            };
            game.AttachNode(kdy, planet);

            Fleet fleet = CreateBombardmentFleet(game, "f1", "empire", planet, 1, 0);
            CapitalShip ship = fleet.CapitalShips[0];
            ship.OwnerInstanceID = "empire";
            ship.RegimentCapacity = 4;
            Regiment troop = new Regiment
            {
                InstanceID = "atk-reg",
                OwnerInstanceID = "empire",
                ManufacturingStatus = ManufacturingStatus.Complete,
            };
            game.AttachNode(troop, ship);

            SequenceRNG rng = new SequenceRNG(intValues: new[] { 0, 0 });
            CombatSystem combat = MakeCombat(game, rng);

            BombardmentResult result = combat.ExecuteOrbitalBombardment(
                new List<Fleet> { fleet },
                planet
            );

            CollectionAssert.Contains(result.DestroyedRegiments, troop);
            CollectionAssert.IsEmpty(ship.Regiments);
        }

        [Test]
        public void ExecuteOrbitalBombardment_ZeroProductionModifier_SkipsFacilityFire()
        {
            GameRoot game = CreateGame();
            (Planet planet, _) = CreatePlanet(game, "p1", "alliance");

            Building kdy = new Building
            {
                InstanceID = "kdy1",
                OwnerInstanceID = "alliance",
                DefenseFacilityClass = DefenseFacilityClass.KDY,
                ProductionModifier = 0,
            };
            game.AttachNode(kdy, planet);

            Fleet fleet = CreateBombardmentFleet(game, "f1", "empire", planet, 1, 0);
            CapitalShip ship = fleet.CapitalShips[0];
            ship.OwnerInstanceID = "empire";
            ship.RegimentCapacity = 4;
            Regiment troop = new Regiment
            {
                InstanceID = "atk-reg",
                OwnerInstanceID = "empire",
                ManufacturingStatus = ManufacturingStatus.Complete,
            };
            game.AttachNode(troop, ship);

            CombatSystem combat = MakeCombat(game, new QueueRNG());
            BombardmentResult result = combat.ExecuteOrbitalBombardment(
                new List<Fleet> { fleet },
                planet
            );

            CollectionAssert.DoesNotContain(result.DestroyedRegiments, troop);
        }

        [Test]
        public void ExecuteOrbitalBombardment_AllocatedEnergyLane_ReducesAllocatedEnergy()
        {
            GameRoot game = CreateGame();
            (Planet planet, _) = CreatePlanet(game, "p1", "alliance", energy: 0);
            planet.AllocatedEnergy = 2;

            Fleet fleet = CreateBombardmentFleet(game, "f1", "empire", planet, 1, 2);

            SequenceRNG rng = new SequenceRNG(intValues: new[] { 0, 0, 1, 0 });
            CombatSystem combat = MakeCombat(game, rng);

            BombardmentResult result = combat.ExecuteOrbitalBombardment(
                new List<Fleet> { fleet },
                planet
            );

            Assert.AreEqual(2, result.EnergyDamage);
            Assert.AreEqual(0, planet.AllocatedEnergy);
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

        [Test]
        public void ExecuteOrbitalBombardment_ClearsIsInCombatOnAttackerAndDefenderFleets()
        {
            GameRoot game = CreateGame();
            (Planet planet, _) = CreatePlanet(game, "p1", "alliance", energy: 5);

            Fleet attacker = CreateBombardmentFleet(game, "atk", "empire", planet, 1, 1);
            attacker.IsInCombat = true;

            Fleet defender = new Fleet("alliance", "Defender");
            game.AttachNode(defender, planet);
            defender.IsInCombat = true;

            CombatSystem combat = MakeCombat(game, new QueueRNG());
            combat.ExecuteOrbitalBombardment(new List<Fleet> { attacker }, planet);

            Assert.IsFalse(attacker.IsInCombat, "Attacker fleet's combat lock should be released");
            Assert.IsFalse(defender.IsInCombat, "Defender fleet's combat lock should be released");
        }

        [Test]
        public void ExecuteOrbitalBombardment_GarrisonWipedAndAttackerHasTroops_ComputesGarrisonRequirement()
        {
            GameRoot game = CreateGame();
            (Planet planet, _) = CreatePlanet(game, "p1", "alliance", energy: 0);

            Fleet attacker = CreateBombardmentFleet(game, "atk", "empire", planet, 1, 0);
            CapitalShip ship = attacker.CapitalShips[0];
            ship.OwnerInstanceID = "empire";
            ship.RegimentCapacity = 4;
            Regiment troop = new Regiment
            {
                InstanceID = "atk-reg",
                OwnerInstanceID = "empire",
                ManufacturingStatus = ManufacturingStatus.Complete,
            };
            game.AttachNode(troop, ship);

            CombatSystem combat = MakeCombat(game, new QueueRNG());
            BombardmentResult result = combat.ExecuteOrbitalBombardment(
                new List<Fleet> { attacker },
                planet
            );

            Assert.Greater(
                result.GarrisonRequirement,
                0,
                "Stage 5 should compute a positive garrison requirement"
            );
        }

        [Test]
        public void ExecuteOrbitalBombardment_GarrisonStillPresent_SkipsGarrisonRequirement()
        {
            GameRoot game = CreateGame();
            (Planet planet, _) = CreatePlanet(game, "p1", "alliance", energy: 0);
            Regiment defender = new Regiment
            {
                InstanceID = "def-reg",
                OwnerInstanceID = "alliance",
                BombardmentDefense = 100,
            };
            game.AttachNode(defender, planet);

            Fleet attacker = CreateBombardmentFleet(game, "atk", "empire", planet, 1, 0);
            CapitalShip ship = attacker.CapitalShips[0];
            ship.OwnerInstanceID = "empire";
            ship.RegimentCapacity = 4;
            Regiment troop = new Regiment
            {
                InstanceID = "atk-reg",
                OwnerInstanceID = "empire",
                ManufacturingStatus = ManufacturingStatus.Complete,
            };
            game.AttachNode(troop, ship);

            CombatSystem combat = MakeCombat(game, new QueueRNG());
            BombardmentResult result = combat.ExecuteOrbitalBombardment(
                new List<Fleet> { attacker },
                planet
            );

            Assert.AreEqual(
                0,
                result.GarrisonRequirement,
                "Stage 5 must not compute a requirement while the garrison is still alive"
            );
        }

        [Test]
        public void ExecuteOrbitalBombardment_ShieldThresholdMinusOne_DoesNotBlock()
        {
            GameRoot game = CreateGame();
            (Planet planet, _) = CreatePlanet(game, "p1", "alliance", energy: 5);

            int oneBelow = game.Config.Combat.BombardmentShieldBlockThreshold - 1;
            for (int i = 0; i < oneBelow; i++)
            {
                Building shield = new Building
                {
                    InstanceID = $"shield{i}",
                    OwnerInstanceID = "alliance",
                    DefenseFacilityClass = DefenseFacilityClass.Shield,
                };
                game.AttachNode(shield, planet);
            }

            Fleet fleet = CreateBombardmentFleet(game, "f1", "empire", planet, 1, 5);
            CombatSystem combat = MakeCombat(game, new QueueRNG());

            BombardmentResult result = combat.ExecuteOrbitalBombardment(
                new List<Fleet> { fleet },
                planet
            );

            Assert.IsFalse(
                result.ShieldBlocked,
                "Shield count below threshold should not block bombardment"
            );
        }

        [Test]
        public void ExecuteOrbitalBombardment_GroundCombatAttackerWins_DestroysGarrisonRegiment()
        {
            GameRoot game = CreateGame();
            (Planet planet, _) = CreatePlanet(game, "p1", "alliance", energy: 0);

            Regiment defenderTroop = new Regiment
            {
                InstanceID = "def-reg",
                OwnerInstanceID = "alliance",
                AttackRating = 0,
                DefenseRating = 0,
            };
            game.AttachNode(defenderTroop, planet);

            Fleet attacker = CreateBombardmentFleet(game, "atk", "empire", planet, 1, 0);
            CapitalShip ship = attacker.CapitalShips[0];
            ship.OwnerInstanceID = "empire";
            ship.RegimentCapacity = 4;
            Regiment attackerTroop = new Regiment
            {
                InstanceID = "atk-reg",
                OwnerInstanceID = "empire",
                AttackRating = 20,
                ManufacturingStatus = ManufacturingStatus.Complete,
            };
            game.AttachNode(attackerTroop, ship);

            SequenceRNG rng = new SequenceRNG(intValues: new[] { 0, 0 });
            CombatSystem combat = MakeCombat(game, rng);

            BombardmentResult result = combat.ExecuteOrbitalBombardment(
                new List<Fleet> { attacker },
                planet
            );

            CollectionAssert.Contains(result.DestroyedRegiments, defenderTroop);
            CollectionAssert.DoesNotContain(planet.GetAllRegiments(), defenderTroop);
        }

        [Test]
        public void ExecuteOrbitalBombardment_GroundCombatDefenderWins_DestroysAttackerRegiment()
        {
            GameRoot game = CreateGame();
            (Planet planet, _) = CreatePlanet(game, "p1", "alliance", energy: 0);

            Regiment defenderTroop = new Regiment
            {
                InstanceID = "def-reg",
                OwnerInstanceID = "alliance",
                AttackRating = 0,
                DefenseRating = 20,
            };
            game.AttachNode(defenderTroop, planet);

            Fleet attacker = CreateBombardmentFleet(game, "atk", "empire", planet, 1, 0);
            CapitalShip ship = attacker.CapitalShips[0];
            ship.OwnerInstanceID = "empire";
            ship.RegimentCapacity = 4;
            Regiment attackerTroop = new Regiment
            {
                InstanceID = "atk-reg",
                OwnerInstanceID = "empire",
                AttackRating = 0,
                ManufacturingStatus = ManufacturingStatus.Complete,
            };
            game.AttachNode(attackerTroop, ship);

            SequenceRNG rng = new SequenceRNG(intValues: new[] { 0, 0 });
            CombatSystem combat = MakeCombat(game, rng);

            combat.ExecuteOrbitalBombardment(new List<Fleet> { attacker }, planet);

            Assert.IsEmpty(ship.Regiments, "Attacker regiment should have been destroyed");
            CollectionAssert.Contains(planet.GetAllRegiments(), defenderTroop);
        }

        [Test]
        public void ExecuteOrbitalBombardment_GroundCombatDraw_BothRegimentsSurvive()
        {
            GameRoot game = CreateGame();
            (Planet planet, _) = CreatePlanet(game, "p1", "alliance", energy: 0);

            Regiment defenderTroop = new Regiment
            {
                InstanceID = "def-reg",
                OwnerInstanceID = "alliance",
                DefenseRating = 5,
            };
            game.AttachNode(defenderTroop, planet);

            Fleet attacker = CreateBombardmentFleet(game, "atk", "empire", planet, 1, 0);
            CapitalShip ship = attacker.CapitalShips[0];
            ship.OwnerInstanceID = "empire";
            ship.RegimentCapacity = 4;
            Regiment attackerTroop = new Regiment
            {
                InstanceID = "atk-reg",
                OwnerInstanceID = "empire",
                AttackRating = 5,
                ManufacturingStatus = ManufacturingStatus.Complete,
            };
            game.AttachNode(attackerTroop, ship);

            SequenceRNG rng = new SequenceRNG(intValues: new[] { 0, 5 });
            CombatSystem combat = MakeCombat(game, rng);

            BombardmentResult result = combat.ExecuteOrbitalBombardment(
                new List<Fleet> { attacker },
                planet
            );

            CollectionAssert.IsEmpty(result.DestroyedRegiments);
            CollectionAssert.Contains(ship.Regiments, attackerTroop);
            CollectionAssert.Contains(planet.GetAllRegiments(), defenderTroop);
        }

        [Test]
        public void ExecuteOrbitalBombardment_NoGarrison_SkipsGroundCombat()
        {
            GameRoot game = CreateGame();
            (Planet planet, _) = CreatePlanet(game, "p1", "alliance", energy: 0);

            Fleet attacker = CreateBombardmentFleet(game, "atk", "empire", planet, 1, 0);
            CapitalShip ship = attacker.CapitalShips[0];
            ship.OwnerInstanceID = "empire";
            ship.RegimentCapacity = 4;
            Regiment attackerTroop = new Regiment
            {
                InstanceID = "atk-reg",
                OwnerInstanceID = "empire",
                AttackRating = 20,
                ManufacturingStatus = ManufacturingStatus.Complete,
            };
            game.AttachNode(attackerTroop, ship);

            CombatSystem combat = MakeCombat(game, new QueueRNG());
            BombardmentResult result = combat.ExecuteOrbitalBombardment(
                new List<Fleet> { attacker },
                planet
            );

            CollectionAssert.DoesNotContain(
                result.DestroyedRegiments,
                attackerTroop,
                "Stage 3 must not destroy the attacker when there is no garrison to duel"
            );
        }

        [Test]
        public void ExecuteOrbitalBombardment_GroundCombatWipesGarrison_TriggersStage5()
        {
            GameRoot game = CreateGame();
            (Planet planet, _) = CreatePlanet(game, "p1", "alliance", energy: 0);

            Regiment defenderTroop = new Regiment
            {
                InstanceID = "def-reg",
                OwnerInstanceID = "alliance",
                DefenseRating = 0,
            };
            game.AttachNode(defenderTroop, planet);

            Fleet attacker = CreateBombardmentFleet(game, "atk", "empire", planet, 1, 0);
            CapitalShip ship = attacker.CapitalShips[0];
            ship.OwnerInstanceID = "empire";
            ship.RegimentCapacity = 4;
            Regiment attackerTroop = new Regiment
            {
                InstanceID = "atk-reg",
                OwnerInstanceID = "empire",
                AttackRating = 20,
                ManufacturingStatus = ManufacturingStatus.Complete,
            };
            game.AttachNode(attackerTroop, ship);

            SequenceRNG rng = new SequenceRNG(intValues: new[] { 0, 0 });
            CombatSystem combat = MakeCombat(game, rng);

            BombardmentResult result = combat.ExecuteOrbitalBombardment(
                new List<Fleet> { attacker },
                planet
            );

            Assert.Greater(result.GarrisonRequirement, 0);
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
        public void ExecutePlanetaryAssault_RollSucceeds_ReturnsSuccess()
        {
            GameRoot game = CreateGame();
            (Planet planet, _) = CreatePlanet(game, "p1", "alliance");

            Fleet fleet = CreateAssaultFleet(game, "ef1", "empire", planet, weaponPower: 100);
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
            fleet.CapitalShips[0].RegimentCapacity = 1;
            game.AttachNode(
                new Regiment
                {
                    InstanceID = "atk-reg",
                    OwnerInstanceID = "empire",
                    ManufacturingStatus = ManufacturingStatus.Complete,
                },
                fleet.CapitalShips[0]
            );

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
                Ratings = new Dictionary<OfficerRating, int> { { OfficerRating.Leadership, 80 } },
            };
            game.AttachNode(commander, fleet.CapitalShips[0]);

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

            SequenceRNG rng = new SequenceRNG(intValues: new[] { 0, 10 });
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

        [Test]
        public void ExecutePlanetaryAssault_SuccessfulAssaultWithSurvivors_TransfersOwnership()
        {
            GameRoot game = CreateGame();
            (Planet planet, _) = CreatePlanet(game, "p1", "alliance", energy: 9);
            Regiment defender = new Regiment
            {
                InstanceID = "def-reg",
                OwnerInstanceID = "alliance",
                BombardmentDefense = 0,
            };
            game.AttachNode(defender, planet);

            Fleet fleet = CreateAssaultFleet(game, "ef1", "empire", planet, weaponPower: 100);
            fleet.CapitalShips[0].RegimentCapacity = 1;
            game.AttachNode(
                new Regiment
                {
                    InstanceID = "atk-reg",
                    OwnerInstanceID = "empire",
                    ManufacturingStatus = ManufacturingStatus.Complete,
                },
                fleet.CapitalShips[0]
            );
            SequenceRNG rng = new SequenceRNG(intValues: new[] { 0, 0, 5, 5, 5, 5, 5, 5 });
            CombatSystem combat = MakeCombat(game, rng);

            PlanetaryAssaultResult result = combat.ExecutePlanetaryAssault(
                new List<Fleet> { fleet },
                planet
            );

            Assert.IsTrue(result.Success);
            Assert.IsTrue(result.OwnershipChanged);
            Assert.AreEqual("empire", planet.GetOwnerInstanceID());
        }

        [Test]
        public void ExecutePlanetaryAssault_SuccessfulAssault_LandsOnlyRequiredRegimentsOnPlanet()
        {
            GameRoot game = CreateGame();
            (Planet planet, PlanetSystem system) = CreatePlanet(game, "p1", "alliance", energy: 9);
            system.SystemType = PlanetSystemType.OuterRim;
            planet.SetPopularSupport("empire", 50);

            Fleet fleet = CreateAssaultFleet(game, "ef1", "empire", planet, weaponPower: 100);
            CapitalShip ship = fleet.CapitalShips[0];
            ship.RegimentCapacity = 4;
            ship.StarfighterCapacity = 4;

            Regiment regiment = new Regiment
            {
                InstanceID = "atk-reg",
                OwnerInstanceID = "empire",
                ManufacturingStatus = ManufacturingStatus.Complete,
            };
            Officer officer = new Officer { InstanceID = "atk-off", OwnerInstanceID = "empire" };
            Starfighter starfighter = new Starfighter
            {
                InstanceID = "atk-sf",
                OwnerInstanceID = "empire",
            };
            game.AttachNode(regiment, ship);
            game.AttachNode(officer, ship);
            game.AttachNode(starfighter, ship);

            SequenceRNG rng = new SequenceRNG(intValues: new[] { 0, 0, 5, 5, 5, 5, 5, 5 });
            CombatSystem combat = MakeCombat(game, rng);

            PlanetaryAssaultResult result = combat.ExecutePlanetaryAssault(
                new List<Fleet> { fleet },
                planet
            );

            Assert.IsTrue(result.OwnershipChanged);
            CollectionAssert.Contains(planet.GetAllRegiments(), regiment);
            CollectionAssert.DoesNotContain(planet.GetAllOfficers(), officer);
            CollectionAssert.DoesNotContain(planet.GetAllStarfighters(), starfighter);
            Assert.IsEmpty(ship.Regiments);
            CollectionAssert.Contains(ship.Officers, officer);
            CollectionAssert.Contains(ship.Starfighters, starfighter);
        }

        [Test]
        public void ExecutePlanetaryAssault_SuccessfulAssaultWithInsufficientGarrison_LandsAvailableTroopsAndTransfersOwnership()
        {
            GameRoot game = CreateGame();
            (Planet planet, PlanetSystem system) = CreatePlanet(game, "p1", "alliance", energy: 9);
            system.SystemType = PlanetSystemType.OuterRim;
            planet.SetPopularSupport("empire", 50);

            Fleet fleet = CreateAssaultFleet(game, "ef1", "empire", planet, weaponPower: 100);
            fleet.CapitalShips[0].RegimentCapacity = 4;

            Regiment regiment = new Regiment
            {
                InstanceID = "atk-reg",
                OwnerInstanceID = "empire",
                ManufacturingStatus = ManufacturingStatus.Complete,
            };
            game.AttachNode(regiment, fleet.CapitalShips[0]);

            SequenceRNG rng = new SequenceRNG(intValues: new[] { 0, 0, 5, 5, 5, 5, 5, 5 });
            CombatSystem combat = MakeCombat(game, rng);

            PlanetaryAssaultResult result = combat.ExecutePlanetaryAssault(
                new List<Fleet> { fleet },
                planet
            );

            Assert.IsTrue(result.Success);
            Assert.IsTrue(result.OwnershipChanged);
            Assert.AreEqual("empire", planet.GetOwnerInstanceID());
            CollectionAssert.Contains(planet.GetAllRegiments(), regiment);
            Assert.IsEmpty(fleet.CapitalShips[0].Regiments);
        }

        [Test]
        public void ExecutePlanetaryAssault_SuccessfulOnUncolonizedPlanet_TransfersOwnership()
        {
            GameRoot game = CreateGame();
            (Planet planet, _) = CreatePlanet(game, "p1", "alliance", energy: 5);
            planet.IsColonized = false;
            Regiment defender = new Regiment
            {
                InstanceID = "def-reg",
                OwnerInstanceID = "alliance",
                BombardmentDefense = 0,
            };
            game.AttachNode(defender, planet);

            Fleet fleet = CreateAssaultFleet(game, "ef1", "empire", planet, weaponPower: 100);
            fleet.CapitalShips[0].RegimentCapacity = 1;
            game.AttachNode(
                new Regiment
                {
                    InstanceID = "atk-reg",
                    OwnerInstanceID = "empire",
                    ManufacturingStatus = ManufacturingStatus.Complete,
                },
                fleet.CapitalShips[0]
            );
            SequenceRNG rng = new SequenceRNG(intValues: new[] { 0, 0, 5, 5, 5, 5, 5, 5 });
            CombatSystem combat = MakeCombat(game, rng);

            PlanetaryAssaultResult result = combat.ExecutePlanetaryAssault(
                new List<Fleet> { fleet },
                planet
            );

            Assert.IsTrue(result.Success);
            Assert.IsTrue(result.OwnershipChanged);
            Assert.AreEqual("empire", planet.GetOwnerInstanceID());
        }
    }
}
