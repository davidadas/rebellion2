using System;
using System.Collections.Generic;
using System.Linq;
using Rebellion.Game.Missions;
using Rebellion.Game.Units;
using Rebellion.SceneGraph;
using UnityEngine;

/// <summary>
/// Contains the transparent source-resolution pixels drawn over one compact unit portrait.
/// </summary>
public sealed class StrategyUnitCardStatsOverlayData
{
    private readonly Color32[] pixels;

    public int Width { get; }

    public int Height { get; }

    /// <summary>
    /// Creates an immutable top-left-origin overlay pixel buffer.
    /// </summary>
    /// <param name="width">The overlay width in source pixels.</param>
    /// <param name="height">The overlay height in source pixels.</param>
    /// <param name="pixels">The row-major top-left-origin pixels.</param>
    internal StrategyUnitCardStatsOverlayData(int width, int height, Color32[] pixels)
    {
        if (width <= 0)
            throw new ArgumentOutOfRangeException(nameof(width));
        if (height <= 0)
            throw new ArgumentOutOfRangeException(nameof(height));
        if (pixels == null)
            throw new ArgumentNullException(nameof(pixels));
        if (pixels.Length != width * height)
            throw new ArgumentException(
                "The pixel count does not match the overlay dimensions.",
                nameof(pixels)
            );

        Width = width;
        Height = height;
        this.pixels = (Color32[])pixels.Clone();
    }

    /// <summary>
    /// Reads one pixel using the card artwork's top-left coordinate system.
    /// </summary>
    /// <param name="x">The source-space horizontal coordinate.</param>
    /// <param name="y">The source-space vertical coordinate.</param>
    /// <returns>The stored color, including a zero alpha for an empty pixel.</returns>
    public Color32 GetPixel(int x, int y)
    {
        if (x < 0 || x >= Width)
            throw new ArgumentOutOfRangeException(nameof(x));
        if (y < 0 || y >= Height)
            throw new ArgumentOutOfRangeException(nameof(y));

        return pixels[y * Width + x];
    }

    /// <summary>
    /// Copies the overlay into Unity's bottom-left-origin texture pixel order.
    /// </summary>
    /// <param name="destination">The destination texture pixel buffer.</param>
    internal void CopyToTexturePixels(Color32[] destination)
    {
        if (destination == null)
            throw new ArgumentNullException(nameof(destination));
        if (destination.Length != pixels.Length)
            throw new ArgumentException(
                "The texture pixel count does not match the overlay dimensions.",
                nameof(destination)
            );

        for (int y = 0; y < Height; y++)
        {
            int sourceOffset = y * Width;
            int destinationOffset = (Height - y - 1) * Width;
            Array.Copy(pixels, sourceOffset, destination, destinationOffset, Width);
        }
    }
}

/// <summary>
/// Projects unit attributes into the compact pixel graphs used by strategy unit cards.
/// </summary>
internal static class StrategyUnitCardStatsOverlayBuilder
{
    internal static readonly Color32 Black = new Color32(0x00, 0x00, 0x00, 0xFF);
    internal static readonly Color32 Cyan = new Color32(0x4C, 0xB6, 0xE3, 0xFF);
    internal static readonly Color32 Purple = new Color32(0xBB, 0x7B, 0xBB, 0xFF);
    internal static readonly Color32 Red = new Color32(0xF2, 0x58, 0x5D, 0xFF);
    internal static readonly Color32 LightGreen = new Color32(0x73, 0xFA, 0x6B, 0xFF);
    internal static readonly Color32 DarkGreen = new Color32(0x2B, 0xBB, 0x2B, 0xFF);
    internal static readonly Color32 White = new Color32(0xFF, 0xFF, 0xFF, 0xFF);
    internal static readonly Color32 Blue = new Color32(0x4C, 0x8B, 0xE1, 0xFF);
    internal static readonly Color32 Orange = new Color32(0xF6, 0x7F, 0x16, 0xFF);
    internal static readonly Color32 Gold = new Color32(0xF1, 0xC7, 0x22, 0xFF);
    internal static readonly Color32 ResearchYellow = new Color32(0xF0, 0xDB, 0x25, 0xFF);
    internal static readonly Color32 Gray = new Color32(0x75, 0x72, 0x72, 0xFF);
    internal static readonly Color32 HullBrown = new Color32(0x8E, 0x55, 0x1E, 0xFF);
    internal static readonly Color32 JediGreen = new Color32(0x00, 0xFF, 0x00, 0xFF);
    internal static readonly Color32 HiltRed = new Color32(0xFF, 0x00, 0x00, 0xFF);

    private const int _compactWidth = 61;
    private const int _wideWidth = 66;
    private const int _height = 25;
    private static readonly string[] _recruiterPattern =
    {
        "XXXXXX",
        "X####X",
        "X#XX#X",
        "X####X",
        "X#X#XX",
        "X#XX#X",
        "XXXXXX",
    };

    private static readonly string[] _rankAdmiralPattern =
    {
        "XXXXXX",
        "X####X",
        "X#XX#X",
        "X####X",
        "X#XX#X",
        "X#XX#X",
        "XXXXXX",
    };

    private static readonly string[] _rankGeneralPattern =
    {
        "XXXXXX",
        "X####X",
        "X#XXXX",
        "X#X##X",
        "X#XX#X",
        "X####X",
        "XXXXXX",
    };

    private static readonly string[] _rankCommanderPattern =
    {
        "XXXXXX",
        "X####X",
        "X#XXXX",
        "X#X...",
        "X#XXXX",
        "X####X",
        "XXXXXX",
    };

    private static readonly string[] _specialEspionagePattern =
    {
        "XXXXX",
        "X###X",
        "X#XXX",
        "X##X.",
        "X#XXX",
        "X###X",
        "XXXXX",
    };

    private static readonly string[] _specialDecoyPattern =
    {
        "XXXX.",
        "X##XX",
        "X#X#X",
        "X#X#X",
        "X#X#X",
        "X##XX",
        "XXXXX",
    };

    private static readonly string[] _specialSabotagePattern =
    {
        "XXXXX",
        "X###X",
        "X#XXX",
        "X###X",
        "XXX#X",
        "X###X",
        "XXXXX",
    };

    private static readonly string[] _specialUprisingPattern =
    {
        "XXXXX",
        "X#X#X",
        "X#X#X",
        "X#X#X",
        "X#X#X",
        "X###X",
        ".XXXX",
    };

    private static readonly string[] _specialPrisonerPattern =
    {
        "XXXXX",
        "X###X",
        "X#X#X",
        "X###X",
        "X#XXX",
        "X#X..",
        "XXX..",
    };

    private static readonly string[] _specialAssassinationPattern =
    {
        "XXXXX",
        "X###X",
        "X#X#X",
        "X###X",
        "X#X#X",
        "X#X#X",
        "XXXXX",
    };

    private static readonly string[] _specialReconPattern =
    {
        "XXXXX",
        "X###X",
        "X#X#X",
        "X###X",
        "X##XX",
        "X#X#X",
        "XXXXX",
    };

    private static readonly IReadOnlyDictionary<char, string[]> _digitPatterns = new Dictionary<
        char,
        string[]
    >
    {
        ['0'] = new[] { "###", "#.#", "#.#", "#.#", "###" },
        ['1'] = new[] { ".#.", "##.", ".#.", ".#.", "###" },
        ['2'] = new[] { "###", "..#", "###", "#..", "###" },
        ['3'] = new[] { "###", "..#", "###", "..#", "###" },
        ['4'] = new[] { "#.#", "#.#", "###", "..#", "..#" },
        ['5'] = new[] { "###", "#..", "###", "..#", "###" },
        ['6'] = new[] { "###", "#..", "###", "#.#", "###" },
        ['7'] = new[] { "###", "..#", "..#", ".#.", ".#." },
        ['8'] = new[] { "###", "#.#", "###", "#.#", "###" },
        ['9'] = new[] { "###", "#.#", "###", "..#", "###" },
    };

    /// <summary>
    /// Builds the supported graph or capability overlay for one unit.
    /// </summary>
    /// <param name="node">The unit represented by the card.</param>
    /// <returns>The source-resolution overlay, or null for an unsupported node type.</returns>
    public static StrategyUnitCardStatsOverlayData Build(ISceneNode node)
    {
        return node switch
        {
            Officer officer => BuildOfficer(officer),
            SpecialForces specialForces => BuildSpecialForces(specialForces),
            Regiment regiment => BuildRegiment(regiment),
            Starfighter starfighter => BuildStarfighter(starfighter),
            CapitalShip capitalShip => BuildCapitalShip(capitalShip),
            _ => null,
        };
    }

    /// <summary>
    /// Builds the four personnel ratings, capabilities, research ratings, and rank markers.
    /// </summary>
    /// <param name="officer">The represented officer.</param>
    /// <returns>The completed personnel overlay.</returns>
    private static StrategyUnitCardStatsOverlayData BuildOfficer(Officer officer)
    {
        OverlayRaster raster = new OverlayRaster(_compactWidth, _height);
        DrawBarStack(
            raster,
            new[] { 1, 3, 5, 7 },
            new[]
            {
                SolidBar(ScaleRounded(officer.GetEffectiveRating(SkillRating.Diplomacy), 10), Cyan),
                SolidBar(
                    ScaleRounded(officer.GetEffectiveRating(SkillRating.Espionage), 10),
                    Purple
                ),
                SolidBar(ScaleRounded(officer.GetEffectiveRating(SkillRating.Combat), 10), Red),
                SolidBar(
                    ScaleRounded(officer.GetEffectiveRating(SkillRating.Leadership), 10),
                    LightGreen
                ),
            }
        );

        if (officer.IsMain)
            DrawPattern(raster, 0, 10, _recruiterPattern, White);

        DrawBarStack(
            raster,
            new[] { 18, 20, 22 },
            new[]
            {
                SolidBar(ScaleRounded(officer.ShipResearch, 10), Blue),
                SolidBar(ScaleRounded(officer.TroopResearch, 10), Orange),
                SolidBar(ScaleRounded(officer.FacilityResearch, 10), ResearchYellow),
            }
        );

        if (officer.IsJediTrainer)
            DrawJediTrainerMarker(raster);

        DrawRankMarker(raster, 0, OfficerRank.Admiral, officer.AllowedRanks);
        DrawRankMarker(raster, 9, OfficerRank.General, officer.AllowedRanks);
        DrawRankMarker(raster, 18, OfficerRank.Commander, officer.AllowedRanks);
        return raster.Build();
    }

    /// <summary>
    /// Builds mission-capability letters for one special-forces unit.
    /// </summary>
    /// <param name="specialForces">The represented special-forces unit.</param>
    /// <returns>The completed capability overlay.</returns>
    private static StrategyUnitCardStatsOverlayData BuildSpecialForces(SpecialForces specialForces)
    {
        OverlayRaster raster = new OverlayRaster(_compactWidth, _height);
        IReadOnlyCollection<string> missionTypeIds =
            specialForces.AllowedMissionTypeIDs
            ?? (IReadOnlyCollection<string>)Array.Empty<string>();

        if (missionTypeIds.Contains(MissionTypeIDs.Espionage))
            DrawPattern(raster, 0, 0, _specialEspionagePattern, Purple);
        if (specialForces.GetEffectiveRating(SkillRating.Combat) >= 50)
            DrawPattern(raster, 0, 6, _specialDecoyPattern, Purple);
        if (missionTypeIds.Contains(MissionTypeIDs.Sabotage))
            DrawPattern(raster, 0, 12, _specialSabotagePattern, Purple);
        if (
            missionTypeIds.Contains(MissionTypeIDs.InciteUprising)
            || missionTypeIds.Contains(MissionTypeIDs.SubdueUprising)
        )
        {
            DrawPattern(raster, 0, 18, _specialUprisingPattern, LightGreen);
        }

        if (
            missionTypeIds.Contains(MissionTypeIDs.Abduction)
            || missionTypeIds.Contains(MissionTypeIDs.Rescue)
        )
        {
            DrawPattern(raster, 56, 0, _specialPrisonerPattern, Red);
        }
        if (missionTypeIds.Contains(MissionTypeIDs.Assassination))
            DrawPattern(raster, 56, 9, _specialAssassinationPattern, Red);
        if (missionTypeIds.Contains(MissionTypeIDs.Reconnaissance))
            DrawPattern(raster, 56, 18, _specialReconPattern, Gold);

        return raster.Build();
    }

    /// <summary>
    /// Builds combat, detection, uprising, and bombardment-defense bars for one regiment.
    /// </summary>
    /// <param name="regiment">The represented regiment.</param>
    /// <returns>The completed regiment overlay.</returns>
    private static StrategyUnitCardStatsOverlayData BuildRegiment(Regiment regiment)
    {
        OverlayRaster raster = new OverlayRaster(_compactWidth, _height);
        DrawBarStack(
            raster,
            new[] { 1, 3 },
            new[] { SolidBar(regiment.AttackRating, Red), SolidBar(regiment.DefenseRating, Cyan) }
        );
        DrawBarStack(
            raster,
            new[] { 8, 10 },
            new[]
            {
                SolidBar(ScaleRounded(regiment.DetectionRating, 5), Purple),
                SolidBar(regiment.UprisingDefense, LightGreen),
            }
        );
        DrawBarStack(raster, new[] { 21 }, new[] { SolidBar(regiment.BombardmentDefense, Gray) });
        return raster.Build();
    }

    /// <summary>
    /// Builds weapon, shield, detection, bombardment-defense, speed, and hyperdrive marks.
    /// </summary>
    /// <param name="starfighter">The represented fighter squadron.</param>
    /// <returns>The completed fighter overlay.</returns>
    private static StrategyUnitCardStatsOverlayData BuildStarfighter(Starfighter starfighter)
    {
        OverlayRaster raster = new OverlayRaster(_wideWidth, _height);
        DrawBarStack(
            raster,
            new[] { 1, 3, 5 },
            new[]
            {
                SolidBar(starfighter.LaserCannon, Gold),
                SolidBar(starfighter.IonCannon, Cyan),
                SolidBar(ScaleRounded(starfighter.Torpedoes, 2), Red),
            }
        );
        DrawBarStack(raster, new[] { 10 }, new[] { SolidBar(starfighter.ShieldStrength, Blue) });
        DrawBarStack(
            raster,
            new[] { 15, 17 },
            new[]
            {
                SolidBar(ScaleRounded(starfighter.DetectionRating, 3), Purple),
                SolidBar(starfighter.Bombardment, Gray),
            }
        );
        DrawBarStack(raster, new[] { 22 }, new[] { SolidBar(starfighter.SublightSpeed, White) });

        if (starfighter.Hyperdrive > 0)
        {
            DrawPattern(
                raster,
                61,
                20,
                new[] { "XXXXX", "X#X#X", "X###X", "X#X#X", "XXXXX" },
                White
            );
        }

        return raster.Build();
    }

    /// <summary>
    /// Builds weapon, durability, defense, speed, interdictor, and cargo marks.
    /// </summary>
    /// <param name="capitalShip">The represented capital ship.</param>
    /// <returns>The completed capital-ship overlay.</returns>
    private static StrategyUnitCardStatsOverlayData BuildCapitalShip(CapitalShip capitalShip)
    {
        OverlayRaster raster = new OverlayRaster(_wideWidth, _height);
        DrawBarStack(
            raster,
            new[] { 1, 3, 5 },
            new[]
            {
                EncodeCompactValue(
                    GetWeaponRating(capitalShip, PrimaryWeaponType.LaserCannon),
                    Gold
                ),
                EncodeCompactValue(GetWeaponRating(capitalShip, PrimaryWeaponType.IonCannon), Cyan),
                EncodeCompactValue(GetWeaponRating(capitalShip, PrimaryWeaponType.Turbolaser), Red),
            }
        );
        DrawBarStack(
            raster,
            new[] { 8, 10 },
            new[]
            {
                EncodeCompactValue(ScaleCeiling(capitalShip.MaxHullStrength, 400), HullBrown),
                EncodeCompactValue(ScaleCeiling(capitalShip.MaxShieldStrength, 200), Blue),
            }
        );
        DrawBarStack(
            raster,
            new[] { 13, 15, 17 },
            new[]
            {
                EncodeCompactValue(GetCapitalShipUprisingRating(capitalShip), DarkGreen),
                EncodeCompactValue(ScaleRounded(capitalShip.DetectionRating, 5), Purple),
                EncodeCompactValue(capitalShip.Bombardment, Gray),
            }
        );
        DrawBarStack(
            raster,
            new[] { 22 },
            new[] { EncodeCompactValue(capitalShip.SublightSpeed, White) }
        );

        if (capitalShip.HasGravityWell)
        {
            DrawPattern(
                raster,
                61,
                0,
                new[] { "XXXXX", "X###X", "XX#XX", "XX#XX", "XX#XX", "X###X", "XXXXX" },
                Red
            );
        }

        DrawNumber(raster, capitalShip.StarfighterCapacity, 12, Blue);
        DrawNumber(raster, capitalShip.RegimentCapacity, 18, Orange);
        return raster.Build();
    }

    /// <summary>
    /// Gets the compact directional weapon rating used by the card graph.
    /// </summary>
    /// <param name="capitalShip">The ship to inspect.</param>
    /// <param name="weaponType">The weapon family to inspect.</param>
    /// <returns>The four directional strengths expressed in hundred-point source units.</returns>
    private static int GetWeaponRating(CapitalShip capitalShip, PrimaryWeaponType weaponType)
    {
        if (
            capitalShip.PrimaryWeapons == null
            || !capitalShip.PrimaryWeapons.TryGetValue(weaponType, out int[] values)
            || values == null
            || values.Length == 0
        )
        {
            return 0;
        }

        int directionalTotal = values.Take(Math.Min(4, values.Length)).Sum();
        return ScaleCeiling(directionalTotal, 100);
    }

    /// <summary>
    /// Derives the compact uprising-defense rating from the unit-data cost relationship.
    /// </summary>
    /// <param name="capitalShip">The ship to inspect.</param>
    /// <returns>The compact uprising-defense rating.</returns>
    private static int GetCapitalShipUprisingRating(CapitalShip capitalShip)
    {
        if (capitalShip.ConstructionCost <= 0)
            return 0;

        return (int)Math.Floor(capitalShip.ConstructionCost * 0.045d);
    }

    /// <summary>
    /// Draws the vertical Jedi-trainer blade and hilt marker.
    /// </summary>
    /// <param name="raster">The destination overlay.</param>
    private static void DrawJediTrainerMarker(OverlayRaster raster)
    {
        for (int y = 1; y <= 17; y++)
        {
            raster.Set(52, y, JediGreen);
            raster.Set(53, y, JediGreen);
        }

        raster.Set(52, 18, JediGreen);
        raster.Set(53, 18, Black);
        raster.Set(52, 19, Black);
        raster.Set(53, 19, Black);
        raster.Set(52, 20, HiltRed);
        raster.Set(53, 20, Black);
        for (int y = 21; y <= 23; y++)
        {
            raster.Set(52, y, Black);
            raster.Set(53, y, Black);
        }
    }

    /// <summary>
    /// Draws one officer-rank glyph with its allowed or unavailable palette.
    /// </summary>
    /// <param name="raster">The destination overlay.</param>
    /// <param name="y">The marker's top coordinate.</param>
    /// <param name="rank">The represented rank.</param>
    /// <param name="allowedRanks">The officer's allowed ranks.</param>
    private static void DrawRankMarker(
        OverlayRaster raster,
        int y,
        OfficerRank rank,
        IReadOnlyCollection<OfficerRank> allowedRanks
    )
    {
        string[] pattern = rank switch
        {
            OfficerRank.Admiral => _rankAdmiralPattern,
            OfficerRank.General => _rankGeneralPattern,
            OfficerRank.Commander => _rankCommanderPattern,
            _ => throw new ArgumentOutOfRangeException(nameof(rank), rank, null),
        };
        Color32 color = allowedRanks?.Contains(rank) == true ? Gold : Gray;
        DrawPattern(raster, 55, y, pattern, color);
    }

    /// <summary>
    /// Draws one positive cargo capacity as a right-aligned boxed number.
    /// </summary>
    /// <param name="raster">The destination overlay.</param>
    /// <param name="value">The positive capacity to draw.</param>
    /// <param name="top">The top of the seven-pixel number box.</param>
    /// <param name="color">The digit color.</param>
    private static void DrawNumber(OverlayRaster raster, int value, int top, Color32 color)
    {
        if (value <= 0)
            return;

        string text = value.ToString();
        int boxWidth = text.Length * 4 + 1;
        int left = raster.Width - boxWidth;
        for (int x = left; x < raster.Width; x++)
        {
            raster.Set(x, top, Black);
            raster.Set(x, top + 6, Black);
        }

        for (int y = top + 1; y < top + 6; y++)
        {
            raster.Set(left, y, Black);
            raster.Set(raster.Width - 1, y, Black);
        }

        for (int digitIndex = 0; digitIndex < text.Length; digitIndex++)
        {
            int digitLeft = left + 1 + digitIndex * 4;
            if (digitIndex > 0)
            {
                for (int y = top + 1; y < top + 6; y++)
                    raster.Set(digitLeft - 1, y, Black);
            }

            string[] pattern = _digitPatterns[text[digitIndex]];
            for (int y = 0; y < pattern.Length; y++)
            {
                for (int x = 0; x < pattern[y].Length; x++)
                {
                    raster.Set(digitLeft + x, top + 1 + y, pattern[y][x] == '#' ? color : Black);
                }
            }
        }
    }

    /// <summary>
    /// Draws a fixed pixel glyph whose X cells are black and hash cells use the supplied color.
    /// </summary>
    /// <param name="raster">The destination overlay.</param>
    /// <param name="left">The glyph's left coordinate.</param>
    /// <param name="top">The glyph's top coordinate.</param>
    /// <param name="pattern">The glyph rows.</param>
    /// <param name="color">The hash-cell color.</param>
    private static void DrawPattern(
        OverlayRaster raster,
        int left,
        int top,
        IReadOnlyList<string> pattern,
        Color32 color
    )
    {
        for (int y = 0; y < pattern.Count; y++)
        {
            string row = pattern[y];
            for (int x = 0; x < row.Length; x++)
            {
                if (row[x] == '#')
                    raster.Set(left + x, top + y, color);
                else if (row[x] == 'X')
                    raster.Set(left + x, top + y, Black);
            }
        }
    }

    /// <summary>
    /// Draws connected two-pixel-high bars with a shared one-pixel black contour.
    /// </summary>
    /// <param name="raster">The destination overlay.</param>
    /// <param name="starts">The top source row of each colored bar.</param>
    /// <param name="rows">The encoded pixels for each bar.</param>
    private static void DrawBarStack(
        OverlayRaster raster,
        IReadOnlyList<int> starts,
        IReadOnlyList<Color32[]> rows
    )
    {
        if (starts.Count != rows.Count)
            throw new ArgumentException("Bar starts and rows must have matching counts.");

        for (int index = 0; index < rows.Count; index++)
        {
            int start = starts[index];
            int width = rows[index].Length;
            for (int x = 0; x <= width; x++)
            {
                raster.Set(x, start - 1, Black);
                raster.Set(x, start + 2, Black);
            }

            for (int y = start; y <= start + 1; y++)
            {
                raster.Set(0, y, Black);
                raster.Set(width, y, Black);
            }
        }

        for (int index = 0; index < rows.Count; index++)
        {
            int start = starts[index];
            for (int x = 0; x < rows[index].Length; x++)
            {
                raster.Set(x, start, rows[index][x]);
                raster.Set(x, start + 1, rows[index][x]);
            }
        }
    }

    /// <summary>
    /// Builds one uninterrupted colored bar of the requested source-pixel length.
    /// </summary>
    /// <param name="length">The nonnegative source-pixel length.</param>
    /// <param name="color">The bar color.</param>
    /// <returns>The solid horizontal pixels.</returns>
    private static Color32[] SolidBar(int length, Color32 color)
    {
        Color32[] pixels = new Color32[Math.Max(0, length)];
        for (int index = 0; index < pixels.Length; index++)
            pixels[index] = color;
        return pixels;
    }

    /// <summary>
    /// Encodes a compact capital-ship value into colored tens and units separated by black.
    /// </summary>
    /// <param name="value">The nonnegative rating to encode.</param>
    /// <param name="color">The rating color.</param>
    /// <returns>The encoded horizontal pixels.</returns>
    private static Color32[] EncodeCompactValue(int value, Color32 color)
    {
        value = Math.Max(0, value);
        if (value < 10)
        {
            Color32[] direct = new Color32[value];
            for (int index = 0; index < direct.Length; index++)
                direct[index] = color;
            return direct;
        }

        int tens = value / 10;
        int units = value % 10;
        Color32[] encoded = new Color32[tens + (units > 0 ? units + 1 : 0)];
        for (int index = 0; index < tens; index++)
            encoded[index] = color;
        if (units <= 0)
            return encoded;

        encoded[tens] = Black;
        for (int index = tens + 1; index < encoded.Length; index++)
            encoded[index] = color;
        return encoded;
    }

    /// <summary>
    /// Scales a value by a positive divisor using midpoint rounding away from zero.
    /// </summary>
    /// <param name="value">The stored rating.</param>
    /// <param name="divisor">The display-unit divisor.</param>
    /// <returns>The nonnegative scaled rating.</returns>
    private static int ScaleRounded(int value, int divisor)
    {
        return value <= 0
            ? 0
            : (int)Math.Round(value / (double)divisor, MidpointRounding.AwayFromZero);
    }

    /// <summary>
    /// Scales a value by a positive divisor while retaining any nonzero remainder.
    /// </summary>
    /// <param name="value">The stored rating.</param>
    /// <param name="divisor">The display-unit divisor.</param>
    /// <returns>The nonnegative scaled rating.</returns>
    private static int ScaleCeiling(int value, int divisor)
    {
        return value <= 0 ? 0 : (int)Math.Ceiling(value / (double)divisor);
    }

    /// <summary>
    /// Owns one mutable top-left-origin overlay while it is being projected.
    /// </summary>
    private sealed class OverlayRaster
    {
        private readonly Color32[] pixels;

        internal int Width { get; }

        private int Height { get; }

        /// <summary>
        /// Creates one transparent overlay raster.
        /// </summary>
        /// <param name="width">The source-space width.</param>
        /// <param name="height">The source-space height.</param>
        internal OverlayRaster(int width, int height)
        {
            Width = width;
            Height = height;
            pixels = new Color32[width * height];
        }

        /// <summary>
        /// Writes one pixel when it lies inside the overlay bounds.
        /// </summary>
        /// <param name="x">The horizontal coordinate.</param>
        /// <param name="y">The vertical coordinate.</param>
        /// <param name="color">The pixel color.</param>
        internal void Set(int x, int y, Color32 color)
        {
            if (x < 0 || x >= Width || y < 0 || y >= Height)
                return;

            pixels[y * Width + x] = color;
        }

        /// <summary>
        /// Freezes the raster while preserving the transparent lower-left source pixel.
        /// </summary>
        /// <returns>The immutable overlay data.</returns>
        internal StrategyUnitCardStatsOverlayData Build()
        {
            pixels[(Height - 1) * Width] = default;
            return new StrategyUnitCardStatsOverlayData(Width, Height, pixels);
        }
    }
}
