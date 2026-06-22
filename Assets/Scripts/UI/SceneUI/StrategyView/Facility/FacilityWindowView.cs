using System;
using System.Collections.Generic;
using System.Linq;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Units;
using Rebellion.SceneGraph;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public sealed class FacilityWindowView
    : MonoBehaviour,
        IStrategyUIRuntimeReceiver,
        IStrategyWindowSelectionView,
        IStrategyWindowStatusTargetView,
        IStrategyWindowScrapItemsView,
        IConstructionWindowControllerReceiver,
        IStrategyWindowContent,
        IPlanetIconWindowView
{
    private const int _manufacturingTab = 0;
    private const int _shipyardsTab = 1;
    private const int _trainingTab = 2;
    private const int _constructionTab = 3;
    private const int _refineriesTab = 4;
    private const int _minesTab = 5;
    private const string _rawResourceNodeTexturePath =
        "Art/UI/StrategyView/ui_strategyview_raw_materials";

    private readonly HashSet<int> selectedCards = new HashSet<int>();
    private readonly HashSet<int> selectedItems = new HashSet<int>();
    private readonly List<FacilityInventoryItemView> inventoryItemViews =
        new List<FacilityInventoryItemView>();
    private int contextManufacturingCard = -1;
    private int contextInventoryItem = -1;
    private bool eventsBound;

    [SerializeField]
    private RawImage backgroundImage;

    [SerializeField]
    private RawImage titleImage;

    [SerializeField]
    private Texture2D backgroundTexture;

    [SerializeField]
    private TextMeshProUGUI captionTextField;

    [SerializeField]
    private RawImage[] buttonImages = System.Array.Empty<RawImage>();

    [SerializeField]
    private int[] buttonActions = System.Array.Empty<int>();

    [SerializeField]
    private Texture2D openSectorButtonUpTexture;

    [SerializeField]
    private Texture2D openSectorButtonDownTexture;

    [SerializeField]
    private Texture2D minimizeButtonUpTexture;

    [SerializeField]
    private Texture2D minimizeButtonDownTexture;

    [SerializeField]
    private Texture2D closeButtonUpTexture;

    [SerializeField]
    private Texture2D closeButtonDownTexture;

    [SerializeField]
    private RawImage[] tabImages = System.Array.Empty<RawImage>();

    [SerializeField]
    private Button[] tabButtons = System.Array.Empty<Button>();

    [SerializeField]
    private Texture2D shipyardTabActiveTexture;

    [SerializeField]
    private Texture2D shipyardTabInactiveTexture;

    [SerializeField]
    private Texture2D shipyardTabDisabledTexture;

    [SerializeField]
    private Texture2D troopTabActiveTexture;

    [SerializeField]
    private Texture2D troopTabInactiveTexture;

    [SerializeField]
    private Texture2D troopTabDisabledTexture;

    [SerializeField]
    private Texture2D constructionTabActiveTexture;

    [SerializeField]
    private Texture2D constructionTabInactiveTexture;

    [SerializeField]
    private Texture2D constructionTabDisabledTexture;

    [SerializeField]
    private Texture2D refineryTabActiveTexture;

    [SerializeField]
    private Texture2D refineryTabInactiveTexture;

    [SerializeField]
    private Texture2D refineryTabDisabledTexture;

    [SerializeField]
    private Texture2D mineTabActiveTexture;

    [SerializeField]
    private Texture2D mineTabInactiveTexture;

    [SerializeField]
    private Texture2D mineTabDisabledTexture;

    [SerializeField]
    private RawImage manufacturingStripImage;

    [SerializeField]
    private Texture2D manufacturingStripTexture;

    [SerializeField]
    private ManufacturingLaneCardView[] manufacturingCardViews =
        System.Array.Empty<ManufacturingLaneCardView>();

    [SerializeField]
    private RectTransform inventoryRoot;

    [SerializeField]
    private TextMeshProUGUI inventoryTitleTextField;

    [SerializeField]
    private FacilityInventoryItemView inventoryItemTemplate;

    private FacilityWindowRenderData lastData;
    private StrategyUIRuntime uiRuntime;
    private UIContext uiContext;
    private ConstructionWindowController constructionWindowController;
    private int activeTab;

    public GalaxyMapPlanet GalaxyMapPlanet { get; private set; }
    public PlanetIcon PlanetIcon => PlanetIcon.Facility;
    public Dictionary<ManufacturingType, string> ManufacturingDestinationPlanetIds { get; } =
        new Dictionary<ManufacturingType, string>();
    public Dictionary<ManufacturingType, string> ManufacturingDestinationItemIds { get; } =
        new Dictionary<ManufacturingType, string>();

    public void InitializeWindow(GalaxyMapPlanet planet)
    {
        GalaxyMapPlanet = planet;
    }

    public void ReconcilePlanet(GalaxyMapPlanet planet)
    {
        InitializeWindow(planet);
    }

    public void InitializeConstruction(ConstructionWindowController constructionWindowController)
    {
        this.constructionWindowController = constructionWindowController;
    }

    public void RefreshWindow(StrategyWindowRenderContext context, UIWindow window, bool active)
    {
        if (window == null)
            return;

        Render(
            new FacilityWindowRenderData
            {
                X = window.X,
                Y = window.Y,
                GalaxyMapPlanet = GalaxyMapPlanet,
                Planet = GalaxyMapPlanet?.Planet,
                OwnerFactionId = GalaxyMapPlanet?.OwnerFactionId,
                Active = active,
                ActiveTab = 0,
                ManufacturingDestinationNames =
                    constructionWindowController?.GetManufacturingDestinationNames(window),
            }
        );
    }

    public void Initialize(StrategyUIRuntime uiRuntime)
    {
        this.uiRuntime = uiRuntime;
        uiContext = uiRuntime?.Context;
        foreach (ManufacturingLaneCardView card in manufacturingCardViews)
            card?.Initialize(uiContext);
        BindEvents();
    }

    public void Render(FacilityWindowRenderData data)
    {
        VerifyReferences();
        data = CreateRenderData(data);
        lastData = data;

        RectTransform rect = transform as RectTransform;
        UILayout.SetSourcePosition(rect, data.X, data.Y);

        SetImageFromTemplate(backgroundImage, backgroundTexture);
        UILayout.SetInteractiveImageTexture(
            titleImage,
            GetTitleTexture(data.OwnerFactionId, data.Active)
        );
        SetTemplateText(captionTextField, data.Caption);
        RenderButtons();
        RenderTabs(data);

        bool showManufacturing = data.ActiveTab == _manufacturingTab;
        manufacturingStripImage.gameObject.SetActive(showManufacturing);
        if (showManufacturing)
        {
            SetImageAtTemplateOrigin(manufacturingStripImage, manufacturingStripTexture);
            RenderManufacturingCards(data.ManufacturingCards);
        }
        else
        {
            HideManufacturingCards();
        }

        inventoryRoot.gameObject.SetActive(!showManufacturing);
        if (!showManufacturing)
            RenderInventory(data);
        else
            HideInventoryItems();

        gameObject.SetActive(true);
    }

    private FacilityWindowRenderData CreateRenderData(FacilityWindowRenderData state)
    {
        Planet planet = state.Planet;
        FacilityWindowRenderData data = new FacilityWindowRenderData
        {
            X = state.X,
            Y = state.Y,
            GalaxyMapPlanet = state.GalaxyMapPlanet,
            Planet = planet,
            OwnerFactionId = state.OwnerFactionId,
            Active = state.Active,
            ActiveTab = activeTab,
            Caption = planet?.GetDisplayName() ?? state.Caption ?? string.Empty,
            SelectedCards = selectedCards,
            SelectedItems = selectedItems,
            ManufacturingDestinationNames = state.ManufacturingDestinationNames,
        };

        PopulateTabCounts(planet, data);
        if (data.ActiveTab == _manufacturingTab)
            data.ManufacturingCards.AddRange(CreateManufacturingCardRenderData(data));
        else
            PopulateInventoryRenderData(data);

        return data;
    }

    internal static bool CanBuildFromCard(GalaxyMapPlanet planet, int card, string playerFactionId)
    {
        return CanBuildFromCard(planet?.Planet, card, playerFactionId);
    }

    private static bool CanBuildFromCard(Planet planet, int card, string playerFactionId)
    {
        if (
            planet == null
            || !string.Equals(planet.OwnerInstanceID, playerFactionId, StringComparison.Ordinal)
        )
            return false;

        return card switch
        {
            _shipyardsTab => GetFacilityItems(planet, _shipyardsTab)
                .Any(building => building.IsOperationalFacility()),
            _trainingTab => GetFacilityItems(planet, _trainingTab)
                .Any(building => building.IsOperationalFacility()),
            _constructionTab => GetFacilityItems(planet, _constructionTab)
                .Any(building => building.IsOperationalFacility()),
            _ => false,
        };
    }

    internal List<StrategyMenuCommand> BuildContextMenu(
        PointerEventData eventData,
        bool playerControlsPlanet,
        string playerFactionId
    )
    {
        if (lastData == null)
            return new List<StrategyMenuCommand>();

        CaptureContextTarget(eventData);

        if (lastData.ActiveTab == _manufacturingTab)
        {
            if (contextManufacturingCard >= 1)
            {
                return new List<StrategyMenuCommand>
                {
                    new StrategyMenuCommand(
                        StrategyContextMenuActions.Build,
                        "Build",
                        CanBuildFromCard(lastData.Planet, contextManufacturingCard, playerFactionId)
                    ),
                    new StrategyMenuCommand(
                        StrategyContextMenuActions.Stop,
                        "Stop",
                        CanStopFromCard(lastData.Planet, contextManufacturingCard, playerFactionId)
                    ),
                    new StrategyMenuCommand(
                        StrategyContextMenuActions.Destination,
                        "Destination",
                        playerControlsPlanet
                    ),
                    new StrategyMenuCommand(0, "Rename", false),
                    new StrategyMenuCommand(
                        StrategyContextMenuActions.Encyclopedia,
                        "Encyclopedia",
                        true
                    ),
                    new StrategyMenuCommand(StrategyContextMenuActions.Status, "Status", true),
                    new StrategyMenuCommand(0, "Reserved", playerControlsPlanet),
                };
            }
        }
        else if (contextInventoryItem >= 0)
        {
            return new List<StrategyMenuCommand>
            {
                new StrategyMenuCommand(
                    StrategyContextMenuActions.Encyclopedia,
                    "Encyclopedia",
                    true
                ),
                new StrategyMenuCommand(StrategyContextMenuActions.Status, "Status", true),
                new StrategyMenuCommand(
                    StrategyContextMenuActions.Scrap,
                    "Scrap",
                    playerControlsPlanet
                ),
            };
        }

        return new List<StrategyMenuCommand>();
    }

    internal int GetSelectedManufacturingPanel()
    {
        return activeTab == _manufacturingTab && selectedCards.Count == 1
            ? selectedCards.First()
            : 0;
    }

    internal int GetContextManufacturingPanel()
    {
        return activeTab == _manufacturingTab && contextManufacturingCard >= 1
            ? contextManufacturingCard
            : GetSelectedManufacturingPanel();
    }

    internal bool TryGetContextManufacturingType(out ManufacturingType type)
    {
        type = ManufacturingType.None;
        ManufacturingType? selected = GetManufacturingTypeFromCard(GetContextManufacturingPanel());
        if (!selected.HasValue)
            return false;

        type = selected.Value;
        return true;
    }

    public void ClearSelection()
    {
        selectedCards.Clear();
        selectedItems.Clear();
        contextManufacturingCard = -1;
        contextInventoryItem = -1;
    }

    private bool TrySelectTarget(ISceneNode item)
    {
        if (uiRuntime?.Targeting.IsTargeting != true)
            return false;

        if (lastData?.GalaxyMapPlanet == null)
            return false;

        return uiRuntime.Targeting.TrySelectTarget(
            new StrategyMissionTarget(lastData.GalaxyMapPlanet, item)
        );
    }

    private static GameObject GetRaycastTarget(PointerEventData eventData)
    {
        if (eventData == null)
            return null;

        return eventData.pointerCurrentRaycast.gameObject
            ?? eventData.pointerPressRaycast.gameObject;
    }

    internal static int GetInventoryCount(GalaxyMapPlanet planet, int tab)
    {
        return GetInventoryCount(planet?.Planet, tab);
    }

    private static int GetInventoryCount(Planet planet, int tab)
    {
        List<Building> items = GetFacilityItems(planet, tab);
        return tab == _minesTab ? planet?.NumRawResourceNodes ?? 0 : items.Count;
    }

    internal static List<Building> GetFacilityItems(GalaxyMapPlanet planet, int tab)
    {
        return GetFacilityItems(planet?.Planet, tab);
    }

    internal static bool CanStopFromCard(GalaxyMapPlanet planet, int card, string playerFactionId)
    {
        return CanStopFromCard(planet?.Planet, card, playerFactionId);
    }

    private static bool CanStopFromCard(Planet planet, int card, string playerFactionId)
    {
        if (
            planet == null
            || !string.Equals(planet.OwnerInstanceID, playerFactionId, StringComparison.Ordinal)
        )
            return false;

        ManufacturingType? type = GetManufacturingTypeFromCard(card);
        return type.HasValue && GetQueue(planet, type.Value).Count > 0;
    }

    internal StrategyStatusTarget GetStatusTarget(GalaxyMapPlanet strategyPlanet)
    {
        if (lastData == null)
            return null;

        if (lastData.ActiveTab == 0)
        {
            int card = contextManufacturingCard;
            if (card < 1 && lastData.SelectedCards?.Count == 1)
                card = lastData.SelectedCards.First();

            ManufacturingType? type = GetManufacturingTypeFromCard(card);
            return type.HasValue ? new StrategyStatusTarget(strategyPlanet, null, type) : null;
        }

        List<Building> items = GetFacilityItems(lastData.Planet, lastData.ActiveTab);
        if (lastData.SelectedItems?.Count == 1)
        {
            int selected = lastData.SelectedItems.First();
            if (selected >= 0 && selected < items.Count)
                return new StrategyStatusTarget(strategyPlanet, items[selected]);
        }

        ISceneNode item = GetDestinationTargetItem();
        return item == null ? null : new StrategyStatusTarget(strategyPlanet, item);
    }

    StrategyStatusTarget IStrategyWindowStatusTargetView.GetStatusTarget(
        GalaxyMapPlanet strategyPlanet
    )
    {
        return GetStatusTarget(strategyPlanet);
    }

    public ISceneNode GetDestinationTargetItem()
    {
        if (lastData == null || lastData.ActiveTab == _manufacturingTab)
            return null;

        int itemIndex = contextInventoryItem;
        List<Building> items = GetFacilityItems(lastData.Planet, lastData.ActiveTab);
        return itemIndex >= 0 && itemIndex < items.Count ? items[itemIndex] : null;
    }

    private void CaptureContextTarget(PointerEventData eventData)
    {
        GameObject target = GetRaycastTarget(eventData);
        ManufacturingLaneCardView card = target?.GetComponentInParent<ManufacturingLaneCardView>();
        if (card != null && card.transform.IsChildOf(transform))
        {
            contextManufacturingCard = card.Index;
            contextInventoryItem = -1;
            SelectContextItem(selectedCards, card.Index);
            return;
        }

        FacilityInventoryItemView item = target?.GetComponentInParent<FacilityInventoryItemView>();
        if (item != null && item.transform.IsChildOf(transform))
        {
            contextManufacturingCard = -1;
            contextInventoryItem = item.Index;
            SelectContextItem(selectedItems, item.Index);
            return;
        }

        contextManufacturingCard = -1;
        contextInventoryItem = -1;
    }

    public List<ISceneNode> GetScrapItems()
    {
        if (lastData == null || lastData.ActiveTab == 0)
            return new List<ISceneNode>();

        List<Building> items = GetFacilityItems(lastData.Planet, lastData.ActiveTab);
        IEnumerable<int> selectedItems = lastData.SelectedItems ?? Enumerable.Empty<int>();
        return selectedItems
            .Where(index => index >= 0 && index < items.Count)
            .OrderBy(index => index)
            .Select(index => (ISceneNode)items[index])
            .ToList();
    }

    private static List<Building> GetFacilityItems(Planet planet, int tab)
    {
        if (planet == null)
            return new List<Building>();

        BuildingType type = tab switch
        {
            _shipyardsTab => BuildingType.Shipyard,
            _trainingTab => BuildingType.TrainingFacility,
            _constructionTab => BuildingType.ConstructionFacility,
            _refineriesTab => BuildingType.Refinery,
            _minesTab => BuildingType.Mine,
            _ => BuildingType.None,
        };

        return planet
            .Buildings.Where(building => building.GetBuildingType() == type)
            .OrderBy(building => building.GetDisplayName())
            .ToList();
    }

    private static ManufacturingType? GetManufacturingTypeFromCard(int card)
    {
        return card switch
        {
            _shipyardsTab => ManufacturingType.Ship,
            _trainingTab => ManufacturingType.Troop,
            _constructionTab => ManufacturingType.Building,
            _ => null,
        };
    }

    private static void PopulateTabCounts(Planet planet, FacilityWindowRenderData data)
    {
        data.TabCounts.Add(1);
        for (int i = _shipyardsTab; i <= _minesTab; i++)
            data.TabCounts.Add(GetInventoryCount(planet, i));
    }

    private IEnumerable<ManufacturingLaneCardRenderData> CreateManufacturingCardRenderData(
        FacilityWindowRenderData data
    )
    {
        yield return CreateManufacturingCardRenderData(data, ManufacturingType.Ship, _shipyardsTab);
        yield return CreateManufacturingCardRenderData(data, ManufacturingType.Troop, _trainingTab);
        yield return CreateManufacturingCardRenderData(
            data,
            ManufacturingType.Building,
            _constructionTab
        );
    }

    private ManufacturingLaneCardRenderData CreateManufacturingCardRenderData(
        FacilityWindowRenderData data,
        ManufacturingType type,
        int index
    )
    {
        Planet planet = data.Planet;
        List<IManufacturable> queue = GetQueue(planet, type);
        IManufacturable current = queue.FirstOrDefault(item =>
            item.GetManufacturingStatus() == ManufacturingStatus.Building
        );
        List<Building> facilities = GetFacilityItems(planet, index);
        int active = facilities.Count(building =>
            building.GetManufacturingStatus() == ManufacturingStatus.Complete
        );

        return new ManufacturingLaneCardRenderData
        {
            OwnerFactionId = data.OwnerFactionId,
            Selected = ContainsSelection(data.SelectedCards, index),
            ManufacturingProgress = current?.GetManufacturingProgress() ?? 0,
            ManufacturingCost = current?.GetConstructionCost() ?? 0,
            Title = GetManufacturingTitle(type),
            EmptyText = GetManufacturingEmptyText(type),
            CurrentName = current?.GetDisplayName() ?? string.Empty,
            CurrentCount = current == null ? string.Empty : "Building " + Math.Max(1, queue.Count),
            DestinationText = "Destination: " + GetDestinationName(data, type),
            ActiveFacilityCount = active.ToString(),
            TotalFacilityCount = facilities.Count.ToString(),
            EntityTexture = current == null ? null : uiContext?.GetEntityTexture(current, true),
        };
    }

    private static string GetDestinationName(FacilityWindowRenderData data, ManufacturingType type)
    {
        if (
            data.ManufacturingDestinationNames != null
            && data.ManufacturingDestinationNames.TryGetValue(type, out string destinationName)
            && !string.IsNullOrEmpty(destinationName)
        )
            return destinationName;

        return data.Planet?.GetDisplayName() ?? string.Empty;
    }

    private void PopulateInventoryRenderData(FacilityWindowRenderData data)
    {
        data.InventoryTitle = GetInventoryTitle(data.ActiveTab);

        List<Building> items = GetFacilityItems(data.Planet, data.ActiveTab);
        int total = GetInventoryCount(data.Planet, data.ActiveTab);

        for (int i = 0; i < total; i++)
        {
            Building item = i < items.Count ? items[i] : null;
            data.InventoryItems.Add(
                new FacilityInventoryItemRenderData
                {
                    Texture = GetInventoryTexture(data.ActiveTab, item),
                    Selected = ContainsSelection(data.SelectedItems, i),
                }
            );
        }
    }

    private Texture GetInventoryTexture(int tab, Building building)
    {
        if (building == null)
            return tab == _minesTab
                ? uiContext?.GetTexture(_rawResourceNodeTexturePath)
                    ?? inventoryItemTemplate.TemplateTexture
                : inventoryItemTemplate.TemplateTexture;

        return GetBuildingStateTexture(building) ?? uiContext?.GetEntityTexture(building, true);
    }

    private Texture GetBuildingStateTexture(Building building)
    {
        if (building == null || uiContext == null)
            return null;

        if (building.Movement != null)
            return uiContext.GetTexture(building.InTransitSmallImagePath);

        if (building.GetManufacturingStatus() != ManufacturingStatus.Building)
            return null;

        return uiContext.GetTexture(GetConstructionImagePath(building));
    }

    private string GetConstructionImagePath(Building building)
    {
        return uiContext
            ?.GetPlayerFactionTheme()
            ?.StrategyWindows?.Facility?.GetConstructionImagePath(building.GetTypeID());
    }

    private static List<IManufacturable> GetQueue(Planet planet, ManufacturingType type)
    {
        if (planet == null)
            return new List<IManufacturable>();

        return planet.ManufacturingQueue.TryGetValue(type, out List<IManufacturable> queue)
            ? queue
            : new List<IManufacturable>();
    }

    private static bool ContainsSelection(IReadOnlyCollection<int> selection, int index)
    {
        return selection != null && selection.Contains(index);
    }

    private static string GetManufacturingTitle(ManufacturingType type)
    {
        return type switch
        {
            ManufacturingType.Ship => "Ship Construction",
            ManufacturingType.Troop => "Troops in Training",
            ManufacturingType.Building => "Facilities Under Construction",
            _ => string.Empty,
        };
    }

    private static string GetManufacturingEmptyText(ManufacturingType type)
    {
        return type switch
        {
            ManufacturingType.Ship => "No Ships are being built",
            ManufacturingType.Troop => "No Troops in training",
            ManufacturingType.Building => "No Facilities are being built",
            _ => string.Empty,
        };
    }

    private static string GetInventoryTitle(int activeTab)
    {
        return activeTab switch
        {
            1 => "Shipyards",
            2 => "Training Facilities",
            3 => "Construction Yards",
            4 => "Refineries",
            5 => "Mines",
            _ => string.Empty,
        };
    }

    private void Awake()
    {
        VerifyReferences();
    }

    private void BindEvents()
    {
        if (eventsBound)
            return;

        for (int i = 0; i < tabButtons.Length; i++)
        {
            int tab = i;
            if (tabButtons[i] != null)
                tabButtons[i].onClick.AddListener(() => SelectTab(tab));
        }

        for (int i = 0; i < manufacturingCardViews.Length; i++)
        {
            ManufacturingLaneCardView card = manufacturingCardViews[i];
            if (card == null)
                continue;

            card.Initialize(uiContext);
            card.SetIndex(i + 1);
            card.Pressed += HandleManufacturingCardPressed;
            card.Released += HandleManufacturingCardReleased;
            card.Dropped += HandleManufacturingCardReleased;
        }

        eventsBound = true;
    }

    private void SelectTab(int tab)
    {
        SetActiveTab(tab);
        RequestRender();
    }

    private void HandleManufacturingCardPressed(
        ManufacturingLaneCardView card,
        PointerEventData eventData
    )
    {
        if (card == null || lastData == null || lastData.ActiveTab != 0)
            return;

        if (
            uiRuntime?.Targeting.IsTargeting == true
            && eventData.button == PointerEventData.InputButton.Left
        )
            return;

        contextManufacturingCard = card.Index;
        contextInventoryItem = -1;
        if (eventData.button == PointerEventData.InputButton.Right)
            SelectContextItem(selectedCards, card.Index);
        else
            SelectIndexedItem(selectedCards, card.Index, 4);

        RequestRender();
    }

    private void HandleManufacturingCardReleased(
        ManufacturingLaneCardView card,
        PointerEventData eventData
    )
    {
        if (card != null && eventData.button == PointerEventData.InputButton.Left)
            TrySelectTarget((ISceneNode)null);
    }

    private void HandleInventoryItemPressed(
        FacilityInventoryItemView item,
        PointerEventData eventData
    )
    {
        if (item == null || lastData == null || lastData.ActiveTab == 0)
            return;

        if (
            uiRuntime?.Targeting.IsTargeting == true
            && eventData.button == PointerEventData.InputButton.Left
        )
            return;

        contextManufacturingCard = -1;
        contextInventoryItem = item.Index;
        if (eventData.button == PointerEventData.InputButton.Right)
            SelectContextItem(selectedItems, item.Index);
        else
            SelectIndexedItem(
                selectedItems,
                item.Index,
                GetInventoryCount(lastData.Planet, activeTab),
                3
            );

        RequestRender();
    }

    private void HandleInventoryItemReleased(
        FacilityInventoryItemView item,
        PointerEventData eventData
    )
    {
        if (item == null || eventData.button != PointerEventData.InputButton.Left)
            return;

        TrySelectTarget(GetInventoryItem(item.Index));
    }

    private Building GetInventoryItem(int index)
    {
        if (lastData == null || lastData.ActiveTab == 0)
            return null;

        List<Building> items = GetFacilityItems(lastData.Planet, lastData.ActiveTab);
        return index >= 0 && index < items.Count ? items[index] : null;
    }

    private void RequestRender()
    {
        uiContext?.Dispatcher.Send(new StrategyUIRequests.RequestRender());
    }

    private void RenderButtons()
    {
        for (int i = 0; i < buttonImages.Length; i++)
        {
            RawImage image = buttonImages[i];
            if (image == null)
                continue;

            int action = GetButtonAction(i);
            ConfigureWindowButton(image, action);
        }
    }

    private void ConfigureWindowButton(RawImage image, int action)
    {
        UILayout.SetInteractiveImageTexture(image, GetButtonTexture(action, false));
    }

    private void SetActiveTab(int tab)
    {
        if (tab == activeTab)
            return;

        activeTab = tab;
        selectedCards.Clear();
        selectedItems.Clear();
        contextManufacturingCard = -1;
        contextInventoryItem = -1;
    }

    private static void SelectIndexedItem(
        HashSet<int> selection,
        int index,
        int count,
        int itemsPerRow = 1
    )
    {
        SelectableListSelection.SelectIndexedItem(selection, index, count, itemsPerRow);
    }

    private static void SelectContextItem(HashSet<int> selection, int index)
    {
        if (selection.Contains(index))
            return;

        selection.Clear();
        selection.Add(index);
    }

    private void RenderTabs(FacilityWindowRenderData data)
    {
        for (int i = 0; i < tabImages.Length; i++)
        {
            RawImage image = tabImages[i];
            int count = i == 0 ? 1 : GetTabCount(data, i);
            int state =
                i == data.ActiveTab ? 0
                : count > 0 ? 1
                : 2;
            UILayout.SetInteractiveImageTexture(
                image,
                GetTabTexture(data.OwnerFactionId, i, state)
            );
        }
    }

    private void RenderManufacturingCards(IReadOnlyList<ManufacturingLaneCardRenderData> cards)
    {
        IReadOnlyList<ManufacturingLaneCardRenderData> safeCards =
            cards ?? System.Array.Empty<ManufacturingLaneCardRenderData>();
        int count = Mathf.Min(safeCards.Count, manufacturingCardViews.Length);
        for (int i = 0; i < count; i++)
        {
            ManufacturingLaneCardView card = manufacturingCardViews[i];
            if (card != null)
                card.Render(safeCards[i]);
        }

        for (int i = count; i < manufacturingCardViews.Length; i++)
        {
            if (manufacturingCardViews[i] != null)
                manufacturingCardViews[i].gameObject.SetActive(false);
        }
    }

    private void HideManufacturingCards()
    {
        for (int i = 0; i < manufacturingCardViews.Length; i++)
        {
            if (manufacturingCardViews[i] != null)
                manufacturingCardViews[i].gameObject.SetActive(false);
        }
    }

    private void RenderInventory(FacilityWindowRenderData data)
    {
        SetTemplateText(inventoryTitleTextField, data.InventoryTitle);

        IReadOnlyList<FacilityInventoryItemRenderData> items =
            data.InventoryItems != null
                ? data.InventoryItems
                : System.Array.Empty<FacilityInventoryItemRenderData>();
        for (int i = 0; i < items.Count; i++)
        {
            FacilityInventoryItemRenderData item = items[i];
            FacilityInventoryItemView itemView = GetInventoryItemView(i);
            RectInt rect = GetInventoryItemRect(i);
            itemView.Render(i, item, GetSelectionTexture(data.OwnerFactionId), rect);
        }

        for (int i = items.Count; i < inventoryItemViews.Count; i++)
            inventoryItemViews[i].gameObject.SetActive(false);
    }

    private void HideInventoryItems()
    {
        inventoryTitleTextField.gameObject.SetActive(false);
        for (int i = 0; i < inventoryItemViews.Count; i++)
            inventoryItemViews[i].gameObject.SetActive(false);
    }

    private int GetButtonAction(int index)
    {
        return index >= 0 && index < buttonActions.Length ? buttonActions[index] : 0;
    }

    private int GetTabCount(FacilityWindowRenderData data, int index)
    {
        return data.TabCounts != null && index >= 0 && index < data.TabCounts.Count
            ? data.TabCounts[index]
            : 0;
    }

    private Texture2D GetTitleTexture(string ownerFactionId, bool active)
    {
        WindowTitleTheme theme = uiContext?.GetTheme(ownerFactionId)?.WindowTitleTheme;
        return uiContext?.GetTexture(active ? theme?.ActiveImagePath : theme?.InactiveImagePath);
    }

    private Texture2D GetButtonTexture(int action, bool pressed)
    {
        return action switch
        {
            StrategyWindowButtonActions.OpenSector => pressed
                ? openSectorButtonDownTexture
                : openSectorButtonUpTexture,
            StrategyWindowButtonActions.MinimizeWindow => pressed
                ? minimizeButtonDownTexture
                : minimizeButtonUpTexture,
            StrategyWindowButtonActions.CloseWindow => pressed
                ? closeButtonDownTexture
                : closeButtonUpTexture,
            _ => null,
        };
    }

    private Texture2D GetTabTexture(string ownerFactionId, int index, int state)
    {
        return index switch
        {
            0 => GetControlTabTexture(ownerFactionId, state),
            1 => GetFacilityTabTexture(
                state,
                shipyardTabActiveTexture,
                shipyardTabInactiveTexture,
                shipyardTabDisabledTexture
            ),
            2 => GetFacilityTabTexture(
                state,
                troopTabActiveTexture,
                troopTabInactiveTexture,
                troopTabDisabledTexture
            ),
            3 => GetFacilityTabTexture(
                state,
                constructionTabActiveTexture,
                constructionTabInactiveTexture,
                constructionTabDisabledTexture
            ),
            4 => GetFacilityTabTexture(
                state,
                refineryTabActiveTexture,
                refineryTabInactiveTexture,
                refineryTabDisabledTexture
            ),
            5 => GetFacilityTabTexture(
                state,
                mineTabActiveTexture,
                mineTabInactiveTexture,
                mineTabDisabledTexture
            ),
            _ => null,
        };
    }

    private Texture2D GetControlTabTexture(string ownerFactionId, int state)
    {
        WindowTabImageTheme theme = uiContext
            ?.GetTheme(ownerFactionId)
            ?.StrategyWindows?.Facility?.ControlTab;
        return uiContext?.GetTexture(theme?.GetImagePath(state));
    }

    private static Texture2D GetFacilityTabTexture(
        int state,
        Texture2D activeTexture,
        Texture2D inactiveTexture,
        Texture2D disabledTexture
    )
    {
        return state switch
        {
            0 => activeTexture,
            1 => inactiveTexture,
            _ => disabledTexture,
        };
    }

    private Texture2D GetSelectionTexture(string ownerFactionId)
    {
        string path = uiContext
            ?.GetTheme(ownerFactionId)
            ?.StrategyWindows?.Facility?.SelectionImagePath;
        return uiContext?.GetTexture(path);
    }

    private FacilityInventoryItemView GetInventoryItemView(int index)
    {
        while (inventoryItemViews.Count <= index)
        {
            FacilityInventoryItemView view = Instantiate(inventoryItemTemplate, inventoryRoot);
            view.name = $"InventoryItem{inventoryItemViews.Count}";
            view.Pressed += HandleInventoryItemPressed;
            view.Released += HandleInventoryItemReleased;
            view.Dropped += HandleInventoryItemReleased;
            inventoryItemViews.Add(view);
        }

        return inventoryItemViews[index];
    }

    private RectInt GetInventoryItemRect(int index)
    {
        RectInt root = UILayout.GetSourceRect(inventoryRoot);
        RectInt template = UILayout.GetSourceRect(inventoryItemTemplate.transform as RectTransform);
        int columns = Mathf.Max(1, Mathf.FloorToInt((root.width - template.x) / template.width));
        int column = index % columns;
        int row = index / columns;
        int strideX =
            columns > 1
                ? Mathf.FloorToInt(
                    (root.width - template.x * 2 - template.width) / (float)(columns - 1)
                )
                : template.width;
        return new RectInt(
            template.x + column * strideX,
            template.y + row * template.height,
            template.width,
            template.height
        );
    }

    private void VerifyReferences()
    {
        if (backgroundImage == null)
            throw new MissingReferenceException($"{name}/BackgroundImage is missing.");
        if (titleImage == null)
            throw new MissingReferenceException($"{name}/TitleImage is missing.");
        if (backgroundTexture == null)
            throw new MissingReferenceException($"{name}/BackgroundTexture is missing.");
        if (captionTextField == null)
            throw new MissingReferenceException($"{name}/CaptionTextField is missing.");
        if (buttonImages == null || buttonImages.Length == 0)
            throw new MissingReferenceException($"{name}/Button images are missing.");
        if (buttonActions == null || buttonActions.Length != buttonImages.Length)
            throw new MissingReferenceException($"{name}/Button actions are missing.");
        if (openSectorButtonUpTexture == null)
            throw new MissingReferenceException($"{name}/OpenSectorButtonUpTexture is missing.");
        if (openSectorButtonDownTexture == null)
            throw new MissingReferenceException($"{name}/OpenSectorButtonDownTexture is missing.");
        if (minimizeButtonUpTexture == null)
            throw new MissingReferenceException($"{name}/MinimizeButtonUpTexture is missing.");
        if (minimizeButtonDownTexture == null)
            throw new MissingReferenceException($"{name}/MinimizeButtonDownTexture is missing.");
        if (closeButtonUpTexture == null)
            throw new MissingReferenceException($"{name}/CloseButtonUpTexture is missing.");
        if (closeButtonDownTexture == null)
            throw new MissingReferenceException($"{name}/CloseButtonDownTexture is missing.");
        for (int i = 0; i < buttonImages.Length; i++)
        {
            if (buttonImages[i] == null)
                throw new MissingReferenceException($"{name}/ButtonImage{i} is missing.");
        }
        if (tabImages == null || tabImages.Length == 0)
            throw new MissingReferenceException($"{name}/Tab images are missing.");
        if (tabButtons == null || tabButtons.Length != tabImages.Length)
            throw new MissingReferenceException($"{name}/Tab buttons are missing.");
        for (int i = 0; i < tabImages.Length; i++)
        {
            if (tabImages[i] == null)
                throw new MissingReferenceException($"{name}/TabImage{i} is missing.");
            if (tabButtons[i] == null)
                throw new MissingReferenceException($"{name}/TabButton{i} is missing.");
        }
        VerifyTabTextures();
        if (manufacturingStripImage == null)
            throw new MissingReferenceException($"{name}/ManufacturingStripImage is missing.");
        if (manufacturingStripTexture == null)
            throw new MissingReferenceException($"{name}/ManufacturingStripTexture is missing.");
        if (manufacturingCardViews == null || manufacturingCardViews.Length == 0)
            throw new MissingReferenceException($"{name}/Manufacturing lane cards are missing.");
        for (int i = 0; i < manufacturingCardViews.Length; i++)
        {
            if (manufacturingCardViews[i] == null)
                throw new MissingReferenceException($"{name}/ManufacturingLaneCard{i} is missing.");
        }
        if (inventoryRoot == null)
            throw new MissingReferenceException($"{name}/Inventory is missing.");
        if (inventoryTitleTextField == null)
            throw new MissingReferenceException($"{name}/InventoryTitleTextField is missing.");
        if (inventoryItemTemplate == null)
            throw new MissingReferenceException($"{name}/InventoryItemTemplate is missing.");
        inventoryItemTemplate.gameObject.SetActive(false);
    }

    private void VerifyTabTextures()
    {
        VerifyFacilityTabTextures(
            "Shipyard",
            shipyardTabActiveTexture,
            shipyardTabInactiveTexture,
            shipyardTabDisabledTexture
        );
        VerifyFacilityTabTextures(
            "Troop",
            troopTabActiveTexture,
            troopTabInactiveTexture,
            troopTabDisabledTexture
        );
        VerifyFacilityTabTextures(
            "Construction",
            constructionTabActiveTexture,
            constructionTabInactiveTexture,
            constructionTabDisabledTexture
        );
        VerifyFacilityTabTextures(
            "Refinery",
            refineryTabActiveTexture,
            refineryTabInactiveTexture,
            refineryTabDisabledTexture
        );
        VerifyFacilityTabTextures(
            "Mine",
            mineTabActiveTexture,
            mineTabInactiveTexture,
            mineTabDisabledTexture
        );
    }

    private void VerifyFacilityTabTextures(
        string label,
        Texture2D activeTexture,
        Texture2D inactiveTexture,
        Texture2D disabledTexture
    )
    {
        if (activeTexture == null)
            throw new MissingReferenceException($"{name}/{label}TabActiveTexture is missing.");
        if (inactiveTexture == null)
            throw new MissingReferenceException($"{name}/{label}TabInactiveTexture is missing.");
        if (disabledTexture == null)
            throw new MissingReferenceException($"{name}/{label}TabDisabledTexture is missing.");
    }

    private static void SetImageFromTemplate(RawImage image, Texture texture)
    {
        UILayout.SetImageTexture(image, texture);
    }

    private static void SetImageAtTemplateOrigin(RawImage image, Texture texture)
    {
        UILayout.SetImageTexture(image, texture);
    }

    private static void SetTemplateText(TextMeshProUGUI textField, string text)
    {
        UILayout.SetTextContent(textField, text, textField.color);
    }
}

public sealed class FacilityWindowRenderData
{
    public int X;
    public int Y;
    internal GalaxyMapPlanet GalaxyMapPlanet;
    public Planet Planet;
    public string OwnerFactionId;
    public bool Active;
    public int ActiveTab;
    public string Caption;
    public string InventoryTitle;
    public IReadOnlyCollection<int> SelectedCards;
    public IReadOnlyCollection<int> SelectedItems;
    public IReadOnlyDictionary<ManufacturingType, string> ManufacturingDestinationNames;
    public List<int> TabCounts = new List<int>();
    public List<ManufacturingLaneCardRenderData> ManufacturingCards =
        new List<ManufacturingLaneCardRenderData>();
    public List<FacilityInventoryItemRenderData> InventoryItems =
        new List<FacilityInventoryItemRenderData>();
}

public sealed class ManufacturingLaneCardRenderData
{
    public string OwnerFactionId;
    public bool Selected;
    public int ManufacturingProgress;
    public int ManufacturingCost;
    public string Title;
    public string EmptyText;
    public string CurrentName;
    public string CurrentCount;
    public string DestinationText;
    public string ActiveFacilityCount;
    public string TotalFacilityCount;
    public Texture EntityTexture;
}

public sealed class FacilityInventoryItemRenderData
{
    public Texture Texture;
    public bool Selected;
}
