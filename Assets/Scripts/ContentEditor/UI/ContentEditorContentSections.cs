using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;
using Rebellion.Game.Missions;

/// <summary>
/// Projects related authored content into task-oriented inspector and navigation sections.
/// </summary>
internal static class ContentEditorContentSections
{
    internal const string FactionMissionPresentationPrefix = "mission-presentation/";

    internal static readonly IReadOnlyList<string> FactionMissionIconKeys =
        ContentPackEditorFieldSchema
            .MissionIDs.Where(id => id != MissionTypeIDs.Research)
            .Select(id => MissionIconKeys.GetMissionIconKey(id))
            .Concat(
                new[] { MissionIconKeys.ResearchShipDesign, MissionIconKeys.ResearchTroopTraining }
            )
            .OrderBy(key => key, StringComparer.Ordinal)
            .ToArray();

    private static readonly string[] _factionArtworkDefaultMembers =
    {
        nameof(FactionArtworkDefaults.OfficerBackgroundImagePath),
        nameof(FactionArtworkDefaults.CapitalShipBackgroundImagePath),
        nameof(FactionArtworkDefaults.StarfighterBackgroundImagePath),
    };

    private static readonly string[] _factionColorMembers =
    {
        nameof(FactionColorScheme.PrimaryColorHex),
        nameof(FactionColorScheme.HighlightTextColorHex),
        nameof(FactionColorScheme.SelectionColorHex),
        nameof(FactionColorScheme.ActiveWindowTitleColorHex),
        nameof(FactionColorScheme.InactiveWindowTitleColorHex),
        nameof(FactionColorScheme.ActiveBuildingPaneColorHex),
        nameof(FactionColorScheme.InactiveBuildingPaneColorHex),
        nameof(FactionColorScheme.PlanetMarkerColorHex),
    };

    internal static readonly string[] GenerationSections =
    {
        "Galaxy",
        "Officers",
        "Buildings",
        "Units",
        "Resources",
        "Support",
        "Balance",
    };

    internal static readonly string[] FactionArtworkCategories =
    {
        "Advisor",
        "Combat",
        "Defense window",
        "Encyclopedia",
        "Facilities and construction",
        "Fleet window",
        "Galactic information",
        "Messages",
        "Missions",
        "Planet sector",
        "Shell and dialogs",
        "Status window",
        "Strategy view",
    };

    internal const string NeutralThemePageKey = "factions/neutral-theme";

    /// <summary>
    /// Separates faction authoring tasks while retaining their existing XML documents.
    /// </summary>
    /// <param name="field">The faction field or associated reference article field.</param>
    /// <param name="neutral">Whether the field belongs to the neutral theme.</param>
    /// <returns>The inspector tab containing the field.</returns>
    internal static string FactionSection(ContentPackEditorField field, bool neutral = false) =>
        field.Record.Kind == "EncyclopediaEntry" ? "Encyclopedia"
        : IsFactionBackgroundField(field) ? "Default backgrounds"
        : field.Group == nameof(FactionTheme.ColorScheme) ? "Colors"
        : neutral
            ? IsFactionInterfaceField(field, neutral: true) ? "Interfaces"
                : "Advanced"
        : IsFactionArtworkField(field) || IsAdvisorPlacementField(field) ? "Interfaces"
        : field.IsMediaDirectory ? "Advanced"
        : field.Record.Element.Element("FactionDataPath") != null ? "Advanced"
        : field.MemberName == "DisallowedMissionTypeIDs" ? "Rules"
        : field.Group switch
        {
            "Settings" => "Rules",
            "ShipNamePools" => "Ship names",
            "Messages" => "Messages",
            _ => "General",
        };

    /// <summary>
    /// Identifies discrete image artwork authored by a faction theme.
    /// </summary>
    /// <param name="field">A theme property.</param>
    /// <param name="neutral">Whether the field belongs to the pack's neutral theme.</param>
    /// <returns>Whether the property is editable standalone faction artwork.</returns>
    internal static bool IsFactionArtworkField(
        ContentPackEditorField field,
        bool neutral = false
    ) =>
        field.Record.Kind == "FactionTheme"
        && field.MediaType == ContentPackEditorMediaType.Image
        && !field.IsMediaDirectory
        && (
            neutral
            || field.Group != nameof(FactionTheme.MissionIcons)
            || !MissionIconKeys.UsesSharedArtwork(
                field
                    .XmlElement.Ancestors(nameof(MissionIconTheme))
                    .FirstOrDefault()
                    ?.Element(nameof(MissionIconTheme.Key))
                    ?.Value
            )
        );

    /// <summary>
    /// Identifies faction backgrounds retained for unit artwork workflows.
    /// </summary>
    /// <param name="field">A faction-theme image field.</param>
    /// <returns>Whether the field belongs to the background editor instead of interface theming.</returns>
    internal static bool IsFactionBackgroundField(ContentPackEditorField field) =>
        IsFactionArtworkField(field) && field.Group == nameof(FactionTheme.ArtworkDefaults);

    /// <summary>
    /// Identifies a keyed large or small mission icon in the owning faction theme.
    /// </summary>
    /// <param name="field">The editable media field.</param>
    /// <returns>True for a mission icon field, including an optional unassigned image.</returns>
    internal static bool IsFactionMissionIcon(ContentPackEditorField field) =>
        IsFactionArtworkField(field) && field.Group == nameof(FactionTheme.MissionIcons);

    /// <summary>
    /// Projects both icon slots for a mission, creating missing XML only when a value is assigned.
    /// </summary>
    /// <param name="theme">The faction theme that owns the icon set.</param>
    /// <param name="key">The mission icon key read by the strategy windows.</param>
    /// <returns>The large and small image fields.</returns>
    internal static IReadOnlyList<ContentPackEditorField> FactionMissionIconFields(
        ContentPackEditorRecord theme,
        string key
    )
    {
        XElement icon = theme
            .Element.Element(nameof(FactionTheme.MissionIcons))
            ?.Element(nameof(MissionIconSetTheme.Icons))
            ?.Elements(nameof(MissionIconTheme))
            .FirstOrDefault(item => item.Element(nameof(MissionIconTheme.Key))?.Value == key);
        XElement owner =
            icon
            ?? new XElement(
                nameof(MissionIconTheme),
                new XElement(nameof(MissionIconTheme.Key), key)
            );
        return new[]
        {
            MissionIconField(
                theme,
                owner,
                nameof(MissionIconTheme.LargeImagePath),
                "Mission setup icon"
            ),
            MissionIconField(
                theme,
                owner,
                nameof(MissionIconTheme.SmallImagePath),
                "Mission list icon"
            ),
        };
    }

    /// <summary>
    /// Binds one icon to its theme and prepares any missing parent elements for the first edit.
    /// </summary>
    /// <param name="theme">The editable faction theme.</param>
    /// <param name="icon">The existing or pending keyed icon entry.</param>
    /// <param name="member">The serialized image member.</param>
    /// <param name="caption">The display label for this use of the icon.</param>
    /// <returns>The editable image field.</returns>
    private static ContentPackEditorField MissionIconField(
        ContentPackEditorRecord theme,
        XElement icon,
        string member,
        string caption
    )
    {
        XElement element = icon.Element(member) ?? new XElement(member);
        return new ContentPackEditorField(
            theme,
            element,
            caption,
            attach: () =>
            {
                if (icon.Parent == null)
                {
                    XElement set = theme.Element.Element(nameof(FactionTheme.MissionIcons));
                    if (set == null)
                        theme.Element.Add(set = new XElement(nameof(FactionTheme.MissionIcons)));
                    XElement icons = set.Element(nameof(MissionIconSetTheme.Icons));
                    if (icons == null)
                        set.Add(icons = new XElement(nameof(MissionIconSetTheme.Icons)));
                    icons.Add(icon);
                }
                if (element.Parent == null)
                    icon.Add(element);
            },
            valueTypeOverride: typeof(string),
            groupOverride: nameof(FactionTheme.MissionIcons),
            caption: caption,
            parentOverride: icon
        );
    }

    /// <summary>
    /// Identifies faction-theme images that are rendered by an interface surface.
    /// </summary>
    /// <param name="field">A faction-theme image field.</param>
    /// <param name="neutral">Whether the neutral theme can edit shared artwork.</param>
    /// <returns>Whether the field belongs to the interface browser.</returns>
    internal static bool IsFactionInterfaceField(
        ContentPackEditorField field,
        bool neutral = false
    ) =>
        IsAdvisorPlacementField(field)
        || IsFactionArtworkField(field, neutral)
            && !IsFactionBackgroundField(field)
            && !IsUnusedUnitTileField(field)
            && !IsRemovedMixedPlanetMarker(field)
            && (neutral || IsFactionOwnedArtworkAddress(field.Value));

    /// <summary>
    /// Identifies a coordinate stored in one of the strategy advisor's placement rectangles.
    /// </summary>
    /// <param name="field">The theme field to inspect.</param>
    /// <returns>Whether this coordinate can be edited on the strategy surface.</returns>
    internal static bool IsAdvisorPlacementField(ContentPackEditorField field) =>
        field.Record.Kind == "FactionTheme"
        && field.MemberName is nameof(SourceRectLayout.X) or nameof(SourceRectLayout.Y)
        && field.XmlElement.Parent?.Name.LocalName
            is nameof(StrategyAdvisorTheme.ProtocolSourceLayout)
                or nameof(StrategyAdvisorTheme.DroidSourceLayout)
        && field.XmlElement.Parent.Parent?.Name.LocalName == nameof(FactionTheme.StrategyAdvisor);

    /// <summary>
    /// Reports whether an image address belongs to one faction instead of shared or application
    /// content.
    /// </summary>
    /// <param name="address">The authored content address.</param>
    /// <returns>True only for faction-scoped pack artwork.</returns>
    internal static bool IsFactionOwnedArtworkAddress(string address) =>
        !string.IsNullOrWhiteSpace(address)
        && address.StartsWith("Pack/Factions/", StringComparison.Ordinal);

    /// <summary>
    /// Identifies serialized unit-tile images that have no interface consumer.
    /// </summary>
    /// <param name="field">A faction-theme image field.</param>
    /// <returns>Whether the image is retained only for content compatibility.</returns>
    private static bool IsUnusedUnitTileField(ContentPackEditorField field)
    {
        if (
            !field
                .XmlElement.Ancestors()
                .Any(element => element.Name.LocalName == nameof(PlanetOverlayTheme.UnitTileIcons))
        )
            return false;
        return field.MemberName
            is nameof(UnitTileIcons.FleetTileImagePath)
                or nameof(UnitTileIcons.MissionTileImagePath)
                or nameof(UnitTileIcons.DefaultTileImagePath)
                or nameof(UnitTileIcons.FleetConstructionLargeImagePath);
    }

    /// <summary>
    /// Identifies the legacy mixed-ownership marker that is not exposed by the faction interface
    /// editor.
    /// </summary>
    /// <param name="field">A faction-theme image field.</param>
    /// <returns>Whether the field is the removed mixed planet marker.</returns>
    private static bool IsRemovedMixedPlanetMarker(ContentPackEditorField field) =>
        field.MemberName == nameof(PlanetIcons.Mixed)
        && field
            .XmlElement.Ancestors()
            .Any(element => element.Name.LocalName == nameof(GalaxyBackground.PlanetIcons));

    /// <summary>
    /// Projects all faction image properties while retaining their authored hierarchy.
    /// </summary>
    /// <param name="theme">The effective faction theme.</param>
    /// <param name="neutral">Whether this is the pack's neutral theme.</param>
    /// <returns>The icon fields backed by their existing XML elements.</returns>
    internal static IReadOnlyList<ContentPackEditorField> FactionArtworkFields(
        ContentPackEditorRecord theme,
        bool neutral = false
    )
    {
        if (theme == null)
            return Array.Empty<ContentPackEditorField>();
        List<ContentPackEditorField> fields = theme
            .GetFields()
            .Where(field =>
                IsFactionArtworkField(field, neutral)
                || IsAdvisorPlacementField(field)
                || field.Group == nameof(FactionTheme.ColorScheme)
            )
            .ToList();
        XElement defaults = theme.Element.Element(nameof(FactionTheme.ArtworkDefaults));
        XElement owner = defaults ?? new XElement(nameof(FactionTheme.ArtworkDefaults));
        foreach (string member in neutral ? Array.Empty<string>() : _factionArtworkDefaultMembers)
        {
            if (
                fields.Any(field =>
                    field.Group == nameof(FactionTheme.ArtworkDefaults)
                    && field.MemberName == member
                )
            )
                continue;
            XElement element = defaults?.Element(member) ?? new XElement(member);
            fields.Add(
                new ContentPackEditorField(
                    theme,
                    element,
                    nameof(FactionTheme.ArtworkDefaults) + " / " + member,
                    attach: () =>
                    {
                        if (owner.Parent == null)
                            theme.Element.Add(owner);
                        if (element.Parent == null)
                            owner.Add(element);
                    },
                    valueTypeOverride: typeof(string),
                    groupOverride: nameof(FactionTheme.ArtworkDefaults),
                    caption: ContentPackEditorField.FormatName(member),
                    sectionPath: new[] { "Default backgrounds" },
                    parentOverride: owner
                )
            );
        }
        XElement colors = theme.Element.Element(nameof(FactionTheme.ColorScheme));
        XElement colorOwner = colors ?? new XElement(nameof(FactionTheme.ColorScheme));
        foreach (string member in _factionColorMembers)
        {
            if (
                fields.Any(field =>
                    field.Group == nameof(FactionTheme.ColorScheme) && field.MemberName == member
                )
            )
                continue;
            XElement element = colors?.Element(member) ?? new XElement(member);
            fields.Add(
                new ContentPackEditorField(
                    theme,
                    element,
                    nameof(FactionTheme.ColorScheme) + " / " + member,
                    attach: () =>
                    {
                        if (colorOwner.Parent == null)
                            theme.Element.Add(colorOwner);
                        if (element.Parent == null)
                            colorOwner.Add(element);
                    },
                    valueTypeOverride: typeof(string),
                    groupOverride: nameof(FactionTheme.ColorScheme),
                    caption: ContentPackEditorField.FormatName(member),
                    sectionPath: new[] { "Color scheme" },
                    parentOverride: colorOwner
                )
            );
        }
        return fields;
    }

    /// <summary>
    /// Assigns faction artwork to the game surface where the image is displayed.
    /// </summary>
    /// <param name="field">A faction-theme image field.</param>
    /// <returns>The artwork browser category.</returns>
    internal static string FactionArtworkCategory(ContentPackEditorField field)
    {
        if (field.Group == nameof(FactionTheme.ArtworkDefaults))
            return "Default backgrounds";
        XElement top = field
            .XmlElement.AncestorsAndSelf()
            .FirstOrDefault(element => element.Parent == field.Record.Element);
        string root = top?.Name.LocalName ?? field.MemberName;
        string window =
            root == nameof(FactionTheme.StrategyWindows)
                ? top
                    ?.Elements()
                    .FirstOrDefault(element =>
                        field.XmlElement.AncestorsAndSelf().Contains(element)
                    )
                    ?.Name.LocalName
                : null;
        if (IsAdvisorPlacementField(field))
            return "Strategy view";
        if (root == nameof(FactionTheme.GalaxyBackground))
            return field.MemberName == nameof(GalaxyBackground.DestroyedPlanetIconPath)
                ? "Planet sector"
                : "Strategy view";
        if (root == nameof(FactionTheme.PlanetOverlayTheme))
        {
            if (field.MemberName == nameof(PlanetOverlayTheme.GalaxyHeadquartersImagePath))
                return "Strategy view";
            if (
                field
                    .XmlElement.Ancestors()
                    .Any(element =>
                        element.Name.LocalName == nameof(PlanetOverlayTheme.PlanetOverlayIcons)
                    )
                || field.MemberName
                    is nameof(PlanetOverlayTheme.PlanetSectorHeadquartersImagePath)
                        or nameof(PlanetOverlayTheme.PlanetSectorUprisingImagePath)
            )
                return "Planet sector";
            return field.MemberName == nameof(UnitTileIcons.MissionTileImagePath)
                ? "Missions"
                : "Fleet window";
        }
        if (root == nameof(FactionTheme.PlanetWindowTheme))
            return PlanetWindowArtworkCategory(field);
        if (root == nameof(FactionTheme.MissionIcons))
            return "Missions";
        if (root == nameof(FactionTheme.GalacticInformationDisplay))
            return "Galactic information";
        if (root is nameof(FactionTheme.StrategyAdvisor) or nameof(FactionTheme.StrategyBriefing))
            return "Advisor";
        if (root == nameof(FactionTheme.TacticalHUDLayout))
            return "Strategy view";
        if (root == nameof(FactionTheme.BattleParticipant))
            return "Combat";
        if (root == nameof(FactionTheme.StrategyWindows))
            return window switch
            {
                "Defense" => "Defense window",
                "Fleet" => "Fleet window",
                "Missions" or "MissionCreate" => "Missions",
                "Facility" => "Facilities and construction",
                "Status" => "Status window",
                "Messages" => "Messages",
                "BattleAlert" => "Combat",
                "Encyclopedia" => "Encyclopedia",
                "Finder" => "Galactic information",
                "AdvisorReport" => "Advisor",
                _ => "Other artwork",
            };
        if (root is nameof(FactionTheme.StrategyBookmarkIcons))
            return "Planet sector";
        if (root == nameof(FactionTheme.StrategyContextMenuTheme))
            return "Strategy view";
        if (
            root
            is nameof(FactionTheme.SavedGameSlotIconImagePath)
                or nameof(FactionTheme.ConfirmDialogTheme)
                or nameof(FactionTheme.StrategyCheckboxTheme)
                or nameof(FactionTheme.WindowTitleTheme)
        )
            return "Shell and dialogs";
        return "Other artwork";
    }

    /// <summary>
    /// Routes the historically shared planet-window containers to the concrete strategy window
    /// that presents each image.
    /// </summary>
    /// <param name="field">An image nested beneath the shared planet-window theme.</param>
    /// <returns>The concrete artwork browser category that owns the image.</returns>
    private static string PlanetWindowArtworkCategory(ContentPackEditorField field)
    {
        HashSet<string> ancestors = field
            .XmlElement.Ancestors()
            .Select(element => element.Name.LocalName)
            .ToHashSet(StringComparer.Ordinal);
        if (ancestors.Contains(nameof(PlanetWindowTheme.BuildingsPane)))
            return "Facilities and construction";
        if (ancestors.Contains(nameof(PlanetWindowTheme.FleetsPane)))
            return "Fleet window";
        if (ancestors.Contains(nameof(PlanetWindowTheme.GarrisonPanel)))
            return "Defense window";
        if (ancestors.Contains(nameof(PlanetWindowTheme.MissionsPane)))
            return "Missions";
        return "Other artwork";
    }

    /// <summary>
    /// Collapses serialized containers into one readable heading for the view that owns an image.
    /// </summary>
    /// <param name="field">A faction-theme image field.</param>
    /// <returns>At most one heading naming the related view and state group.</returns>
    internal static IReadOnlyList<string> FactionArtworkSections(ContentPackEditorField field)
    {
        if (IsAdvisorPlacementField(field))
            return new[] { "Advisor placement" };
        XElement top = field
            .XmlElement.AncestorsAndSelf()
            .FirstOrDefault(element => element.Parent == field.Record.Element);
        string root = top?.Name.LocalName;
        if (field.Group == nameof(FactionTheme.ArtworkDefaults))
            return Array.Empty<string>();
        string heading = root switch
        {
            nameof(FactionTheme.GalaxyBackground) => GalaxyArtworkHeading(field),
            nameof(FactionTheme.PlanetOverlayTheme) => PlanetOverlayArtworkHeading(field),
            nameof(FactionTheme.PlanetWindowTheme) => PlanetWindowArtworkHeading(field),
            nameof(FactionTheme.MissionIcons) => MissionIconArtworkHeading(field),
            nameof(FactionTheme.StrategyWindows) => StrategyWindowArtworkHeading(field),
            nameof(FactionTheme.TacticalHUDLayout) => "Strategy view controls",
            _ => CollapseArtworkHeading(field.SectionPath, 0),
        };
        return string.IsNullOrWhiteSpace(heading) ? Array.Empty<string>() : new[] { heading };
    }

    /// <summary>
    /// Names one galaxy-background group without exposing serializer containers.
    /// </summary>
    /// <param name="field">A galaxy-background image.</param>
    /// <returns>The related visual group.</returns>
    private static string GalaxyArtworkHeading(ContentPackEditorField field) =>
        field.XmlElement.Ancestors().Any(element => element.Name.LocalName == "PlanetIcons")
            ? "Planet markers"
            : "Galaxy background";

    /// <summary>
    /// Names one map overlay group by the state set presented together in the game.
    /// </summary>
    /// <param name="field">A planet-overlay image.</param>
    /// <returns>The related map-overlay group.</returns>
    private static string PlanetOverlayArtworkHeading(ContentPackEditorField field)
    {
        XElement overlay = field
            .XmlElement.Ancestors()
            .FirstOrDefault(element => element.Parent?.Name.LocalName == "PlanetOverlayIcons");
        if (overlay != null)
            return ContentPackEditorField.FormatName(overlay.Name.LocalName) + " overlay";
        if (field.XmlElement.Ancestors().Any(element => element.Name.LocalName == "UnitTileIcons"))
            return field.MemberName switch
            {
                "FleetTileImagePath" or "MissionTileImagePath" => "Fleet and mission tiles",
                "FleetListIconImagePath"
                or "FleetListEnrouteIconImagePath"
                or "FleetListDamagedIconImagePath" => "Fleet list states",
                "FleetListSelectionImagePath" or "FleetDetailSelectionImagePath" =>
                    "Fleet selection frames",
                "FleetConstructionSmallImagePath" or "FleetConstructionLargeImagePath" =>
                    "Fleet construction",
                "FleetStarfightersBadgeImagePath"
                or "FleetTroopsBadgeImagePath"
                or "FleetPersonnelBadgeImagePath" => "Fleet badges",
                _ => "Unit tiles",
            };
        return "Map markers";
    }

    /// <summary>
    /// Names one planet-window group using the visible pane instead of its XML nesting.
    /// </summary>
    /// <param name="field">A planet-window image.</param>
    /// <returns>The pane and state group displayed together.</returns>
    private static string PlanetWindowArtworkHeading(ContentPackEditorField field)
    {
        HashSet<string> ancestors = field
            .XmlElement.Ancestors()
            .Select(element => element.Name.LocalName)
            .ToHashSet(StringComparer.Ordinal);
        if (ancestors.Contains("BuildingsPane"))
            return ancestors.Contains("Production") ? "Buildings pane: Production"
                : ancestors.Contains("ConstructionHeader") ? "Buildings pane: Construction"
                : ancestors.Contains("ManufacturingLaneState")
                    ? "Buildings pane: Manufacturing lanes"
                : "Buildings pane";
        if (ancestors.Contains("FleetsPane"))
            return "Fleets pane";
        if (ancestors.Contains("GarrisonPanel"))
            return "Garrison panel: " + NamedStateOwner(field, "GarrisonPanel");
        if (ancestors.Contains("MissionsPane"))
            return "Missions pane: " + NamedStateOwner(field, "MissionTabs");
        return "Planet window";
    }

    /// <summary>
    /// Names one mission icon by its authored mission key.
    /// </summary>
    /// <param name="field">A mission icon image.</param>
    /// <returns>The mission icon library group.</returns>
    private static string MissionIconArtworkHeading(ContentPackEditorField field)
    {
        string key = field
            .XmlElement.Ancestors("MissionIconTheme")
            .Select(element => element.Element("Key")?.Value)
            .FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));
        return string.IsNullOrWhiteSpace(key)
            ? "Mission icon library"
            : "Mission icon library: " + ContentPackEditorField.FormatName(key);
    }

    /// <summary>
    /// Names one strategy-window state group without repeating the selected window category.
    /// </summary>
    /// <param name="field">A strategy-window image.</param>
    /// <returns>The visible control or state group.</returns>
    private static string StrategyWindowArtworkHeading(ContentPackEditorField field)
    {
        IReadOnlyList<string> sections = field.SectionPath;
        string heading = CollapseArtworkHeading(sections, Math.Min(2, sections.Count));
        return string.IsNullOrWhiteSpace(heading) ? "Window presentation" : heading;
    }

    /// <summary>
    /// Finds the nearest named state owner beneath one serialized container.
    /// </summary>
    /// <param name="field">The represented image.</param>
    /// <param name="containerName">The container whose immediate child names the state group.</param>
    /// <returns>The readable state owner.</returns>
    private static string NamedStateOwner(ContentPackEditorField field, string containerName)
    {
        XElement owner = field
            .XmlElement.Ancestors()
            .FirstOrDefault(element => element.Parent?.Name.LocalName == containerName);
        return owner == null
            ? "Presentation"
            : ContentPackEditorField.FormatName(owner.Name.LocalName);
    }

    /// <summary>
    /// Converts remaining serializer sections into one compact view heading.
    /// </summary>
    /// <param name="sections">The authored section path.</param>
    /// <param name="skip">The category containers already represented by the selector.</param>
    /// <returns>The combined visible heading.</returns>
    private static string CollapseArtworkHeading(IReadOnlyList<string> sections, int skip)
    {
        string[] meaningful = sections
            .Skip(skip)
            .Where(section =>
                section
                    is not "Icons"
                        and not "Categories"
                        and not "Tabs"
                        and not "Buttons"
                        and not "Buildings tabs"
                        and not "Mission tabs"
            )
            .ToArray();
        return string.Join(": ", meaningful);
    }

    /// <summary>
    /// Explains the scope represented by one faction artwork browser category.
    /// </summary>
    /// <param name="category">The selected category.</param>
    /// <returns>A short description of the in-game surfaces represented by the category.</returns>
    internal static string FactionArtworkCategoryDescription(string category) =>
        category switch
        {
            "Planet sector" =>
                "Destroyed planets, the composed planet-sector window, map overlays, and bookmarks.",
            "Strategy view" =>
                "The complete strategy interface, including its frame, controls, speed indicators, message notifications, and context menus.",
            "Defense window" =>
                "Personnel, regiment, fighter, shield, and battery tab states plus card backgrounds and selection artwork.",
            "Fleet window" =>
                "Fleet details, banners, unit tabs, construction states, badges, and selection artwork.",
            "Missions" =>
                "Mission map states, mission-window controls, participant tabs, and the large and small mission icon library.",
            "Facilities and construction" =>
                "Facility-window controls, selection artwork, resource nodes, and construction presentation.",
            "Status window" => "Status backgrounds, fleet states, and shipyard presentation.",
            "Messages" => "Message-window categories, controls, and state artwork.",
            "Combat" =>
                "Battle alerts, combatant states, result controls, and victory or defeat presentation.",
            "Encyclopedia" => "Encyclopedia navigation, controls, frames, and category artwork.",
            "Galactic information" =>
                "Galactic-information categories, frames, close controls, and finder artwork.",
            "Advisor" => "Advisor animations, reports, objectives, and briefing artwork.",
            "Shell and dialogs" =>
                "Faction-owned save-slot identity and confirmation-dialog artwork. Shared controls and color-driven masks are configured elsewhere.",
            "Default backgrounds" =>
                "The default officer, capital-ship, and starfighter backgrounds used when a unit does not provide an override.",
            _ => "Interface artwork outside the previews.",
        };

    /// <summary>
    /// Assigns generation settings to their authoring task without changing their XML layout.
    /// </summary>
    /// <param name="field">The generated-world setting.</param>
    /// <returns>The inspector tab that owns it.</returns>
    internal static string GenerationSection(ContentPackEditorField field) =>
        field.Group switch
        {
            "GalaxyClassification" => "Galaxy",
            "Officers" => "Officers",
            "FacilityGeneration" => "Buildings",
            "UnitDeployment" => "Units",
            "PlanetResources" => "Resources",
            "PlanetSupport" => "Support",
            _ => "Balance",
        };

    /// <summary>
    /// Identifies entities whose encyclopedia article is generated from their definition.
    /// </summary>
    /// <param name="record">The selected authored definition.</param>
    /// <returns>Whether the entity supports an encyclopedia inspector.</returns>
    internal static bool HasEncyclopedia(ContentPackEditorRecord record)
    {
        return record?.Kind
            is "Officer"
                or "SpecialForce"
                or "Regiment"
                or "CapitalShip"
                or "Starfighter"
                or "Building"
                or "Planet";
    }

    /// <summary>
    /// Finds the effective article, honoring embedded entity data before standalone fallback data.
    /// </summary>
    /// <param name="record">The entity being edited.</param>
    /// <param name="catalogs">The working project's catalogs.</param>
    /// <returns>The editable fields used by the runtime encyclopedia.</returns>
    internal static IReadOnlyList<ContentPackEditorField> EncyclopediaFields(
        ContentPackEditorRecord record,
        IReadOnlyList<ContentPackEditorCatalog> catalogs
    )
    {
        bool embedded =
            !string.IsNullOrEmpty(record.Element.Element("EncyclopediaImagePath")?.Value)
            || !string.IsNullOrEmpty(record.Element.Element("EncyclopediaDescription")?.Value)
            || record.Element.Element("EncyclopediaStats")?.HasElements == true;
        ContentPackEditorRecord article = embedded
            ? null
            : catalogs
                .SelectMany(catalog => catalog.Records)
                .FirstOrDefault(candidate =>
                    candidate.Kind == "EncyclopediaEntry" && candidate.ID == record.ID
                );
        if (article != null)
            return article.GetFields().Where(field => field.Group != "TypeID").ToArray();
        List<ContentPackEditorField> fields = record
            .GetFields()
            .Where(IsEncyclopediaField)
            .ToList();
        foreach (string member in new[] { "EncyclopediaImagePath", "EncyclopediaDescription" })
            if (!fields.Any(field => field.Group == member))
                fields.Add(record.GetField(member));
        return fields;
    }

    /// <summary>
    /// Keeps encyclopedia-only properties out of the entity's general and media forms.
    /// </summary>
    /// <param name="field">The authored property.</param>
    /// <returns>Whether the property belongs to the encyclopedia tab.</returns>
    internal static bool IsEncyclopediaField(ContentPackEditorField field)
    {
        return field.Group.StartsWith("Encyclopedia", StringComparison.Ordinal);
    }
}
