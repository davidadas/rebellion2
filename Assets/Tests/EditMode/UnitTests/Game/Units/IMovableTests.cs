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
        /// <summary>
        /// Verifies that a unit's own movement takes precedence over inherited movement.
        /// </summary>
        [Test]
        public void GetTransitMovement_WithDirectMovement_ReturnsDirectMovement()
        {
            MovementState movement = new MovementState();
            IMovable movable = new Officer { Movement = movement };

            MovementState result = movable.GetTransitMovement();

            Assert.AreSame(movement, result);
        }

        /// <summary>
        /// Verifies that a passenger inherits movement from its capital ship.
        /// </summary>
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

        /// <summary>
        /// Verifies that a passenger inherits movement from its fleet when its ship is stationary.
        /// </summary>
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

        /// <summary>
        /// Verifies that a stationary unit uses its containing planet's position.
        /// </summary>
        [Test]
        public void GetPosition_StationaryAtPlanet_ReturnsPlanetPosition()
        {
            Planet planet = new Planet { PositionX = 12, PositionY = 34 };
            Fleet fleet = new Fleet();
            fleet.SetParent(planet);

            Point result = ((IMovable)fleet).GetPosition();

            Assert.AreEqual(new Point(12, 34), result);
        }

        /// <summary>
        /// Verifies that a moving unit uses its movement state's current position.
        /// </summary>
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

        /// <summary>
        /// Verifies that a moving unit's current position can be updated.
        /// </summary>
        [Test]
        public void SetPosition_InTransit_UpdatesCurrentPosition()
        {
            IMovable movable = new Fleet { Movement = new MovementState() };

            movable.SetPosition(new Point(90, 123));

            Assert.AreEqual(new Point(90, 123), movable.Movement.CurrentPosition);
        }

        /// <summary>
        /// Verifies that a stationary unit cannot be assigned an in-transit position.
        /// </summary>
        [Test]
        public void SetPosition_WithoutMovement_ThrowsInvalidOperationException()
        {
            IMovable movable = new Fleet { DisplayName = "Test Fleet" };

            Assert.Throws<InvalidOperationException>(() => movable.SetPosition(Point.Empty));
        }
    }
}
