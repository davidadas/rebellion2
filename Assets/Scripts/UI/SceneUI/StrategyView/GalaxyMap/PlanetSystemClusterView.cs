using System.Collections.Generic;
using Rebellion.Game.Galaxy;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public sealed class PlanetSystemClusterView
    : MonoBehaviour,
        IPointerEnterHandler,
        IPointerExitHandler,
        IPointerClickHandler
{
    private const string _unknownThemeId = "UNKNOWN";

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
    private UIContext uiContext;
    private PlanetSystem system;

    public PlanetSystem System => system;
    public event System.Action<PlanetSystemClusterView> Hovered;
    public event System.Action<PlanetSystemClusterView> HoverCleared;
    public event System.Action<PlanetSystemClusterView, PointerEventData> OpenRequested;

    public void Initialize(UIContext uiContext)
    {
        if (uiContext == null)
            throw new System.ArgumentNullException(nameof(uiContext));

        this.uiContext = uiContext;
    }

    public void Render(
        PlanetSystem system,
        IReadOnlyList<GalaxyMapSystemStar> stars,
        int x,
        int y,
        string labelText,
        bool showLabel
    )
    {
        VerifyReferences();
        this.system = system;
        RectTransform rect = transform as RectTransform;
        if (rect == null)
            throw new MissingReferenceException($"{name} is missing RectTransform.");

        RectInt templateRect = GetSourceRect(rect);
        SetSourceRect(rect, x, y, templateRect.width, templateRect.height);
        UILayout.SetSourceRect(
            hitAreaImage.rectTransform,
            0,
            0,
            templateRect.width,
            templateRect.height
        );
        hitAreaImage.enabled = true;
        hitAreaImage.raycastTarget = true;
        hitAreaImage.canvasRenderer.cullTransparentMesh = false;
        RenderStars(stars, x, y);
        RenderLabel(labelText, showLabel);
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (system != null && gameObject.activeInHierarchy)
            Hovered?.Invoke(this);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        HoverCleared?.Invoke(this);
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (
            system != null
            && eventData.button == PointerEventData.InputButton.Left
            && eventData.clickCount >= 2
        )
        {
            OpenRequested?.Invoke(this, eventData);
        }
    }

    public void SetLabelVisible(bool visible)
    {
        systemNameTextField.gameObject.SetActive(
            visible && !string.IsNullOrEmpty(systemNameTextField.text)
        );
    }

    private void RenderStars(IReadOnlyList<GalaxyMapSystemStar> stars, int clusterX, int clusterY)
    {
        int count = stars?.Count ?? 0;
        for (int i = 0; i < count; i++)
        {
            GalaxyMapSystemStar star = stars[i];
            RawImage starImage = GetOrCreateImage(starImages, "Star", i);
            RawImage headquartersImage = GetOrCreateImage(headquartersImages, "Headquarters", i);

            ApplyImage(starImage, GetStarTexture(star), star.X - clusterX, star.Y - clusterY);
            ApplyImage(
                headquartersImage,
                !star.IsUnexplored && !string.IsNullOrEmpty(star.HeadquartersFactionId)
                    ? GetHeadquartersTexture(star.HeadquartersFactionId)
                    : null,
                star.X - clusterX,
                star.Y - clusterY
            );
        }

        HideUnusedImages(starImages, count);
        HideUnusedImages(headquartersImages, count);
    }

    private void RenderLabel(string labelText, bool showLabel)
    {
        systemNameTextField.text = labelText ?? string.Empty;
        systemNameTextField.gameObject.SetActive(showLabel && !string.IsNullOrEmpty(labelText));
    }

    private RawImage GetOrCreateImage(List<RawImage> images, string name, int index)
    {
        while (images.Count <= index)
        {
            RawImage template = name == "Star" ? starImageTemplate : headquartersImageTemplate;
            RawImage image = Instantiate(template, transform);
            image.name = $"{name}{images.Count}Image";
            image.raycastTarget = false;
            images.Add(image);
        }

        return images[index];
    }

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

    private Texture2D GetStarTexture(GalaxyMapSystemStar star)
    {
        if (star.IsUnexplored)
            return uiContext?.GetTexture(
                uiContext.GetTheme(_unknownThemeId)?.GalaxyBackground?.PlanetIcons?.Small
            );

        int supportIndex = GetSupportIndex(star.PopularSupport);
        PlanetIcons icons = uiContext?.GetTheme(star.OwnerFactionId)?.GalaxyBackground?.PlanetIcons;
        return uiContext?.GetTexture(GetPlanetIconPath(icons, supportIndex));
    }

    private Texture2D GetHeadquartersTexture(string factionId)
    {
        return uiContext?.GetTexture(
            uiContext.GetTheme(factionId)?.PlanetOverlayTheme?.GalaxyHeadquartersImagePath
        );
    }

    private static string GetPlanetIconPath(PlanetIcons icons, int supportIndex)
    {
        return supportIndex switch
        {
            0 => icons?.Small,
            1 => icons?.Medium ?? icons?.Small,
            2 => icons?.Large ?? icons?.Medium ?? icons?.Small,
            _ => icons?.XL ?? icons?.Large ?? icons?.Medium ?? icons?.Small,
        };
    }

    private static int GetSupportIndex(int support)
    {
        if (support > 80)
            return 3;
        if (support > 60)
            return 2;
        if (support > 50)
            return 1;
        return 0;
    }

    private static void ApplyImage(RawImage image, Texture2D texture, int x, int y)
    {
        image.texture = texture;
        image.enabled = texture != null;
        image.gameObject.SetActive(texture != null);

        if (texture == null)
            return;

        SetSourceRect(image.rectTransform, x, y, texture.width, texture.height);
    }

    private static void HideUnusedImages(List<RawImage> images, int usedCount)
    {
        for (int i = usedCount; i < images.Count; i++)
            images[i].gameObject.SetActive(false);
    }

    private static void SetSourceRect(RectTransform rect, int x, int y, int width, int height)
    {
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = new Vector2(x, -y);
        rect.sizeDelta = new Vector2(width, height);
        rect.localScale = Vector3.one;
    }

    private static RectInt GetSourceRect(RectTransform rect)
    {
        return new RectInt(
            Mathf.RoundToInt(rect.anchoredPosition.x),
            -Mathf.RoundToInt(rect.anchoredPosition.y),
            Mathf.RoundToInt(rect.sizeDelta.x),
            Mathf.RoundToInt(rect.sizeDelta.y)
        );
    }
}
