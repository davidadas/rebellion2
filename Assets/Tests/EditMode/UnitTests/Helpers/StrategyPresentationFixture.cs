using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using Rebellion.Game;
using Rebellion.Game.Encyclopedia;
using UnityEngine;

internal sealed class StrategyPresentationFixture : IDisposable
{
    private readonly Dictionary<string, Texture2D> _textures = new(StringComparer.Ordinal);
    private readonly FactionThemeLibrary _themes;

    /// <summary>
    /// Loads the strategy themes and records their declared texture addresses.
    /// </summary>
    internal StrategyPresentationFixture()
    {
        FactionThemes themes = new FactionThemes();
        foreach (
            string file in Directory.EnumerateFiles(
                TestPackFixture.SourceRoot,
                "theme.xml",
                SearchOption.AllDirectories
            )
        )
        {
            string xml = File.ReadAllText(file);
            foreach (FactionTheme theme in SerializationHelper.Deserialize<FactionThemes>(xml))
            {
                theme.GalacticInformationDisplay = CreateDisplayTheme();
                themes.Add(theme);
            }

            IEnumerable<string> addresses = XDocument
                .Parse(xml)
                .Descendants("FactionTheme")
                .SelectMany(theme => theme.Elements())
                .Where(section =>
                    section.Name.LocalName is "GalaxyBackground" or "PlanetOverlayTheme"
                )
                .SelectMany(section => section.Descendants())
                .Where(element => !element.HasElements)
                .Select(element => element.Value)
                .Where(address =>
                    address.StartsWith("Pack/", StringComparison.Ordinal)
                    || address.StartsWith("Application/", StringComparison.Ordinal)
                );
            foreach (string address in addresses)
                _textures.TryAdd(address, null);
        }
        _themes = new FactionThemeLibrary(themes);
    }

    /// <summary>
    /// Defines the filters used by the strategy projection tests.
    /// </summary>
    /// <returns>The galactic information display theme.</returns>
    private static GalacticInformationDisplayTheme CreateDisplayTheme()
    {
        return new GalacticInformationDisplayTheme
        {
            ActiveFilterLabelColorHex = "#B0D8F0",
            ActiveFilterLabelFontSize = 18,
            ActiveFilterLabelSourceLayout = new SourceRectLayout
            {
                X = 20,
                Y = 30,
                Width = 180,
                Height = 24,
            },
            Categories =
            {
                new GalacticInformationCategoryTheme
                {
                    Filters =
                    {
                        new GalacticInformationFilterTheme
                        {
                            Mode = GalacticInformationFilterMode.Uprisings,
                            Label = "Uprisings",
                            LowThreshold = 1,
                            MediumThreshold = 1,
                            HighThreshold = 1,
                        },
                        new GalacticInformationFilterTheme
                        {
                            Mode = GalacticInformationFilterMode.IdleShipyards,
                            Label = "Idle shipyards",
                            LowThreshold = 1,
                            MediumThreshold = 1,
                            HighThreshold = 1,
                        },
                        new GalacticInformationFilterTheme
                        {
                            Mode = GalacticInformationFilterMode.IdleFleets,
                            Label = "Idle fleets",
                            LowThreshold = 1,
                            MediumThreshold = 2,
                            HighThreshold = 3,
                        },
                    },
                },
            },
        };
    }

    /// <summary>
    /// Creates a strategy presentation context with test themes and textures.
    /// </summary>
    /// <param name="game">The game that supplies the projected state.</param>
    /// <returns>The presentation context.</returns>
    internal UIContext CreateContext(GameRoot game)
    {
        return new UIContext(
            game,
            _themes,
            new EncyclopediaCatalog(Array.Empty<EncyclopediaEntry>()),
            GetTexture
        );
    }

    /// <summary>
    /// Destroys the textures allocated by this fixture.
    /// </summary>
    public void Dispose()
    {
        foreach (Texture2D texture in _textures.Values)
            UnityEngine.Object.DestroyImmediate(texture);
    }

    /// <summary>
    /// Returns the texture assigned to a fixture address.
    /// </summary>
    /// <param name="address">The requested texture address.</param>
    /// <returns>The fixture texture or null for an unknown address.</returns>
    private Texture2D GetTexture(string address)
    {
        if (!_textures.TryGetValue(address, out Texture2D texture))
            return null;
        if (texture == null)
        {
            texture = new Texture2D(16, 16) { name = address };
            _textures[address] = texture;
        }
        return texture;
    }
}
