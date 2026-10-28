using System;
using System.Linq;
using NUnit.Framework;
using Rebellion.Game.Combat;
using Rebellion.Game.Units;
using Rebellion.Tactical;

namespace Rebellion.Tests.Tactical
{
    [TestFixture]
    public sealed class TacticalMovementTests
    {
        /// <summary>
        /// Checks elapsed time and the individual ship's configured speed determine travel distance.
        /// </summary>
        /// <param name="speed">The ship's sublight rating.</param>
        /// <param name="elapsed">The elapsed battle time.</param>
        /// <param name="expected">The expected travel distance.</param>
        [TestCase(4, 2f, 8f)]
        [TestCase(8, 2f, 16f)]
        [TestCase(4, 0.5f, 2f)]
        [TestCase(0, 2f, 0f)]
        public void Advance_ConfiguredShip_UsesIndividualSpeed(
            int speed,
            float elapsed,
            float expected
        )
        {
            SpaceCombatUnit unit = SpaceCombatUnit.Create(
                new CapitalShip { SublightSpeed = speed }
            );
            unit.MoveOrder.Destination = new BattleVector3 { Z = 100f };

            TacticalMovement.Advance(unit, elapsed);

            Assert.That(unit.Position.Z, Is.EqualTo(expected).Within(0.0001f));
        }

        /// <summary>
        /// Checks a synchronized order limits a faster ship without changing its configured speed.
        /// </summary>
        [Test]
        public void Advance_SynchronizedOrder_UsesOrderSpeedLimit()
        {
            CapitalShip ship = new CapitalShip { SublightSpeed = 10 };
            SpaceCombatUnit unit = SpaceCombatUnit.Create(ship);
            unit.MoveOrder.Destination = new BattleVector3 { Z = 100f };
            unit.MoveOrder.SpeedLimit = 4f;

            TacticalMovement.Advance(unit, 2f);

            Assert.AreEqual(8f, unit.Position.Z);
            Assert.AreEqual(10, ship.SublightSpeed);
        }

        /// <summary>
        /// Checks fighter movement uses the same speed contract as capital movement.
        /// </summary>
        [Test]
        public void Advance_Fighter_UsesFighterSpeed()
        {
            SpaceCombatUnit unit = SpaceCombatUnit.Create(
                new Starfighter
                {
                    SublightSpeed = 11,
                    CurrentSquadronSize = 12,
                    MaxSquadronSize = 12,
                }
            );
            unit.Forward = new BattleVector3 { X = 1f };
            unit.MoveOrder.Destination = new BattleVector3 { X = 100f };

            TacticalMovement.Advance(unit, 2f);

            Assert.AreEqual(22f, unit.Position.X);
            Assert.AreEqual(1f, unit.Forward.X);
        }

        /// <summary>
        /// Checks an oversized time step stops exactly at the destination and clears the order.
        /// </summary>
        [Test]
        public void Advance_PassesDestination_StopsWithoutOvershoot()
        {
            SpaceCombatUnit unit = SpaceCombatUnit.Create(new CapitalShip { SublightSpeed = 10 });
            unit.Forward = new BattleVector3 { X = 0.6f, Z = 0.8f };
            unit.MoveOrder.Destination = new BattleVector3 { X = 3f, Z = 4f };

            TacticalMovement.Advance(unit, 1f);

            Assert.AreEqual(3f, unit.Position.X);
            Assert.AreEqual(4f, unit.Position.Z);
            Assert.IsNull(unit.MoveOrder.Destination);
        }

        /// <summary>
        /// Checks splitting elapsed time does not alter the distance traveled.
        /// </summary>
        [Test]
        public void Advance_DifferentFrameDurations_TravelsSameDistance()
        {
            SpaceCombatUnit first = SpaceCombatUnit.Create(new CapitalShip { SublightSpeed = 4 });
            SpaceCombatUnit second = SpaceCombatUnit.Create(new CapitalShip { SublightSpeed = 4 });
            first.MoveOrder.Destination = new BattleVector3 { Z = 100f };
            second.MoveOrder.Destination = new BattleVector3 { Z = 100f };

            TacticalMovement.Advance(first, 1f);
            for (int frame = 0; frame < 10; frame++)
                TacticalMovement.Advance(second, 0.1f);

            Assert.That(second.Position.Z, Is.EqualTo(first.Position.Z).Within(0.0001f));
        }

        /// <summary>
        /// Checks capital ships turn at half the fighter angular rate without reducing forward speed.
        /// </summary>
        /// <param name="fighter">Whether to use a fighter rather than a capital ship.</param>
        /// <param name="speed">The configured movement speed.</param>
        /// <param name="elapsed">The elapsed turn time.</param>
        /// <param name="expectedDegrees">The expected heading change.</param>
        [TestCase(false, 4, 1f, 20f)]
        [TestCase(false, 8, 0.5f, 20f)]
        [TestCase(true, 4, 1f, 40f)]
        [TestCase(true, 11, 0.5f, 55f)]
        public void Advance_DestinationToSide_TurnsAtSpeedDerivedRate(
            bool fighter,
            int speed,
            float elapsed,
            float expectedDegrees
        )
        {
            SpaceCombatUnit unit = fighter
                ? SpaceCombatUnit.Create(
                    new Starfighter
                    {
                        SublightSpeed = speed,
                        CurrentSquadronSize = 12,
                        MaxSquadronSize = 12,
                    }
                )
                : SpaceCombatUnit.Create(new CapitalShip { SublightSpeed = speed });
            unit.MoveOrder.Destination = new BattleVector3 { X = 100f };

            TacticalMovement.Advance(unit, elapsed);

            float radians = expectedDegrees * (float)Math.PI / 180f;
            Assert.That(unit.Forward.X, Is.EqualTo(Math.Sin(radians)).Within(0.0001f));
            Assert.That(unit.Forward.Z, Is.EqualTo(Math.Cos(radians)).Within(0.0001f));
            double radius = (fighter ? 18d : 36d) / Math.PI;
            Assert.That(
                unit.Position.X,
                Is.EqualTo(radius * (1d - Math.Cos(radians))).Within(0.0001f)
            );
            Assert.That(unit.Position.Z, Is.EqualTo(radius * Math.Sin(radians)).Within(0.0001f));
            Assert.IsNotNull(unit.MoveOrder.Destination);
        }

        /// <summary>
        /// Checks a frame can include both curved travel and the final tangent approach.
        /// </summary>
        [Test]
        public void Advance_ReachesTangentDuringFrame_CompletesArcThenMovesStraight()
        {
            SpaceCombatUnit unit = SpaceCombatUnit.Create(new CapitalShip { SublightSpeed = 4 });
            float radius = 36f / (float)Math.PI;
            unit.MoveOrder.Destination = new BattleVector3 { X = radius + 10f, Z = radius };

            TacticalMovement.Advance(unit, 5f);

            Assert.That(unit.Position.X, Is.EqualTo(radius + 2f).Within(0.0001f));
            Assert.That(unit.Position.Z, Is.EqualTo(radius).Within(0.0001f));
            Assert.That(unit.Forward.X, Is.EqualTo(1f).Within(0.0001f));
        }

        /// <summary>
        /// Checks an exact reversal produces a finite gradual horizontal turn rather than snapping.
        /// </summary>
        [Test]
        public void Advance_DestinationBehind_TurnsWithoutSnappingOrTilting()
        {
            SpaceCombatUnit unit = SpaceCombatUnit.Create(new CapitalShip { SublightSpeed = 4 });
            unit.MoveOrder.Destination = new BattleVector3 { Z = -100f };

            TacticalMovement.Advance(unit, 2.25f);

            Assert.That(unit.Forward.X, Is.EqualTo(Math.Sqrt(0.5)).Within(0.0001f));
            Assert.That(unit.Forward.Z, Is.EqualTo(Math.Sqrt(0.5)).Within(0.0001f));
            Assert.AreEqual(0f, unit.Forward.Y);
            Assert.That(unit.Position.Z, Is.GreaterThan(0f));
        }

        /// <summary>
        /// Checks vertical reversals also have a well-defined rotation axis.
        /// </summary>
        [Test]
        public void Advance_VerticalReversal_PreservesFiniteUnitHeading()
        {
            SpaceCombatUnit unit = SpaceCombatUnit.Create(new CapitalShip { SublightSpeed = 4 });
            unit.Forward = new BattleVector3 { Y = 1f };
            unit.MoveOrder.Destination = new BattleVector3 { Y = -100f };

            TacticalMovement.Advance(unit, 2.25f);

            Assert.AreEqual(0f, unit.Forward.X);
            Assert.That(unit.Forward.Y, Is.EqualTo(Math.Sqrt(0.5)).Within(0.0001f));
            Assert.That(unit.Forward.Z, Is.EqualTo(Math.Sqrt(0.5)).Within(0.0001f));
        }

        /// <summary>
        /// Checks partitioning a turn and subsequent travel across frames does not change the result.
        /// </summary>
        [Test]
        public void Advance_TurningAcrossDifferentFrames_PreservesHeadingAndDistance()
        {
            SpaceCombatUnit first = SpaceCombatUnit.Create(new CapitalShip { SublightSpeed = 4 });
            SpaceCombatUnit second = SpaceCombatUnit.Create(new CapitalShip { SublightSpeed = 4 });
            first.MoveOrder.Destination = new BattleVector3 { X = 100f };
            second.MoveOrder.Destination = new BattleVector3 { X = 100f };

            TacticalMovement.Advance(first, 3f);
            for (int frame = 0; frame < 30; frame++)
                TacticalMovement.Advance(second, 0.1f);

            Assert.That(second.Position.X, Is.EqualTo(first.Position.X).Within(0.0001f));
            Assert.That(second.Position.Z, Is.EqualTo(first.Position.Z).Within(0.0001f));
            Assert.That(second.Forward.X, Is.EqualTo(first.Forward.X).Within(0.0001f));
            Assert.That(second.Forward.Z, Is.EqualTo(first.Forward.Z).Within(0.0001f));
        }

        /// <summary>
        /// Checks retargeting an active turn starts from the saved heading instead of resetting it.
        /// </summary>
        [Test]
        public void MoveTo_DuringTurn_PreservesHeadingForNextTurn()
        {
            ActiveBattle battle = CreateBattle();
            SpaceCombatUnit unit = AddShip(battle, "FRIEND", 0f);
            unit.MoveOrder.Destination = new BattleVector3 { X = 100f };
            TacticalMovement.Advance(unit, 1f);
            float previousX = unit.Forward.X;
            float previousPositionX = unit.Position.X;
            float previousPositionZ = unit.Position.Z;

            TacticalMovement.MoveTo(
                battle,
                CreateBounds(),
                new[] { unit },
                "FRIEND",
                new BattleVector3 { X = -100f }
            );
            Assert.AreEqual(previousX, unit.Forward.X);
            TacticalMovement.Advance(unit, 0.5f);

            Assert.That(unit.Forward.X, Is.EqualTo(Math.Sin(Math.PI / 18)).Within(0.0001f));
            Assert.That(unit.Position.X, Is.GreaterThan(previousPositionX));
            Assert.That(unit.Position.Z, Is.GreaterThan(previousPositionZ));
        }

        /// <summary>
        /// Checks a nearby off-axis destination is reached instead of being trapped inside a turning circle.
        /// </summary>
        /// <param name="side">The destination's lateral offset.</param>
        /// <param name="ahead">The destination's forward offset.</param>
        [TestCase(1f, 0f)]
        [TestCase(0.01f, 0f)]
        [TestCase(-1f, 0f)]
        [TestCase(0f, -1f)]
        [TestCase(3f, 4f)]
        public void Advance_NearbyDestination_ReachesPointWithoutOrbiting(float side, float ahead)
        {
            SpaceCombatUnit unit = SpaceCombatUnit.Create(new CapitalShip { SublightSpeed = 4 });
            unit.MoveOrder.Destination = new BattleVector3 { X = side, Z = ahead };

            for (int frame = 0; frame < 600 && unit.MoveOrder.Destination != null; frame++)
                TacticalMovement.Advance(unit, 0.05f);

            Assert.IsNull(unit.MoveOrder.Destination);
            Assert.That(unit.Position.X, Is.EqualTo(side).Within(0.0001f));
            Assert.That(unit.Position.Z, Is.EqualTo(ahead).Within(0.0001f));
        }

        /// <summary>
        /// Checks a large frame cannot skip a required turn by teleporting across a nearby destination.
        /// </summary>
        [Test]
        public void Advance_CloseDestinationWithInsufficientTurnTime_KeepsMovingAlongArc()
        {
            SpaceCombatUnit unit = SpaceCombatUnit.Create(new CapitalShip { SublightSpeed = 4 });
            unit.MoveOrder.Destination = new BattleVector3 { X = 1f };

            TacticalMovement.Advance(unit, 1f);

            Assert.IsNotNull(unit.MoveOrder.Destination);
            Assert.That(unit.Position.X, Is.LessThan(0f));
            Assert.That(unit.Position.Z, Is.GreaterThan(0f));
            Assert.That(unit.Forward.X, Is.EqualTo(-Math.Sin(Math.PI / 9d)).Within(0.0001f));
        }

        /// <summary>
        /// Checks partial-frame displacement follows the changing heading rather than sliding toward the target.
        /// </summary>
        [Test]
        public void Advance_SmallTurningStep_MovesAlongForwardArc()
        {
            SpaceCombatUnit unit = SpaceCombatUnit.Create(new CapitalShip { SublightSpeed = 4 });
            unit.MoveOrder.Destination = new BattleVector3 { X = 100f };

            TacticalMovement.Advance(unit, 0.01f);

            Assert.That(unit.Position.Z, Is.EqualTo(0.04f).Within(0.00001f));
            Assert.That(unit.Position.X, Is.GreaterThan(0f).And.LessThan(0.001f));
            Assert.That(unit.Forward.X, Is.EqualTo(Math.Sin(Math.PI / 900d)).Within(0.00001f));
        }

        /// <summary>
        /// Checks a speed change affects the angular rate of an existing order immediately.
        /// </summary>
        [Test]
        public void Advance_SpeedChangesDuringTurn_UsesCurrentAngularRate()
        {
            SpaceCombatUnit unit = SpaceCombatUnit.Create(new CapitalShip { SublightSpeed = 4 });
            unit.MoveOrder.Destination = new BattleVector3 { X = 100f };
            TacticalMovement.Advance(unit, 1f);
            unit.SublightSpeed = 2;

            TacticalMovement.Advance(unit, 1f);

            Assert.That(unit.Forward.X, Is.EqualTo(0.5f).Within(0.0001f));
            Assert.That(unit.Forward.Z, Is.EqualTo(Math.Cos(Math.PI / 6)).Within(0.0001f));
        }

        /// <summary>
        /// Checks a stopped engine cannot turn toward a pending destination.
        /// </summary>
        [Test]
        public void Advance_ZeroSpeed_DoesNotTurn()
        {
            SpaceCombatUnit unit = SpaceCombatUnit.Create(new CapitalShip { SublightSpeed = 0 });
            unit.MoveOrder.Destination = new BattleVector3 { X = 100f };

            TacticalMovement.Advance(unit, 1f);

            Assert.AreEqual(0f, unit.Forward.X);
            Assert.AreEqual(1f, unit.Forward.Z);
        }

        /// <summary>
        /// Checks cancelling an order also stops its unfinished rotation.
        /// </summary>
        [Test]
        public void Stop_DuringTurn_PreservesCurrentHeading()
        {
            ActiveBattle battle = CreateBattle();
            SpaceCombatUnit unit = AddShip(battle, "FRIEND", 0f);
            unit.MoveOrder.Destination = new BattleVector3 { X = 100f };
            TacticalMovement.Advance(unit, 1f);
            float previousX = unit.Forward.X;
            float previousZ = unit.Forward.Z;

            TacticalMovement.Stop(battle, new[] { unit }, "FRIEND");
            TacticalMovement.Advance(unit, 1f);

            Assert.AreEqual(previousX, unit.Forward.X);
            Assert.AreEqual(previousZ, unit.Forward.Z);
        }

        /// <summary>
        /// Checks a changed speed affects an existing order on its next movement update.
        /// </summary>
        [Test]
        public void Advance_SpeedChanges_UsesCurrentSpeedWithoutReplacingOrder()
        {
            SpaceCombatUnit unit = SpaceCombatUnit.Create(new CapitalShip { SublightSpeed = 4 });
            unit.MoveOrder.Destination = new BattleVector3 { Z = 100f };
            TacticalMovement.Advance(unit, 1f);
            unit.SublightSpeed = 0;
            TacticalMovement.Advance(unit, 1f);
            Assert.AreEqual(4f, unit.Position.Z);
            Assert.IsNotNull(unit.MoveOrder.Destination);

            unit.SublightSpeed = 2;
            TacticalMovement.Advance(unit, 1f);

            Assert.AreEqual(6f, unit.Position.Z);
        }

        /// <summary>
        /// Checks departed combatants do not continue traveling.
        /// </summary>
        [Test]
        public void Advance_RetreatedUnit_DoesNotMove()
        {
            SpaceCombatUnit unit = SpaceCombatUnit.Create(new CapitalShip { SublightSpeed = 4 });
            unit.HasRetreated = true;
            unit.MoveOrder.Destination = new BattleVector3 { Z = 100f };

            TacticalMovement.Advance(unit, 1f);

            Assert.AreEqual(0f, unit.Position.Z);
        }

        /// <summary>
        /// Checks hull destruction prevents further movement.
        /// </summary>
        [Test]
        public void Advance_DestroyedUnit_DoesNotMove()
        {
            SpaceCombatUnit unit = SpaceCombatUnit.Create(
                new CapitalShip
                {
                    SublightSpeed = 4,
                    MaxHullStrength = 100,
                    CurrentHullStrength = 0,
                }
            );
            unit.MoveOrder.Destination = new BattleVector3 { Z = 100f };

            TacticalMovement.Advance(unit, 1f);

            Assert.AreEqual(0f, unit.Position.Z);
        }

        /// <summary>
        /// Checks invalid elapsed time cannot corrupt saved positions.
        /// </summary>
        /// <param name="elapsed">The invalid duration.</param>
        [TestCase(-1f)]
        [TestCase(float.NaN)]
        [TestCase(float.PositiveInfinity)]
        public void Advance_InvalidElapsedTime_ThrowsArgumentOutOfRangeException(float elapsed)
        {
            SpaceCombatUnit unit = SpaceCombatUnit.Create(new CapitalShip());

            Assert.Throws<ArgumentOutOfRangeException>(() =>
                TacticalMovement.Advance(unit, elapsed)
            );
        }

        /// <summary>
        /// Checks a group order translates destinations without collapsing ships onto one point.
        /// </summary>
        [Test]
        public void MoveTo_Formation_PreservesRelativeDestinations()
        {
            ActiveBattle battle = CreateBattle();
            SpaceCombatUnit first = AddShip(battle, "FRIEND", -10f);
            SpaceCombatUnit second = AddShip(battle, "FRIEND", 10f);

            TacticalMovement.MoveTo(
                battle,
                CreateBounds(),
                new[] { first, second },
                "FRIEND",
                new BattleVector3 { X = 40f, Z = 30f }
            );

            Assert.AreEqual(30f, first.MoveOrder.Destination.X);
            Assert.AreEqual(50f, second.MoveOrder.Destination.X);
            Assert.AreEqual(30f, first.MoveOrder.Destination.Z);
            Assert.AreEqual(30f, second.MoveOrder.Destination.Z);
        }

        /// <summary>
        /// Checks a synchronized formation order records the slowest selected movement speed.
        /// </summary>
        [Test]
        public void MoveTo_MatchingSpeed_AssignsSlowestSpeedLimitToFormation()
        {
            ActiveBattle battle = CreateBattle();
            SpaceCombatUnit slow = AddShip(battle, "FRIEND", -10f, 4);
            SpaceCombatUnit fast = AddShip(battle, "FRIEND", 10f, 9);

            TacticalMovement.MoveTo(
                battle,
                CreateBounds(),
                new[] { slow, fast },
                "FRIEND",
                new BattleVector3 { Z = 30f },
                true
            );

            Assert.AreEqual(4f, slow.MoveOrder.SpeedLimit);
            Assert.AreEqual(4f, fast.MoveOrder.SpeedLimit);
        }

        /// <summary>
        /// Checks a normal movement order releases an earlier synchronized speed limit.
        /// </summary>
        [Test]
        public void MoveTo_NormalOrder_ClearsPreviousSpeedLimit()
        {
            ActiveBattle battle = CreateBattle();
            SpaceCombatUnit unit = AddShip(battle, "FRIEND", 0f, 9);
            unit.MoveOrder.SpeedLimit = 4f;

            TacticalMovement.MoveTo(
                battle,
                CreateBounds(),
                new[] { unit },
                "FRIEND",
                new BattleVector3 { Z = 30f }
            );

            Assert.IsNull(unit.MoveOrder.SpeedLimit);
        }

        /// <summary>
        /// Checks map-edge clamping preserves formation spacing and keeps centers inside the map.
        /// </summary>
        [Test]
        public void MoveTo_OutsideMap_ClampsFormationTranslation()
        {
            ActiveBattle battle = CreateBattle();
            SpaceCombatUnit first = AddShip(battle, "FRIEND", -10f);
            SpaceCombatUnit second = AddShip(battle, "FRIEND", 10f);

            TacticalMovement.MoveTo(
                battle,
                CreateBounds(),
                new[] { first, second },
                "FRIEND",
                new BattleVector3 { X = 1000f }
            );

            Assert.AreEqual(80f, first.MoveOrder.Destination.X);
            Assert.AreEqual(100f, second.MoveOrder.Destination.X);
        }

        /// <summary>
        /// Checks mixed selections cannot issue orders to hostile or unrelated units.
        /// </summary>
        [Test]
        public void MoveTo_MixedSelection_OrdersOnlyOwnedBattleMembers()
        {
            ActiveBattle battle = CreateBattle();
            SpaceCombatUnit friendly = AddShip(battle, "FRIEND", 0f);
            SpaceCombatUnit hostile = AddShip(battle, "ENEMY", 0f);
            SpaceCombatUnit outsider = SpaceCombatUnit.Create(
                new CapitalShip { OwnerInstanceID = "FRIEND" }
            );

            bool accepted = TacticalMovement.MoveTo(
                battle,
                CreateBounds(),
                new[] { friendly, hostile, outsider },
                "FRIEND",
                new BattleVector3 { Z = 30f }
            );

            Assert.IsTrue(accepted);
            Assert.AreEqual(30f, friendly.MoveOrder.Destination.Z);
            Assert.IsNull(hostile.MoveOrder.Destination);
            Assert.IsNull(outsider.MoveOrder.Destination);
        }

        /// <summary>
        /// Checks a selection without controlled deployed units reports no accepted order.
        /// </summary>
        [Test]
        public void MoveTo_NoControlledDeployedUnits_ReturnsFalse()
        {
            ActiveBattle battle = CreateBattle();
            SpaceCombatUnit hostile = AddShip(battle, "ENEMY", 0f);

            bool accepted = TacticalMovement.MoveTo(
                battle,
                CreateBounds(),
                new[] { hostile },
                "FRIEND",
                new BattleVector3 { Z = 30f }
            );

            Assert.IsFalse(accepted);
            Assert.IsNull(hostile.MoveOrder.Destination);
        }

        /// <summary>
        /// Checks stopping selected ships does not cancel hostile orders.
        /// </summary>
        [Test]
        public void Stop_MixedSelection_CancelsOnlyFriendlyOrders()
        {
            ActiveBattle battle = CreateBattle();
            SpaceCombatUnit friendly = AddShip(battle, "FRIEND", 0f);
            SpaceCombatUnit hostile = AddShip(battle, "ENEMY", 0f);
            friendly.MoveOrder.Destination = new BattleVector3 { Z = 30f };
            friendly.MoveOrder.SpeedLimit = 2f;
            hostile.MoveOrder.Destination = new BattleVector3 { Z = 30f };
            hostile.MoveOrder.SpeedLimit = 2f;

            TacticalMovement.Stop(battle, new[] { friendly, hostile }, "FRIEND");

            Assert.IsNull(friendly.MoveOrder.Destination);
            Assert.IsNull(friendly.MoveOrder.SpeedLimit);
            Assert.IsNotNull(hostile.MoveOrder.Destination);
            Assert.AreEqual(2f, hostile.MoveOrder.SpeedLimit);
        }

        /// <summary>
        /// Creates an empty space battle for order tests.
        /// </summary>
        /// <returns>The test battle.</returns>
        private static ActiveBattle CreateBattle() => new ActiveBattle { Kind = BattleKind.Space };

        /// <summary>
        /// Adds one movable capital ship at a known battle location.
        /// </summary>
        /// <param name="battle">The receiving battle.</param>
        /// <param name="owner">The owning participant.</param>
        /// <param name="x">The initial horizontal coordinate.</param>
        /// <param name="speed">The ship's configured sublight speed.</param>
        /// <returns>The newly added battle copy.</returns>
        private static SpaceCombatUnit AddShip(
            ActiveBattle battle,
            string owner,
            float x,
            int speed = 4
        )
        {
            SpaceCombatUnit unit = battle
                .AddCombatants(new CapitalShip { OwnerInstanceID = owner, SublightSpeed = speed })
                .Single();
            unit.Position.X = x;
            return unit;
        }

        /// <summary>
        /// Creates a bounded volume shared by order tests.
        /// </summary>
        /// <returns>The playable volume.</returns>
        private static BattleMapBounds CreateBounds() =>
            new BattleMapBounds
            {
                MinimumX = -100f,
                MaximumX = 100f,
                MinimumY = -100f,
                MaximumY = 100f,
                MinimumZ = -100f,
                MaximumZ = 100f,
            };
    }
}
