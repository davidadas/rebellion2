using System;
using UnityEngine;

/// <summary>
/// Identifies the UI model that initiated a drag operation.
/// </summary>
public sealed class DragRequest
{
    /// <summary>
    /// Creates a drag request for a non-null source model.
    /// </summary>
    /// <param name="source">The model initiating the drag.</param>
    public DragRequest(object source)
    {
        Source = source ?? throw new ArgumentNullException(nameof(source));
    }

    /// <summary>
    /// Gets the source.
    /// </summary>
    public object Source { get; }
}

/// <summary>
/// Defines the texture, dimensions, and pointer offset of a drag preview.
/// </summary>
public sealed class DragPreview
{
    /// <summary>
    /// Creates immutable drag-preview presentation data.
    /// </summary>
    /// <param name="texture">The preview texture.</param>
    /// <param name="width">The source-space width.</param>
    /// <param name="height">The source-space height.</param>
    /// <param name="offsetX">The horizontal pointer offset.</param>
    /// <param name="offsetY">The vertical pointer offset.</param>
    public DragPreview(Texture texture, int width, int height, int offsetX, int offsetY)
    {
        Texture = texture;
        Width = width;
        Height = height;
        OffsetX = offsetX;
        OffsetY = offsetY;
    }

    /// <summary>
    /// Gets the texture.
    /// </summary>
    public Texture Texture { get; }

    /// <summary>
    /// Gets the width.
    /// </summary>
    public int Width { get; }

    /// <summary>
    /// Gets the height.
    /// </summary>
    public int Height { get; }

    /// <summary>
    /// Gets the offset x.
    /// </summary>
    public int OffsetX { get; }

    /// <summary>
    /// Gets the offset y.
    /// </summary>
    public int OffsetY { get; }
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
    /// Gets a value indicating whether candidate is present.
    /// </summary>
    public bool HasCandidate => candidateRequest != null;

    /// <summary>
    /// Gets a value indicating whether dragging is active.
    /// </summary>
    public bool IsDragging => activeRequest != null;

    /// <summary>
    /// Gets the candidate request.
    /// </summary>
    public DragRequest CandidateRequest => candidateRequest;

    /// <summary>
    /// Gets the active request.
    /// </summary>
    public DragRequest ActiveRequest => activeRequest;

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
