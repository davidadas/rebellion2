using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

internal enum ContentEditorArtworkEffect
{
    Travel,
    Damage,
}

/// <summary>
/// Identifies a travel pattern and the display art it was authored against.
/// </summary>
internal readonly struct ContentEditorTravelPattern
{
    internal string Image { get; }
    internal string Subject { get; }

    /// <summary>
    /// Pairs a travel image with the artwork it was designed to fit.
    /// </summary>
    /// <param name="image">The resolved travel image.</param>
    /// <param name="subject">The corresponding unit artwork when the pattern includes one.</param>
    internal ContentEditorTravelPattern(string image, string subject)
    {
        Image = image;
        Subject = subject;
    }
}

internal enum ContentEditorArtworkEffectStyle
{
    ShipTravel,
    PersonnelTravel,
    RegimentTravel,
    FacilityTravel,
    Damage,
}

/// <summary>
/// Draws original gameplay effects against the silhouette and placement of the selected unit.
/// </summary>
internal static class ContentEditorArtworkEffectGenerator
{
    private const float _shortWakeReach = 0.84f;
    private const float _longWakeReach = 1.4f;
    private static readonly Color _previewBackground = new(0.035f, 0.04f, 0.05f);
    private static readonly Color _wakeShadow = new(0.035f, 0.14f, 0.22f, 0.82f);
    private static readonly Color _wakeEdge = new(0.015f, 0.43f, 0.51f, 0.94f);
    private static readonly Color _wakeCore = new(0.045f, 0.93f, 0.96f);
    private static readonly Color _fireShadow = new(0.43f, 0.055f, 0.025f, 0.91f);
    private static readonly Color _fireRed = new(0.76f, 0.13f, 0.035f);
    private static readonly Color _fireOrange = new(0.96f, 0.34f, 0.045f);
    private static readonly Color _fireGold = new(1f, 0.67f, 0.095f);
    private static readonly Color _fireCore = new(1f, 0.87f, 0.23f);

    // Streak length and height deciles in source pixels for the standard card sizes.
    private static readonly int[] _compactPersonnelStreakWidths =
    {
        4,
        4,
        5,
        7,
        12,
        17,
        43,
        50,
        50,
        50,
        50,
    };
    private static readonly int[] _largePersonnelStreakWidths =
    {
        7,
        12,
        19,
        30,
        38,
        48,
        50,
        50,
        50,
        50,
        50,
    };
    private static readonly int[] _compactPersonnelStreakHeights =
    {
        4,
        4,
        4,
        4,
        4,
        5,
        5,
        5,
        5,
        9,
        9,
    };
    private static readonly int[] _largePersonnelStreakHeights =
    {
        4,
        4,
        4,
        4,
        4,
        5,
        5,
        5,
        5,
        5,
        9,
    };
    private static readonly int[] _facilityStreakWidths = { 2, 2, 3, 5, 7, 10, 14, 19, 25, 35, 50 };
    private static readonly int[] _facilityStreakHeights = { 2, 3, 3, 3, 3, 3, 4, 4, 5, 5, 5 };
    private static readonly int[] _regimentStreakWidths =
    {
        21,
        24,
        28,
        33,
        38,
        42,
        43,
        44,
        46,
        52,
        62,
    };
    private static readonly int[] _regimentStreakHeights = { 2, 2, 3, 3, 4, 4, 4, 4, 5, 5, 5 };

    /// <summary>
    /// Selects the export format that consumes the gameplay effect.
    /// </summary>
    /// <param name="record">The unit whose artwork is edited.</param>
    /// <param name="effect">The chosen gameplay state.</param>
    /// <param name="style">The format used by the runtime card.</param>
    /// <returns>Whether the unit has an editable effect of that kind.</returns>
    internal static bool TryGetStyle(
        ContentPackEditorRecord record,
        ContentEditorArtworkEffect effect,
        out ContentEditorArtworkEffectStyle style
    )
    {
        ContentEditorArtworkEffectStyle? match = (record.Kind, effect) switch
        {
            ("CapitalShip" or "Starfighter", ContentEditorArtworkEffect.Damage) =>
                ContentEditorArtworkEffectStyle.Damage,
            ("CapitalShip" or "Starfighter", ContentEditorArtworkEffect.Travel) =>
                ContentEditorArtworkEffectStyle.ShipTravel,
            ("SpecialForce", ContentEditorArtworkEffect.Travel) =>
                ContentEditorArtworkEffectStyle.PersonnelTravel,
            ("Regiment", ContentEditorArtworkEffect.Travel) =>
                ContentEditorArtworkEffectStyle.RegimentTravel,
            ("Building", ContentEditorArtworkEffect.Travel)
                when !ContentEditorUnitArtworkLayout.IsHeadquartersIcon(record) =>
                ContentEditorArtworkEffectStyle.FacilityTravel,
            _ => null,
        };
        style = match.GetValueOrDefault();
        return match.HasValue;
    }

    /// <summary>
    /// Returns a repeatable seed for the unit and the chosen effect.
    /// </summary>
    /// <param name="unit">The unit's serialized identifier.</param>
    /// <param name="effect">The selected gameplay state.</param>
    /// <returns>The initial variation seed.</returns>
    internal static int GetSeed(string unit, ContentEditorArtworkEffect effect)
    {
        unchecked
        {
            uint hash = 2166136261u;
            foreach (char character in unit)
                hash = (hash ^ character) * 16777619u;
            return (int)((hash ^ (uint)effect) * 16777619u);
        }
    }

    /// <summary>
    /// Decodes one source and generates an authored asset with an optional composed preview.
    /// </summary>
    /// <param name="sourcePath">The resolved normal unit artwork.</param>
    /// <param name="size">The output canvas, or null to match the source exactly.</param>
    /// <param name="style">How the runtime consumes this effect.</param>
    /// <param name="seed">The repeatable variation seed.</param>
    /// <param name="includePreview">Whether to render a composed preview for the editor.</param>
    /// <param name="stretchSource">Whether the runtime stretches both layers into the same frame.</param>
    /// <param name="placements">The primary layer offset followed by additional effect positions, in canvas proportions.</param>
    /// <param name="previewBackground">An optional readable gameplay background used for the composed preview.</param>
    /// <param name="existingEffectPath">An authored ship state to place as the initial effect layer, if present.</param>
    /// <returns>The encoded state image and optional caller-owned preview texture.</returns>
    internal static (byte[] Png, Texture2D Preview) Generate(
        string sourcePath,
        Vector2Int? size,
        ContentEditorArtworkEffectStyle style,
        int seed,
        bool includePreview,
        bool stretchSource = false,
        IReadOnlyList<Vector2> placements = null,
        Texture2D previewBackground = null,
        string existingEffectPath = null
    )
    {
        Texture2D source = new(2, 2, TextureFormat.RGBA32, false);
        Texture2D output = null;
        try
        {
            if (!ImageConversion.LoadImage(source, File.ReadAllBytes(sourcePath)))
                throw new InvalidDataException("The selected unit image could not be decoded.");

            Vector2Int target = size ?? new Vector2Int(source.width, source.height);
            Color[] subject = FitToCanvas(source, target, stretchSource);
            RectInt bounds = FindSubject(subject, target);
            System.Random random = new(seed);
            Color[] effect =
                ReadExistingEffect(existingEffectPath, target)
                ?? (
                    style switch
                    {
                        ContentEditorArtworkEffectStyle.ShipTravel => DrawShipTravel(
                            subject,
                            target,
                            bounds,
                            random
                        ),
                        ContentEditorArtworkEffectStyle.Damage => DrawDamage(
                            subject,
                            target,
                            bounds,
                            random
                        ),
                        ContentEditorArtworkEffectStyle.RegimentTravel => DrawStarfield(
                            target,
                            random,
                            transparent: true
                        ),
                        _ => DrawStarfield(target, random),
                    }
                );
            if (placements is { Count: > 0 })
            {
                if (style == ContentEditorArtworkEffectStyle.FacilityTravel)
                    subject = ShiftLayer(subject, target, placements[0]);
                else
                    effect = ShiftLayer(effect, target, placements[0]);
                for (int index = 1; index < placements.Count; index++)
                    DrawAdditionalEffect(effect, target, style, placements[index], random);
            }
            Color[] gameplayBackground =
                includePreview && previewBackground != null
                    ? FitPreviewBackground(previewBackground, target)
                    : null;
            Color[] preview = includePreview ? new Color[subject.Length] : null;
            for (int index = 0; index < effect.Length; index++)
            {
                Color entity = subject[index];
                switch (style)
                {
                    case ContentEditorArtworkEffectStyle.FacilityTravel:
                        effect[index] = Over(effect[index], entity);
                        if (includePreview)
                            preview[index] = effect[index];
                        break;
                    case ContentEditorArtworkEffectStyle.PersonnelTravel:
                        Color field = effect[index];
                        field.a *= 1f - entity.a;
                        effect[index] = field;
                        if (includePreview)
                            preview[index] = Over(Over(_previewBackground, field), entity);
                        break;
                    default:
                        if (style == ContentEditorArtworkEffectStyle.RegimentTravel)
                            effect[index].a *= 1f - entity.a;
                        if (includePreview)
                            preview[index] = Over(
                                Over(gameplayBackground?[index] ?? _previewBackground, entity),
                                effect[index]
                            );
                        break;
                }
            }
            output = CreateTexture(target, effect);
            byte[] png = ImageConversion.EncodeToPNG(output);
            return (png, includePreview ? CreateTexture(target, preview) : null);
        }
        finally
        {
            Release(source);
            Release(output);
        }
    }

    /// <summary>
    /// Builds the distinct layers that the author can arrange before exporting a state image.
    /// </summary>
    /// <param name="sourcePath">The clean display artwork used to position each effect.</param>
    /// <param name="size">The output dimensions, or null to use the source dimensions.</param>
    /// <param name="style">How the game consumes the generated state.</param>
    /// <param name="seed">The variation seed.</param>
    /// <param name="stretchSource">Whether the runtime stretches the art into the destination.</param>
    /// <param name="backdrop">The readable backdrop supplied by the game theme, if configured.</param>
    /// <param name="existingPath">The current image to open as layers, when present.</param>
    /// <param name="variant">The selected wake or streak composition.</param>
    /// <param name="legacyStreaks">Whether a saved draft uses the previous streak geometry.</param>
    /// <param name="facilityClearance">The stored thickness of the facility's silhouette margin, or null to sample it.</param>
    /// <param name="facilityReferencePath">The registered facility card used to sample its silhouette margin.</param>
    /// <param name="completeTravel">Whether the draft uses one complete gameplay travel pattern.</param>
    /// <param name="preserveRegisteredTravel">Whether matching travel cards retain their authored pixels.</param>
    /// <param name="varyShipWakes">Whether to vary the reach of each authored ship wake.</param>
    /// <param name="travelPattern">The matching authored travel and display references, if available.</param>
    /// <returns>The caller-owned frame with separable artwork.</returns>
    internal static EditableFrame CreateEditableFrame(
        string sourcePath,
        Vector2Int? size,
        ContentEditorArtworkEffectStyle style,
        int seed,
        bool stretchSource,
        Texture2D backdrop,
        string existingPath = null,
        int variant = 0,
        bool legacyStreaks = false,
        int? facilityClearance = null,
        string facilityReferencePath = null,
        bool completeTravel = false,
        ContentEditorTravelPattern travelPattern = default,
        bool preserveRegisteredTravel = false,
        bool varyShipWakes = false
    )
    {
        Texture2D source = new(2, 2, TextureFormat.RGBA32, false);
        try
        {
            bool hasSubject = !string.IsNullOrEmpty(sourcePath);
            if (hasSubject && !ImageConversion.LoadImage(source, File.ReadAllBytes(sourcePath)))
                throw new InvalidDataException("The selected unit image could not be decoded.");
            Vector2Int target = size ?? new Vector2Int(source.width, source.height);
            Color[] subject = hasSubject
                ? FitToCanvas(source, target, stretchSource)
                : new Color[target.x * target.y];
            bool facility = style == ContentEditorArtworkEffectStyle.FacilityTravel;
            bool registeredFacility =
                facility && IsRegisteredTravelSubject(travelPattern.Subject, sourcePath);
            Color[] facilityPatternSubject = facility
                ? ResolveFacilityTravelSubject(
                    travelPattern,
                    target,
                    registeredFacility ? subject : null
                )
                : null;
            if (registeredFacility && facilityPatternSubject != null)
                subject = facilityPatternSubject;
            RectInt bounds = FindSubject(subject, target);
            System.Random random = new(seed);
            bool personnel = style == ContentEditorArtworkEffectStyle.PersonnelTravel;
            bool regiment = style == ContentEditorArtworkEffectStyle.RegimentTravel;
            Color[] imported = ReadExistingEffect(existingPath, target);
            bool registeredPersonnel =
                personnel && IsRegisteredTravelSubject(travelPattern.Subject, sourcePath);
            Color[] registeredCard =
                imported == null
                && preserveRegisteredTravel
                && variant == 0
                && (registeredFacility || registeredPersonnel)
                    ? ReadExistingEffect(travelPattern.Image, target)
                    : null;
            int clearance = 0;
            int[] silhouetteDistance = null;
            if (facility && facilityClearance != 0)
            {
                int referenceRadius = Mathf.Max(1, Mathf.RoundToInt(target.x * 10f / 302));
                silhouetteDistance = GetSilhouetteDistance(
                    subject,
                    target,
                    Mathf.Max(referenceRadius, facilityClearance ?? 0)
                );
                if (facilityClearance.HasValue)
                    clearance = facilityClearance.Value;
                else
                {
                    Color[] reference =
                        facilityReferencePath == existingPath
                            ? imported
                            : ReadExistingEffect(facilityReferencePath, target);
                    int[] referenceDistance = silhouetteDistance;
                    if (facilityPatternSubject != null)
                    {
                        reference = ReadTravelFieldReference(travelPattern.Image, target);
                        if (!ReferenceEquals(facilityPatternSubject, subject))
                            referenceDistance = GetSilhouetteDistance(
                                facilityPatternSubject,
                                target,
                                referenceRadius
                            );
                    }
                    clearance = GetFacilityClearance(
                        referenceDistance,
                        target,
                        reference,
                        referenceRadius
                    );
                }
            }
            Color[] background = backdrop != null ? FitPreviewBackground(backdrop, target) : null;
            Color[] exportBackground = null;
            List<Color[]> pieces = new();
            if (facility || personnel || (regiment && completeTravel && imported == null))
            {
                exportBackground = new Color[subject.Length];
                Array.Fill(exportBackground, Color.black);
                background = exportBackground;
            }
            if (registeredCard != null)
            {
                if (facility)
                    SplitRegisteredFacilityCard(
                        registeredCard,
                        subject,
                        silhouetteDistance,
                        clearance,
                        exportBackground,
                        pieces
                    );
                else
                    SplitTransitCard(
                        registeredCard,
                        subject,
                        target,
                        false,
                        exportBackground,
                        pieces,
                        bandCount: 1
                    );
            }
            else if (imported != null)
            {
                if (facility || personnel)
                {
                    SplitTransitCard(imported, subject, target, facility, exportBackground, pieces);
                    if (facility && clearance > 0)
                        AddFacilityClearance(pieces[^1], silhouetteDistance, clearance, imported);
                }
                else
                    pieces.AddRange(SplitOverlay(imported, target));
            }
            else
            {
                switch (style)
                {
                    case ContentEditorArtworkEffectStyle.ShipTravel:
                        Color[] wake = completeTravel
                            ? FitTravelWake(
                                travelPattern,
                                sourcePath,
                                subject,
                                bounds,
                                target,
                                variant
                            )
                            : null;
                        if (wake != null)
                        {
                            IReadOnlyList<Color[]> layers = SplitOverlay(wake, target);
                            if (varyShipWakes)
                            {
                                Vector2 axis = GetShipAxis(subject, target, bounds);
                                foreach (Color[] layer in layers)
                                    pieces.Add(VaryWakeReach(layer, subject, target, axis, random));
                            }
                            else
                                pieces.AddRange(layers);
                        }
                        else
                            DrawShipTravel(subject, target, bounds, random, pieces, variant);
                        break;
                    case ContentEditorArtworkEffectStyle.Damage:
                        DrawDamage(subject, target, bounds, random, pieces);
                        break;
                    default:
                        if (completeTravel)
                        {
                            Color[] field = CreateTravelField(
                                travelPattern,
                                sourcePath,
                                target,
                                style,
                                variant,
                                random,
                                facilityPatternSubject,
                                out int longestStreak
                            );
                            FitVisibleTravelStreaks(
                                field,
                                subject,
                                target,
                                facility ? silhouetteDistance : null,
                                clearance,
                                longestStreak
                            );
                            pieces.Add(field);
                        }
                        else
                        {
                            for (int band = 0; band < 3; band++)
                                pieces.Add(
                                    legacyStreaks
                                        ? DrawLegacyStreaks(target, random, variant, band, 3)
                                        : DrawStreaks(target, style, random, variant, band, 3)
                                );
                        }
                        if (facility)
                        {
                            Color[] foreground = (Color[])subject.Clone();
                            if (clearance > 0)
                                AddFacilityClearance(foreground, silhouetteDistance, clearance);
                            pieces.Add(foreground);
                        }
                        break;
                }
            }
            Color[] preview = new Color[subject.Length];
            for (int index = 0; index < preview.Length; index++)
            {
                Color backing = background?[index] ?? _previewBackground;
                preview[index] =
                    facility || personnel || regiment
                        ? Over(Color.black, backing)
                        : Over(backing, subject[index]);
            }
            return new EditableFrame(
                target,
                style,
                subject,
                exportBackground,
                CreateTexture(target, preview),
                personnel || regiment ? CreateTexture(target, subject) : null,
                pieces,
                imported ?? registeredCard,
                variant,
                legacyStreaks,
                clearance
            );
        }
        finally
        {
            Release(source);
        }
    }

    /// <summary>
    /// Owns the source textures and export rules for one artwork output size.
    /// </summary>
    internal sealed class EditableFrame : IDisposable
    {
        private readonly ContentEditorArtworkEffectStyle style;
        private readonly Color[] subject;
        private readonly Color[] exportBackground;
        private readonly Color[] imported;
        private readonly int variant;
        private readonly bool legacyStreaks;
        private bool[] streakObstacles;
        private readonly List<(Texture2D Texture, RectInt Bounds)> defaults = new();
        private readonly Dictionary<int, (Texture2D Texture, RectInt Bounds)> additions = new();

        internal Vector2Int Size { get; }
        internal Texture2D BaseImage { get; }
        internal Texture2D SubjectOverlay { get; }
        internal int FacilityClearance { get; }
        internal int DefaultLayerCount => defaults.Count;
        internal int FacilityLayerIndex =>
            style == ContentEditorArtworkEffectStyle.FacilityTravel ? defaults.Count - 1 : -1;

        /// <summary>
        /// Captures the rendered layers and their original positions.
        /// </summary>
        /// <param name="size">The complete canvas dimensions.</param>
        /// <param name="style">The runtime consumption rules for the exported image.</param>
        /// <param name="subject">The source unit pixels.</param>
        /// <param name="exportBackground">An opaque backing when the runtime expects one.</param>
        /// <param name="baseImage">The fixed preview surface.</param>
        /// <param name="subjectOverlay">The fixed subject drawn above backdrop effects.</param>
        /// <param name="pieces">The independently editable effects.</param>
        /// <param name="imported">The starting artwork, when editing an existing reference.</param>
        /// <param name="variant">The selected motion composition.</param>
        /// <param name="legacyStreaks">Whether to re-create the geometry of an earlier draft.</param>
        /// <param name="facilityClearance">The facility silhouette margin in source pixels.</param>
        internal EditableFrame(
            Vector2Int size,
            ContentEditorArtworkEffectStyle style,
            Color[] subject,
            Color[] exportBackground,
            Texture2D baseImage,
            Texture2D subjectOverlay,
            IReadOnlyList<Color[]> pieces,
            Color[] imported,
            int variant,
            bool legacyStreaks,
            int facilityClearance
        )
        {
            Size = size;
            this.style = style;
            this.subject = subject;
            this.exportBackground = exportBackground;
            this.imported = imported;
            this.variant = variant;
            this.legacyStreaks = legacyStreaks;
            FacilityClearance = facilityClearance;
            BaseImage = baseImage;
            SubjectOverlay = subjectOverlay;
            foreach (Color[] piece in pieces)
                defaults.Add(CropLayer(piece, size));
        }

        /// <summary>
        /// Resolves one source layer. Extra ship effects are created once per frame and seed.
        /// </summary>
        /// <param name="layer">The editable layer entry.</param>
        /// <returns>The cropped texture and original canvas bounds.</returns>
        internal (Texture2D Texture, RectInt Bounds) GetLayer(ContentEditorArtworkLayer layer)
        {
            if (layer.Source >= 0)
                return layer.Source < defaults.Count ? defaults[layer.Source] : default;
            if (additions.TryGetValue(layer.Seed, out var result))
                return result;
            Color[] pixels = new Color[Size.x * Size.y];
            System.Random random = new(layer.Seed);
            if (style == ContentEditorArtworkEffectStyle.ShipTravel)
                DrawExtraWake(pixels, random);
            else if (style == ContentEditorArtworkEffectStyle.Damage)
                DrawAdditionalEffect(pixels, Size, style, new Vector2(0.5f, 0.5f), random);
            else if (layer.VisibleStreakGroup)
                pixels = DrawStreaks(
                    Size,
                    style,
                    random,
                    variant,
                    0,
                    1,
                    extra: true,
                    blocked: streakObstacles ??= BuildStreakObstacles()
                );
            else
            {
                int band = random.Next(4);
                pixels =
                    legacyStreaks && !layer.MeasuredStreaks
                        ? DrawLegacyStreaks(Size, random, variant, band, 4, extra: true)
                        : DrawStreaks(Size, style, random, variant, band, 4, extra: true);
            }
            result = CropLayer(pixels, Size);
            additions.Add(layer.Seed, result);
            return result;
        }

        /// <summary>
        /// Finds the spaces where newly added streaks can be seen around the installed artwork.
        /// </summary>
        /// <returns>The blocked pixels in the default layout, including the subject and existing strokes.</returns>
        private bool[] BuildStreakObstacles()
        {
            bool[] blocked = new bool[subject.Length];
            for (int i = 0; i < blocked.Length; i++)
                blocked[i] =
                    subject[i].a > 0.1f
                    || (
                        style == ContentEditorArtworkEffectStyle.PersonnelTravel
                        && imported != null
                        && imported[i].a < 0.9f
                    );

            foreach (var piece in defaults)
            {
                if (piece.Texture == null)
                    continue;
                Color32[] pixels = piece.Texture.GetPixels32();
                for (int y = 0; y < piece.Bounds.height; y++)
                for (int x = 0; x < piece.Bounds.width; x++)
                    if (pixels[y * piece.Bounds.width + x].a > 0)
                        blocked[(piece.Bounds.yMin + y) * Size.x + piece.Bounds.xMin + x] = true;
            }
            return blocked;
        }

        /// <summary>
        /// Places a standalone wake beside the ship's generated exhaust, following the hull's angle.
        /// </summary>
        /// <param name="pixels">The new layer's transparent canvas.</param>
        /// <param name="random">The repeatable source of exhaust dimensions.</param>
        private void DrawExtraWake(Color[] pixels, System.Random random)
        {
            RectInt hull = FindSubject(subject, Size);
            Vector2 direction = GetShipAxis(subject, Size, hull);
            if (Mathf.Abs(direction.y) <= 0.26f)
                direction = Vector2.right;
            Vector2 across = new(-direction.y, direction.x);
            RectInt wake = defaults.Count > 0 ? defaults[0].Bounds : default;
            Vector2 center =
                wake.width > 0 ? wake.center : GetAftCenter(subject, Size, hull, direction);
            float spacing = Size.y * 0.19f;
            center += across * (center.y > Size.y * 0.5f ? -spacing : spacing);
            float length = Size.y * RandomBetween(random, 0.32f, 0.39f);
            float radius = Size.y * 0.075f;
            Vector2 margin = new(
                Mathf.Abs(direction.x) * length * 0.5f + Mathf.Abs(across.x) * radius,
                Mathf.Abs(direction.y) * length * 0.5f + Mathf.Abs(across.y) * radius
            );
            center.x = Mathf.Clamp(center.x, margin.x, Size.x - margin.x);
            center.y = Mathf.Clamp(center.y, margin.y, Size.y - margin.y);
            DrawAngledWake(
                pixels,
                Size,
                null,
                center - direction * (length * 0.5f),
                direction,
                length,
                radius
            );
        }

        /// <summary>
        /// Flattens the current draft in the format consumed by the associated unit state.
        /// </summary>
        /// <param name="layers">The ordered draft layers and positions.</param>
        /// <returns>The complete PNG.</returns>
        internal byte[] Bake(IReadOnlyList<ContentEditorArtworkLayer> layers)
        {
            Color[] result =
                exportBackground == null
                    ? new Color[Size.x * Size.y]
                    : (Color[])exportBackground.Clone();
            foreach (ContentEditorArtworkLayer layer in layers)
            {
                if (layer.Hidden)
                    continue;
                var piece = GetLayer(layer);
                if (piece.Texture == null)
                    continue;
                int x0 = piece.Bounds.x + Mathf.RoundToInt(layer.Offset.x * Size.x);
                int y0 = piece.Bounds.y + Mathf.RoundToInt(layer.Offset.y * Size.y);
                Color[] pixels = piece.Texture.GetPixels();
                for (int y = Mathf.Max(0, y0); y < Mathf.Min(Size.y, y0 + piece.Bounds.height); y++)
                for (int x = Mathf.Max(0, x0); x < Mathf.Min(Size.x, x0 + piece.Bounds.width); x++)
                {
                    int index = y * Size.x + x;
                    result[index] = Over(
                        result[index],
                        pixels[(y - y0) * piece.Bounds.width + x - x0]
                    );
                }
            }
            if (
                imported == null
                && style
                    is (
                        ContentEditorArtworkEffectStyle.PersonnelTravel
                        or ContentEditorArtworkEffectStyle.RegimentTravel
                    )
            )
            {
                for (int index = 0; index < result.Length; index++)
                    result[index].a *= 1f - subject[index].a;
            }
            Texture2D output = CreateTexture(Size, result);
            try
            {
                return ImageConversion.EncodeToPNG(output);
            }
            finally
            {
                Release(output);
            }
        }

        /// <summary>
        /// Releases all textures owned by the frame.
        /// </summary>
        public void Dispose()
        {
            Release(BaseImage);
            Release(SubjectOverlay);
            foreach (var piece in defaults)
                Release(piece.Texture);
            foreach (var piece in additions.Values)
                Release(piece.Texture);
        }
    }

    /// <summary>
    /// Trims transparent padding so moving and selecting a layer track the visible artwork.
    /// </summary>
    /// <param name="pixels">The full-frame effect pixels.</param>
    /// <param name="size">The size of the input canvas.</param>
    /// <returns>The cropped texture and source bounds.</returns>
    private static (Texture2D Texture, RectInt Bounds) CropLayer(Color[] pixels, Vector2Int size)
    {
        int minX = size.x,
            minY = size.y,
            maxX = -1,
            maxY = -1;
        for (int y = 0; y < size.y; y++)
        for (int x = 0; x < size.x; x++)
        {
            if (pixels[y * size.x + x].a < 1f / 255f)
                continue;
            minX = Mathf.Min(minX, x);
            minY = Mathf.Min(minY, y);
            maxX = Mathf.Max(maxX, x);
            maxY = Mathf.Max(maxY, y);
        }
        if (maxX < minX)
            return default;
        RectInt bounds = new(minX, minY, maxX - minX + 1, maxY - minY + 1);
        Color[] cropped = new Color[bounds.width * bounds.height];
        for (int row = 0; row < bounds.height; row++)
            Array.Copy(
                pixels,
                (minY + row) * size.x + minX,
                cropped,
                row * bounds.width,
                bounds.width
            );
        return (CreateTexture(bounds.size, cropped), bounds);
    }

    /// <summary>
    /// Separates an alpha overlay into editable regions and assigns detached sparks to their nearest effect.
    /// </summary>
    /// <param name="source">The decoded overlay.</param>
    /// <param name="size">The frame dimensions.</param>
    /// <returns>The editable regions in spatial order.</returns>
    private static IReadOnlyList<Color[]> SplitOverlay(Color[] source, Vector2Int size)
    {
        int[] assigned = new int[source.Length];
        Array.Fill(assigned, -1);
        List<(List<int> Pixels, Vector2 Center)> regions = new();
        int gap = Mathf.Max(1, GetEffectCellSize(size) / 2);
        for (int index = 0; index < source.Length; index++)
        {
            if (source[index].a == 0f || assigned[index] != -1)
                continue;
            List<int> region = new() { index };
            assigned[index] = regions.Count;
            Vector2 sum = Vector2.zero;
            for (int cursor = 0; cursor < region.Count; cursor++)
            {
                int pixel = region[cursor];
                int x = pixel % size.x;
                int y = pixel / size.x;
                sum += new Vector2(x, y);
                for (int distance = 1; distance <= gap; distance++)
                {
                    if (x >= distance)
                        Visit(pixel - distance);
                    if (x + distance < size.x)
                        Visit(pixel + distance);
                    if (y >= distance)
                        Visit(pixel - distance * size.x);
                    if (y + distance < size.y)
                        Visit(pixel + distance * size.x);
                }
            }
            regions.Add((region, sum / region.Count));

            void Visit(int candidate)
            {
                if (source[candidate].a == 0f || assigned[candidate] != -1)
                    return;
                assigned[candidate] = regions.Count;
                region.Add(candidate);
            }
        }
        if (regions.Count == 0)
            return Array.Empty<Color[]>();
        regions.Sort(
            (a, b) =>
            {
                int area = b.Pixels.Count.CompareTo(a.Pixels.Count);
                return area != 0 ? area : a.Pixels[0].CompareTo(b.Pixels[0]);
            }
        );
        int minimum = Mathf.Max(8, size.y * size.y / 1800);
        int retained = 1;
        while (
            retained < regions.Count && retained < 8 && regions[retained].Pixels.Count >= minimum
        )
            retained++;
        for (int index = retained; index < regions.Count; index++)
        {
            var region = regions[index];
            int nearest = 0;
            for (int candidate = 1; candidate < retained; candidate++)
                if (
                    (region.Center - regions[candidate].Center).sqrMagnitude
                    < (region.Center - regions[nearest].Center).sqrMagnitude
                )
                    nearest = candidate;
            regions[nearest].Pixels.AddRange(region.Pixels);
        }
        var main = regions.GetRange(0, retained);
        main.Sort(
            (a, b) =>
            {
                int row = a.Center.y.CompareTo(b.Center.y);
                return row != 0 ? row : a.Center.x.CompareTo(b.Center.x);
            }
        );
        List<Color[]> separated = new(retained);
        foreach (var region in main)
        {
            Color[] layer = new Color[source.Length];
            foreach (int index in region.Pixels)
                layer[index] = source[index];
            separated.Add(layer);
        }
        return separated;
    }

    /// <summary>
    /// Separates an authored transit card into its backing, streak bands, and foreground.
    /// </summary>
    /// <param name="source">The existing gameplay card.</param>
    /// <param name="subject">The registered normal unit silhouette.</param>
    /// <param name="size">The frame dimensions.</param>
    /// <param name="facility">Whether the runtime consumes a complete facility composite.</param>
    /// <param name="background">The backing field retained beneath editable features.</param>
    /// <param name="layers">Receives the layers in draw order.</param>
    /// <param name="bandCount">The number of editable fields to create.</param>
    private static void SplitTransitCard(
        Color[] source,
        Color[] subject,
        Vector2Int size,
        bool facility,
        Color[] background,
        List<Color[]> layers,
        int bandCount = 3
    )
    {
        for (int band = 0; band < bandCount; band++)
            layers.Add(new Color[source.Length]);
        Color[] foreground = new Color[source.Length];
        for (int index = 0; index < source.Length; index++)
        {
            Color pixel = source[index];
            background[index] = pixel.a >= 1f ? Color.black : Color.clear;
            if (pixel.a == 0f)
                continue;
            bool inFacility = facility && subject[index].a >= 0.5f;
            bool accent = !facility && pixel.g > pixel.r * 1.45f && pixel.b <= pixel.g * 1.1f;
            if (inFacility || accent)
                foreground[index] = pixel;
            else if (pixel.r > 0f || pixel.g > 0f || pixel.b > 0f || pixel.a < 1f)
                layers[(index / size.x) * bandCount / size.y][index] = pixel;
        }
        if (facility)
            layers.Add(foreground);
        else
            layers.AddRange(SplitOverlay(foreground, size));
    }

    /// <summary>
    /// Separates a registered facility and its dark outline from the complete authored field.
    /// </summary>
    /// <param name="card">The authored transit composite.</param>
    /// <param name="subject">The registered normal subject pixels.</param>
    /// <param name="distance">The distance from the silhouette, when it has a border.</param>
    /// <param name="clearance">The thickness of the dark outline.</param>
    /// <param name="background">The fixed matte used behind the editable layers.</param>
    /// <param name="layers">The field and facility layers in draw order.</param>
    private static void SplitRegisteredFacilityCard(
        Color[] card,
        Color[] subject,
        int[] distance,
        int clearance,
        Color[] background,
        List<Color[]> layers
    )
    {
        var field = new Color[card.Length];
        var facility = new Color[card.Length];
        for (int index = 0; index < card.Length; index++)
        {
            Color pixel = card[index];
            background[index] = pixel.a >= 1f ? Color.black : Color.clear;
            if (pixel.a <= 0f)
                continue;
            bool black = pixel.maxColorComponent == 0f;
            if (
                subject[index].a >= 0.5f
                || (black && distance != null && distance[index] <= clearance)
            )
                facility[index] = pixel;
            else if (!black || pixel.a < 1f)
                field[index] = pixel;
        }
        layers.Add(field);
        layers.Add(facility);
    }

    /// <summary>
    /// Measures the clear rings around a facility silhouette.
    /// </summary>
    /// <param name="subject">The fitted facility artwork on a transparent canvas.</param>
    /// <param name="size">The canvas dimensions.</param>
    /// <param name="radius">The farthest ring used by the margin.</param>
    /// <returns>The distance, in pixels, from each point to the nearest visible part of the facility.</returns>
    private static int[] GetSilhouetteDistance(Color[] subject, Vector2Int size, int radius)
    {
        int[] distance = new int[subject.Length];
        Array.Fill(distance, radius + 1);
        Queue<int> nearby = new();
        for (int i = 0; i < subject.Length; i++)
        {
            if (subject[i].a < 0.5f)
                continue;
            distance[i] = 0;
            nearby.Enqueue(i);
        }
        while (nearby.Count > 0)
        {
            int index = nearby.Dequeue();
            int next = distance[index] + 1;
            if (next > radius)
                continue;
            int x = index % size.x;
            int y = index / size.x;
            for (int row = Mathf.Max(0, y - 1); row <= Mathf.Min(size.y - 1, y + 1); row++)
            for (int column = Mathf.Max(0, x - 1); column <= Mathf.Min(size.x - 1, x + 1); column++)
            {
                int pixel = row * size.x + column;
                if (distance[pixel] <= next)
                    continue;
                distance[pixel] = next;
                nearby.Enqueue(pixel);
            }
        }
        return distance;
    }

    /// <summary>
    /// Recognizes a dark gap around the facility in a registered transit card.
    /// </summary>
    /// <param name="distance">The silhouette distance at each sample pixel.</param>
    /// <param name="size">The canvas dimensions.</param>
    /// <param name="reference">The authored transit card, if supplied.</param>
    /// <param name="radius">The number of rings available for comparison.</param>
    /// <returns>The thickness of the clear border in source pixels.</returns>
    private static int GetFacilityClearance(
        int[] distance,
        Vector2Int size,
        Color[] reference,
        int radius
    )
    {
        int defaultClearance = Mathf.RoundToInt(size.x * 7f / 302);
        if (reference == null)
            return defaultClearance;

        int[] pixelsPerRing = new int[radius + 1];
        int[] streaksPerRing = new int[radius + 1];
        int count = 0;
        for (int i = 0; i < reference.Length; i++)
        {
            int ring = distance[i];
            if (ring == 0)
                continue;
            bool streak = IsTravelStreak(reference[i]);
            if (streak)
                count++;
            if (ring > radius)
                continue;
            pixelsPerRing[ring]++;
            if (streak)
                streaksPerRing[ring]++;
        }
        if (count == 0)
            return 0;
        for (int ring = 1; ring < radius; ring++)
        {
            // Require the field to continue past reflections and antialiased silhouette edges.
            if (
                streaksPerRing[ring] * 200 > pixelsPerRing[ring]
                && streaksPerRing[ring + 1] * 200 > pixelsPerRing[ring + 1]
                && (ring + 2 > radius || streaksPerRing[ring + 2] * 200 > pixelsPerRing[ring + 2])
            )
                return ring - 1;
        }
        return defaultClearance;
    }

    /// <summary>
    /// Attaches a solid dark band to the facility layer so it moves with the silhouette.
    /// </summary>
    /// <param name="foreground">The editable facility layer to update.</param>
    /// <param name="distance">The silhouette distance at each position.</param>
    /// <param name="clearance">The width of the surrounding band.</param>
    /// <param name="reference">The authored card when a draft starts from existing artwork.</param>
    private static void AddFacilityClearance(
        Color[] foreground,
        int[] distance,
        int clearance,
        Color[] reference = null
    )
    {
        for (int i = 0; i < foreground.Length; i++)
        {
            if (distance[i] > clearance)
                continue;
            if (reference == null)
                foreground[i] = Over(Color.black, foreground[i]);
            else if (reference[i].a >= 1f && reference[i].maxColorComponent == 0f)
                foreground[i] = Color.black;
        }
    }

    /// <summary>
    /// Picks the reference whose visible hull is closest to the new ship's silhouette.
    /// </summary>
    /// <param name="subject">The new ship's compact or display artwork.</param>
    /// <param name="references">Registered wake and hull pairs from the same ship category.</param>
    /// <returns>The closest usable pair, if one could be read.</returns>
    internal static ContentEditorTravelPattern SelectShipTravelPattern(
        string subject,
        IReadOnlyList<ContentEditorTravelPattern> references
    )
    {
        Vector2Int size = new(297, 113);
        if (!TryMeasureTravelHull(subject, size, out Vector3 target))
            return default;

        ContentEditorTravelPattern best = default;
        float smallest = float.PositiveInfinity;
        foreach (ContentEditorTravelPattern reference in references)
        {
            if (!TryMeasureTravelHull(reference.Subject, size, out Vector3 candidate))
                continue;
            Vector3 delta = target - candidate;
            float difference = delta.x * delta.x + 4f * delta.y * delta.y + delta.z * delta.z;
            if (difference >= smallest)
                continue;
            smallest = difference;
            best = reference;
        }
        return best;
    }

    /// <summary>
    /// Measures the silhouette's proportions, orientation, and fill for selecting a wake.
    /// </summary>
    /// <param name="source">The source hull artwork.</param>
    /// <param name="size">The compact card used for all candidates.</param>
    /// <param name="measurement">The measured aspect, angle, and visible fill.</param>
    /// <returns>Whether a transparent hull was available.</returns>
    private static bool TryMeasureTravelHull(
        string source,
        Vector2Int size,
        out Vector3 measurement
    )
    {
        measurement = default;
        Color[] pixels = ReadExistingEffect(source, size);
        if (pixels == null || !TryGetTravelBounds(pixels, size, out RectInt bounds))
            return false;
        int area = 0;
        foreach (Color pixel in pixels)
            if (pixel.a >= 0.5f)
                area++;
        Vector2 axis = GetShipAxis(pixels, size, bounds);
        measurement = new Vector3(
            Mathf.Log((float)bounds.width / bounds.height),
            Mathf.Atan2(axis.y, axis.x),
            (float)area / (bounds.width * bounds.height)
        );
        return true;
    }

    /// <summary>
    /// Checks whether a referenced image contains a usable transparent subject.
    /// </summary>
    /// <param name="pixels">The fitted reference image.</param>
    /// <param name="size">The canvas dimensions.</param>
    /// <param name="bounds">The visible subject bounds.</param>
    /// <returns>Whether the image supplied both visible artwork and transparency.</returns>
    private static bool TryGetTravelBounds(Color[] pixels, Vector2Int size, out RectInt bounds)
    {
        try
        {
            bounds = FindSubject(pixels, size);
            return true;
        }
        catch (InvalidDataException)
        {
            bounds = default;
            return false;
        }
    }

    /// <summary>
    /// Checks whether a saved reference and the current artwork share the same hull.
    /// </summary>
    /// <param name="reference">The registered display image, which may be a packed snapshot.</param>
    /// <param name="subject">The display currently assigned to the unit.</param>
    /// <returns>Whether the effect may retain its exact placement.</returns>
    private static bool IsRegisteredTravelSubject(string reference, string subject)
    {
        if (string.IsNullOrEmpty(reference) || string.IsNullOrEmpty(subject))
            return false;
        if (string.Equals(reference, subject, StringComparison.OrdinalIgnoreCase))
            return true;
        return new FileInfo(reference).Length == new FileInfo(subject).Length
            && File.ReadAllBytes(reference).AsSpan().SequenceEqual(File.ReadAllBytes(subject));
    }

    /// <summary>
    /// Places a complete transparent wake overlay with the current ship artwork.
    /// </summary>
    /// <param name="pattern">The registered pattern and its hull.</param>
    /// <param name="subjectPath">The current hull address.</param>
    /// <param name="subject">The current hull pixels.</param>
    /// <param name="bounds">The current hull bounds.</param>
    /// <param name="size">The destination artwork size.</param>
    /// <param name="variant">The optional reach or spread adjustment.</param>
    /// <returns>The complete wake in its new position, when a reference could be fitted.</returns>
    private static Color[] FitTravelWake(
        ContentEditorTravelPattern pattern,
        string subjectPath,
        Color[] subject,
        RectInt bounds,
        Vector2Int size,
        int variant
    )
    {
        Color[] wake = ReadExistingEffect(pattern.Image, size);
        if (wake == null || !TryGetTravelBounds(wake, size, out _))
            return null;
        bool registered = IsRegisteredTravelSubject(pattern.Subject, subjectPath);
        if (registered && variant == 0)
            return wake;

        Color[] donor = registered ? subject : ReadExistingEffect(pattern.Subject, size);
        if (donor == null || !TryGetTravelBounds(donor, size, out RectInt donorBounds))
            return null;

        Vector2 fromAxis = GetShipAxis(donor, size, donorBounds);
        Vector2 toAxis = GetShipAxis(subject, size, bounds);
        Vector2 fromOrigin = GetAftCenter(donor, size, donorBounds, fromAxis);
        Vector2 toOrigin = GetAftCenter(subject, size, bounds, toAxis);
        Vector2 fromAcross = new(-fromAxis.y, fromAxis.x);
        Vector2 toAcross = new(-toAxis.y, toAxis.x);
        float scale = Mathf.Clamp(
            Mathf.Sqrt(
                (float)(bounds.width * bounds.height) / (donorBounds.width * donorBounds.height)
            ),
            0.7f,
            1.3f
        );
        float reach = scale * (variant == 1 ? 1.14f : 1f);
        float spread = scale * (variant == 2 ? 1.14f : 1f);
        Color[] result = new Color[wake.Length];
        for (int y = 0; y < size.y; y++)
        for (int x = 0; x < size.x; x++)
        {
            Vector2 relative = new Vector2(x, y) - toOrigin;
            Vector2 original =
                fromOrigin
                + fromAxis * (Vector2.Dot(relative, toAxis) / reach)
                + fromAcross * (Vector2.Dot(relative, toAcross) / spread);
            int sx = Mathf.RoundToInt(original.x);
            int sy = Mathf.RoundToInt(original.y);
            if ((uint)sx < (uint)size.x && (uint)sy < (uint)size.y)
                result[y * size.x + x] = wake[sy * size.x + sx];
        }
        return result;
    }

    /// <summary>
    /// Changes the reach of one wake while keeping the part attached to the hull in place.
    /// </summary>
    /// <param name="wake">One separable layer from the fitted wake.</param>
    /// <param name="subject">The fitted hull under the wake.</param>
    /// <param name="size">The output dimensions.</param>
    /// <param name="axis">The ship's aft-facing axis.</param>
    /// <param name="random">The saved variation stream.</param>
    /// <returns>The authored artwork resampled to a new trail length.</returns>
    private static Color[] VaryWakeReach(
        Color[] wake,
        Color[] subject,
        Vector2Int size,
        Vector2 axis,
        System.Random random
    )
    {
        float front = float.PositiveInfinity;
        float lastContact = float.NegativeInfinity;
        for (int index = 0; index < wake.Length; index++)
        {
            if (wake[index].a == 0f)
                continue;
            float position = Vector2.Dot(new Vector2(index % size.x, index / size.x), axis);
            front = Mathf.Min(front, position);
            if (subject[index].a >= 0.55f)
                lastContact = Mathf.Max(lastContact, position);
        }
        float anchor = Mathf.Max(front, lastContact);

        // Keep the longest available trail inside the card. Use the short and long wake presets
        // as the bounds so this uses the same range as the built-in ship effects.
        float maximum = _longWakeReach;
        for (int index = 0; index < wake.Length; index++)
        {
            if (wake[index].a == 0f)
                continue;
            Vector2 point = new(index % size.x, index / size.x);
            float distance = Vector2.Dot(point, axis) - anchor;
            if (distance <= 0f)
                continue;
            Vector2 tail = axis * distance;
            float right = size.x - 1 - point.x;
            float top = size.y - 1 - point.y;
            if (tail.x > 0f)
                maximum = Mathf.Min(maximum, 1f + right / tail.x);
            if (tail.y > 0f)
                maximum = Mathf.Min(maximum, 1f + top / tail.y);
            if (tail.y < 0f)
                maximum = Mathf.Min(maximum, 1f - point.y / tail.y);
        }

        float correction = 1f / RandomBetween(random, _shortWakeReach, maximum) - 1f;
        var pixels = new Color[wake.Length];
        for (int y = 0; y < size.y; y++)
        for (int x = 0; x < size.x; x++)
        {
            Vector2 point = new(x, y);
            float distance = Vector2.Dot(point, axis) - anchor;
            Vector2 original = point + axis * (Mathf.Max(0f, distance) * correction);
            int sx = Mathf.RoundToInt(original.x);
            int sy = Mathf.RoundToInt(original.y);
            if ((uint)sx < (uint)size.x && (uint)sy < (uint)size.y)
                pixels[y * size.x + x] = wake[sy * size.x + sx];
        }
        return pixels;
    }

    /// <summary>
    /// Recovers a facility's registered compact subject when both cards have baked backgrounds.
    /// </summary>
    /// <param name="pattern">The matching normal and travel cards.</param>
    /// <param name="size">The expected format for both cards.</param>
    /// <param name="subject">The decoded normal card when it is already available.</param>
    /// <returns>The facility pixels with transparency, when the pair supplies a silhouette.</returns>
    private static Color[] ResolveFacilityTravelSubject(
        ContentEditorTravelPattern pattern,
        Vector2Int size,
        Color[] subject
    )
    {
        subject ??= ReadExistingEffect(pattern.Subject, size);
        if (subject == null || TryGetTravelBounds(subject, size, out _))
            return subject;
        Color[] travel = ReadTravelFieldReference(pattern.Image, size);
        if (travel == null)
            return null;

        var foreground = new Color[subject.Length];
        for (int index = 0; index < foreground.Length; index++)
        {
            Color32 normalPixel = subject[index];
            Color32 travelPixel = travel[index];
            if (
                normalPixel.a == byte.MaxValue
                && (normalPixel.r | normalPixel.g | normalPixel.b) != 0
                && normalPixel.r == travelPixel.r
                && normalPixel.g == travelPixel.g
                && normalPixel.b == travelPixel.b
                && normalPixel.a == travelPixel.a
            )
                foreground[index] = travel[index];
        }
        return TryGetTravelBounds(foreground, size, out _) ? foreground : null;
    }

    /// <summary>
    /// Builds a complete field from the pack's streak pattern and its native color steps.
    /// </summary>
    /// <param name="pattern">The registered travel artwork and any subject it contains.</param>
    /// <param name="subjectPath">The current unit image address.</param>
    /// <param name="size">The destination card's dimensions.</param>
    /// <param name="style">The runtime format used by the card.</param>
    /// <param name="variant">The selected horizontal phase of the field.</param>
    /// <param name="random">The seed used when the pack supplies no reference.</param>
    /// <param name="facilitySubject">The facility silhouette recovered from the pair, if applicable.</param>
    /// <param name="longestStreak">Receives the reference field's longest streak.</param>
    /// <returns>The full field as one editable layer.</returns>
    private static Color[] CreateTravelField(
        ContentEditorTravelPattern pattern,
        string subjectPath,
        Vector2Int size,
        ContentEditorArtworkEffectStyle style,
        int variant,
        System.Random random,
        Color[] facilitySubject,
        out int longestStreak
    )
    {
        Color[] reference = ReadTravelFieldReference(pattern.Image, size);
        if (reference == null)
        {
            longestStreak = size.x;
            return DrawStreaks(size, style, random, variant, 0, 1);
        }

        Color[] donor = facilitySubject ?? ReadExistingEffect(pattern.Subject, size);
        if (donor != null && facilitySubject == null && !TryGetTravelBounds(donor, size, out _))
            donor = null;
        longestStreak = GetLongestTravelStreak(reference, donor, size);
        bool registered = IsRegisteredTravelSubject(pattern.Subject, subjectPath);
        Color[] field = new Color[reference.Length];
        int phase =
            variant == 1 ? size.x / 9
            : variant == 2 ? size.x / 5
            : 0;
        int shadowPhase = size.y / 2;
        for (int y = 0; y < size.y; y++)
        for (int x = 0; x < size.x; x++)
        {
            int sx = (x + phase) % size.x;
            int index = y * size.x + sx;
            Color pixel = reference[index];
            bool blocked = (donor != null && donor[index].a >= 0.5f) || pixel.a < 0.1f;
            if (blocked)
            {
                if (registered && phase == 0)
                    continue;
                int other = ((y + shadowPhase) % size.y) * size.x + sx;
                if (donor != null && donor[other].a >= 0.5f)
                    continue;
                pixel = reference[other];
            }
            if (IsTravelStreak(pixel))
                field[y * size.x + x] = pixel;
        }
        return field;
    }

    /// <summary>
    /// Recognizes the blue strokes while excluding transparent pixels and cyan accents.
    /// </summary>
    /// <param name="color">The pixel from a travel field.</param>
    /// <returns>True for a pixel in a blue stroke.</returns>
    private static bool IsTravelStreak(Color32 color) =>
        color.a > 128 && color.b > color.r + 12 && color.b > color.g + 7;

    /// <summary>
    /// Measures the longest visible horizontal stroke in the registered field.
    /// </summary>
    /// <param name="reference">The registered travel field pixels.</param>
    /// <param name="donor">The registered foreground to exclude, when available.</param>
    /// <param name="size">The reference canvas dimensions.</param>
    /// <returns>The upper stroke length for the generated field.</returns>
    private static int GetLongestTravelStreak(Color[] reference, Color[] donor, Vector2Int size)
    {
        int longest = 0;
        for (int y = 0; y < size.y; y++)
        {
            int length = 0;
            for (int x = 0; x < size.x; x++)
            {
                int index = y * size.x + x;
                if (IsTravelStreak(reference[index]) && (donor == null || donor[index].a < 0.5f))
                {
                    length++;
                    longest = Math.Max(longest, length);
                }
                else
                    length = 0;
            }
        }
        return longest;
    }

    /// <summary>
    /// Samples a gameplay field without smoothing pixel edges or doubling the stroke lengths.
    /// </summary>
    /// <param name="path">The authored travel image, if configured.</param>
    /// <param name="size">The destination canvas.</param>
    /// <returns>The sampled field pixels, or null when no image was supplied.</returns>
    private static Color[] ReadTravelFieldReference(string path, Vector2Int size)
    {
        if (string.IsNullOrEmpty(path))
            return null;

        Texture2D texture = new(2, 2, TextureFormat.RGBA32, false);
        try
        {
            if (!ImageConversion.LoadImage(texture, File.ReadAllBytes(path)))
                return null;
            Color[] source = texture.GetPixels();
            int width = texture.width;
            int height = texture.height;
            if (width == size.x && height == size.y)
                return source;

            Color[] field = new Color[size.x * size.y];
            Rect crop = ContentEditorArtworkCrop.GetCenteredUvRect(
                new Vector2Int(width, height),
                size
            );
            bool tile = size.x > width * 1.25f;
            for (int y = 0; y < size.y; y++)
            for (int x = 0; x < size.x; x++)
            {
                int sx = tile
                    ? x % (width + 2)
                    : Mathf.Min(
                        width - 1,
                        Mathf.FloorToInt((crop.x + (x + 0.5f) * crop.width / size.x) * width)
                    );
                int sy = tile
                    ? y % height
                    : Mathf.Min(
                        height - 1,
                        Mathf.FloorToInt((crop.y + (y + 0.5f) * crop.height / size.y) * height)
                    );
                if (sx < width)
                    field[y * size.x + x] = source[sy * width + sx];
            }
            return field;
        }
        finally
        {
            Release(texture);
        }
    }

    /// <summary>
    /// Keeps visible strokes within the reference length and clears short clipped remnants.
    /// </summary>
    /// <param name="field">The complete editable streak field.</param>
    /// <param name="subject">The normal unit pixels.</param>
    /// <param name="size">The card dimensions.</param>
    /// <param name="distance">The facility outline distance when applicable.</param>
    /// <param name="clearance">The facility's dark gap from the silhouette.</param>
    /// <param name="longestStreak">The longest visible stroke in the reference.</param>
    private static void FitVisibleTravelStreaks(
        Color[] field,
        Color[] subject,
        Vector2Int size,
        int[] distance,
        int clearance,
        int longestStreak
    )
    {
        const int minimum = 4;
        int maximum = Math.Max(minimum, longestStreak);
        for (int y = 0; y < size.y; y++)
        {
            int offset = y * size.x;
            int first = -1;
            for (int x = 0; x <= size.x; x++)
            {
                int index = offset + x;
                if (
                    x < size.x
                    && field[index].a > 0f
                    && subject[index].a < 0.5f
                    && (distance == null || distance[index] > clearance)
                )
                {
                    if (first < 0)
                        first = x;
                    continue;
                }
                if (first < 0)
                    continue;
                while (x - first > maximum)
                {
                    int gap = first + Mathf.Clamp((x - first) / 2, minimum, maximum);
                    int gapLength = Math.Min(2, x - gap);
                    Array.Clear(field, offset + gap, gapLength);
                    first = gap + gapLength;
                }
                if (x - first < minimum)
                    Array.Clear(field, offset + first, x - first);
                first = -1;
            }
        }
    }

    /// <summary>
    /// Loads the gameplay state already supplied by the pack so it can be opened as layers.
    /// </summary>
    /// <param name="path">The resolved file for the current effect, if configured.</param>
    /// <param name="size">The target band's artwork dimensions.</param>
    /// <returns>The existing effect pixels, or null when a generated layer is needed.</returns>
    private static Color[] ReadExistingEffect(string path, Vector2Int size)
    {
        if (string.IsNullOrEmpty(path))
            return null;
        Texture2D texture = new(2, 2, TextureFormat.RGBA32, false);
        try
        {
            return ImageConversion.LoadImage(texture, File.ReadAllBytes(path))
                ? FitToCanvas(texture, size, stretchSource: true)
                : null;
        }
        finally
        {
            Release(texture);
        }
    }

    /// <summary>
    /// Samples the same centered background crop used in the editor's gameplay cards.
    /// </summary>
    /// <param name="texture">The readable background image.</param>
    /// <param name="size">The preview canvas size.</param>
    /// <returns>The cropped background pixels.</returns>
    private static Color[] FitPreviewBackground(Texture2D texture, Vector2Int size)
    {
        Rect crop = ContentEditorArtworkCrop.GetCenteredUvRect(
            new Vector2Int(texture.width, texture.height),
            size
        );
        Color[] pixels = new Color[size.x * size.y];
        for (int y = 0; y < size.y; y++)
        for (int x = 0; x < size.x; x++)
            pixels[y * size.x + x] = texture.GetPixelBilinear(
                crop.x + (x + 0.5f) * crop.width / size.x,
                crop.y + (y + 0.5f) * crop.height / size.y
            );
        return pixels;
    }

    /// <summary>
    /// Moves a rendered layer within its fixed output bounds.
    /// </summary>
    /// <param name="pixels">The layer to move.</param>
    /// <param name="size">The output dimensions.</param>
    /// <param name="offset">The displacement in canvas proportions.</param>
    /// <returns>The translated layer with transparent uncovered area.</returns>
    private static Color[] ShiftLayer(Color[] pixels, Vector2Int size, Vector2 offset)
    {
        int dx = Mathf.RoundToInt(offset.x * size.x);
        int dy = Mathf.RoundToInt(offset.y * size.y);
        if (dx == 0 && dy == 0)
            return pixels;
        Color[] moved = new Color[pixels.Length];
        int left = Mathf.Max(dx, 0);
        int width = size.x - Mathf.Abs(dx);
        if (width <= 0)
            return moved;
        for (int y = Mathf.Max(dy, 0); y < Mathf.Min(size.y, size.y + dy); y++)
            Array.Copy(pixels, (y - dy) * size.x + left - dx, moved, y * size.x + left, width);
        return moved;
    }

    /// <summary>
    /// Draws an independently placed extra impact or exhaust trail.
    /// </summary>
    /// <param name="layer">The transparent effect canvas.</param>
    /// <param name="size">The output dimensions.</param>
    /// <param name="style">The gameplay effect being edited.</param>
    /// <param name="position">The new effect center in canvas proportions.</param>
    /// <param name="random">The seeded source of effect details.</param>
    private static void DrawAdditionalEffect(
        Color[] layer,
        Vector2Int size,
        ContentEditorArtworkEffectStyle style,
        Vector2 position,
        System.Random random
    )
    {
        Vector2 center = new(position.x * size.x, position.y * size.y);
        if (style == ContentEditorArtworkEffectStyle.Damage)
            DrawImpact(layer, size, center, size.y * 0.16f, RandomBetween(random, 0, 2 * Mathf.PI));
        else if (style == ContentEditorArtworkEffectStyle.ShipTravel)
        {
            float halfLength = size.y * 0.23f;
            DrawExhaustTrail(
                layer,
                size,
                GetEffectCellSize(size),
                center.x - halfLength,
                center.x + halfLength,
                center.y,
                size.y * 0.075f
            );
        }
    }

    /// <summary>
    /// Uses a shared wake for a connected stern and separate exhaust trails for distinct aft sections.
    /// </summary>
    /// <param name="subject">The fitted ship artwork.</param>
    /// <param name="size">The output dimensions.</param>
    /// <param name="bounds">The ship silhouette bounds.</param>
    /// <param name="random">The seeded variation source.</param>
    /// <returns>A transparent wake layer aligned to the ship.</returns>
    /// <param name="pieces">Receives the separate wakes when building an editable draft.</param>
    /// <param name="variant">The wake's reach and spread composition.</param>
    private static Color[] DrawShipTravel(
        Color[] subject,
        Vector2Int size,
        RectInt bounds,
        System.Random random,
        List<Color[]> pieces = null,
        int variant = 0
    )
    {
        float reach =
            variant == 1 ? _longWakeReach
            : variant == 2 ? _shortWakeReach
            : 1f;
        float spread =
            variant == 1 ? 0.64f
            : variant == 2 ? 1.38f
            : 1f;
        Vector2 direction = GetShipAxis(subject, size, bounds);
        if (Mathf.Abs(direction.y) > 0.26f)
        {
            Color[] trail = new Color[subject.Length];
            Vector2 origin = GetAftCenter(subject, size, bounds, direction);
            DrawAngledWake(
                trail,
                size,
                subject,
                origin,
                direction,
                size.y * RandomBetween(random, 0.37f, 0.48f) * reach,
                size.y * 0.11f * spread
            );
            pieces?.Add(trail);
            return trail;
        }

        int cell = GetEffectCellSize(size);
        int minimumBandHeight = Mathf.Max(cell * 2, Mathf.RoundToInt(bounds.height * 0.13f));
        int aftStart = bounds.xMin + Mathf.RoundToInt(bounds.width * 0.70f);
        List<(int Bottom, int Top, int Right)> bands = new();
        int bottom = bounds.yMin;
        int right = -1;
        for (int y = bounds.yMin; y <= bounds.yMax; y++)
        {
            int rowTip = -1;
            if (y < bounds.yMax)
                for (int x = bounds.xMax - 1; x >= aftStart; x--)
                {
                    if (subject[y * size.x + x].a < 0.55f)
                        continue;
                    rowTip = x;
                    break;
                }
            if (rowTip < 0)
            {
                if (right >= 0 && y - bottom >= minimumBandHeight)
                    bands.Add((bottom, y, right));
                right = -1;
                continue;
            }
            if (right < 0)
                bottom = y;
            right = Mathf.Max(right, rowTip);
        }
        if (bands.Count < 2)
        {
            Color[] arc = DrawTravelArc(subject, size, bounds, random, reach, spread);
            pieces?.Add(arc);
            return arc;
        }

        Color[] pixels = new Color[subject.Length];
        foreach (var band in bands)
        {
            float center = (band.Bottom + band.Top) * 0.5f;
            float radius = (band.Top - band.Bottom) * RandomBetween(random, 0.35f, 0.5f) * spread;
            float length = Mathf.Max(
                cell * 4,
                size.y * RandomBetween(random, 0.38f, 0.54f) * reach
            );
            float tail = Mathf.Min(band.Right + length * 0.55f, size.x - cell);
            Color[] wake = pieces == null ? pixels : new Color[pixels.Length];
            DrawExhaustTrail(wake, size, cell, tail - length, tail, center, radius);
            pieces?.Add(wake);
        }
        return pixels;
    }

    /// <summary>
    /// Finds the orientation of an angled ship from the covariance of its visible silhouette.
    /// </summary>
    /// <param name="subject">The fitted ship image.</param>
    /// <param name="size">The canvas size.</param>
    /// <param name="bounds">The visible hull rectangle.</param>
    /// <returns>The ship's aft-facing axis.</returns>
    private static Vector2 GetShipAxis(Color[] subject, Vector2Int size, RectInt bounds)
    {
        double xSum = 0,
            ySum = 0,
            xx = 0,
            yy = 0,
            xy = 0;
        int count = 0;
        for (int y = bounds.yMin; y < bounds.yMax; y++)
        for (int x = bounds.xMin; x < bounds.xMax; x++)
        {
            if (subject[y * size.x + x].a < 0.55f)
                continue;
            count++;
            xSum += x;
            ySum += y;
            xx += (double)x * x;
            yy += (double)y * y;
            xy += (double)x * y;
        }
        if (count == 0)
            return Vector2.right;
        double cross = xy - xSum * ySum / count;
        double difference = xx - xSum * xSum / count - yy + ySum * ySum / count;
        float angle = (float)(0.5 * Math.Atan2(2 * cross, difference));
        return new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
    }

    /// <summary>
    /// Finds the center of the aft silhouette along the directed ship axis.
    /// </summary>
    /// <param name="subject">The fitted ship image.</param>
    /// <param name="size">The canvas size.</param>
    /// <param name="bounds">The visible hull rectangle.</param>
    /// <param name="direction">The axis pointing toward the aft end.</param>
    /// <returns>The point where an angled exhaust trail begins.</returns>
    private static Vector2 GetAftCenter(
        Color[] subject,
        Vector2Int size,
        RectInt bounds,
        Vector2 direction
    )
    {
        float aft = float.NegativeInfinity;
        for (int y = bounds.yMin; y < bounds.yMax; y++)
        for (int x = bounds.xMin; x < bounds.xMax; x++)
            if (subject[y * size.x + x].a >= 0.55f)
                aft = Mathf.Max(aft, x * direction.x + y * direction.y);

        Vector2 sum = Vector2.zero;
        int count = 0;
        float cutoff = aft - Mathf.Min(bounds.width, bounds.height) * 0.14f;
        for (int y = bounds.yMin; y < bounds.yMax; y++)
        for (int x = bounds.xMin; x < bounds.xMax; x++)
        {
            if (subject[y * size.x + x].a < 0.55f || x * direction.x + y * direction.y < cutoff)
                continue;
            sum += new Vector2(x, y);
            count++;
        }
        return count > 0 ? sum / count : bounds.center;
    }

    /// <summary>
    /// Draws a tapered exhaust along the source artwork's orientation.
    /// </summary>
    /// <param name="pixels">The destination effect layer.</param>
    /// <param name="size">The output size.</param>
    /// <param name="subject">An optional ship silhouette that masks exhaust pixels.</param>
    /// <param name="origin">The trail's starting position.</param>
    /// <param name="direction">The direction away from the hull.</param>
    /// <param name="length">The trail's visible length.</param>
    /// <param name="radius">The trail's starting half-width.</param>
    private static void DrawAngledWake(
        Color[] pixels,
        Vector2Int size,
        Color[] subject,
        Vector2 origin,
        Vector2 direction,
        float length,
        float radius
    )
    {
        int cell = GetEffectCellSize(size);
        Vector2 across = new(-direction.y, direction.x);
        for (int y = 0; y < size.y; y += cell)
        for (int x = 0; x < size.x; x += cell)
        {
            Vector2 point = new Vector2(GetCellCenter(x, cell), GetCellCenter(y, cell)) - origin;
            float progress = Vector2.Dot(point, direction) / length;
            if (progress < 0f || progress >= 1f)
                continue;
            float halfWidth = radius * Mathf.Lerp(1f, 0.24f, progress);
            float distance = Mathf.Abs(Vector2.Dot(point, across)) / halfWidth;
            if (distance > 1f)
                continue;
            Color color =
                distance < 0.48f && progress < 0.76f ? _wakeCore
                : distance < 0.74f && progress < 0.93f ? _wakeEdge
                : _wakeShadow;
            for (int row = y; row < Mathf.Min(y + cell, size.y); row++)
            for (int column = x; column < Mathf.Min(x + cell, size.x); column++)
            {
                int index = row * size.x + column;
                if (subject == null || subject[index].a < 0.55f)
                    pixels[index] = color;
            }
        }
    }

    /// <summary>
    /// Draws a tapered, color-banded exhaust trail behind one aft section.
    /// </summary>
    /// <param name="pixels">The destination effect pixels.</param>
    /// <param name="size">The output dimensions.</param>
    /// <param name="cell">The effect's pixel grid size.</param>
    /// <param name="start">The start of the trail at the engine.</param>
    /// <param name="tail">The trailing end of the exhaust.</param>
    /// <param name="center">The vertical center of the exhaust.</param>
    /// <param name="radius">The starting half-height of the exhaust.</param>
    private static void DrawExhaustTrail(
        Color[] pixels,
        Vector2Int size,
        int cell,
        float start,
        float tail,
        float center,
        float radius
    )
    {
        int bottom = Mathf.Max(cell, Mathf.FloorToInt((center - radius) / cell) * cell);
        int left = Mathf.Max(cell, Mathf.FloorToInt(start / cell) * cell);
        for (int x = left; x + cell < size.x && x < tail; x += cell)
        {
            float progress = (GetCellCenter(x, cell) - start) / (tail - start);
            if (progress < 0 || progress >= 1)
                continue;
            float halfHeight = radius * Mathf.Lerp(1f, 0.24f, progress);
            for (int y = bottom; y + cell < size.y && y < center + halfHeight; y += cell)
            {
                float distance = Mathf.Abs(GetCellCenter(y, cell) - center) / halfHeight;
                if (distance > 1f)
                    continue;
                Color color =
                    distance < 0.48f && progress < 0.76f ? _wakeCore
                    : distance < 0.74f && progress < 0.93f ? _wakeEdge
                    : _wakeShadow;
                for (int row = y; row < y + cell; row++)
                    Array.Fill(pixels, color, row * size.x + x, cell);
            }
        }
    }

    /// <summary>
    /// Resamples the full source canvas using the centered aspect fit of the unit image slot.
    /// </summary>
    /// <param name="source">The readable source texture.</param>
    /// <param name="target">The destination canvas.</param>
    /// <param name="stretchSource">Whether both runtime layers are stretched edge to edge.</param>
    /// <returns>Canvas pixels with the source placement preserved.</returns>
    private static Color[] FitToCanvas(Texture2D source, Vector2Int target, bool stretchSource)
    {
        if (source.width == target.x && source.height == target.y)
            return source.GetPixels();
        float scale = Mathf.Min((float)target.x / source.width, (float)target.y / source.height);
        float width = stretchSource ? target.x : scale * source.width;
        float height = stretchSource ? target.y : scale * source.height;
        float left = (target.x - width) / 2f;
        float bottom = (target.y - height) / 2f;
        Color[] pixels = new Color[target.x * target.y];
        for (int y = 0; y < target.y; y++)
        for (int x = 0; x < target.x; x++)
        {
            float u = (x + 0.5f - left) / width;
            float v = (y + 0.5f - bottom) / height;
            if (u >= 0f && u <= 1f && v >= 0f && v <= 1f)
                pixels[y * target.x + x] = source.GetPixelBilinear(u, v);
        }
        return pixels;
    }

    /// <summary>
    /// Locates the unit's alpha silhouette in the output frame.
    /// </summary>
    /// <param name="pixels">The unit pixels.</param>
    /// <param name="size">The complete canvas dimensions.</param>
    /// <returns>The rectangle containing the visible subject.</returns>
    private static RectInt FindSubject(Color[] pixels, Vector2Int size)
    {
        int left = size.x;
        int right = -1;
        int bottom = size.y;
        int top = -1;
        bool hasTransparency = false;
        for (int index = 0; index < pixels.Length; index++)
        {
            if (pixels[index].a < 0.1f)
                hasTransparency = true;
            if (pixels[index].a < 0.35f)
                continue;
            int x = index % size.x;
            int y = index / size.x;
            left = Mathf.Min(left, x);
            right = Mathf.Max(right, x);
            bottom = Mathf.Min(bottom, y);
            top = Mathf.Max(top, y);
        }
        if (right < left || !hasTransparency)
            throw new InvalidDataException(
                "Choose normal unit artwork with a visible subject on a transparent background."
            );
        return new RectInt(left, bottom, right - left + 1, top - bottom + 1);
    }

    /// <summary>
    /// Draws a stepped cyan wake aligned to the aft silhouette.
    /// </summary>
    /// <param name="subject">The fitted unit artwork.</param>
    /// <param name="size">The output canvas.</param>
    /// <param name="bounds">The silhouette bounds.</param>
    /// <param name="random">The seeded source of controlled variation.</param>
    /// <param name="reach">The horizontal stretch of the wake.</param>
    /// <param name="spread">The vertical spread of the wake.</param>
    /// <returns>A transparent forward layer containing the wake.</returns>
    private static Color[] DrawTravelArc(
        Color[] subject,
        Vector2Int size,
        RectInt bounds,
        System.Random random,
        float reach,
        float spread
    )
    {
        int aftStart = bounds.xMax - Mathf.Max(1, Mathf.RoundToInt(bounds.width * 0.16f));
        int bottom = bounds.yMax;
        int top = bounds.yMin;
        for (int y = bounds.yMin; y < bounds.yMax; y++)
        for (int x = aftStart; x < bounds.xMax; x++)
        {
            if (subject[y * size.x + x].a < 0.55f)
                continue;
            bottom = Mathf.Min(bottom, y);
            top = Mathf.Max(top, y);
        }
        int cell = GetEffectCellSize(size);
        float radiusY = Mathf.Min(
            size.y * 0.30f,
            Mathf.Max((top - bottom) * 0.43f, bounds.height * 0.22f) + size.y * 0.035f
        );
        radiusY *= spread;
        float radiusX = Mathf.Min(
            size.x * 0.24f,
            size.y * RandomBetween(random, 0.29f, 0.38f) * reach
        );
        float verticalMargin = Mathf.Min(size.y * 0.5f, radiusY * 1.38f + cell);
        float centerY = Mathf.Clamp(
            bottom <= top ? (bottom + top) * 0.5f : bounds.center.y,
            verticalMargin,
            size.y - verticalMargin
        );
        float centerX = Mathf.Min(bounds.xMax - radiusX * 0.7f, size.x - radiusX * 1.38f - cell);
        float phase = RandomBetween(random, 0, 6.283f);
        Color[] pixels = new Color[subject.Length];
        for (int y = 0; y < size.y; y++)
        for (int x = 0; x < size.x; x++)
        {
            float vertical = (GetCellCenter(y, cell) - centerY) / radiusY;
            float horizontal = (GetCellCenter(x, cell) - centerX) / radiusX;
            if (horizontal < -0.24f || horizontal > 1.48f || Mathf.Abs(vertical) > 1.4f)
                continue;
            float rim = 1f + 0.05f * Mathf.Sin(vertical * 10f + phase);
            float ring = Mathf.Abs(Mathf.Sqrt(horizontal * horizontal + vertical * vertical) - rim);
            bool nearTrailingEdge = horizontal > (Mathf.Abs(vertical) > 0.82f ? -0.16f : 0.20f);
            if (!nearTrailingEdge || ring >= 0.36f)
                continue;
            pixels[y * size.x + x] =
                ring < 0.105f && horizontal > 0.14f ? _wakeCore
                : ring < 0.23f ? _wakeEdge
                : _wakeShadow;
        }
        return pixels;
    }

    /// <summary>
    /// Places distinct blue streaks using the reference size and spacing of each gameplay card.
    /// </summary>
    /// <param name="size">The destination canvas.</param>
    /// <param name="style">The gameplay card's transit format.</param>
    /// <param name="random">The repeatable source of line placement.</param>
    /// <param name="variant">The selected speed line composition.</param>
    /// <param name="band">The vertical band assigned to this layer.</param>
    /// <param name="bands">The number of vertical bands in the composition.</param>
    /// <param name="extra">Whether this is an independently added smaller group.</param>
    /// <param name="blocked">The subject and existing artwork to avoid when adding a new group.</param>
    /// <returns>The visible lines on a transparent canvas.</returns>
    private static Color[] DrawStreaks(
        Vector2Int size,
        ContentEditorArtworkEffectStyle style,
        System.Random random,
        int variant,
        int band,
        int bands,
        bool extra = false,
        bool[] blocked = null
    )
    {
        bool personnel = style == ContentEditorArtworkEffectStyle.PersonnelTravel;
        float personnelSize = Mathf.InverseLerp(275, 549, size.x);
        var reference = style switch
        {
            ContentEditorArtworkEffectStyle.PersonnelTravel => (
                Scale: 1f,
                Widths: _compactPersonnelStreakWidths,
                Heights: _compactPersonnelStreakHeights,
                Count: Mathf.Lerp(68, 132, personnelSize)
            ),
            ContentEditorArtworkEffectStyle.FacilityTravel => (
                Scale: size.x / 302f,
                Widths: _facilityStreakWidths,
                Heights: _facilityStreakHeights,
                Count: 108f
            ),
            _ => (
                Scale: size.x / 275f,
                Widths: _regimentStreakWidths,
                Heights: _regimentStreakHeights,
                Count: 59f
            ),
        };
        float density =
            variant == 1 ? 0.66f
            : variant == 2 ? 1.4f
            : 1f;
        int firstRow = band * size.y / bands;
        int lastRow = (band + 1) * size.y / bands;
        Color[] pixels = new Color[size.x * size.y];
        int count = extra ? 5 : Mathf.CeilToInt(reference.Count * density / bands);
        bool[] usedRows = blocked == null ? null : new bool[size.y];
        for (int index = 0; index < count; index++)
        {
            float sample = (float)random.NextDouble();
            if (blocked != null)
                sample = (index + sample) / count;
            sample =
                variant == 1 ? Mathf.Sqrt(sample)
                : variant == 2 ? sample * sample
                : sample;
            float heightSample = (float)random.NextDouble();
            float width = SampleStreakSize(reference.Widths, sample);
            float height = SampleStreakSize(reference.Heights, heightSample);
            if (personnel)
            {
                width = Mathf.Lerp(
                    width,
                    SampleStreakSize(_largePersonnelStreakWidths, sample),
                    personnelSize
                );
                height = Mathf.Lerp(
                    height,
                    SampleStreakSize(_largePersonnelStreakHeights, heightSample),
                    personnelSize
                );
            }
            int length = Mathf.Clamp(Mathf.RoundToInt(width * reference.Scale), 1, size.x);
            int strokeHeight = Mathf.RoundToInt(height);
            int lastStart = lastRow - strokeHeight - 1;
            if (lastStart < firstRow)
                continue;

            if (blocked != null)
            {
                RectInt open = FindOpenStreak(
                    blocked,
                    usedRows,
                    size,
                    length,
                    strokeHeight,
                    random
                );
                if (open.width > 0)
                {
                    PaintStreak(pixels, size, open, open.xMin, open.width);
                    Array.Fill(usedRows, true, open.yMin, open.height);
                }
                continue;
            }

            const int attempts = 12;
            for (int attempt = 0; attempt < attempts; attempt++)
            {
                int head = random.Next(-length / 5, size.x);
                int left = Mathf.Max(0, head);
                int right = Mathf.Min(size.x, head + length);
                int y = random.Next(firstRow, lastStart + 1);
                RectInt stroke = new(left, y, right - left, strokeHeight);
                if (!HasSpaceForStreak(pixels, size, stroke))
                    continue;

                PaintStreak(pixels, size, stroke, head, length);
                break;
            }
        }
        return pixels;
    }

    /// <summary>
    /// Finds a stroke dimension by interpolating between consecutive measured deciles.
    /// </summary>
    /// <param name="deciles">The dimension values from minimum to maximum.</param>
    /// <param name="sample">The normalized rank of the requested dimension.</param>
    /// <returns>The sampled size in source pixels.</returns>
    private static float SampleStreakSize(int[] deciles, float sample)
    {
        float position = sample * (deciles.Length - 1);
        int index = Mathf.Min((int)position, deciles.Length - 2);
        return Mathf.Lerp(deciles[index], deciles[index + 1], position - index);
    }

    /// <summary>
    /// Tests a candidate's surroundings so streaks retain a visible gap.
    /// </summary>
    /// <param name="pixels">The effect band being drawn.</param>
    /// <param name="size">The destination canvas.</param>
    /// <param name="stroke">The candidate rectangle.</param>
    /// <returns>Whether the stroke can be drawn away from nearby streaks.</returns>
    private static bool HasSpaceForStreak(Color[] pixels, Vector2Int size, RectInt stroke)
    {
        const int horizontalGap = 3;
        int left = Mathf.Max(0, stroke.xMin - horizontalGap);
        int right = Mathf.Min(size.x, stroke.xMax + horizontalGap);
        int bottom = Mathf.Max(0, stroke.yMin - 1);
        int top = Mathf.Min(size.y, stroke.yMax + 1);
        for (int y = bottom; y < top; y++)
        for (int x = left; x < right; x++)
            if (pixels[y * size.x + x].a > 0f)
                return false;
        return true;
    }

    /// <summary>
    /// Finds space for a complete streak on its own row, away from the canvas edges.
    /// </summary>
    /// <param name="blocked">The pixels occupied by the existing layout.</param>
    /// <param name="usedRows">The rows occupied by the other new strokes in this group.</param>
    /// <param name="size">The destination canvas.</param>
    /// <param name="length">The sampled width, in source pixels.</param>
    /// <param name="height">The sampled height, in source pixels.</param>
    /// <param name="random">The source of varying starting positions.</param>
    /// <returns>A usable rectangle, or an empty rectangle when the field has no suitable space.</returns>
    private static RectInt FindOpenStreak(
        bool[] blocked,
        bool[] usedRows,
        Vector2Int size,
        int length,
        int height,
        System.Random random
    )
    {
        const int gap = 2;
        int margin = Mathf.Max(gap, size.y / 32);
        int rows = size.y - margin * 2 - height + 1;
        int right = size.x - margin;
        int minimum = Mathf.Max(4, height * 2);
        if (rows <= 0 || right - margin < minimum)
            return default;

        length = Mathf.Max(minimum, length);
        int startRow = random.Next(rows);
        int preferredX = random.Next(margin, right);
        int nearest = int.MaxValue;
        RectInt best = default;
        for (int offset = 0; offset < rows; offset++)
        {
            int y = margin + (startRow + offset) % rows;
            if (Array.IndexOf(usedRows, true, y - 1, height + 2) >= 0)
                continue;

            int runStart = margin;
            for (int x = margin; x <= right; x++)
            {
                if (x < right && IsOpenStreakColumn(blocked, size.x, x, y, height))
                    continue;
                int width = Mathf.Min(length, x - runStart - 2 * gap);
                if (width >= minimum && width >= best.width)
                {
                    int left = Mathf.Clamp(preferredX - width / 2, runStart + gap, x - gap - width);
                    int distance = Mathf.Abs(left + width / 2 - preferredX);
                    if (width > best.width || distance < nearest)
                    {
                        best = new RectInt(left, y, width, height);
                        nearest = distance;
                    }
                }
                runStart = x + 1;
            }
            if (best.width == length)
                break;
        }
        return best;
    }

    /// <summary>
    /// Tests a column for existing artwork across the stroke and its vertical gap.
    /// </summary>
    /// <param name="blocked">The pixels occupied by the layout.</param>
    /// <param name="width">The width of the complete canvas.</param>
    /// <param name="column">The horizontal coordinate to test.</param>
    /// <param name="y">The bottom row of the proposed stroke.</param>
    /// <param name="height">The height of the proposed stroke.</param>
    /// <returns>Whether the column is free along the height of the stroke.</returns>
    private static bool IsOpenStreakColumn(bool[] blocked, int width, int column, int y, int height)
    {
        for (int row = y - 1; row <= y + height; row++)
            if (blocked[row * width + column])
                return false;
        return true;
    }

    /// <summary>
    /// Draws one streak in color steps with its highlight aligned to the stroke's head.
    /// </summary>
    /// <param name="pixels">The editable streak layer.</param>
    /// <param name="size">The dimensions of the canvas.</param>
    /// <param name="stroke">The visible portion of the streak.</param>
    /// <param name="head">The start of the complete stroke.</param>
    /// <param name="length">The length of the complete stroke.</param>
    private static void PaintStreak(
        Color[] pixels,
        Vector2Int size,
        RectInt stroke,
        int head,
        int length
    )
    {
        int step = Mathf.Clamp(stroke.height, 3, 5);
        for (int x = stroke.xMin; x < stroke.xMax; x += step)
        {
            Color color = GetStreakColor((x - head) / (float)length);
            int segment = Mathf.Min(step, stroke.xMax - x);
            for (int row = stroke.yMin; row < stroke.yMax; row++)
                Array.Fill(pixels, color, row * size.x + x, segment);
        }
    }

    /// <summary>
    /// Gets the color at a point along a stepped blue streak.
    /// </summary>
    /// <param name="position">The normalized distance from the bright head.</param>
    /// <returns>The opaque palette color at that point.</returns>
    private static Color32 GetStreakColor(float position) =>
        position switch
        {
            < 0.16f => new Color32(224, 231, 255, 255),
            < 0.43f => new Color32(154, 177, 235, 255),
            < 0.76f => new Color32(94, 136, 201, 255),
            _ => new Color32(56, 74, 139, 255),
        };

    /// <summary>
    /// Re-creates the streak geometry stored by previous drafts.
    /// </summary>
    /// <param name="size">The destination canvas.</param>
    /// <param name="random">The repeatable source of line placement.</param>
    /// <param name="variant">The selected speed line composition.</param>
    /// <param name="band">The vertical band assigned to this layer.</param>
    /// <param name="bands">The number of vertical bands in the composition.</param>
    /// <param name="extra">Whether this is an independently added smaller group.</param>
    /// <returns>The visible lines on a transparent canvas.</returns>
    private static Color[] DrawLegacyStreaks(
        Vector2Int size,
        System.Random random,
        int variant,
        int band,
        int bands,
        bool extra = false
    )
    {
        var pattern = variant switch
        {
            1 => (Count: 32, Shortest: 0.16f, Longest: 0.52f),
            2 => (Count: 82, Shortest: 0.035f, Longest: 0.19f),
            _ => (Count: 56, Shortest: 0.07f, Longest: 0.32f),
        };
        Color[] pixels = new Color[size.x * size.y];
        int cell = GetEffectCellSize(size);
        int firstRow = band * size.y / bands;
        int lastRow = Mathf.Max(firstRow + 1, (band + 1) * size.y / bands);
        int count = extra ? 5 : Mathf.CeilToInt(pattern.Count / (float)bands);
        for (int index = 0; index < count; index++)
        {
            int length = Mathf.Max(
                cell * 2,
                Mathf.RoundToInt(
                    size.x * RandomBetween(random, pattern.Shortest, pattern.Longest) / cell
                ) * cell
            );
            int head = random.Next(-length / 3, size.x) / cell * cell;
            int y = random.Next(firstRow, lastRow) / cell * cell;
            int thickness = Mathf.Max(1, index % 4 == 0 ? cell : cell / 2);
            for (int offset = 0; offset < length; offset += cell)
            {
                int left = Mathf.Max(0, head + offset);
                int right = Mathf.Min(size.x, head + offset + cell);
                if (left >= right)
                    continue;
                float position = offset / (float)length;
                Color32 color = GetStreakColor(position);
                for (int row = y; row < Mathf.Min(size.y, y + thickness); row++)
                    Array.Fill(pixels, (Color)color, row * size.x + left, right - left);
            }
        }
        return pixels;
    }

    /// <summary>
    /// Draws stepped, horizontally smeared stars across a deep backing field.
    /// </summary>
    /// <param name="size">The output canvas.</param>
    /// <param name="random">The seeded source of star placement and intensity.</param>
    /// <returns>The complete starfield layer.</returns>
    /// <param name="transparent">Whether only the motion streaks should be saved.</param>
    private static Color[] DrawStarfield(
        Vector2Int size,
        System.Random random,
        bool transparent = false
    )
    {
        Color[] pixels = new Color[size.x * size.y];
        if (!transparent)
            Array.Fill(pixels, new Color(0.009f, 0.011f, 0.027f));
        int cell = GetEffectCellSize(size);
        for (int index = 0; index < 42; index++)
        {
            int head = random.Next(size.x) / cell * cell;
            int y = random.Next(size.y) / cell * cell;
            int length = Mathf.Max(
                cell,
                Mathf.RoundToInt(size.x * RandomBetween(random, 0.035f, 0.34f)) / cell * cell
            );
            bool bright = index < 16;
            for (int offset = 0; offset < length && head + offset < size.x; offset += cell)
            {
                float position = offset / (float)length;
                Color color =
                    bright && position < 0.28f ? new Color(0.65f, 0.76f, 0.9f)
                    : position < 0.52f ? new Color(0.25f, 0.36f, 0.59f)
                    : new Color(0.08f, 0.12f, 0.28f);
                if (transparent)
                    color.a = Mathf.Lerp(0.62f, 0.12f, position);
                int width = Mathf.Min(cell, size.x - head - offset);
                for (int row = y; row < Mathf.Min(size.y, y + Mathf.Max(1, cell / 2)); row++)
                    Array.Fill(pixels, color, row * size.x + head + offset, width);
            }
        }
        return pixels;
    }

    /// <summary>
    /// Selects separated points around the hull and draws opaque, high-contrast fire lobes.
    /// </summary>
    /// <param name="subject">The fitted ship artwork.</param>
    /// <param name="size">The output canvas.</param>
    /// <param name="bounds">The silhouette bounds.</param>
    /// <param name="random">The seeded source of impact positions and shapes.</param>
    /// <returns>A transparent impact layer aligned to the ship artwork.</returns>
    /// <param name="pieces">Receives each explosion and its sparks as an editable layer.</param>
    private static Color[] DrawDamage(
        Color[] subject,
        Vector2Int size,
        RectInt bounds,
        System.Random random,
        List<Color[]> pieces = null
    )
    {
        Color[] pixels = new Color[subject.Length];
        const int impacts = 3;
        for (int index = 0; index < impacts; index++)
        {
            float column = (index + RandomBetween(random, 0.40f, 0.86f)) / impacts;
            float height =
                index == 1
                    ? RandomBetween(random, 0.72f, 0.97f)
                    : RandomBetween(random, 0.2f, 0.54f);
            Vector2 desired = new(
                bounds.xMin + column * bounds.width,
                bounds.yMin + height * bounds.height
            );
            Vector2 center = FindClosestHullPixel(subject, size, bounds, desired);
            float canvas = Mathf.Min(size.y, bounds.width);
            float radius =
                canvas
                * (
                    index == 0
                        ? RandomBetween(random, 0.12f, 0.17f)
                        : RandomBetween(random, 0.18f, 0.24f)
                );
            float inset = radius * 1.48f + GetEffectCellSize(size);
            float horizontalInset = Mathf.Min(inset, size.x * 0.5f);
            float verticalInset = Mathf.Min(inset, size.y * 0.5f);
            center.x = Mathf.Clamp(center.x, horizontalInset, size.x - horizontalInset);
            center.y = Mathf.Clamp(center.y, verticalInset, size.y - verticalInset);
            float phase = RandomBetween(random, 0, 6.283f);
            Color[] impact = pieces == null ? pixels : new Color[pixels.Length];
            DrawImpact(impact, size, center, radius, phase);
            for (int spark = 0; spark < 3; spark++)
            {
                float angle = RandomBetween(random, 0, 6.283f);
                float distance = radius * RandomBetween(random, 0.7f, 1.05f);
                Vector2 ember = center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * distance;
                DrawImpact(
                    impact,
                    size,
                    ember,
                    radius * RandomBetween(random, 0.15f, 0.27f),
                    angle
                );
            }
            pieces?.Add(impact);
        }
        return pixels;
    }

    /// <summary>
    /// Finds an opaque hull position near the desired impact point.
    /// </summary>
    /// <param name="subject">The fitted ship artwork.</param>
    /// <param name="size">The output canvas.</param>
    /// <param name="bounds">The silhouette bounds.</param>
    /// <param name="desired">The sampled position within the hull bounds.</param>
    /// <returns>The closest visible hull pixel.</returns>
    private static Vector2 FindClosestHullPixel(
        Color[] subject,
        Vector2Int size,
        RectInt bounds,
        Vector2 desired
    )
    {
        float bestDistance = float.PositiveInfinity;
        Vector2 closest = desired;
        for (int y = bounds.yMin; y < bounds.yMax; y++)
        for (int x = bounds.xMin; x < bounds.xMax; x++)
        {
            if (subject[y * size.x + x].a < 0.35f)
                continue;
            Vector2 point = new(x, y);
            float distance = (point - desired).sqrMagnitude;
            if (distance >= bestDistance)
                continue;
            bestDistance = distance;
            closest = point;
        }
        return closest;
    }

    /// <summary>
    /// Rasterizes a color-banded, irregular blast onto the damage layer.
    /// </summary>
    /// <param name="pixels">The destination effect layer.</param>
    /// <param name="size">The output canvas.</param>
    /// <param name="center">The impact position.</param>
    /// <param name="radius">The nominal pixel radius.</param>
    /// <param name="phase">The orientation of the irregular rim.</param>
    private static void DrawImpact(
        Color[] pixels,
        Vector2Int size,
        Vector2 center,
        float radius,
        float phase
    )
    {
        int cell = GetEffectCellSize(size);
        int left = Mathf.Max(0, Mathf.FloorToInt(center.x - 1.6f * radius) / cell * cell);
        int bottom = Mathf.Max(0, Mathf.FloorToInt(center.y - 1.6f * radius) / cell * cell);
        for (int y = bottom; y < size.y && y <= center.y + 1.6f * radius; y += cell)
        for (int x = left; x < size.x && x <= center.x + 1.6f * radius; x += cell)
        {
            float dx = GetCellCenter(x, cell) - center.x;
            float dy = GetCellCenter(y, cell) - center.y;
            float angle = Mathf.Atan2(dy, dx);
            float rim =
                1f + 0.21f * Mathf.Sin(angle * 3f + phase) + 0.11f * Mathf.Sin(angle * 5f - phase);
            float distance = Mathf.Sqrt(dx * dx + dy * dy) / (radius * rim);
            if (distance >= 1.1f)
                continue;
            Color color = distance switch
            {
                < 0.27f => _fireCore,
                < 0.49f => _fireGold,
                < 0.68f => _fireOrange,
                < 0.88f => _fireRed,
                _ => _fireShadow,
            };
            for (int row = y; row < Mathf.Min(size.y, y + cell); row++)
            for (int column = x; column < Mathf.Min(size.x, x + cell); column++)
            {
                int index = row * size.x + column;
                pixels[index] = Over(pixels[index], color);
            }
        }
    }

    /// <summary>
    /// Chooses a pixel cell size that keeps the same detail level across output sizes.
    /// </summary>
    /// <param name="size">The effect canvas.</param>
    /// <returns>The width of one effect cell in output pixels.</returns>
    private static int GetEffectCellSize(Vector2Int size) =>
        Mathf.Max(1, Mathf.RoundToInt(size.y / 22f));

    /// <summary>
    /// Finds the sample coordinate shared by every pixel in an effect cell.
    /// </summary>
    /// <param name="coordinate">The output coordinate.</param>
    /// <param name="cell">The cell width in output pixels.</param>
    /// <returns>The center of the containing effect cell.</returns>
    private static float GetCellCenter(int coordinate, int cell) =>
        (coordinate / cell + 0.5f) * cell;

    /// <summary>
    /// Composites one straight-alpha foreground pixel over a background pixel.
    /// </summary>
    /// <param name="background">The existing lower layer.</param>
    /// <param name="foreground">The new upper layer.</param>
    /// <returns>The composited straight-alpha pixel.</returns>
    private static Color Over(Color background, Color foreground)
    {
        float retained = background.a * (1f - foreground.a);
        float alpha = foreground.a + retained;
        if (alpha <= 0f)
            return Color.clear;
        Color pixel = (foreground * foreground.a + background * retained) / alpha;
        pixel.a = alpha;
        return pixel;
    }

    /// <summary>
    /// Samples a continuous range using the supplied variation stream.
    /// </summary>
    /// <param name="random">The seeded source of variation.</param>
    /// <param name="minimum">The lower bound.</param>
    /// <param name="maximum">The upper bound.</param>
    /// <returns>A value in the specified range.</returns>
    private static float RandomBetween(System.Random random, float minimum, float maximum) =>
        minimum + (float)random.NextDouble() * (maximum - minimum);

    /// <summary>
    /// Uploads one complete canvas into a readable texture for encoding or preview.
    /// </summary>
    /// <param name="size">The canvas dimensions.</param>
    /// <param name="pixels">The bottom-up pixel buffer.</param>
    /// <returns>A caller-owned texture.</returns>
    private static Texture2D CreateTexture(Vector2Int size, Color[] pixels)
    {
        Texture2D texture = new(size.x, size.y, TextureFormat.RGBA32, false);
        texture.SetPixels(pixels);
        texture.Apply(false, false);
        return texture;
    }

    /// <summary>
    /// Releases a generated texture in either the editor or the built application.
    /// </summary>
    /// <param name="texture">The texture to release, if allocated.</param>
    private static void Release(Texture2D texture)
    {
        if (texture == null)
            return;
        if (Application.isPlaying)
            UnityEngine.Object.Destroy(texture);
        else
            UnityEngine.Object.DestroyImmediate(texture);
    }
}

/// <summary>
/// Persists the editable portion of a generated state image alongside the PNG.
/// </summary>
[Serializable]
internal sealed class ContentEditorArtworkRecipe
{
    internal const int CurrentVersion = 7;
    public int Version = CurrentVersion;
    public string OutputMember;
    public int Seed;
    public int NextAddition;
    public int Variant;
    public int FacilityClearance = -1;
    public bool UsesExisting;
    public bool VaryShipWakes;
    public string[] ExistingSources;
    public string[] TravelPatterns;
    public string[] TravelSubjects;
    internal bool HasStoredSources => ExistingSources is { Length: > 0 };
    public List<ContentEditorArtworkLayer> Layers = new();

    /// <summary>
    /// Reads one artwork sidecar and isolates the sources belonging to its output.
    /// </summary>
    /// <param name="json">The metadata stored beside this output's PNG.</param>
    /// <param name="member">The output field that owns the PNG.</param>
    /// <param name="index">The output's position in legacy recipes.</param>
    /// <param name="count">The number of outputs in the legacy recipe.</param>
    /// <param name="shipEffects">Whether the original ship recipe format is supported.</param>
    /// <returns>The independent draft, or null when the PNG must be opened as imported layers.</returns>
    internal static ContentEditorArtworkRecipe ReadForOutput(
        string json,
        string member,
        int index,
        int count,
        bool shipEffects
    )
    {
        if (string.IsNullOrWhiteSpace(json))
            return null;
        ContentEditorArtworkRecipe recipe;
        try
        {
            recipe = JsonUtility.FromJson<ContentEditorArtworkRecipe>(json);
        }
        catch (ArgumentException)
        {
            return null;
        }
        if (
            recipe?.Layers == null
            || recipe.Version < (shipEffects ? 1 : 2)
            || recipe.Version > CurrentVersion
        )
            return null;
        if (!string.IsNullOrEmpty(recipe.OutputMember))
            return
                recipe.OutputMember == member
                && (!recipe.HasStoredSources || recipe.ExistingSources.Length == 1)
                ? recipe
                : null;
        if (recipe.HasStoredSources && recipe.ExistingSources.Length != count)
            return null;

        recipe.OutputMember = member;
        recipe.ExistingSources = SelectSource(recipe.ExistingSources, index, count);
        recipe.TravelPatterns = SelectSource(recipe.TravelPatterns, index, count);
        recipe.TravelSubjects = SelectSource(recipe.TravelSubjects, index, count);
        return recipe;
    }

    /// <summary>
    /// Retains just the legacy artwork source for this output.
    /// </summary>
    /// <param name="sources">The previous collection of sources.</param>
    /// <param name="index">The output's original source slot.</param>
    /// <param name="count">The expected number of slots.</param>
    /// <returns>The output's stored source, or null if it was never recorded.</returns>
    private static string[] SelectSource(string[] sources, int index, int count) =>
        sources?.Length == count ? new[] { sources[index] } : null;
}

/// <summary>
/// Identifies a generated source layer or seeded extra effect and its normalized displacement.
/// </summary>
[Serializable]
internal struct ContentEditorArtworkLayer
{
    public int Source;
    public int Seed;
    public bool MeasuredStreaks;
    public bool VisibleStreakGroup;
    public bool Hidden;
    public string Name;
    public Vector2 Offset;
}
