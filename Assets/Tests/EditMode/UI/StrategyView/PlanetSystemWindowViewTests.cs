using System.Collections.Generic;
using NUnit.Framework;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Units;
using Rebellion.SceneGraph;

namespace Rebellion.Tests.UI.StrategyView
{
    [TestFixture]
    public sealed class PlanetSystemWindowViewTests
    {
        [Test]
        public void CreateTargetForHit_CreateMissionOnPlanetOverlayIcon_TargetsPlanet()
        {
            PlanetSystemWindowHit hit = CreateHit(PlanetIcon.Facility, false);
            Fleet fleet = new Fleet();
            TargetingRequest request = CreateRequest(StrategyContextMenuActions.CreateMission);

            StrategyMissionTarget target = PlanetSystemWindowView.CreateTargetForHit(
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
            Fleet fleet = new Fleet();
            TargetingRequest request = CreateRequest(StrategyContextMenuActions.Destination);

            StrategyMissionTarget target = PlanetSystemWindowView.CreateTargetForHit(
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
            Fleet fleet = new Fleet();
            TargetingRequest request = CreateRequest(StrategyContextMenuActions.Move);

            StrategyMissionTarget target = PlanetSystemWindowView.CreateTargetForHit(
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
            PlanetSystem system = new PlanetSystem();
            Planet planet = new Planet();
            GalaxyMapPlanet galaxyMapPlanet = new GalaxyMapPlanet(system, planet, string.Empty);
            return new PlanetSystemWindowHit(galaxyMapPlanet, planet, icon, planetImage);
        }

        private sealed class TestTargetingReceiver : ITargetingReceiver
        {
            public void OnTargetSelected(TargetingRequest request, object target) { }

            public void OnTargetingCancelled(TargetingRequest request) { }
        }
    }
}
