using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Rebellion.Game;
using Rebellion.Game.Factions;
using Rebellion.Game.Galaxy;
using Rebellion.Game.GameEvents;
using Rebellion.Game.Missions;
using Rebellion.Game.Results;
using Rebellion.Game.Scripting;
using Rebellion.Game.Units;
using Rebellion.Gameplay;
using Rebellion.SceneGraph;

namespace Rebellion.Tests.Gameplay
{
    /// <summary>
    /// Tests for JediCommands force discovery state and force user discovery scanning.
    /// </summary>
    [TestFixture]
    public class JediCommandsTests
    {
        private GameRoot _game;
        private JediCommands _system;
        private Faction _alliance;
        private Faction _empire;
        private Planet _tatooine;

        /// <summary>
        /// Sets up.
        /// </summary>
        [SetUp]
        public void SetUp()
        {
            GameConfig config = TestContent.Data.GameConfig;
            _game = TestGame.Create(config);
            _system = new JediCommands(_game, new FixedRNG());

            _alliance = new Faction { InstanceID = "FNALL1", DisplayName = "Alliance" };
            _empire = new Faction { InstanceID = "FNEMP1", DisplayName = "Empire" };
            _game.GetFactions().Add(_alliance);
            _game.GetFactions().Add(_empire);

            PlanetSector planetSector = new PlanetSector
            {
                InstanceID = "TATOO",
                PositionX = 0,
                PositionY = 0,
            };
            _game.AttachNode(planetSector, _game.GetGalaxyMap());

            _tatooine = new Planet
            {
                InstanceID = "TATOOINE",
                DisplayName = "Tatooine",
                OwnerInstanceID = "FNALL1",
                IsColonized = true,
            };
            _game.AttachNode(_tatooine, planetSector);
        }

        [TestCase("LUKE_SKYWALKER", true)]
        [TestCase("DARTH_VADER", true)]
        [TestCase("EMPEROR_PALPATINE", false)]
        [TestCase("LEIA_ORGANA", false)]
        public void ProcessTick_AuthoredOfficerAtDiscoveryRank_UsesTrainerQualification(
            string instanceID,
            bool expectedDiscovery
        )
        {
            Officer officer = (Officer)
                TestContent
                    .Data.Officers.Single(template => template.InstanceID == instanceID)
                    .CreateCopy();
            officer.OwnerInstanceID = _alliance.InstanceID;
            officer.ForceValue = _game.Config.Jedi.DiscoveringForceUserThreshold;
            _game.AttachNode(officer, _tatooine);

            new JediTickProcessor(_system).ProcessTick(_game);

            Assert.AreEqual(expectedDiscovery, officer.IsDiscoveringForceUser);
        }

        [Test]
        public void ProcessTick_ForceRankAboveThreshold_EntersDiscoveringState()
        {
            Officer luke = CreateJediTrainer("LUKE", forceValue: 85);

            List<ForceDiscoveryResult> results = new JediTickProcessor(_system)
                .ProcessTick(_game)
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
            Officer luke = CreateJediTrainer("LUKE", forceValue: 80);

            List<ForceDiscoveryResult> results = new JediTickProcessor(_system)
                .ProcessTick(_game)
                .OfType<ForceDiscoveryResult>()
                .ToList();

            Assert.IsTrue(luke.IsDiscoveringForceUser);
            Assert.AreEqual(1, results.Count);
        }

        [Test]
        public void ProcessTick_ForceRankBelowThreshold_NoDiscovery()
        {
            Officer luke = CreateJediTrainer("LUKE", forceValue: 79);

            List<ForceDiscoveryResult> results = new JediTickProcessor(_system)
                .ProcessTick(_game)
                .OfType<ForceDiscoveryResult>()
                .ToList();

            Assert.IsFalse(luke.IsDiscoveringForceUser);
            Assert.AreEqual(0, results.Count);
        }

        [Test]
        public void ProcessTick_CapturedOfficer_NoDiscoveringState()
        {
            Officer luke = CreateJediTrainer("LUKE", forceValue: 100);
            luke.IsCaptured = true;

            List<ForceDiscoveryResult> results = new JediTickProcessor(_system)
                .ProcessTick(_game)
                .OfType<ForceDiscoveryResult>()
                .ToList();

            Assert.IsFalse(luke.IsDiscoveringForceUser);
            Assert.AreEqual(0, results.Count);
        }

        [Test]
        public void ProcessTick_AlreadyDiscovering_NoRepeatedEvent()
        {
            Officer luke = CreateJediTrainer("LUKE", forceValue: 100);
            luke.IsDiscoveringForceUser = true;

            List<ForceDiscoveryResult> results = new JediTickProcessor(_system)
                .ProcessTick(_game)
                .OfType<ForceDiscoveryResult>()
                .ToList();

            Assert.IsTrue(luke.IsDiscoveringForceUser);
            Assert.AreEqual(0, results.Count);
        }

        [Test]
        public void ProcessTick_ForceRankDropsBelowThreshold_ClearsDiscoveringState()
        {
            Officer luke = CreateJediTrainer("LUKE", forceValue: 50);
            luke.IsDiscoveringForceUser = true; // Was set previously

            new JediTickProcessor(_system).ProcessTick(_game);

            Assert.IsFalse(luke.IsDiscoveringForceUser);
        }

        [Test]
        public void ProcessTick_NonJediOfficer_ClearsDiscoveringState()
        {
            Officer han = new Officer
            {
                InstanceID = "HAN",
                DisplayName = "Han",
                OwnerInstanceID = _alliance.InstanceID,
                IsForceSensitive = false,
                IsForceEligible = true,
                ForceValue = 100,
                IsDiscoveringForceUser = true,
            };
            _game.AttachNode(han, _tatooine);

            List<ForceDiscoveryResult> results = new JediTickProcessor(_system)
                .ProcessTick(_game)
                .OfType<ForceDiscoveryResult>()
                .ToList();

            Assert.IsFalse(han.IsDiscoveringForceUser);
            Assert.IsEmpty(results);
        }

        [Test]
        public void ProcessTick_NonTrainerJedi_ClearsDiscoveringState()
        {
            Officer luke = CreateKnownJedi("LUKE", forceValue: 100);
            luke.IsDiscoveringForceUser = true;

            List<ForceDiscoveryResult> results = new JediTickProcessor(_system)
                .ProcessTick(_game)
                .OfType<ForceDiscoveryResult>()
                .ToList();

            Assert.IsFalse(luke.IsDiscoveringForceUser);
            Assert.IsEmpty(results);
        }

        [Test]
        public void ProcessTick_ForceIneligibleJedi_ClearsDiscoveringState()
        {
            Officer leia = CreateDormantJedi("LEIA");
            leia.IsJediTrainer = true;
            leia.IsDiscoveringForceUser = true;

            List<ForceDiscoveryResult> results = new JediTickProcessor(_system)
                .ProcessTick(_game)
                .OfType<ForceDiscoveryResult>()
                .ToList();

            Assert.IsFalse(leia.IsDiscoveringForceUser);
            Assert.IsEmpty(results);
        }

        [Test]
        public void ProcessTick_EmptyGame_NoEvents()
        {
            List<ForceDiscoveryResult> results = new JediTickProcessor(_system)
                .ProcessTick(_game)
                .OfType<ForceDiscoveryResult>()
                .ToList();

            Assert.AreEqual(0, results.Count);
        }

        [Test]
        public void ProcessTick_MultipleOfficers_AllProcessed()
        {
            Officer luke = CreateJediTrainer("LUKE", forceValue: 85);
            Officer vader = CreateJediTrainer("VADER", forceValue: 120);

            List<ForceDiscoveryResult> results = new JediTickProcessor(_system)
                .ProcessTick(_game)
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
            Officer luke = CreateJediTrainer("LUKE", forceValue: 70);
            luke.ForceTrainingAdjustment = 15;

            List<ForceDiscoveryResult> results = new JediTickProcessor(_system)
                .ProcessTick(_game)
                .OfType<ForceDiscoveryResult>()
                .ToList();

            Assert.IsTrue(luke.IsDiscoveringForceUser);
            Assert.AreEqual(1, results.Count);
            Assert.AreEqual(85, results[0].ForceRank);
        }

        [Test]
        public void ProcessTick_DiscoveringJediWithDormantCandidate_DiscoversDormant()
        {
            Officer luke = CreateJediTrainer("LUKE", forceValue: 120);
            luke.IsDiscoveringForceUser = true;

            Officer leia = CreateDormantJedi("LEIA");

            _system = new JediCommands(_game, new FixedRNG(0.0));
            List<ForceDiscoveryResult> results = new JediTickProcessor(_system)
                .ProcessTick(_game)
                .OfType<ForceDiscoveryResult>()
                .Where(r => r.EventType == ForceEventType.ForceUserDiscovered)
                .ToList();

            Assert.IsTrue(leia.IsForceEligible);
            Assert.Greater(leia.ForceValue, 0);
            Assert.AreEqual(1, results.Count);
            Assert.AreEqual(luke, results[0].Discoverer);
        }

        [Test]
        public void ProcessTick_EnemyDormantCandidate_DoesNotDiscover()
        {
            CreateJediTrainer("LUKE", forceValue: 120);
            Fleet fleet = new Fleet(_empire.InstanceID, "Imperial Fleet")
            {
                InstanceID = "IMPERIAL_FLEET",
            };
            CapitalShip ship = new CapitalShip
            {
                InstanceID = "IMPERIAL_SHIP",
                OwnerInstanceID = _empire.InstanceID,
            };
            Officer vader = new Officer
            {
                InstanceID = "VADER",
                DisplayName = "VADER",
                OwnerInstanceID = _empire.InstanceID,
                IsForceSensitive = true,
                IsForceEligible = false,
                JediLevel = 10,
            };
            _game.AttachNode(fleet, _tatooine);
            _game.AttachNode(ship, fleet);
            _game.AttachNode(vader, ship);
            _system = new JediCommands(_game, new FixedRNG(0.0));

            List<ForceDiscoveryResult> results = new JediTickProcessor(_system)
                .ProcessTick(_game)
                .OfType<ForceDiscoveryResult>()
                .Where(result => result.EventType == ForceEventType.ForceUserDiscovered)
                .ToList();

            Assert.IsFalse(vader.IsForceEligible);
            Assert.IsEmpty(results);
        }

        [Test]
        public void ProcessTick_ScannerAtDiscoveryThreshold_DiscoversCandidate()
        {
            Officer luke = CreateJediTrainer(
                "LUKE",
                _game.Config.Jedi.DiscoveringForceUserThreshold
            );
            luke.IsDiscoveringForceUser = true;

            Officer leia = CreateDormantJedi("LEIA");

            List<ForceDiscoveryResult> results = new JediTickProcessor(_system)
                .ProcessTick(_game)
                .OfType<ForceDiscoveryResult>()
                .Where(r => r.EventType == ForceEventType.ForceUserDiscovered)
                .ToList();

            Assert.IsTrue(luke.IsDiscoveringForceUser);
            Assert.IsTrue(leia.IsForceEligible);
            Assert.AreEqual(10, leia.ForceValue);
            Assert.AreEqual(1, results.Count);
        }

        [Test]
        public void ProcessTick_BelowThresholdJedi_NoScan()
        {
            // ForceRank 50 is below threshold 80, so no discovering state
            Officer luke = CreateJediTrainer("LUKE", forceValue: 50);

            Officer leia = CreateDormantJedi("LEIA");

            _system = new JediCommands(_game, new FixedRNG(0.0));
            List<ForceDiscoveryResult> results = new JediTickProcessor(_system)
                .ProcessTick(_game)
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
            Officer luke = CreateJediTrainer("LUKE", forceValue: 120);
            luke.IsDiscoveringForceUser = true;

            Officer vader = CreateKnownJedi("VADER", forceValue: 100);

            _system = new JediCommands(_game, new FixedRNG(0.0));
            List<ForceDiscoveryResult> results = new JediTickProcessor(_system)
                .ProcessTick(_game)
                .OfType<ForceDiscoveryResult>()
                .Where(r => r.EventType == ForceEventType.ForceUserDiscovered)
                .ToList();

            Assert.AreEqual(0, results.Count);
        }

        [Test]
        public void ProcessTick_CapturedCandidate_Skipped()
        {
            Officer luke = CreateJediTrainer("LUKE", forceValue: 120);
            luke.IsDiscoveringForceUser = true;

            Officer leia = CreateDormantJedi("LEIA");
            leia.IsCaptured = true;

            _system = new JediCommands(_game, new FixedRNG(0.0));
            List<ForceDiscoveryResult> results = new JediTickProcessor(_system)
                .ProcessTick(_game)
                .OfType<ForceDiscoveryResult>()
                .Where(r => r.EventType == ForceEventType.ForceUserDiscovered)
                .ToList();

            Assert.IsFalse(leia.IsForceEligible);
            Assert.AreEqual(0, results.Count);
        }

        [Test]
        public void ProcessTick_OnMissionCandidate_Skipped()
        {
            Officer luke = CreateJediTrainer("LUKE", forceValue: 120);
            luke.IsDiscoveringForceUser = true;

            Officer leia = CreateDormantJedi("LEIA");

            // Put Leia on a mission — she should be skipped
            StubMission mission = new StubMission(_alliance.InstanceID, _tatooine.InstanceID);
            _game.AttachNode(mission, _tatooine);
            _game.MoveNode(leia, mission);

            _system = new JediCommands(_game, new FixedRNG(0.0));
            List<ForceDiscoveryResult> results = new JediTickProcessor(_system)
                .ProcessTick(_game)
                .OfType<ForceDiscoveryResult>()
                .Where(r => r.EventType == ForceEventType.ForceUserDiscovered)
                .ToList();

            Assert.IsFalse(leia.IsForceEligible);
            Assert.AreEqual(0, results.Count);
        }

        [Test]
        public void ProcessTick_MaximumRandomRoll_DiscoversCandidate()
        {
            Officer luke = CreateJediTrainer("LUKE", forceValue: 101);
            luke.IsDiscoveringForceUser = true;

            Officer leia = CreateDormantJedi("LEIA");

            _system = new JediCommands(_game, new MaximumRNG());
            List<ForceDiscoveryResult> results = new JediTickProcessor(_system)
                .ProcessTick(_game)
                .OfType<ForceDiscoveryResult>()
                .Where(r => r.EventType == ForceEventType.ForceUserDiscovered)
                .ToList();

            Assert.IsTrue(leia.IsForceEligible);
            Assert.AreEqual(1, results.Count);
        }

        [Test]
        public void ProcessTick_OfficerWithTemplate_InitializesForceValue()
        {
            Officer luke = CreateJediTrainer("LUKE", forceValue: 120);
            luke.IsDiscoveringForceUser = true;

            Officer leia = CreateDormantJedi("LEIA");
            leia.JediLevel = 10;
            leia.JediLevelVariance = 5;

            _system = new JediCommands(_game, new FixedRNG(0.0));
            new JediTickProcessor(_system).ProcessTick(_game);

            Assert.IsTrue(leia.IsForceEligible);
            Assert.AreEqual(10, leia.ForceValue);
        }

        [TestCase(25)]
        [TestCase(5)]
        [TestCase(-1)]
        public void ProcessTick_DormantOfficerWithForceValue_PreservesValueWithoutRandomRoll(
            int forceValue
        )
        {
            Officer luke = CreateJediTrainer("LUKE", forceValue: 120);
            luke.IsDiscoveringForceUser = true;
            Officer leia = CreateDormantJedi("LEIA");
            leia.ForceValue = forceValue;
            _system = new JediCommands(_game, new ThrowingRNG());

            ForceExperienceResult result = new JediTickProcessor(_system)
                .ProcessTick(_game)
                .OfType<ForceExperienceResult>()
                .Single(r => r.Officer == leia);

            Assert.IsTrue(leia.IsForceEligible);
            Assert.AreEqual(forceValue, leia.ForceValue);
            Assert.Zero(result.ExperienceGained);
        }

        [TestCase("LUKE_SKYWALKER", false, false)]
        [TestCase("LUKE_SKYWALKER", true, true)]
        [TestCase("OTHER_TRAINER", true, false)]
        public void ProcessTick_AuthoredDiscoveryRequirements_RequiresDiscovererAndEncounter(
            string discovererID,
            bool hasEncountered,
            bool expectedDiscovery
        )
        {
            CreateJediTrainer(discovererID, forceValue: 80);
            Officer leia = CreateDormantJedi("LEIA_ORGANA");
            Officer template = TestContent.Data.Officers.Single(officer =>
                officer.InstanceID == leia.InstanceID
            );
            leia.ForceDiscoveryConditionals = template.ForceDiscoveryConditionals;
            _game.EventRuntime.GetState("LUKE_VADER_ENCOUNTER_EFFECTS").ActivationCount =
                hasEncountered ? 1 : 0;

            new JediTickProcessor(_system).ProcessTick(_game);
            _game.CurrentTick++;
            new JediTickProcessor(_system).ProcessTick(_game);

            Assert.AreEqual(100, template.JediProbability);
            Assert.AreEqual(expectedDiscovery, leia.IsForceEligible);
        }

        [Test]
        public void ProcessTick_AuthoredDiscoveryRequirementsAfterSave_PreservesEligibilityGate()
        {
            CreateJediTrainer("OTHER_TRAINER", forceValue: 120);
            Officer leia = CreateDormantJedi("LEIA_ORGANA");
            leia.ForceDiscoveryConditionals.Add(
                new BindingIncludesUnitConditional
                {
                    Binding = "$discoverer",
                    UnitInstanceID = "LUKE_SKYWALKER",
                }
            );
            string xml = SerializationHelper.Serialize(leia);
            Officer restored = SerializationHelper.Deserialize<Officer>(xml);
            _game.DeleteNode(leia);
            _game.AttachNode(restored, _tatooine);

            new JediTickProcessor(_system).ProcessTick(_game);

            Assert.IsFalse(restored.IsForceEligible);
        }

        [TestCase(0, 0, true)]
        [TestCase(1, 1, true)]
        [TestCase(1, 2, false)]
        [TestCase(1, 0, false)]
        [TestCase(0, 1, false)]
        public void ProcessTick_OfficersAtSamePlanet_RequiresSameImmediateContainer(
            int scannerLocation,
            int candidateLocation,
            bool expectedDiscovery
        )
        {
            Officer scanner = CreateJediTrainer("TRAINER", forceValue: 80);
            Officer candidate = CreateDormantJedi("STUDENT");
            Fleet fleet = new Fleet(_alliance.InstanceID, "Fleet") { InstanceID = "FLEET" };
            _game.AttachNode(fleet, _tatooine);
            CapitalShip firstShip = new CapitalShip
            {
                InstanceID = "FIRST_SHIP",
                OwnerInstanceID = _alliance.InstanceID,
                ManufacturingStatus = ManufacturingStatus.Complete,
            };
            CapitalShip secondShip = new CapitalShip
            {
                InstanceID = "SECOND_SHIP",
                OwnerInstanceID = _alliance.InstanceID,
                ManufacturingStatus = ManufacturingStatus.Complete,
            };
            _game.AttachNode(firstShip, fleet);
            _game.AttachNode(secondShip, fleet);
            ISceneNode[] locations = { _tatooine, firstShip, secondShip };
            _game.MoveNode(scanner, locations[scannerLocation]);
            _game.MoveNode(candidate, locations[candidateLocation]);

            new JediTickProcessor(_system).ProcessTick(_game);
            _game.CurrentTick++;
            new JediTickProcessor(_system).ProcessTick(_game);

            Assert.AreEqual(expectedDiscovery, candidate.IsForceEligible);
        }

        [Test]
        public void ProcessTick_HiddenOfficersRegisteredOutOfOrder_DiscoversInRosterOrder()
        {
            Officer scanner = CreateJediTrainer("TRAINER", forceValue: 80);
            scanner.IsDiscoveringForceUser = true;
            Officer third = CreateDormantJedi("THIRD");
            third.RosterOrder = 30;
            Officer first = CreateDormantJedi("FIRST");
            first.RosterOrder = 10;
            Officer second = CreateDormantJedi("SECOND");
            second.RosterOrder = 20;
            foreach (Officer candidate in new[] { first, second, third })
                candidate.JediLevelVariance = 10;
            _system = new JediCommands(_game, new SequenceRNG(intValues: new[] { 1, 2, 3 }));

            List<ForceDiscoveryResult> results = new JediTickProcessor(_system)
                .ProcessTick(_game)
                .OfType<ForceDiscoveryResult>()
                .Where(result => result.EventType == ForceEventType.ForceUserDiscovered)
                .ToList();

            CollectionAssert.AreEqual(
                new[] { first, second, third },
                results.Select(result => result.Officer)
            );
            Assert.AreEqual(11, first.ForceValue);
            Assert.AreEqual(12, second.ForceValue);
            Assert.AreEqual(13, third.ForceValue);
        }

        [Test]
        public void ProcessTick_NewScanner_WaitsUntilFollowingTick()
        {
            Officer scanner = CreateJediTrainer("TRAINER", forceValue: 80);
            Officer candidate = CreateDormantJedi("STUDENT");

            new JediTickProcessor(_system).ProcessTick(_game);

            Assert.IsTrue(scanner.IsDiscoveringForceUser);
            Assert.IsFalse(candidate.IsForceEligible);

            _game.CurrentTick++;
            new JediTickProcessor(_system).ProcessTick(_game);

            Assert.IsTrue(candidate.IsForceEligible);
        }

        [TestCase(true)]
        [TestCase(false)]
        public void ProcessTick_OfficerInTransit_DoesNotDiscover(bool scannerInTransit)
        {
            Officer scanner = CreateJediTrainer("TRAINER", forceValue: 120);
            Officer candidate = CreateDormantJedi("STUDENT");
            Officer moving = scannerInTransit ? scanner : candidate;
            moving.Movement = new MovementState();

            new JediTickProcessor(_system).ProcessTick(_game);

            Assert.IsFalse(candidate.IsForceEligible);
        }

        [Test]
        public void ProcessTick_InactiveTrainer_ClearsDiscoveringState()
        {
            Officer scanner = CreateJediTrainer("TRAINER", forceValue: 120);
            scanner.IsDiscoveringForceUser = true;
            scanner.IsEnabled = false;
            Officer candidate = CreateDormantJedi("STUDENT");

            new JediTickProcessor(_system).ProcessTick(_game);

            Assert.IsFalse(scanner.IsDiscoveringForceUser);
            Assert.IsFalse(candidate.IsForceEligible);
        }

        /// <summary>
        /// Creates jedi trainer.
        /// </summary>
        /// <param name="id">The id.</param>
        /// <param name="forceValue">The force value.</param>
        /// <returns>The created jedi trainer.</returns>
        private Officer CreateJediTrainer(string id, int forceValue)
        {
            Officer officer = CreateKnownJedi(id, forceValue);
            officer.IsJediTrainer = true;
            return officer;
        }

        /// <summary>
        /// Creates known jedi.
        /// </summary>
        /// <param name="id">The id.</param>
        /// <param name="forceValue">The force value.</param>
        /// <returns>The created known jedi.</returns>
        private Officer CreateKnownJedi(string id, int forceValue)
        {
            Officer officer = new Officer
            {
                InstanceID = id,
                DisplayName = id,
                OwnerInstanceID = _alliance.InstanceID,
                IsForceSensitive = true,
                IsForceEligible = true,
                ForceValue = forceValue,
                ForceTrainingAdjustment = 0,
            };
            _game.AttachNode(officer, _tatooine);
            return officer;
        }

        /// <summary>
        /// Creates dormant jedi.
        /// </summary>
        /// <param name="id">The id.</param>
        /// <returns>The created dormant jedi.</returns>
        private Officer CreateDormantJedi(string id)
        {
            Officer officer = new Officer
            {
                InstanceID = id,
                DisplayName = id,
                OwnerInstanceID = _alliance.InstanceID,
                IsForceSensitive = true,
                IsForceEligible = false,
                ForceValue = 0,
                ForceTrainingAdjustment = 0,
                JediLevel = 10,
                JediLevelVariance = 0,
            };
            _game.AttachNode(officer, _tatooine);
            return officer;
        }
    }
}
