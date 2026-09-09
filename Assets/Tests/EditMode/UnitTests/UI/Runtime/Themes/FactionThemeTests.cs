using NUnit.Framework;
using UnityEngine;

namespace Rebellion.Tests.UI.Runtime.Themes
{
    [TestFixture]
    public class FactionThemeTests
    {
        [Test]
        public void GetPrimaryColor_ValidHex_ReturnsParsedColor()
        {
            FactionTheme theme = new FactionTheme { FactionPrimaryColorHex = "#123456" };

            Color color = theme.GetPrimaryColor();

            Assert.AreEqual(new Color32(0x12, 0x34, 0x56, 0xff), (Color32)color);
        }

        [Test]
        public void GetPrimaryColor_InvalidHex_ReturnsWhite()
        {
            FactionTheme theme = new FactionTheme { FactionPrimaryColorHex = "not-a-color" };

            Color color = theme.GetPrimaryColor();

            Assert.AreEqual(Color.white, color);
        }

        [Test]
        public void GetPrimaryColor_WhitespaceHex_ReturnsWhite()
        {
            FactionTheme theme = new FactionTheme { FactionPrimaryColorHex = "   " };

            Color color = theme.GetPrimaryColor();

            Assert.AreEqual(Color.white, color);
        }

        [Test]
        public void GetPrimaryColor_AfterFirstRead_ReturnsCachedColor()
        {
            FactionTheme theme = new FactionTheme { FactionPrimaryColorHex = "#123456" };
            Color first = theme.GetPrimaryColor();

            theme.FactionPrimaryColorHex = "#abcdef";
            Color second = theme.GetPrimaryColor();

            Assert.AreEqual(first, second);
        }
    }
}
