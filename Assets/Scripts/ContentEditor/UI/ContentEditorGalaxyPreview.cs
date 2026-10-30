using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// Renders authored star clusters and refits their source geometry when the map surface resizes.
/// </summary>
[ExecuteAlways]
public sealed class ContentEditorGalaxyPreview : UIBehaviour
{
    [SerializeField]
    private ContentEditorMapMarker markerTemplate;
    private readonly List<ContentEditorMapMarker> markers = new();
    private float sourceWidth;

    public event Action DragEnded;

    public event Action<ContentEditorMapMarker> Selected;
    public event Action<ContentEditorMapMarker> Opened;
    public event Action<ContentEditorMapMarker> DragStarted;
    public event Action<ContentEditorMapMarker, Vector2> Moved;

    /// <summary>
    /// Presents every sector using gameplay star artwork and the authored coordinate frame.
    /// </summary>
    /// <param name="records">The authored sectors.</param>
    /// <param name="star">The neutral gameplay marker texture.</param>
    /// <param name="sourceSize">The background's size in source units.</param>
    public void Render(
        IReadOnlyList<ContentPackEditorRecord> records,
        Texture2D star,
        Vector2Int sourceSize
    )
    {
        sourceWidth = sourceSize.x;
        gameObject.SetActive(true);
        for (int i = 0; i < records.Count; i++)
        {
            if (markers.Count <= i)
            {
                ContentEditorMapMarker marker = Instantiate(markerTemplate, transform);
                marker.Selected += selected => Selected?.Invoke(selected);
                marker.Opened += opened => Opened?.Invoke(opened);
                marker.DragStarted += selected => DragStarted?.Invoke(selected);
                marker.Moved += (selected, position) => Moved?.Invoke(selected, position);
                marker.DragEnded += () => DragEnded?.Invoke();
                markers.Add(marker);
            }
            ContentPackEditorRecord record = records[i];
            markers[i]
                .Render(
                    record,
                    new Vector2((float)record.X / sourceSize.x, 1 - (float)record.Y / sourceSize.y),
                    new GalaxyMapClusterRenderData(
                        record.ID,
                        0,
                        0,
                        record.Name,
                        false,
                        record
                            .Planets.Select(planet => new GalaxyMapStarRenderData(
                                planet.ID,
                                planet.X - record.X,
                                planet.Y - record.Y,
                                star,
                                null
                            ))
                            .ToArray()
                    )
                );
        }
        for (int i = records.Count; i < markers.Count; i++)
            markers[i].gameObject.SetActive(false);
        Fit();
    }

    /// <summary>
    /// Hides galaxy markers while the sector preview occupies the map surface.
    /// </summary>
    public void Hide() => gameObject.SetActive(false);

    /// <summary>
    /// Releases project references held by pooled sector markers.
    /// </summary>
    public void Clear()
    {
        foreach (ContentEditorMapMarker marker in markers)
            marker.Clear();
        Hide();
    }

    /// <summary>
    /// Refits clusters after the stretched map surface receives its actual layout dimensions.
    /// </summary>
    protected override void OnRectTransformDimensionsChange()
    {
        base.OnRectTransformDimensionsChange();
        Fit();
    }

    /// <summary>
    /// Scales native source geometry without changing authored positions or marker artwork.
    /// </summary>
    private void Fit()
    {
        if (sourceWidth <= 0)
            return;
        float scale = ((RectTransform)transform).rect.width / sourceWidth;
        foreach (ContentEditorMapMarker marker in markers)
            marker.transform.localScale = Vector3.one * scale;
    }
}
