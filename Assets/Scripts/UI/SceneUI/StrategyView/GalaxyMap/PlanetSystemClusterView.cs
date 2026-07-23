using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Presents one authored planet-system cluster and translates pointer input into cluster events.
/// </summary>
public sealed class PlanetSystemClusterView
    : MonoBehaviour,
        IPointerEnterHandler,
        IPointerExitHandler,
        IPointerClickHandler
{
    [SerializeField]
    private RawImage hitAreaImage;

    [SerializeField]
    private TextMeshProUGUI systemNameTextField;

    [SerializeField]
    private RawImage starImageTemplate;

    [SerializeField]
    private RawImage headquartersImageTemplate;

    private readonly List<RawImage> starImages = new List<RawImage>();
    private readonly List<RawImage> headquartersImages = new List<RawImage>();
    private readonly List<GalaxyMapStarRenderData> renderedStars =
        new List<GalaxyMapStarRenderData>();

    private GalaxyMapClusterRenderData renderData;

    /// <summary>
    /// Raised when the pointer enters the rendered cluster.
    /// </summary>
    public event Action<PlanetSystemClusterView> Hovered;

    /// <summary>
    /// Raised when the pointer exits the rendered cluster.
    /// </summary>
    public event Action<PlanetSystemClusterView> HoverCleared;

    /// <summary>
    /// Raised when the rendered cluster receives a left-button double-click.
    /// </summary>
    public event Action<PlanetSystemClusterView, PointerEventData> OpenRequested;

    public string SystemInstanceId => renderData?.SystemInstanceId;

    /// <summary>
    /// Validates authored references when Unity creates the view.
    /// </summary>
    private void Awake()
    {
        VerifyReferences();
    }

    /// <summary>
    /// Applies immutable cluster presentation data to authored and pooled child controls.
    /// </summary>
    /// <param name="data">The cluster presentation snapshot.</param>
    public void Render(GalaxyMapClusterRenderData data)
    {
        if (data == null)
            throw new ArgumentNullException(nameof(data));

        VerifyReferences();
        renderData = data;
        RectTransform rect = transform as RectTransform;
        if (rect == null)
            throw new MissingReferenceException($"{name} is missing RectTransform.");

        RectInt authoredBounds = GetSourceRect(rect);
        UILayout.SetSourceRect(
            rect,
            data.SourceX,
            data.SourceY,
            authoredBounds.width,
            authoredBounds.height
        );
        UILayout.SetSourceRect(
            hitAreaImage.rectTransform,
            0,
            0,
            authoredBounds.width,
            authoredBounds.height
        );
        hitAreaImage.enabled = true;
        hitAreaImage.raycastTarget = true;
        hitAreaImage.canvasRenderer.cullTransparentMesh = false;
        RenderStars(data.Stars);
        RenderLabel(data.Label, data.ShowLabel);
    }

    /// <summary>
    /// Resolves a pointer hit to the rendered planet identifier beneath it.
    /// </summary>
    /// <param name="eventData">The current pointer event.</param>
    /// <param name="planetInstanceId">Receives the rendered planet identifier.</param>
    /// <returns>True when the pointer is over a visible marker.</returns>
    internal bool TryGetPlanetInstanceID(PointerEventData eventData, out string planetInstanceId)
    {
        planetInstanceId = null;
        if (
            eventData == null
            || renderData == null
            || !gameObject.activeInHierarchy
            || transform is not RectTransform rect
            || !TryGetSourcePosition(rect, eventData, out int sourceX, out int sourceY)
        )
        {
            return false;
        }

        for (int i = renderedStars.Count - 1; i >= 0; i--)
        {
            GalaxyMapStarRenderData star = renderedStars[i];
            RectInt starBounds = GetRenderedStarSourceRect(i);
            if (
                !string.IsNullOrEmpty(star.PlanetInstanceId)
                && starBounds.width > 0
                && starBounds.height > 0
                && starBounds.Contains(new Vector2Int(sourceX, sourceY))
            )
            {
                planetInstanceId = star.PlanetInstanceId;
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Returns the current source-space cluster bounds for focused tests and hit resolution.
    /// </summary>
    /// <returns>The current source-space cluster bounds.</returns>
    internal RectInt GetRenderedSourceRect()
    {
        return GetSourceRect(transform as RectTransform);
    }

    /// <summary>
    /// Returns one rendered star marker's source-space bounds within the cluster.
    /// </summary>
    /// <param name="index">The rendered marker index.</param>
    /// <returns>The marker bounds, or default for an invalid index.</returns>
    internal RectInt GetRenderedStarSourceRect(int index)
    {
        return index >= 0 && index < starImages.Count
            ? GetSourceRect(starImages[index].rectTransform)
            : default;
    }

    /// <summary>
    /// Emits a semantic hover event when the pointer enters a rendered cluster.
    /// </summary>
    /// <param name="eventData">The originating pointer event.</param>
    public void OnPointerEnter(PointerEventData eventData)
    {
        if (!string.IsNullOrEmpty(SystemInstanceId) && gameObject.activeInHierarchy)
            Hovered?.Invoke(this);
    }

    /// <summary>
    /// Emits a semantic hover-clear event when the pointer exits the cluster.
    /// </summary>
    /// <param name="eventData">The originating pointer event.</param>
    public void OnPointerExit(PointerEventData eventData)
    {
        HoverCleared?.Invoke(this);
    }

    /// <summary>
    /// Emits a semantic open request for a left-button double-click.
    /// </summary>
    /// <param name="eventData">The originating pointer event.</param>
    public void OnPointerClick(PointerEventData eventData)
    {
        if (
            !string.IsNullOrEmpty(SystemInstanceId)
            && eventData?.button == PointerEventData.InputButton.Left
            && eventData.clickCount >= 2
        )
        {
            OpenRequested?.Invoke(this, eventData);
        }
    }

    /// <summary>
    /// Applies resolved marker and headquarters textures to reusable image slots.
    /// </summary>
    /// <param name="stars">The rendered marker presentations.</param>
    private void RenderStars(IReadOnlyList<GalaxyMapStarRenderData> stars)
    {
        int count = stars?.Count ?? 0;
        renderedStars.Clear();
        for (int i = 0; i < count; i++)
        {
            GalaxyMapStarRenderData star = stars[i];
            renderedStars.Add(star);
            ApplyImage(GetOrCreateStarImage(i), star.StarTexture, star.SourceX, star.SourceY);
            ApplyImage(
                GetOrCreateHeadquartersImage(i),
                star.HeadquartersTexture,
                star.SourceX,
                star.SourceY
            );
        }

        HideUnusedImages(starImages, count);
        HideUnusedImages(headquartersImages, count);
    }

    /// <summary>
    /// Applies the projected system label and visibility.
    /// </summary>
    /// <param name="label">The displayed system name.</param>
    /// <param name="visible">Whether the label is visible.</param>
    private void RenderLabel(string label, bool visible)
    {
        systemNameTextField.text = label ?? string.Empty;
        systemNameTextField.gameObject.SetActive(visible && !string.IsNullOrEmpty(label));
    }

    /// <summary>
    /// Gets or creates a pooled star-marker image.
    /// </summary>
    /// <param name="index">The requested marker index.</param>
    /// <returns>The reusable star-marker image.</returns>
    private RawImage GetOrCreateStarImage(int index)
    {
        return GetOrCreateImage(starImages, starImageTemplate, "Star", index);
    }

    /// <summary>
    /// Gets or creates a pooled headquarters-overlay image.
    /// </summary>
    /// <param name="index">The requested overlay index.</param>
    /// <returns>The reusable headquarters-overlay image.</returns>
    private RawImage GetOrCreateHeadquartersImage(int index)
    {
        return GetOrCreateImage(
            headquartersImages,
            headquartersImageTemplate,
            "Headquarters",
            index
        );
    }

    /// <summary>
    /// Gets or creates one pooled image cloned from its authored template.
    /// </summary>
    /// <param name="images">The owned image pool.</param>
    /// <param name="template">The authored image template.</param>
    /// <param name="prefix">The generated object-name prefix.</param>
    /// <param name="index">The requested image index.</param>
    /// <returns>The reusable image at the requested index.</returns>
    private RawImage GetOrCreateImage(
        List<RawImage> images,
        RawImage template,
        string prefix,
        int index
    )
    {
        while (images.Count <= index)
        {
            RawImage image = Instantiate(template, transform);
            image.name = $"{prefix}{images.Count}Image";
            image.raycastTarget = false;
            image.transform.SetSiblingIndex(systemNameTextField.transform.GetSiblingIndex());
            images.Add(image);
        }

        return images[index];
    }

    /// <summary>
    /// Applies a resolved texture and source-space placement to one marker image.
    /// </summary>
    /// <param name="image">The image to update.</param>
    /// <param name="texture">The resolved marker texture.</param>
    /// <param name="sourceX">The source-space horizontal offset.</param>
    /// <param name="sourceY">The source-space vertical offset.</param>
    private static void ApplyImage(RawImage image, Texture2D texture, int sourceX, int sourceY)
    {
        image.texture = texture;
        image.enabled = texture != null;
        image.gameObject.SetActive(texture != null);
        if (texture == null)
            return;

        Vector2Int size = UILayout.GetTextureSourceSize(texture);
        UILayout.SetSourceRect(image.rectTransform, sourceX, sourceY, size.x, size.y);
    }

    /// <summary>
    /// Hides pooled images that are not used by the current snapshot.
    /// </summary>
    /// <param name="images">The owned image pool.</param>
    /// <param name="usedCount">The number of images used by the current snapshot.</param>
    private static void HideUnusedImages(List<RawImage> images, int usedCount)
    {
        for (int i = usedCount; i < images.Count; i++)
            images[i].gameObject.SetActive(false);
    }

    /// <summary>
    /// Resolves source-space pointer coordinates within a rendered cluster.
    /// </summary>
    /// <param name="rect">The rendered cluster transform.</param>
    /// <param name="eventData">The current pointer event.</param>
    /// <param name="sourceX">Receives the horizontal source coordinate.</param>
    /// <param name="sourceY">Receives the vertical source coordinate.</param>
    /// <returns>True when the pointer resolves inside the cluster.</returns>
    private static bool TryGetSourcePosition(
        RectTransform rect,
        PointerEventData eventData,
        out int sourceX,
        out int sourceY
    )
    {
        sourceX = 0;
        sourceY = 0;
        if (
            rect == null
            || eventData == null
            || !RectTransformUtility.ScreenPointToLocalPointInRectangle(
                rect,
                eventData.position,
                eventData.pressEventCamera,
                out Vector2 local
            )
        )
        {
            return false;
        }

        Rect sourceRect = rect.rect;
        int width = Mathf.RoundToInt(sourceRect.width);
        int height = Mathf.RoundToInt(sourceRect.height);
        if (width <= 0)
            width = Mathf.RoundToInt(rect.sizeDelta.x);
        if (height <= 0)
            height = Mathf.RoundToInt(rect.sizeDelta.y);
        if (width <= 0 || height <= 0)
            return false;

        sourceX = Mathf.RoundToInt(local.x - sourceRect.xMin);
        sourceY = Mathf.RoundToInt(sourceRect.yMax - local.y);
        return sourceX >= 0 && sourceX < width && sourceY >= 0 && sourceY < height;
    }

    /// <summary>
    /// Reads source-space bounds from an authored rectangle.
    /// </summary>
    /// <param name="rect">The authored rectangle.</param>
    /// <returns>The equivalent source-space bounds.</returns>
    private static RectInt GetSourceRect(RectTransform rect)
    {
        if (rect == null)
            return default;

        return new RectInt(
            Mathf.RoundToInt(rect.anchoredPosition.x),
            -Mathf.RoundToInt(rect.anchoredPosition.y),
            Mathf.RoundToInt(rect.sizeDelta.x),
            Mathf.RoundToInt(rect.sizeDelta.y)
        );
    }

    /// <summary>
    /// Verifies every authored reference required for cluster presentation and input.
    /// </summary>
    private void VerifyReferences()
    {
        if (hitAreaImage == null)
            throw new MissingReferenceException($"{name}/HitAreaImage is missing.");
        if (systemNameTextField == null)
            throw new MissingReferenceException($"{name}/SystemNameTextField is missing.");
        if (starImageTemplate == null)
            throw new MissingReferenceException($"{name}/StarImageTemplate is missing.");
        if (headquartersImageTemplate == null)
            throw new MissingReferenceException($"{name}/HeadquartersImageTemplate is missing.");
        starImageTemplate.gameObject.SetActive(false);
        headquartersImageTemplate.gameObject.SetActive(false);
        systemNameTextField.raycastTarget = false;
    }
}
