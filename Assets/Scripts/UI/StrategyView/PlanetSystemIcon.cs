using System;
using System.Linq;
using Rebellion.Game;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Displays a single PlanetSystem inside the Galaxy map.
/// </summary>
public sealed class PlanetSystemIcon
    : MonoBehaviour,
        IPointerClickHandler,
        IPointerEnterHandler,
        IPointerExitHandler
{
    private PlanetSystem system;
    private UIContext context;
    private Transform rootLayer;

    private GameObject hoverOverlay;

    private float lastClickTime;
    private const float DoubleClickThreshold = 0.25f;

    public event Action<PlanetSystem> OnClicked;
    public event Action<PlanetSystem> OnDoubleClicked;

    /// <summary>
    /// Initializes this view.
    /// </summary>
    public void Initialize(PlanetSystem system, UIContext context, Transform rootLayer)
    {
        if (system == null)
            throw new ArgumentNullException(nameof(system));

        if (context == null)
            throw new ArgumentNullException(nameof(context));

        if (rootLayer == null)
            throw new ArgumentNullException(nameof(rootLayer));

        this.system = system;
        this.context = context;
        this.rootLayer = rootLayer;

        BuildContainer();
        BuildPlanets();
        BuildHoverOverlay();
    }

    /// <summary>
    /// Builds the invisible clickable container for the system.
    /// </summary>
    private void BuildContainer()
    {
        if (system.Planets == null || system.Planets.Count == 0)
            return;

        float minX = system.Planets.Min(p => p.PositionX);
        float maxX = system.Planets.Max(p => p.PositionX);
        float minY = system.Planets.Min(p => p.PositionY);
        float maxY = system.Planets.Max(p => p.PositionY);

        float padding = 30f;

        float width = (maxX - minX) + padding;
        float height = (maxY - minY) + padding;

        float centerX = (minX + maxX) * 0.5f;
        float centerY = (minY + maxY) * 0.5f;

        RectTransform rect = gameObject.AddComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.zero;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = new Vector2(centerX, centerY);
        rect.sizeDelta = new Vector2(Mathf.Max(width, height), Mathf.Max(width, height));

        Image baseImage = gameObject.AddComponent<Image>();
        baseImage.color = new Color(0, 0, 0, 0);
        baseImage.raycastTarget = true;
    }

    /// <summary>
    /// Builds the planet icons belonging to this system.
    /// </summary>
    private void BuildPlanets()
    {
        foreach (Planet planet in system.Planets)
        {
            GameObject go = new GameObject(planet.DisplayName);
            go.transform.SetParent(rootLayer, false);

            PlanetIcon icon = go.AddComponent<PlanetIcon>();
            icon.Initialize(planet, context);
        }
    }

    /// <summary>
    /// Builds the hover overlay.
    /// </summary>
    private void BuildHoverOverlay()
    {
        hoverOverlay = new GameObject("HoverOverlay");
        hoverOverlay.transform.SetParent(transform, false);

        RectTransform rect = hoverOverlay.AddComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        Image hoverImage = hoverOverlay.AddComponent<Image>();
        hoverImage.sprite = ResourceManager.GetSprite("Art/UI/StrategyView/planet_system_hover");

        hoverImage.raycastTarget = false;

        hoverOverlay.SetActive(false);
    }

    /// <summary>
    /// Handles pointer click interaction.
    /// </summary>
    public void OnPointerClick(PointerEventData eventData)
    {
        if (eventData.button != PointerEventData.InputButton.Left)
            return;

        float now = Time.unscaledTime;

        if (now - lastClickTime <= DoubleClickThreshold)
        {
            OnDoubleClicked?.Invoke(system);
        }
        else
            OnClicked?.Invoke(system);

        lastClickTime = now;
    }

    /// <summary>
    /// Enables hover overlay on pointer enter.
    /// </summary>
    public void OnPointerEnter(PointerEventData eventData)
    {
        hoverOverlay.SetActive(true);
    }

    /// <summary>
    /// Disables hover overlay on pointer exit.
    /// </summary>
    public void OnPointerExit(PointerEventData eventData)
    {
        hoverOverlay.SetActive(false);
    }
}
