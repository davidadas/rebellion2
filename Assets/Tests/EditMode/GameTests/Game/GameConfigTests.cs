using System;
using System.Reflection;
using NUnit.Framework;
using Rebellion.Game;

namespace Rebellion.Tests.Game
{
    [TestFixture]
    public class GameConfigTests
    {
        [Test]
        public void GetConfig_ConfigNotSet_ThrowsException()
        {
            GameRoot game = new GameRoot();

            Assert.Throws<InvalidOperationException>(
                () => game.GetConfig(),
                "GetConfig should throw when config not set"
            );
        }

        [Test]
        public void GameRoot_ConfigConstructor_SetsConfig()
        {
            GameConfig config = TestConfig.Create();
            GameRoot game = new GameRoot(config);

            Assert.IsNotNull(game.Config, "Game.Config should not be null");
            Assert.AreEqual(config, game.Config, "Config should be the same instance");
        }

        [Test]
        public void SetConfig_ValidConfig_SetsConfig()
        {
            GameConfig config = TestConfig.Create();
            GameRoot game = new GameRoot();

            game.SetConfig(config);

            Assert.AreEqual(config, game.GetConfig(), "Config should be set correctly");
        }

        [Test]
        public void GameManager_SetGameSpeed_UsesConfiguredIntervals()
        {
            GameConfig config = TestConfig.Create();
            config.GameSpeed.FastTickIntervalSeconds = 2.5f;
            config.GameSpeed.MediumTickIntervalSeconds = 12.5f;
            config.GameSpeed.SlowTickIntervalSeconds = 90.5f;
            config.GameSpeed.VerySlowTickIntervalSeconds = 120.5f;
            GameRoot game = new GameRoot(config);
            GameManager manager = TestContent.CreateGameManager(game);

            manager.SetGameSpeed(TickSpeed.Fast);
            Assert.AreEqual(2.5f, GetTickInterval(manager));

            manager.SetGameSpeed(TickSpeed.Medium);
            Assert.AreEqual(12.5f, GetTickInterval(manager));

            manager.SetGameSpeed(TickSpeed.Slow);
            Assert.AreEqual(90.5f, GetTickInterval(manager));

            manager.SetGameSpeed(TickSpeed.VerySlow);
            Assert.AreEqual(120.5f, GetTickInterval(manager));
        }

        private static float? GetTickInterval(GameManager manager)
        {
            FieldInfo field = typeof(GameManager).GetField(
                "_tickInterval",
                BindingFlags.Instance | BindingFlags.NonPublic
            );

            return (float?)field.GetValue(manager);
        }
    }
}
