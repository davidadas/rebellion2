using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Rebellion.Core.Configuration;
using Rebellion.Core.Simulation;
using Rebellion.Game;
using Rebellion.SceneGraph;
using Rebellion.Systems;
using Rebellion.Systems.Results;

namespace Rebellion.Tests.Systems
{
    /// <summary>
    /// Tests for JediSystem tier advancement and detection mechanics.
    /// Tests validate Force tier progression (Aware → Training → Experienced) and
    /// periodic detection checks against original REBEXE.EXE behavior.
    /// XP accumulation is currently unimplemented - tests manually set ForceExperience.
    /// </summary>
    [TestFixture]
    public class JediSystemTests
    {
        private GameRoot game;
        private JediSystem manager;
        private Faction alliance;
        private Planet tatooine;

        [SetUp]
        public void SetUp()
        {
            GameConfig config = ConfigLoader.LoadGameConfig();
            config.Jedi.XpToTraining = 50;
            config.Jedi.XpToExperienced = 150;
            config.Jedi.DetectionCheckInterval = 30;
            config.Jedi.DetectProbAware = 0.05;
            config.Jedi.DetectProbTraining = 0.15;
            config.Jedi.DetectProbExperienced = 0.30;

            game = new GameRoot(config);
            manager = new JediSystem(game);

            alliance = new Faction { InstanceID = "FNALL1", DisplayName = "Alliance" };
            game.Factions.Add(alliance);

            PlanetSystem system = new PlanetSystem
            {
                InstanceID = "TATOO",
                PositionX = 0,
                PositionY = 0,
            };
            game.AttachNode(system, game.GetGalaxyMap());

            tatooine = new Planet
            {
                InstanceID = "TATOOINE",
                DisplayName = "Tatooine",
                OwnerInstanceID = "FNALL1",
            };
            game.AttachNode(tatooine, system);
        }

        // ── Tier Advancement ────────────────────────────────────────────────────

        [Test]
        public void ProcessTick_AdvancesToTraining()
        {
            Officer luke = CreateOfficer("LUKE", 100, 50, ForceTier.Aware);

            List<JediResult> results = manager.ProcessTick(game, new MockRNG());

            Assert.AreEqual(ForceTier.Training, luke.ForceTier);
            Assert.AreEqual(1, results.Count);
            Assert.AreEqual(JediEventType.TierAdvanced, results[0].EventType);
            Assert.AreEqual(ForceTier.Aware, results[0].OldTier);
            Assert.AreEqual(ForceTier.Training, results[0].NewTier);
        }

        [Test]
        public void ProcessTick_AdvancesToExperienced()
        {
            Officer luke = CreateOfficer("LUKE", 100, 150, ForceTier.Training);

            List<JediResult> results = manager.ProcessTick(game, new MockRNG());

            Assert.AreEqual(ForceTier.Experienced, luke.ForceTier);
            Assert.AreEqual(2, results.Count);
            Assert.IsTrue(results.Any(r => r.EventType == JediEventType.TierAdvanced));
            Assert.IsTrue(results.Any(r => r.EventType == JediEventType.TrainingComplete));
        }

        [Test]
        public void ProcessTick_BelowThreshold_NoAdvancement()
        {
            Officer luke = CreateOfficer("LUKE", 100, 49, ForceTier.Aware);

            List<JediResult> results = manager.ProcessTick(game, new MockRNG());

            Assert.AreEqual(ForceTier.Aware, luke.ForceTier);
            Assert.AreEqual(0, results.Count);
        }

        [Test]
        public void ProcessTick_ExactThreshold_Advances()
        {
            Officer luke = CreateOfficer("LUKE", 100, 50, ForceTier.Aware);

            List<JediResult> results = manager.ProcessTick(game, new MockRNG());

            Assert.AreEqual(ForceTier.Training, luke.ForceTier);
            Assert.AreEqual(1, results.Count);
        }

        [Test]
        public void ProcessTick_JumpFromNoneToExperienced()
        {
            // Palpatine starts at 150 XP from None
            Officer palpatine = CreateOfficer("PALPATINE", 100, 150, ForceTier.None);

            List<JediResult> results = manager.ProcessTick(game, new MockRNG());

            Assert.AreEqual(ForceTier.Experienced, palpatine.ForceTier);
            Assert.AreEqual(2, results.Count);
            Assert.IsTrue(results.Any(r => r.EventType == JediEventType.TierAdvanced));
            Assert.IsTrue(results.Any(r => r.EventType == JediEventType.TrainingComplete));
        }

        [Test]
        public void ProcessTick_NoForceUser_NoEvents()
        {
            Officer tarkin = CreateOfficer("TARKIN", 0, 0, ForceTier.None);

            List<JediResult> results = manager.ProcessTick(game, new MockRNG());

            Assert.AreEqual(ForceTier.None, tarkin.ForceTier);
            Assert.AreEqual(0, results.Count);
        }

        [Test]
        public void ProcessTick_MultipleOfficers_AllAdvance()
        {
            Officer luke = CreateOfficer("LUKE", 100, 50, ForceTier.Aware);
            Officer leia = CreateOfficer("LEIA", 100, 150, ForceTier.Training);

            List<JediResult> results = manager.ProcessTick(game, new MockRNG());

            Assert.AreEqual(ForceTier.Training, luke.ForceTier);
            Assert.AreEqual(ForceTier.Experienced, leia.ForceTier);
            Assert.AreEqual(3, results.Count); // Luke: 1 TierAdvanced, Leia: 1 TierAdvanced + 1 TrainingComplete
        }

        // ── Detection ────────────────────────────────────────────────────────────

        [Test]
        public void ProcessTick_DetectionTriggered()
        {
            Officer luke = CreateOfficer("LUKE", 100, 50, ForceTier.Training);
            game.CurrentTick = 30; // Detection interval

            MockRNG rng = new MockRNG(0.01); // Roll < 0.15 (DetectProbTraining)
            List<JediResult> results = manager.ProcessTick(game, rng);

            Assert.IsTrue(luke.IsDiscoveredJedi);
            Assert.IsTrue(results.Any(r => r.EventType == JediEventType.JediDiscovered));
        }

        [Test]
        public void ProcessTick_DetectionFailsHighRoll()
        {
            Officer luke = CreateOfficer("LUKE", 100, 50, ForceTier.Training);
            game.CurrentTick = 30;

            MockRNG rng = new MockRNG(0.99); // Roll > 0.15
            List<JediResult> results = manager.ProcessTick(game, rng);

            Assert.IsFalse(luke.IsDiscoveredJedi);
            Assert.IsFalse(results.Any(r => r.EventType == JediEventType.JediDiscovered));
        }

        [Test]
        public void ProcessTick_AlreadyDiscovered_NoRecheck()
        {
            Officer luke = CreateOfficer("LUKE", 100, 50, ForceTier.Training);
            luke.IsDiscoveredJedi = true;
            game.CurrentTick = 60; // Multiple intervals passed

            MockRNG rng = new MockRNG(0.01); // Low roll
            List<JediResult> results = manager.ProcessTick(game, rng);

            Assert.IsFalse(results.Any(r => r.EventType == JediEventType.JediDiscovered));
        }

        [Test]
        public void ProcessTick_NotDetectionInterval_NoCheck()
        {
            Officer luke = CreateOfficer("LUKE", 100, 50, ForceTier.Training);
            game.CurrentTick = 29; // One tick before interval

            MockRNG rng = new MockRNG(0.01);
            List<JediResult> results = manager.ProcessTick(game, rng);

            Assert.IsFalse(luke.IsDiscoveredJedi);
            Assert.IsFalse(results.Any(r => r.EventType == JediEventType.JediDiscovered));
        }

        [Test]
        public void ProcessTick_AwareTier_LowerDetectionRate()
        {
            Officer luke = CreateOfficer("LUKE", 100, 10, ForceTier.Aware);
            game.CurrentTick = 30;

            // Roll 0.04 < 0.05 (DetectProbAware) → should detect
            MockRNG rng = new MockRNG(0.04);
            List<JediResult> results = manager.ProcessTick(game, rng);

            Assert.IsTrue(luke.IsDiscoveredJedi);
        }

        [Test]
        public void ProcessTick_ExperiencedTier_HigherDetectionRate()
        {
            Officer palpatine = CreateOfficer("PALPATINE", 100, 150, ForceTier.Experienced);
            game.CurrentTick = 30;

            // Roll 0.29 < 0.30 (DetectProbExperienced) → should detect
            MockRNG rng = new MockRNG(0.29);
            List<JediResult> results = manager.ProcessTick(game, rng);

            Assert.IsTrue(palpatine.IsDiscoveredJedi);
        }

        [Test]
        public void ProcessTick_NoneTier_NeverDetected()
        {
            Officer tarkin = CreateOfficer("TARKIN", 0, 0, ForceTier.None);
            game.CurrentTick = 30;

            MockRNG rng = new MockRNG(0.0); // Guaranteed roll
            List<JediResult> results = manager.ProcessTick(game, rng);

            Assert.IsFalse(tarkin.IsDiscoveredJedi);
        }

        // ── Config Injection ─────────────────────────────────────────────────────

        [Test]
        public void ProcessTick_CustomConfig_UsesCustomThresholds()
        {
            game.Config.Jedi.XpToTraining = 100; // Custom threshold
            game.Config.Jedi.XpToExperienced = 200;

            Officer luke = CreateOfficer("LUKE", 100, 99, ForceTier.Aware);
            List<JediResult> results = manager.ProcessTick(game, new MockRNG());

            Assert.AreEqual(ForceTier.Aware, luke.ForceTier); // Still Aware at 99 XP

            luke.ForceExperience = 100;
            results = manager.ProcessTick(game, new MockRNG());

            Assert.AreEqual(ForceTier.Training, luke.ForceTier); // Advances at 100 XP
        }

        [Test]
        public void ProcessTick_CustomDetectionInterval_Honored()
        {
            game.Config.Jedi.DetectionCheckInterval = 50;

            Officer luke = CreateOfficer("LUKE", 100, 50, ForceTier.Training);
            game.CurrentTick = 30;

            MockRNG rng = new MockRNG(0.01);
            List<JediResult> results = manager.ProcessTick(game, rng);

            Assert.IsFalse(luke.IsDiscoveredJedi); // 30 % 50 != 0

            game.CurrentTick = 50;
            results = manager.ProcessTick(game, rng);

            Assert.IsTrue(luke.IsDiscoveredJedi); // 50 % 50 == 0
        }

        [Test]
        public void ProcessTick_CustomDetectionProb_Honored()
        {
            game.Config.Jedi.DetectProbTraining = 0.50; // Custom 50% rate

            Officer luke = CreateOfficer("LUKE", 100, 50, ForceTier.Training);
            game.CurrentTick = 30;

            MockRNG rng = new MockRNG(0.49); // Just below threshold
            List<JediResult> results = manager.ProcessTick(game, rng);

            Assert.IsTrue(luke.IsDiscoveredJedi);
        }

        // ── Edge Cases ────────────────────────────────────────────────────────────

        [Test]
        public void ProcessTick_EmptyGame_NoEvents()
        {
            List<JediResult> results = manager.ProcessTick(game, new MockRNG());

            Assert.AreEqual(0, results.Count);
        }

        [Test]
        public void ProcessTick_TierAndDetection_BothFire()
        {
            Officer luke = CreateOfficer("LUKE", 100, 50, ForceTier.Aware);
            game.CurrentTick = 30;

            MockRNG rng = new MockRNG(0.01); // Low roll for detection
            List<JediResult> results = manager.ProcessTick(game, rng);

            Assert.AreEqual(ForceTier.Training, luke.ForceTier);
            Assert.IsTrue(luke.IsDiscoveredJedi);
            Assert.AreEqual(2, results.Count); // TierAdvanced + JediDiscovered
        }

        // ── Helpers ───────────────────────────────────────────────────────────────

        private Officer CreateOfficer(string id, int jediProb, int forceXP, ForceTier tier)
        {
            Officer officer = new Officer
            {
                InstanceID = id,
                DisplayName = id,
                OwnerInstanceID = alliance.InstanceID,
                JediProbability = jediProb,
                ForceExperience = forceXP,
                ForceTier = tier,
                IsDiscoveredJedi = false,
            };
            game.AttachNode(officer, tatooine);
            return officer;
        }

        private class MockRNG : IRandomNumberProvider
        {
            private double value;

            public MockRNG(double value = 0.5)
            {
                this.value = value;
            }

            public double NextDouble() => value;

            public int NextInt(int min, int max) => min;
        }
    }
}
