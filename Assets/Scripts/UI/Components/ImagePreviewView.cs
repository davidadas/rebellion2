using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Inspects artwork at its authored aspect ratio without covering the current workspace.
/// </summary>
public sealed class ImagePreviewView : MonoBehaviour, ICancelable
{
    [SerializeField]
    private RawImage image;

    [SerializeField]
    private TextMeshProUGUI caption;

    [SerializeField]
    private Button closeButton;

    [SerializeField]
    private RectTransform artworkSlot;

    [SerializeField]
    private RectTransform previewPanel;

    [SerializeField]
    private Button nextSizeButton;

    [SerializeField]
    private Button rerollButton;

    [SerializeField]
    private Button applyButton;

    private bool bound;
    private IReadOnlyList<(string Caption, Texture2D Texture)> generatedPreviews;
    private string generatedTitle;
    private int previewIndex;

    /// <summary>
    /// Shows one cached texture in the focused artwork viewer.
    /// </summary>
    /// <param name="texture">The resolved artwork.</param>
    /// <param name="address">Its content address.</param>
    public void Show(Texture2D texture, string address)
    {
        if (texture == null)
            throw new ArgumentNullException(nameof(texture));
        VerifyReferences();
        ClearGenerated();
        Present(texture, address + "  /  " + texture.width + " x " + texture.height);
    }

    /// <summary>
    /// Displays a generated state preview with selection and variation controls.
    /// </summary>
    /// <param name="title">The unit and state shown in the viewer.</param>
    /// <param name="previews">The owned textures in preview order.</param>
    /// <param name="reroll">The action that generates the next variation.</param>
    /// <param name="apply">The action that assigns the generated images.</param>
    internal void ShowGenerated(
        string title,
        IReadOnlyList<(string Caption, Texture2D Texture)> previews,
        Action reroll,
        Action apply
    )
    {
        VerifyReferences();
        if (
            artworkSlot == null
            || previewPanel == null
            || nextSizeButton == null
            || rerollButton == null
            || applyButton == null
        )
            throw new MissingReferenceException(name + " has incomplete generation controls.");
        int previousIndex = generatedPreviews == null ? 0 : previewIndex;
        ClearGenerated();
        generatedPreviews = previews;
        generatedTitle = title;
        previewIndex = Mathf.Min(previousIndex, previews.Count - 1);
        SetAction(
            nextSizeButton,
            previews.Count > 1,
            () =>
            {
                previewIndex = (previewIndex + 1) % generatedPreviews.Count;
                PresentGeneratedSize();
            }
        );
        SetAction(rerollButton, true, () => reroll());
        SetAction(
            applyButton,
            true,
            () =>
            {
                TryCancel();
                apply();
            }
        );
        artworkSlot.offsetMin = new Vector2(artworkSlot.offsetMin.x, 78);
        LayoutGeneratedPanel();
        PresentGeneratedSize();
    }

    /// <summary>
    /// Validates and binds the shared viewer controls before displaying an image.
    /// </summary>
    private void VerifyReferences()
    {
        if (
            image == null
            || caption == null
            || closeButton == null
            || image.GetComponent<AspectRatioFitter>() == null
        )
            throw new MissingReferenceException(name + " has incomplete image-preview references.");
        if (!bound)
        {
            closeButton.onClick.AddListener(() => TryCancel());
            bound = true;
        }
    }

    /// <summary>
    /// Updates the texture and caption without taking ownership of the texture.
    /// </summary>
    /// <param name="texture">The ready-to-display image.</param>
    /// <param name="title">The visible image caption.</param>
    private void Present(Texture2D texture, string title)
    {
        image.texture = texture;
        image.GetComponent<AspectRatioFitter>().aspectRatio = (float)texture.width / texture.height;
        caption.text = title;
        gameObject.SetActive(true);
        transform.SetAsLastSibling();
    }

    /// <summary>
    /// Presents the active size and the default dimensions of its generated asset.
    /// </summary>
    private void PresentGeneratedSize()
    {
        var preview = generatedPreviews[previewIndex];
        Present(
            preview.Texture,
            $"{generatedTitle}  /  {preview.Caption} ({preview.Texture.width} × {preview.Texture.height})"
        );
    }

    /// <summary>
    /// Keeps the variation viewer compact as the workspace window changes size.
    /// </summary>
    private void OnRectTransformDimensionsChange()
    {
        if (generatedPreviews != null)
            LayoutGeneratedPanel();
    }

    /// <summary>
    /// Centers the generated image and controls in a panel sized for gameplay artwork.
    /// </summary>
    private void LayoutGeneratedPanel()
    {
        Rect available = ((RectTransform)transform).rect;
        previewPanel.anchorMin = previewPanel.anchorMax = new Vector2(0.5f, 0.5f);
        previewPanel.anchoredPosition = Vector2.zero;
        previewPanel.sizeDelta = new Vector2(
            Mathf.Min(840, available.width - 48),
            Mathf.Min(440, available.height - 48)
        );
    }

    /// <summary>
    /// Rebinds one optional action while the generation controls are visible.
    /// </summary>
    /// <param name="button">The reused optional button.</param>
    /// <param name="visible">Whether to show the button.</param>
    /// <param name="action">The command to invoke when selected.</param>
    private static void SetAction(
        Button button,
        bool visible,
        UnityEngine.Events.UnityAction action
    )
    {
        if (button == null)
            return;
        button.onClick.RemoveAllListeners();
        if (action != null)
            button.onClick.AddListener(action);
        button.gameObject.SetActive(visible);
    }

    /// <summary>
    /// Discards all temporary canvases and restores the ordinary artwork viewer layout.
    /// </summary>
    private void ClearGenerated()
    {
        if (generatedPreviews != null)
        {
            foreach (var preview in generatedPreviews)
            {
                if (Application.isPlaying)
                    Destroy(preview.Texture);
                else
                    DestroyImmediate(preview.Texture);
            }
            generatedPreviews = null;
            if (previewPanel != null)
            {
                previewPanel.anchorMin = Vector2.zero;
                previewPanel.anchorMax = Vector2.one;
                previewPanel.offsetMin = new Vector2(160, 100);
                previewPanel.offsetMax = new Vector2(-160, -100);
            }
        }
        SetAction(nextSizeButton, false, null);
        SetAction(rerollButton, false, null);
        SetAction(applyButton, false, null);
        if (artworkSlot != null)
            artworkSlot.offsetMin = new Vector2(artworkSlot.offsetMin.x, 28);
    }

    /// <summary>
    /// Dismisses the artwork viewer without changing its source asset reference.
    /// </summary>
    /// <returns>True when the viewer was dismissed.</returns>
    public bool TryCancel()
    {
        if (!gameObject.activeSelf)
            return false;
        image.texture = null;
        ClearGenerated();
        gameObject.SetActive(false);
        return true;
    }

    /// <summary>
    /// Releases any owned preview textures during workspace teardown.
    /// </summary>
    private void OnDestroy() => ClearGenerated();
}
