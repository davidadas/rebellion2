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
    /// <summary>
    /// Tests for JediSystem force discovery state and force user discovery scanning.
    /// </summary>
    [TestFixture]
    public class JediSystemTests
    {
        private GameRoot _game;
        private JediSystem _system;
        private Faction _alliance;
        private Planet _tatooine;

        [SetUp]
        public void SetUp()
        {
            GameConfig config = ResourceManager.GetConfig<GameConfig>();
            _game = new GameRoot(config);
            _system = new JediSystem(_game, new FixedRNG());

            _alliance = new Faction { InstanceID = "FNALL1", DisplayName = "Alliance" };
            _game.Factions.Add(_alliance);

            PlanetSystem system = new PlanetSystem
            {
                InstanceID = "TATOO",
                PositionX = 0,
                PositionY = 0,
            };
            _game.AttachNode(system, _game.GetGalaxyMap());

            _tatooine = new Planet
            {
                InstanceID = "TATOOINE",
                DisplayName = "Tatooine",
                OwnerInstanceID = "FNALL1",
                IsColonized = true,
            };
            _game.AttachNode(_tatooine, system);
        }

        [Test]
        public void ProcessTick_ForceRankAboveThreshold_EntersDiscoveringState()
        {
            Officer luke = CreateKnownJedi("LUKE", forceValue: 85);

            List<ForceDiscoveryResult> results = _system
                .ProcessTick()
                .OfType<ForceDiscoveryResult>()
                .ToList();

            Assert.IsTrue(luke.IsDiscoveringForceUser);
            Assert.AreEqual(1, results.Count);
            Assert.AreEqual(ForceEventType.DiscoveringForceUser, results[0].EventType);
            Assert.AreEqual(85, results[0].ForceRank);
        }

        [Test]
        public void ProcessTick_ForceRankExactlyAtThreshold_EntersDiscoveringState()
        {
            // Default threshold is 80
            Officer luke = CreateKnownJedi("LUKE", forceValue: 80);

            List<ForceDiscoveryResult> results = _system
                .ProcessTick()
                .OfType<ForceDiscoveryResult>()
                .ToList();

            Assert.IsTrue(luke.IsDiscoveringForceUser);
            Assert.AreEqual(1, results.Count);
        }

        [Test]
        public void ProcessTick_ForceRankBelowThreshold_NoDiscovery()
        {
            Officer luke = CreateKnownJedi("LUKE", forceValue: 79);

            List<ForceDiscoveryResult> results = _system
                .ProcessTick()
                .OfType<ForceDiscoveryResult>()
                .ToList();

            Assert.IsFalse(luke.IsDiscoveringForceUser);
            Assert.AreEqual(0, results.Count);
        }

        [Test]
        public void ProcessTick_CapturedOfficer_NoDiscoveringState()
        {
            Officer luke = CreateKnownJedi("LUKE", forceValue: 100);
            luke.IsCaptured = true;

            List<ForceDiscoveryResult> results = _system
                .ProcessTick()
                .OfType<ForceDiscoveryResult>()
                .ToList();

            Assert.IsFalse(luke.IsDiscoveringForceUser);
            Assert.AreEqual(0, results.Count);
        }

        [Test]
        public void ProcessTick_AlreadyDiscovering_NoRepeatedEvent()
        {
            Officer luke = CreateKnownJedi("LUKE", forceValue: 100);
            luke.IsDiscoveringForceUser = true;

            List<ForceDiscoveryResult> results = _system
                .ProcessTick()
                .OfType<ForceDiscoveryResult>()
                .ToList();

            Assert.IsTrue(luke.IsDiscoveringForceUser);
            Assert.AreEqual(0, results.Count);
        }

        [Test]
        public void ProcessTick_ForceRankDropsBelowThreshold_ClearsDiscoveringState()
        {
            Officer luke = CreateKnownJedi("LUKE", forceValue: 50);
            luke.IsDiscoveringForceUser = true; // Was set previously

            _system.ProcessTick();

            Assert.IsFalse(luke.IsDiscoveringForceUser);
        }

        [Test]
        public void ProcessTick_NonJediOfficer_Skipped()
        {
            Officer han = new Officer
            {
                InstanceID = "HAN",
                DisplayName = "Han",
                OwnerInstanceID = _alliance.InstanceID,
                IsJedi = false,
                ForceValue = 100,
            };
            _game.AttachNode(han, _tatooine);

            List<ForceDiscoveryResult> results = _system
                .ProcessTick()
                .OfType<ForceDiscoveryResult>()
                .ToList();

            Assert.AreEqual(0, results.Count);
        }

        [Test]
        public void ProcessTick_DormantJediNotForceEligible_Skipped()
        {
            Officer leia = CreateDormantJedi("LEIA");

            List<ForceDiscoveryResult> results = _system
                .ProcessTick()
                .OfType<ForceDiscoveryResult>()
                .ToList();

            Assert.IsFalse(leia.IsDiscoveringForceUser);
            Assert.AreEqual(0, results.Count);
        }

        [Test]
        public void ProcessTick_EmptyGame_NoEvents()
        {
            List<ForceDiscoveryResult> results = _system
                .ProcessTick()
                .OfType<ForceDiscoveryResult>()
                .ToList();

            Assert.AreEqual(0, results.Count);
        }

        [Test]
        public void ProcessTick_MultipleOfficers_AllProcessed()
        {
            Officer luke = CreateKnownJedi("LUKE", forceValue: 85);
            Officer vader = CreateKnownJedi("VADER", forceValue: 120);

            List<ForceDiscoveryResult> results = _system
                .ProcessTick()
                .OfType<ForceDiscoveryResult>()
                .ToList();

            Assert.IsTrue(luke.IsDiscoveringForceUser);
            Assert.IsTrue(vader.IsDiscoveringForceUser);
            Assert.AreEqual(2, results.Count);
        }

        [Test]
        public void ProcessTick_OfficerWithTrainingAdjustment_IncludesAdjustmentInRank()
        {
            // ForceValue 70 alone is below threshold 80, but with adjustment of 15 => rank 85
            Officer luke = CreateKnownJedi("LUKE", forceValue: 70);
            luke.ForceTrainingAdjustment = 15;

            List<ForceDiscoveryResult> results = _system
                .ProcessTick()
                .OfType<ForceDiscoveryResult>()
                .ToList();

            Assert.IsTrue(luke.IsDiscoveringForceUser);
            Assert.AreEqual(1, results.Count);
            Assert.AreEqual(85, results[0].ForceRank);
        }

        [Test]
        public void ProcessTick_DiscoveringJediWithDormantCandidate_DiscoversDormant()
        {
            Officer luke = CreateKnownJedi("LUKE", forceValue: 120);
            luke.IsDiscoveringForceUser = true;

            Officer leia = CreateDormantJedi("LEIA");

            // Probability = 120 + 0 - 100 = 20%. Roll = 0.0 * 100 = 0% < 20%.
            _system = new JediSystem(_game, new FixedRNG(0.0));
            List<ForceDiscoveryResult> results = _system
                .ProcessTick()
                .OfType<ForceDiscoveryResult>()
                .Where(r => r.EventType == ForceEventType.ForceUserDiscovered)
                .ToList();

            Assert.IsTrue(leia.IsForceEligible);
            Assert.Greater(leia.ForceValue, 0);
            Assert.AreEqual(1, results.Count);
        }

        [Test]
        public void ProcessTick_LowRankScanner_CannotDiscover()
        {
            // Rank 50 + 0 - 100 = -50% -> impossible
            Officer luke = CreateKnownJedi("LUKE", forceValue: 50);
            luke.IsDiscoveringForceUser = true;

            Officer leia = CreateDormantJedi("LEIA");

            List<ForceDiscoveryResult> results = _system
                .ProcessTick()
                .OfType<ForceDiscoveryResult>()
                .Where(r => r.EventType == ForceEventType.ForceUserDiscovered)
                .ToList();

            Assert.IsFalse(leia.IsForceEligible);
            Assert.AreEqual(0, leia.ForceValue);
            Assert.AreEqual(0, results.Count);
        }

        [Test]
        public void ProcessTick_BelowThresholdJedi_NoScan()
        {
            // ForceRank 50 is below threshold 80, so no discovering state
            Officer luke = CreateKnownJedi("LUKE", forceValue: 50);

            Officer leia = CreateDormantJedi("LEIA");

            _system = new JediSystem(_game, new FixedRNG(0.0));
            List<ForceDiscoveryResult> results = _system
                .ProcessTick()
                .OfType<ForceDiscoveryResult>()
                .Where(r => r.EventType == ForceEventType.ForceUserDiscovered)
                .ToList();

            Assert.IsFalse(luke.IsDiscoveringForceUser);
            Assert.IsFalse(leia.IsForceEligible);
            Assert.AreEqual(0, results.Count);
        }

        [Test]
        public void ProcessTick_AlreadyEligibleCandidate_Skipped()
        {
            Officer luke = CreateKnownJedi("LUKE", forceValue: 120);
            luke.IsDiscoveringForceUser = true;

            Officer vader = CreateKnownJedi("VADER", forceValue: 100);

            _system = new JediSystem(_game, new FixedRNG(0.0));
            List<ForceDiscoveryResult> results = _system
                .ProcessTick()
                .OfType<ForceDiscoveryResult>()
                .Where(r => r.EventType == ForceEventType.ForceUserDiscovered)
                .ToList();

            Assert.AreEqual(0, results.Count);
        }

        [Test]
        public void ProcessTick_CapturedCandidate_Skipped()
        {
            Officer luke = CreateKnownJedi("LUKE", forceValue: 120);
            luke.IsDiscoveringForceUser = true;

            Officer leia = CreateDormantJedi("LEIA");
            leia.IsCaptured = true;

            _system = new JediSystem(_game, new FixedRNG(0.0));
            List<ForceDiscoveryResult> results = _system
                .ProcessTick()
                .OfType<ForceDiscoveryResult>()
                .Where(r => r.EventType == ForceEventType.ForceUserDiscovered)
                .ToList();

            Assert.IsFalse(leia.IsForceEligible);
            Assert.AreEqual(0, results.Count);
        }

        [Test]
        public void ProcessTick_OnMissionCandidate_Skipped()
        {
            Officer luke = CreateKnownJedi("LUKE", forceValue: 120);
            luke.IsDiscoveringForceUser = true;

            Officer leia = CreateDormantJedi("LEIA");

            // Put Leia on a mission — she should be skipped
            StubMission mission = new StubMission(_alliance.InstanceID, _tatooine.InstanceID);
            _game.AttachNode(mission, _tatooine);
            _game.MoveNode(leia, mission);

            _system = new JediSystem(_game, new FixedRNG(0.0));
            List<ForceDiscoveryResult> results = _system
                .ProcessTick()
                .OfType<ForceDiscoveryResult>()
                .Where(r => r.EventType == ForceEventType.ForceUserDiscovered)
                .ToList();

            Assert.IsFalse(leia.IsForceEligible);
            Assert.AreEqual(0, results.Count);
        }

        [Test]
        public void ProcessTick_HighRoll_DiscoveryFails()
        {
            // Rank 101 + 0 - 100 = 1%. MaxRNG returns 0.99 -> roll = 99.0 >= 1%
            Officer luke = CreateKnownJedi("LUKE", forceValue: 101);
            luke.IsDiscoveringForceUser = true;

            Officer leia = CreateDormantJedi("LEIA");

            _system = new JediSystem(_game, new MaxRNG());
            List<ForceDiscoveryResult> results = _system
                .ProcessTick()
                .OfType<ForceDiscoveryResult>()
                .Where(r => r.EventType == ForceEventType.ForceUserDiscovered)
                .ToList();

            Assert.IsFalse(leia.IsForceEligible);
            Assert.AreEqual(0, results.Count);
        }

        [Test]
        public void ProcessTick_OfficerWithTemplate_InitializesForceValue()
        {
            Officer luke = CreateKnownJedi("LUKE", forceValue: 120);
            luke.IsDiscoveringForceUser = true;

            Officer leia = CreateDormantJedi("LEIA");
            leia.JediLevel = 10;
            leia.JediLevelVariance = 5;

            // FixedRNG(0.0): NextDouble()=0.0 for discovery roll, NextInt(0, 6)=0 for ForceValue
            _system = new JediSystem(_game, new FixedRNG(0.0));
            _system.ProcessTick();

            Assert.IsTrue(leia.IsForceEligible);
            Assert.AreEqual(10, leia.ForceValue);
        }

        private Officer CreateKnownJedi(string id, int forceValue)
        {
            Officer officer = new Officer
            {
                InstanceID = id,
                DisplayName = id,
                OwnerInstanceID = _alliance.InstanceID,
                IsJedi = true,
                IsForceEligible = true,
                ForceValue = forceValue,
                ForceTrainingAdjustment = 0,
            };
            _game.AttachNode(officer, _tatooine);
            return officer;
        }

        private Officer CreateDormantJedi(string id)
        {
            Officer officer = new Officer
            {
                InstanceID = id,
                DisplayName = id,
                OwnerInstanceID = _alliance.InstanceID,
                IsJedi = true,
                IsForceEligible = false,
                ForceValue = 0,
                ForceTrainingAdjustment = 0,
                JediLevel = 10,
                JediLevelVariance = 0,
            };
            _game.AttachNode(officer, _tatooine);
            return officer;
        }

        [Test]
        public void ApplyForceGrowth_EligibleOfficer_GrowsForce()
        {
            Officer luke = new Officer
            {
                InstanceID = "LUKE",
                OwnerInstanceID = _alliance.InstanceID,
                GrowsForceOnMission = true,
                IsForceEligible = true,
                ForceValue = 10,
            };
            int before = luke.ForceValue;
            int growth = _game.Config.Jedi.ForceGrowthPerMission;

            _system.ApplyForceGrowth(new List<IMissionParticipant> { luke });

            Assert.AreEqual(before + growth, luke.ForceValue);
        }

        [Test]
        public void ApplyForceGrowth_NotForceEligible_NoGrowth()
        {
            Officer officer = new Officer
            {
                InstanceID = "O1",
                OwnerInstanceID = _alliance.InstanceID,
                GrowsForceOnMission = true,
                IsForceEligible = false,
                ForceValue = 10,
            };
            int before = officer.ForceValue;

            _system.ApplyForceGrowth(new List<IMissionParticipant> { officer });

            Assert.AreEqual(before, officer.ForceValue);
        }
    }

    /// <summary>
    /// RNG that always returns max - 1 from NextInt, used for testing max-roll paths.
    /// </summary>
    internal class MaxRNG : IRandomNumberProvider
    {
        public double NextDouble() => 0.99;

        public int NextInt(int min, int max) => max > min ? max - 1 : min;
    }
}
