using System;
using System.Drawing;
using NUnit.Framework;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Units;

namespace Rebellion.Tests.Game.Units
{
    [TestFixture]
    public class IMovableTests
    {
        [Test]
        public void GetTransitMovement_WithDirectMovement_ReturnsDirectMovement()
        {
            MovementState movement = new MovementState();
            IMovable movable = new Officer { Movement = movement };

            MovementState result = movable.GetTransitMovement();

            Assert.AreSame(movement, result);
        }

        [Test]
        public void GetTransitMovement_AboardMovingCapitalShip_ReturnsCapitalShipMovement()
        {
            MovementState movement = new MovementState();
            Officer officer = new Officer { OwnerInstanceID = "faction" };
            CapitalShip ship = new CapitalShip { OwnerInstanceID = "faction", Movement = movement };
            officer.SetParent(ship);

            MovementState result = ((IMovable)officer).GetTransitMovement();

            Assert.AreSame(movement, result);
        }

        [Test]
        public void GetTransitMovement_AboardMovingFleet_ReturnsFleetMovement()
        {
            MovementState movement = new MovementState();
            Officer officer = new Officer { OwnerInstanceID = "faction" };
            CapitalShip ship = new CapitalShip { OwnerInstanceID = "faction" };
            Fleet fleet = new Fleet { OwnerInstanceID = "faction", Movement = movement };
            officer.SetParent(ship);
            ship.SetParent(fleet);

            MovementState result = ((IMovable)officer).GetTransitMovement();

            Assert.AreSame(movement, result);
        }

        [Test]
        public void GetPosition_StationaryAtPlanet_ReturnsPlanetPosition()
        {
            Planet planet = new Planet { PositionX = 12, PositionY = 34 };
            Fleet fleet = new Fleet();
            fleet.SetParent(planet);

            Point result = ((IMovable)fleet).GetPosition();

            Assert.AreEqual(new Point(12, 34), result);
        }

        [Test]
        public void GetPosition_InTransit_ReturnsCurrentPosition()
        {
            IMovable movable = new Fleet
            {
                Movement = new MovementState { CurrentPosition = new Point(56, 78) },
            };

            Point result = movable.GetPosition();

            Assert.AreEqual(new Point(56, 78), result);
        }

        [Test]
        public void SetPosition_InTransit_UpdatesCurrentPosition()
        {
            IMovable movable = new Fleet { Movement = new MovementState() };

            movable.SetPosition(new Point(90, 123));

            Assert.AreEqual(new Point(90, 123), movable.Movement.CurrentPosition);
        }

        [Test]
        public void SetPosition_WithoutMovement_ThrowsInvalidOperationException()
        {
            IMovable movable = new Fleet { DisplayName = "Test Fleet" };

            Assert.Throws<InvalidOperationException>(() => movable.SetPosition(Point.Empty));
        }
    }
}
