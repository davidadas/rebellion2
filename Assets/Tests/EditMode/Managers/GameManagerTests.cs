using NUnit.Framework;
using UnityEngine.TestTools;
using System.Collections;

// Mock GameEvent for testing
public class MockEvent : GameEvent
{
    public bool WasExecuted { get; private set; }

    public MockEvent(int scheduledTick) : base(scheduledTick)
    {
        WasExecuted = false;
    }

    protected override void TriggerEvent(Game game)
    {
        WasExecuted = true;  // Mark the event as executed when this method is called
    }
}

[TestFixture]
public class GameManagerTests
{
    private GameManager gameManager;
    private MockEvent mockEvent;

    [SetUp]
    public void Setup()
    {
        // Create a GameSummary and a new Game for the test
        GameSummary summary = new GameSummary
        {
            GalaxySize = GameSize.Large,
            Difficulty = GameDifficulty.Medium,
            VictoryCondition = GameVictoryCondition.Headquarters,
            ResourceAvailability = GameResourceAvailability.Normal,
            PlayerFactionID = "FNALL1"
        };

        Game game = new Game(summary);

        // Initialize GameManager with the game instance
        gameManager = new GameManager(game);
    }

    [Test]
    public void TestEventExecutesAtScheduledTick()
    {
        // Create a mock event scheduled to occur at tick 5
        mockEvent = new MockEvent(5);

        gameManager.EventManager.ScheduleEvent(mockEvent);
        gameManager.SetTickSpeed(TickSpeed.Fast);

        // Simulate 4 ticks (the event should not trigger yet)
        for (int i = 0; i < 4; i++)
        {
            gameManager.Update(1f);  // Simulate 1 second of game time (1 tick)
            Assert.IsFalse(mockEvent.WasExecuted, "Event should not have been executed yet.");
        }

        // Simulate 1 more tick (the 5th tick, where the event is scheduled)
        gameManager.Update(1f);  // Simulate 1 second of game time (1 tick)

        Assert.IsTrue(mockEvent.WasExecuted, "Event should have been executed at tick 5.");
    }
}
