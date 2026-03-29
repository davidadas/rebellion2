using System;
using System.Collections.Generic;
using Rebellion.Game;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Displays a single PlanetSystem and its planets.
/// Responsible only for rendering and translating child view interactions
/// into higher-level panel events.
///
/// This class does NOT open menus or mutate game state.
/// Those responsibilities belong to StrategyController.
/// </summary>
public sealed class PlanetSystemPanel : MonoBehaviour
{
    [Header("References")]
    [SerializeField]
    private RectTransform planetContainer;

    [SerializeField]
    private TextMeshProUGUI systemNameText;

    [SerializeField]
    private Button closeButton;

    [SerializeField]
    private PlanetView planetPrefab;

    [Header("Layout")]
    [SerializeField]
    private float panelOffset = 90f;

    [SerializeField]
    private float padding = 60f;

    private UIContext uiContext;
    private PlanetSystem currentSystem;

    /// <summary>
    /// Raised when this panel is closed.
    /// </summary>
    public event Action<PlanetSystemPanel> OnClosed;

    /// <summary>
    /// Raised when a planet icon interaction requires higher-level handling.
    /// Controller decides what UI to open.
    /// </summary>
    public event Action<
        Planet,
        PlanetViewIconType,
        PlanetIconInteractionType
    > PlanetIconActionRequested;

    private void Awake()
    {
        closeButton.onClick.AddListener(CloseWindow);
    }

    public void Initialize(UIContext context)
    {
        if (context == null)
            throw new InvalidOperationException(
                "PlanetSystemPanel.Initialize received null UIContext."
            );

        uiContext = context;
    }

    public bool IsShowing(PlanetSystem system)
    {
        return currentSystem == system;
    }

    public string CurrentSystemInstanceID => currentSystem?.InstanceID;

    public void Show(PlanetSystem system)
    {
        if (uiContext == null)
            throw new InvalidOperationException("PlanetSystemPanel.Show called before Initialize.");

        if (system == null)
            throw new InvalidOperationException("PlanetSystemPanel.Show received null system.");

        currentSystem = system;
        gameObject.SetActive(true);

        systemNameText.text = system.DisplayName;

        BuildPlanets();

        AudioManager.Instance.PlaySFX(
            "Audio/SFX/StrategyView/sfx_strategyview_planet_system_panel_open"
        );
    }

    public void Refresh(PlanetSystem updatedSystem)
    {
        if (updatedSystem == null)
            return;

        currentSystem = updatedSystem;
        systemNameText.text = updatedSystem.DisplayName;
        BuildPlanets();
    }

    private void BuildPlanets()
    {
        ClearPlanets();

        List<Planet> planets = currentSystem.Planets;
        if (planets == null || planets.Count == 0)
            return;

        float containerWidth = planetContainer.rect.width;
        float containerHeight = planetContainer.rect.height;

        float usableWidth = containerWidth - (padding * 2f) - panelOffset;
        float usableHeight = containerHeight - (padding * 2f) - panelOffset;

        float minX = float.MaxValue;
        float maxX = float.MinValue;
        float minY = float.MaxValue;
        float maxY = float.MinValue;

        foreach (Planet planet in planets)
        {
            minX = Mathf.Min(minX, planet.PositionX);
            maxX = Mathf.Max(maxX, planet.PositionX);
            minY = Mathf.Min(minY, planet.PositionY);
            maxY = Mathf.Max(maxY, planet.PositionY);
        }

        float rangeX = Mathf.Max(1f, maxX - minX);
        float rangeY = Mathf.Max(1f, maxY - minY);

        foreach (Planet planet in planets)
        {
            float normalizedX = (planet.PositionX - minX) / rangeX;
            float normalizedY = (planet.PositionY - minY) / rangeY;

            float x = padding + (normalizedX * usableWidth);
            float y = padding + (normalizedY * usableHeight);

            PlanetView view = Instantiate(planetPrefab, planetContainer);

            RectTransform rect = view.GetComponent<RectTransform>();

            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.zero;
            rect.pivot = new Vector2(0f, 0f);

            float scale = 150f / 297f;
            rect.localScale = new Vector3(scale, scale, 1f);
            rect.anchoredPosition = new Vector2(x, y);

            view.Initialize(planet, uiContext);

            // Unified bubbling
            view.PlanetIconInteracted += (iconType, interactionType) =>
            {
                PlanetIconActionRequested?.Invoke(planet, iconType, interactionType);
            };
        }
    }

    private void ClearPlanets()
    {
        for (int i = planetContainer.childCount - 1; i >= 0; i--)
        {
            Destroy(planetContainer.GetChild(i).gameObject);
        }
    }

    private void CloseWindow()
    {
        OnClosed?.Invoke(this);
        AudioManager.Instance.PlaySFX(
            "Audio/SFX/StrategyView/sfx_strategyview_planet_system_panel_close"
        );
    }
}
