using System;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Presents the authored eight-section frame around a galactic-information panel.
/// </summary>
public sealed class GalacticInformationFrameView : MonoBehaviour
{
    private const int _frameSectionCount = 8;

    [SerializeField]
    private RawImage[] frameImages = Array.Empty<RawImage>();

    /// <summary>
    /// Validates authored frame references when Unity creates the view.
    /// </summary>
    private void Awake()
    {
        VerifyReferences();
    }

    /// <summary>
    /// Applies resolved frame textures and dimensions to the authored frame sections.
    /// </summary>
    /// <param name="data">The immutable frame presentation.</param>
    public void Render(GalacticInformationFrameRenderData data)
    {
        VerifyReferences();
        if (data == null)
        {
            HideAllImages();
            return;
        }

        if (data.Textures.Count != _frameSectionCount)
        {
            throw new ArgumentException(
                $"A galactic-information frame requires {_frameSectionCount} textures.",
                nameof(data)
            );
        }

        UILayout.SetSourceRect(transform as RectTransform, 0, 0, data.Width, data.Height);
        Vector2Int[] sizes = new Vector2Int[_frameSectionCount];
        for (int i = 0; i < frameImages.Length; i++)
        {
            Texture2D texture = data.Textures[i];
            RawImage image = frameImages[i];
            image.texture = texture;
            image.enabled = texture != null;
            image.raycastTarget = false;
            sizes[i] = texture == null ? Vector2Int.zero : UILayout.GetTextureSourceSize(texture);
        }

        SetRect(frameImages[0], 0, 0, sizes[0].x, sizes[0].y);
        SetRect(frameImages[1], data.Width - sizes[1].x, 0, sizes[1].x, sizes[1].y);
        SetRect(frameImages[2], 0, data.Height - sizes[2].y, sizes[2].x, sizes[2].y);
        SetRect(
            frameImages[3],
            data.Width - sizes[3].x,
            data.Height - sizes[3].y,
            sizes[3].x,
            sizes[3].y
        );
        SetRect(frameImages[4], sizes[0].x, 0, data.Width - sizes[0].x - sizes[1].x, sizes[4].y);
        SetRect(frameImages[5], 0, sizes[0].y, sizes[5].x, data.Height - sizes[0].y - sizes[2].y);
        SetRect(
            frameImages[6],
            data.Width - sizes[6].x,
            sizes[1].y,
            sizes[6].x,
            data.Height - sizes[1].y - sizes[3].y
        );
        SetRect(
            frameImages[7],
            sizes[2].x,
            data.Height - sizes[7].y,
            data.Width - sizes[2].x - sizes[3].x,
            sizes[7].y
        );
    }

    /// <summary>
    /// Applies source-space bounds and visibility to one frame section.
    /// </summary>
    /// <param name="image">The frame-section image.</param>
    /// <param name="sourceX">The source-space horizontal position.</param>
    /// <param name="sourceY">The source-space vertical position.</param>
    /// <param name="width">The source-space width.</param>
    /// <param name="height">The source-space height.</param>
    private static void SetRect(RawImage image, int sourceX, int sourceY, int width, int height)
    {
        UILayout.SetSourceRect(image.rectTransform, sourceX, sourceY, width, height);
        image.gameObject.SetActive(image.texture != null && width > 0 && height > 0);
    }

    /// <summary>
    /// Hides every frame section when no presentation is available.
    /// </summary>
    private void HideAllImages()
    {
        foreach (RawImage image in frameImages)
        {
            image.texture = null;
            image.enabled = false;
            image.gameObject.SetActive(false);
        }
    }

    /// <summary>
    /// Verifies that the authored frame contains exactly eight valid image sections.
    /// </summary>
    private void VerifyReferences()
    {
        if (frameImages == null || frameImages.Length != _frameSectionCount)
        {
            throw new MissingReferenceException(
                $"{name}/FrameImages must contain {_frameSectionCount} images."
            );
        }

        foreach (RawImage image in frameImages)
        {
            if (image == null)
            {
                throw new MissingReferenceException(
                    $"{name}/FrameImages contains a missing image."
                );
            }
        }
    }
}
