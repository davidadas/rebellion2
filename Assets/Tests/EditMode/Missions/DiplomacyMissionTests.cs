using NUnit.Framework;
using System.Collections.Generic;

[TestFixture]
public class DiplomacyMissionTests
{
    private Game game;
    private PlanetSystem planetSystem;
    private Planet planet;

    [SetUp]
    public void SetUp()
    {
        // Initialize the faction for the mission.
        Faction faction = new Faction()
        {
            GameID = "FNALL1"
        };

        // Initialize the planet for the mission.
        planet = new Planet()
        {
            OwnerGameID = "FNALL1"
        };

        // Initialize the planet system with the planet.
        planetSystem = new PlanetSystem()
        {
            Planets = new List<Planet>()
            {
                planet
            }
        };

        // Initialize the game and embed the planet system.
        game = new Game()
        {
            Galaxy = new GalaxyMap()
            {
                PlanetSystems = new List<PlanetSystem>()
                {
                    planetSystem
                }
            },
            Factions = new List<Faction>()
            {
                faction
            }
        };
    }

    [Test]
    public void TestMissionInitialization()
    {
        // Initialize participants.
        List<MissionParticipant> mainParticipants = new List<MissionParticipant>
        {
            new MissionParticipant
            {
                Skills = new SerializableDictionary<MissionParticipantSkill, int>()
                {
                    { MissionParticipantSkill.Diplomacy, 0 },
                }
            }
        };

        List<MissionParticipant> decoyParticipants = new List<MissionParticipant>();

        // Initialize the DiplomacyMission and append it to the scene graph.
        DiplomacyMission diplomacyMission = new DiplomacyMission("FNALL1", mainParticipants, decoyParticipants);
        diplomacyMission.SetParent(planet);

        // Verify that the mission is initialized correctly
        Assert.AreEqual("Diplomacy", diplomacyMission.Name);
        Assert.AreEqual(diplomacyMission.ParticipantSkill, MissionParticipantSkill.Diplomacy);
        Assert.AreEqual(mainParticipants, diplomacyMission.GetChildren());
    }

    [Test]
    public void TestOnSuccess()
    {
        // Initialize participants.
        List<MissionParticipant> mainParticipants = new List<MissionParticipant>
        {
            new MissionParticipant
            {
                Skills = new SerializableDictionary<MissionParticipantSkill, int>()
                {
                    { MissionParticipantSkill.Diplomacy, 1000 },
                }
            }
        };

        List<MissionParticipant> decoyParticipants = new List<MissionParticipant>();

        // Initialize the DiplomacyMission and append it to the scene graph.
        DiplomacyMission diplomacyMission = new DiplomacyMission("FNALL1", mainParticipants, decoyParticipants);
        diplomacyMission.SetParent(planet);
        
        // Create a MissionEvent and execute it.
        MissionEvent missionEvent = new MissionEvent(0, diplomacyMission);
        missionEvent.Execute(game);

        // Verify that planetary support has increased.
        Assert.Greater(planet.GetPopularSupport("FNALL1"), 0);
    }

    [Test]
    public void TestOnFailure()
    {
        // Initialize participants
        List<MissionParticipant> mainParticipants = new List<MissionParticipant>
        {
            new MissionParticipant
            {
                Skills = new SerializableDictionary<MissionParticipantSkill, int>()
                {
                    { MissionParticipantSkill.Diplomacy, -1000 },
                }
            }
        };

        List<MissionParticipant> decoyParticipants = new List<MissionParticipant>();

        // Initialize the DiplomacyMission and append it to the scene graph.
        DiplomacyMission diplomacyMission = new DiplomacyMission("FNALL1", mainParticipants, decoyParticipants);
        diplomacyMission.SetParent(planet);

        // Create a MissionEvent and execute it.
        MissionEvent missionEvent = new MissionEvent(0, diplomacyMission);
        missionEvent.Execute(game);

        // @TODO: Verify failure.
    }
}
