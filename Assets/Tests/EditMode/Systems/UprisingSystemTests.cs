using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Rebellion.Game;
using Rebellion.Game.Results;
using Rebellion.Systems;
using Rebellion.Util.Common;

namespace Rebellion.Tests.Systems
{
    [TestFixture]
    public class UprisingSystemTests
    {
        private (GameRoot game, Planet planet, UprisingSystem system) BuildScene(
            int ownerSupport = 10,
            int opposingSupport = 50,
            int troopCount = 0,
            bool isCoreSystem = false,
            IRandomNumberProvider rng = null
        )
        {
            GameConfig config = TestConfig.Create();
            GameRoot game = new GameRoot(config);
            game.Factions.Add(new Faction { InstanceID = "empire" });
            game.Factions.Add(new Faction { InstanceID = "rebels" });

            PlanetSystem system = new PlanetSystem
            {
                InstanceID = "sys1",
                SystemType = isCoreSystem ? PlanetSystemType.CoreSystem : PlanetSystemType.OuterRim,
            };
            game.AttachNode(system, game.Galaxy);

            Planet planet = new Planet
            {
                InstanceID = "p1",
                OwnerInstanceID = "empire",
                IsColonized = true,
                PopularSupport = new Dictionary<string, int>
                {
                    { "empire", ownerSupport },
                    { "rebels", opposingSupport },
                },
            };
            game.AttachNode(planet, system);

            // Add garrison troops
            for (int i = 0; i < troopCount; i++)
            {
                Regiment regiment = EntityFactory.CreateRegiment($"r{i}", "empire");
                game.AttachNode(regiment, planet);
            }

            MovementSystem movementSystem = new MovementSystem(game, new FogOfWarSystem(game));
            PlanetaryControlSystem planetaryControl = new PlanetaryControlSystem(
                game,
                movementSystem,
                new ManufacturingSystem(game)
            );
            UprisingSystem uprisingSystem = new UprisingSystem(
                game,
                rng ?? new StubRNG(),
                planetaryControl
            );
            return (game, planet, uprisingSystem);
        }

        [Test]
        public void ProcessTick_SufficientGarrison_NoUprising()
        {
            // Garrison requirement is 5 at support 10. Five troops meets it exactly.
            (GameRoot game, Planet planet, UprisingSystem system) = BuildScene(
                ownerSupport: 10,
                troopCount: 5
            );

            system.ProcessTick();

            Assert.IsFalse(planet.IsInUprising, "Sufficient garrison should prevent uprising");
        }

        [Test]
        public void ProcessTick_NoGarrison_UprisingStarts()
        {
            // Garrison requirement is 5 at support 10. Zero troops means a deficit, so uprising starts.
            (GameRoot game, Planet planet, UprisingSystem system) = BuildScene(
                ownerSupport: 10,
                troopCount: 0
            );

            List<GameResult> results = system.ProcessTick();

            Assert.IsTrue(planet.IsInUprising, "Garrison deficit should trigger uprising");
            Assert.AreEqual(
                "empire",
                planet.OwnerInstanceID,
                "UprisingSystem must not change ownership"
            );
            Assert.IsTrue(results.OfType<PlanetUprisingStartedResult>().Any());
        }

        [Test]
        public void ProcessTick_ExactGarrison_NoUprising()
        {
            // Garrison requirement is 5 at support 10. Five troops exactly meets it, no uprising.
            (GameRoot game, Planet planet, UprisingSystem system) = BuildScene(
                ownerSupport: 10,
                troopCount: 5
            );

            system.ProcessTick();

            Assert.IsFalse(
                planet.IsInUprising,
                "Exactly sufficient garrison should not trigger uprising"
            );
        }

        [Test]
        public void ProcessTick_ActiveUprisingWithFacility_DestroysFacility()
        {
            // Planet already in uprising with one garrison troop (so the zero-garrison flip
            // mechanic doesn't fire). Dice produce a score that triggers consequence case 1
            // (destroy facility). A building is present, so it gets destroyed.
            (GameRoot game, Planet planet, UprisingSystem system) = BuildScene(
                ownerSupport: 10,
                troopCount: 1,
                rng: new SequenceRNG(intValues: new[] { 0, 0 })
            );
            planet.IsInUprising = true;
            planet.EnergyCapacity = 1;
            Building facility = EntityFactory.CreateBuilding("b1", "empire");
            game.AttachNode(facility, planet);

            system.ProcessTick();

            Assert.IsNull(
                game.GetSceneNodeByInstanceID<Building>("b1"),
                "Facility should be destroyed by uprising case 1"
            );
            Assert.IsTrue(planet.IsInUprising, "Uprising should remain active after consequence");
        }

        [Test]
        public void ProcessTick_ActiveUprising_OfficerCaptured()
        {
            // Planet in uprising with a garrison troop (so the zero-garrison flip doesn't fire)
            // and an officer present. Dice trigger consequence case 3 (capture officer).
            (GameRoot game, Planet planet, UprisingSystem system) = BuildScene(
                ownerSupport: 10,
                troopCount: 1,
                rng: new SequenceRNG(intValues: new[] { 2, 1 })
            );
            planet.IsInUprising = true;
            Officer officer = new Officer { InstanceID = "o1", OwnerInstanceID = "empire" };
            game.AttachNode(officer, planet);

            List<GameResult> results = system.ProcessTick();

            Assert.IsTrue(officer.IsCaptured, "Officer should be captured by uprising case 3");
            Assert.IsTrue(results.OfType<OfficerCaptureStateResult>().Any(r => r.IsCaptured));
        }

        [Test]
        public void ProcessTick_ActiveUprising_CapturedOfficerFreed()
        {
            // Planet in uprising with a garrison troop (so the zero-garrison flip doesn't fire)
            // and an already-captured officer. Dice trigger consequence case 4 (free officer).
            (GameRoot game, Planet planet, UprisingSystem system) = BuildScene(
                ownerSupport: 10,
                troopCount: 1,
                rng: new SequenceRNG(intValues: new[] { 3, 2 })
            );
            planet.IsInUprising = true;
            Officer captive = new Officer
            {
                InstanceID = "o1",
                OwnerInstanceID = "empire",
                IsCaptured = true,
            };
            game.AttachNode(captive, planet);

            List<GameResult> results = system.ProcessTick();

            Assert.IsFalse(
                captive.IsCaptured,
                "Captured officer should be freed by uprising case 4"
            );
            Assert.IsTrue(
                results.OfType<OfficerCaptureStateResult>().Any(r => !r.IsCaptured),
                "Should emit OfficerCaptureStateResult with IsCaptured=false"
            );
        }

        [Test]
        public void ProcessTick_HighSupport_NoUprising()
        {
            // At support 80, garrison requirement is 0. No uprising regardless of troops.
            (GameRoot game, Planet planet, UprisingSystem system) = BuildScene(
                ownerSupport: 80,
                opposingSupport: 20,
                troopCount: 0,
                rng: new SequenceRNG(intValues: new[] { 8, 8 })
            );

            system.ProcessTick();

            Assert.IsFalse(planet.IsInUprising, "High support should prevent uprising");
        }

        [Test]
        public void ProcessTick_ActiveUprising_ZeroTroops_PlanetGoesNeutral()
        {
            // Planet in active uprising with zero garrison troops: the controller has lost the
            // planet. Uprising ends and the planet is reset to neutral ownership.
            (GameRoot game, Planet planet, UprisingSystem system) = BuildScene(
                ownerSupport: 10,
                troopCount: 0,
                rng: new SequenceRNG(intValues: new[] { 8, 8 })
            );
            planet.IsInUprising = true;

            List<GameResult> results = system.ProcessTick();

            Assert.IsFalse(planet.IsInUprising, "Uprising should end when controller loses planet");
            Assert.IsNull(planet.OwnerInstanceID, "Planet should become neutral");
            PlanetOwnershipChangedResult flip = results
                .OfType<PlanetOwnershipChangedResult>()
                .SingleOrDefault();
            Assert.IsNotNull(flip, "PlanetOwnershipChangedResult should be emitted");
            Assert.AreEqual("empire", flip.PreviousOwner?.InstanceID);
            Assert.IsNull(flip.NewOwner);
        }

        [Test]
        public void ProcessTick_NeutralPlanet_Skipped()
        {
            GameConfig config = TestConfig.Create();
            GameRoot game = new GameRoot(config);
            PlanetSystem system = new PlanetSystem { InstanceID = "sys1" };
            game.AttachNode(system, game.Galaxy);
            Planet planet = new Planet
            {
                InstanceID = "p1",
                OwnerInstanceID = null,
                PopularSupport = new Dictionary<string, int>(),
            };
            game.AttachNode(planet, system);

            MovementSystem movementSystem = new MovementSystem(game, new FogOfWarSystem(game));
            PlanetaryControlSystem planetaryControl = new PlanetaryControlSystem(
                game,
                movementSystem,
                new ManufacturingSystem(game)
            );
            UprisingSystem uprisingSystem = new UprisingSystem(
                game,
                new StubRNG(),
                planetaryControl
            );
            uprisingSystem.ProcessTick();

            Assert.IsFalse(planet.IsInUprising, "Neutral planet should not revolt");
        }

        [Test]
        public void ProcessTick_EmpireGarrisonOnCoreSystem_HalvesRequirement()
        {
            // On a core system with GarrisonEfficiency=2, the base garrison requirement of 3
            // is halved to 1. One troop meets it, so no uprising.
            GameConfig config = TestConfig.Create();
            GameRoot game = new GameRoot(config);
            Faction empire = new Faction
            {
                InstanceID = "empire",
                Modifiers = new FactionModifiers { GarrisonEfficiency = 2 },
            };
            game.Factions.Add(empire);
            game.Factions.Add(new Faction { InstanceID = "rebels" });

            PlanetSystem system = new PlanetSystem
            {
                InstanceID = "sys1",
                SystemType = PlanetSystemType.CoreSystem,
            };
            game.AttachNode(system, game.Galaxy);
            Planet planet = new Planet
            {
                InstanceID = "p1",
                OwnerInstanceID = "empire",
                IsColonized = true,
                PopularSupport = new Dictionary<string, int> { { "empire", 30 }, { "rebels", 50 } },
            };
            game.AttachNode(planet, system);
            Regiment regiment = EntityFactory.CreateRegiment("r1", "empire");
            game.AttachNode(regiment, planet);

            MovementSystem movementSystem = new MovementSystem(game, new FogOfWarSystem(game));
            PlanetaryControlSystem planetaryControl = new PlanetaryControlSystem(
                game,
                movementSystem,
                new ManufacturingSystem(game)
            );
            UprisingSystem uprisingSystem = new UprisingSystem(
                game,
                new StubRNG(),
                planetaryControl
            );
            uprisingSystem.ProcessTick();

            Assert.IsFalse(
                planet.IsInUprising,
                "GarrisonEfficiency=2 on core system should halve the garrison requirement"
            );
        }

        [Test]
        public void ProcessTick_EmpireGarrisonOnOuterRim_NoBonus()
        {
            // On an outer rim planet, GarrisonEfficiency does not apply. Garrison requirement
            // stays at 3 with one troop, so the deficit triggers an uprising.
            GameConfig config = TestConfig.Create();
            GameRoot game = new GameRoot(config);
            Faction empire = new Faction
            {
                InstanceID = "empire",
                Modifiers = new FactionModifiers { GarrisonEfficiency = 2 },
            };
            game.Factions.Add(empire);
            game.Factions.Add(new Faction { InstanceID = "rebels" });

            PlanetSystem system = new PlanetSystem
            {
                InstanceID = "sys1",
                SystemType = PlanetSystemType.OuterRim,
            };
            game.AttachNode(system, game.Galaxy);
            Planet planet = new Planet
            {
                InstanceID = "p1",
                OwnerInstanceID = "empire",
                IsColonized = true,
                PopularSupport = new Dictionary<string, int> { { "empire", 30 }, { "rebels", 50 } },
            };
            game.AttachNode(planet, system);
            Regiment regiment = EntityFactory.CreateRegiment("r1", "empire");
            game.AttachNode(regiment, planet);

            MovementSystem movementSystem = new MovementSystem(game, new FogOfWarSystem(game));
            PlanetaryControlSystem planetaryControl = new PlanetaryControlSystem(
                game,
                movementSystem,
                new ManufacturingSystem(game)
            );
            UprisingSystem uprisingSystem = new UprisingSystem(
                game,
                new StubRNG(),
                planetaryControl
            );
            uprisingSystem.ProcessTick();

            Assert.IsTrue(
                planet.IsInUprising,
                "Outer rim should NOT apply GarrisonEfficiency — garrison deficit triggers uprising"
            );
        }
    }

    [TestFixture]
    public class GarrisonRequirementTests
    {
        [TestCase(80, 0)]
        [TestCase(60, 0)]
        [TestCase(55, 1)]
        [TestCase(50, 1)]
        [TestCase(40, 2)]
        [TestCase(30, 3)]
        [TestCase(20, 4)]
        [TestCase(10, 5)]
        [TestCase(0, 6)]
        public void CalculateGarrisonRequirement_StandardPlanet_MatchesOriginalFormula(
            int support,
            int expectedGarrison
        )
        {
            GameConfig config = TestConfig.Create();
            GameRoot game = new GameRoot(config);
            Faction faction = new Faction { InstanceID = "empire" };
            game.Factions.Add(faction);

            PlanetSystem system = new PlanetSystem
            {
                InstanceID = "sys1",
                SystemType = PlanetSystemType.OuterRim,
            };
            game.AttachNode(system, game.Galaxy);

            Planet planet = new Planet
            {
                InstanceID = "p1",
                OwnerInstanceID = "empire",
                IsColonized = true,
                PopularSupport = new Dictionary<string, int> { { "empire", support } },
            };
            game.AttachNode(planet, system);

            int garrison = UprisingSystem.CalculateGarrisonRequirement(
                planet,
                faction,
                config.AI.Garrison
            );

            Assert.AreEqual(expectedGarrison, garrison, $"Garrison for support={support}");
        }

        [Test]
        public void CalculateGarrisonRequirement_CoreWorldEmpire_Halved()
        {
            GameConfig config = TestConfig.Create();
            GameRoot game = new GameRoot(config);
            Faction empire = new Faction
            {
                InstanceID = "empire",
                Modifiers = new FactionModifiers { GarrisonEfficiency = 2 },
            };
            game.Factions.Add(empire);

            PlanetSystem system = new PlanetSystem
            {
                InstanceID = "sys1",
                SystemType = PlanetSystemType.CoreSystem,
            };
            game.AttachNode(system, game.Galaxy);

            Planet planet = new Planet
            {
                InstanceID = "p1",
                OwnerInstanceID = "empire",
                IsColonized = true,
                PopularSupport = new Dictionary<string, int> { { "empire", 20 } },
            };
            game.AttachNode(planet, system);

            // Base: ceil((60-20)/10) = 4. Halved: 4/2 = 2.
            int garrison = UprisingSystem.CalculateGarrisonRequirement(
                planet,
                empire,
                config.AI.Garrison
            );

            Assert.AreEqual(2, garrison, "Empire core world should halve garrison");
        }

        [Test]
        public void CalculateGarrisonRequirement_CoreWorldAlliance_NotHalved()
        {
            GameConfig config = TestConfig.Create();
            GameRoot game = new GameRoot(config);
            Faction alliance = new Faction
            {
                InstanceID = "alliance",
                Modifiers = new FactionModifiers { GarrisonEfficiency = 1 },
            };
            game.Factions.Add(alliance);

            PlanetSystem system = new PlanetSystem
            {
                InstanceID = "sys1",
                SystemType = PlanetSystemType.CoreSystem,
            };
            game.AttachNode(system, game.Galaxy);

            Planet planet = new Planet
            {
                InstanceID = "p1",
                OwnerInstanceID = "alliance",
                IsColonized = true,
                PopularSupport = new Dictionary<string, int> { { "alliance", 20 } },
            };
            game.AttachNode(planet, system);

            // Base: ceil((60-20)/10) = 4. Alliance: no halving.
            int garrison = UprisingSystem.CalculateGarrisonRequirement(
                planet,
                alliance,
                config.AI.Garrison
            );

            Assert.AreEqual(4, garrison, "Alliance core world should NOT halve garrison");
        }

        [Test]
        public void CalculateGarrisonRequirement_CoreWorldEmpire_CanBeZero()
        {
            GameConfig config = TestConfig.Create();
            GameRoot game = new GameRoot(config);
            Faction empire = new Faction
            {
                InstanceID = "empire",
                Modifiers = new FactionModifiers { GarrisonEfficiency = 2 },
            };
            game.Factions.Add(empire);

            PlanetSystem system = new PlanetSystem
            {
                InstanceID = "sys1",
                SystemType = PlanetSystemType.CoreSystem,
            };
            game.AttachNode(system, game.Galaxy);

            Planet planet = new Planet
            {
                InstanceID = "p1",
                OwnerInstanceID = "empire",
                IsColonized = true,
                PopularSupport = new Dictionary<string, int> { { "empire", 55 } },
            };
            game.AttachNode(planet, system);

            // Base: ceil((60-55)/10) = 1. Halved: 1/2 = 0 (integer division).
            int garrison = UprisingSystem.CalculateGarrisonRequirement(
                planet,
                empire,
                config.AI.Garrison
            );

            Assert.AreEqual(
                0,
                garrison,
                "Empire core world garrison can be 0 via integer division (no min-1 floor)"
            );
        }
    }

    [TestFixture]
    public class SupportShiftSystemTests
    {
        private (
            GameRoot game,
            Planet planet,
            Faction faction,
            PlanetaryControlSystem system
        ) BuildScene(
            int support = 20,
            int hostileFleets = 0,
            int hostileFighters = 0,
            int hostileTroops = 0,
            int friendlyFleets = 0,
            bool isCoreSystem = false,
            bool invertSupport = false,
            SupportShiftCondition weakPenalty = SupportShiftCondition.Positive,
            int troopEffectiveness = 1
        )
        {
            GameConfig config = TestConfig.Create();
            GameRoot game = new GameRoot(config);
            Faction empire = new Faction
            {
                InstanceID = "empire",
                Modifiers = new FactionModifiers
                {
                    InvertSupportShift = invertSupport,
                    WeakSupportPenaltyTrigger = weakPenalty,
                    TroopEffectiveness = troopEffectiveness,
                },
            };
            Faction rebels = new Faction { InstanceID = "rebels" };
            game.Factions.Add(empire);
            game.Factions.Add(rebels);

            PlanetSystem system = new PlanetSystem
            {
                InstanceID = "sys1",
                SystemType = isCoreSystem ? PlanetSystemType.CoreSystem : PlanetSystemType.OuterRim,
            };
            game.AttachNode(system, game.Galaxy);

            Planet planet = new Planet
            {
                InstanceID = "p1",
                OwnerInstanceID = "empire",
                IsColonized = true,
                PositionX = 0,
                PositionY = 0,
                PopularSupport = new Dictionary<string, int> { { "empire", support } },
            };
            game.AttachNode(planet, system);

            // Add hostile fleets
            for (int i = 0; i < hostileFleets; i++)
            {
                Fleet fleet = EntityFactory.CreateFleet($"hf{i}", "rebels");
                game.AttachNode(fleet, planet);
            }

            // Add hostile fighters (loose on planet — placed directly, bypassing ownership check)
            for (int i = 0; i < hostileFighters; i++)
            {
                Starfighter starfighter = EntityFactory.CreateStarfighter($"hsf{i}", "rebels");
                planet.Starfighters.Add(starfighter);
            }

            // Add hostile troops (placed directly, bypassing ownership check)
            for (int i = 0; i < hostileTroops; i++)
            {
                Regiment regiment = EntityFactory.CreateRegiment($"hr{i}", "rebels");
                planet.Regiments.Add(regiment);
            }

            // Add friendly fleets
            for (int i = 0; i < friendlyFleets; i++)
            {
                Fleet fleet = EntityFactory.CreateFleet($"ff{i}", "empire");
                game.AttachNode(fleet, planet);
            }

            MovementSystem movementSystem = new MovementSystem(game, new FogOfWarSystem(game));
            PlanetaryControlSystem controlSystem = new PlanetaryControlSystem(
                game,
                movementSystem,
                new ManufacturingSystem(game)
            );
            return (game, planet, empire, controlSystem);
        }

        [Test]
        public void ProcessTick_LowSupportNoHostiles_RecoveryApplied()
        {
            // Support 15 falls in the 0-20 bracket with a base shift of 75. No hostiles present.
            (GameRoot game, Planet planet, _, PlanetaryControlSystem system) = BuildScene(
                support: 15
            );

            system.ProcessTick();

            Assert.AreEqual(
                90,
                planet.GetPopularSupport("empire"),
                "Support should recover by 75 (15 + 75 = 90)"
            );
        }

        [Test]
        public void ProcessTick_MidBracket_CorrectBaseShift()
        {
            // Support 25 falls in the 21-30 bracket with a base shift of 50. No hostiles present.
            (GameRoot game, Planet planet, _, PlanetaryControlSystem system) = BuildScene(
                support: 25
            );

            system.ProcessTick();

            Assert.AreEqual(
                75,
                planet.GetPopularSupport("empire"),
                "Support should recover by 50 (25 + 50 = 75)"
            );
        }

        [Test]
        public void ProcessTick_HighBracket_CorrectBaseShift()
        {
            // Support 35 falls in the 31-40 bracket with a base shift of 25. No hostiles present.
            (GameRoot game, Planet planet, _, PlanetaryControlSystem system) = BuildScene(
                support: 35
            );

            system.ProcessTick();

            Assert.AreEqual(
                60,
                planet.GetPopularSupport("empire"),
                "Support should recover by 25 (35 + 25 = 60)"
            );
        }

        [Test]
        public void ProcessTick_AboveThreshold_NoShift()
        {
            // Support 50 is above the recovery threshold (40), so no shift occurs.
            (GameRoot game, Planet planet, _, PlanetaryControlSystem system) = BuildScene(
                support: 50
            );

            system.ProcessTick();

            Assert.AreEqual(
                50,
                planet.GetPopularSupport("empire"),
                "Support above threshold should not shift"
            );
        }

        [Test]
        public void ProcessTick_FriendlyFleetPresent_NoShift()
        {
            // Support 15 with a friendly fleet present blocks the shift entirely.
            (GameRoot game, Planet planet, _, PlanetaryControlSystem system) = BuildScene(
                support: 15,
                friendlyFleets: 1
            );

            system.ProcessTick();

            Assert.AreEqual(
                15,
                planet.GetPopularSupport("empire"),
                "Friendly fleet should block support shift"
            );
        }

        [Test]
        public void ProcessTick_HostileForces_ReduceRecovery()
        {
            // Base shift is 75 at support 15. Hostile forces reduce it:
            // 2 fleets (-20), 3 fighters (-15), 1 troop (-2) = net shift of 38.
            (GameRoot game, Planet planet, _, PlanetaryControlSystem system) = BuildScene(
                support: 15,
                hostileFleets: 2,
                hostileFighters: 3,
                hostileTroops: 1
            );

            system.ProcessTick();

            Assert.AreEqual(
                53,
                planet.GetPopularSupport("empire"),
                "Hostile forces should reduce recovery (15 + 38 = 53)"
            );
        }

        [Test]
        public void ProcessTick_TroopEffectivenessOnCoreSystem_DoublesHostileTroopPenalty()
        {
            // Base shift is 75 at support 15. On a core system with TroopEffectiveness=2,
            // 5 hostile troops apply a doubled penalty of 20, resulting in shift of 55.
            (GameRoot game, Planet planet, _, PlanetaryControlSystem system) = BuildScene(
                support: 15,
                hostileTroops: 5,
                troopEffectiveness: 2,
                isCoreSystem: true,
                weakPenalty: SupportShiftCondition.Negative
            );

            system.ProcessTick();

            Assert.AreEqual(
                70,
                planet.GetPopularSupport("empire"),
                "TroopEffectiveness=2 should double hostile troop penalty (15 + 55 = 70)"
            );
        }

        [Test]
        public void ProcessTick_TroopEffectivenessOnOuterRim_NoBonus()
        {
            // On outer rim, TroopEffectiveness bonus does not apply.
            // Base shift is 75, 5 hostile troops apply the standard penalty of 10, shift is 65.
            (GameRoot game, Planet planet, _, PlanetaryControlSystem system) = BuildScene(
                support: 15,
                hostileTroops: 5,
                troopEffectiveness: 2,
                isCoreSystem: false
            );

            system.ProcessTick();

            Assert.AreEqual(
                80,
                planet.GetPopularSupport("empire"),
                "Outer rim should NOT apply TroopEffectiveness bonus (15 + 65 = 80)"
            );
        }

        [Test]
        public void ProcessTick_InvertSupportShift_NegatesRecovery()
        {
            // With InvertSupportShift=true, the base shift of 75 is negated to -75.
            // Support drops from 15 to 0 (clamped).
            (GameRoot game, Planet planet, _, PlanetaryControlSystem system) = BuildScene(
                support: 15,
                invertSupport: true
            );

            system.ProcessTick();

            Assert.AreEqual(
                0,
                planet.GetPopularSupport("empire"),
                "Inverted support shift should reduce support (clamped to 0)"
            );
        }

        [Test]
        public void ProcessTick_CoreWorldWeakPenalty_HalvesShift()
        {
            // On a core system with WeakSupportPenaltyTrigger=Positive, the positive base
            // shift of 75 is halved to 37.
            (GameRoot game, Planet planet, _, PlanetaryControlSystem system) = BuildScene(
                support: 15,
                isCoreSystem: true,
                weakPenalty: SupportShiftCondition.Positive
            );

            system.ProcessTick();

            Assert.AreEqual(
                52,
                planet.GetPopularSupport("empire"),
                "Core weak support should halve recovery (15 + 37 = 52)"
            );
        }
    }
}
