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
        game.AttachNode(planet, officer);

        Assert.Contains(officer, planet.Officers, "Officer should be added to the planet.");
    }

    [Test]
    public void TestRemoveOfficer()
    {
        Officer officer = new Officer { OwnerGameID = "FNALL1" };
        game.AttachNode(planet, officer);
        game.DetachNode(officer);

        Assert.IsFalse(planet.Officers.Contains(officer), "Officer should be removed from the planet.");
    }

    [Test]
    public void TestAddSpecialForces()
    {
        SpecialForces specialForces = new SpecialForces { OwnerGameID = "FNALL1" };
        game.AttachNode(planet, specialForces);

        Assert.Contains(specialForces, planet.Regiments, "Special Forces should be added to the planet.");
    }

    [Test]
    public void TestRemoveSpecialForces()
    {
        SpecialForces specialForces = new SpecialForces { OwnerGameID = "FNALL1" };
        game.AttachNode(planet, specialForces);
        game.DetachNode(specialForces);

        Assert.IsFalse(planet.Regiments.Contains(specialForces), "Special Forces should be removed from the planet.");
    }

    [Test]
    public void TestAddStarfighter()
    {
        Starfighter starfighter = new Starfighter { SquadronSize = 12 };
        game.AttachNode(planet, starfighter);

        Assert.Contains(starfighter, planet.GetChildren().OfType<Starfighter>().ToList(), "Starfighter should be added to the planet.");
    }

    [Test]
    public void TestRemoveStarfighter()
    {
        Starfighter starfighter = new Starfighter { SquadronSize = 12 };
        game.AttachNode(planet, starfighter);
        game.DetachNode(starfighter);

        Assert.IsFalse(planet.GetChildren().OfType<Starfighter>().Contains(starfighter), "Starfighter should be removed from the planet.");
    }

    [Test]
    public void TestAddMultipleChildNodes()
    {
        Officer officer = new Officer { OwnerGameID = "FNALL1" };
        SpecialForces specialForces = new SpecialForces { OwnerGameID = "FNALL1" };
        Starfighter starfighter = new Starfighter { SquadronSize = 12 };

        game.AttachNode(planet, officer);
        game.AttachNode(planet, specialForces);
        game.AttachNode(planet, starfighter);

        IEnumerable<SceneNode> children = planet.GetChildren();
        List<SceneNode> expectedChildren = new List<SceneNode> { officer, specialForces, starfighter };

        CollectionAssert.AreEquivalent(expectedChildren, children, "All child nodes should be added to the planet.");
    }

    [Test]
    public void TestRemoveMultipleChildNodes()
    {
        Officer officer = new Officer { OwnerGameID = "FNALL1" };
        SpecialForces specialForces = new SpecialForces { OwnerGameID = "FNALL1" };
        Starfighter starfighter = new Starfighter { SquadronSize = 12 };

        game.AttachNode(planet, officer);
        game.AttachNode(planet, specialForces);
        game.AttachNode(planet, starfighter);

        game.DetachNode(officer);
        game.DetachNode(specialForces);
        game.DetachNode(starfighter);

        Assert.IsFalse(planet.GetChildren().Contains(officer), "Officer should be removed from the planet.");
        Assert.IsFalse(planet.GetChildren().Contains(specialForces), "Special Forces should be removed from the planet.");
        Assert.IsFalse(planet.GetChildren().Contains(starfighter), "Starfighter should be removed from the planet.");
    }

    [Test]
    public void TestSerializeAndDeserialize()
    {
        // Create a planet object to serialize
        Planet originalPlanet = new Planet
        {
            DisplayName = "Test Planet",
            IsColonized = true,
            GroundSlots = 5,
            OrbitSlots = 3,
            OwnerGameID = "FNALL1"
        };

        // Serialize the planet
        string serializedPlanet = SerializationHelper.Serialize(originalPlanet);

        // Deserialize the planet
        Planet deserializedPlanet = SerializationHelper.Deserialize<Planet>(serializedPlanet);

        // Verify that the deserialized object matches the original
        Assert.AreEqual(originalPlanet.DisplayName, deserializedPlanet.DisplayName, "Planet DisplayName should match after deserialization.");
        Assert.AreEqual(originalPlanet.IsColonized, deserializedPlanet.IsColonized, "Planet IsColonized should match after deserialization.");
        Assert.AreEqual(originalPlanet.GroundSlots, deserializedPlanet.GroundSlots, "Planet GroundSlots should match after deserialization.");
        Assert.AreEqual(originalPlanet.OrbitSlots, deserializedPlanet.OrbitSlots, "Planet OrbitSlots should match after deserialization.");
        Assert.AreEqual(originalPlanet.OwnerGameID, deserializedPlanet.OwnerGameID, "Planet OwnerGameID should match after deserialization.");
    }
}
