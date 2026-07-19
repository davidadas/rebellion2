using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

namespace Rebellion.Tests.UI.Runtime.Themes
{
    [TestFixture]
    public class FactionThemeLibraryTests
    {
        private FactionThemeLibrary _library;

        [SetUp]
        public void SetUp()
        {
            _library = new FactionThemeLibrary();
        }

        [Test]
        public void GetTheme_ConfiguredFaction_ReturnsExactTheme()
        {
            FactionTheme theme = _library.GetTheme("FNALL1");

            Assert.AreEqual("FNALL1", theme.FactionInstanceID);
        }

        [Test]
        public void GetTheme_EmptyOrUnknownFaction_ReturnsDefaultTheme()
        {
            FactionTheme nullTheme = _library.GetTheme(null);
            FactionTheme emptyTheme = _library.GetTheme(string.Empty);
            FactionTheme unknownTheme = _library.GetTheme("missing-faction");

            Assert.AreEqual("DEFAULT", nullTheme.FactionInstanceID);
            Assert.AreSame(nullTheme, emptyTheme);
            Assert.AreSame(nullTheme, unknownTheme);
        }

        [Test]
        public void GetAllThemes_MutatedResult_DoesNotChangeLibraryContents()
        {
            List<FactionTheme> themes = _library.GetAllThemes();
            string[] configuredIds = themes.Select(theme => theme.FactionInstanceID).ToArray();

            themes.Clear();

            Assert.IsNotEmpty(configuredIds);
            CollectionAssert.AreEqual(
                configuredIds,
                _library.GetAllThemes().Select(theme => theme.FactionInstanceID).ToArray()
            );
            Assert.IsFalse(configuredIds.Contains("DEFAULT"));
        }
    }
}
