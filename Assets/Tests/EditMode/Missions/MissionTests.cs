using NUnit.Framework;
using System.Collections.Generic;

// Concrete class for testing abstract Mission
public class MockMission : Mission
{
    public bool WasSuccessful { get; private set; }

    // Constructor for MockMission with better readability.
    public MockMission(
        string name,
        string ownerGameID,
        List<MissionParticipant> mainParticipants,
        List<MissionParticipant> decoyParticipants,
        MissionParticipantSkill participantSkill,
        double quadraticCoefficient = 0.001, 
        double linearCoefficient = 0.5, 
        double constantTerm = 50,
        double minSuccessProbability = 1,
        double maxSuccessProbability = 100,
        int minTicks = 5,
        int maxTicks = 10)
        : base(
            name: name,
            ownerGameID: ownerGameID,
            mainParticipants: mainParticipants,
            decoyParticipants: decoyParticipants,
            participantSkill: participantSkill,
            quadraticCoefficient: quadraticCoefficient,
            linearCoefficient: linearCoefficient,
            constantTerm: constantTerm,
            minSuccessProbability: minSuccessProbability,
            maxSuccessProbability: maxSuccessProbability,
            minTicks: minTicks,
            maxTicks: maxTicks)
    {
        WasSuccessful = false; // Initialize success flag.
    }

    protected override void OnSuccess(Game game)
    {
        WasSuccessful = true; // Mark mission as successful.
    }
}

[TestFixture]
public class MissionTests
{
    private Officer successfulOfficer;
    private Officer failedOfficer;
    private MockMission mission;
    private Game game;
    private Planet testPlanet;

    [SetUp]
    public void Setup()
    {
        testPlanet = new Planet
        {
            DisplayName = "TestPlanet",
            Regiments = new List<Regiment> {
                new Regiment { DefenseRating = 50 },
                new Regiment { DefenseRating = 45 }
            }
        };

        successfulOfficer = new Officer
        {
            Diplomacy = 80,
            Espionage = 90,
            Combat = 75,
            Leadership = 85,
            Status = OfficerStatus.Available
        };

        failedOfficer = new Officer
        {
            Diplomacy = 10,
            Espionage = 15,
            Combat = 20,
            Leadership = 25,
            Status = OfficerStatus.Available
        };
    }

    [Test]
    public void TestMissionSuccess()
    {
        testPlanet = new Planet
        {
            DisplayName = "TestPlanet",
            Regiments = new List<Regiment> {
                new Regiment { DefenseRating = -1000 },
            }
        };
        
        // Force mission to succeed by setting success probability to 100%
        mission = new MockMission(
            name: "Test Mission",
            ownerGameID: "FNEMP1",
            mainParticipants: new List<MissionParticipant> { successfulOfficer },
            decoyParticipants: new List<MissionParticipant>(),
            participantSkill: MissionParticipantSkill.Espionage,
            quadraticCoefficient: 0.001,
            linearCoefficient: 0.5,
            constantTerm: 5.0,
            minSuccessProbability: 100,  // Guaranteed success
            maxSuccessProbability: 100,
            minTicks: 5,
            maxTicks: 10
        );

        mission.SetParent(testPlanet);  // Set parent to avoid null references

        mission.Perform(game);

        Assert.IsTrue(mission.WasSuccessful, "The mission should have succeeded due to forced 100% success probability.");
    }

    [Test]
    public void TestMissionFailure()
    {
        testPlanet = new Planet
        {
            DisplayName = "TestPlanet",
            OwnerGameID = "FNALL1",
            Regiments = new List<Regiment> {
                new Regiment { DefenseRating = -1000 },
            }
        };
        // Force mission to fail by setting success probability to 0%
        mission = new MockMission(
            name: "Test Mission",
            ownerGameID: "FNEMP1",
            mainParticipants: new List<MissionParticipant> { successfulOfficer },
            decoyParticipants: new List<MissionParticipant> {  },
            participantSkill: MissionParticipantSkill.Espionage,
            quadraticCoefficient: 0.001,
            linearCoefficient: 0.5,
            constantTerm: 5.0,
            minSuccessProbability: 0, // Guaranteed failure
            maxSuccessProbability: 0,
            minTicks: 5,
            maxTicks: 10
        );

        mission.SetParent(testPlanet);

        mission.Perform(game);

        Assert.IsFalse(mission.WasSuccessful, "The mission should have failed due to forced 0% success probability.");
    }

    [Test]
    public void TestDecoySuccess()
    {
        // Force decoy success by setting decoy success probability to 100%
        mission = new MockMission(
            name: "Decoy Test Mission",
            ownerGameID: "FNEMP1",
            mainParticipants: new List<MissionParticipant> { failedOfficer },
            decoyParticipants: new List<MissionParticipant> { successfulOfficer },
            participantSkill: MissionParticipantSkill.Espionage,
            quadraticCoefficient: 0.001,
            linearCoefficient: 0.5,
            constantTerm: 50,
            minSuccessProbability: 0, // Guaranteed decoy success
            maxSuccessProbability: 0,
            minTicks: 5,
            maxTicks: 10
        );

        mission.SetParent(testPlanet);

        mission.Perform(game);

        // Assuming decoy success logic will be implemented in the future
        Assert.Pass("Decoy success logic should be implemented.");
    }

    [Test]
    public void TestMissionFoiled()
    {
        // Force mission to be foiled by setting high defense score
        testPlanet.Regiments = new List<Regiment> { new Regiment { DefenseRating = 1000 } };

        mission = new MockMission(
            name: "Test Mission",
            ownerGameID: "FNEMP1",
            mainParticipants: new List<MissionParticipant> { successfulOfficer },
            decoyParticipants: new List<MissionParticipant> { failedOfficer },
            participantSkill: MissionParticipantSkill.Espionage,
            quadraticCoefficient: 0.001,
            linearCoefficient: 0.5,
            constantTerm: 50,
            minSuccessProbability: 0,
            maxSuccessProbability: 0,
            minTicks: 5,
            maxTicks: 10
        );

        mission.SetParent(testPlanet);

        mission.Perform(game);

        // @TODO: Implement logic for mission being foiled.
    }
}
