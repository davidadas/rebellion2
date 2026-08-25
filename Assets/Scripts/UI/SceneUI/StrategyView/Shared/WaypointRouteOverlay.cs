using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Describes one source-space waypoint route segment.
/// </summary>
internal readonly struct WaypointRouteLineRenderData
{
    public Vector2Int Start { get; }

    public Vector2Int End { get; }

    /// <summary>
    /// Creates a route segment between two source-space positions.
    /// </summary>
    /// <param name="start">The segment's starting position.</param>
    /// <param name="end">The segment's ending position.</param>
    public WaypointRouteLineRenderData(Vector2Int start, Vector2Int end)
    {
        Start = start;
        End = end;
    }
}

/// <summary>
/// Describes one numbered source-space waypoint marker.
/// </summary>
internal readonly struct WaypointRouteMarkerRenderData
{
    public int Order { get; }

    public Vector2Int Position { get; }

    /// <summary>
    /// Creates a numbered waypoint marker at one source-space position.
    /// </summary>
    /// <param name="order">The one-based waypoint order.</param>
    /// <param name="position">The marker position.</param>
    public WaypointRouteMarkerRenderData(int order, Vector2Int position)
    {
        Order = order;
        Position = position;
    }
}

/// <summary>
/// Owns pooled, non-interactive route lines and numbered waypoint markers for a strategy map.
/// </summary>
internal sealed class WaypointRouteOverlay
{
    private const float _lineThickness = 1f;
    private const int _markerOffsetX = 4;
    private const int _markerOffsetY = -10;
    private const int _markerSize = 11;

    private readonly RectTransform _lineLayer;
    private readonly List<Image> _lines = new List<Image>();
    private readonly RectTransform _markerLayer;
    private readonly List<Image> _markers = new List<Image>();
    private readonly List<TextMeshProUGUI> _numberLabels = new List<TextMeshProUGUI>();
    private readonly TextMeshProUGUI _textTemplate;

    /// <summary>
    /// Creates waypoint presentation layers under one source-space map root.
    /// </summary>
    /// <param name="parent">The map root that owns the route presentation.</param>
    /// <param name="textTemplate">The authored text style used for waypoint numbers.</param>
    public WaypointRouteOverlay(RectTransform parent, TextMeshProUGUI textTemplate)
    {
        if (parent == null)
            throw new ArgumentNullException(nameof(parent));

        _textTemplate = textTemplate ?? throw new ArgumentNullException(nameof(textTemplate));
        _lineLayer = CreateLayer(parent, "WaypointLines");
        _markerLayer = CreateLayer(parent, "WaypointMarkers");
        SetPresentationOrder();
    }

    /// <summary>
    /// Renders the complete route presentation and hides unused pooled elements.
    /// </summary>
    /// <param name="lineData">The visible route segments.</param>
    /// <param name="markerData">The visible numbered stops.</param>
    public void Render(
        IReadOnlyList<WaypointRouteLineRenderData> lineData,
        IReadOnlyList<WaypointRouteMarkerRenderData> markerData
    )
    {
        int lineCount = lineData?.Count ?? 0;
        for (int index = 0; index < lineCount; index++)
            RenderLine(GetOrCreateLine(index), lineData[index]);
        HideUnused(_lines, lineCount);

        int markerCount = markerData?.Count ?? 0;
        for (int index = 0; index < markerCount; index++)
        {
            Image marker = GetOrCreateMarker(index);
            RenderMarker(marker, _numberLabels[index], markerData[index]);
        }
        HideUnused(_markers, markerCount);
    }

    /// <summary>
    /// Places route lines below map objects and waypoint numbers above them.
    /// </summary>
    public void SetPresentationOrder()
    {
        _lineLayer.SetAsFirstSibling();
        _markerLayer.SetAsLastSibling();
    }

    /// <summary>
    /// Creates one full-map, non-interactive presentation layer.
    /// </summary>
    /// <param name="parent">The map root that owns the layer.</param>
    /// <param name="name">The layer object name.</param>
    /// <returns>The created layer.</returns>
    private static RectTransform CreateLayer(RectTransform parent, string name)
    {
        GameObject layerObject = new GameObject(name, typeof(RectTransform));
        RectTransform layer = layerObject.GetComponent<RectTransform>();
        layer.SetParent(parent, false);
        layer.anchorMin = Vector2.zero;
        layer.anchorMax = Vector2.one;
        layer.pivot = new Vector2(0.5f, 0.5f);
        layer.offsetMin = Vector2.zero;
        layer.offsetMax = Vector2.zero;
        return layer;
    }

    /// <summary>
    /// Gets or creates one pooled white route line.
    /// </summary>
    /// <param name="index">The requested pool index.</param>
    /// <returns>The reusable route line.</returns>
    private Image GetOrCreateLine(int index)
    {
        int missingCount = index - _lines.Count + 1;
        for (int missingIndex = 0; missingIndex < missingCount; missingIndex++)
        {
            GameObject lineObject = new GameObject(
                $"WaypointLine{_lines.Count + 1}",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image)
            );
            lineObject.transform.SetParent(_lineLayer, false);
            Image line = lineObject.GetComponent<Image>();
            line.color = Color.white;
            line.raycastTarget = false;
            _lines.Add(line);
        }

        return _lines[index];
    }

    /// <summary>
    /// Gets or creates one pooled numbered route marker.
    /// </summary>
    /// <param name="index">The requested pool index.</param>
    /// <returns>The reusable waypoint marker.</returns>
    private Image GetOrCreateMarker(int index)
    {
        int missingCount = index - _markers.Count + 1;
        for (int missingIndex = 0; missingIndex < missingCount; missingIndex++)
        {
            GameObject markerObject = new GameObject(
                $"WaypointMarker{_markers.Count + 1}",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image)
            );
            markerObject.transform.SetParent(_markerLayer, false);
            Image marker = markerObject.GetComponent<Image>();
            marker.color = new Color(0f, 0f, 0f, 0.8f);
            marker.raycastTarget = false;

            TextMeshProUGUI label = UnityEngine.Object.Instantiate(
                _textTemplate,
                markerObject.transform
            );
            label.name = "Number";
            label.gameObject.SetActive(true);
            label.alignment = TextAlignmentOptions.Center;
            label.color = Color.white;
            label.fontSize = 8;
            label.fontStyle = FontStyles.Bold;
            label.raycastTarget = false;
            UILayout.SetSourceRect(label.rectTransform, 0, 0, _markerSize, _markerSize);

            _markers.Add(marker);
            _numberLabels.Add(label);
        }

        return _markers[index];
    }

    /// <summary>
    /// Places and rotates one line between two source-space coordinates.
    /// </summary>
    /// <param name="line">The line to position.</param>
    /// <param name="data">The source-space line endpoints.</param>
    private static void RenderLine(Image line, WaypointRouteLineRenderData data)
    {
        Vector2 delta = new Vector2(data.End.x - data.Start.x, data.End.y - data.Start.y);
        RectTransform rect = line.rectTransform;
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 0.5f);
        rect.anchoredPosition = new Vector2(data.Start.x, -data.Start.y);
        rect.sizeDelta = new Vector2(delta.magnitude, _lineThickness);
        rect.localEulerAngles = new Vector3(0f, 0f, -Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg);
        line.gameObject.SetActive(delta.sqrMagnitude > 0f);
    }

    /// <summary>
    /// Places one numbered marker beside its waypoint.
    /// </summary>
    /// <param name="marker">The marker image to position.</param>
    /// <param name="label">The marker's number label.</param>
    /// <param name="data">The waypoint number and source-space position.</param>
    private static void RenderMarker(
        Image marker,
        TextMeshProUGUI label,
        WaypointRouteMarkerRenderData data
    )
    {
        UILayout.SetSourceRect(
            marker.rectTransform,
            data.Position.x + _markerOffsetX,
            data.Position.y + _markerOffsetY,
            _markerSize,
            _markerSize
        );
        label.text = data.Order.ToString();
        marker.gameObject.SetActive(true);
    }

    /// <summary>
    /// Hides pooled images at and after one used count.
    /// </summary>
    /// <param name="images">The pooled images.</param>
    /// <param name="usedCount">The number of active entries.</param>
    private static void HideUnused(List<Image> images, int usedCount)
    {
        for (int index = usedCount; index < images.Count; index++)
            images[index].gameObject.SetActive(false);
    }
}
