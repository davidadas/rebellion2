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
        public void Deserialize_ArtworkDefaults_PreservesEveryUnitBackground()
        {
            const string xml =
                @"
                <FactionTheme>
                  <ArtworkDefaults>
                    <OfficerBackgroundImagePath>Pack/Art/officer-background.png</OfficerBackgroundImagePath>
                    <CapitalShipBackgroundImagePath>Pack/Art/ship-background.png</CapitalShipBackgroundImagePath>
                    <StarfighterBackgroundImagePath>Pack/Art/starfighter-background.png</StarfighterBackgroundImagePath>
                  </ArtworkDefaults>
                </FactionTheme>
                ";
            using StringReader reader = new StringReader(xml);

            FactionTheme theme = (FactionTheme)
                new GameSerializer(typeof(FactionTheme)).Deserialize(reader);

            Assert.AreEqual(
                "Pack/Art/officer-background.png",
                theme.ArtworkDefaults.OfficerBackgroundImagePath
            );
            Assert.AreEqual(
                "Pack/Art/ship-background.png",
                theme.ArtworkDefaults.CapitalShipBackgroundImagePath
            );
            Assert.AreEqual(
                "Pack/Art/starfighter-background.png",
                theme.ArtworkDefaults.StarfighterBackgroundImagePath
            );
        }

        [Test]
        public void Deserialize_ColorScheme_PreservesEveryFactionMaskColor()
        {
            const string xml =
                @"
                <FactionTheme>
                  <FactionPrimaryColorHex>010203</FactionPrimaryColorHex>
                  <ColorScheme>
                    <PrimaryColorHex>111213</PrimaryColorHex>
                    <SelectionColorHex>212223</SelectionColorHex>
                    <ActiveWindowTitleColorHex>313233</ActiveWindowTitleColorHex>
                    <InactiveWindowTitleColorHex>414243</InactiveWindowTitleColorHex>
                    <ActiveBuildingPaneColorHex>515253</ActiveBuildingPaneColorHex>
                    <InactiveBuildingPaneColorHex>616263</InactiveBuildingPaneColorHex>
                    <PlanetMarkerColorHex>717273</PlanetMarkerColorHex>
                  </ColorScheme>
                </FactionTheme>
                ";
            using StringReader reader = new StringReader(xml);

            FactionTheme theme = (FactionTheme)
                new GameSerializer(typeof(FactionTheme)).Deserialize(reader);

            Assert.AreEqual(new Color32(0x11, 0x12, 0x13, 0xff), (Color32)theme.GetPrimaryColor());
            Assert.AreEqual(
                new Color32(0x21, 0x22, 0x23, 0xff),
                (Color32)theme.GetSelectionColor()
            );
            Assert.AreEqual(
                new Color32(0x31, 0x32, 0x33, 0xff),
                (Color32)theme.GetActiveWindowTitleColor()
            );
            Assert.AreEqual(
                new Color32(0x41, 0x42, 0x43, 0xff),
                (Color32)theme.GetInactiveWindowTitleColor()
            );
            Assert.AreEqual(
                new Color32(0x51, 0x52, 0x53, 0xff),
                (Color32)theme.GetActiveBuildingPaneColor()
            );
            Assert.AreEqual(
                new Color32(0x61, 0x62, 0x63, 0xff),
                (Color32)theme.GetInactiveBuildingPaneColor()
            );
            Assert.AreEqual(
                new Color32(0x71, 0x72, 0x73, 0xff),
                (Color32)theme.GetPlanetMarkerColor()
            );
        }

        [Test]
        public void GetMaskColors_MissingColorScheme_FallBackToPrimaryColor()
        {
            FactionTheme theme = new FactionTheme { FactionPrimaryColorHex = "#123456" };
            Color32 expected = new Color32(0x12, 0x34, 0x56, 0xff);

            Assert.AreEqual(expected, (Color32)theme.GetSelectionColor());
            Assert.AreEqual(expected, (Color32)theme.GetActiveWindowTitleColor());
            Assert.AreEqual(expected, (Color32)theme.GetInactiveWindowTitleColor());
            Assert.AreEqual(expected, (Color32)theme.GetActiveBuildingPaneColor());
            Assert.AreEqual(expected, (Color32)theme.GetInactiveBuildingPaneColor());
            Assert.AreEqual(expected, (Color32)theme.GetPlanetMarkerColor());
        }

        [Test]
        public void GetPrimaryColor_ValidHex_ReturnsParsedColor()
        {
            FactionTheme theme = new FactionTheme { FactionPrimaryColorHex = "#123456" };

            Color color = theme.GetPrimaryColor();

            Assert.AreEqual(new Color32(0x12, 0x34, 0x56, 0xff), (Color32)color);
        }

        [Test]
        public void GetPrimaryColor_BlankSchemePrimary_ReturnsLegacyPrimary()
        {
            FactionTheme theme = new()
            {
                FactionPrimaryColorHex = "#123456",
                ColorScheme = new FactionColorScheme { PrimaryColorHex = string.Empty },
            };

            Assert.AreEqual(new Color32(0x12, 0x34, 0x56, 0xff), (Color32)theme.GetPrimaryColor());
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
