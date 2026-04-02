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

            game.Factions.Add(new Faction { InstanceID = "empire" });
            game.Factions.Add(new Faction { InstanceID = "rebels" });

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
        public void RequestMove_WhenUnitAlreadyInTransit_RedirectsFromCurrentPosition()
        {
            (
                GameRoot game,
                Planet origin,
                Planet destination,
                Officer officer,
                MovementSystem movement
            ) = BuildScene();

            movement.RequestMove(officer, destination);

            Point midPoint = new Point(50, 0);
            officer.Movement.CurrentPosition = midPoint;

            movement.RequestMove(officer, origin);

            Assert.AreEqual(
                midPoint,
                officer.Movement.OriginPosition,
                "Redirected unit must start its new journey from its current visual position"
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
        public void RequestMove_WhenDestinationRejectsUnit_DoesNotThrow()
        {
            // Destination ownership changes after scene setup (e.g. enemy captures the planet).
            // RequestMove must not propagate the SceneAccessException — the unit stays at origin.
            (
                GameRoot game,
                Planet origin,
                Planet destination,
                Officer officer,
                MovementSystem movement
            ) = BuildScene();

            destination.OwnerInstanceID = "rebels";

            Assert.DoesNotThrow(
                () => movement.RequestMove(officer, destination),
                "RequestMove must not throw when the destination rejects the unit"
            );
        }

        [Test]
        public void RequestMove_WhenDestinationRejectsUnit_UnitStaysAtOrigin()
        {
            (
                GameRoot game,
                Planet origin,
                Planet destination,
                Officer officer,
                MovementSystem movement
            ) = BuildScene();

            destination.OwnerInstanceID = "rebels";

            try
            {
                movement.RequestMove(officer, destination);
            }
            catch
            { /* ignored for this assertion */
            }

            Assert.AreEqual(
                origin,
                officer.GetParent(),
                "Unit must remain at origin when destination rejects it"
            );
        }

        [Test]
        public void RequestMove_WhenDestinationRejectsUnit_MovementStateNotSet()
        {
            (
                GameRoot game,
                Planet origin,
                Planet destination,
                Officer officer,
                MovementSystem movement
            ) = BuildScene();

            destination.OwnerInstanceID = "rebels";

            try
            {
                movement.RequestMove(officer, destination);
            }
            catch
            { /* ignored for this assertion */
            }

            Assert.IsNull(
                officer.Movement,
                "Movement state must not be set when the destination rejected the unit"
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

        [Test]
        public void RequestMove_CapturedOfficer_IsNotMoved()
        {
            (
                GameRoot game,
                Planet origin,
                Planet destination,
                Officer officer,
                MovementSystem movement
            ) = BuildScene();
            officer.IsCaptured = true;

            movement.RequestMove(officer, destination);

            Assert.AreEqual(
                origin,
                officer.GetParent(),
                "Captured officer must not be moved by its owning faction"
            );
        }

        [Test]
        public void RequestGroupMove_NonCapturedUnits_AllMove()
        {
            (
                GameRoot game,
                Planet origin,
                Planet destination,
                Officer officer,
                MovementSystem movement
            ) = BuildScene();

            Officer officer2 = EntityFactory.CreateOfficer("o2", "empire");
            game.AttachNode(officer2, origin);

            movement.RequestGroupMove(
                new System.Collections.Generic.List<IMovable> { officer, officer2 },
                destination
            );

            Assert.AreEqual(destination, officer.GetParent());
            Assert.AreEqual(destination, officer2.GetParent());
        }

        [Test]
        public void RequestGroupMove_CapturedOfficer_WithEscortFromCaptor_BothMove()
        {
            (
                GameRoot game,
                Planet origin,
                Planet destination,
                Officer escort,
                MovementSystem movement
            ) = BuildScene();

            Officer captive = new Officer
            {
                InstanceID = "captive",
                DisplayName = "captive",
                OwnerInstanceID = "rebels",
                IsCaptured = true,
                CaptorInstanceID = "empire",
            };
            game.AttachNode(captive, origin);

            movement.RequestGroupMove(
                new System.Collections.Generic.List<IMovable> { escort, captive },
                destination
            );

            Assert.AreEqual(destination, escort.GetParent(), "Escort should move to destination");
            Assert.AreEqual(destination, captive.GetParent(), "Captive should move with escort");
        }

        [Test]
        public void RequestGroupMove_CapturedOfficer_WithoutEscort_CapturedOfficerNotMoved()
        {
            (
                GameRoot game,
                Planet origin,
                Planet destination,
                Officer officer,
                MovementSystem movement
            ) = BuildScene();

            Officer captive = new Officer
            {
                InstanceID = "captive",
                DisplayName = "captive",
                OwnerInstanceID = "rebels",
                IsCaptured = true,
                CaptorInstanceID = "empire",
            };
            game.AttachNode(captive, origin);

            movement.RequestGroupMove(
                new System.Collections.Generic.List<IMovable> { captive },
                destination
            );

            Assert.AreEqual(
                origin,
                captive.GetParent(),
                "Captive must not move without an escort from the capturing faction"
            );
        }

        [Test]
        public void RequestGroupMove_CapturedOfficer_EscortFromWrongFaction_CapturedOfficerNotMoved()
        {
            (
                GameRoot game,
                Planet origin,
                Planet destination,
                Officer escort,
                MovementSystem movement
            ) = BuildScene();

            Officer captive = new Officer
            {
                InstanceID = "captive",
                DisplayName = "captive",
                OwnerInstanceID = "rebels",
                IsCaptured = true,
                CaptorInstanceID = "other",
            };
            game.AttachNode(captive, origin);

            movement.RequestGroupMove(
                new System.Collections.Generic.List<IMovable> { escort, captive },
                destination
            );

            Assert.AreEqual(
                origin,
                captive.GetParent(),
                "Captive must not move when the escort is from a different faction than the captor"
            );
        }
    }
}
