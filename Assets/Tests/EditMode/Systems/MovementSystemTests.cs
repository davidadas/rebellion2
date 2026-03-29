using System.Drawing;
using NUnit.Framework;
using Rebellion.Core.Configuration;
using Rebellion.Game;
using Rebellion.Systems;

namespace Rebellion.Tests.Systems
{
    [TestFixture]
    public class MovementSystemTests
    {
        // Builds a minimal scene: two planets in the same system, an officer parented to
        // the origin planet, and a MovementSystem ready to use.
        private (
            GameRoot game,
            Planet origin,
            Planet destination,
            Officer officer,
            MovementSystem movement
        ) BuildScene()
        {
            GameConfig config = ConfigLoader.LoadGameConfig();
            GameRoot game = new GameRoot(config);

            Faction faction = new Faction { InstanceID = "empire" };
            game.Factions.Add(faction);

            PlanetSystem system = new PlanetSystem
            {
                InstanceID = "sys1",
                PositionX = 0,
                PositionY = 0,
            };
            game.AttachNode(system, game.GetGalaxyMap());

            Planet origin = new Planet
            {
                InstanceID = "p1",
                OwnerInstanceID = "empire",
                IsColonized = true,
                PositionX = 0,
                PositionY = 0,
            };
            game.AttachNode(origin, system);

            Planet destination = new Planet
            {
                InstanceID = "p2",
                OwnerInstanceID = "empire",
                IsColonized = true,
                PositionX = 100,
                PositionY = 100,
            };
            game.AttachNode(destination, system);

            Officer officer = EntityFactory.CreateOfficer("o1", "empire");
            game.AttachNode(officer, origin);

            MovementSystem movement = new MovementSystem(game, new FogOfWarSystem(game));

            return (game, origin, destination, officer, movement);
        }

        [Test]
        public void RequestMove_ImmediatelyReparentsUnitToDestination()
        {
            (
                GameRoot game,
                Planet origin,
                Planet destination,
                Officer officer,
                MovementSystem movement
            ) = BuildScene();

            movement.RequestMove(officer, destination);

            Assert.AreEqual(destination, officer.GetParent());
        }

        [Test]
        public void RequestMove_UnitIsNoLongerAtOrigin()
        {
            (
                GameRoot game,
                Planet origin,
                Planet destination,
                Officer officer,
                MovementSystem movement
            ) = BuildScene();

            movement.RequestMove(officer, destination);

            Assert.AreNotEqual(origin, officer.GetParent());
        }

        [Test]
        public void RequestMove_SetsMovementStateWithDestination()
        {
            (
                GameRoot game,
                Planet origin,
                Planet destination,
                Officer officer,
                MovementSystem movement
            ) = BuildScene();

            movement.RequestMove(officer, destination);

            Assert.IsNotNull(officer.Movement);
            Assert.AreEqual(destination.InstanceID, officer.Movement.DestinationInstanceID);
        }

        [Test]
        public void RequestMove_SetsOriginPositionFromDeparturePlanet()
        {
            (
                GameRoot game,
                Planet origin,
                Planet destination,
                Officer officer,
                MovementSystem movement
            ) = BuildScene();
            Point expectedOrigin = origin.GetPosition();

            movement.RequestMove(officer, destination);

            Assert.AreEqual(expectedOrigin, officer.Movement.OriginPosition);
        }

        [Test]
        public void RequestMove_SetsTransitTicksGreaterThanZero()
        {
            (
                GameRoot game,
                Planet origin,
                Planet destination,
                Officer officer,
                MovementSystem movement
            ) = BuildScene();

            movement.RequestMove(officer, destination);

            Assert.Greater(officer.Movement.TransitTicks, 0);
        }

        [Test]
        public void RequestMove_WhenUnitAlreadyInTransit_Throws()
        {
            (
                GameRoot game,
                Planet origin,
                Planet destination,
                Officer officer,
                MovementSystem movement
            ) = BuildScene();
            officer.Movement = new MovementState
            {
                DestinationInstanceID = destination.InstanceID,
                TransitTicks = 5,
                TicksElapsed = 0,
            };

            Assert.Throws<System.InvalidOperationException>(() =>
                movement.RequestMove(officer, destination)
            );
        }

        [Test]
        public void RequestMove_WhenUnitNotAtAnyPlanet_Throws()
        {
            (
                GameRoot game,
                Planet origin,
                Planet destination,
                Officer officer,
                MovementSystem movement
            ) = BuildScene();
            game.DetachNode(officer);

            Assert.Throws<System.InvalidOperationException>(() =>
                movement.RequestMove(officer, destination)
            );
        }

        [Test]
        public void UpdateMovement_WhenNotInTransit_DoesNothing()
        {
            (
                GameRoot game,
                Planet origin,
                Planet destination,
                Officer officer,
                MovementSystem movement
            ) = BuildScene();

            Assert.DoesNotThrow(() => movement.ProcessTick());
            Assert.IsNull(officer.Movement);
        }

        [Test]
        public void UpdateMovement_InTransit_IncrementsElapsedTicks()
        {
            (
                GameRoot game,
                Planet origin,
                Planet destination,
                Officer officer,
                MovementSystem movement
            ) = BuildScene();
            movement.RequestMove(officer, destination);

            movement.UpdateMovement(officer);

            Assert.AreEqual(1, officer.Movement.TicksElapsed);
        }

        [Test]
        public void UpdateMovement_OnArrival_ClearsMovementState()
        {
            (
                GameRoot game,
                Planet origin,
                Planet destination,
                Officer officer,
                MovementSystem movement
            ) = BuildScene();
            movement.RequestMove(officer, destination);
            officer.Movement.TicksElapsed = officer.Movement.TransitTicks;

            movement.UpdateMovement(officer);

            Assert.IsNull(officer.Movement);
        }

        [Test]
        public void UpdateMovement_OnArrival_UnitRemainsAtDestination()
        {
            (
                GameRoot game,
                Planet origin,
                Planet destination,
                Officer officer,
                MovementSystem movement
            ) = BuildScene();
            movement.RequestMove(officer, destination);
            officer.Movement.TicksElapsed = officer.Movement.TransitTicks;

            movement.UpdateMovement(officer);

            Assert.AreEqual(destination, officer.GetParent());
        }
    }
}
