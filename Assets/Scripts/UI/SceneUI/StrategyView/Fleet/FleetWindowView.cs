using System.Collections.Generic;
using System.Linq;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Units;
using Rebellion.SceneGraph;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public sealed class FleetWindowView
    : MonoBehaviour,
        IStrategyUIRuntimeReceiver,
        IStrategyWindowSelectionView,
        IStrategyWindowStatusTargetView,
        IStrategyWindowContextItemsView,
        IStrategyWindowDragImageView,
        IStrategyWindowContent,
        IPlanetIconWindowView
{
    private readonly HashSet<int> selectedFleetItems = new HashSet<int>();
    private readonly HashSet<int> selectedItems = new HashSet<int>();
    private readonly List<FleetListRowView> listRowViews = new List<FleetListRowView>();
    private readonly List<StrategyUnitCardView> detailItemViews = new List<StrategyUnitCardView>();
    private readonly List<string> renderedFleetRowNames = new List<string>();
    private readonly List<string> renderedDetailItemNames = new List<string>();

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
    private int contextMenuWidth;

    [SerializeField]
    private int bombardmentContextMenuWidth;

    [SerializeField]
    private ScrollAreaView fleetListScrollArea;

    [SerializeField]
    private FleetListRowView fleetListRowTemplate;

    [SerializeField]
    private RawImage detailBackgroundImage;

    [SerializeField]
    private RawImage bannerImage;

    [SerializeField]
    private RawImage bannerEnrouteOverlayImage;

    [SerializeField]
    private RawImage bannerDamagedOverlayImage;

    [SerializeField]
    private TextMeshProUGUI fleetNameTextField;

    [SerializeField]
    private TextMeshProUGUI capacityLeftTextField;

    [SerializeField]
    private TextMeshProUGUI capacityRightTextField;

    [SerializeField]
    private RectTransform tabsRoot;

    [SerializeField]
    private RawImage[] tabImages = System.Array.Empty<RawImage>();

    [SerializeField]
    private Button[] tabButtons = System.Array.Empty<Button>();

    [SerializeField]
    private ScrollAreaView detailItemsScrollArea;

    [SerializeField]
    private RectTransform detailItemsScrollPaddingTemplate;

    [SerializeField]
    private StrategyUnitCardView detailItemTemplate;

    [SerializeField]
    private Texture2D personnelBackgroundTexture;

    [SerializeField]
    private Texture2D personnelEnrouteBackgroundTexture;

    [SerializeField]
    private FleetWindowRenderData lastData;
    private StrategyUIRuntime uiRuntime;
    private UIContext uiContext;
    private bool stateInitialized;
    private int activeTab;
    private int selectedIndex = -1;
    private bool renderedAnyFleetRows;
    private int renderedDetailActiveTab = -1;
    private int renderedDetailSelectedIndex = -1;
    private bool renderedAnyDetailItems;
    private bool scrollAreaDragEventsBound;
    private UIWindow windowShell;
    private int contextFleetIndex = -1;
    private int contextDetailItemIndex = -1;
    private int dragFleetIndex = -1;
    private int dragDetailItemIndex = -1;

    public GalaxyMapPlanet GalaxyMapPlanet { get; private set; }
    public PlanetIcon PlanetIcon => PlanetIcon.Fleet;

    public void InitializeWindow(GalaxyMapPlanet planet)
    {
        GalaxyMapPlanet = planet;
    }

    public void ReconcilePlanet(GalaxyMapPlanet planet)
    {
        InitializeWindow(planet);
    }

    public void RefreshWindow(StrategyWindowRenderContext context, UIWindow window, bool active)
    {
        if (window == null)
            return;

        List<Fleet> fleets = GalaxyMapPlanet?.Planet?.Fleets?.ToList() ?? new List<Fleet>();
        int activeTab = GetActiveTab(0);
        int selectedIndex = GetSelectedIndex(-1, fleets.Count);
        Render(
            new FleetWindowRenderData
            {
                X = window.X,
                Y = window.Y,
                GalaxyMapPlanet = GalaxyMapPlanet,
                Planet = GalaxyMapPlanet?.Planet,
                OwnerFactionId = GalaxyMapPlanet?.OwnerFactionId,
                Active = active,
                ActiveTab = activeTab,
                SelectedIndex = selectedIndex,
                SelectedFleetItems = GetSelectedFleetItems(),
                SelectedItems = GetSelectedItems(),
            }
        );
    }

    public void Initialize(StrategyUIRuntime uiRuntime)
    {
        this.uiRuntime = uiRuntime;
        uiContext = uiRuntime?.Context;
        fleetListRowTemplate?.Initialize(uiContext);
        foreach (FleetListRowView row in listRowViews)
            row.Initialize(uiContext);
        BindScrollAreaDragEvents();
    }

    public void Render(FleetWindowRenderData data)
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
        RenderFleetRows(data.FleetRows);
        SetImageAtTemplateOrigin(
            detailBackgroundImage,
            GetDetailBackgroundTexture(data.SelectedFleetOwnerFactionId)
        );

        RenderSelectedFleet(data);
        gameObject.SetActive(true);
    }

    private FleetWindowRenderData CreateRenderData(FleetWindowRenderData state)
    {
        InitializeState(state.ActiveTab, state.SelectedIndex);
        List<Fleet> fleets = state.Planet?.Fleets?.ToList() ?? new List<Fleet>();
        if (selectedIndex < 0 && fleets.Count > 0)
            selectedIndex = 0;
        if (selectedIndex >= fleets.Count)
            selectedIndex = fleets.Count - 1;

        Fleet selectedFleet =
            selectedIndex >= 0 && selectedIndex < fleets.Count ? fleets[selectedIndex] : null;
        string selectedFleetOwnerFactionId = selectedFleet?.OwnerInstanceID ?? state.OwnerFactionId;

        FleetWindowRenderData data = new FleetWindowRenderData
        {
            X = state.X,
            Y = state.Y,
            GalaxyMapPlanet = state.GalaxyMapPlanet,
            Planet = state.Planet,
            OwnerFactionId = state.OwnerFactionId,
            Active = state.Active,
            SelectedFleetOwnerFactionId = selectedFleetOwnerFactionId,
            ActiveTab = activeTab,
            Caption = state.Planet?.GetDisplayName() ?? state.Caption ?? string.Empty,
            SelectedIndex = selectedIndex,
            SelectedFleetItems = selectedFleetItems,
            SelectedItems = selectedItems,
        };

        PopulateFleetRows(data, fleets);
        if (selectedFleet != null)
            PopulateSelectedFleet(data, selectedFleet);

        return data;
    }

    internal int GetActiveTab(int initialTab)
    {
        InitializeState(initialTab, -1);
        return activeTab;
    }

    internal int GetSelectedIndex(int initialSelectedIndex, int fleetCount)
    {
        InitializeState(0, initialSelectedIndex);
        if (selectedIndex < 0 && initialSelectedIndex >= 0)
            selectedIndex = initialSelectedIndex;
        if (selectedIndex < 0 && fleetCount > 0)
            selectedIndex = 0;
        if (selectedIndex >= fleetCount)
            selectedIndex = fleetCount - 1;

        return selectedIndex;
    }

    internal IReadOnlyCollection<int> GetSelectedFleetItems()
    {
        return selectedFleetItems;
    }

    internal IReadOnlyCollection<int> GetSelectedItems()
    {
        return selectedItems;
    }

    internal void SelectFinderTarget(FinderWindowRow row)
    {
        if (row == null)
            return;

        List<Fleet> fleets = GalaxyMapPlanet?.Planet?.Fleets?.ToList() ?? new List<Fleet>();
        Fleet targetFleet = row.Fleet ?? row.Node as Fleet ?? row.Node?.GetParentOfType<Fleet>();
        int fleetIndex = fleets.FindIndex(fleet => fleet == targetFleet);
        if (fleetIndex >= 0)
            selectedIndex = fleetIndex;

        if (row.Node is CapitalShip)
            activeTab = 0;
        else if (row.Node is Regiment)
            activeTab = 2;
        else if (row.Node is Officer or SpecialForces)
            activeTab = 3;

        stateInitialized = true;
    }

    private void PopulateFleetRows(FleetWindowRenderData data, IReadOnlyList<Fleet> fleets)
    {
        for (int i = 0; i < fleets.Count; i++)
        {
            Fleet fleet = fleets[i];
            bool selected =
                ContainsSelection(data.SelectedFleetItems, i)
                || (data.SelectedFleetItems == null || data.SelectedFleetItems.Count == 0)
                    && i == data.SelectedIndex;
            data.FleetRows.Add(
                new FleetListRowRenderData
                {
                    Name = fleet.GetDisplayName(),
                    OwnerFactionId = fleet.OwnerInstanceID,
                    EnrouteOverlayTexture = GetFleetListEnrouteOverlayTexture(fleet),
                    DamagedOverlayTexture = GetFleetListDamagedOverlayTexture(fleet),
                    StarfighterBadgeTexture = GetFleetStarfighterBadgeTexture(
                        fleet.OwnerInstanceID,
                        fleet.GetStarfighters().Any()
                    ),
                    TroopBadgeTexture = GetFleetTroopBadgeTexture(
                        fleet.OwnerInstanceID,
                        fleet.GetRegiments().Any()
                    ),
                    PersonnelBadgeTexture = GetFleetPersonnelBadgeTexture(
                        fleet.OwnerInstanceID,
                        fleet.GetOfficers().Any() || fleet.GetSpecialForces().Any()
                    ),
                    Selected = selected,
                }
            );
        }
    }

    private void PopulateSelectedFleet(FleetWindowRenderData data, Fleet fleet)
    {
        data.HasSelectedFleet = true;
        data.FleetName = fleet.GetDisplayName();
        data.FleetNameColor = GetFactionColor(fleet.OwnerInstanceID);
        data.BannerTexture = GetFleetBannerTexture(fleet);
        data.BannerEnrouteOverlayTexture = GetFleetBannerEnrouteOverlayTexture(fleet);
        data.BannerDamagedOverlayTexture = GetFleetBannerDamagedOverlayTexture(fleet);

        if (data.ActiveTab == 1)
        {
            data.ShowCapacity = true;
            data.CapacityLeft = fleet.GetStarfighters().Count().ToString();
            data.CapacityRight = fleet.GetStarfighterCapacity().ToString();
        }
        else if (data.ActiveTab == 2)
        {
            data.ShowCapacity = true;
            data.CapacityLeft = fleet.GetRegiments().Count().ToString();
            data.CapacityRight = fleet.GetRegimentCapacity().ToString();
        }

        List<ISceneNode>[] tabItems = GetAllTabItems(fleet);
        for (int i = 0; i < 4; i++)
            data.TabCounts.Add(tabItems[i].Count);

        List<ISceneNode> detailItems = GetDetailItems(fleet, data.ActiveTab);
        for (int i = 0; i < detailItems.Count; i++)
        {
            ISceneNode item = detailItems[i];
            CapitalShip capitalShip = item as CapitalShip;
            data.DetailItems.Add(
                new StrategyUnitCardRenderData
                {
                    Name = item.GetDisplayName(),
                    NameColor = Color.white,
                    ShowName = true,
                    UseAlternateNameLayout = data.ActiveTab == 3,
                    BackgroundTexture = GetDetailItemBackgroundTexture(fleet, item, data.ActiveTab),
                    EntityTexture = uiContext?.GetEntityTexture(item, true),
                    ConstructionOverlayTexture = GetFleetDetailConstructionOverlayTexture(item),
                    EnrouteOverlayTexture = GetFleetDetailEnrouteOverlayTexture(fleet, item),
                    DamagedOverlayTexture = GetFleetDetailDamagedOverlayTexture(item),
                    CapturedOverlayTexture = uiContext?.GetEntityCapturedOverlayTexture(item),
                    SelectionTexture = ContainsSelection(data.SelectedItems, i)
                        ? GetFleetDetailSelectionTexture(data.SelectedFleetOwnerFactionId)
                        : null,
                    StarfighterBadgeTexture = GetFleetStarfighterBadgeTexture(
                        item.GetOwnerInstanceID(),
                        capitalShip?.Starfighters.Any() == true
                    ),
                    TroopBadgeTexture = GetFleetTroopBadgeTexture(
                        item.GetOwnerInstanceID(),
                        capitalShip?.Regiments.Any() == true
                    ),
                    PersonnelBadgeTexture = GetFleetPersonnelBadgeTexture(
                        item.GetOwnerInstanceID(),
                        capitalShip?.Officers.Any() == true
                    ),
                    CanDrag = true,
                }
            );
        }
    }

    private static bool ContainsSelection(IReadOnlyCollection<int> selection, int index)
    {
        return selection != null && selection.Contains(index);
    }

    internal bool NeedsFleetListScrollbar(int fleetCount)
    {
        return fleetCount * GetFleetListRowHeight() > fleetListScrollArea.ViewportHeight;
    }

    internal int GetFleetListScrollContentHeight(int fleetCount)
    {
        return fleetCount * GetFleetListRowHeight();
    }

    internal int GetFleetListScrollViewportHeight(bool scrollbarVisible)
    {
        return Mathf.RoundToInt(fleetListScrollArea.ViewportHeight);
    }

    internal int GetFleetListScrollStep()
    {
        return GetFleetListRowHeight();
    }

    internal bool NeedsDetailScrollbar(int itemCount)
    {
        return itemCount * GetFleetDetailItemHeight() > detailItemsScrollArea.ViewportHeight;
    }

    internal int GetDetailScrollContentHeight(int itemCount)
    {
        return UILayout.GetSourceRect(detailItemsScrollPaddingTemplate).height
            + itemCount * GetFleetDetailItemHeight();
    }

    internal int GetDetailScrollViewportHeight()
    {
        return Mathf.RoundToInt(detailItemsScrollArea.ViewportHeight);
    }

    internal int GetDetailScrollStep()
    {
        return GetFleetDetailItemHeight();
    }

    internal static List<ISceneNode>[] GetAllTabItems(Fleet fleet)
    {
        if (fleet == null)
        {
            return new[]
            {
                new List<ISceneNode>(),
                new List<ISceneNode>(),
                new List<ISceneNode>(),
                new List<ISceneNode>(),
            };
        }

        return new[]
        {
            fleet.CapitalShips.Cast<ISceneNode>().ToList(),
            fleet.GetStarfighters().Cast<ISceneNode>().ToList(),
            fleet.GetRegiments().Cast<ISceneNode>().ToList(),
            fleet.GetOfficers().Cast<ISceneNode>().Concat(fleet.GetSpecialForces()).ToList(),
        };
    }

    internal static List<ISceneNode> GetDetailItems(Fleet fleet, int tab)
    {
        if (fleet == null)
            return new List<ISceneNode>();

        return tab switch
        {
            0 => fleet.CapitalShips.Cast<ISceneNode>().ToList(),
            1 => fleet.GetStarfighters().Cast<ISceneNode>().ToList(),
            2 => fleet.GetRegiments().Cast<ISceneNode>().ToList(),
            3 => fleet.GetOfficers().Cast<ISceneNode>().Concat(fleet.GetSpecialForces()).ToList(),
            _ => new List<ISceneNode>(),
        };
    }

    internal static Fleet GetSelectedFleet(GalaxyMapPlanet planet, int selectedIndex)
    {
        List<Fleet> fleets = planet?.Planet?.Fleets?.ToList() ?? new List<Fleet>();
        return selectedIndex >= 0 && selectedIndex < fleets.Count ? fleets[selectedIndex] : null;
    }

    internal static int GetDetailItemCount(GalaxyMapPlanet planet, int selectedIndex, int tab)
    {
        return GetDetailItems(GetSelectedFleet(planet, selectedIndex), tab).Count;
    }

    private static int GetDetailItemCount(Planet planet, int selectedIndex, int tab)
    {
        List<Fleet> fleets = planet?.Fleets?.ToList() ?? new List<Fleet>();
        Fleet fleet =
            selectedIndex >= 0 && selectedIndex < fleets.Count ? fleets[selectedIndex] : null;
        return GetDetailItems(fleet, tab).Count;
    }

    internal List<ISceneNode> GetContextItems(
        List<Fleet> fleets,
        HashSet<int> selectedFleetIndexes,
        Fleet selectedFleet,
        int activeTab,
        HashSet<int> selectedItemIndexes,
        int fleetIndex,
        int itemIndex
    )
    {
        fleets ??= new List<Fleet>();
        selectedFleetIndexes ??= new HashSet<int>();
        selectedItemIndexes ??= new HashSet<int>();

        if (fleetIndex >= 0)
        {
            if (fleetIndex < fleets.Count && selectedFleetIndexes.Contains(fleetIndex))
            {
                List<ISceneNode> selectedFleets = selectedFleetIndexes
                    .Where(index => index >= 0 && index < fleets.Count)
                    .OrderBy(index => index)
                    .Select(index => (ISceneNode)fleets[index])
                    .ToList();
                if (selectedFleets.Count > 0)
                    return selectedFleets;
            }

            if (fleetIndex < fleets.Count)
                return new List<ISceneNode> { fleets[fleetIndex] };

            return new List<ISceneNode>();
        }

        if (itemIndex >= 0)
        {
            List<ISceneNode> detailItems = GetDetailItems(selectedFleet, activeTab);
            if (itemIndex < detailItems.Count && selectedItemIndexes.Contains(itemIndex))
            {
                List<ISceneNode> selectedItems = GetSelectedDetailItems(
                    selectedItemIndexes,
                    detailItems
                );
                if (selectedItems.Count > 0)
                    return selectedItems;
            }

            if (itemIndex < detailItems.Count)
                return new List<ISceneNode> { detailItems[itemIndex] };

            return new List<ISceneNode>();
        }

        return new List<ISceneNode>();
    }

    public List<ISceneNode> GetContextItems()
    {
        if (lastData == null)
            return new List<ISceneNode>();

        List<Fleet> fleets = lastData.Planet?.Fleets?.ToList() ?? new List<Fleet>();
        Fleet selectedFleet =
            selectedIndex >= 0 && selectedIndex < fleets.Count ? fleets[selectedIndex] : null;

        return GetContextItems(
            fleets,
            selectedFleetItems,
            selectedFleet,
            activeTab,
            selectedItems,
            contextFleetIndex,
            contextDetailItemIndex
        );
    }

    internal void CaptureContextTarget(PointerEventData eventData)
    {
        GameObject target = GetRaycastTarget(eventData);
        FleetListRowView row = target?.GetComponentInParent<FleetListRowView>();
        if (row != null && listRowViews.Contains(row))
        {
            selectedIndex = row.Index;
            contextFleetIndex = row.Index;
            contextDetailItemIndex = -1;
            SelectContextItem(selectedFleetItems, row.Index);
            selectedItems.Clear();
            return;
        }

        StrategyUnitCardView item = target?.GetComponentInParent<StrategyUnitCardView>();
        if (item != null && detailItemViews.Contains(item))
        {
            contextFleetIndex = -1;
            contextDetailItemIndex = item.Index;
            SelectContextItem(selectedItems, item.Index);
            return;
        }

        contextFleetIndex = -1;
        contextDetailItemIndex = -1;
    }

    public bool TryGetDragPreview(int sourceX, int sourceY, out DragPreview preview)
    {
        preview = null;
        if (lastData == null)
            return false;

        int fleetIndex = dragFleetIndex;
        if (fleetIndex >= 0 && fleetIndex < listRowViews.Count)
        {
            FleetListRowView row = listRowViews[fleetIndex];
            if (row != null && row.TryGetDragImage(out Texture texture, out RectTransform rect))
            {
                RectInt rowRect = UILayout.GetSourceRect(row.transform as RectTransform);
                RectInt childRect = UILayout.GetSourceRect(rect);
                RectInt sourceRect = GetVisibleContentRect(fleetListScrollArea, rowRect, childRect);
                preview = UILayout.CreateDragPreview(texture, sourceRect, sourceX, sourceY);
                return true;
            }
        }

        int itemIndex = dragDetailItemIndex;
        if (itemIndex >= 0 && itemIndex < detailItemViews.Count)
        {
            StrategyUnitCardView item = detailItemViews[itemIndex];
            if (item != null && item.TryGetDragImage(out Texture texture, out RectTransform rect))
            {
                RectInt itemRect = UILayout.GetSourceRect(item.transform as RectTransform);
                RectInt childRect = UILayout.GetSourceRect(rect);
                RectInt sourceRect = GetVisibleContentRect(
                    detailItemsScrollArea,
                    itemRect,
                    childRect
                );
                preview = UILayout.CreateDragPreview(texture, sourceRect, sourceX, sourceY);
                return true;
            }
        }

        return false;
    }

    internal StrategyStatusTarget GetStatusTarget(GalaxyMapPlanet planet)
    {
        if (lastData == null)
            return null;

        List<ISceneNode> items = GetContextItems();
        return items.Count == 1 ? new StrategyStatusTarget(planet, items[0]) : null;
    }

    StrategyStatusTarget IStrategyWindowStatusTargetView.GetStatusTarget(GalaxyMapPlanet planet)
    {
        return GetStatusTarget(planet);
    }

    public ISceneNode GetDestinationTargetItem()
    {
        List<ISceneNode> items = GetContextItems();
        return items.Count > 0 ? items[0] : null;
    }

    public void ClearSelection()
    {
        selectedFleetItems.Clear();
        selectedItems.Clear();
        contextFleetIndex = -1;
        contextDetailItemIndex = -1;
        dragFleetIndex = -1;
        dragDetailItemIndex = -1;
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

    internal List<StrategyMenuCommand> BuildContextMenu(
        IReadOnlyList<ISceneNode> items,
        bool playerControlsItems,
        bool canMove,
        bool canCreateMission,
        bool canRetire
    )
    {
        if (items == null || items.Count == 0)
            return new List<StrategyMenuCommand>
            {
                new StrategyMenuCommand(
                    StrategyContextMenuActions.Encyclopedia,
                    "Encyclopedia",
                    false
                ),
                new StrategyMenuCommand(StrategyContextMenuActions.Status, "Status", false),
            };

        int fleetCount = items.OfType<Fleet>().Count();
        int shipCount = items.OfType<CapitalShip>().Count();
        if (fleetCount > 0 || shipCount > 0)
        {
            int itemCount = fleetCount + shipCount;
            List<StrategyMenuCommand> commands = new List<StrategyMenuCommand>
            {
                new StrategyMenuCommand(
                    StrategyContextMenuActions.Move,
                    "Move",
                    playerControlsItems
                ),
                new StrategyMenuCommand(
                    StrategyContextMenuActions.MoveConfirm,
                    "Confirmed Move",
                    playerControlsItems
                ),
            };

            if (fleetCount > 0 && shipCount == 0 && fleetCount == 1)
            {
                commands.Add(
                    new StrategyMenuCommand(
                        StrategyContextMenuActions.Submenu,
                        "Planetary Bombardment",
                        playerControlsItems
                    )
                );
                commands.Add(new StrategyMenuCommand(0, "Planetary Assault", playerControlsItems));
            }
            else if (fleetCount == 0)
            {
                commands.Add(new StrategyMenuCommand(0, "Create Fleet", playerControlsItems));
            }

            commands.Add(
                new StrategyMenuCommand(0, "Rename", itemCount == 1 && playerControlsItems)
            );
            commands.Add(
                new StrategyMenuCommand(
                    StrategyContextMenuActions.Encyclopedia,
                    "Encyclopedia",
                    itemCount == 1
                )
            );
            commands.Add(
                new StrategyMenuCommand(StrategyContextMenuActions.Status, "Status", itemCount == 1)
            );
            commands.Add(
                new StrategyMenuCommand(
                    StrategyContextMenuActions.Scrap,
                    "Scrap",
                    playerControlsItems
                )
            );
            return commands;
        }

        int fighterCount = items.OfType<Starfighter>().Count();
        int troopCount = items.OfType<Regiment>().Count();
        if (fighterCount > 0 || troopCount > 0)
        {
            return new List<StrategyMenuCommand>
            {
                new StrategyMenuCommand(StrategyContextMenuActions.Move, "Move", canMove),
                new StrategyMenuCommand(
                    StrategyContextMenuActions.MoveConfirm,
                    "Confirmed Move",
                    canMove
                ),
                new StrategyMenuCommand(
                    StrategyContextMenuActions.Encyclopedia,
                    "Encyclopedia",
                    items.Count == 1
                ),
                new StrategyMenuCommand(
                    StrategyContextMenuActions.Status,
                    "Status",
                    items.Count == 1
                ),
                new StrategyMenuCommand(StrategyContextMenuActions.Scrap, "Scrap", canMove),
            };
        }

        List<ISceneNode> personnel = items
            .Where(item => item is Officer || item is SpecialForces)
            .ToList();
        if (personnel.Count > 0)
        {
            List<StrategyMenuCommand> commands = new List<StrategyMenuCommand>
            {
                new StrategyMenuCommand(StrategyContextMenuActions.Move, "Move", canMove),
                new StrategyMenuCommand(
                    StrategyContextMenuActions.MoveConfirm,
                    "Confirmed Move",
                    canMove
                ),
                new StrategyMenuCommand(
                    StrategyContextMenuActions.CreateMission,
                    "Mission",
                    canCreateMission
                ),
            };

            if (!personnel.OfType<SpecialForces>().Any())
                commands.Add(
                    new StrategyMenuCommand(StrategyContextMenuActions.Submenu, "Command", canMove)
                );

            commands.Add(
                new StrategyMenuCommand(
                    StrategyContextMenuActions.Encyclopedia,
                    "Encyclopedia",
                    personnel.Count == 1
                )
            );
            commands.Add(
                new StrategyMenuCommand(
                    StrategyContextMenuActions.Status,
                    "Status",
                    personnel.Count == 1
                )
            );
            commands.Add(
                new StrategyMenuCommand(StrategyContextMenuActions.Retire, "Retire ", canRetire)
            );
            return commands;
        }

        return new List<StrategyMenuCommand>
        {
            new StrategyMenuCommand(StrategyContextMenuActions.Encyclopedia, "Encyclopedia", false),
            new StrategyMenuCommand(StrategyContextMenuActions.Status, "Status", false),
        };
    }

    internal int GetContextMenuWidth(List<StrategyMenuCommand> commands)
    {
        foreach (StrategyMenuCommand command in commands)
        {
            if (command.Text == "Planetary Bombardment")
                return bombardmentContextMenuWidth;
        }

        return contextMenuWidth;
    }

    private void Awake()
    {
        VerifyReferences();
        BindControls();
    }

    private void OnDestroy()
    {
        UnbindScrollAreaDragEvents();
    }

    private void BindScrollAreaDragEvents()
    {
        if (scrollAreaDragEventsBound)
            return;

        if (fleetListScrollArea != null)
        {
            fleetListScrollArea.Dragged += DispatchScrollAreaDrag;
            fleetListScrollArea.DragEnded += DispatchScrollAreaDragEnd;
            fleetListScrollArea.Dropped += HandleFleetListDropped;
        }

        if (detailItemsScrollArea != null)
        {
            detailItemsScrollArea.Dragged += DispatchScrollAreaDrag;
            detailItemsScrollArea.DragEnded += DispatchScrollAreaDragEnd;
        }

        scrollAreaDragEventsBound = true;
    }

    private void UnbindScrollAreaDragEvents()
    {
        if (!scrollAreaDragEventsBound)
            return;

        if (fleetListScrollArea != null)
        {
            fleetListScrollArea.Dragged -= DispatchScrollAreaDrag;
            fleetListScrollArea.DragEnded -= DispatchScrollAreaDragEnd;
            fleetListScrollArea.Dropped -= HandleFleetListDropped;
        }

        if (detailItemsScrollArea != null)
        {
            detailItemsScrollArea.Dragged -= DispatchScrollAreaDrag;
            detailItemsScrollArea.DragEnded -= DispatchScrollAreaDragEnd;
        }

        scrollAreaDragEventsBound = false;
    }

    private void DispatchScrollAreaDrag(PointerEventData eventData)
    {
        if (uiContext == null || eventData == null)
            return;

        uiContext.Dispatcher.Send(new StrategyUIRequests.WindowItemDragMove(eventData));
    }

    private void DispatchScrollAreaDragEnd(PointerEventData eventData)
    {
        if (uiContext == null || eventData == null)
            return;

        uiContext.Dispatcher.Send(new StrategyUIRequests.WindowItemDragEnd(eventData));
    }

    private void InitializeState(int initialTab, int initialSelectedIndex)
    {
        if (stateInitialized)
            return;

        activeTab = Mathf.Max(0, initialTab);
        selectedIndex = initialSelectedIndex;
        stateInitialized = true;
    }

    private void SetActiveTab(int tab)
    {
        if (tab == activeTab)
            return;

        activeTab = tab;
        selectedItems.Clear();
        contextDetailItemIndex = -1;
        dragDetailItemIndex = -1;
    }

    private void BindControls()
    {
        for (int i = 0; i < tabButtons.Length; i++)
        {
            int tab = i;
            tabButtons[i].onClick.AddListener(() => SelectTab(tab));
        }
    }

    private void SelectTab(int tab)
    {
        if (tab < 0 || tab >= tabButtons.Length)
            return;

        SetActiveTab(tab);
        uiContext?.Dispatcher.Send(new StrategyUIRequests.RequestRender());
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

    private static void SelectIndexedItemForDrag(
        HashSet<int> selection,
        int index,
        int count,
        int itemsPerRow = 1
    )
    {
        SelectableListSelection.SelectIndexedItemForDrag(selection, index, count, itemsPerRow);
    }

    private static void SelectContextItem(HashSet<int> selection, int index)
    {
        if (selection.Contains(index))
            return;

        selection.Clear();
        selection.Add(index);
    }

    private static List<ISceneNode> GetSelectedDetailItems(
        HashSet<int> selectedItemIndexes,
        List<ISceneNode> items
    )
    {
        return selectedItemIndexes
            .Where(index => index >= 0 && index < items.Count)
            .OrderBy(index => index)
            .Select(index => items[index])
            .ToList();
    }

    private void RenderSelectedFleet(FleetWindowRenderData data)
    {
        bannerImage.gameObject.SetActive(data.HasSelectedFleet);
        bannerEnrouteOverlayImage.gameObject.SetActive(data.HasSelectedFleet);
        bannerDamagedOverlayImage.gameObject.SetActive(data.HasSelectedFleet);
        fleetNameTextField.gameObject.SetActive(data.HasSelectedFleet);
        capacityLeftTextField.gameObject.SetActive(data.HasSelectedFleet && data.ShowCapacity);
        capacityRightTextField.gameObject.SetActive(data.HasSelectedFleet && data.ShowCapacity);
        tabsRoot.gameObject.SetActive(data.HasSelectedFleet);
        detailItemsScrollArea.gameObject.SetActive(data.HasSelectedFleet);

        if (!data.HasSelectedFleet)
        {
            HideTabs();
            HideDetailItems();
            return;
        }

        SetImageAtTemplateOrigin(bannerImage, data.BannerTexture);
        SetImageAtTemplateOrigin(bannerEnrouteOverlayImage, data.BannerEnrouteOverlayTexture);
        SetImageAtTemplateOrigin(bannerDamagedOverlayImage, data.BannerDamagedOverlayTexture);
        SetTemplateText(fleetNameTextField, data.FleetName, data.FleetNameColor);

        if (data.ShowCapacity)
        {
            SetTemplateText(capacityLeftTextField, data.CapacityLeft);
            SetTemplateText(capacityRightTextField, data.CapacityRight);
        }

        RenderTabs(data);
        RenderDetailItems(data.DetailItems);
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

    private void RenderFleetRows(IReadOnlyList<FleetListRowRenderData> rows)
    {
        IReadOnlyList<FleetListRowRenderData> safeRows =
            rows ?? System.Array.Empty<FleetListRowRenderData>();
        bool resetScroll = FleetRowsChanged(safeRows);
        fleetListScrollArea.SetContentHeight(
            GetFleetListScrollContentHeight(safeRows.Count),
            GetFleetListScrollStep(),
            resetScroll
        );
        for (int i = 0; i < safeRows.Count; i++)
        {
            FleetListRowView row = GetListRowView(i);
            row.SetIndex(i);
            row.Render(safeRows[i]);
        }

        for (int i = safeRows.Count; i < listRowViews.Count; i++)
            listRowViews[i].gameObject.SetActive(false);

        renderedAnyFleetRows = true;
        renderedFleetRowNames.Clear();
        for (int i = 0; i < safeRows.Count; i++)
            renderedFleetRowNames.Add(safeRows[i].Name ?? string.Empty);
    }

    private void RenderTabs(FleetWindowRenderData data)
    {
        for (int i = 0; i < tabImages.Length; i++)
        {
            bool active = i == data.ActiveTab;
            bool empty = GetTabCount(data, i) == 0;
            UILayout.SetInteractiveImageTexture(
                tabImages[i],
                GetTabTexture(data.SelectedFleetOwnerFactionId, i, empty, active)
            );
        }
    }

    private void HideTabs()
    {
        for (int i = 0; i < tabImages.Length; i++)
            tabImages[i].gameObject.SetActive(false);
    }

    private void RenderDetailItems(IReadOnlyList<StrategyUnitCardRenderData> items)
    {
        IReadOnlyList<StrategyUnitCardRenderData> safeItems =
            items ?? System.Array.Empty<StrategyUnitCardRenderData>();
        bool resetScroll = DetailItemsChanged(safeItems);
        detailItemsScrollArea.SetContentHeight(
            GetDetailScrollContentHeight(safeItems.Count),
            GetDetailScrollStep(),
            resetScroll
        );
        for (int i = 0; i < safeItems.Count; i++)
        {
            StrategyUnitCardView item = GetDetailItemView(i);
            item.SetIndex(i);
            item.Render(safeItems[i]);
        }

        for (int i = safeItems.Count; i < detailItemViews.Count; i++)
            detailItemViews[i].gameObject.SetActive(false);

        renderedAnyDetailItems = true;
        renderedDetailActiveTab = activeTab;
        renderedDetailSelectedIndex = selectedIndex;
        renderedDetailItemNames.Clear();
        for (int i = 0; i < safeItems.Count; i++)
            renderedDetailItemNames.Add(safeItems[i].Name ?? string.Empty);
    }

    private void HideDetailItems()
    {
        for (int i = 0; i < detailItemViews.Count; i++)
            detailItemViews[i].gameObject.SetActive(false);
    }

    private FleetListRowView GetListRowView(int index)
    {
        while (listRowViews.Count <= index)
        {
            FleetListRowView row = Instantiate(
                fleetListRowTemplate,
                fleetListScrollArea.ContentRoot
            );
            row.name = $"FleetListRow{listRowViews.Count}";
            row.Initialize(uiContext);
            row.Pressed += HandleFleetRowPressed;
            row.Released += HandleFleetRowReleased;
            row.Dropped += HandleFleetRowReleased;
            listRowViews.Add(row);
        }

        return listRowViews[index];
    }

    private RectInt GetVisibleContentRect(
        ScrollAreaView scrollArea,
        RectInt itemRect,
        RectInt innerRect
    )
    {
        RectInt scrollAreaRect = UILayout.GetSourceRect(scrollArea.transform as RectTransform);
        RectInt viewportRect = UILayout.GetSourceRect(scrollArea.ViewportRoot);
        int scrollY = Mathf.RoundToInt(scrollArea.ContentRoot.anchoredPosition.y);
        return new RectInt(
            lastData.X + scrollAreaRect.x + viewportRect.x + itemRect.x + innerRect.x,
            lastData.Y + scrollAreaRect.y + viewportRect.y + itemRect.y - scrollY + innerRect.y,
            innerRect.width,
            innerRect.height
        );
    }

    private int GetButtonAction(int index)
    {
        return index >= 0 && index < buttonActions.Length ? buttonActions[index] : 0;
    }

    private int GetTabCount(FleetWindowRenderData data, int index)
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

    private Texture2D GetDetailBackgroundTexture(string ownerFactionId)
    {
        return uiContext?.GetTexture(GetFleetTheme(ownerFactionId)?.DetailBackgroundImagePath);
    }

    private Texture2D GetFleetDetailSelectionTexture(string ownerFactionId)
    {
        return uiContext?.GetTexture(
            uiContext?.GetTheme(ownerFactionId)?.GetFleetDetailSelectionImagePath()
        );
    }

    private Texture2D GetDetailItemBackgroundTexture(Fleet fleet, ISceneNode item, int activeTab)
    {
        bool enroute = IsItemInTransit(fleet, item);
        if (activeTab == 2 && item is Regiment)
            return enroute ? personnelEnrouteBackgroundTexture : personnelBackgroundTexture;

        if (activeTab == 3 && item is Officer or SpecialForces)
        {
            if (enroute)
                return GetPersonnelEnrouteBackgroundTexture(item)
                    ?? personnelEnrouteBackgroundTexture;

            return personnelBackgroundTexture;
        }

        return null;
    }

    private Texture2D GetFleetListEnrouteOverlayTexture(Fleet fleet)
    {
        if (!IsFleetInTransit(fleet))
            return null;

        UnitTileIcons icons = uiContext
            ?.GetTheme(fleet.OwnerInstanceID)
            ?.PlanetOverlayTheme?.UnitTileIcons;
        return uiContext?.GetTexture(icons?.FleetListEnrouteIconImagePath);
    }

    private Texture2D GetFleetListDamagedOverlayTexture(Fleet fleet)
    {
        if (fleet == null)
            return null;

        UnitTileIcons icons = uiContext
            ?.GetTheme(fleet.OwnerInstanceID)
            ?.PlanetOverlayTheme?.UnitTileIcons;
        if (fleet.CapitalShips.Any(ship => ship.IsDamaged()))
            return uiContext?.GetTexture(icons?.FleetListDamagedIconImagePath);

        return null;
    }

    private Texture2D GetFleetStarfighterBadgeTexture(string ownerInstanceId, bool visible)
    {
        return GetFleetBadgeTexture(
            ownerInstanceId,
            visible,
            icons => icons.FleetStarfightersBadgeImagePath
        );
    }

    private Texture2D GetFleetTroopBadgeTexture(string ownerInstanceId, bool visible)
    {
        return GetFleetBadgeTexture(
            ownerInstanceId,
            visible,
            icons => icons.FleetTroopsBadgeImagePath
        );
    }

    private Texture2D GetFleetPersonnelBadgeTexture(string ownerInstanceId, bool visible)
    {
        return GetFleetBadgeTexture(
            ownerInstanceId,
            visible,
            icons => icons.FleetPersonnelBadgeImagePath
        );
    }

    private Texture2D GetFleetBadgeTexture(
        string ownerInstanceId,
        bool visible,
        System.Func<UnitTileIcons, string> selectPath
    )
    {
        if (!visible)
            return null;

        UnitTileIcons icons = uiContext
            ?.GetTheme(ownerInstanceId)
            ?.PlanetOverlayTheme?.UnitTileIcons;
        return uiContext?.GetTexture(icons == null ? null : selectPath(icons));
    }

    private Texture2D GetFleetDetailConstructionOverlayTexture(ISceneNode item)
    {
        if (item is not IManufacturable manufacturable)
            return null;

        if (manufacturable.GetManufacturingStatus() != ManufacturingStatus.Building)
            return null;

        UnitTileIcons icons = uiContext
            ?.GetTheme(item.GetOwnerInstanceID())
            ?.PlanetOverlayTheme?.UnitTileIcons;
        return uiContext?.GetTexture(icons?.FleetConstructionSmallImagePath);
    }

    private Texture2D GetFleetDetailEnrouteOverlayTexture(Fleet fleet, ISceneNode item)
    {
        if (!IsItemInTransit(fleet, item) || item is Regiment or Officer or SpecialForces)
            return null;

        if (item is IManufacturable { ManufacturingStatus: ManufacturingStatus.Building })
            return null;

        string path = SelectStatusPath(item.InTransitSmallImagePath, item.InTransitImagePath);
        return string.IsNullOrEmpty(path) ? null : uiContext?.GetTexture(path);
    }

    private Texture2D GetPersonnelEnrouteBackgroundTexture(ISceneNode item)
    {
        string path = SelectStatusPath(item.InTransitSmallImagePath, item.InTransitImagePath);
        return string.IsNullOrEmpty(path) ? null : uiContext?.GetTexture(path);
    }

    private Texture2D GetFleetDetailDamagedOverlayTexture(ISceneNode item)
    {
        if (item == null)
            return null;

        if (item is IManufacturable { ManufacturingStatus: ManufacturingStatus.Building })
            return null;

        if (item is Officer { InjuryPoints: > 0 } && !string.IsNullOrEmpty(item.InjuredImagePath))
            return uiContext?.GetTexture(item.InjuredImagePath);

        bool damaged =
            item is CapitalShip capitalShip && capitalShip.IsDamaged()
            || item is Starfighter starfighter && starfighter.HasLosses();
        if (!damaged)
            return null;

        string path = SelectStatusPath(item.DamagedSmallImagePath, item.DamagedImagePath);
        return string.IsNullOrEmpty(path) ? null : uiContext?.GetTexture(path);
    }

    private Texture2D GetFleetBannerTexture(Fleet fleet)
    {
        if (fleet == null)
            return null;

        FleetWindowTheme fleetTheme = GetFleetTheme(fleet.OwnerInstanceID);
        return uiContext?.GetTexture(fleetTheme?.BannerImagePath);
    }

    private Texture2D GetFleetBannerEnrouteOverlayTexture(Fleet fleet)
    {
        if (!IsFleetInTransit(fleet))
            return null;

        return uiContext?.GetTexture(
            GetStatusTheme(fleet.OwnerInstanceID)?.FleetBannerEnrouteImagePath
        );
    }

    private Texture2D GetFleetBannerDamagedOverlayTexture(Fleet fleet)
    {
        if (fleet == null || !fleet.CapitalShips.Any(ship => ship.IsDamaged()))
            return null;

        return uiContext?.GetTexture(
            GetStatusTheme(fleet.OwnerInstanceID)?.FleetBannerDamagedImagePath
        );
    }

    private static string SelectStatusPath(string preferredPath, string fallbackPath)
    {
        return !string.IsNullOrEmpty(preferredPath) ? preferredPath : fallbackPath;
    }

    private static bool IsFleetInTransit(Fleet fleet)
    {
        if (fleet == null)
            return false;

        return fleet.Movement != null;
    }

    private static bool IsItemInTransit(Fleet fleet, ISceneNode item)
    {
        if (fleet?.Movement != null)
            return true;

        if (item is IMovable { Movement: not null })
            return true;

        return item?.GetParentOfType<CapitalShip>()?.Movement != null;
    }

    private Texture2D GetTabTexture(string ownerFactionId, int tab, bool empty, bool active)
    {
        FleetWindowTabsTheme tabs = GetFleetTheme(ownerFactionId)?.Tabs;
        WindowTabImageTheme theme = tab switch
        {
            0 => tabs?.CapitalShips,
            1 => tabs?.Starfighters,
            2 => tabs?.Regiments,
            3 => tabs?.Officers,
            _ => null,
        };

        return uiContext?.GetTexture(theme?.GetImagePathForContent(!empty, active));
    }

    private FleetWindowTheme GetFleetTheme(string ownerFactionId)
    {
        return uiContext?.GetTheme(ownerFactionId)?.StrategyWindows?.Fleet;
    }

    private StatusWindowTheme GetStatusTheme(string ownerFactionId)
    {
        return uiContext?.GetTheme(ownerFactionId)?.StrategyWindows?.Status;
    }

    private Color32 GetFactionColor(string ownerFactionId)
    {
        FactionTheme theme = uiContext?.GetTheme(ownerFactionId);
        return theme?.GetPrimaryColor() ?? Color.white;
    }

    private StrategyUnitCardView GetDetailItemView(int index)
    {
        while (detailItemViews.Count <= index)
        {
            StrategyUnitCardView item = Instantiate(
                detailItemTemplate,
                detailItemsScrollArea.ContentRoot
            );
            item.name = $"FleetDetailItem{detailItemViews.Count}";
            item.gameObject.SetActive(false);
            item.Pressed += HandleDetailItemPressed;
            item.Released += HandleDetailItemReleased;
            item.Dropped += HandleDetailItemReleased;
            detailItemViews.Add(item);
        }

        return detailItemViews[index];
    }

    private void HandleFleetRowPressed(FleetListRowView row, PointerEventData eventData)
    {
        if (row == null || lastData == null)
            return;

        int fleetIndex = row.Index;
        int fleetCount = lastData.Planet?.Fleets?.Count ?? 0;
        if (fleetIndex < 0 || fleetIndex >= fleetCount)
            return;

        if (
            uiRuntime?.Targeting.IsTargeting == true
            && eventData.button == PointerEventData.InputButton.Left
        )
            return;

        selectedIndex = fleetIndex;
        contextFleetIndex = fleetIndex;
        contextDetailItemIndex = -1;
        dragFleetIndex = fleetIndex;
        dragDetailItemIndex = -1;

        if (eventData.button == PointerEventData.InputButton.Right)
        {
            SelectContextItem(selectedFleetItems, fleetIndex);
            selectedItems.Clear();
            uiContext?.Dispatcher.Send(new StrategyUIRequests.RequestRender());
            return;
        }

        if (eventData.button != PointerEventData.InputButton.Left)
            return;

        SelectIndexedItemForDrag(selectedFleetItems, fleetIndex, fleetCount);
        selectedItems.Clear();
        if (TryGetDesktopPosition(eventData, out int x, out int y))
            uiContext?.Dispatcher.Send(
                new StrategyUIRequests.StartWindowItemDrag(GetWindowId(), x, y)
            );
        else
            uiContext?.Dispatcher.Send(new StrategyUIRequests.RequestRender());
    }

    private void HandleFleetRowReleased(FleetListRowView row, PointerEventData eventData)
    {
        if (row != null)
            TrySelectTarget(GetFleetAtIndex(row.Index));
    }

    private void HandleFleetListDropped(PointerEventData eventData)
    {
        TrySelectTarget((ISceneNode)null);
    }

    private void HandleDetailItemPressed(StrategyUnitCardView item, PointerEventData eventData)
    {
        if (item == null || lastData == null)
            return;

        int itemIndex = item.Index;
        int itemCount = GetDetailItemCount(lastData.Planet, selectedIndex, activeTab);
        if (itemIndex < 0 || itemIndex >= itemCount)
            return;

        if (
            uiRuntime?.Targeting.IsTargeting == true
            && eventData.button == PointerEventData.InputButton.Left
        )
            return;

        contextFleetIndex = -1;
        contextDetailItemIndex = itemIndex;
        dragFleetIndex = -1;
        dragDetailItemIndex = itemIndex;

        if (eventData.button == PointerEventData.InputButton.Right)
        {
            SelectContextItem(selectedItems, itemIndex);
            uiContext?.Dispatcher.Send(new StrategyUIRequests.RequestRender());
            return;
        }

        if (eventData.button != PointerEventData.InputButton.Left)
            return;

        SelectIndexedItemForDrag(selectedItems, itemIndex, itemCount);
        if (TryGetDesktopPosition(eventData, out int x, out int y))
            uiContext?.Dispatcher.Send(
                new StrategyUIRequests.StartWindowItemDrag(GetWindowId(), x, y)
            );
        else
            uiContext?.Dispatcher.Send(new StrategyUIRequests.RequestRender());
    }

    private void HandleDetailItemReleased(StrategyUnitCardView item, PointerEventData eventData)
    {
        if (item != null)
            TrySelectTarget(GetDetailItemAtIndex(item.Index));
    }

    private bool TryGetDesktopPosition(PointerEventData eventData, out int x, out int y)
    {
        x = 0;
        y = 0;
        UIWindow window = GetWindowShell();
        return window != null
            && window.TryGetDesktopPosition(eventData, eventData.position, out x, out y);
    }

    private int GetWindowId()
    {
        UIWindow window = GetWindowShell();
        return window == null ? 0 : window.Id;
    }

    private UIWindow GetWindowShell()
    {
        if (windowShell == null)
            windowShell = GetComponent<UIWindow>();

        return windowShell;
    }

    private Fleet GetFleetAtIndex(int fleetIndex)
    {
        List<Fleet> fleets = lastData?.Planet?.Fleets?.ToList() ?? new List<Fleet>();
        return fleetIndex >= 0 && fleetIndex < fleets.Count ? fleets[fleetIndex] : null;
    }

    private ISceneNode GetDetailItemAtIndex(int itemIndex)
    {
        List<Fleet> fleets = lastData?.Planet?.Fleets?.ToList() ?? new List<Fleet>();
        Fleet fleet =
            selectedIndex >= 0 && selectedIndex < fleets.Count ? fleets[selectedIndex] : null;
        List<ISceneNode> items = GetDetailItems(fleet, activeTab);
        return itemIndex >= 0 && itemIndex < items.Count ? items[itemIndex] : null;
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
        if (fleetListScrollArea == null)
            throw new MissingReferenceException($"{name}/FleetListScrollArea is missing.");
        if (fleetListRowTemplate == null)
            throw new MissingReferenceException($"{name}/FleetListRowTemplate is missing.");
        if (detailBackgroundImage == null)
            throw new MissingReferenceException($"{name}/DetailBackgroundImage is missing.");
        if (bannerImage == null)
            throw new MissingReferenceException($"{name}/BannerImage is missing.");
        if (bannerEnrouteOverlayImage == null)
            throw new MissingReferenceException($"{name}/BannerEnrouteOverlayImage is missing.");
        if (bannerDamagedOverlayImage == null)
            throw new MissingReferenceException($"{name}/BannerDamagedOverlayImage is missing.");
        if (fleetNameTextField == null)
            throw new MissingReferenceException($"{name}/FleetNameTextField is missing.");
        if (capacityLeftTextField == null)
            throw new MissingReferenceException($"{name}/CapacityLeftTextField is missing.");
        if (capacityRightTextField == null)
            throw new MissingReferenceException($"{name}/CapacityRightTextField is missing.");
        if (tabsRoot == null)
            throw new MissingReferenceException($"{name}/Tabs is missing.");
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
        if (detailItemsScrollArea == null)
            throw new MissingReferenceException($"{name}/DetailItemsScrollArea is missing.");
        if (detailItemsScrollPaddingTemplate == null)
            throw new MissingReferenceException(
                $"{name}/DetailItemsScrollPaddingTemplate is missing."
            );
        if (detailItemTemplate == null)
            throw new MissingReferenceException($"{name}/DetailItemTemplate is missing.");
        if (personnelBackgroundTexture == null)
            throw new MissingReferenceException($"{name}/PersonnelBackgroundTexture is missing.");
        if (personnelEnrouteBackgroundTexture == null)
            throw new MissingReferenceException(
                $"{name}/PersonnelEnrouteBackgroundTexture is missing."
            );

        fleetListRowTemplate.gameObject.SetActive(false);
        detailItemsScrollPaddingTemplate.gameObject.SetActive(false);
        detailItemTemplate.gameObject.SetActive(false);
    }

    private bool FleetRowsChanged(IReadOnlyList<FleetListRowRenderData> rows)
    {
        if (!renderedAnyFleetRows || renderedFleetRowNames.Count != rows.Count)
            return true;

        for (int i = 0; i < rows.Count; i++)
        {
            if (renderedFleetRowNames[i] != (rows[i].Name ?? string.Empty))
                return true;
        }

        return false;
    }

    private bool DetailItemsChanged(IReadOnlyList<StrategyUnitCardRenderData> items)
    {
        if (
            !renderedAnyDetailItems
            || renderedDetailActiveTab != activeTab
            || renderedDetailSelectedIndex != selectedIndex
            || renderedDetailItemNames.Count != items.Count
        )
            return true;

        for (int i = 0; i < items.Count; i++)
        {
            if (renderedDetailItemNames[i] != (items[i].Name ?? string.Empty))
                return true;
        }

        return false;
    }

    private int GetFleetListRowHeight()
    {
        return UILayout.GetSourceRect(fleetListRowTemplate.transform as RectTransform).height;
    }

    private int GetFleetDetailItemHeight()
    {
        return UILayout.GetSourceRect(detailItemTemplate.transform as RectTransform).height;
    }

    private static void SetImage(RawImage image, Texture texture)
    {
        image.texture = texture;
        image.enabled = texture != null;
        image.gameObject.SetActive(texture != null);
        image.raycastTarget = false;
    }

    private static void SetImage(RawImage image, Texture texture, int x, int y)
    {
        image.texture = texture;
        image.enabled = texture != null;
        image.gameObject.SetActive(texture != null);
        image.raycastTarget = false;
        if (texture != null)
            SetSourceRect(image.rectTransform, x, y, texture.width, texture.height);
    }

    private static void SetImage(
        RawImage image,
        Texture texture,
        int x,
        int y,
        int width,
        int height
    )
    {
        image.texture = texture;
        image.enabled = texture != null;
        image.gameObject.SetActive(texture != null);
        image.raycastTarget = false;
        SetSourceRect(image.rectTransform, x, y, width, height);
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
        SetTemplateText(textField, text, textField.color);
    }

    private static void SetTemplateText(TextMeshProUGUI textField, string text, Color32 color)
    {
        UILayout.SetTextContent(textField, text, color);
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
}

public sealed class FleetWindowRenderData
{
    public int X;
    public int Y;
    internal GalaxyMapPlanet GalaxyMapPlanet;
    public Planet Planet;
    public string OwnerFactionId;
    public bool Active;
    public string SelectedFleetOwnerFactionId;
    public int ActiveTab;
    public bool HasSelectedFleet;
    public bool ShowCapacity;
    public string Caption;
    public string FleetName;
    public string CapacityLeft;
    public string CapacityRight;
    public Color32 FleetNameColor;
    public Texture BannerTexture;
    public Texture BannerEnrouteOverlayTexture;
    public Texture BannerDamagedOverlayTexture;
    public int SelectedIndex;
    public IReadOnlyCollection<int> SelectedFleetItems;
    public IReadOnlyCollection<int> SelectedItems;
    public List<FleetListRowRenderData> FleetRows = new List<FleetListRowRenderData>();
    public List<int> TabCounts = new List<int>();
    public List<StrategyUnitCardRenderData> DetailItems = new List<StrategyUnitCardRenderData>();
}
