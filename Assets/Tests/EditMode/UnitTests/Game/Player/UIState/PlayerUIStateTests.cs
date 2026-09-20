using System;
using NUnit.Framework;
using Rebellion.Game.UIState;

namespace Rebellion.Tests.Game.UIState
{
    [TestFixture]
    public sealed class PlayerUIStateTests
    {
        [Test]
        public void GetOrCreateSection_MissingSection_CreatesSection()
        {
            PlayerUIState state = new PlayerUIState();

            UIStateSection section = state.GetOrCreateSection("Strategy");

            Assert.AreEqual("Strategy", section.SectionID);
            CollectionAssert.Contains(state.UIStateSections, section);
        }

        [Test]
        public void GetOrCreateSection_ExistingSection_ReturnsExistingSection()
        {
            UIStateSection existing = new UIStateSection { SectionID = "Strategy" };
            PlayerUIState state = new PlayerUIState { UIStateSections = { existing } };

            UIStateSection section = state.GetOrCreateSection("Strategy");

            Assert.AreSame(existing, section);
            Assert.AreEqual(1, state.UIStateSections.Count);
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase(" ")]
        public void GetOrCreateSection_InvalidSectionID_ThrowsArgumentException(string sectionID)
        {
            PlayerUIState state = new PlayerUIState();

            Assert.Throws<ArgumentException>(() => state.GetOrCreateSection(sectionID));
        }
    }
}
