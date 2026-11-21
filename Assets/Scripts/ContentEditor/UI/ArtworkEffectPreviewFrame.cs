using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// Describes the visible texture and position of one editable effect.
/// </summary>
internal readonly struct ArtworkEffectLayerPreview
{
    internal int Index { get; }
    internal string Name { get; }
    internal Texture2D Texture { get; }
    internal Rect Bounds { get; }
    internal Vector2 Offset { get; }
    internal bool Hidden { get; }
    internal bool Locked { get; }
    internal int? ScalePercent { get; }

    /// <summary>
    /// Captures a layer's position and artwork in one preview.
    /// </summary>
    /// <param name="index">The source draft index.</param>
    /// <param name="name">The displayed layer name.</param>
    /// <param name="texture">The borrowed cropped effect.</param>
    /// <param name="bounds">Its current normalized canvas rectangle.</param>
    /// <param name="offset">The layer's authored movement from its original position.</param>
    /// <param name="hidden">Whether the layer is excluded from the exported card.</param>
    /// <param name="locked">Whether the layer represents fixed composition artwork.</param>
    /// <param name="scalePercent">The current scale percentage for a resizable effect.</param>
    internal ArtworkEffectLayerPreview(
        int index,
        string name,
        Texture2D texture,
        Rect bounds,
        Vector2 offset = default,
        bool hidden = false,
        bool locked = false,
        int? scalePercent = null
    )
    {
        Index = index;
        Name = name;
        Texture = texture;
        Bounds = bounds;
        Offset = offset;
        Hidden = hidden;
        Locked = locked;
        ScalePercent = scalePercent;
    }
}

/// <summary>
/// Describes a layered preview for one output size.
/// </summary>
internal readonly struct ArtworkEffectPreviewFrame
{
    internal string Caption { get; }
    internal Texture2D BaseImage { get; }
    internal Texture2D SubjectOverlay { get; }
    internal IReadOnlyList<ArtworkEffectLayerPreview> Layers { get; }
    internal Vector2Int Size => new(BaseImage.width, BaseImage.height);
    internal bool IsMissing { get; }

    /// <summary>
    /// Captures the layer stack for one output size.
    /// </summary>
    /// <param name="caption">The output's label.</param>
    /// <param name="baseImage">The fixed gameplay background.</param>
    /// <param name="subjectOverlay">The optional unit drawn above the transit effects.</param>
    /// <param name="layers">The effects in back-to-front order, including any used only by another output.</param>
    /// <param name="isMissing">Whether the pack has no assigned image for this output.</param>
    internal ArtworkEffectPreviewFrame(
        string caption,
        Texture2D baseImage,
        Texture2D subjectOverlay,
        IReadOnlyList<ArtworkEffectLayerPreview> layers,
        bool isMissing = false
    )
    {
        Caption = caption;
        BaseImage = baseImage;
        SubjectOverlay = subjectOverlay;
        Layers = layers.Where(layer => layer.Texture != null).ToArray();
        IsMissing = isMissing;
    }
}
