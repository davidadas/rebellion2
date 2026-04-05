using System;
using System.Collections.Generic;
using Rebellion.Game;
using UnityEngine;
using UnityEngine.UI;

public sealed class FleetsPane : MonoBehaviour
{
    private enum FleetTabType
    {
        CapitalShips,
        Starfighters,
        Regiments,
        Officers,
    }

    [Header("Grids")]
    [SerializeField]
    private UnitGridPane fleetsGrid;

    [SerializeField]
    private UnitGridPane garrisonGrid;

    [Header("Faction Tinted UI Elements")]
    [SerializeField]
    private Graphic[] factionTintedGraphics;

    [Header("Fleet Header")]
    [SerializeField]
    private TMPro.TMP_Text fleetNameText;

    [SerializeField]
    private Image fleetImage;

    [Header("Fleet Tabs")]
    [SerializeField]
    private IconButton capitalShipsTab;

    [SerializeField]
    private IconButton starfightersTab;

    [SerializeField]
    private IconButton regimentsTab;

    [SerializeField]
    private IconButton officersTab;

    private Button capitalShipsButton;
    private Button starfightersButton;
    private Button regimentsButton;
    private Button officersButton;

    private Planet planet;
    private UIContext uiContext;

    private UnitTile selectedTile;
    private FleetTabType activeTab;

    public void Initialize(Planet planet, UIContext uiContext)
    {
        if (planet == null)
            throw new ArgumentNullException(nameof(planet));

        if (uiContext == null)
            throw new ArgumentNullException(nameof(uiContext));

        if (fleetsGrid == null || garrisonGrid == null)
            throw new InvalidOperationException("FleetsPane missing grid references.");

        this.planet = planet;
        this.uiContext = uiContext;

        fleetsGrid.Initialize(uiContext);
        garrisonGrid.Initialize(uiContext);

        fleetsGrid.TileClicked += HandleFleetTileClicked;

        capitalShipsButton = capitalShipsTab.GetButton();
        starfightersButton = starfightersTab.GetButton();
        regimentsButton = regimentsTab.GetButton();
        officersButton = officersTab.GetButton();

        capitalShipsButton.onClick.AddListener(() => SelectTab(FleetTabType.CapitalShips));
        starfightersButton.onClick.AddListener(() => SelectTab(FleetTabType.Starfighters));
        regimentsButton.onClick.AddListener(() => SelectTab(FleetTabType.Regiments));
        officersButton.onClick.AddListener(() => SelectTab(FleetTabType.Officers));

        SelectTab(FleetTabType.CapitalShips);

        Render();
    }

    private void SelectTab(FleetTabType tab)
    {
        activeTab = tab;

        capitalShipsTab.SetSelected(false);
        starfightersTab.SetSelected(false);
        regimentsTab.SetSelected(false);
        officersTab.SetSelected(false);

        switch (tab)
        {
            case FleetTabType.CapitalShips:
                capitalShipsTab.SetSelected(true);
                break;

            case FleetTabType.Starfighters:
                starfightersTab.SetSelected(true);
                break;

            case FleetTabType.Regiments:
                regimentsTab.SetSelected(true);
                break;

            case FleetTabType.Officers:
                officersTab.SetSelected(true);
                break;
        }

        if (selectedTile?.Entity is Fleet fleet)
            ShowFleetContents(fleet);
    }

    private void ApplyFleetTheme(Fleet fleet)
    {
        if (fleet == null)
            return;

        string ownerId = fleet.GetOwnerInstanceID();

        if (string.IsNullOrEmpty(ownerId))
            throw new InvalidOperationException("Fleet missing owner instance ID.");

        FactionTheme theme = uiContext.GetTheme(ownerId);

        if (theme == null)
            throw new InvalidOperationException($"FactionTheme missing for owner '{ownerId}'.");

        ApplyFactionTint(theme);
        ApplyFleetImage(theme);
        ApplyFleetTabSprites(theme);
    }

    private void ApplyFactionTint(FactionTheme theme)
    {
        Color factionColor = theme.GetPrimaryColor();

        if (factionTintedGraphics == null)
            return;

        foreach (Graphic graphic in factionTintedGraphics)
        {
            if (graphic != null)
                graphic.color = factionColor;
        }
    }

    private void ApplyFleetImage(FactionTheme theme)
    {
        if (fleetImage == null)
            return;

        string path = theme.GetFleetsPaneImagePath();

        if (string.IsNullOrEmpty(path))
        {
            fleetImage.sprite = null;
            return;
        }

        fleetImage.sprite = ResourceManager.Instance.GetSprite(path);
    }

    private void ApplyFleetTabSprites(FactionTheme theme)
    {
        FleetTabsTheme tabs = theme?.GetFleetTabsTheme();

        if (tabs == null)
            return;

        ApplyTabSprites(capitalShipsTab, tabs.CapitalShips);
        ApplyTabSprites(starfightersTab, tabs.Starfighters);
        ApplyTabSprites(regimentsTab, tabs.Regiments);
        ApplyTabSprites(officersTab, tabs.Officers);
    }

    private void ApplyTabSprites(IconButton button, FleetTabIconSet tab)
    {
        if (button == null || tab == null)
            return;

        Sprite normal = TryLoad(tab.NormalImagePath);
        Sprite selected = TryLoad(tab.SelectedImagePath);
        Sprite disabled = TryLoad(tab.DisabledImagePath);

        if (disabled == null)
            disabled = normal;

        button.SetSprites(
            normalSprite: normal,
            hoverSprite: normal,
            pressedSprite: normal,
            disabledSprite: disabled,
            selectedSprite: selected
        );
    }

    private Sprite TryLoad(string path)
    {
        if (string.IsNullOrEmpty(path))
            return null;

        return ResourceManager.Instance.GetSprite(path);
    }

    public void Refresh()
    {
        Render();
    }

    private void Render()
    {
        fleetsGrid.Clear();
        garrisonGrid.Clear();
        selectedTile = null;

        List<Fleet> fleets = planet.GetFleets();

        foreach (Fleet fleet in fleets)
            fleetsGrid.AddTile(fleet);

        UnitTile firstTile = fleetsGrid.GetTileAt(0);

        if (firstTile != null)
            HandleFleetTileClicked(firstTile);
        else
            ClearFleetHeader();
    }

    private void HandleFleetTileClicked(UnitTile tile)
    {
        if (tile == null)
            return;

        if (selectedTile == tile)
            return;

        if (selectedTile != null)
            selectedTile.SetSelected(false);

        selectedTile = tile;
        selectedTile.SetSelected(true);

        if (tile.Entity is Fleet fleet)
        {
            ApplyFleetTheme(fleet);

            UpdateFleetTabs(fleet);
            ShowFleetContents(fleet);

            fleetNameText.text = fleet.GetDisplayName();
        }
        else
        {
            garrisonGrid.Clear();
            ClearFleetHeader();
        }
    }

    private void ClearFleetHeader()
    {
        if (fleetNameText != null)
            fleetNameText.text = "";

        if (fleetImage != null)
            fleetImage.sprite = null;
    }

    private void UpdateFleetTabs(Fleet fleet)
    {
        bool hasShips = fleet.CapitalShips.Count > 0;

        int fighterCount = 0;
        int regimentCount = 0;
        int officerCount = 0;

        foreach (var ship in fleet.CapitalShips)
        {
            fighterCount += ship.Starfighters.Count;
            regimentCount += ship.Regiments.Count;
            officerCount += ship.Officers.Count;
        }

        bool hasFighters = fighterCount > 0;
        bool hasRegiments = regimentCount > 0;
        bool hasOfficers = officerCount > 0;

        capitalShipsButton.interactable = hasShips;
        starfightersButton.interactable = hasFighters;
        regimentsButton.interactable = hasRegiments;
        officersButton.interactable = hasOfficers;

        bool activeValid =
            (activeTab == FleetTabType.CapitalShips && hasShips)
            || (activeTab == FleetTabType.Starfighters && hasFighters)
            || (activeTab == FleetTabType.Regiments && hasRegiments)
            || (activeTab == FleetTabType.Officers && hasOfficers);

        if (!activeValid)
        {
            if (hasShips)
                SelectTab(FleetTabType.CapitalShips);
            else if (hasFighters)
                SelectTab(FleetTabType.Starfighters);
            else if (hasRegiments)
                SelectTab(FleetTabType.Regiments);
            else if (hasOfficers)
                SelectTab(FleetTabType.Officers);
        }
    }

    private void ShowFleetContents(Fleet fleet)
    {
        garrisonGrid.Clear();

        if (fleet == null)
            return;

        switch (activeTab)
        {
            case FleetTabType.CapitalShips:
                foreach (var ship in fleet.CapitalShips)
                    garrisonGrid.AddTile(ship);
                break;

            case FleetTabType.Starfighters:
                foreach (var fighter in fleet.GetStarfighters())
                    garrisonGrid.AddTile(fighter);
                break;

            case FleetTabType.Regiments:
                foreach (var regiment in fleet.GetRegiments())
                    garrisonGrid.AddTile(regiment);
                break;

            case FleetTabType.Officers:
                foreach (var officer in fleet.GetOfficers())
                    garrisonGrid.AddTile(officer);
                break;
        }
    }

    private void OnDestroy()
    {
        if (fleetsGrid != null)
            fleetsGrid.TileClicked -= HandleFleetTileClicked;
    }
}
