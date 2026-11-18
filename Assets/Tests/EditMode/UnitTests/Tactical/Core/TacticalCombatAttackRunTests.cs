using System;
using System.Collections.Generic;
using System.Numerics;
using NUnit.Framework;
using Rebellion.Game.Results;
using Rebellion.Game.Units;
using Rebellion.Gameplay;
using Rebellion.Tactical;
using Rebellion.Util.Random;

namespace Rebellion.Tests.Tactical
{
    [TestFixture]
    public sealed class TacticalCombatAttackRunTests
    {
        [Test]
        public void ApplyState_StartingSuccessfulSelection_BeginsTwoMinuteSequence()
        {
            TacticalCombatAttackRun run = CreateRun(1);
            run.ApplyState(TacticalCombatAttackRunState.Starting);
            Assert.AreEqual(TacticalCombatAttackRunState.Succeeding, run.State);
            Assert.AreEqual(120000, run.RemainingMilliseconds);
        }

        [Test]
        public void Advance_TwoMinuteSuccessSequence_RequestsQueuedSuccess()
        {
            TacticalCombatAttackRun run = CreateRun(1);
            run.ApplyState(TacticalCombatAttackRunState.Starting);
            TacticalCombatAttackRunState? requested = null;
            run.StateChangeRequested += (_, state) => requested = state;
            run.Advance(120000);
            Assert.AreEqual(TacticalCombatAttackRunState.Succeeded, requested);
            Assert.AreEqual(TacticalCombatAttackRunState.Succeeding, run.State);
        }

        [Test]
        public void ApplyState_SuccessWithThreeSquadrons_DestroysFirstSquadron()
        {
            TacticalCombatAttackRun run = CreateRun(3);
            run.ApplyState(TacticalCombatAttackRunState.Starting);
            List<TacticalCombatUnit> destroyed = new();
            run.UnitDestroyed += destroyed.Add;
            run.ApplyState(TacticalCombatAttackRunState.Succeeded);
            Assert.AreEqual(1, destroyed.Count);
            Assert.AreEqual("fighter0", destroyed[0].InstanceID);
        }

        [Test]
        public void ApplyState_SuccessWithOneSquadron_PreservesSurvivor()
        {
            TacticalCombatAttackRun run = CreateRun(1);
            run.ApplyState(TacticalCombatAttackRunState.Starting);
            List<TacticalCombatUnit> destroyed = new();
            run.UnitDestroyed += destroyed.Add;
            run.ApplyState(TacticalCombatAttackRunState.Succeeded);
            Assert.IsEmpty(destroyed);
            Assert.AreEqual(TacticalCombatUnitStatus.Active, run.Group.Members[0].Status);
        }

        [Test]
        public void ApplyState_FailedAttack_DestroysEverySquadron()
        {
            TacticalCombatAttackRun run = CreateRun(3);
            run.ApplyState(TacticalCombatAttackRunState.Starting);
            List<TacticalCombatUnit> destroyed = new();
            run.UnitDestroyed += destroyed.Add;
            run.ApplyState(TacticalCombatAttackRunState.Failed);
            Assert.AreEqual(3, destroyed.Count);
        }

        [Test]
        public void ApplyState_CancelledAttack_RestoresParticipation()
        {
            TacticalCombatAttackRun run = CreateRun(1);
            run.ApplyState(TacticalCombatAttackRunState.Starting);
            run.ApplyState(TacticalCombatAttackRunState.Cancelled);
            Assert.AreEqual(TacticalCombatUnitStatus.Active, run.Group.Members[0].Status);
        }

        [Test]
        public void ApplyState_CancelledAttack_RequestsWithdrawalThroughOrderOwner()
        {
            TacticalCombatAttackRun run = CreateRun(1);
            List<(TacticalCombatGroup, TacticalCombatOrder)> requests = new();
            run.OrderRequested += (group, order) => requests.Add((group, order));

            run.ApplyState(TacticalCombatAttackRunState.Cancelled);

            CollectionAssert.AreEqual(
                new[] { (run.Group, TacticalCombatOrder.Withdraw) },
                requests
            );
        }

        [Test]
        public void ApplyState_SucceededAttack_RequestsClearOrderAfterMovieLaunch()
        {
            TacticalCombatAttackRun run = CreateRun(1);
            List<string> requests = new();
            run.MovieRequested += _ => requests.Add("movie");
            run.OrderRequested += (_, order) => requests.Add(order.ToString());

            run.ApplyState(TacticalCombatAttackRunState.Succeeded);

            CollectionAssert.AreEqual(new[] { "movie", "None" }, requests);
        }

        [TestCase(0, 313, 314, 315)]
        [TestCase(1, 301, 302, 303)]
        [TestCase(2, 305, 306, 307)]
        [TestCase(3, 309, 310, 311)]
        [TestCase(4, 309, 310, 311)]
        public void ApplyState_AllianceAttackLifecycle_AnnouncesRetainedCategoryBeforeMovie(
            int category,
            int approach,
            int start,
            int failure
        )
        {
            TacticalCombatAttackRun run = CreateRun(1, fighterCategory: category);
            List<int> messages = new();
            run.NotificationRequested += messages.Add;
            run.MovieRequested += _ =>
                CollectionAssert.AreEqual(new[] { approach, start, failure }, messages);

            run.Advance(0);
            run.Advance(0);
            run.ApplyState(TacticalCombatAttackRunState.Starting);
            run.ApplyState(TacticalCombatAttackRunState.Failed);

            CollectionAssert.AreEqual(new[] { approach, start, failure }, messages);
        }

        [Test]
        public void Advance_EmpireAttack_AnnouncesApproachAndStartWithoutTimedProgressCues()
        {
            TacticalCombatAttackRun run = CreateRun(1, playerFactionIndex: 1);
            List<int> messages = new();
            run.NotificationRequested += messages.Add;
            run.Advance(0);
            run.ApplyState(TacticalCombatAttackRunState.Starting);

            for (int tick = 0; tick < 10; tick++)
                run.Advance(12000);
            run.ApplyState(TacticalCombatAttackRunState.Failed);

            CollectionAssert.AreEqual(new[] { 277, 285 }, messages);
        }

        [Test]
        public void Advance_AllianceSuccessfulAttackWithoutLosses_UsesUndamagedProgressSequence()
        {
            TacticalCombatAttackRun run = CreateRun(1);
            run.ApplyState(TacticalCombatAttackRunState.Starting);
            Assert.AreEqual(TacticalCombatAttackRunState.Succeeding, run.State);
            List<int> messages = new();
            run.NotificationRequested += messages.Add;

            for (int tick = 0; tick < 10; tick++)
                run.Advance(12000);

            CollectionAssert.AreEqual(new[] { 286, 287, 288, 296, 289, 290, 297 }, messages);
        }

        [Test]
        public void Advance_AllianceSuccessfulAttackWithSelectionLoss_UsesDamagedProgressSequence()
        {
            SequenceRandom random = new(10, 1, 1);
            TacticalCombatAttackRun run = CreateRun(1, maneuver: 0, random: random);
            Assert.AreEqual(1, run.Group.Members[0].GetManeuverability());
            float? hullAtSelection = null;
            random.BeforeDraw = (_, max) =>
            {
                if (max == 101)
                    hullAtSelection = run.Group.Members[0].Systems.Hull;
            };
            run.ApplyState(TacticalCombatAttackRunState.Starting);
            Assert.AreEqual(TacticalCombatAttackRunState.Succeeding, run.State);
            Assert.AreEqual(3, random.DrawCount);
            Assert.AreEqual(88, hullAtSelection);
            Assert.AreEqual(96, run.Group.Members[0].Systems.Hull);
            List<int> messages = new();
            run.NotificationRequested += messages.Add;

            for (int tick = 0; tick < 10; tick++)
                run.Advance(12000);

            CollectionAssert.AreEqual(new[] { 286, 291, 292, 293, 298, 299, 294 }, messages);
        }

        [Test]
        public void Advance_AllianceFailedAttack_UsesFailureProgressAndFinalWarnings()
        {
            TacticalCombatAttackRun run = CreateRun(
                1,
                maneuver: 0,
                torpedoes: 0,
                random: new MinimumRandom()
            );
            run.ApplyState(TacticalCombatAttackRunState.Starting);
            Assert.AreEqual(TacticalCombatAttackRunState.Failing, run.State);
            List<int> messages = new();
            run.NotificationRequested += messages.Add;

            for (int tick = 0; tick < 10; tick++)
                run.Advance(12000);

            CollectionAssert.AreEqual(
                new[] { 286, 291, 292, 293, 298, 299, 294, 300, 295 },
                messages
            );
        }

        [Test]
        public void Advance_RepeatedFrameAtThreshold_RequestsProgressCueOnlyOnce()
        {
            TacticalCombatAttackRun run = CreateRun(1);
            run.ApplyState(TacticalCombatAttackRunState.Starting);
            List<int> messages = new();
            run.NotificationRequested += messages.Add;

            run.Advance(0);
            run.Advance(0);
            run.Advance(12000);
            run.Advance(0);
            run.Advance(0);

            CollectionAssert.AreEqual(new[] { 286, 287 }, messages);
        }

        /// <summary>
        /// Creates deterministic highly maneuverable attack squadrons.
        /// </summary>
        /// <param name="count">The number of squadrons.</param>
        /// <param name="playerFactionIndex">The faction receiving command notifications.</param>
        /// <param name="fighterCategory">The authored fighter command category.</param>
        /// <param name="maneuver">The squadron's agility rating.</param>
        /// <param name="torpedoes">The squadron's torpedo strength.</param>
        /// <param name="random">The controlled selection stream, or null for the seeded fixture stream.</param>
        /// <returns>The unstarted attack.</returns>
        private static TacticalCombatAttackRun CreateRun(
            int count,
            int playerFactionIndex = 0,
            int fighterCategory = 0,
            int maneuver = 20,
            int torpedoes = 100,
            IRandomNumberProvider random = null
        )
        {
            List<TacticalCombatUnit> fighters = new();
            for (int index = 0; index < count; index++)
                fighters.Add(
                    new TacticalCombatUnit(
                        TacticalCombatUnitCapture.Capture(
                            new Starfighter
                            {
                                InstanceID = "fighter" + index,
                                MaxSquadronSize = 12,
                                CurrentSquadronSize = 12,
                                Agility = maneuver,
                                Torpedoes = torpedoes,
                            }
                        ),
                        CombatSide.Attacker,
                        index + 1
                    )
                );
            TacticalCombatGroup group = new(10, fighters, Vector3.UnitZ)
            {
                FighterCategory = fighterCategory,
            };
            return new TacticalCombatAttackRun(
                group,
                random ?? new SystemRandomProvider(42),
                () => 1,
                playerFactionIndex
            );
        }

        private sealed class MinimumRandom : IRandomNumberProvider
        {
            /// <summary>
            /// Returns the minimum fractional draw.
            /// </summary>
            /// <returns>Zero.</returns>
            public double NextDouble() => 0;

            /// <summary>
            /// Returns the inclusive lower bound of the requested interval.
            /// </summary>
            /// <param name="min">The inclusive lower bound.</param>
            /// <param name="max">The exclusive upper bound.</param>
            /// <returns>The lower bound.</returns>
            public int NextInt(int min, int max) => min;
        }

        private sealed class SequenceRandom : IRandomNumberProvider
        {
            private readonly Queue<int> _values;
            public int DrawCount { get; private set; }
            public Action<int, int> BeforeDraw { get; set; }

            /// <summary>
            /// Captures the exact ordered draws used by a selection fixture.
            /// </summary>
            /// <param name="values">The integer results in request order.</param>
            public SequenceRandom(params int[] values) => _values = new Queue<int>(values);

            /// <summary>
            /// Returns the minimum fractional draw.
            /// </summary>
            /// <returns>Zero.</returns>
            public double NextDouble() => 0;

            /// <summary>
            /// Observes the current selection state and returns the next controlled integer.
            /// </summary>
            /// <param name="min">The inclusive lower bound.</param>
            /// <param name="max">The exclusive upper bound.</param>
            /// <returns>The next fixture draw.</returns>
            public int NextInt(int min, int max)
            {
                BeforeDraw?.Invoke(min, max);
                int value = _values.Dequeue();
                Assert.That(value, Is.GreaterThanOrEqualTo(min).And.LessThan(max));
                DrawCount++;
                return value;
            }
        }
    }
}
