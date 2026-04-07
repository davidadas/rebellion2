using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Rebellion.Core.Configuration;
using Rebellion.Game;
using Rebellion.Game.Results;
using Rebellion.SceneGraph;
using Rebellion.Core.Simulation;
using Rebellion.Systems;

namespace Rebellion.Tests.Systems
{
    /// <summary>
    /// Tests for JediSystem force discovery, mission growth, training catch-up,
    /// and force user discovery scanning.
    /// Validates deterministic threshold-based discovery and two-tier Jedi mechanics
    /// matching REBEXE.EXE.
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
            GameConfig config = ConfigLoader.LoadGameConfig();
            _game = new GameRoot(config);
            _system = new JediSystem(_game);

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

        // --- Discovery state tests ---

        [Test]
        public void ProcessTick_ForceRankAboveThreshold_EntersDiscoveringState()
        {
            Officer luke = CreateKnownJedi("LUKE", forceValue: 85);

            List<ForceDiscoveryResult> results = _system
                .ProcessTick(new FixedRNG())
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
                .ProcessTick(new FixedRNG())
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
                .ProcessTick(new FixedRNG())
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
                .ProcessTick(new FixedRNG())
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
                .ProcessTick(new FixedRNG())
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

            _system.ProcessTick(new FixedRNG());

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
                .ProcessTick(new FixedRNG())
                .OfType<ForceDiscoveryResult>()
                .ToList();

            Assert.AreEqual(0, results.Count);
        }

        [Test]
        public void ProcessTick_DormantJedi_NotForceEligible_Skipped()
        {
            Officer leia = CreateDormantJedi("LEIA");

            List<ForceDiscoveryResult> results = _system
                .ProcessTick(new FixedRNG())
                .OfType<ForceDiscoveryResult>()
                .ToList();

            Assert.IsFalse(leia.IsDiscoveringForceUser);
            Assert.AreEqual(0, results.Count);
        }

        [Test]
        public void ProcessTick_EmptyGame_NoEvents()
        {
            List<ForceDiscoveryResult> results = _system
                .ProcessTick(new FixedRNG())
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
                .ProcessTick(new FixedRNG())
                .OfType<ForceDiscoveryResult>()
                .ToList();

            Assert.IsTrue(luke.IsDiscoveringForceUser);
            Assert.IsTrue(vader.IsDiscoveringForceUser);
            Assert.AreEqual(2, results.Count);
        }

        [Test]
        public void ProcessTick_ForceRankIncludesTrainingAdjustment()
        {
            // ForceValue 70 alone is below threshold 80, but with adjustment of 15 => rank 85
            Officer luke = CreateKnownJedi("LUKE", forceValue: 70);
            luke.ForceTrainingAdjustment = 15;

            List<ForceDiscoveryResult> results = _system
                .ProcessTick(new FixedRNG())
                .OfType<ForceDiscoveryResult>()
                .ToList();

            Assert.IsTrue(luke.IsDiscoveringForceUser);
            Assert.AreEqual(1, results.Count);
            Assert.AreEqual(85, results[0].ForceRank);
        }

        // --- Mission force growth tests ---

        [Test]
        public void AwardMissionForceGrowth_ForceEligibleJedi_IncreasesForceValue()
        {
            Officer luke = CreateKnownJedi("LUKE", forceValue: 50);

            _system.AwardMissionForceGrowth(luke);

            Assert.AreEqual(50 + _game.Config.Jedi.ForceGrowthPerMission, luke.ForceValue);
        }

        [Test]
        public void AwardMissionForceGrowth_DormantJedi_NoChange()
        {
            Officer leia = CreateDormantJedi("LEIA");

            _system.AwardMissionForceGrowth(leia);

            Assert.AreEqual(0, leia.ForceValue);
        }

        [Test]
        public void AwardMissionForceGrowth_NonJedi_NoChange()
        {
            Officer han = new Officer
            {
                InstanceID = "HAN",
                DisplayName = "Han",
                IsJedi = false,
                ForceValue = 0,
            };

            _system.AwardMissionForceGrowth(han);

            Assert.AreEqual(0, han.ForceValue);
        }

        // --- Training catch-up tests ---

        [Test]
        public void ApplyTrainingCatchUp_TraineeBelowTeacher_GainsAdjustment()
        {
            Officer trainee = CreateKnownJedi("TRAINEE", forceValue: 50);
            Officer teacher = CreateKnownJedi("TEACHER", forceValue: 100);

            // Gap = 50, catch-up range = 50 * 50 / 100 = 25
            // FixedRNG.NextInt(0, 26) returns 0 (min)
            _system.ApplyTrainingCatchUp(trainee, teacher, new FixedRNG());

            // With min-returning RNG, bonus is 0
            Assert.AreEqual(0, trainee.ForceTrainingAdjustment);
        }

        [Test]
        public void ApplyTrainingCatchUp_TraineeAboveTeacher_NoGain()
        {
            Officer trainee = CreateKnownJedi("TRAINEE", forceValue: 120);
            Officer teacher = CreateKnownJedi("TEACHER", forceValue: 100);

            _system.ApplyTrainingCatchUp(trainee, teacher, new FixedRNG());

            Assert.AreEqual(0, trainee.ForceTrainingAdjustment);
        }

        [Test]
        public void ApplyTrainingCatchUp_TraineeEqualsTeacher_NoGain()
        {
            Officer trainee = CreateKnownJedi("TRAINEE", forceValue: 100);
            Officer teacher = CreateKnownJedi("TEACHER", forceValue: 100);

            _system.ApplyTrainingCatchUp(trainee, teacher, new FixedRNG());

            Assert.AreEqual(0, trainee.ForceTrainingAdjustment);
        }

        [Test]
        public void ApplyTrainingCatchUp_WithMaxRoll_GainsFullCatchUp()
        {
            Officer trainee = CreateKnownJedi("TRAINEE", forceValue: 50);
            Officer teacher = CreateKnownJedi("TEACHER", forceValue: 100);

            // Gap = 50, catch-up range = 50 * 50 / 100 = 25
            // MaxRNG returns max-1, so NextInt(0, 26) = 25
            _system.ApplyTrainingCatchUp(trainee, teacher, new MaxRNG());

            Assert.AreEqual(25, trainee.ForceTrainingAdjustment);
        }

        // --- ForceRankLabel tests ---

        [Test]
        public void GetForceRankLabel_BelowTen_ReturnsNone()
        {
            Officer officer = new Officer { IsJedi = true, ForceValue = 9 };
            Assert.AreEqual(ForceRankLabel.None, _system.GetForceRankLabel(officer));
        }

        [Test]
        public void GetForceRankLabel_AtTen_ReturnsNovice()
        {
            Officer officer = new Officer { IsJedi = true, ForceValue = 10 };
            Assert.AreEqual(ForceRankLabel.Novice, _system.GetForceRankLabel(officer));
        }

        [Test]
        public void GetForceRankLabel_AtTwenty_ReturnsTrainee()
        {
            Officer officer = new Officer { IsJedi = true, ForceValue = 20 };
            Assert.AreEqual(ForceRankLabel.Trainee, _system.GetForceRankLabel(officer));
        }

        [Test]
        public void GetForceRankLabel_AtEighty_ReturnsForceStudent()
        {
            Officer officer = new Officer { IsJedi = true, ForceValue = 80 };
            Assert.AreEqual(ForceRankLabel.ForceStudent, _system.GetForceRankLabel(officer));
        }

        [Test]
        public void GetForceRankLabel_AtHundred_ReturnsForceKnight()
        {
            Officer officer = new Officer { IsJedi = true, ForceValue = 100 };
            Assert.AreEqual(ForceRankLabel.ForceKnight, _system.GetForceRankLabel(officer));
        }

        [Test]
        public void GetForceRankLabel_AtOneTwenty_ReturnsForceMaster()
        {
            Officer officer = new Officer { IsJedi = true, ForceValue = 120 };
            Assert.AreEqual(ForceRankLabel.ForceMaster, _system.GetForceRankLabel(officer));
        }

        [Test]
        public void ForceRank_IsForceValuePlusTrainingAdjustment()
        {
            Officer officer = new Officer { ForceValue = 50, ForceTrainingAdjustment = 30 };
            Assert.AreEqual(80, officer.ForceRank);
        }

        // --- Force user discovery scan tests ---

        [Test]
        public void ScanForForceUsers_DiscoveringJediDiscoversDormant()
        {
            Officer luke = CreateKnownJedi("LUKE", forceValue: 120);
            luke.IsDiscoveringForceUser = true;

            Officer leia = CreateDormantJedi("LEIA");

            Mission mission = CreateMissionWithParticipants(luke, leia);

            List<GameResult> results = new List<GameResult>();
            // Probability = 120 + 0 - 100 = 20%. FixedRNG returns 0.0 which is < 20%.
            _system.ScanForForceUsers(mission, new FixedRNG(), results);

            Assert.IsTrue(leia.IsForceEligible);
            Assert.Greater(leia.ForceValue, 0);
            Assert.AreEqual(1, results.OfType<ForceDiscoveryResult>().Count());
            Assert.AreEqual(
                ForceEventType.ForceUserDiscovered,
                results.OfType<ForceDiscoveryResult>().First().EventType
            );
        }

        [Test]
        public void ScanForForceUsers_LowRank_CannotDiscover()
        {
            // Rank 50 + 0 - 100 = -50% -> impossible
            Officer luke = CreateKnownJedi("LUKE", forceValue: 50);
            luke.IsDiscoveringForceUser = true;

            Officer leia = CreateDormantJedi("LEIA");

            Mission mission = CreateMissionWithParticipants(luke, leia);

            List<GameResult> results = new List<GameResult>();
            _system.ScanForForceUsers(mission, new FixedRNG(), results);

            Assert.IsFalse(leia.IsForceEligible);
            Assert.AreEqual(0, leia.ForceValue);
            Assert.AreEqual(0, results.Count);
        }

        [Test]
        public void ScanForForceUsers_NonDiscoveringJedi_NoScan()
        {
            Officer luke = CreateKnownJedi("LUKE", forceValue: 120);
            luke.IsDiscoveringForceUser = false; // Not in discovering state

            Officer leia = CreateDormantJedi("LEIA");

            Mission mission = CreateMissionWithParticipants(luke, leia);

            List<GameResult> results = new List<GameResult>();
            _system.ScanForForceUsers(mission, new FixedRNG(), results);

            Assert.IsFalse(leia.IsForceEligible);
            Assert.AreEqual(0, results.Count);
        }

        [Test]
        public void ScanForForceUsers_AlreadyEligible_Skipped()
        {
            Officer luke = CreateKnownJedi("LUKE", forceValue: 120);
            luke.IsDiscoveringForceUser = true;

            Officer vader = CreateKnownJedi("VADER", forceValue: 100);

            Mission mission = CreateMissionWithParticipants(luke, vader);

            List<GameResult> results = new List<GameResult>();
            _system.ScanForForceUsers(mission, new FixedRNG(), results);

            // Vader is already force-eligible, so no discovery event
            Assert.AreEqual(0, results.Count);
        }

        [Test]
        public void ScanForForceUsers_CapturedCandidate_Skipped()
        {
            Officer luke = CreateKnownJedi("LUKE", forceValue: 120);
            luke.IsDiscoveringForceUser = true;

            Officer leia = CreateDormantJedi("LEIA");
            leia.IsCaptured = true;

            Mission mission = CreateMissionWithParticipants(luke, leia);

            List<GameResult> results = new List<GameResult>();
            _system.ScanForForceUsers(mission, new FixedRNG(), results);

            Assert.IsFalse(leia.IsForceEligible);
            Assert.AreEqual(0, results.Count);
        }

        [Test]
        public void ScanForForceUsers_ScansLocalOfficersOnPlanet()
        {
            Officer luke = CreateKnownJedi("LUKE", forceValue: 120);
            luke.IsDiscoveringForceUser = true;

            // Leia is NOT on the mission but stationed on Tatooine
            Officer leia = CreateDormantJedi("LEIA");

            // Mission only has Luke
            Mission mission = CreateMissionWithParticipants(luke);

            List<GameResult> results = new List<GameResult>();
            _system.ScanForForceUsers(mission, new FixedRNG(), results);

            // Luke should still find Leia at the planet
            Assert.IsTrue(leia.IsForceEligible);
            Assert.AreEqual(1, results.Count);
        }

        [Test]
        public void ScanForForceUsers_HighRoll_DiscoveryFails()
        {
            // Rank 101 + 0 - 100 = 1%. MaxRNG returns 0.99 -> roll = 99.0 >= 1%
            Officer luke = CreateKnownJedi("LUKE", forceValue: 101);
            luke.IsDiscoveringForceUser = true;

            Officer leia = CreateDormantJedi("LEIA");

            Mission mission = CreateMissionWithParticipants(luke, leia);

            List<GameResult> results = new List<GameResult>();
            _system.ScanForForceUsers(mission, new MaxRNG(), results);

            Assert.IsFalse(leia.IsForceEligible);
            Assert.AreEqual(0, results.Count);
        }

        [Test]
        public void ScanForForceUsers_InitializesForceValueFromTemplate()
        {
            Officer luke = CreateKnownJedi("LUKE", forceValue: 120);
            luke.IsDiscoveringForceUser = true;

            Officer leia = CreateDormantJedi("LEIA");
            leia.JediLevel = 10;
            leia.JediLevelVariance = 5;

            Mission mission = CreateMissionWithParticipants(luke, leia);

            List<GameResult> results = new List<GameResult>();
            // FixedRNG: NextInt(0, 6) = 0, so ForceValue = 10 + 0 = 10
            _system.ScanForForceUsers(mission, new FixedRNG(), results);

            Assert.IsTrue(leia.IsForceEligible);
            Assert.AreEqual(10, leia.ForceValue);
        }

        // --- Helper methods ---

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

        private Mission CreateMissionWithParticipants(params Officer[] participants)
        {
            List<IMissionParticipant> mainParticipants =
                participants.Cast<IMissionParticipant>().ToList();

            Mission mission = new DiplomacyMission(
                _alliance.InstanceID,
                _tatooine,
                mainParticipants,
                new List<IMissionParticipant>()
            );
            _game.AttachNode(mission, _tatooine);

            foreach (Officer officer in participants)
            {
                _game.MoveNode(officer, mission);
            }

            return mission;
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
