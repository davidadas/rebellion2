using System;
using System.Numerics;
using NUnit.Framework;
using Rebellion.Game.Results;
using Rebellion.Game.Units;
using Rebellion.Gameplay;

namespace Rebellion.Tests.Gameplay
{
    [TestFixture]
    public sealed class TacticalCombatFormationTests
    {
        [TestCase(TacticalCombatFormationKind.Line)]
        [TestCase(TacticalCombatFormationKind.Surround)]
        public void MoveTowards_ThreeDimensionalDirection_UsesStoredScale(
            TacticalCombatFormationKind kind
        )
        {
            TacticalCombatUnit unit = CreateUnit();
            TacticalCombatFormation formation = new(kind, new[] { unit }, Vector3.UnitZ);
            formation.Refresh(0);

            formation.MoveTowards(new Vector3(3, 4, 5), 1000, 0, Vector3.UnitZ);

            Assert.AreEqual(0x3f2bf7e8, BitConverter.SingleToInt32Bits(formation.Velocity.Z));
        }

        [Test]
        public void MoveTowards_DirectDistanceRoundsToThreshold_StillMoves()
        {
            TacticalCombatUnit unit = CreateUnit();
            TacticalCombatFormation formation = new(
                TacticalCombatFormationKind.Direct,
                new[] { unit },
                Vector3.UnitZ
            );
            formation.Refresh(0);

            bool moved = formation.MoveTowards(
                new Vector3(2, 1f / 4096, 0),
                1000,
                2,
                Vector3.UnitZ
            );

            Assert.IsTrue(moved);
            Assert.AreNotEqual(Vector3.Zero, formation.Velocity);
        }

        [Test]
        public void MoveTowards_StoredNormalizedHeading_PreservesItsComponents()
        {
            TacticalCombatFormation formation = new(
                TacticalCombatFormationKind.Line,
                new[] { CreateUnit() },
                Vector3.UnitZ
            );
            formation.Refresh(0);
            Vector3 heading = new(0.57735026f, 0.57735026f, 0.57735026f);

            formation.MoveTowards(Vector3.Zero, 10000, 0, heading);

            Assert.AreEqual(heading, formation.Heading);
        }

        [Test]
        public void MoveTowards_VerticalSurroundTravel_UsesSideLaneForHeading()
        {
            TacticalCombatUnit unit = CreateUnit();
            unit.Heading = Vector3.UnitX;
            TacticalCombatFormation formation = new(
                TacticalCombatFormationKind.Surround,
                new[] { unit },
                Vector3.UnitZ
            );
            formation.Refresh(0);

            formation.MoveTowards(new Vector3(0, 10, 0), 1000, 0, Vector3.UnitZ);

            Assert.AreEqual(Vector3.UnitZ, formation.Heading);
        }

        [TestCase(TacticalCombatFormationKind.Line)]
        [TestCase(TacticalCombatFormationKind.Surround)]
        public void SetKind_ExistingMovement_ResetsMarkerMotion(TacticalCombatFormationKind kind)
        {
            TacticalCombatUnit unit = CreateUnit();
            TacticalCombatFormation formation = new(
                TacticalCombatFormationKind.Line,
                new[] { unit },
                Vector3.UnitZ
            );
            formation.Refresh(0);
            formation.MoveTowards(new Vector3(10, 0, 0), 1000, 0, Vector3.UnitZ);
            Assert.AreNotEqual(Vector3.Zero, formation.Velocity);

            formation.SetKind(kind);

            Assert.AreEqual(Vector3.Zero, formation.Velocity);
            Assert.AreEqual(0, formation.MovementSpeed);
            Assert.AreEqual(0, formation.TurnRate);
            Assert.AreEqual(0, formation.StableUpdates);
            Assert.AreEqual(unit.Position, formation.Position);
            Assert.AreEqual(Vector3.UnitZ, formation.Heading);
        }

        [Test]
        public void SetKind_PreviousPosition_DoesNotCarryStableCount()
        {
            TacticalCombatUnit unit = CreateUnit();
            unit.Position = new Vector3(10, 0, 0);
            TacticalCombatFormation formation = new(
                TacticalCombatFormationKind.Line,
                new[] { unit },
                Vector3.UnitZ
            );
            formation.Refresh(0);
            formation.Refresh(0);
            Assert.AreEqual(1, formation.StableUpdates);

            formation.SetKind(TacticalCombatFormationKind.Surround);
            formation.Refresh(0);

            Assert.AreEqual(0, formation.StableUpdates);
        }

        [Test]
        public void SetKind_PreviousMovement_DoesNotSteerNewSurroundLayout()
        {
            TacticalCombatFormation formation = new(
                TacticalCombatFormationKind.Line,
                new[] { CreateUnit() },
                Vector3.UnitZ
            );
            formation.Refresh(0);
            formation.MoveTowards(new Vector3(10, 0, 0), 1000, 0, Vector3.UnitZ);
            formation.SetKind(TacticalCombatFormationKind.Surround);
            formation.Refresh(0);

            formation.MoveTowards(Vector3.Zero, 1000, 0, Vector3.UnitZ);

            Assert.AreEqual(Vector3.UnitZ, formation.Heading);
        }

        [Test]
        public void SetKind_RecentCollisionRecovery_PreservesUnitCooldown()
        {
            (TacticalCombatFormation formation, TacticalCombatUnit unit) =
                CreateRecoveringFormation();

            formation.SetKind(TacticalCombatFormationKind.Surround);
            Stabilize(formation, 14);

            Assert.AreEqual(new Vector3(0, 0, -12.5f), formation.GetMemberMovement(unit));
        }

        [Test]
        public void SetKind_ExpiredCollisionRecovery_ContinuesUnitPhase()
        {
            (TacticalCombatFormation formation, TacticalCombatUnit unit) =
                CreateRecoveringFormation();

            formation.SetKind(TacticalCombatFormationKind.Surround);
            Stabilize(formation, 26);

            Assert.AreEqual(new Vector3(20, 0, 0), formation.GetMemberMovement(unit));
        }

        [Test]
        public void AddMember_RejoiningUnit_PreservesRecoveryCooldown()
        {
            (TacticalCombatFormation formation, TacticalCombatUnit unit) =
                CreateRecoveringFormation();

            formation.RemoveMember(unit);
            formation.AddMember(unit);
            formation.Refresh(14);

            Assert.AreEqual(Vector3.Zero, formation.GetMemberMovement(unit));
        }

        [Test]
        public void Constructor_PreviouslyGroupedUnit_ContinuesRecoveryPhase()
        {
            (TacticalCombatFormation previous, TacticalCombatUnit unit) =
                CreateRecoveringFormation();
            previous.RemoveMember(unit);
            TacticalCombatFormation next = new(
                TacticalCombatFormationKind.Line,
                new[] { unit },
                Vector3.UnitZ
            );

            Stabilize(next, 26);

            Assert.AreEqual(new Vector3(20, 0, 0), next.GetMemberMovement(unit));
        }

        /// <summary>
        /// Captures a mobile unit with one unit of movement budget and a deterministic recovery phase.
        /// </summary>
        /// <returns>The captured tactical unit.</returns>
        private static TacticalCombatUnit CreateUnit() =>
            new(
                new CapitalShip
                {
                    InstanceID = "ship",
                    MaxHullStrength = 100,
                    CurrentHullStrength = 100,
                },
                CombatSide.Attacker,
                1
            );

        /// <summary>
        /// Creates a stationary formation whose unit has completed its first collision recovery request.
        /// </summary>
        /// <returns>The formation and its colliding unit.</returns>
        private static (
            TacticalCombatFormation Formation,
            TacticalCombatUnit Unit
        ) CreateRecoveringFormation()
        {
            TacticalCombatUnit unit = CreateUnit();
            unit.HasCollision = true;
            TacticalCombatFormation formation = new(
                TacticalCombatFormationKind.Line,
                new[] { unit },
                Vector3.UnitZ
            );
            Stabilize(formation, 13);
            Assert.AreEqual(new Vector3(5, 0, 20), formation.GetMemberMovement(unit));
            return (formation, unit);
        }

        /// <summary>
        /// Refreshes a stationary marker until its collision recovery stability gate is reached.
        /// </summary>
        /// <param name="formation">The stationary marker.</param>
        /// <param name="time">The elapsed whole-second clock.</param>
        private static void Stabilize(TacticalCombatFormation formation, float time)
        {
            for (int update = 0; update < 6; update++)
                formation.Refresh(time);
        }
    }
}
