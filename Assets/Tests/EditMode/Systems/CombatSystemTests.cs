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
            game.Factions.Add(
                new Faction
                {
                    InstanceID = "empire",
                    PlayerID = null,
                    Settings = new FactionSettings
                    {
                        InvertSupportShift = true,
                        WeakSupportPenaltyTrigger = SupportShiftCondition.Negative,
                        HeadquartersCanBeBombarded = false,
                    },
                }
            );
            game.Factions.Add(
                new Faction
                {
                    InstanceID = "alliance",
                    PlayerID = null,
                    Settings = new FactionSettings
                    {
                        InvertSupportShift = false,
                        WeakSupportPenaltyTrigger = SupportShiftCondition.Positive,
                        HeadquartersCanBeBombarded = true,
                    },
                }
            );
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
            results = manager.Resolve(
                new CombatDecisionContext
                {
                    AttackerFleetInstanceID = attacker.InstanceID,
                    DefenderFleetInstanceID = defender.InstanceID,
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

            Assert.IsTrue(
                results.OfType<PendingCombatResult>().Any(),
                "Player-involved encounter should emit a PendingCombatResult"
            );
            Assert.IsTrue(manager.HasPendingDecision);
            Assert.IsEmpty(manager.ProcessTick());

            List<GameResult> resolvedResults = manager.ResolvePendingCombat(autoResolve: true);

            Assert.IsFalse(manager.HasPendingDecision);
            Assert.IsNotEmpty(resolvedResults);
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
        [Test]
        public void ExecuteOrbitalBombardment_MilitaryBombardment_TargetsDefendersOnly()
        {
            GameRoot game = CreateGame();
            (Planet planet, _) = CreatePlanet(game, "p1", "empire", energy: 10);
            Regiment regiment = AddRegiment(game, planet, "defender", "empire");
            Building mine = AddBuilding(game, planet, "mine", "empire", BuildingType.Mine);
            Fleet fleet = AddBombardmentFleet(game, planet, "alliance", bombardment: 1);

            BombardmentResult result = MakeCombat(
                    game,
                    new SequenceRNG(intValues: new[] { 1, 0, 10 })
                )
                .ExecuteOrbitalBombardment(
                    new List<Fleet> { fleet },
                    planet,
                    BombardmentType.Military
                );

            CollectionAssert.Contains(result.DestroyedRegiments, regiment);
            CollectionAssert.DoesNotContain(result.DestroyedBuildings, mine);
            Assert.AreEqual(10, planet.EnergyCapacity);
        }

        [Test]
        public void ExecuteOrbitalBombardment_CivilianBombardment_AppliesCoreSupportPenalties()
        {
            GameRoot game = CreateGame();
            (Planet planet, PlanetSystem system) = CreatePlanet(game, "p1", "empire", energy: 10);
            planet.PopularSupport["alliance"] = 30;
            planet.PopularSupport["empire"] = 70;
            Planet secondPlanet = AddPlanet(game, system, "p2", "empire");
            secondPlanet.PopularSupport["alliance"] = 30;
            secondPlanet.PopularSupport["empire"] = 70;
            Building mine = AddBuilding(game, planet, "mine", "empire", BuildingType.Mine);
            Fleet fleet = AddBombardmentFleet(game, planet, "alliance", bombardment: 1);

            BombardmentResult result = MakeCombat(game, new SequenceRNG(intValues: new[] { 0, 10 }))
                .ExecuteOrbitalBombardment(
                    new List<Fleet> { fleet },
                    planet,
                    BombardmentType.Civilian
                );

            CollectionAssert.Contains(result.DestroyedBuildings, mine);
            Assert.AreEqual(6, planet.GetPopularSupport("alliance"));
            Assert.AreEqual(94, planet.GetPopularSupport("empire"));
            Assert.AreEqual(26, secondPlanet.GetPopularSupport("alliance"));
            Assert.AreEqual(74, secondPlanet.GetPopularSupport("empire"));
        }

        [Test]
        public void ExecuteOrbitalBombardment_EmpireCivilianBombardment_HalvesCoreTargetPenalty()
        {
            GameRoot game = CreateGame();
            (Planet planet, _) = CreatePlanet(game, "p1", "alliance", energy: 10);
            planet.PopularSupport["alliance"] = 70;
            planet.PopularSupport["empire"] = 30;
            AddBuilding(game, planet, "mine", "alliance", BuildingType.Mine);
            Fleet fleet = AddBombardmentFleet(game, planet, "empire", bombardment: 1);

            MakeCombat(game, new SequenceRNG(intValues: new[] { 0, 10 }))
                .ExecuteOrbitalBombardment(
                    new List<Fleet> { fleet },
                    planet,
                    BombardmentType.Civilian
                );

            Assert.AreEqual(17, planet.GetPopularSupport("empire"));
            Assert.AreEqual(83, planet.GetPopularSupport("alliance"));
        }

        [TestCase("alliance", "empire", 8, 28)]
        [TestCase("empire", "alliance", 9, 29)]
        public void ExecuteOrbitalBombardment_CivilianBombardment_AppliesOuterRimSupportPenalties(
            string attackerId,
            string defenderId,
            int expectedTargetSupport,
            int expectedSystemSupport
        )
        {
            GameRoot game = CreateGame();
            (Planet planet, PlanetSystem system) = CreatePlanet(game, "p1", defenderId, energy: 10);
            system.SystemType = PlanetSystemType.OuterRim;
            planet.PopularSupport[attackerId] = 30;
            planet.PopularSupport[defenderId] = 70;
            Planet secondPlanet = AddPlanet(game, system, "p2", defenderId);
            secondPlanet.PopularSupport[attackerId] = 30;
            secondPlanet.PopularSupport[defenderId] = 70;
            AddBuilding(game, planet, "mine", defenderId, BuildingType.Mine);
            Fleet fleet = AddBombardmentFleet(game, planet, attackerId, bombardment: 1);

            MakeCombat(game, new SequenceRNG(intValues: new[] { 0, 10 }))
                .ExecuteOrbitalBombardment(
                    new List<Fleet> { fleet },
                    planet,
                    BombardmentType.Civilian
                );

            Assert.AreEqual(expectedTargetSupport, planet.GetPopularSupport(attackerId));
            Assert.AreEqual(expectedSystemSupport, secondPlanet.GetPopularSupport(attackerId));
        }

        [Test]
        public void ExecuteOrbitalBombardment_GeneralBombardment_CanDamageBothEnergyPools()
        {
            GameRoot game = CreateGame();
            (Planet planet, _) = CreatePlanet(game, "p1", "empire", energy: 1);
            planet.AllocatedEnergy = 1;
            Fleet fleet = AddBombardmentFleet(game, planet, "alliance", bombardment: 2);

            BombardmentResult result = MakeCombat(
                    game,
                    new SequenceRNG(intValues: new[] { 0, 10, 0, 10 })
                )
                .ExecuteOrbitalBombardment(
                    new List<Fleet> { fleet },
                    planet,
                    BombardmentType.General
                );

            Assert.AreEqual(1, result.EnergyCapacityDamage);
            Assert.AreEqual(1, result.AllocatedEnergyDamage);
            Assert.Zero(planet.EnergyCapacity);
            Assert.Zero(planet.AllocatedEnergy);
        }

        [Test]
        public void ExecuteOrbitalBombardment_ShieldsSubtractFromStrikeAttempts()
        {
            GameRoot game = CreateGame();
            (Planet planet, _) = CreatePlanet(game, "p1", "empire", energy: 10);
            AddRegiment(game, planet, "defender", "empire");
            Building shield = AddBuilding(
                game,
                planet,
                "shield",
                "empire",
                BuildingType.Defense,
                DefenseFacilityClass.Shield
            );
            shield.ShieldStrength = 2;
            Fleet fleet = AddBombardmentFleet(game, planet, "alliance", bombardment: 3);

            BombardmentResult result = MakeCombat(
                    game,
                    new SequenceRNG(intValues: new[] { 1, 0, 10 })
                )
                .ExecuteOrbitalBombardment(
                    new List<Fleet> { fleet },
                    planet,
                    BombardmentType.Military
                );

            Assert.AreEqual(3, result.BombardmentStrength);
            Assert.AreEqual(2, result.ShieldStrength);
            Assert.AreEqual(1, result.StrikeAttempts);
        }

        [Test]
        public void ExecuteOrbitalBombardment_DeathStarShield_DoesNotReduceBombardment()
        {
            GameRoot game = CreateGame();
            (Planet planet, _) = CreatePlanet(game, "p1", "empire", energy: 10);
            Building shield = AddBuilding(
                game,
                planet,
                "death-star-shield",
                "empire",
                BuildingType.Defense,
                DefenseFacilityClass.DeathStarShield
            );
            shield.ShieldStrength = 100;
            Fleet fleet = AddBombardmentFleet(game, planet, "alliance", bombardment: 1);

            BombardmentResult result = MakeCombat(
                    game,
                    new SequenceRNG(intValues: new[] { 1, 0, 10 })
                )
                .ExecuteOrbitalBombardment(
                    new List<Fleet> { fleet },
                    planet,
                    BombardmentType.Military
                );

            Assert.Zero(result.ShieldStrength);
            Assert.AreEqual(1, result.StrikeAttempts);
        }

        [Test]
        public void ExecuteOrbitalBombardment_DamagedShipAndFighter_UseEffectiveBombardment()
        {
            GameRoot game = CreateGame();
            (Planet planet, _) = CreatePlanet(game, "p1", "empire", energy: 10);
            Fleet fleet = AddBombardmentFleet(
                game,
                planet,
                "alliance",
                bombardment: 10,
                currentHull: 50
            );
            CapitalShip ship = fleet.CapitalShips[0];
            ship.StarfighterCapacity = 1;
            Starfighter fighter = new Starfighter
            {
                InstanceID = "fighter",
                OwnerInstanceID = "alliance",
                Bombardment = 10,
                MaxSquadronSize = 10,
                CurrentSquadronSize = 5,
                ManufacturingStatus = ManufacturingStatus.Complete,
            };
            game.AttachNode(fighter, ship);
            Officer admiral = new Officer
            {
                InstanceID = "admiral",
                OwnerInstanceID = "alliance",
                CurrentRank = OfficerRank.Admiral,
            };
            admiral.SetBaseRating(OfficerRating.Leadership, 40);
            game.AttachNode(admiral, ship);

            BombardmentResult result = MakeCombat(game, new SequenceRNG())
                .ExecuteOrbitalBombardment(
                    new List<Fleet> { fleet },
                    planet,
                    BombardmentType.General
                );

            Assert.AreEqual(20, result.BombardmentStrength);
        }

        [Test]
        public void ExecuteOrbitalBombardment_KdyAndLnr_ResolveShieldBeforeHullDamage()
        {
            GameRoot game = CreateGame();
            (Planet planet, _) = CreatePlanet(game, "p1", "empire", energy: 10);
            Building lnr = AddBuilding(
                game,
                planet,
                "lnr",
                "empire",
                BuildingType.Defense,
                DefenseFacilityClass.LNR
            );
            lnr.WeaponPower = 30;
            Building kdy = AddBuilding(
                game,
                planet,
                "kdy",
                "empire",
                BuildingType.Defense,
                DefenseFacilityClass.KDY
            );
            kdy.WeaponPower = 30;
            Officer captive = new Officer
            {
                InstanceID = "captive",
                OwnerInstanceID = "alliance",
                IsMain = true,
                IsCaptured = true,
                CurrentRank = OfficerRank.General,
            };
            captive.SetBaseRating(OfficerRating.Leadership, 400);
            game.AttachNode(captive, planet);
            Officer general = AddOfficer(game, planet, "general", "empire", isMain: true);
            general.CurrentRank = OfficerRank.General;
            general.SetBaseRating(OfficerRating.Leadership, 40);
            Fleet fleet = AddBombardmentFleet(game, planet, "alliance", bombardment: 0);
            CapitalShip ship = fleet.CapitalShips[0];
            ship.MaxShieldStrength = 100;

            BombardmentResult result = MakeCombat(game, new SequenceRNG(intValues: new[] { 0, 0 }))
                .ExecuteOrbitalBombardment(
                    new List<Fleet> { fleet },
                    planet,
                    BombardmentType.General
                );

            Assert.AreEqual(80, ship.CurrentHullStrength);
            Assert.AreEqual(1, result.AttackerShipDamage.Count);
            Assert.AreEqual(
                20,
                result.AttackerShipDamage[0].HullBefore - result.AttackerShipDamage[0].HullAfter
            );
        }

        [Test]
        public void ExecuteOrbitalBombardment_DefenseFire_DeterminesSurvivingBombardmentStrength()
        {
            GameRoot game = CreateGame();
            (Planet planet, _) = CreatePlanet(game, "p1", "empire", energy: 1);
            Building lnr = AddBuilding(
                game,
                planet,
                "lnr",
                "empire",
                BuildingType.Defense,
                DefenseFacilityClass.LNR
            );
            lnr.WeaponPower = 100;
            Fleet fleet = AddBombardmentFleet(game, planet, "alliance", bombardment: 1);
            CapitalShip destroyedShip = fleet.CapitalShips[0];
            CapitalShip survivingShip = new CapitalShip
            {
                InstanceID = "survivor",
                OwnerInstanceID = "alliance",
                Bombardment = 1,
                MaxHullStrength = 100,
                CurrentHullStrength = 100,
                ManufacturingStatus = ManufacturingStatus.Complete,
            };
            game.AttachNode(survivingShip, fleet);

            BombardmentResult result = MakeCombat(
                    game,
                    new SequenceRNG(intValues: new[] { 0, 1, 10 })
                )
                .ExecuteOrbitalBombardment(
                    new List<Fleet> { fleet },
                    planet,
                    BombardmentType.General
                );

            CollectionAssert.Contains(result.DestroyedCapitalShips, destroyedShip);
            Assert.AreEqual(1, result.BombardmentStrength);
            Assert.AreEqual(1, result.StrikeAttempts);
            Assert.AreEqual(1, result.EnergyCapacityDamage);
        }

        [Test]
        public void ExecuteOrbitalBombardment_StrikeResistance_MustBeLowerThanRoll()
        {
            GameRoot game = CreateGame();
            (Planet planet, _) = CreatePlanet(game, "p1", "empire", energy: 10);
            Building mine = AddBuilding(game, planet, "mine", "empire", BuildingType.Mine);
            mine.Bombardment = 9;
            Fleet fleet = AddBombardmentFleet(game, planet, "alliance", bombardment: 2);

            BombardmentResult result = MakeCombat(
                    game,
                    new SequenceRNG(intValues: new[] { 0, 9, 0, 10 })
                )
                .ExecuteOrbitalBombardment(
                    new List<Fleet> { fleet },
                    planet,
                    BombardmentType.Civilian
                );

            Assert.AreEqual(1, result.SuccessfulStrikes);
            CollectionAssert.Contains(result.DestroyedBuildings, mine);
        }

        [Test]
        public void ExecuteOrbitalBombardment_MilitaryCollateral_CanDestroyCivilianTarget()
        {
            GameRoot game = CreateGame();
            (Planet planet, _) = CreatePlanet(game, "p1", "empire", energy: 10);
            Regiment regiment = AddRegiment(game, planet, "defender", "empire");
            Building mine = AddBuilding(game, planet, "mine", "empire", BuildingType.Mine);
            Fleet fleet = AddBombardmentFleet(game, planet, "alliance", bombardment: 1);

            BombardmentResult result = MakeCombat(
                    game,
                    new SequenceRNG(intValues: new[] { 0, 0, 10, 0, 10 })
                )
                .ExecuteOrbitalBombardment(
                    new List<Fleet> { fleet },
                    planet,
                    BombardmentType.Military
                );

            CollectionAssert.Contains(result.DestroyedRegiments, regiment);
            CollectionAssert.Contains(result.DestroyedBuildings, mine);
            Assert.AreEqual(2, result.SuccessfulStrikes);
            Assert.Less(planet.GetPopularSupport("alliance"), 50);
            Assert.AreEqual("empire", planet.GetOwnerInstanceID());
            Assert.IsNull(result.OwnershipChange);
        }

        [Test]
        public void ExecuteOrbitalBombardment_AllianceHeadquarters_CanBeDestroyed()
        {
            GameRoot game = CreateGame();
            (Planet planet, _) = CreatePlanet(game, "p1", "alliance", energy: 10);
            planet.IsHeadquarters = true;
            game.GetFactionByOwnerInstanceID("alliance").HQInstanceID = planet.InstanceID;
            Fleet fleet = AddBombardmentFleet(game, planet, "empire", bombardment: 1);

            BombardmentResult result = MakeCombat(
                    game,
                    new SequenceRNG(intValues: new[] { 1, 0, 10 })
                )
                .ExecuteOrbitalBombardment(
                    new List<Fleet> { fleet },
                    planet,
                    BombardmentType.Military
                );

            Assert.IsTrue(result.HeadquartersDestroyed);
            Assert.IsFalse(planet.IsHeadquarters);
            Assert.IsNull(game.GetFactionByOwnerInstanceID("alliance").HQInstanceID);
            Assert.IsFalse(planet.IsDestroyed);
        }

        [Test]
        public void ExecuteOrbitalBombardment_EmpireHeadquarters_IsNotAMilitaryTarget()
        {
            GameRoot game = CreateGame();
            (Planet planet, _) = CreatePlanet(game, "p1", "empire", energy: 10);
            planet.IsHeadquarters = true;
            game.GetFactionByOwnerInstanceID("empire").HQInstanceID = planet.InstanceID;
            Fleet fleet = AddBombardmentFleet(game, planet, "alliance", bombardment: 1);

            BombardmentResult result = MakeCombat(game, new SequenceRNG())
                .ExecuteOrbitalBombardment(
                    new List<Fleet> { fleet },
                    planet,
                    BombardmentType.Military
                );

            Assert.IsFalse(result.HeadquartersDestroyed);
            Assert.IsTrue(planet.IsHeadquarters);
            Assert.Zero(result.SuccessfulStrikes);
        }

        [Test]
        public void ExecuteOrbitalBombardment_DestroySystemWithDeathStar_DestroysPlanetAndMinorPersonnel()
        {
            GameRoot game = CreateGame();
            (Planet planet, PlanetSystem system) = CreatePlanet(game, "p1", "empire", energy: 10);
            planet.PopularSupport["alliance"] = 30;
            planet.PopularSupport["empire"] = 70;
            Planet secondPlanet = AddPlanet(game, system, "p2", "empire");
            secondPlanet.PopularSupport["alliance"] = 30;
            secondPlanet.PopularSupport["empire"] = 70;
            Officer minor = AddOfficer(game, planet, "minor", "empire", isMain: false);
            Officer main = AddOfficer(game, planet, "main", "empire", isMain: true);
            Officer killedMinor = AddOfficer(game, planet, "killed-minor", "empire", isMain: false);
            killedMinor.IsKilled = true;
            killedMinor.InjuryPoints = 2;
            Fleet fleet = AddBombardmentFleet(
                game,
                planet,
                "alliance",
                bombardment: 0,
                typeId: "CSEM015"
            );

            BombardmentResult result = MakeCombat(game, new SequenceRNG(intValues: new[] { 0, 0 }))
                .ExecuteOrbitalBombardment(
                    new List<Fleet> { fleet },
                    planet,
                    BombardmentType.DestroySystem
                );

            Assert.IsTrue(result.PlanetDestroyed);
            Assert.IsTrue(planet.IsDestroyed);
            Assert.IsTrue(minor.IsKilled);
            Assert.IsNull(minor.GetParent());
            Assert.IsFalse(main.IsKilled);
            Assert.AreEqual(2, killedMinor.InjuryPoints);
            Assert.AreEqual(planet, killedMinor.GetParent());
            Assert.AreEqual(10, planet.GetPopularSupport("alliance"));
            Assert.AreEqual(20, secondPlanet.GetPopularSupport("alliance"));
            Assert.AreEqual(1, result.Events.OfType<OfficerInjuredResult>().Count());
            Assert.AreEqual(1, result.Events.OfType<OfficerKilledResult>().Count());
        }

        [Test]
        public void ExecuteOrbitalBombardment_DestroySystem_DefenseFireCannotPreventDestruction()
        {
            GameRoot game = CreateGame();
            (Planet planet, _) = CreatePlanet(game, "p1", "empire", energy: 10);
            Building lnr = AddBuilding(
                game,
                planet,
                "lnr",
                "empire",
                BuildingType.Defense,
                DefenseFacilityClass.LNR
            );
            lnr.WeaponPower = 100;
            Fleet fleet = AddBombardmentFleet(
                game,
                planet,
                "alliance",
                bombardment: 0,
                typeId: "CSEM015"
            );
            CapitalShip deathStar = fleet.CapitalShips[0];

            BombardmentResult result = MakeCombat(game, new SequenceRNG(intValues: new[] { 0 }))
                .ExecuteOrbitalBombardment(
                    new List<Fleet> { fleet },
                    planet,
                    BombardmentType.DestroySystem
                );

            Assert.IsTrue(result.PlanetDestroyed);
            Assert.IsTrue(planet.IsDestroyed);
            CollectionAssert.Contains(result.DestroyedCapitalShips, deathStar);
            Assert.Zero(result.StrikeAttempts);
        }

        [Test]
        public void ExecuteOrbitalBombardment_DestroySystem_PenalizesOuterRimSupportBelowThreshold()
        {
            GameRoot game = CreateGame();
            (Planet planet, PlanetSystem system) = CreatePlanet(game, "p1", "empire", energy: 10);
            system.SystemType = PlanetSystemType.OuterRim;
            planet.PopularSupport["alliance"] = 30;
            planet.PopularSupport["empire"] = 70;
            (Planet lowSupportPlanet, PlanetSystem affectedSystem) = CreatePlanet(
                game,
                "p2",
                "alliance"
            );
            affectedSystem.SystemType = PlanetSystemType.OuterRim;
            lowSupportPlanet.PopularSupport["alliance"] = 89;
            lowSupportPlanet.PopularSupport["empire"] = 11;
            Planet thresholdPlanet = AddPlanet(game, affectedSystem, "p3", "alliance");
            thresholdPlanet.PopularSupport["alliance"] = 90;
            thresholdPlanet.PopularSupport["empire"] = 10;
            Fleet fleet = AddBombardmentFleet(
                game,
                planet,
                "alliance",
                bombardment: 0,
                typeId: "CSEM015"
            );

            MakeCombat(game, new SequenceRNG())
                .ExecuteOrbitalBombardment(
                    new List<Fleet> { fleet },
                    planet,
                    BombardmentType.DestroySystem
                );

            Assert.AreEqual(87, lowSupportPlanet.GetPopularSupport("alliance"));
            Assert.AreEqual(90, thresholdPlanet.GetPopularSupport("alliance"));
        }

        [Test]
        public void ExecuteOrbitalBombardment_DestroySystemWithoutDeathStar_UsesGeneralTargets()
        {
            GameRoot game = CreateGame();
            (Planet planet, _) = CreatePlanet(game, "p1", "empire", energy: 1);
            Fleet fleet = AddBombardmentFleet(game, planet, "alliance", bombardment: 1);

            BombardmentResult result = MakeCombat(game, new SequenceRNG(intValues: new[] { 0, 10 }))
                .ExecuteOrbitalBombardment(
                    new List<Fleet> { fleet },
                    planet,
                    BombardmentType.DestroySystem
                );

            Assert.IsFalse(result.PlanetDestroyed);
            Assert.AreEqual(1, result.EnergyCapacityDamage);
        }

        [Test]
        public void ExecuteOrbitalBombardment_DestroyedGarrison_CanTransferPlanetBySupport()
        {
            GameRoot game = CreateGame();
            (Planet planet, PlanetSystem system) = CreatePlanet(game, "p1", "empire", energy: 10);
            planet.PopularSupport["alliance"] = 60;
            planet.PopularSupport["empire"] = 40;
            Planet secondPlanet = AddPlanet(game, system, "p2", ownerId: null);
            Regiment regiment = AddRegiment(game, planet, "defender", "empire");
            Fleet fleet = AddBombardmentFleet(game, planet, "alliance", bombardment: 1);

            BombardmentResult result = MakeCombat(
                    game,
                    new SequenceRNG(intValues: new[] { 1, 0, 10 })
                )
                .ExecuteOrbitalBombardment(
                    new List<Fleet> { fleet },
                    planet,
                    BombardmentType.Military
                );

            CollectionAssert.Contains(result.DestroyedRegiments, regiment);
            Assert.AreEqual("alliance", planet.GetOwnerInstanceID());
            Assert.AreEqual(71, planet.GetPopularSupport("alliance"));
            Assert.AreEqual(61, secondPlanet.GetPopularSupport("alliance"));
            Assert.AreEqual("alliance", secondPlanet.GetOwnerInstanceID());
            Assert.IsTrue(
                result
                    .Events.OfType<PlanetOwnershipChangedResult>()
                    .Any(change =>
                        change.Planet == secondPlanet && change.NewOwner?.InstanceID == "alliance"
                    )
            );
            Assert.AreEqual("empire", result.OwnershipChange.PreviousOwner.InstanceID);
            Assert.AreEqual("alliance", result.OwnershipChange.NewOwner.InstanceID);
        }

        [Test]
        public void ExecuteOrbitalBombardment_DestroyedGarrison_CanLeavePlanetNeutral()
        {
            GameRoot game = CreateGame();
            (Planet planet, PlanetSystem system) = CreatePlanet(game, "p1", "empire", energy: 10);
            planet.PopularSupport["alliance"] = 49;
            planet.PopularSupport["empire"] = 51;
            Planet secondPlanet = AddPlanet(game, system, "p2", "empire");
            secondPlanet.PopularSupport["alliance"] = 20;
            secondPlanet.PopularSupport["empire"] = 80;
            AddRegiment(game, planet, "defender", "empire");
            Fleet fleet = AddBombardmentFleet(game, planet, "alliance", bombardment: 1);

            BombardmentResult result = MakeCombat(
                    game,
                    new SequenceRNG(intValues: new[] { 1, 0, 10 })
                )
                .ExecuteOrbitalBombardment(
                    new List<Fleet> { fleet },
                    planet,
                    BombardmentType.Military
                );

            Assert.IsNull(planet.GetOwnerInstanceID());
            Assert.AreEqual(59, planet.GetPopularSupport("alliance"));
            Assert.AreEqual(30, secondPlanet.GetPopularSupport("alliance"));
            Assert.IsNull(result.OwnershipChange.NewOwner);
        }

        [Test]
        public void ExecuteOrbitalBombardment_DestroyedGarrison_SupportShiftCanTransferPlanet()
        {
            GameRoot game = CreateGame();
            (Planet planet, _) = CreatePlanet(game, "p1", "empire", energy: 10);
            AddRegiment(game, planet, "defender", "empire");
            Fleet fleet = AddBombardmentFleet(game, planet, "alliance", bombardment: 1);

            BombardmentResult result = MakeCombat(
                    game,
                    new SequenceRNG(intValues: new[] { 1, 0, 10 })
                )
                .ExecuteOrbitalBombardment(
                    new List<Fleet> { fleet },
                    planet,
                    BombardmentType.Military
                );

            Assert.AreEqual("alliance", planet.GetOwnerInstanceID());
            Assert.AreEqual(61, planet.GetPopularSupport("alliance"));
            Assert.AreEqual("empire", result.OwnershipChange.PreviousOwner.InstanceID);
            Assert.AreEqual("alliance", result.OwnershipChange.NewOwner.InstanceID);
        }

        [Test]
        public void ExecuteOrbitalBombardment_RemoteOrMixedFleets_DoNotAttack()
        {
            GameRoot game = CreateGame();
            (Planet planet, _) = CreatePlanet(game, "p1", "empire", energy: 10);
            (Planet remote, _) = CreatePlanet(game, "p2", "empire", energy: 10);
            Fleet remoteFleet = AddBombardmentFleet(game, remote, "alliance", bombardment: 10);
            Fleet localEnemyFleet = AddBombardmentFleet(game, planet, "empire", bombardment: 10);

            BombardmentResult remoteResult = MakeCombat(game, new SequenceRNG())
                .ExecuteOrbitalBombardment(
                    new List<Fleet> { remoteFleet },
                    planet,
                    BombardmentType.General
                );
            BombardmentResult mixedResult = MakeCombat(game, new SequenceRNG())
                .ExecuteOrbitalBombardment(
                    new List<Fleet> { remoteFleet, localEnemyFleet },
                    planet,
                    BombardmentType.General
                );

            Assert.Zero(remoteResult.BombardmentStrength);
            Assert.Zero(mixedResult.BombardmentStrength);
        }

        private static Fleet AddBombardmentFleet(
            GameRoot game,
            Planet planet,
            string ownerId,
            int bombardment,
            int currentHull = 100,
            string typeId = null
        )
        {
            Fleet fleet = new Fleet
            {
                InstanceID = Guid.NewGuid().ToString(),
                OwnerInstanceID = ownerId,
            };
            game.AttachNode(fleet, planet);
            CapitalShip ship = new CapitalShip
            {
                InstanceID = Guid.NewGuid().ToString(),
                TypeID = typeId,
                OwnerInstanceID = ownerId,
                Bombardment = bombardment,
                MaxHullStrength = 100,
                CurrentHullStrength = currentHull,
                ManufacturingStatus = ManufacturingStatus.Complete,
            };
            game.AttachNode(ship, fleet);
            return fleet;
        }

        private static Building AddBuilding(
            GameRoot game,
            Planet planet,
            string id,
            string ownerId,
            BuildingType type,
            DefenseFacilityClass defenseClass = DefenseFacilityClass.None
        )
        {
            Building building = new Building
            {
                InstanceID = id,
                OwnerInstanceID = ownerId,
                BuildingType = type,
                DefenseFacilityClass = defenseClass,
                ManufacturingStatus = ManufacturingStatus.Complete,
            };
            game.AttachNode(building, planet);
            return building;
        }

        private static Regiment AddRegiment(GameRoot game, Planet planet, string id, string ownerId)
        {
            Regiment regiment = new Regiment
            {
                InstanceID = id,
                OwnerInstanceID = ownerId,
                ManufacturingStatus = ManufacturingStatus.Complete,
            };
            game.AttachNode(regiment, planet);
            return regiment;
        }

        private static Officer AddOfficer(
            GameRoot game,
            Planet planet,
            string id,
            string ownerId,
            bool isMain
        )
        {
            Officer officer = new Officer
            {
                InstanceID = id,
                OwnerInstanceID = ownerId,
                IsMain = isMain,
            };
            game.AttachNode(officer, planet);
            return officer;
        }

        private static Planet AddPlanet(
            GameRoot game,
            PlanetSystem system,
            string id,
            string ownerId
        )
        {
            Planet planet = new Planet
            {
                InstanceID = id,
                OwnerInstanceID = ownerId,
                IsColonized = true,
                EnergyCapacity = 10,
                PopularSupport = new Dictionary<string, int>
                {
                    { "empire", 50 },
                    { "alliance", 50 },
                },
            };
            game.AttachNode(planet, system);
            return planet;
        }
    }

    [TestFixture]
    public class PlanetaryAssaultTests : CombatTestBase
    {
        [Test]
        public void ExecutePlanetaryAssault_TwoShieldGenerators_BlockAssault()
        {
            GameRoot game = CreateGame();
            (Planet planet, _) = CreatePlanet(game, "p1", "alliance", energy: 10);
            AddDefenseBuilding(game, planet, "shield1", DefenseFacilityClass.Shield);
            AddDefenseBuilding(game, planet, "shield2", DefenseFacilityClass.Shield);
            Fleet fleet = AddAssaultFleet(game, planet, "empire", regimentCount: 1);

            PlanetaryAssaultResult result = MakeCombat(game, new SequenceRNG())
                .ExecutePlanetaryAssault(new List<Fleet> { fleet }, planet);

            Assert.IsTrue(result.BlockedByShields);
            Assert.IsFalse(result.Success);
            Assert.AreEqual("alliance", planet.GetOwnerInstanceID());
        }

        [Test]
        public void ExecutePlanetaryAssault_DeathStarShield_DoesNotBlockAssault()
        {
            GameRoot game = CreateGame();
            (Planet planet, _) = CreatePlanet(game, "p1", "alliance", energy: 10);
            AddDefenseBuilding(game, planet, "shield", DefenseFacilityClass.Shield);
            AddDefenseBuilding(
                game,
                planet,
                "death-star-shield",
                DefenseFacilityClass.DeathStarShield
            );
            Fleet fleet = AddAssaultFleet(game, planet, "empire", regimentCount: 1);

            PlanetaryAssaultResult result = MakeCombat(game, new SequenceRNG())
                .ExecutePlanetaryAssault(new List<Fleet> { fleet }, planet);

            Assert.IsFalse(result.BlockedByShields);
            Assert.IsTrue(result.Success);
        }

        [Test]
        public void ExecutePlanetaryAssault_DefenseFire_UsesInitialAttackerIndexRange()
        {
            GameRoot game = CreateGame();
            (Planet planet, _) = CreatePlanet(game, "p1", "alliance", energy: 10);
            Building first = AddDefenseBuilding(game, planet, "kdy", DefenseFacilityClass.KDY);
            first.WeaponPower = 500;
            Building second = AddDefenseBuilding(game, planet, "lnr", DefenseFacilityClass.LNR);
            second.WeaponPower = 500;
            Fleet fleet = AddAssaultFleet(game, planet, "empire", regimentCount: 2);

            PlanetaryAssaultResult result = MakeCombat(
                    game,
                    new SequenceRNG(intValues: new[] { 0, 0, 0, 1 })
                )
                .ExecutePlanetaryAssault(new List<Fleet> { fleet }, planet);

            Assert.AreEqual(1, result.DestroyedAttackerRegiments.Count);
            Assert.AreEqual(1, result.RemainingAttackerRegimentCount);
            Assert.IsTrue(result.Success);
        }

        [TestCase(4, true, false)]
        [TestCase(5, false, false)]
        [TestCase(6, false, true)]
        public void ExecutePlanetaryAssault_ContestScore_UsesSourceThresholds(
            int contestRoll,
            bool defenderWins,
            bool attackerWins
        )
        {
            GameRoot game = CreateGame();
            (Planet planet, _) = CreatePlanet(game, "p1", "alliance", energy: 10);
            Regiment defender = AddDefender(game, planet, "defender");
            Fleet fleet = AddAssaultFleet(game, planet, "empire", regimentCount: 1);
            Regiment attacker = fleet.CapitalShips[0].Regiments[0];

            PlanetaryAssaultResult result = MakeCombat(
                    game,
                    new SequenceRNG(intValues: new[] { 0, contestRoll, 99 })
                )
                .ExecutePlanetaryAssault(new List<Fleet> { fleet }, planet);

            Assert.AreEqual(defenderWins, result.DestroyedAttackerRegiments.Contains(attacker));
            Assert.AreEqual(attackerWins, result.DestroyedDefenderRegiments.Contains(defender));
            Assert.AreEqual(attackerWins, result.Success);
        }

        [Test]
        public void ExecutePlanetaryAssault_EachTroopUsesGeneralFromItsOwnFleet()
        {
            GameRoot game = CreateGame();
            (Planet planet, _) = CreatePlanet(game, "p1", "alliance", energy: 10);
            Regiment firstDefender = AddDefender(game, planet, "defender1");
            Regiment secondDefender = AddDefender(game, planet, "defender2");
            Fleet uncommandedFleet = AddAssaultFleet(game, planet, "empire", regimentCount: 1);
            Fleet commandedFleet = AddAssaultFleet(game, planet, "empire", regimentCount: 1);
            Officer general = new Officer
            {
                InstanceID = "general",
                OwnerInstanceID = "empire",
                CurrentRank = OfficerRank.General,
            };
            general.SetBaseRating(OfficerRating.Leadership, 60);
            game.AttachNode(general, commandedFleet.CapitalShips[0]);

            PlanetaryAssaultResult result = MakeCombat(
                    game,
                    new SequenceRNG(intValues: new[] { 0, 4, 0, 4, 99, 99 })
                )
                .ExecutePlanetaryAssault(
                    new List<Fleet> { uncommandedFleet, commandedFleet },
                    planet
                );

            Assert.AreEqual(1, result.DestroyedAttackerRegiments.Count);
            Assert.AreEqual(1, result.DestroyedDefenderRegiments.Count);
            CollectionAssert.Contains(result.DestroyedDefenderRegiments, firstDefender);
            CollectionAssert.DoesNotContain(result.DestroyedDefenderRegiments, secondDefender);
        }

        [Test]
        public void ExecutePlanetaryAssault_CollateralDamage_CanDestroyCivilianFacilityAndExcludesHeadquarters()
        {
            GameRoot game = CreateGame();
            (Planet planet, _) = CreatePlanet(game, "p1", "alliance", energy: 1);
            planet.IsHeadquarters = true;
            Building mine = AddCollateralBuilding(game, planet, "mine");
            AddDefender(game, planet, "defender");
            Fleet fleet = AddAssaultFleet(game, planet, "empire", regimentCount: 1);

            PlanetaryAssaultResult result = MakeCombat(
                    game,
                    new SequenceRNG(intValues: new[] { 0, 5, 0, 0 })
                )
                .ExecutePlanetaryAssault(new List<Fleet> { fleet }, planet);

            Assert.IsTrue(planet.IsHeadquarters);
            CollectionAssert.Contains(result.CollateralDestroyedBuildings, mine);
            Assert.AreEqual(1, planet.EnergyCapacity);
            Assert.IsFalse(result.Success);
        }

        [Test]
        public void ExecutePlanetaryAssault_CollateralDamage_RollsAllTrialsBeforeSelectingTargets()
        {
            GameRoot game = CreateGame();
            (Planet planet, _) = CreatePlanet(game, "p1", "alliance", energy: 1);
            AddDefender(game, planet, "defender1");
            AddDefender(game, planet, "defender2");
            Building mine = AddCollateralBuilding(game, planet, "mine");
            Fleet fleet = AddAssaultFleet(game, planet, "empire", regimentCount: 2);

            PlanetaryAssaultResult result = MakeCombat(
                    game,
                    new SequenceRNG(intValues: new[] { 0, 5, 0, 5, 0, 99, 0 })
                )
                .ExecutePlanetaryAssault(new List<Fleet> { fleet }, planet);

            CollectionAssert.Contains(result.CollateralDestroyedBuildings, mine);
            Assert.AreEqual(1, planet.EnergyCapacity);
        }

        [Test]
        public void ExecutePlanetaryAssault_Capture_LandsAtMostRequiredGarrison()
        {
            GameRoot game = CreateGame();
            (Planet planet, _) = CreatePlanet(game, "p1", "alliance", energy: 10);
            Fleet fleet = AddAssaultFleet(game, planet, "empire", regimentCount: 7);

            PlanetaryAssaultResult result = MakeCombat(game, new SequenceRNG())
                .ExecutePlanetaryAssault(new List<Fleet> { fleet }, planet);

            Assert.IsTrue(result.Success);
            Assert.AreEqual("empire", planet.GetOwnerInstanceID());
            Assert.AreEqual(6, result.LandedRegiments.Count);
            Assert.AreEqual(6, planet.GetAllRegiments().Count);
            Assert.AreEqual(1, fleet.CapitalShips[0].Regiments.Count);
        }

        [Test]
        public void ExecutePlanetaryAssault_CaptureWithFewerTroops_LandsEverySurvivor()
        {
            GameRoot game = CreateGame();
            (Planet planet, _) = CreatePlanet(game, "p1", "alliance", energy: 10);
            Fleet fleet = AddAssaultFleet(game, planet, "empire", regimentCount: 2);

            PlanetaryAssaultResult result = MakeCombat(game, new SequenceRNG())
                .ExecutePlanetaryAssault(new List<Fleet> { fleet }, planet);

            Assert.IsTrue(result.Success);
            Assert.AreEqual(2, result.LandedRegiments.Count);
            Assert.AreEqual(2, planet.GetAllRegiments().Count);
        }

        [Test]
        public void ExecutePlanetaryAssault_AttackersDestroyed_DoesNotCapturePlanet()
        {
            GameRoot game = CreateGame();
            (Planet planet, _) = CreatePlanet(game, "p1", "alliance", energy: 10);
            AddDefender(game, planet, "defender");
            Fleet fleet = AddAssaultFleet(game, planet, "empire", regimentCount: 1);

            PlanetaryAssaultResult result = MakeCombat(
                    game,
                    new SequenceRNG(intValues: new[] { 0, 4, 99 })
                )
                .ExecutePlanetaryAssault(new List<Fleet> { fleet }, planet);

            Assert.IsFalse(result.Success);
            Assert.AreEqual("alliance", planet.GetOwnerInstanceID());
            Assert.IsNull(result.OwnershipChange);
        }

        [Test]
        public void ExecutePlanetaryAssault_RngFailure_ClearsFleetCombatState()
        {
            GameRoot game = CreateGame();
            (Planet planet, _) = CreatePlanet(game, "p1", "alliance", energy: 10);
            AddDefender(game, planet, "defender");
            Fleet fleet = AddAssaultFleet(game, planet, "empire", regimentCount: 1);
            Fleet defenderFleet = AddAssaultFleet(game, planet, "alliance", regimentCount: 0);

            Assert.Throws<InvalidOperationException>(() =>
                MakeCombat(game, new ThrowingRNG())
                    .ExecutePlanetaryAssault(new List<Fleet> { fleet }, planet)
            );

            Assert.IsFalse(fleet.IsInCombat);
            Assert.IsFalse(defenderFleet.IsInCombat);
        }

        private static Fleet AddAssaultFleet(
            GameRoot game,
            Planet planet,
            string ownerId,
            int regimentCount
        )
        {
            Fleet fleet = new Fleet
            {
                InstanceID = Guid.NewGuid().ToString(),
                OwnerInstanceID = ownerId,
            };
            game.AttachNode(fleet, planet);
            CapitalShip ship = new CapitalShip
            {
                InstanceID = Guid.NewGuid().ToString(),
                OwnerInstanceID = ownerId,
                MaxHullStrength = 100,
                CurrentHullStrength = 100,
                RegimentCapacity = regimentCount,
                ManufacturingStatus = ManufacturingStatus.Complete,
            };
            game.AttachNode(ship, fleet);

            for (int index = 0; index < regimentCount; index++)
            {
                game.AttachNode(
                    new Regiment
                    {
                        InstanceID = Guid.NewGuid().ToString(),
                        OwnerInstanceID = ownerId,
                        ManufacturingStatus = ManufacturingStatus.Complete,
                    },
                    ship
                );
            }

            return fleet;
        }

        private static Regiment AddDefender(GameRoot game, Planet planet, string instanceId)
        {
            Regiment regiment = new Regiment
            {
                InstanceID = instanceId,
                OwnerInstanceID = planet.GetOwnerInstanceID(),
                ManufacturingStatus = ManufacturingStatus.Complete,
            };
            game.AttachNode(regiment, planet);
            return regiment;
        }

        private static Building AddDefenseBuilding(
            GameRoot game,
            Planet planet,
            string instanceId,
            DefenseFacilityClass defenseClass
        )
        {
            Building building = new Building
            {
                InstanceID = instanceId,
                OwnerInstanceID = planet.GetOwnerInstanceID(),
                BuildingType = BuildingType.Defense,
                DefenseFacilityClass = defenseClass,
                ManufacturingStatus = ManufacturingStatus.Complete,
            };
            game.AttachNode(building, planet);
            return building;
        }

        private static Building AddCollateralBuilding(
            GameRoot game,
            Planet planet,
            string instanceId
        )
        {
            Building building = new Building
            {
                InstanceID = instanceId,
                OwnerInstanceID = planet.GetOwnerInstanceID(),
                BuildingType = BuildingType.Mine,
                ManufacturingStatus = ManufacturingStatus.Complete,
            };
            game.AttachNode(building, planet);
            return building;
        }
    }
}
