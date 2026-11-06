using System.IO;
using NUnit.Framework;
using Rebellion.Game;

namespace Rebellion.Tests.Game
{
    [TestFixture]
    public sealed class CustomGameSettingsTests
    {
        [Test]
        public void CreateCopy_AIDecisionInterval_PreservesOverride()
        {
            CustomGameSettings source = new() { AIDecisionTickInterval = 13 };

            CustomGameSettings copy = source.CreateCopy();
            Assert.AreEqual(13, copy.AIDecisionTickInterval);
            copy.AIDecisionTickInterval = 17;

            Assert.AreEqual(13, source.AIDecisionTickInterval);
        }

        [TestCase(0)]
        [TestCase(-1)]
        public void ApplyRuntimeRules_NonpositiveAIDecisionInterval_RejectsOverride(int interval)
        {
            CustomGameSettings settings = new() { AIDecisionTickInterval = interval };

            Assert.Throws<InvalidDataException>(() => settings.ApplyRuntimeRules(new GameConfig()));
        }
    }
}
