using System;
using NUnit.Framework;
using Rebellion.Game.UIState;

namespace Rebellion.Tests.Game.UIState
{
    [TestFixture]
    public sealed class PlayerUIStateTests
    {
        /// <summary>
        /// Verifies that requesting an absent section creates and stores it.
        /// </summary>
        [Test]
        public void GetOrCreateSection_MissingSection_CreatesSection()
        {
            PlayerUIState state = new PlayerUIState();

            UIStateSection section = state.GetOrCreateSection("Strategy");

            Assert.AreEqual("Strategy", section.SectionID);
            CollectionAssert.Contains(state.UIStateSections, section);
        }

        /// <summary>
        /// Verifies that requesting an existing section reuses the stored instance.
        /// </summary>
        [Test]
        public void GetOrCreateSection_ExistingSection_ReturnsExistingSection()
        {
            UIStateSection existing = new UIStateSection { SectionID = "Strategy" };
            PlayerUIState state = new PlayerUIState { UIStateSections = { existing } };

            UIStateSection section = state.GetOrCreateSection("Strategy");

            Assert.AreSame(existing, section);
            Assert.AreEqual(1, state.UIStateSections.Count);
        }

        /// <summary>
        /// Verifies that section identifiers must contain non-whitespace text.
        /// </summary>
        /// <param name="sectionID">The invalid identifier under test.</param>
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
