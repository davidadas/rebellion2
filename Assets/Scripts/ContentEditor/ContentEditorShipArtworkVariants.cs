using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

/// <summary>
/// Copies the active effect across one ship type’s gameplay cards.
/// </summary>
internal sealed class ContentEditorShipArtworkVariants
{
    private readonly string member;
    private readonly Vector2Int size;
    private readonly ContentEditorArtworkEffectStyle style;
    private readonly string recipe;
    private readonly Dictionary<string, byte[]> sources = new(StringComparer.Ordinal);

    /// <summary>
    /// Captures the unmasked effect layers without copying the source ship or its backdrop.
    /// </summary>
    /// <param name="field">The destination card slot for this ship.</param>
    /// <param name="frame">The active card frame that owns the currently edited layer pixels.</param>
    /// <param name="draft">The current layer order, visibility, and transforms.</param>
    internal ContentEditorShipArtworkVariants(
        ContentPackEditorField field,
        ContentEditorArtworkEffectGenerator.EditableFrame frame,
        ContentEditorArtworkRecipe draft
    )
    {
        member = field.MemberName;
        if (
            member
                is not (
                    "InTransitImagePath"
                    or "InTransitSmallImagePath"
                    or "BattleResultInTransitImagePath"
                    or "DamagedImagePath"
                    or "DamagedSmallImagePath"
                    or "BattleResultDamagedImagePath"
                )
            || field.Record.Kind is not ("CapitalShip" or "Starfighter")
            || !ContentEditorArtworkEffectGenerator.TryGetStyle(
                field.Record,
                member.Contains("InTransit", StringComparison.Ordinal)
                    ? ContentEditorArtworkEffect.Travel
                    : ContentEditorArtworkEffect.Damage,
                out style
            )
        )
            throw new ArgumentException("Choose ship damage or hyperspace artwork.", nameof(field));
        if (!ContentPackEditorFieldMetadata.TryGetExpectedImageDimensions(field, out size))
            throw new ArgumentException("The card has no configured output size.", nameof(field));
        var portable = new ContentEditorArtworkRecipe
        {
            Version = ContentEditorArtworkRecipe.CurrentVersion,
            OutputMember = member,
            Seed = draft.Seed,
            NextAddition = draft.NextAddition,
            Variant = draft.Variant,
            VaryShipWakes = draft.VaryShipWakes,
        };
        foreach (ContentEditorArtworkLayer original in draft.Layers)
        {
            var layer = original;
            if (layer.Role == ContentEditorArtworkLayerRole.Effect)
            {
                byte[] png = frame.CaptureLayerSource(layer, size);
                if (png == null)
                    continue;
                layer.ImageSource = ContentPackEditorStorage.GetArtworkLayerSourceAddress(
                    png,
                    ".png"
                );
                sources.TryAdd(layer.ImageSource, png);
                layer.Source = -1;
            }
            portable.Layers.Add(layer);
        }
        recipe = JsonUtility.ToJson(portable);
    }

    /// <summary>
    /// Prepares every output before writing the complete batch as one undoable pack edit.
    /// </summary>
    /// <param name="project">The working pack containing this ship.</param>
    /// <param name="source">The only ship definition whose card images may change.</param>
    /// <param name="templates">The captured small, large, and battle-report effects.</param>
    /// <param name="resolve">Resolves this ship's normal image for each card.</param>
    /// <param name="background">Resolves this ship's gameplay backdrop for each card.</param>
    /// <param name="cancellationToken">Cancels preparation before any pack files are changed.</param>
    /// <returns>The number of this ship's card images updated.</returns>
    internal static async Task<int> ApplyAsync(
        ContentPackEditorProject project,
        ContentPackEditorRecord source,
        IReadOnlyList<ContentEditorShipArtworkVariants> templates,
        Func<ContentPackEditorField, string> resolve,
        Func<ContentPackEditorField, (Texture2D Texture, Rect Uv)> background,
        CancellationToken cancellationToken = default
    )
    {
        project.RequireOwnedRecord(source);
        if (source.Kind is not ("CapitalShip" or "Starfighter"))
            throw new ArgumentException("Choose a capital ship or starfighter.", nameof(source));
        var images = new List<(ContentPackEditorField Field, byte[] Png, string Recipe)>();
        foreach (ContentEditorShipArtworkVariants template in templates)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ContentPackEditorField field = source.GetField(template.member);
            if (
                !ContentPackEditorFieldMetadata.TryGetExpectedImageDimensions(
                    field,
                    out var expected
                )
                || expected != template.size
            )
                throw new InvalidOperationException(
                    "The card artwork has a different output size."
                );
            string subjectMember =
                template.member.StartsWith("BattleResult", StringComparison.Ordinal)
                    ? "BattleResultImagePath"
                : template.member.Contains("Small", StringComparison.Ordinal)
                    ? "SmallDisplayImagePath"
                : "DisplayImagePath";
            string subject =
                resolve(source.GetField(subjectMember))
                ?? resolve(source.GetField("SmallDisplayImagePath"))
                ?? resolve(source.GetField("DisplayImagePath"));
            var backdrop = background(field);
            using (
                var frame = ContentEditorArtworkEffectGenerator.CreateEditableFrame(
                    subject,
                    expected,
                    template.style,
                    0,
                    source.Kind == "CapitalShip" && subjectMember == "DisplayImagePath",
                    backdrop.Texture,
                    loadLayerSource: address => template.sources.GetValueOrDefault(address),
                    backdropUv: backdrop.Texture == null ? null : backdrop.Uv
                )
            )
            {
                var draft = JsonUtility.FromJson<ContentEditorArtworkRecipe>(template.recipe);
                images.Add((field, frame.Bake(draft.Layers), template.recipe));
            }
            // Yield after disposal so the runtime can release this output's temporary textures.
            await Task.Yield();
        }
        cancellationToken.ThrowIfCancellationRequested();
        using (project.BeginEdit("Apply effects to all cards"))
        {
            foreach (
                var image in templates
                    .SelectMany(template => template.sources)
                    .GroupBy(pair => pair.Key)
                    .Select(group => group.First())
            )
                ContentPackEditorStorage.StoreArtworkLayerSource(project, image.Key, image.Value);
            foreach (var image in images)
            {
                string address = ContentPackEditorStorage.WriteGeneratedPng(
                    project,
                    image.Field,
                    image.Png,
                    preservePrevious: true,
                    layerRecipe: image.Recipe
                );
                project.SetField(image.Field, address);
            }
        }
        return images.Count;
    }
}
