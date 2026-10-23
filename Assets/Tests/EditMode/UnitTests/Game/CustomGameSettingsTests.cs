using System.IO;
using NUnit.Framework;
using Rebellion.Game;

namespace Rebellion.Tests.Game
{
    [TestFixture]
    public sealed class CustomGameSettingsTests
    {
        [Test]
        public void ApplyRuntimeRules_DistanceDivisorOverride_SetsRuntimeMovementRule()
        {
            GameConfig config = new GameConfig();
            CustomGameSettings settings = new CustomGameSettings { DistanceDivisor = 7.5 };

            settings.ApplyRuntimeRules(config);

            Assert.AreEqual(7.5, config.Movement.DistanceDivisor);
        }

        [Test]
        public void ApplyRuntimeRules_UnspecifiedDistanceDivisor_PreservesRuntimeMovementRule()
        {
            GameConfig config = new GameConfig();
            config.Movement.DistanceDivisor = 2.5;

            new CustomGameSettings().ApplyRuntimeRules(config);

            Assert.AreEqual(2.5, config.Movement.DistanceDivisor);
        }

        [TestCase(0)]
        [TestCase(-1)]
        [TestCase(double.NaN)]
        [TestCase(double.PositiveInfinity)]
        public void ApplyRuntimeRules_InvalidDistanceDivisor_ThrowsInvalidDataException(
            double invalid
        )
        {
            GameConfig config = new GameConfig();
            CustomGameSettings settings = new CustomGameSettings { DistanceDivisor = invalid };

            InvalidDataException exception = Assert.Throws<InvalidDataException>(() =>
                settings.ApplyRuntimeRules(config)
            );

            Assert.AreEqual("Movement distance divisor must be positive.", exception.Message);
        }
    }
}
