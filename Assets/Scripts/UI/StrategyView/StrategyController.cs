using System.Collections.Generic;
using Rebellion.Game;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class StrategyController : MonoBehaviour
{
    [SerializeField]
    private Image factionBackground;

    [SerializeField]
    private Image galaxyBackground;

    [SerializeField]
    private RectTransform mapViewport;

    [SerializeField]
    private RectTransform planetSystemPanelsLayer;

    [SerializeField]
    private RectTransform planetPanelsLayer;

    [SerializeField]
    private GalaxyView galaxyView;

    [SerializeField]
    private PlanetSystemPanel planetSystemPanelPrefab;

    [SerializeField]
    private PlanetPanel planetPanelPrefab;

    // HUD text
    [SerializeField]
    private TextMeshProUGUI tickCounterText;

    [SerializeField]
    private TextMeshProUGUI rawMaterialsText;

    [SerializeField]
    private TextMeshProUGUI refinedMaterialsText;

    [SerializeField]
    private TextMeshProUGUI maintenanceText;

    private GameManager gameManager;
    private UIContext uiContext;

    private readonly List<PlanetSystemPanel> activePanels = new();
    private readonly List<PlanetPanel> activePlanetPanels = new();

    private readonly string[] tracks =
    {
        "Audio/Music/battle_of_endor_medley_2",
        "Audio/Music/main_title_death_star_tatooine_emperor",
        "Audio/Music/emperor_arrives_death_of_yoda_obi_wan_revelation_stinger",
        "Audio/Music/emperor_arrives_death_of_yoda_obi_wan_revelation",
        "Audio/Music/imperial_march",
        "Audio/Music/battle_of_hoth_medley",
    };

    public void Initialize(GameManager gameManager, UIContext uiContext)
    {
        if (gameManager == null)
            throw new GameException("StrategyController.Initialize received null GameManager.");

        if (uiContext == null)
            throw new GameException("StrategyController.Initialize received null UIContext.");

        if (galaxyView == null)
            throw new GameException("StrategyController missing GalaxyView reference.");

        if (this.gameManager != null)
            throw new GameException("StrategyController.Initialize called twice.");

        this.gameManager = gameManager;
        this.uiContext = uiContext;

        OnGameReady();
    }

    private void OnGameReady()
    {
        gameManager.SetGameSpeed(TickSpeed.Paused);

        AudioManager.Instance.PlaySFX("Audio/SFX/StrategyView/sfx_strategyview_open");
        AudioManager.Instance.PlayPlaylistPaths(tracks, true);

        ApplyFactionUI();

        // Build faction-specific galaxy view using fog of war system
        GalaxyMap galaxy = gameManager
            .GetFogOfWarSystem()
            .BuildFactionView(gameManager.GetPlayerFaction());

        galaxyView.OnSystemSelected -= HandleSystemSelected;
        galaxyView.OnSystemOpened -= HandleSystemOpened;

        galaxyView.OnSystemSelected += HandleSystemSelected;
        galaxyView.OnSystemOpened += HandleSystemOpened;

        galaxyView.Initialize(galaxy, uiContext);
    }

    /// <summary>
    /// Rebuilds the galaxy view from fog of war state.
    /// Called when visibility changes (fleet arrival/departure, espionage, ownership change).
    /// </summary>
    public void RefreshGalaxyView()
    {
        if (gameManager == null || galaxyView == null)
            return;

        GalaxyMap updatedGalaxy = gameManager
            .GetFogOfWarSystem()
            .BuildFactionView(gameManager.GetPlayerFaction());

        galaxyView.Initialize(updatedGalaxy, uiContext);
    }

    private void Update()
    {
        if (gameManager == null)
            return;

        int previousTick = gameManager.GetCurrentTick();
        gameManager.Update();
        int currentTick = gameManager.GetCurrentTick();

        // Refresh galaxy view if game tick advanced (visibility may have changed)
        if (currentTick != previousTick)
        {
            RefreshGalaxyView();
        }

        UpdateHUDValues();
    }

    private void ApplyFactionUI()
    {
        Faction faction = gameManager.GetPlayerFaction();
        if (faction == null)
            throw new GameException("Player faction missing.");

        FactionTheme theme = uiContext.GetPlayerFactionTheme();

        ApplyMainBackground(theme);
        ApplyGalaxyBackground(theme.GalaxyBackground);
        ApplyHUDLayouts(theme);
    }

    private void ApplyMainBackground(FactionTheme theme)
    {
        if (theme == null || string.IsNullOrEmpty(theme.TacticalHUDLayout.ImagePath))
            throw new GameException("FactionTheme missing TacticalHUDLayout ImagePath.");

        Sprite background = ResourceManager.Instance.GetSprite(theme.TacticalHUDLayout.ImagePath);
        factionBackground.sprite = background;
    }

    private void ApplyGalaxyBackground(GalaxyBackground galaxy)
    {
        if (galaxy == null)
            throw new GameException("GalaxyBackground missing.");

        if (string.IsNullOrEmpty(galaxy.ImagePath))
            throw new GameException("GalaxyBackground missing ImagePath.");

        Sprite sprite = ResourceManager.Instance.GetSprite(galaxy.ImagePath);
        galaxyBackground.sprite = sprite;

        galaxy.ImageLayout.Apply(galaxyBackground.rectTransform);
        galaxy.MapViewportLayout.Apply(mapViewport);

        SetGalaxyDimmed(true);
    }

    private void ApplyHUDLayouts(FactionTheme theme)
    {
        TacticalHUDLayout hud = theme.TacticalHUDLayout;
        Color factionColor = theme.GetPrimaryColor();

        ApplyTextLayout(tickCounterText, hud.TickCounterTextLayout, factionColor);
        ApplyTextLayout(rawMaterialsText, hud.RawMaterialsTextLayout, factionColor);
        ApplyTextLayout(refinedMaterialsText, hud.RefinedMaterialsTextLayout, factionColor);
        ApplyTextLayout(maintenanceText, hud.MaintenanceTextLayout, factionColor);
    }

    private void ApplyTextLayout(TextMeshProUGUI text, RectLayout layout, Color color)
    {
        if (text == null)
            return;

        RectTransform rect = text.GetComponent<RectTransform>();

        layout?.Apply(rect);

        text.color = color;
    }

    private void UpdateHUDValues()
    {
        Faction faction = gameManager.GetPlayerFaction();
        GameRoot game = gameManager.GetGame();

        tickCounterText.text = gameManager.GetCurrentTick().ToString();

        rawMaterialsText.text = game.GetRawMaterials(faction).ToString();

        refinedMaterialsText.text = game.GetRefinedMaterials(faction).ToString();

        maintenanceText.text = faction.GetTotalUnitCost().ToString();
    }

    private void HandleSystemSelected(PlanetSystem system)
    {
        // TODO: show system details in side panel instead of opening system panel on single click.
    }

    private void HandleSystemOpened(PlanetSystem system)
    {
        if (system == null)
            return;

        if (activePanels.Count >= 3)
            return;

        foreach (PlanetSystemPanel panel in activePanels)
        {
            if (panel.IsShowing(system))
            {
                panel.transform.SetAsLastSibling();
                return;
            }
        }

        PlanetSystemPanel newPanel = Instantiate(
            planetSystemPanelPrefab,
            planetSystemPanelsLayer,
            false
        );

        newPanel.Initialize(uiContext);

        newPanel.OnClosed += HandlePanelClosed;
        newPanel.PlanetIconActionRequested += HandlePlanetIconActionRequested;

        RectTransform rect = newPanel.GetComponent<RectTransform>();
        DockPanelToSlot(rect, activePanels.Count);

        activePanels.Add(newPanel);
        newPanel.Show(system);
    }

    private void HandlePlanetIconActionRequested(
        Planet planet,
        PlanetViewIconType iconType,
        PlanetIconInteractionType interactionType
    )
    {
        if (planet == null)
            return;

        if (interactionType == PlanetIconInteractionType.DoubleClick)
            OpenPlanetWindow(planet, iconType);
    }

    private void OpenPlanetWindow(Planet planet, PlanetViewIconType initialView)
    {
        if (planet == null)
            return;

        foreach (PlanetPanel panel in activePlanetPanels)
        {
            if (panel == null)
                continue;

            if (panel.IsShowing(planet, initialView))
            {
                panel.transform.SetAsLastSibling();
                return;
            }
        }

        PlanetPanel newPanel = Instantiate(planetPanelPrefab, planetPanelsLayer, false);

        newPanel.Initialize(planet, uiContext, initialView);
        newPanel.ConfigureDrag(mapViewport);

        newPanel.transform.SetAsLastSibling();

        newPanel.OnClosed += HandlePlanetPanelClosed;

        activePlanetPanels.Add(newPanel);
    }

    private void DockPanelToSlot(RectTransform panelRect, int slotIndex)
    {
        const float gap = 0.003f;
        const int panelCount = 3;

        float totalGapWidth = gap * (panelCount - 1);
        float usableWidth = 1f - totalGapWidth;
        float panelWidth = usableWidth / panelCount;

        float minX = slotIndex * (panelWidth + gap);
        float maxX = minX + panelWidth;

        panelRect.anchorMin = new Vector2(minX, 0f);
        panelRect.anchorMax = new Vector2(maxX, 1f);
        panelRect.offsetMin = Vector2.zero;
        panelRect.offsetMax = Vector2.zero;
        panelRect.pivot = new Vector2(0.5f, 0.5f);
    }

    public void SetGalaxyDimmed(bool dimmed)
    {
        galaxyBackground.color = dimmed ? new Color(0.35f, 0.4f, 0.5f, 1f) : Color.white;
    }

    private void HandlePanelClosed(PlanetSystemPanel panel)
    {
        if (panel == null)
            return;

        panel.PlanetIconActionRequested -= HandlePlanetIconActionRequested;
        panel.OnClosed -= HandlePanelClosed;

        if (activePanels.Contains(panel))
            activePanels.Remove(panel);

        Destroy(panel.gameObject);

        for (int i = 0; i < activePanels.Count; i++)
        {
            RectTransform rect = activePanels[i].GetComponent<RectTransform>();
            DockPanelToSlot(rect, i);
        }
    }

    private void HandlePlanetPanelClosed(PlanetPanel panel)
    {
        if (panel == null)
            return;

        if (activePlanetPanels.Contains(panel))
            activePlanetPanels.Remove(panel);
    }
}
