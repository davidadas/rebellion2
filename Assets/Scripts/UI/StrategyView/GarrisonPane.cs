using System;
using Rebellion.Game;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class GarrisonPane : MonoBehaviour
{
    private enum GarrisonTabType
    {
        Officers,
        Starfighters,
        Regiments,
        Shields,
        Weapons,
    }

    [Header("Grid")]
    [SerializeField]
    private UnitGridPane garrisonGrid;

    [Header("Section Header")]
    [SerializeField]
    private TMP_Text sectionNameText;

    [Header("Tabs")]
    [SerializeField]
    private IconButton officersTab;

    [SerializeField]
    private IconButton starfightersTab;

    [SerializeField]
    private IconButton regimentsTab;

    [SerializeField]
    private IconButton shieldsTab;

    [SerializeField]
    private IconButton weaponsTab;

    private Button officersButton;
    private Button starfightersButton;
    private Button regimentsButton;
    private Button shieldsButton;
    private Button weaponsButton;

    private Planet planet;
    private UIContext uiContext;

    private GarrisonTabType activeTab;

    public void Initialize(Planet planet, UIContext uiContext)
    {
        if (planet == null)
            throw new ArgumentNullException(nameof(planet));

        if (uiContext == null)
            throw new ArgumentNullException(nameof(uiContext));

        if (garrisonGrid == null)
            throw new InvalidOperationException("GarrisonPane missing grid reference.");

        this.planet = planet;
        this.uiContext = uiContext;

        garrisonGrid.Initialize(uiContext);

        officersButton = officersTab.GetComponent<Button>();
        starfightersButton = starfightersTab.GetComponent<Button>();
        regimentsButton = regimentsTab.GetComponent<Button>();
        shieldsButton = shieldsTab.GetComponent<Button>();
        weaponsButton = weaponsTab.GetComponent<Button>();

        ApplyThemeTabSprites();

        officersButton.onClick.AddListener(() => SelectTab(GarrisonTabType.Officers));
        starfightersButton.onClick.AddListener(() => SelectTab(GarrisonTabType.Starfighters));
        regimentsButton.onClick.AddListener(() => SelectTab(GarrisonTabType.Regiments));
        shieldsButton.onClick.AddListener(() => SelectTab(GarrisonTabType.Shields));
        weaponsButton.onClick.AddListener(() => SelectTab(GarrisonTabType.Weapons));

        SelectTab(GarrisonTabType.Officers);
    }

    private void ApplyThemeTabSprites()
    {
        string ownerId = planet.GetOwnerInstanceID();

        if (string.IsNullOrEmpty(ownerId))
            return;

        FactionTheme theme = uiContext.GetTheme(ownerId);

        if (theme == null)
            return;

        GarrisonPanelTheme panel = theme?.PlanetWindowTheme?.GarrisonPanel;

        if (panel == null)
            return;

        ApplyTabSprites(officersTab, panel.Officers);
        ApplyTabSprites(starfightersTab, panel.Starfighters);
        ApplyTabSprites(regimentsTab, panel.Regiments);
        ApplyTabSprites(shieldsTab, panel.Shields);
        ApplyTabSprites(weaponsTab, panel.Weapons);
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

        return ResourceManager.GetSprite(path);
    }

    private void SelectTab(GarrisonTabType tab)
    {
        activeTab = tab;

        officersTab.SetSelected(false);
        starfightersTab.SetSelected(false);
        regimentsTab.SetSelected(false);
        shieldsTab.SetSelected(false);
        weaponsTab.SetSelected(false);

        switch (tab)
        {
            case GarrisonTabType.Officers:
                officersTab.SetSelected(true);
                break;

            case GarrisonTabType.Starfighters:
                starfightersTab.SetSelected(true);
                break;

            case GarrisonTabType.Regiments:
                regimentsTab.SetSelected(true);
                break;

            case GarrisonTabType.Shields:
                shieldsTab.SetSelected(true);
                break;

            case GarrisonTabType.Weapons:
                weaponsTab.SetSelected(true);
                break;
        }

        if (sectionNameText != null)
            sectionNameText.text = GetTabDisplayText(tab);

        Render();
    }

    private string GetTabDisplayText(GarrisonTabType tab)
    {
        switch (tab)
        {
            case GarrisonTabType.Officers:
                return officersTab.GetDisplayText();

            case GarrisonTabType.Starfighters:
                return starfightersTab.GetDisplayText();

            case GarrisonTabType.Regiments:
                return regimentsTab.GetDisplayText();

            case GarrisonTabType.Shields:
                return shieldsTab.GetDisplayText();

            case GarrisonTabType.Weapons:
                return weaponsTab.GetDisplayText();
        }

        return "";
    }

    public void Refresh()
    {
        Render();
    }

    private void Render()
    {
        garrisonGrid.Clear();

        switch (activeTab)
        {
            case GarrisonTabType.Officers:
                foreach (Officer officer in planet.GetAllOfficers())
                    garrisonGrid.AddTile(officer);
                break;

            case GarrisonTabType.Starfighters:
                foreach (Starfighter fighter in planet.GetAllStarfighters())
                    garrisonGrid.AddTile(fighter);
                break;

            case GarrisonTabType.Regiments:
                foreach (Regiment regiment in planet.GetAllRegiments())
                    garrisonGrid.AddTile(regiment);
                break;

            case GarrisonTabType.Shields:
                foreach (Building building in planet.GetAllBuildings())
                {
                    if (building.GetBuildingType() == BuildingType.Defense)
                        garrisonGrid.AddTile(building);
                }
                break;

            case GarrisonTabType.Weapons:
                foreach (Building building in planet.GetAllBuildings())
                {
                    if (building.GetBuildingType() == BuildingType.Weapon)
                        garrisonGrid.AddTile(building);
                }
                break;
        }

        UpdateTabStates();
    }

    private void UpdateTabStates()
    {
        officersButton.interactable = planet.GetOfficerCount() > 0;
        starfightersButton.interactable = planet.GetStarfighterCount() > 0;
        regimentsButton.interactable = planet.GetRegimentCount() > 0;

        shieldsButton.interactable =
            planet.GetBuildingTypeCount(BuildingType.Defense, EntityStateFilter.All) > 0;

        weaponsButton.interactable =
            planet.GetBuildingTypeCount(BuildingType.Weapon, EntityStateFilter.All) > 0;
    }
}
