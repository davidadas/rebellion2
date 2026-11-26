using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Xml.Linq;
using UnityEngine;

/// <summary>
/// Projects a pack's launch slots and their assigned faction models into editable fields.
/// </summary>
internal static class ContentEditorMainMenuSettings
{
    internal const string PageKey = "interfaces/main-menu";

    /// <summary>
    /// Gets the authored main-menu blocks, using the manifest until a layout exists.
    /// </summary>
    /// <param name="project">The working content pack.</param>
    /// <param name="record">The pack manifest.</param>
    /// <returns>The editable layout or manifest and any authored faction medallion fields.</returns>
    internal static IReadOnlyList<ContentEditorXmlScope> GetXmlScopes(
        ContentPackEditorProject project,
        ContentPackEditorRecord record
    )
    {
        XElement layout = record.Element.Element(nameof(ContentPackDefinition.MainMenuLayout));
        var scopes = new List<ContentEditorXmlScope>
        {
            new(
                record,
                layout ?? record.Element,
                layout == null ? "Pack manifest" : "Main menu layout"
            ),
        };
        ContentPackEditorField[] models = GetLaunchFactions(project)
            .Select(faction => faction.Model)
            .Where(field => field?.XmlElement.Parent != null)
            .ToArray();
        scopes.AddRange(ContentEditorXmlScope.Create(record, models));
        return scopes;
    }

    /// <summary>
    /// Projects a control's artwork fields and placement pair from the pack manifest.
    /// </summary>
    /// <param name="record">The working pack manifest.</param>
    /// <param name="layout">The shared layout element, which can be detached.</param>
    /// <param name="control">The authored control defining which media can be replaced.</param>
    /// <returns>The editable fields for the control.</returns>
    internal static (
        ContentPackEditorField Media,
        ContentPackEditorField Selection,
        ContentEditorArtworkPlacement Placement,
        ContentEditorArtworkPlacement SelectionPlacement
    ) GetControlFields(
        ContentPackEditorRecord record,
        XElement layout,
        MainMenuArtwork.Control control
    )
    {
        XElement entry =
            layout
                .Elements("Control")
                .FirstOrDefault(element => (string)element.Attribute("ID") == control.ID)
            ?? new XElement("Control", new XAttribute("ID", control.ID));
        Action attach = () =>
        {
            if (layout.Parent == null)
                record.Element.Add(layout);
            if (entry.Parent == null)
                layout.Add(entry);
        };
        string mediaMember =
            control.Model != null ? nameof(MainMenuControlLayout.ModelPath)
            : control.CanReplaceImage ? nameof(MainMenuControlLayout.ImagePath)
            : null;
        return (
            mediaMember == null
                ? null
                : ContentPackEditorField.CreateOptional(
                    record,
                    entry,
                    mediaMember,
                    typeof(string),
                    control.Model != null ? "Model"
                        : control.IsRenderedBackdrop ? "Static image override"
                        : "Image",
                    attach
                ),
            control.Selection == null
                ? null
                : ContentPackEditorField.CreateOptional(
                    record,
                    entry,
                    nameof(MainMenuControlLayout.SelectionImagePath),
                    typeof(string),
                    "Selection icon",
                    attach
                ),
            new ContentEditorArtworkPlacement(
                ContentPackEditorField.CreateOptional(
                    record,
                    entry,
                    nameof(MainMenuControlLayout.X),
                    typeof(int),
                    "Horizontal offset",
                    attach
                ),
                ContentPackEditorField.CreateOptional(
                    record,
                    entry,
                    nameof(MainMenuControlLayout.Y),
                    typeof(int),
                    "Vertical offset",
                    attach
                )
            ),
            control.Selection == null
                ? null
                : new ContentEditorArtworkPlacement(
                    ContentPackEditorField.CreateOptional(
                        record,
                        entry,
                        nameof(MainMenuControlLayout.SelectionX),
                        typeof(int),
                        "Selection horizontal offset",
                        attach
                    ),
                    ContentPackEditorField.CreateOptional(
                        record,
                        entry,
                        nameof(MainMenuControlLayout.SelectionY),
                        typeof(int),
                        "Selection vertical offset",
                        attach
                    )
                )
        );
    }

    /// <summary>
    /// Finds the launch factions in the menu's default scenario and resolves their theme fields.
    /// </summary>
    /// <param name="project">The active working pack.</param>
    /// <returns>The launch factions and optional model fields, in visible slot order.</returns>
    internal static (string Name, ContentPackEditorField Model)[] GetLaunchFactions(
        ContentPackEditorProject project
    )
    {
        IReadOnlyList<ContentPackEditorCatalog> catalogs = project.GetCatalogs();
        ContentPackEditorRecord[] records = catalogs
            .SelectMany(catalog => catalog.Records)
            .ToArray();
        IEnumerable<XElement> playable =
            records
                .FirstOrDefault(record =>
                    record.Kind == "Scenario" && record.ID == project.Definition.DefaultScenarioID
                )
                ?.Element.Element(nameof(ContentScenarioDefinition.PlayableFactionIDs))
                ?.Elements()
            ?? Enumerable.Empty<XElement>();
        return playable
            .Take(2)
            .Select(element =>
            {
                string id = element.Value;
                ContentPackEditorRecord faction = records.FirstOrDefault(record =>
                    record.Kind == "Faction"
                    && record.Element.Element("ID")?.Value == id
                    && record.Element.Element("ThemePath") != null
                );
                string themePath = faction?.Element.Element("ThemePath")?.Value;
                ContentPackEditorRecord theme = string.IsNullOrWhiteSpace(themePath)
                    ? null
                    : catalogs
                        .FirstOrDefault(catalog =>
                            catalog.Document.Address == "Pack/" + themePath.Replace('\\', '/')
                        )
                        ?.Records.FirstOrDefault(record => record.Kind == nameof(FactionTheme));
                ContentPackEditorField model =
                    theme == null
                        ? null
                        : ContentPackEditorField.CreateOptional(
                            theme,
                            theme.Element,
                            nameof(FactionTheme.MainMenuMedallionModelPath),
                            typeof(string)
                        );
                return (faction?.Name ?? id, model);
            })
            .ToArray();
    }

    /// <summary>
    /// Projects both launch-slot coordinate pairs from the pack manifest.
    /// </summary>
    /// <param name="record">The pack manifest being edited.</param>
    /// <param name="layout">The shared layout element, which can be detached.</param>
    /// <returns>The editable offsets in slot order.</returns>
    internal static ContentEditorArtworkPlacement[] GetPlacements(
        ContentPackEditorRecord record,
        XElement layout
    )
    {
        return Enumerable
            .Range(0, 2)
            .Select(slot =>
            {
                (string x, string y) = MainMenuLayout.GetLaunchMembers(slot);
                return new ContentEditorArtworkPlacement(
                    ContentPackEditorField.CreateOptional(
                        record,
                        layout,
                        x,
                        typeof(int),
                        "Horizontal offset"
                    ),
                    ContentPackEditorField.CreateOptional(
                        record,
                        layout,
                        y,
                        typeof(int),
                        "Vertical offset"
                    )
                );
            })
            .ToArray();
    }

    /// <summary>
    /// Reads one coordinate pair, treating absent values as the authored default.
    /// </summary>
    /// <param name="placement">The editable coordinate pair.</param>
    /// <returns>The source-pixel offset.</returns>
    internal static Vector2Int GetOffset(ContentEditorArtworkPlacement placement) =>
        new(
            int.TryParse(
                placement.XField.Value,
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out int x
            )
                ? x
                : 0,
            int.TryParse(
                placement.YField.Value,
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out int y
            )
                ? y
                : 0
        );
}
