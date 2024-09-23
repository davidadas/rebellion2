using NUnit.Framework;
using System.Collections.Generic;
using System.Linq;

public class PlanetTests
{
    private Planet planet;
    private Game game;

    [SetUp]
    public void Setup()
    {
        planet = new Planet
        {
            IsColonized = true,
            GroundSlots = 5,
            OrbitSlots = 3,
            OwnerGameID = "FNALL1"
        };

        // Generate a game given a summary.
        GameSummary summary = new GameSummary
        {
            GalaxySize = GameSize.Large,
            Difficulty = GameDifficulty.Easy,
            VictoryCondition = GameVictoryCondition.Headquarters,
            ResourceAvailability = GameResourceAvailability.Abundant,
            PlayerFactionID = "FNALL1",
        };

        // Save the file to disk for testing.
        game = new Game
        {
            Summary = summary,
            Galaxy = new GalaxyMap(),
        };
    }

    [Test]
    public void TestAddFleet()
    {
        Fleet fleet = new Fleet { OwnerGameID = "FNALL1" };
        game.AttachNode(planet, fleet);

        Assert.Contains(fleet, planet.Fleets);
    }

    [Test]
    public void TestAddOfficer()
    {
        Officer officer = new Officer { OwnerGameID = "FNALL1" };
        game.AttachNode(planet, officer);

        Assert.Contains(officer, planet.Officers);
    }

    [Test]
    public void TestAddBuilding()
    {
        Building building = new Building { Slot = BuildingSlot.Ground, DisplayName = "Test Building" };
        game.AttachNode(planet, building);
        Building[] buildings = planet.GetBuildings(BuildingSlot.Ground);

        Assert.Contains(building, buildings);
    }

    [Test]
    public void TestRemoveFleet()
    {
        Fleet fleet = new Fleet { OwnerGameID = "FNALL1" };
        game.AttachNode(planet, fleet);
        game.DetachNode(fleet);

        Assert.IsFalse(planet.Fleets.Contains(fleet));
    }

    [Test]
    public void TestRemoveOfficer()
    {
        Officer officer = new Officer { OwnerGameID = "FNALL1" };
        game.AttachNode(planet, officer);
        game.DetachNode(officer);

        Assert.IsFalse(planet.Officers.Contains(officer));
    }

    [Test]
    public void TestRemoveBuilding()
    {
        Building building = new Building { Slot = BuildingSlot.Ground, DisplayName = "Test Building" };
        game.AttachNode(planet, building);
        game.DetachNode(building);

        Assert.IsFalse(planet.Buildings[BuildingSlot.Ground].Contains(building));
    }

    [Test]
    public void TestGetChildren()
    {
        Fleet fleet = new Fleet { OwnerGameID = "FNALL1" };
        Officer officer = new Officer { OwnerGameID = "FNALL1" };
        Building building = new Building { Slot = BuildingSlot.Ground, DisplayName = "Test Building" };

        game.AttachNode(planet, fleet);
        game.AttachNode(planet, officer);
        game.AttachNode(planet, building);

        IEnumerable<SceneNode> children = planet.GetChildren();
        List<SceneNode> expectedChildren = new List<SceneNode> { fleet, officer, building };

        CollectionAssert.AreEquivalent(expectedChildren, children);
    }
}
