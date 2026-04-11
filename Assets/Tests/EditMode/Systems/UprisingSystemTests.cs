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
            bool isCoreSystem = false
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
                Regiment r = EntityFactory.CreateRegiment($"r{i}", "empire");
                game.AttachNode(r, planet);
            }

            UprisingSystem uprisingSystem = new UprisingSystem(game);
            return (game, planet, uprisingSystem);
        }

        [Test]
        public void ProcessTick_SufficientGarrison_NoUprising()
        {
            // Support 10 → threshold ceil((60-10)/10) = 5.
            // 5 troops = garrison met. No uprising regardless of dice.
            (GameRoot game, Planet planet, UprisingSystem system) = BuildScene(
                ownerSupport: 10,
                troopCount: 5
            );

            system.ProcessTick(new StubRNG());

            Assert.IsFalse(planet.IsInUprising, "Sufficient garrison should prevent uprising");
        }

        [Test]
        public void ProcessTick_NoGarrison_UprisingStarts()
        {
            // Support 10 → garrison required = ceil((60-10)/10) = 5.
            // 0 troops → surplus = -5 < 0 → uprising starts unconditionally.
            (GameRoot game, Planet planet, UprisingSystem system) = BuildScene(
                ownerSupport: 10,
                troopCount: 0
            );

            List<GameResult> results = system.ProcessTick(new StubRNG());

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
            // Support 10 → garrison required = 5. 5 troops → surplus = 0 ≥ 0 → no uprising.
            (GameRoot game, Planet planet, UprisingSystem system) = BuildScene(
                ownerSupport: 10,
                troopCount: 5
            );

            system.ProcessTick(new StubRNG());

            Assert.IsFalse(
                planet.IsInUprising,
                "Exactly sufficient garrison should not trigger uprising"
            );
        }

        [Test]
        public void ProcessTick_ActiveUprising_ConsequencesApplied_FacilityDestroyed()
        {
            // Support 10 → threshold = 5. Planet is already in uprising with no troops.
            // Dice: rollA = 3+1=4, rollB = 3+1=4. Score = 4+4+(5-0) = 13.
            // UPRIS1: >= 10 → 2 (destroy troop), but no troops → nothing.
            // UPRIS2: >= 12 → 5 (free all captured), but no captured → nothing.
            // Use dice that produce case 1 (facility destroyed): score 7-9 → upris1=1.
            // rollA=2+1=3, rollB=2+1=3. Score = 3+3+(5-0) = 11.
            // UPRIS1: >= 10 → 2 (destroy troop). No troops → nothing.
            // Use score = 7 (just above 6): rollA=0+1=1, rollB=0+1=1. Score=1+1+5=7 → upris1=1.
            (GameRoot game, Planet planet, UprisingSystem system) = BuildScene(
                ownerSupport: 10,
                troopCount: 0
            );
            planet.IsInUprising = true;
            Building facility = EntityFactory.CreateBuilding("b1", "empire");
            game.AttachNode(facility, planet);

            system.ProcessTick(new SequenceRNG(intValues: new[] { 0, 0 }));

            Assert.IsNull(
                game.GetSceneNodeByInstanceID<Building>("b1"),
                "Facility should be destroyed by uprising case 1"
            );
            Assert.IsTrue(planet.IsInUprising, "Uprising should remain active after consequence");
        }

        [Test]
        public void ProcessTick_ActiveUprising_OfficerCaptured()
        {
            // Score = 7 → upris1=1 (facility). No facility, so nothing.
            // Score sufficient for case 3 (capture officer): upris2 >= 9 → 3.
            // Score = 10: rollA=3+1=4, rollB=3+1=4. Score=4+4+(5-0)=13.
            // UPRIS1: >= 10 → 2 (troop). No troops → nothing.
            // UPRIS2: >= 12 → 5 (free all captured). No captured → nothing.
            // Score = 9: rollA=1+1=2, rollB=1+1=2. Score=2+2+5=9.
            // UPRIS1: >= 6 → 1 (facility). No facility → nothing.
            // UPRIS2: >= 9 → 3 (capture officer). Officer present → capture!
            (GameRoot game, Planet planet, UprisingSystem system) = BuildScene(
                ownerSupport: 10,
                troopCount: 0
            );
            planet.IsInUprising = true;
            Officer officer = new Officer { InstanceID = "o1", OwnerInstanceID = "empire" };
            game.AttachNode(officer, planet);

            List<GameResult> results = system.ProcessTick(
                new SequenceRNG(intValues: new[] { 1, 1 })
            );

            Assert.IsTrue(officer.IsCaptured, "Officer should be captured by uprising case 3");
            Assert.IsTrue(results.OfType<OfficerCaptureStateResult>().Any(r => r.IsCaptured));
        }

        [Test]
        public void ProcessTick_ActiveUprising_CapturedOfficerFreed()
        {
            // Score = 9 → upris2 = 3 (capture). But officers are all captured → case 3 skips.
            // Need score >= 11 → upris2 = 4 (free one captured).
            // rollA=2+1=3, rollB=2+1=3. Score=3+3+5=11.
            // UPRIS1: >= 10 → 2 (destroy troop). No troops → nothing.
            // UPRIS2: >= 11 → 4 (free one captured officer).
            (GameRoot game, Planet planet, UprisingSystem system) = BuildScene(
                ownerSupport: 10,
                troopCount: 0
            );
            planet.IsInUprising = true;
            Officer captive = new Officer
            {
                InstanceID = "o1",
                OwnerInstanceID = "empire",
                IsCaptured = true,
            };
            game.AttachNode(captive, planet);

            List<GameResult> results = system.ProcessTick(
                new SequenceRNG(intValues: new[] { 2, 2 })
            );

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
            // Support 80 → threshold = 0. Garrison met by definition.
            (GameRoot game, Planet planet, UprisingSystem system) = BuildScene(
                ownerSupport: 80,
                opposingSupport: 20,
                troopCount: 0
            );

            system.ProcessTick(new SequenceRNG(intValues: new[] { 8, 8 }));

            Assert.IsFalse(planet.IsInUprising, "High support should prevent uprising");
        }

        [Test]
        public void ProcessTick_AlreadyInUprising_ConsequencesFireButNothingToDestroy()
        {
            // Planet already in uprising with no troops and no facilities.
            // High dice → upris1=2 (destroy troop), upris2=5 (free all captured).
            // Both consequences skip because there are no valid targets.
            // Uprising remains active; ownership does not change.
            (GameRoot game, Planet planet, UprisingSystem system) = BuildScene(
                ownerSupport: 10,
                troopCount: 0
            );
            planet.IsInUprising = true;

            system.ProcessTick(new SequenceRNG(intValues: new[] { 8, 8 }));

            Assert.IsTrue(planet.IsInUprising);
            Assert.AreEqual(
                "empire",
                planet.OwnerInstanceID,
                "UprisingSystem must not change ownership"
            );
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

            UprisingSystem uprisingSystem = new UprisingSystem(game);
            uprisingSystem.ProcessTick(new StubRNG());

            Assert.IsFalse(planet.IsInUprising, "Neutral planet should not revolt");
        }

        [Test]
        public void ProcessTick_EmpireGarrisonEfficiency_CoreSystem_HalvesRequirement()
        {
            // Core system, Support 30 → base garrison = 3. GarrisonEfficiency=2 → halved to 1.
            // 1 troop → garrison surplus = 1-1 = 0 ≥ 0 → no uprising.
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
            Regiment r = EntityFactory.CreateRegiment("r1", "empire");
            game.AttachNode(r, planet);

            UprisingSystem uprisingSystem = new UprisingSystem(game);
            uprisingSystem.ProcessTick(new StubRNG());

            Assert.IsFalse(
                planet.IsInUprising,
                "GarrisonEfficiency=2 on core system should halve the garrison requirement"
            );
        }

        [Test]
        public void ProcessTick_EmpireGarrisonEfficiency_OuterRim_NoBonus()
        {
            // Outer rim, Support 30 → garrison = 3. GarrisonEfficiency NOT applied on outer rim.
            // 1 troop → garrison surplus = 1-3 = -2 < 0 → uprising starts.
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
            Regiment r = EntityFactory.CreateRegiment("r1", "empire");
            game.AttachNode(r, planet);

            UprisingSystem uprisingSystem = new UprisingSystem(game);
            uprisingSystem.ProcessTick(new StubRNG());

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
            SupportShiftSystem system
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
                Fleet f = EntityFactory.CreateFleet($"hf{i}", "rebels");
                game.AttachNode(f, planet);
            }

            // Add hostile fighters (loose on planet — placed directly, bypassing ownership check)
            for (int i = 0; i < hostileFighters; i++)
            {
                Starfighter sf = EntityFactory.CreateStarfighter($"hsf{i}", "rebels");
                planet.Starfighters.Add(sf);
            }

            // Add hostile troops (placed directly, bypassing ownership check)
            for (int i = 0; i < hostileTroops; i++)
            {
                Regiment r = EntityFactory.CreateRegiment($"hr{i}", "rebels");
                planet.Regiments.Add(r);
            }

            // Add friendly fleets
            for (int i = 0; i < friendlyFleets; i++)
            {
                Fleet f = EntityFactory.CreateFleet($"ff{i}", "empire");
                game.AttachNode(f, planet);
            }

            SupportShiftSystem shiftSystem = new SupportShiftSystem(game);
            return (game, planet, empire, shiftSystem);
        }

        [Test]
        public void ProcessTick_LowSupport_NoHostiles_RecoveryApplied()
        {
            // Support 15 → bracket 0-20 → base shift 75. No hostiles. Shift = 75.
            (GameRoot game, Planet planet, _, SupportShiftSystem system) = BuildScene(support: 15);

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
            // Support 25 → bracket 21-30 → base shift 50. No hostiles. Shift = 50.
            (GameRoot game, Planet planet, _, SupportShiftSystem system) = BuildScene(support: 25);

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
            // Support 35 → bracket 31-40 → base shift 25. No hostiles. Shift = 25.
            (GameRoot game, Planet planet, _, SupportShiftSystem system) = BuildScene(support: 35);

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
            // Support 50 → above threshold (40). No shift.
            (GameRoot game, Planet planet, _, SupportShiftSystem system) = BuildScene(support: 50);

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
            // Support 15 but friendly fleet present → no shift.
            (GameRoot game, Planet planet, _, SupportShiftSystem system) = BuildScene(
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
            // Support 15 → base 75. 2 hostile fleets (×10=20), 3 fighters (×5=15), 1 troop (×2=2).
            // Shift = 75 - 20 - 15 - 2 = 38.
            (GameRoot game, Planet planet, _, SupportShiftSystem system) = BuildScene(
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
        public void ProcessTick_TroopEffectiveness_CoreSystem_DoublesHostileTroopPenalty()
        {
            // Core system, Support 15 → base 75. 5 hostile troops × TroopEffectiveness=2 = 10.
            // Penalty = 10 × 2 (troop penalty) = 20.
            // Shift = 75 - 20 = 55. (WeakSupportPenalty disabled so shift is not halved.)
            (GameRoot game, Planet planet, _, SupportShiftSystem system) = BuildScene(
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
        public void ProcessTick_TroopEffectiveness_OuterRim_NoBonus()
        {
            // Outer rim, TroopEffectiveness=2 should NOT apply.
            // Support 15 → base 75. 5 hostile troops × 1 = 5. Penalty = 5×2 = 10.
            // Shift = 75 - 10 = 65.
            (GameRoot game, Planet planet, _, SupportShiftSystem system) = BuildScene(
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
            // Support 15, InvertSupportShift=true → base 75, negated to -75.
            // 15 + (-75) = -60, clamped to 0.
            (GameRoot game, Planet planet, _, SupportShiftSystem system) = BuildScene(
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
            // Support 15, core system, WeakSupportPenaltyTrigger=Positive.
            // Base shift = 75 (positive). Penalty applies → 75/2 = 37.
            (GameRoot game, Planet planet, _, SupportShiftSystem system) = BuildScene(
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
