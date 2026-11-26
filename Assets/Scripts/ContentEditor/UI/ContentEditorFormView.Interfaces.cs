using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;
using UnityEngine;
using UnityEngine.UI;

public sealed partial class ContentEditorFormView
{
    /// <summary>
    /// Reads the current shared header overrides from the working pack manifest.
    /// </summary>
    /// <returns>The optional authored layout element.</returns>
    private XElement GetPlanetWindowHeaderElement() =>
        catalogs
            .FirstOrDefault(catalog => catalog.CanAddFaction)
            ?.DocumentRecord.Element.Element(
                nameof(ContentPackDefinition.PlanetWindowHeaderLayout)
            );

    /// <summary>
    /// Deserializes the working shared header layout used by the gameplay previews.
    /// </summary>
    /// <param name="element">The optional manifest element.</param>
    /// <returns>The current overrides or null to use the authored defaults.</returns>
    private static PlanetWindowHeaderLayout ReadPlanetWindowHeader(XElement element) =>
        element == null
            ? null
            : ContentDefinitionSerializer.DeserializeText<PlanetWindowHeaderLayout>(
                element.ToString(SaveOptions.DisableFormatting)
            );

    /// <summary>
    /// Edits the pack-wide planet window header over a blank window shell.
    /// </summary>
    private void RenderPlanetWindowHeader()
    {
        previewPane.SetActive(false);
        float width = fieldScroll.ScrollArea.ViewportWidth;
        int label = 0;
        int top =
            12
            + PlaceLabel(
                "Shared by Defense, Fleet, Facilities, and Missions. Click to select, then drag or use the arrow keys or WASD to move.",
                label++,
                4,
                4,
                width - 8,
                false
            );
        string category = ContentEditorArtworkWindowPreview.PlanetHeaderCategory;
        Vector2Int size = artworkWindowPreview.GetPreferredSize(
            category,
            Mathf.Max(1, Mathf.FloorToInt(width - 8)),
            GetInterfacePreviewHeight(top)
        );
        XElement authored = GetPlanetWindowHeaderElement();
        PlanetWindowHeader header = artworkWindowPreview.RenderPlanetHeader(
            assets,
            ReadPlanetWindowHeader(authored)
        );
        if (header != null)
        {
            top = PlaceInterfacePreviewFrames(
                top,
                width,
                ref label,
                (artworkWindowPreview, size, null)
            );
            XElement layout =
                authored ?? new XElement(nameof(ContentPackDefinition.PlanetWindowHeaderLayout));
            Rect rect = ((RectTransform)header.transform).rect;
            RectInt bounds = new(0, 0, Mathf.RoundToInt(rect.width), Mathf.RoundToInt(rect.height));
            BindPlanetWindowHeaderPlacement(
                header.Title,
                layout,
                nameof(PlanetWindowHeaderLayout.TitleOffset),
                bounds
            );
            BindPlanetWindowHeaderPlacement(
                header.Caption,
                layout,
                nameof(PlanetWindowHeaderLayout.CaptionOffset),
                bounds
            );
            List<InterfaceArtworkRow> rows = new();
            AddInterfaceButtonArtwork(
                rows,
                layout,
                "Open Sector",
                header.OpenSector,
                bounds,
                nameof(PlanetWindowHeaderLayout.OpenSectorOffset),
                nameof(PlanetWindowHeaderLayout.OpenSectorImagePath),
                nameof(PlanetWindowHeaderLayout.OpenSectorPressedImagePath),
                relativePosition: true
            );
            AddInterfaceButtonArtwork(
                rows,
                layout,
                "Minimize",
                header.Minimize,
                bounds,
                nameof(PlanetWindowHeaderLayout.MinimizeOffset),
                nameof(PlanetWindowHeaderLayout.MinimizeImagePath),
                nameof(PlanetWindowHeaderLayout.MinimizePressedImagePath),
                relativePosition: true
            );
            AddInterfaceButtonArtwork(
                rows,
                layout,
                "Close",
                header.Close,
                bounds,
                nameof(PlanetWindowHeaderLayout.CloseOffset),
                nameof(PlanetWindowHeaderLayout.CloseImagePath),
                nameof(PlanetWindowHeaderLayout.ClosePressedImagePath),
                relativePosition: true
            );
            top = PlaceInterfaceArtworkRows(rows, top, width, ref label);
        }
        fieldScroll.SetContentHeight(top + 20, !reflowingFields);
    }

    /// <summary>
    /// Enables direct placement for the title graphic or caption within the preview window.
    /// </summary>
    /// <param name="graphic">The rendered header element.</param>
    /// <param name="layout">The pack element that owns the offset.</param>
    /// <param name="member">The element name of the offset.</param>
    /// <param name="bounds">The allowed source window rectangle.</param>
    private void BindPlanetWindowHeaderPlacement(
        Graphic graphic,
        XElement layout,
        string member,
        RectInt bounds
    )
    {
        ContentEditorArtworkEditTarget target =
            graphic.GetComponent<ContentEditorArtworkEditTarget>()
            ?? graphic.gameObject.AddComponent<ContentEditorArtworkEditTarget>();
        target.BindPlacement(CreateWindowControlPlacement(layout, member), bounds);
    }

    /// <summary>
    /// Binds one control's position, attaching its optional layout only when edited.
    /// </summary>
    /// <param name="layout">The pack layout storing the control's position.</param>
    /// <param name="member">The control's position property.</param>
    /// <param name="origin">The source coordinates to use before any edit.</param>
    /// <returns>The fields used by both drag and keyboard moves.</returns>
    private ContentEditorArtworkPlacement CreateWindowControlPlacement(
        XElement layout,
        string member,
        Vector2Int origin = default
    )
    {
        ContentPackEditorRecord record = selectedRecord;
        XElement point =
            layout.Element(member)
            ?? new XElement(
                member,
                new XElement(nameof(SourcePointLayout.X), origin.x),
                new XElement(nameof(SourcePointLayout.Y), origin.y)
            );
        Action attach = () =>
        {
            if (layout.Parent == null)
                record.Element.Add(layout);
            if (point.Parent == null)
                layout.Add(point);
        };
        return new ContentEditorArtworkPlacement(
            ContentPackEditorField.CreateOptional(
                record,
                point,
                nameof(SourcePointLayout.X),
                typeof(int),
                attachOwner: attach
            ),
            ContentPackEditorField.CreateOptional(
                record,
                point,
                nameof(SourcePointLayout.Y),
                typeof(int),
                attachOwner: attach
            )
        );
    }

    /// <summary>
    /// Leaves room below a window preview for its artwork inspector.
    /// </summary>
    /// <param name="top">The top of the preview in the inspector.</param>
    /// <returns>The space available to fit the rendered window.</returns>
    private int GetInterfacePreviewHeight(int top) =>
        Mathf.Max(240, Mathf.FloorToInt(fieldScroll.ScrollArea.ViewportHeight - top - 96));

    /// <summary>
    /// Centers one interface preview or a row of alternative states above the artwork inspector.
    /// </summary>
    /// <param name="top">The top of the preview row.</param>
    /// <param name="width">The available browser width.</param>
    /// <param name="label">The next shared label.</param>
    /// <param name="frames">The rendered surfaces, their fitted dimensions, and optional titles.</param>
    /// <returns>The next position below the preview frames.</returns>
    private int PlaceInterfacePreviewFrames(
        int top,
        float width,
        ref int label,
        params (Component View, Vector2Int Size, string Title)[] frames
    )
    {
        const int gap = 16;
        int rowWidth = frames.Sum(frame => frame.Size.x) + gap * (frames.Length - 1);
        int start = Mathf.Max(4, Mathf.RoundToInt((width - rowWidth) * 0.5f));
        int labelHeight = 0;
        int x = start;
        foreach (var frame in frames)
        {
            if (frame.Title != null)
                labelHeight = Mathf.Max(
                    labelHeight,
                    PlaceLabel(frame.Title, label++, x, top, frame.Size.x, true) + 4
                );
            x += frame.Size.x + gap;
        }
        x = start;
        foreach (var frame in frames)
        {
            UILayout.SetSourceRect(
                (RectTransform)frame.View.transform,
                x,
                top + labelHeight,
                frame.Size.x,
                frame.Size.y
            );
            x += frame.Size.x + gap;
        }
        return top + labelHeight + frames.Max(frame => frame.Size.y) + 8;
    }

    /// <summary>
    /// Reveals the image field represented by an exact gameplay-window preview element.
    /// </summary>
    /// <param name="field">The exact field supplying the clicked rendered image.</param>
    private void OpenWindowArtwork(ContentPackEditorField field)
    {
        if (field == null)
            return;
        if (artworkFieldTops.ContainsKey(field.XmlElement) && FocusField(field))
            return;
        ContentPackEditorField current = selectedPage?.Fields?.FirstOrDefault(candidate =>
            SameField(candidate, field) || SameFactionColor(candidate, field)
        );
        if (current == null)
        {
            AssetEditRequested?.Invoke(field);
            return;
        }
        if (current.Group == nameof(FactionTheme.ColorScheme))
        {
            SectionRequested?.Invoke("Colors");
            FocusColorPicker(current);
            return;
        }
        string category = ContentEditorContentSections.FactionArtworkCategory(
            current,
            selectedPage.Key == ContentEditorContentSections.NeutralThemePageKey
        );
        if (category != selectedFactionArtworkCategory)
        {
            selectedFactionArtworkCategory = category;
            RenderFields(true);
        }
        FocusField(current);
    }

    /// <summary>
    /// Displays the supplied groups and artwork rows using the common interface inspector layout.
    /// </summary>
    /// <param name="rows">The artwork represented by the current page.</param>
    /// <param name="y">The next vertical form position.</param>
    /// <param name="width">The available browser width.</param>
    /// <param name="labelIndex">The next reusable label.</param>
    /// <returns>The next vertical position.</returns>
    private int PlaceInterfaceArtworkRows(
        IEnumerable<InterfaceArtworkRow> rows,
        int y,
        float width,
        ref int labelIndex
    )
    {
        int field = 0;
        int image = 0;
        IReadOnlyList<string> previousSections = Array.Empty<string>();
        foreach (InterfaceArtworkRow row in rows)
        {
            y = Mathf.CeilToInt(
                PlaceSections(row.Sections, previousSections, ref labelIndex, 4, y, width - 8)
            );
            previousSections = row.Sections;
            y = PlaceInterfaceArtworkRow(row, y, width, ref field, ref labelIndex, ref image);
        }
        return y;
    }

    /// <summary>
    /// The caption, asset field and optional in-context artwork for an interface row.
    /// </summary>
    private readonly struct InterfaceArtworkRow
    {
        public string Caption { get; }
        public ContentPackEditorField Field { get; }
        public Graphic LiveGraphic { get; }
        public string BuiltInName { get; }
        public Texture2D StockTexture { get; }
        public IReadOnlyList<string> Sections { get; }

        /// <summary>
        /// Defines one authored asset slot or read-only preview.
        /// </summary>
        /// <param name="caption">The label displayed above the artwork.</param>
        /// <param name="field">The authored media slot, when editable.</param>
        /// <param name="liveGraphic">The graphic supplying the rendered thumbnail.</param>
        /// <param name="builtInName">The artwork name displayed when the slot uses the default.</param>
        /// <param name="stockTexture">A resolved texture to display in the thumbnail.</param>
        /// <param name="sections">The group headings above the slot.</param>
        public InterfaceArtworkRow(
            string caption,
            ContentPackEditorField field = null,
            Graphic liveGraphic = null,
            string builtInName = null,
            Texture2D stockTexture = null,
            IReadOnlyList<string> sections = null
        )
        {
            Caption = caption;
            Field = field;
            LiveGraphic = liveGraphic;
            BuiltInName = builtInName;
            StockTexture = stockTexture;
            Sections = sections ?? Array.Empty<string>();
        }
    }

    /// <summary>
    /// Renders a caption and thumbnail with the standard media field when the artwork is editable.
    /// </summary>
    /// <param name="row">The artwork and optional editing information.</param>
    /// <param name="y">The next vertical form position.</param>
    /// <param name="width">The available form width.</param>
    /// <param name="fieldIndex">The next reusable field view.</param>
    /// <param name="labelIndex">The next reusable label.</param>
    /// <param name="imageIndex">The next reusable thumbnail.</param>
    /// <returns>The next vertical position.</returns>
    private int PlaceInterfaceArtworkRow(
        InterfaceArtworkRow row,
        int y,
        float width,
        ref int fieldIndex,
        ref int labelIndex,
        ref int imageIndex
    )
    {
        ContentPackEditorField field = row.Field;
        if (field != null)
            artworkFieldTops[field.XmlElement] = y;
        y += PlaceLabel(row.Caption, labelIndex++, 4, y, width - 8, false, field?.HelpText);

        bool beside = width >= 900 && field?.IsMediaDirectory == false;
        float previewWidth = beside ? Mathf.Min(300, Mathf.Max(220, width - 640)) : width;
        int maxWidth = beside ? 300 : 360;
        int maxHeight = beside ? 140 : 160;
        int previewBottom = y;
        if (row.LiveGraphic != null)
        {
            int availableWidth = Mathf.Max(1, Mathf.FloorToInt(previewWidth - 8));
            int height = Mathf.Min(maxHeight, 72);
            PlaceLiveArtworkThumbnail(
                field,
                row.LiveGraphic,
                new Rect(4, y, Mathf.Min(availableWidth, 112), height),
                ref imageIndex
            );
            previewBottom += height + 8;
        }
        else if (row.StockTexture != null)
        {
            Texture2D texture = row.StockTexture;
            int availableWidth = Mathf.Max(1, Mathf.FloorToInt(previewWidth - 8));
            float scale = Mathf.Min(
                1f,
                (float)Mathf.Min(availableWidth, maxWidth) / texture.width,
                (float)maxHeight / texture.height
            );
            int imageWidth = Mathf.Max(1, Mathf.RoundToInt(texture.width * scale));
            int imageHeight = Mathf.Max(1, Mathf.RoundToInt(texture.height * scale));
            PlaceArtworkCompositionLayer(
                row.Caption,
                texture,
                default,
                true,
                4,
                y,
                imageWidth,
                imageHeight,
                ref imageIndex
            );
            previewBottom += imageHeight + 8;
        }
        else if (field != null)
        {
            previewBottom = PlaceArtworkImagePreview(
                field,
                4,
                y,
                previewWidth,
                maxWidth,
                maxHeight,
                ref imageIndex,
                ref labelIndex
            );
        }
        else
        {
            previewBottom +=
                PlaceLabel("Image unavailable", labelIndex++, 4, y, width - 8, false) + 8;
        }

        if (field == null)
            return previewBottom + 12;

        float fieldX = beside ? previewWidth + 16 : 0;
        int fieldTop = beside ? y : previewBottom;
        int index = fieldIndex++;
        int fieldHeight = PlaceField(field, index, fieldX, fieldTop, width - fieldX, 0);
        if (row.BuiltInName != null && string.IsNullOrWhiteSpace(field.Value))
            fieldViews[index].ShowBuiltInArtwork(row.BuiltInName);
        return Mathf.Max(previewBottom, fieldTop + fieldHeight) + 12;
    }
}
