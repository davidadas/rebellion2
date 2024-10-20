// using NUnit.Framework;

// [TestFixture]
// public class GameManagerTests
// {
//     private GameManager gameManager;
//     private Game game;

//     [SetUp]
//     public void Setup()
//     {
//         // Initialize the game and GameManager before each test
//         game = new Game();
//         gameManager = new GameManager(game);
//     }

//     [Test]
//     public void TestSetTickSpeedFastProcessesTickEvery1Second()
//     {
//         // Set tick speed to Fast (1 second per tick)
//         gameManager.SetTickSpeed(TickSpeed.Fast);

//         // Simulate 1 second passing
//         gameManager.Update(1f);

//         // Assert that one tick has been processed
//         Assert.AreEqual(1, game.CurrentTick, "Game should process one tick after 1 second at Fast speed.");
//     }

//     [Test]
//     public void TestSetTickSpeedMediumProcessesTickEvery10Seconds()
//     {
//         // Set tick speed to Medium (10 seconds per tick)
//         gameManager.SetTickSpeed(TickSpeed.Medium);

//         // Simulate 9 seconds passing (should not trigger a tick yet)
//         gameManager.Update(9f);
//         Assert.AreEqual(0, game.CurrentTick, "Game should not process a tick before 10 seconds at Medium speed.");

//         // Simulate 1 more second (total 10 seconds, which should trigger a tick)
//         gameManager.Update(1f);
//         Assert.AreEqual(1, game.CurrentTick, "Game should process one tick after 10 seconds at Medium speed.");
//     }

//     [Test]
//     public void TestSetTickSpeedSlowProcessesTickEvery60Seconds()
//     {
//         // Set tick speed to Slow (60 seconds per tick)
//         gameManager.SetTickSpeed(TickSpeed.Slow);

//         // Simulate 59 seconds passing (should not trigger a tick yet)
//         gameManager.Update(59f);
//         Assert.AreEqual(0, game.CurrentTick, "Game should not process a tick before 60 seconds at Slow speed.");

//         // Simulate 1 more second (total 60 seconds, which should trigger a tick)
//         gameManager.Update(1f);
//         Assert.AreEqual(1, game.CurrentTick, "Game should process one tick after 60 seconds at Slow speed.");
//     }

//     [Test]
//     public void TestSetTickSpeedPausedNoTicksProcessed()
//     {
//         // Set tick speed to Paused (no ticking)
//         gameManager.SetTickSpeed(TickSpeed.Paused);

//         // Simulate some time passing (should not trigger any ticks)
//         gameManager.Update(100f);
//         Assert.AreEqual(0, game.CurrentTick, "Game should not process any ticks when the tick speed is paused.");
//     }

//     [Test]
//     public void TestSetTickSpeedSwitchSpeedsDuringGame()
//     {
//         // Set tick speed to Fast and simulate 1 second
//         gameManager.SetTickSpeed(TickSpeed.Fast);
//         gameManager.Update(1f);
//         Assert.AreEqual(1, game.CurrentTick, "Game should process one tick after 1 second at Fast speed.");

//         // Switch to Medium speed and simulate 10 seconds
//         gameManager.SetTickSpeed(TickSpeed.Medium);
//         gameManager.Update(10f);
//         Assert.AreEqual(2, game.CurrentTick, "Game should process one tick after 10 seconds at Medium speed.");

//         // Switch to Slow speed and simulate 60 seconds
//         gameManager.SetTickSpeed(TickSpeed.Slow);
//         gameManager.Update(60f);
//         Assert.AreEqual(3, game.CurrentTick, "Game should process one tick after 60 seconds at Slow speed.");
//     }
// }
