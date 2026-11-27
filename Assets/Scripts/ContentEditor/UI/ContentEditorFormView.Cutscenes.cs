using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed partial class ContentEditorFormView
{
    private ContentEditorVideoStills videoStills;
    private IReadOnlyList<Vector2Int> videoResolutions;

    /// <summary>
    /// Lays out the pack or faction's cutscenes with the shared media controls.
    /// </summary>
    private void RenderCutsceneFields()
    {
        previewPane.SetActive(false);
        videoResolutions ??= new DisplayManager().GetSupportedResolutions();
        float width = fieldScroll.ScrollArea.ViewportWidth;
        const int gap = 24;
        int columns = width >= 640 ? 2 : 1;
        int stillWidth = Mathf.Clamp(
            Mathf.FloorToInt((width - 8 - gap * (columns - 1)) / columns),
            1,
            390
        );
        int stillHeight = Mathf.Max(1, Mathf.RoundToInt(stillWidth * 9f / 16));
        int[] tops = Enumerable.Repeat(4, columns).ToArray();
        int fieldIndex = 0;
        int labelIndex = 0;
        int imageIndex = 0;
        List<ContentEditorVideoStills.Tile> tiles = new();
        Color placeholderColor =
            GetComponentInParent<ContentEditorAppearance>()?.ColorFor(ContentEditorColorRole.Input)
            ?? ContentEditorAppearance.ColorFor(
                ContentEditorColorMode.Dark,
                ContentEditorColorRole.Input
            );
        IEnumerable<ContentPackEditorField> fields =
            selectedPage?.Fields == null
                ? ContentEditorInspectorLayout
                    .GetFields(selectedRecord)
                    .Where(field => field.MediaType == ContentPackEditorMediaType.Video)
                : selectedPage.Fields.Where(field =>
                    ContentEditorContentSections.FactionSection(field)
                    == ContentEditorContentSections.CutscenesSection
                );
        foreach (ContentPackEditorField field in fields)
        {
            int column = fieldIndex % columns;
            int x = 4 + column * (stillWidth + gap);
            int y = tops[column];
            artworkFieldTops[field.XmlElement] = y;
            y += PlaceLabel(field.Caption, labelIndex++, x, y, stillWidth, true, field.HelpText);

            string address = GetCutsceneAddress(field);
            RectInt rect = new(x, y, stillWidth, stillHeight);
            RawImage image = GetArtworkPreviewImage(
                ref imageIndex,
                "VideoStill-" + field.MemberName
            );
            image.uvRect = new Rect(0, 0, 1, 1);
            var source = ResolveVideoStill(address);
            string status =
                source.Url != null ? "Loading still..."
                : address.Length == 0 ? "No video selected"
                : "Video file unavailable";
            image.texture = null;
            image.color = placeholderColor;
            SetFormRect(image.rectTransform, x, y, stillWidth, stillHeight);
            int placeholder = labelIndex++;
            PlaceLabel(status, placeholder, x + 12, y + 8, stillWidth - 24, false);
            TMP_Text message = groupLabels[placeholder];
            if (source.Url != null)
                tiles.Add(new ContentEditorVideoStills.Tile(source, image, message, rect));

            y += stillHeight + 4;
            tops[column] =
                y
                + PlaceField(field, fieldIndex++, x, y, stillWidth, 0, videoPreviewAddress: address)
                + 18;
        }
        if (Application.isPlaying)
        {
            videoStills ??= gameObject.AddComponent<ContentEditorVideoStills>();
            videoStills.Present(tiles);
        }
        fieldScroll.SetContentHeight(tops.Max(), !reflowingFields);
    }

    /// <summary>
    /// Gets the effective video shown by an authored slot.
    /// </summary>
    /// <param name="field">The editable media field.</param>
    /// <returns>The authored or built-in content address, or an empty string when unused.</returns>
    private static string GetCutsceneAddress(ContentPackEditorField field) =>
        !string.IsNullOrWhiteSpace(field.Value) ? field.Value
        : field.Record.Kind != "ContentPack" ? string.Empty
        : field.MemberName switch
        {
            nameof(ContentPackDefinition.StartupIntroVideoPath) =>
                ContentPackDefinition.DefaultStartupIntroVideoPath,
            nameof(ContentPackDefinition.OpeningCrawlVideoPath) =>
                ContentPackDefinition.DefaultOpeningCrawlVideoPath,
            _ => string.Empty,
        };

    /// <summary>
    /// Resolves a local video and identifies replacements at the same address.
    /// </summary>
    /// <param name="address">The effective cutscene address.</param>
    /// <returns>The current file revision, or an empty source for an unavailable clip.</returns>
    private (string Url, DateTime Modified, long Length) ResolveVideoStill(string address)
    {
        if (string.IsNullOrEmpty(address))
            return default;
        try
        {
            string url = assets.GetVideoUrl(address);
            FileInfo file = new(new Uri(url).LocalPath);
            return (url, file.LastWriteTimeUtc, file.Length);
        }
        catch (Exception exception) when (exception is IOException or ArgumentException)
        {
            return default;
        }
    }
}
