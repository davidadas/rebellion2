using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using NUnit.Framework;
using Rebellion.Game.Results;
using Rebellion.Gameplay;

namespace Rebellion.Tests.Gameplay
{
    [TestFixture]
    public sealed class TacticalCombatArrivalTests
    {
        [Test]
        public void Advance_ExplicitEnteringSide_PresentsOnlyThatSide()
        {
            Presentation presentation = new();
            TacticalCombatArrival arrival = Create(
                presentation,
                CombatSide.Defender,
                Member(4, CombatSide.Attacker),
                Member(5, CombatSide.Defender)
            );
            arrival.Advance(500);
            CollectionAssert.AreEqual(
                new[] { 5, 5 },
                presentation.Poses.Select(pose => pose.Index)
            );
        }

        [Test]
        public void Advance_DockedMember_DoesNotResetPresentOrRequestSound()
        {
            Presentation presentation = new();
            TacticalCombatArrival arrival = Create(
                presentation,
                CombatSide.Attacker,
                Member(4, docked: true)
            );
            arrival.Advance(500);
            CollectionAssert.AreEqual(new[] { "strobe:True" }, presentation.Events);
        }

        [Test]
        public void Advance_FirstFrame_ResetsAllMembersBeforeOrderedSoundAndTransforms()
        {
            Presentation presentation = new();
            TacticalCombatArrival arrival = Create(
                presentation,
                CombatSide.Attacker,
                Member(4),
                Member(5, fighter: true)
            );
            arrival.Advance(500);
            CollectionAssert.AreEqual(
                new[]
                {
                    "strobe:True",
                    "reset:4",
                    "pose:4",
                    "refresh:4",
                    "reset:5",
                    "pose:5",
                    "refresh:5",
                    "sound:30",
                    "pose:4",
                    "refresh:4",
                    "sound:28",
                    "pose:5",
                    "refresh:5",
                },
                presentation.Events
            );
        }

        [Test]
        public void Advance_FirstSelectorThree_KeepsZeroInitialDisplacement()
        {
            Presentation presentation = new();
            TacticalCombatArrival arrival = Create(presentation, CombatSide.Attacker, Member(7));
            arrival.Advance(0);
            Assert.AreEqual(new Vector4(10, 20, 100, -1), presentation.Poses[0].Position);
        }

        [Test]
        public void Advance_SelectorThreeAfterAnotherMember_CarriesPreviousInitialDisplacement()
        {
            Presentation presentation = new();
            TacticalCombatArrival arrival = Create(
                presentation,
                CombatSide.Attacker,
                Member(5),
                Member(7)
            );
            arrival.Advance(0);
            Assert.AreEqual(new Vector4(10, 20, -475, -1), presentation.Poses[1].Position);
        }

        [Test]
        public void Advance_AuthoredOrdinal_CombinesWithBattleRegistration()
        {
            Presentation presentation = new();
            TacticalCombatArrival arrival = Create(
                presentation,
                CombatSide.Attacker,
                Member(4, ordinal: 2)
            );
            arrival.Advance(0);
            Assert.AreEqual(-550, presentation.Poses[0].Position.Z);
        }

        [Test]
        public void Advance_LargeFrame_UpperClampsScaledArrivalDelta()
        {
            Presentation presentation = new();
            TacticalCombatArrival arrival = Create(presentation, CombatSide.Attacker, Member(4));
            arrival.Advance(5000);
            Assert.AreEqual(200, arrival.ElapsedMilliseconds);
            Assert.That(
                presentation.Poses.Last().Position.Z,
                Is.EqualTo(-292.88889f).Within(0.0001f)
            );
        }

        [Test]
        public void Advance_NegativeFrame_DoesNotInventLowerDeltaClamp()
        {
            Presentation presentation = new();
            TacticalCombatArrival arrival = Create(presentation, CombatSide.Attacker, Member(4));
            arrival.Advance(-100);
            Assert.AreEqual(-40, arrival.ElapsedMilliseconds);
        }

        [Test]
        public void Advance_ZeroElapsedRepeated_RequestsArrivalSoundAgain()
        {
            Presentation presentation = new();
            TacticalCombatArrival arrival = Create(presentation, CombatSide.Attacker, Member(4));
            arrival.Advance(0);
            arrival.Advance(0);
            Assert.AreEqual(2, presentation.Events.Count(value => value == "sound:30"));
        }

        [Test]
        public void Advance_LaterPositiveFrame_DoesNotRepeatFirstFrameSound()
        {
            Presentation presentation = new();
            TacticalCombatArrival arrival = Create(presentation, CombatSide.Attacker, Member(4));
            arrival.Advance(1);
            arrival.Advance(1);
            Assert.AreEqual(1, presentation.Events.Count(value => value == "sound:30"));
        }

        [Test]
        public void Advance_ShortTotalDuration_DoesNotCompleteEarly()
        {
            Presentation presentation = new();
            TacticalCombatArrival arrival = Create(presentation, CombatSide.Attacker, Member(4));
            for (int frame = 0; frame < 7; frame++)
                Assert.IsFalse(arrival.Advance(500));
            Assert.AreEqual(1400, arrival.ElapsedMilliseconds);
            Assert.IsFalse(arrival.IsComplete);
        }

        [Test]
        public void Advance_CompletionFrame_SnapsOriginalTransformBeforeClearingStrobe()
        {
            Presentation presentation = new();
            TacticalCombatArrivalMember member = new(
                4,
                0,
                CombatSide.Attacker,
                false,
                false,
                new Vector4(10, 20, 100, 7),
                new Vector4(1, 2, 3, 4)
            );
            TacticalCombatArrival arrival = Create(presentation, CombatSide.Attacker, member);
            for (int frame = 0; frame < 7; frame++)
                arrival.Advance(500);
            presentation.Events.Clear();
            Assert.IsTrue(arrival.Advance(250));
            Assert.AreEqual(member.Position, presentation.Poses.Last().Position);
            Assert.AreEqual(member.Direction, presentation.Poses.Last().Direction);
            CollectionAssert.AreEqual(
                new[]
                {
                    "strobe:True",
                    "pose:4",
                    "refresh:4",
                    "pose:4",
                    "refresh:4",
                    "strobe:False",
                },
                presentation.Events
            );
        }

        [Test]
        public void Advance_InitialLaneHasHorizontalComponents_OnlyInitialPassOffsetsThem()
        {
            Presentation presentation = new();
            TacticalCombatArrival arrival = new(
                CombatSide.Attacker,
                new[] { Member(4) },
                new Vector3(2, 3, 1),
                presentation,
                message => presentation.Events.Add($"sound:{message}")
            );
            arrival.Advance(500);
            Assert.AreEqual(-790, presentation.Poses[0].Position.X);
            Assert.AreEqual(-1180, presentation.Poses[0].Position.Y);
            Assert.AreEqual(10, presentation.Poses[1].Position.X);
            Assert.AreEqual(20, presentation.Poses[1].Position.Y);
        }

        [TestCase(1f, -475f)]
        [TestCase(-1f, 675f)]
        public void Advance_StaggeredMemberReachesStoredPosition_ClampsWithoutOvershoot(
            float laneZ,
            float initialZ
        )
        {
            Presentation presentation = new();
            TacticalCombatArrival arrival = new(
                CombatSide.Attacker,
                new[] { Member(5) },
                new Vector3(0, 0, laneZ),
                presentation,
                message => presentation.Events.Add($"sound:{message}")
            );
            for (int frame = 0; frame < 7; frame++)
                arrival.Advance(500);
            Assert.AreEqual(initialZ, presentation.Poses[0].Position.Z);
            Assert.AreEqual(100, presentation.Poses.Last().Position.Z);
            Assert.IsFalse(arrival.IsComplete);
        }

        /// <summary>
        /// Creates arrival with the supplied ordered members and a positive-z display lane.
        /// </summary>
        /// <param name="presentation">The presentation observer.</param>
        /// <param name="side">The explicitly captured entering side.</param>
        /// <param name="members">The ordered presentation members.</param>
        /// <returns>The arrival subject.</returns>
        private static TacticalCombatArrival Create(
            Presentation presentation,
            CombatSide side,
            params TacticalCombatArrivalMember[] members
        ) =>
            new(
                side,
                members,
                Vector3.UnitZ,
                presentation,
                message => presentation.Events.Add($"sound:{message}")
            );

        /// <summary>
        /// Creates one explicitly authored presentation member.
        /// </summary>
        /// <param name="index">The battle registration identifier.</param>
        /// <param name="side">The encounter side.</param>
        /// <param name="ordinal">The authored presentation type ordinal.</param>
        /// <param name="fighter">Whether fighter audio is used.</param>
        /// <param name="docked">Whether this member remains docked.</param>
        /// <returns>The presentation member.</returns>
        private static TacticalCombatArrivalMember Member(
            int index,
            CombatSide side = CombatSide.Attacker,
            int ordinal = 0,
            bool fighter = false,
            bool docked = false
        ) =>
            new(
                index,
                ordinal,
                side,
                fighter,
                docked,
                new Vector4(10, 20, 100, -1),
                new Vector4(0, 0, 1, -1)
            );

        private sealed class Presentation : ITacticalCombatArrivalPresentation
        {
            public List<string> Events { get; } = new();
            public List<(int Index, Vector4 Position, Vector4 Direction)> Poses { get; } = new();

            /// <summary>
            /// Records the observable control strobe.
            /// </summary>
            /// <param name="active">Whether the strobe is active.</param>
            public void SetPauseStrobe(bool active) => Events.Add($"strobe:{active}");

            /// <summary>
            /// Records effect reset ordering.
            /// </summary>
            /// <param name="unitIndex">The affected unit.</param>
            public void Reset(int unitIndex) => Events.Add($"reset:{unitIndex}");

            /// <summary>
            /// Records a presentation transform.
            /// </summary>
            /// <param name="unitIndex">The affected unit.</param>
            /// <param name="position">The supplied position.</param>
            /// <param name="direction">The supplied direction.</param>
            public void SetTransform(int unitIndex, Vector4 position, Vector4 direction)
            {
                Events.Add($"pose:{unitIndex}");
                Poses.Add((unitIndex, position, direction));
            }

            /// <summary>
            /// Records visibility refresh ordering.
            /// </summary>
            /// <param name="unitIndex">The affected unit.</param>
            public void Refresh(int unitIndex) => Events.Add($"refresh:{unitIndex}");
        }
    }
}
