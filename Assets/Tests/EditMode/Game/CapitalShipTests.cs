using NUnit.Framework;
using System.Collections.Generic;

[TestFixture]
public class CapitalShipTests
{
    private CapitalShip capitalShip;
    private Game game;

    [SetUp]
    public void Setup()
    {
        capitalShip = new CapitalShip
        {
            OwnerGameID = "FNALL1",
            InitialParentGameID = "Fleet1",
            RequiredResearchLevel = 2
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
    public void TestAddOfficer()
    {
        Officer officer = new Officer { OwnerGameID = "FNALL1" };
        game.AttachNode(capitalShip, officer);

        Assert.Contains(officer, capitalShip.Officers);
    }

    [Test]
    public void TestAddOfficerWithDifferentOwner()
    {
        Officer officer = new Officer { OwnerGameID = "FNEMP1" };

        Assert.Throws<SceneAccessException>(() => game.AttachNode(capitalShip, officer));
    }

    [Test]
    public void TestRemoveOfficer()
    {
        Officer officer = new Officer { OwnerGameID = "FNALL1" };
        game.AttachNode(capitalShip, officer);
        game.DetachNode(officer);

        Assert.IsFalse(capitalShip.Officers.Contains(officer));
    }

    [Test]
    public void TestGetChildren()
    {
        Officer officer1 = new Officer { OwnerGameID = "FNALL1" };
        Officer officer2 = new Officer { OwnerGameID = "FNALL1" };

        game.AttachNode(capitalShip, officer1);
        game.AttachNode(capitalShip, officer2);

        IEnumerable<SceneNode> children = capitalShip.GetChildren();
        List<SceneNode> expectedChildren = new List<SceneNode> { officer1, officer2 };

        CollectionAssert.AreEquivalent(expectedChildren, children);
    }
}
