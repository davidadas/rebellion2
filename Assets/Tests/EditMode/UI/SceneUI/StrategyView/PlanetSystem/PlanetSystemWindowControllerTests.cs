using System.Collections.Generic;
using NUnit.Framework;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Units;
using Rebellion.SceneGraph;
using GameFleet = Rebellion.Game.Units.Fleet;
using GamePlanetSystem = Rebellion.Game.Galaxy.PlanetSystem;

namespace Rebellion.Tests.UI.SceneUI.StrategyView.PlanetSystem
{
    [TestFixture]
    public class PlanetSystemWindowControllerTests
    {
        [Test]
        public void CreateTargetForHit_CreateMissionOnPlanetOverlayIcon_TargetsPlanet()
        {
            PlanetSystemWindowHit hit = CreateHit(PlanetIcon.Facility, false);
            GameFleet fleet = new GameFleet();
            TargetingRequest request = CreateRequest(StrategyContextMenuActions.CreateMission);

            StrategyMissionTarget target = PlanetSystemWindowController.CreateTargetForHit(
                hit,
                request,
                fleet
            );

            Assert.IsNotNull(target);
            Assert.AreSame(hit.GalaxyMapPlanet, target.Planet);
            Assert.IsNull(target.Item);
        }

        [Test]
        public void CreateTargetForHit_DestinationOnFleetOverlayIcon_TargetsPlanet()
        {
            PlanetSystemWindowHit hit = CreateHit(PlanetIcon.Fleet, false);
            GameFleet fleet = new GameFleet();
            TargetingRequest request = CreateRequest(StrategyContextMenuActions.Destination);

            StrategyMissionTarget target = PlanetSystemWindowController.CreateTargetForHit(
                hit,
                request,
                fleet
            );

            Assert.IsNotNull(target);
            Assert.AreSame(hit.GalaxyMapPlanet, target.Planet);
            Assert.IsNull(target.Item);
        }

        [Test]
        public void CreateTargetForHit_MoveOnFleetOverlayIcon_TargetsFleet()
        {
            PlanetSystemWindowHit hit = CreateHit(PlanetIcon.Fleet, false);
            GameFleet fleet = new GameFleet();
            TargetingRequest request = CreateRequest(StrategyContextMenuActions.Move);

            StrategyMissionTarget target = PlanetSystemWindowController.CreateTargetForHit(
                hit,
                request,
                fleet
            );

            Assert.IsNotNull(target);
            Assert.AreSame(hit.GalaxyMapPlanet, target.Planet);
            Assert.AreSame(fleet, target.Item);
        }

        private static TargetingRequest CreateRequest(int action)
        {
            return new TargetingRequest(
                StrategyWindowTargetingSource.GetPrompt(action),
                new StrategyWindowTargetingSource(null, action, 0, 0, new List<ISceneNode>()),
                new TestTargetingReceiver()
            );
        }

        private static PlanetSystemWindowHit CreateHit(PlanetIcon icon, bool planetImage)
        {
            GamePlanetSystem system = new GamePlanetSystem();
            Planet planet = new Planet();
            GalaxyMapPlanet galaxyMapPlanet = new GalaxyMapPlanet(system, planet, string.Empty);
            return new PlanetSystemWindowHit(galaxyMapPlanet, icon, planetImage);
        }

        private sealed class TestTargetingReceiver : ITargetingReceiver
        {
            public void OnTargetSelected(TargetingRequest request, object target) { }

            public void OnTargetingCancelled(TargetingRequest request) { }
        }
    }
}
