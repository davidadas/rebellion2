using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Displays a selectable layer with an independent visibility control and real artwork thumbnail.
/// </summary>
public sealed class ContentEditorArtworkLayerRow : MonoBehaviour
{
    [SerializeField]
    private Button select;

    [SerializeField]
    private Button visibility;

    [SerializeField]
    private TMP_Text caption;

    [SerializeField]
    private TMP_Text visibilityCaption;

    [SerializeField]
    private RawImage thumbnail;

    private int index;
    private Action<int> onSelect;
    private Action<int> onToggle;
    private bool bound;

    /// <summary>
    /// Updates an authored row for the currently displayed draft output.
    /// </summary>
    /// <param name="layer">The named texture and editing state.</param>
    /// <param name="selected">Whether the layer is selected.</param>
    /// <param name="editing">Whether the draft is editable.</param>
    /// <param name="selectLayer">Selects the layer.</param>
    /// <param name="toggleLayer">Changes its export visibility.</param>
    internal void Render(
        ArtworkEffectLayerPreview layer,
        bool selected,
        bool editing,
        Action<int> selectLayer,
        Action<int> toggleLayer
    )
    {
        if (!bound)
        {
            bound = true;
            select.onClick.AddListener(() => onSelect?.Invoke(index));
            visibility.onClick.AddListener(() => onToggle?.Invoke(index));
        }
        index = layer.Index;
        onSelect = selectLayer;
        onToggle = toggleLayer;
        caption.text = layer.Name;
        thumbnail.texture = layer.Texture;
        thumbnail.uvRect = layer.Uv;
        thumbnail.GetComponent<AspectRatioFitter>().aspectRatio =
            (float)layer.Texture.width / layer.Texture.height;
        thumbnail.color = layer.Hidden ? new Color(1, 1, 1, 0.48f) : Color.white;
        visibilityCaption.text =
            layer.Linked ? "Linked"
            : layer.Hidden ? "Show"
            : "Hide";
        visibility.interactable = editing && !layer.Linked;
        ContentEditorArtworkWorkspace.SetSelected(select, selected);
    }

    /// <summary>
    /// Releases borrowed artwork before the owning draft is disposed.
    /// </summary>
    internal void Clear()
    {
        thumbnail.texture = null;
        onSelect = onToggle = null;
    }
}
