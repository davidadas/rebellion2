using System.Numerics;
using NUnit.Framework;
using Rebellion.Game.Results;
using Rebellion.Game.Units;
using Rebellion.Gameplay;
using Rebellion.Util.Mathematics;

namespace Rebellion.Tests.Gameplay
{
    [TestFixture]
    public sealed class TacticalCombatGroupTests
    {
        [Test]
        public void Advance_CapitalAtBufferedRangeBoundary_StopsUsingStoredMovementDistance()
        {
            CapitalShip ship = new()
            {
                InstanceID = "member",
                MaxHullStrength = 100,
                CurrentHullStrength = 100,
                SublightSpeed = 1,
                WeaponRecharge = 1,
            };
            ship.PrimaryWeapons[PrimaryWeaponType.Turbolaser] = new[] { 1, 1, 1, 1, 6 };
            TacticalCombatUnit member = new(ship, CombatSide.Attacker, 1);
            TacticalCombatUnit target = new(
                new CapitalShip
                {
                    InstanceID = "target",
                    MaxHullStrength = 100,
                    CurrentHullStrength = 100,
                },
                CombatSide.Defender,
                1000001
            )
            {
                Position = new Vector3(3.999999761581421f, 0.001953125f, 3.725290298461914e-9f),
            };
            TacticalCombatGroup group = new(10, new[] { member }, Vector3.UnitZ);
            group.SetTarget(target);
            group.Refresh(new[] { member, target }, 0);
            Assert.AreEqual(5, group.Engagement.MinimumRange);
            Assert.AreEqual(6, group.Engagement.MaximumRange);
            Assert.That(group.Formation.MovementSpeed, Is.GreaterThan(0));

            group.Advance(1000, 100, 0);

            Assert.AreEqual(Vector3.Zero, group.Formation.Position);
            Assert.AreEqual(Vector3.Zero, group.Formation.Velocity);
        }

        [Test]
        public void Advance_DistantCapitalEngagement_RetainsRangeScalarUntilStepStores()
        {
            TacticalCombatUnit[] members = new TacticalCombatUnit[6];
            for (int index = 0; index < members.Length; index++)
            {
                CapitalShip ship = new()
                {
                    InstanceID = "member-" + index,
                    MaxHullStrength = 100,
                    CurrentHullStrength = 100,
                    WeaponRecharge = 1,
                };
                ship.PrimaryWeapons[PrimaryWeaponType.Turbolaser] = new[] { 1, 1, 1, 1, 100 };
                members[index] = new TacticalCombatUnit(ship, CombatSide.Attacker, index + 1);
            }
            TacticalCombatUnit target = new(
                new CapitalShip
                {
                    InstanceID = "target",
                    MaxHullStrength = 100,
                    CurrentHullStrength = 100,
                },
                CombatSide.Defender,
                1000001
            )
            {
                Position = new Vector3(300, 400, 500),
            };
            TacticalCombatGroup group = new(10, members, Vector3.UnitZ);
            group.SetTarget(target);
            group.Refresh(members, 0);

            group.Advance(1000, 100, 0);

            Assert.AreEqual(
                0x3ece5cb0,
                System.BitConverter.SingleToInt32Bits(group.Formation.Position.X)
            );
            Assert.AreEqual(
                0x3e5c1e9a,
                System.BitConverter.SingleToInt32Bits(group.Formation.Position.Y)
            );
            Assert.AreEqual(
                0x3f2bf7e9,
                System.BitConverter.SingleToInt32Bits(group.Formation.Position.Z)
            );
        }

        [TestCase(1000f, unchecked((int)0xbef9c7d2))]
        [TestCase(10000f, 0x3fe0a3d4)]
        public void Advance_CloseFighterOrbit_RetainsPhaseComparisonAndStoresHorizontalOffset(
            float elapsed,
            int expectedX
        )
        {
            TacticalCombatUnit fighter = new(
                new Starfighter
                {
                    InstanceID = "fighter",
                    MaxSquadronSize = 12,
                    CurrentSquadronSize = 12,
                    LaserCannon = 1,
                    LaserRange = 1,
                },
                CombatSide.Attacker,
                1
            );
            TacticalCombatUnit target = CreateFighter("target", CombatSide.Defender);
            target.Position = new Vector3(-1.125f, 0, 0);
            TacticalCombatGroup group = new(10, new[] { fighter }, Vector3.UnitZ);
            group.SetTarget(target);
            group.Refresh(new[] { fighter, target }, 0);
            Assert.Less(group.Engagement.MovementDistance, 2);

            group.Advance(elapsed, 100, 0);

            Assert.AreEqual(
                expectedX,
                System.BitConverter.SingleToInt32Bits(group.Formation.Position.X)
            );
        }

        [Test]
        public void GetMemberHeading_ThreeDimensionalFighterTarget_UsesRoundedDirectionScale()
        {
            TacticalCombatUnit fighter = CreateFighter("fighter", CombatSide.Attacker);
            TacticalCombatUnit target = CreateFighter("target", CombatSide.Defender);
            target.Position = new Vector3(3, 4, 5);
            TacticalCombatGroup group = new(10, new[] { fighter }, Vector3.UnitZ);
            group.SetTarget(target);

            Vector3 heading = group.GetMemberHeading(fighter);

            Assert.AreEqual(new Vector3(0.424264073f, 0.565685451f, 0.707106829f), heading);
        }

        [Test]
        public void GetMemberHeading_StationaryUntargetedFighter_PreservesFormationHeading()
        {
            TacticalCombatUnit fighter = CreateFighter("fighter", CombatSide.Attacker);
            Vector3 heading = new(0.57735026f, 0.57735026f, 0.57735026f);
            TacticalCombatGroup group = new(10, new[] { fighter }, heading);

            Assert.AreEqual(heading, group.GetMemberHeading(fighter));
        }

        [Test]
        public void GetMemberHeading_StationaryRearBattery_RetainsRotationBeforeComponentStores()
        {
            TacticalCombatUnit member = CreateCapital(1, new[] { 0, 1, 0, 0, 100 });
            TacticalCombatGroup group = CreateStationaryEngagement(
                member,
                new Vector3(93.9543686f, 0, 1.07471245e9f)
            );

            Vector3 heading = group.GetMemberHeading(member);

            if (NativeVectorMath.SignificandBits == 64)
                Assert.AreEqual(
                    unchecked((int)0xa89c9435),
                    System.BitConverter.SingleToInt32Bits(heading.X)
                );
            Assert.That(heading.X, Is.LessThan(0));
            Assert.AreEqual(0, heading.Y);
            Assert.AreEqual(-1, heading.Z);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void GetMemberHeading_NoPositiveBatteryBudget_AlignsForward(bool clearBattery)
        {
            TacticalCombatUnit member = CreateCapital(1, new[] { 0, 0, 0, 0, 100 });
            if (clearBattery)
                member.Weapons.ClearBattery(TacticalWeaponArc.Starboard);
            TacticalCombatGroup group = CreateStationaryEngagement(member, new Vector3(3, 4, 0));

            Assert.AreEqual(Vector3.UnitX, group.GetMemberHeading(member));
        }

        [Test]
        public void GetMemberHeading_ClearedStrongestBattery_RetainsAuthoredAlignment()
        {
            TacticalCombatUnit member = CreateCapital(1, new[] { 0, 0, 0, 1, 100 });
            member.Weapons.ClearBattery(TacticalWeaponArc.Starboard);
            TacticalCombatGroup group = CreateStationaryEngagement(member, Vector3.UnitZ);

            Assert.AreEqual(
                TacticalWeaponArc.Starboard,
                TacticalCombatFireControl.GetStrongestArc(member, false)
            );
            Assert.That(group.GetMemberHeading(member).X, Is.LessThan(-0.999f));
        }

        [Test]
        public void GetMemberHeading_UnorderedStationaryMovement_ResolvesCapitalTarget()
        {
            TacticalCombatUnit member = CreateCapital(1, new[] { 0, 0, 0, 0, 100 });
            TacticalCombatGroup group = CreateStationaryEngagement(member, Vector3.UnitX);
            member.DesiredMovement = new Vector3(float.NaN, 0, float.NaN);

            Assert.AreEqual(Vector3.UnitX, group.GetMemberHeading(member));
        }

        [Test]
        public void GetMemberHeading_UnorderedPlanarMovement_RetainsCurrentCapitalHeading()
        {
            TacticalCombatUnit member = CreateCapital(1, new[] { 0, 0, 0, 0, 100 });
            member.DesiredMovement = new Vector3(float.NaN, 4, float.NaN);
            TacticalCombatGroup group = new(10, new[] { member }, Vector3.UnitZ);

            Assert.AreEqual(Vector3.UnitZ, group.GetMemberHeading(member));
        }

        [Test]
        public void GetMemberHeading_UnorderedUntargetedFighter_PreservesFormationHeading()
        {
            TacticalCombatUnit fighter = CreateFighter("fighter", CombatSide.Attacker);
            fighter.DesiredMovement = new Vector3(float.NaN, 0, float.NaN);
            TacticalCombatGroup group = new(10, new[] { fighter }, Vector3.UnitZ);

            Assert.AreEqual(Vector3.UnitZ, group.GetMemberHeading(fighter));
        }

        [Test]
        public void GetAttackRate_ZeroHullActiveFighter_UpdatesItsRechargeSample()
        {
            TacticalCombatUnit fighter = CreateFighter("fighter", CombatSide.Attacker);
            TacticalCombatUnit target = CreateFighter("target", CombatSide.Defender);
            fighter.Systems.Destroy();
            TacticalCombatGroup group = new(10, new[] { fighter }, Vector3.UnitZ);

            group.GetAttackRate(target);

            Assert.AreEqual(0, fighter.Systems.GetWeaponRechargeRatio());
        }

        [Test]
        public void PrimaryWaypoint_RequestedOrderChanges_RetainsAppliedPoint()
        {
            TacticalCombatGroup group = new(
                10,
                new[] { CreateFighter("fighter", CombatSide.Attacker) },
                Vector3.UnitZ
            );
            Vector3 first = new(10, 20, 30);
            Vector3 next = new(40, 50, 60);
            Assert.IsNull(group.PrimaryWaypoint);
            group.SetWaypoint(first);

            group.SetOrder(TacticalCombatOrder.Stop);

            Assert.AreEqual(first, group.PrimaryWaypoint);
            Assert.IsEmpty(group.RequestedWaypoints);
            group.SetWaypoint(next);
            Assert.AreEqual(next, group.PrimaryWaypoint);
        }

        [Test]
        public void PrimaryWaypoint_AppliedRouteReplacedAndCleared_ReturnsNewPointThenAbsence()
        {
            TacticalCombatGroup group = new(
                10,
                new[] { CreateFighter("fighter", CombatSide.Attacker) },
                Vector3.UnitZ
            );
            Vector3 first = new(10, 20, 30);
            Vector3 next = new(40, 50, 60);
            group.SetWaypoint(first);
            group.SetWaypoint(next, true);

            group.SetWaypoint(next);
            Assert.AreEqual(next, group.PrimaryWaypoint);
            group.SetOrder(TacticalCombatOrder.Anvil);

            Assert.IsNull(group.PrimaryWaypoint);
        }

        /// <summary>
        /// Creates an active capital ship with authored laser battery strengths.
        /// </summary>
        /// <param name="id">The battle-local identifier.</param>
        /// <param name="weapons">The four strengths followed by their range.</param>
        /// <returns>The captured tactical capital ship.</returns>
        private static TacticalCombatUnit CreateCapital(int id, int[] weapons)
        {
            CapitalShip ship = new()
            {
                InstanceID = "capital-" + id,
                MaxHullStrength = 100,
                CurrentHullStrength = 100,
            };
            ship.PrimaryWeapons[PrimaryWeaponType.LaserCannon] = weapons;
            return new TacticalCombatUnit(ship, CombatSide.Attacker, id);
        }

        /// <summary>
        /// Creates an engagement whose stationary marker has completed its stability interval.
        /// </summary>
        /// <param name="member">The member that will align its battery.</param>
        /// <param name="targetPosition">The stationary target position.</param>
        /// <returns>The targeted stationary group.</returns>
        private static TacticalCombatGroup CreateStationaryEngagement(
            TacticalCombatUnit member,
            Vector3 targetPosition
        )
        {
            TacticalCombatUnit target = CreateCapital(1000001, new[] { 0, 0, 0, 0, 0 });
            target.Position = targetPosition;
            TacticalCombatGroup group = new(10, new[] { member }, Vector3.UnitZ);
            group.SetTarget(target);
            for (int index = 0; index < 5; index++)
                group.Formation.Refresh(index);
            return group;
        }

        /// <summary>
        /// Captures an active fighter squadron for group heading behavior.
        /// </summary>
        /// <param name="id">The squadron identifier.</param>
        /// <param name="side">The owning combat side.</param>
        /// <returns>The captured tactical squadron.</returns>
        private static TacticalCombatUnit CreateFighter(string id, CombatSide side) =>
            new(
                new Starfighter
                {
                    InstanceID = id,
                    MaxSquadronSize = 12,
                    CurrentSquadronSize = 12,
                },
                side,
                side == CombatSide.Attacker ? 1 : 1000001
            );
    }
}
