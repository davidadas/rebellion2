using System;
using UnityEngine;

public sealed class DragRequest
{
    public DragRequest(object source)
    {
        Source = source ?? throw new ArgumentNullException(nameof(source));
    }

    public object Source { get; }
}

public sealed class DragPreview
{
    public DragPreview(Texture texture, int width, int height, int offsetX, int offsetY)
    {
        Texture = texture;
        Width = width;
        Height = height;
        OffsetX = offsetX;
        OffsetY = offsetY;
    }

    public Texture Texture { get; }
    public int Width { get; }
    public int Height { get; }
    public int OffsetX { get; }
    public int OffsetY { get; }
}

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

    public DragController(int startDistance)
    {
        if (startDistance < 0)
            throw new ArgumentOutOfRangeException(nameof(startDistance));

        this.startDistance = startDistance;
    }

    public bool HasCandidate => candidateRequest != null;
    public bool IsDragging => activeRequest != null;
    public DragRequest CandidateRequest => candidateRequest;
    public DragRequest ActiveRequest => activeRequest;

    public void StartCandidate(DragRequest request, int x, int y)
    {
        candidateRequest = request ?? throw new ArgumentNullException(nameof(request));
        candidateStartX = x;
        candidateStartY = y;
    }

    public bool HasCandidateDragStarted(int x, int y)
    {
        if (candidateRequest == null)
            return false;

        int deltaX = x - candidateStartX;
        int deltaY = y - candidateStartY;
        return deltaX * deltaX + deltaY * deltaY >= startDistance * startDistance;
    }

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

    public bool Move(int x, int y)
    {
        if (activeRequest == null)
            return false;

        currentX = x;
        currentY = y;
        return true;
    }

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

    public void ClearCandidate()
    {
        candidateRequest = null;
        candidateStartX = 0;
        candidateStartY = 0;
    }

    public void ClearActive()
    {
        activeRequest = null;
        activePreview = null;
        currentX = 0;
        currentY = 0;
    }

    public void Clear()
    {
        ClearCandidate();
        ClearActive();
    }

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
