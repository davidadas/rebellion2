using NUnit.Framework;

namespace Rebellion.Tests.Content.Presentation
{
    [TestFixture]
    public sealed class BattleAlertWindowThemeTests
    {
        [TestCase(false, "standard", "personnel", "summary", "standard")]
        [TestCase(false, " ", "personnel", "summary", "summary")]
        [TestCase(false, null, "personnel", null, null)]
        [TestCase(true, "standard", "personnel", "summary", "personnel")]
        [TestCase(true, "standard", " ", "summary", "standard")]
        [TestCase(true, null, null, "summary", null)]
        public void GetResultListBackgroundImagePath_ConfiguredColumns_UsesTheMatchingFallback(
            bool personnel,
            string standard,
            string officers,
            string summary,
            string expected
        )
        {
            BattleAlertWindowTheme theme = new()
            {
                ResultListBackgroundImagePath = standard,
                ResultPersonnelListBackgroundImagePath = officers,
                SummaryBackgroundImagePath = summary,
            };

            Assert.AreEqual(expected, theme.GetResultListBackgroundImagePath(personnel));
        }
    }
}
