using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Rebellion.Game;
using Rebellion.Game.Units;
using Rebellion.Gameplay;

namespace Rebellion.Tests.Gameplay
{
    /// <summary>
    /// Verifies pairing order and probability without applying encounter consequences.
    /// </summary>
    [TestFixture]
    public sealed class MissionForceEncounterSelectorTests
    {
        private GameConfig.JediConfig _config;

        /// <summary>
        /// Configures the authored encounter probability offset.
        /// </summary>
        [SetUp]
        public void SetUp()
        {
            _config = new GameConfig.JediConfig { EncounterProbabilityOffset = -100 };
        }

        [Test]
        public void SelectPairs_AuthoredPriorities_PrefersLukeThenVader()
        {
            _config = TestContent.Data.GameConfig.Jedi;
            Officer luke = CreateOfficer("LUKE_SKYWALKER", 80);
            Officer vader = CreateOfficer("DARTH_VADER", 120);
            Officer participant = CreateOfficer("participant", 150);
            Officer defender = CreateOfficer("defender", 150);

            IReadOnlyList<(Officer Participant, Officer Defender)> pairs =
                new MissionForceEncounterSelector(_config, new FixedRNG(0)).SelectPairs(
                    new[] { participant, luke },
                    new[] { defender, vader }
                );

            Assert.AreEqual((luke, vader), pairs[0]);
            Assert.AreEqual((participant, defender), pairs[1]);
        }

        [TestCase(0)]
        [TestCase(10)]
        public void SelectPairs_UnmatchedParticipant_RetriesWithNextDefender(int firstChance)
        {
            _config.MissionEncounterPriorityOfficerInstanceIDs.AddRange(
                new[] { "first", "second" }
            );
            Officer participant = CreateOfficer("participant", 40);
            Officer first = CreateOfficer("first", 60 + firstChance);
            Officer second = CreateOfficer("second", 150);

            IReadOnlyList<(Officer Participant, Officer Defender)> pairs =
                new MissionForceEncounterSelector(
                    _config,
                    new FixedRandomProvider(new[] { 0.5 })
                ).SelectPairs(new[] { participant }, new[] { first, second });

            Assert.AreEqual(1, pairs.Count);
            Assert.AreEqual((participant, second), pairs[0]);
        }

        [Test]
        public void SelectPairs_MajorAndMinorParticipants_SelectsMajorFirst()
        {
            Officer major = CreateOfficer("major", 100);
            Officer minor = CreateOfficer("minor", 150, isMain: false);
            Officer defender = CreateOfficer("defender", 150);

            IReadOnlyList<(Officer Participant, Officer Defender)> pairs =
                new MissionForceEncounterSelector(_config, new FixedRNG(0)).SelectPairs(
                    new[] { minor, major },
                    new[] { defender }
                );

            Assert.AreEqual(major, pairs.Single().Participant);
        }

        [Test]
        public void SelectPairs_MajorAndMinorDefenders_SelectsMajorFirst()
        {
            Officer participant = CreateOfficer("participant", 150);
            Officer major = CreateOfficer("major", 100);
            Officer minor = CreateOfficer("minor", 150, isMain: false);

            IReadOnlyList<(Officer Participant, Officer Defender)> pairs =
                new MissionForceEncounterSelector(_config, new FixedRNG(0)).SelectPairs(
                    new[] { participant },
                    new[] { minor, major }
                );

            Assert.AreEqual(major, pairs.Single().Defender);
        }

        [Test]
        public void SelectPairs_PreferredMinor_DoesNotBypassMajorPool()
        {
            Officer major = CreateOfficer("major", 100);
            Officer minor = CreateOfficer("minor", 150, isMain: false);
            Officer defender = CreateOfficer("defender", 150);
            _config.MissionEncounterPriorityOfficerInstanceIDs.Add(minor.InstanceID);

            IReadOnlyList<(Officer Participant, Officer Defender)> pairs =
                new MissionForceEncounterSelector(_config, new FixedRNG(0)).SelectPairs(
                    new[] { minor, major },
                    new[] { defender }
                );

            Assert.AreEqual(major, pairs.Single().Participant);
        }

        [Test]
        public void SelectPairs_MoreDefendersThanParticipants_DoesNotReuseMatchedParticipant()
        {
            Officer participant = CreateOfficer("participant", 150);
            Officer first = CreateOfficer("first", 150);
            Officer second = CreateOfficer("second", 150);

            IReadOnlyList<(Officer Participant, Officer Defender)> pairs =
                new MissionForceEncounterSelector(_config, new FixedRNG(0)).SelectPairs(
                    new[] { participant },
                    new[] { first, second }
                );

            Assert.AreEqual(1, pairs.Count);
        }

        [Test]
        public void SelectPairs_MoreParticipantsThanDefenders_DoesNotReuseMatchedDefender()
        {
            Officer first = CreateOfficer("first", 150);
            Officer second = CreateOfficer("second", 150);
            Officer defender = CreateOfficer("defender", 150);

            IReadOnlyList<(Officer Participant, Officer Defender)> pairs =
                new MissionForceEncounterSelector(_config, new FixedRNG(0)).SelectPairs(
                    new[] { first, second },
                    new[] { defender }
                );

            Assert.AreEqual(1, pairs.Count);
        }

        [TestCase(0, 150)]
        [TestCase(150, 0)]
        [TestCase(40, 60)]
        [TestCase(-20, 120)]
        public void SelectPairs_ZeroRankOrNonpositiveChance_DoesNotRollEncounter(
            int participantRank,
            int defenderRank
        )
        {
            Officer participant = CreateOfficer("participant", participantRank);
            Officer defender = CreateOfficer("defender", defenderRank);
            _config.MissionEncounterPriorityOfficerInstanceIDs.AddRange(
                new[] { participant.InstanceID, defender.InstanceID }
            );

            IReadOnlyList<(Officer Participant, Officer Defender)> pairs =
                new MissionForceEncounterSelector(_config, new ThrowingRNG()).SelectPairs(
                    new[] { participant },
                    new[] { defender }
                );

            Assert.IsEmpty(pairs);
        }

        [TestCase(0.29, true)]
        [TestCase(0.30, false)]
        [TestCase(0.99, false)]
        public void SelectPairs_PositiveChance_UsesSumAndConfiguredOffset(
            double roll,
            bool expectedMatch
        )
        {
            Officer participant = CreateOfficer("participant", 50);
            Officer defender = CreateOfficer("defender", 80);

            IReadOnlyList<(Officer Participant, Officer Defender)> pairs =
                new MissionForceEncounterSelector(
                    _config,
                    new FixedRandomProvider(new[] { roll })
                ).SelectPairs(new[] { participant }, new[] { defender });

            Assert.AreEqual(expectedMatch, pairs.Count == 1);
        }

        /// <summary>
        /// Creates a scoped officer with a stored Force rank and major/minor classification.
        /// </summary>
        /// <param name="instanceID">The candidate's identity.</param>
        /// <param name="forceRank">The candidate's effective Force rank.</param>
        /// <param name="isMain">Whether the officer belongs to the major pool.</param>
        /// <returns>The configured officer.</returns>
        private static Officer CreateOfficer(string instanceID, int forceRank, bool isMain = true)
        {
            return new Officer
            {
                InstanceID = instanceID,
                ForceValue = forceRank,
                IsMain = isMain,
            };
        }
    }
}
