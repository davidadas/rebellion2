using System.IO;
using NUnit.Framework;
using Rebellion.Util.Serialization;
using UnityEngine;

namespace Rebellion.Tests.UI.Runtime.Themes
{
    [TestFixture]
    public class FactionThemeTests
    {
        [Test]
        public void Deserialize_ShipSpecificCutscenes_ReadsAttributeMappings()
        {
            const string xml =
                @"
                <FactionTheme>
                  <PlanetDestructionCutscenes>
                    <Cutscene ShipTypeID=""destroyer-a"" Path=""Pack/Shared/Cutscenes/movie-a"" />
                    <Cutscene ShipTypeID=""destroyer-b"" Path=""Pack/Shared/Cutscenes/movie-b"" />
                  </PlanetDestructionCutscenes>
                </FactionTheme>
                ";
            using StringReader reader = new StringReader(xml);

            FactionTheme theme = (FactionTheme)
                new GameSerializer(typeof(FactionTheme)).Deserialize(reader);

            Assert.AreEqual(2, theme.PlanetDestructionCutscenes.Count);
            Assert.AreEqual("destroyer-a", theme.PlanetDestructionCutscenes[0].ShipTypeID);
            Assert.AreEqual(
                "Pack/Shared/Cutscenes/movie-a",
                theme.PlanetDestructionCutscenes[0].Path
            );
            Assert.AreEqual("destroyer-b", theme.PlanetDestructionCutscenes[1].ShipTypeID);
            Assert.AreEqual(
                "Pack/Shared/Cutscenes/movie-b",
                theme.PlanetDestructionCutscenes[1].Path
            );
        }

        [Test]
        public void Deserialize_ThemeWithoutCutsceneMappings_LeavesEmptyCollection()
        {
            using StringReader reader = new StringReader("<FactionTheme />");

            FactionTheme theme = (FactionTheme)
                new GameSerializer(typeof(FactionTheme)).Deserialize(reader);

            Assert.IsEmpty(theme.PlanetDestructionCutscenes);
        }

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
