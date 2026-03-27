using System;
using System.Collections.Generic;
using Rebellion.Game;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class BuildingsPane : MonoBehaviour
{
    [Header("Grid")]
    [SerializeField]
    private UnitGridPane buildingsGrid;

    [Header("Section Header")]
    [SerializeField]
    private TMP_Text sectionNameText;

    [SerializeField]
    private List<Image> sectionHeaderImages = new();

    [Header("Tabs")]
    [SerializeField]
    private TabGroup tabGroup;

    [SerializeField]
    private IconButton productionTab;

    [SerializeField]
    private IconButton shipyardsTab;

    [SerializeField]
    private IconButton trainingFacilitiesTab;

    [SerializeField]
    private IconButton constructionFacilitiesTab;

    [SerializeField]
    private IconButton refineriesTab;

    [SerializeField]
    private IconButton minesTab;

    private Planet planet;
    private UIContext uiContext;

    private BuildingType activeTab;

    public void Initialize(Planet planet, UIContext uiContext)
    {
        if (planet == null)
            throw new ArgumentNullException(nameof(planet));

        if (uiContext == null)
            throw new ArgumentNullException(nameof(uiContext));

        if (buildingsGrid == null)
            throw new InvalidOperationException("BuildingsPane missing grid reference.");

        this.planet = planet;
        this.uiContext = uiContext;

        buildingsGrid.Initialize(uiContext);

        ApplyTheme();

        tabGroup.TabSelected -= HandleTabSelected;
        tabGroup.TabSelected += HandleTabSelected;

        UpdateTabStates();
    }

    private void ApplyTheme()
    {
        FactionTheme theme = uiContext.GetTheme(planet.GetOwnerInstanceID());
        BuildingsPaneTheme paneTheme = theme?.GetBuildingsPaneTheme();

        if (paneTheme == null)
            return;

        ApplyHeaderTheme(paneTheme);
        ApplyProductionTabTheme(paneTheme);
    }

    private void ApplyHeaderTheme(BuildingsPaneTheme paneTheme)
    {
        string path = paneTheme?.ConstructionHeader?.ImagePath;

        if (string.IsNullOrEmpty(path))
            return;

        Sprite sprite = ResourceManager.Instance.GetSprite(path);

        if (sprite == null)
            return;

        foreach (Image img in sectionHeaderImages)
        {
            if (img != null)
                img.sprite = sprite;
        }
    }

    private void ApplyProductionTabTheme(BuildingsPaneTheme paneTheme)
    {
        FleetTabIconSet tab = paneTheme?.BuildingsTabs?.Production;

        if (tab == null || productionTab == null)
            return;

        Sprite normal = ResourceManager.Instance.GetSprite(tab.NormalImagePath);
        Sprite selected = ResourceManager.Instance.GetSprite(tab.SelectedImagePath);

        Sprite disabled = null;

        if (!string.IsNullOrEmpty(tab.DisabledImagePath))
            disabled = ResourceManager.Instance.GetSprite(tab.DisabledImagePath);

        productionTab.SetSprites(
            normalSprite: normal,
            hoverSprite: normal,
            pressedSprite: normal,
            disabledSprite: disabled,
            selectedSprite: selected
        );
    }

    private void HandleTabSelected(int index)
    {
        switch (index)
        {
            case 0:
                activeTab = BuildingType.None;
                SetHeader(productionTab);
                break;

            case 1:
                activeTab = BuildingType.Shipyard;
                SetHeader(shipyardsTab);
                break;

            case 2:
                activeTab = BuildingType.TrainingFacility;
                SetHeader(trainingFacilitiesTab);
                break;

            case 3:
                activeTab = BuildingType.ConstructionFacility;
                SetHeader(constructionFacilitiesTab);
                break;

            case 4:
                activeTab = BuildingType.Refinery;
                SetHeader(refineriesTab);
                break;

            case 5:
                activeTab = BuildingType.Mine;
                SetHeader(minesTab);
                break;
        }

        Render();
    }

    private void SetHeader(IconButton tab)
    {
        if (sectionNameText != null && tab != null)
            sectionNameText.text = tab.GetDisplayText();
    }

    private void Render()
    {
        buildingsGrid.Clear();

        List<Building> buildings = planet.GetAllBuildings();

        foreach (Building building in buildings)
        {
            if (activeTab == BuildingType.None || building.GetBuildingType() == activeTab)
                buildingsGrid.AddTile(building);
        }

        UpdateTabStates();
    }

    private void UpdateTabStates()
    {
        productionTab.SetDisabled(false);

        shipyardsTab.SetDisabled(planet.GetBuildingTypeCount(BuildingType.Shipyard) == 0);

        trainingFacilitiesTab.SetDisabled(
            planet.GetBuildingTypeCount(BuildingType.TrainingFacility) == 0
        );

        constructionFacilitiesTab.SetDisabled(
            planet.GetBuildingTypeCount(BuildingType.ConstructionFacility) == 0
        );

        refineriesTab.SetDisabled(planet.GetBuildingTypeCount(BuildingType.Refinery) == 0);

        minesTab.SetDisabled(planet.GetBuildingTypeCount(BuildingType.Mine) == 0);
    }
}
