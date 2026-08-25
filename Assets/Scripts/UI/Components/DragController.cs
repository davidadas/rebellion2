using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Identifies the UI model that initiated a drag operation.
/// </summary>
public sealed class DragRequest
{
    public object Source { get; }

    /// <summary>
    /// Creates a drag request for a non-null source model.
    /// </summary>
    /// <param name="source">The model initiating the drag.</param>
    public DragRequest(object source)
    {
        Source = source ?? throw new ArgumentNullException(nameof(source));
    }
}

/// <summary>
/// Defines the ordered image layers and source-space hotspot of a drag preview.
/// </summary>
public sealed class DragPreview
{
    private readonly IReadOnlyList<DragPreviewImage> images;

    /// <summary>
    /// Gets the immutable image layers in rendering order.
    /// </summary>
    public IReadOnlyList<DragPreviewImage> Images => images;

    /// <summary>
    /// Gets the source-space horizontal pointer coordinate captured with the preview.
    /// </summary>
    public int HotspotX { get; }

    /// <summary>
    /// Gets the source-space vertical pointer coordinate captured with the preview.
    /// </summary>
    public int HotspotY { get; }

    public Texture Texture => images.Count > 0 ? images[0].Texture : null;

    public int Width => images.Count > 0 ? images[0].Bounds.width : 0;

    public int Height => images.Count > 0 ? images[0].Bounds.height : 0;

    public int OffsetX => images.Count > 0 ? HotspotX - images[0].Bounds.x : 0;

    public int OffsetY => images.Count > 0 ? HotspotY - images[0].Bounds.y : 0;

    /// <summary>
    /// Gets whether at least one image layer has a texture and positive dimensions.
    /// </summary>
    public bool HasDrawableImages
    {
        get
        {
            for (int index = 0; index < images.Count; index++)
            {
                DragPreviewImage image = images[index];
                if (image.Texture != null && image.Bounds.width > 0 && image.Bounds.height > 0)
                    return true;
            }

            return false;
        }
    }

    /// <summary>
    /// Creates immutable drag-preview presentation data.
    /// </summary>
    /// <param name="texture">The preview texture.</param>
    /// <param name="width">The source-space width.</param>
    /// <param name="height">The source-space height.</param>
    /// <param name="offsetX">The horizontal pointer offset.</param>
    /// <param name="offsetY">The vertical pointer offset.</param>
    public DragPreview(Texture texture, int width, int height, int offsetX, int offsetY)
        : this(
            new[] { new DragPreviewImage(texture, new RectInt(-offsetX, -offsetY, width, height)) },
            0,
            0
        ) { }

    /// <summary>
    /// Creates immutable drag-preview presentation data from ordered image layers.
    /// </summary>
    /// <param name="images">The image layers in rendering order.</param>
    /// <param name="hotspotX">The source-space horizontal pointer coordinate.</param>
    /// <param name="hotspotY">The source-space vertical pointer coordinate.</param>
    public DragPreview(IReadOnlyList<DragPreviewImage> images, int hotspotX, int hotspotY)
    {
        if (images == null)
            throw new ArgumentNullException(nameof(images));

        this.images = new List<DragPreviewImage>(images).AsReadOnly();
        HotspotX = hotspotX;
        HotspotY = hotspotY;
    }
}

/// <summary>
/// Describes one textured drag-preview layer in source-space coordinates.
/// </summary>
public readonly struct DragPreviewImage
{
    /// <summary>
    /// Gets the displayed texture.
    /// </summary>
    public Texture Texture { get; }

    /// <summary>
    /// Gets the source-space layer bounds.
    /// </summary>
    public RectInt Bounds { get; }

    /// <summary>
    /// Creates one drag-preview image layer.
    /// </summary>
    /// <param name="texture">The displayed texture.</param>
    /// <param name="bounds">The source-space layer bounds.</param>
    public DragPreviewImage(Texture texture, RectInt bounds)
    {
        Texture = texture;
        Bounds = bounds;
    }
}

/// <summary>
/// Owns the candidate, threshold, active state, and preview position of one UI drag flow.
/// </summary>
public sealed class DragController
{
    private readonly int startDistance;
    private DragRequest candidateRequest;
    private DragRequest activeRequest;
    private DragPreview activePreview;
    private int candidateStartX;
    private int candidateStartY;
    private int currentX;
    private int currentY;

    public bool HasCandidate => candidateRequest != null;

    public bool IsDragging => activeRequest != null;

    public DragRequest CandidateRequest => candidateRequest;

    public DragRequest ActiveRequest => activeRequest;

    /// <summary>
    /// Creates a drag controller with a non-negative activation distance.
    /// </summary>
    /// <param name="startDistance">The source-space activation distance.</param>
    public DragController(int startDistance)
    {
        if (startDistance < 0)
            throw new ArgumentOutOfRangeException(nameof(startDistance));

        this.startDistance = startDistance;
    }

    /// <summary>
    /// Starts a potential drag at a source-space pointer position.
    /// </summary>
    /// <param name="request">The drag source request.</param>
    /// <param name="x">The pointer x-coordinate.</param>
    /// <param name="y">The pointer y-coordinate.</param>
    public void StartCandidate(DragRequest request, int x, int y)
    {
        candidateRequest = request ?? throw new ArgumentNullException(nameof(request));
        candidateStartX = x;
        candidateStartY = y;
    }

    /// <summary>
    /// Reports whether a candidate crossed the configured activation distance.
    /// </summary>
    /// <param name="x">The current pointer x-coordinate.</param>
    /// <param name="y">The current pointer y-coordinate.</param>
    /// <returns>True when an active candidate crossed the threshold.</returns>
    public bool HasCandidateDragStarted(int x, int y)
    {
        if (candidateRequest == null)
            return false;

        int deltaX = x - candidateStartX;
        int deltaY = y - candidateStartY;
        return deltaX * deltaX + deltaY * deltaY >= startDistance * startDistance;
    }

    /// <summary>
    /// Promotes the current candidate into an active drag.
    /// </summary>
    /// <param name="preview">The drag-preview presentation.</param>
    /// <param name="x">The current pointer x-coordinate.</param>
    /// <param name="y">The current pointer y-coordinate.</param>
    public void BeginDrag(DragPreview preview, int x, int y)
    {
        if (candidateRequest == null)
            throw new InvalidOperationException("Cannot begin a drag without a candidate.");

        activeRequest = candidateRequest;
        activePreview = preview ?? throw new ArgumentNullException(nameof(preview));
        currentX = x;
        currentY = y;
        ClearCandidate();
    }

    /// <summary>
    /// Updates an active drag's source-space pointer position.
    /// </summary>
    /// <param name="x">The current pointer x-coordinate.</param>
    /// <param name="y">The current pointer y-coordinate.</param>
    /// <returns>True when an active drag was updated.</returns>
    public bool Move(int x, int y)
    {
        if (activeRequest == null)
            return false;

        currentX = x;
        currentY = y;
        return true;
    }

    /// <summary>
    /// Completes an active drag and returns its source request.
    /// </summary>
    /// <param name="x">The final pointer x-coordinate.</param>
    /// <param name="y">The final pointer y-coordinate.</param>
    /// <param name="request">The completed drag request.</param>
    /// <returns>True when an active drag completed.</returns>
    public bool End(int x, int y, out DragRequest request)
    {
        request = null;
        if (activeRequest == null)
            return false;

        currentX = x;
        currentY = y;
        request = activeRequest;
        ClearActive();
        return true;
    }

    /// <summary>
    /// Resolves the active drag preview at its current source-space position.
    /// </summary>
    /// <param name="texture">The preview texture.</param>
    /// <param name="x">The preview's left coordinate.</param>
    /// <param name="y">The preview's top coordinate.</param>
    /// <param name="width">The preview width.</param>
    /// <param name="height">The preview height.</param>
    /// <returns>True when an active drawable preview is available.</returns>
    public bool TryGetPreview(
        out Texture texture,
        out int x,
        out int y,
        out int width,
        out int height
    )
    {
        texture = null;
        x = 0;
        y = 0;
        width = 0;
        height = 0;

        if (activeRequest == null || activePreview == null)
            return false;

        texture = activePreview.Texture;
        x = currentX - activePreview.OffsetX;
        y = currentY - activePreview.OffsetY;
        width = activePreview.Width;
        height = activePreview.Height;
        return texture != null;
    }

    /// <summary>
    /// Resolves the complete active drag preview at its current pointer position.
    /// </summary>
    /// <param name="preview">Receives the ordered drag-preview presentation.</param>
    /// <param name="pointerX">Receives the current source-space horizontal pointer coordinate.</param>
    /// <param name="pointerY">Receives the current source-space vertical pointer coordinate.</param>
    /// <returns>True when an active drawable preview is available.</returns>
    public bool TryGetPreview(out DragPreview preview, out int pointerX, out int pointerY)
    {
        preview = activePreview;
        pointerX = currentX;
        pointerY = currentY;
        return activeRequest != null && preview?.HasDrawableImages == true;
    }

    /// <summary>
    /// Clears only pending candidate state.
    /// </summary>
    public void ClearCandidate()
    {
        candidateRequest = null;
        candidateStartX = 0;
        candidateStartY = 0;
    }

    /// <summary>
    /// Clears only active drag state.
    /// </summary>
    public void ClearActive()
    {
        activeRequest = null;
        activePreview = null;
        currentX = 0;
        currentY = 0;
    }

    /// <summary>
    /// Clears pending and active drag state.
    /// </summary>
    public void Clear()
    {
        ClearCandidate();
        ClearActive();
    }

    /// <summary>
    /// Clears candidate or active state owned by one source model.
    /// </summary>
    /// <param name="source">The source model being removed.</param>
    public void ClearSource(object source)
    {
        if (source == null)
            return;

        if (ReferenceEquals(candidateRequest?.Source, source))
            ClearCandidate();

        if (ReferenceEquals(activeRequest?.Source, source))
            ClearActive();
    }
}
