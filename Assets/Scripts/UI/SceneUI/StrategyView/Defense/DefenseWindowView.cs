using System.Collections.Generic;
using System.Linq;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Units;
using Rebellion.SceneGraph;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public sealed class DefenseWindowView
    : MonoBehaviour,
        IStrategyUIRuntimeReceiver,
        IStrategyWindowSelectionView,
        IStrategyWindowStatusTargetView,
        IStrategyWindowContextItemsView,
        IStrategyWindowDragImageView,
        IStrategyWindowContent,
        IPlanetIconWindowView
{
    private static readonly Color32 White = new Color32(255, 255, 255, 255);
    private readonly HashSet<int> selectedItems = new HashSet<int>();
    private readonly List<StrategyUnitCardView> itemCards = new List<StrategyUnitCardView>();
    private readonly List<string> renderedItemNames = new List<string>();

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
    private Texture2D shieldAvailableTabTexture;

    [SerializeField]
    private Texture2D shieldActiveTabTexture;

    [SerializeField]
    private Texture2D shieldDisabledTabTexture;

    [SerializeField]
    private Texture2D batteryAvailableTabTexture;

    [SerializeField]
    private Texture2D batteryActiveTabTexture;

    [SerializeField]
    private Texture2D batteryDisabledTabTexture;

    [SerializeField]
    private TextMeshProUGUI tabTitleTextField;

    [SerializeField]
    private ScrollAreaView itemsScrollArea;

    [SerializeField]
    private GridLayoutGroup itemsGridLayout;

    [SerializeField]
    private StrategyUnitCardView itemCardTemplate;

    [SerializeField]
    private Texture2D personnelBackgroundTexture;

    [SerializeField]
    private Texture2D enrouteBackgroundTexture;

    private DefenseWindowRenderData lastData;
    private StrategyUIRuntime uiRuntime;
    private UIContext uiContext;
    private bool stateInitialized;
    private int activeTab;
    private int renderedActiveTab = -1;
    private bool renderedAnyItems;
    private bool scrollAreaDragEventsBound;
    private UIWindow windowShell;
    private int contextItemIndex = -1;
    private int dragItemIndex = -1;

    public GalaxyMapPlanet GalaxyMapPlanet { get; private set; }
    public PlanetIcon PlanetIcon => PlanetIcon.Defense;

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

        Render(
            new DefenseWindowRenderData
            {
                X = window.X,
                Y = window.Y,
                GalaxyMapPlanet = GalaxyMapPlanet,
                Planet = GalaxyMapPlanet?.Planet,
                OwnerFactionId = GalaxyMapPlanet?.OwnerFactionId,
                Active = active,
                ActiveTab = GetActiveTab(0),
                SelectedItems = GetSelectedItems(),
            }
        );
    }

    public void Initialize(StrategyUIRuntime uiRuntime)
    {
        this.uiRuntime = uiRuntime;
        uiContext = uiRuntime?.Context;
        BindScrollAreaDragEvents();
    }

    public void Render(DefenseWindowRenderData data)
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
        SetTemplateText(tabTitleTextField, data.TabTitle);

        RenderItems(data.Items);
        gameObject.SetActive(true);
    }

    private DefenseWindowRenderData CreateRenderData(DefenseWindowRenderData state)
    {
        InitializeState(state.ActiveTab);
        List<ISceneNode>[] tabItems = GetAllTabItems(state.Planet);
        activeTab = Mathf.Clamp(activeTab, 0, tabItems.Length - 1);
        DefenseWindowRenderData data = new DefenseWindowRenderData
        {
            X = state.X,
            Y = state.Y,
            GalaxyMapPlanet = state.GalaxyMapPlanet,
            Planet = state.Planet,
            OwnerFactionId = state.OwnerFactionId,
            Active = state.Active,
            ActiveTab = activeTab,
            Caption = state.Planet?.GetDisplayName() ?? state.Caption ?? string.Empty,
            TabTitle = GetTabTitle(activeTab),
            SelectedItems = selectedItems,
        };

        for (int i = 0; i < tabItems.Length; i++)
            data.TabCounts.Add(tabItems[i].Count);

        foreach (ISceneNode item in tabItems[activeTab])
        {
            data.SourceItems.Add(item);
            data.Items.Add(CreateItemRenderData(item, data));
        }

        return data;
    }

    internal int GetActiveTab(int initialTab)
    {
        InitializeState(initialTab);
        return activeTab;
    }

    internal IReadOnlyCollection<int> GetSelectedItems()
    {
        return selectedItems;
    }

    internal void SelectFinderTab(int tab)
    {
        InitializeState(tab);
        activeTab = tab;
    }

    private StrategyUnitCardRenderData CreateItemRenderData(
        ISceneNode item,
        DefenseWindowRenderData data
    )
    {
        int index = data.Items.Count;
        bool selected = data.SelectedItems != null && data.SelectedItems.Contains(index);
        return new StrategyUnitCardRenderData
        {
            Name = item.GetDisplayName(),
            NameColor = selected ? GetFactionColor(data.OwnerFactionId) : White,
            ShowName = true,
            BackgroundTexture = GetItemBackgroundTexture(item),
            EntityTexture = uiContext?.GetEntityTexture(item, true),
            ConstructionOverlayTexture = GetItemConstructionOverlayTexture(item),
            EnrouteOverlayTexture = GetItemEnrouteOverlayTexture(item),
            DamagedOverlayTexture = GetItemDamagedOverlayTexture(item),
            CapturedOverlayTexture = uiContext?.GetEntityCapturedOverlayTexture(item),
            SelectionTexture = selected ? GetSelectionTexture(data.OwnerFactionId) : null,
            CanDrag = CanDragItem(item),
        };
    }

    internal static List<ISceneNode>[] GetAllTabItems(Planet planet)
    {
        if (planet == null)
        {
            return new[]
            {
                new List<ISceneNode>(),
                new List<ISceneNode>(),
                new List<ISceneNode>(),
                new List<ISceneNode>(),
                new List<ISceneNode>(),
            };
        }

        return new[]
        {
            GetPersonnel(planet),
            planet.Regiments.Cast<ISceneNode>().ToList(),
            planet.Starfighters.Cast<ISceneNode>().ToList(),
            GetBuildings(planet, true),
            GetBuildings(planet, false),
        };
    }

    internal static List<ISceneNode> GetTabItems(Planet planet, int tab)
    {
        if (planet == null)
            return new List<ISceneNode>();

        return tab switch
        {
            0 => GetPersonnel(planet),
            1 => planet.Regiments.Cast<ISceneNode>().ToList(),
            2 => planet.Starfighters.Cast<ISceneNode>().ToList(),
            3 => GetBuildings(planet, true),
            4 => GetBuildings(planet, false),
            _ => new List<ISceneNode>(),
        };
    }

    internal static string GetTabTitle(int tab)
    {
        return tab switch
        {
            0 => "Personnel",
            1 => "Troops/Regiments",
            2 => "Fighter Squadrons",
            3 => "Planetary Shields",
            4 => "Planetary Batteries",
            _ => string.Empty,
        };
    }

    internal int GetItemScrollContentHeight(int itemCount)
    {
        int columns = GetItemColumnCount();
        int rows = Mathf.CeilToInt(itemCount / (float)columns);
        return rows * GetItemCellHeight();
    }

    internal List<ISceneNode> GetContextItems(
        IReadOnlyCollection<int> selectedIndexes,
        out ISceneNode hitItem
    )
    {
        return GetContextItemsForIndex(contextItemIndex, selectedIndexes, out hitItem);
    }

    private List<ISceneNode> GetContextItemsForIndex(
        int itemIndex,
        IReadOnlyCollection<int> selectedIndexes,
        out ISceneNode hitItem
    )
    {
        hitItem = null;
        List<ISceneNode> sourceItems = lastData?.SourceItems ?? new List<ISceneNode>();
        if (itemIndex < 0 || itemIndex >= sourceItems.Count)
            return new List<ISceneNode>();

        hitItem = sourceItems[itemIndex];
        if (selectedIndexes != null && selectedIndexes.Contains(itemIndex))
        {
            List<ISceneNode> selectedItems = selectedIndexes
                .Where(index => index >= 0 && index < sourceItems.Count)
                .OrderBy(index => index)
                .Select(index => sourceItems[index])
                .ToList();
            if (selectedItems.Count > 0)
                return selectedItems;
        }

        return new List<ISceneNode> { hitItem };
    }

    internal List<ISceneNode> GetContextItems(out ISceneNode hitItem)
    {
        return GetContextItems(selectedItems, out hitItem);
    }

    public List<ISceneNode> GetContextItems()
    {
        return GetContextItems(out _);
    }

    internal void CaptureContextTarget(PointerEventData eventData)
    {
        StrategyUnitCardView card = GetRaycastTarget(eventData)
            ?.GetComponentInParent<StrategyUnitCardView>();
        if (card != null && itemCards.Contains(card))
        {
            contextItemIndex = card.Index;
            SelectContextItem(selectedItems, card.Index);
            return;
        }

        contextItemIndex = -1;
    }

    public bool TryGetDragPreview(int sourceX, int sourceY, out DragPreview preview)
    {
        preview = null;
        if (lastData == null)
            return false;

        int itemIndex = dragItemIndex;
        if (itemIndex < 0 || itemIndex >= itemCards.Count)
            return false;

        StrategyUnitCardView card = itemCards[itemIndex];
        if (card == null || !card.TryGetDragImage(out Texture texture, out RectTransform rect))
            return false;

        RectInt cardRect = UILayout.GetSourceRect(card.transform as RectTransform);
        RectInt childRect = UILayout.GetSourceRect(rect);
        RectInt sourceRect = GetVisibleContentRect(
            itemsScrollArea,
            OffsetRect(childRect, cardRect)
        );
        preview = UILayout.CreateDragPreview(texture, sourceRect, sourceX, sourceY);
        return preview != null;
    }

    internal StrategyStatusTarget GetStatusTarget(GalaxyMapPlanet planet)
    {
        if (lastData == null)
            return null;

        if (lastData.SelectedItems?.Count == 1)
        {
            int selected = lastData.SelectedItems.First();
            if (selected >= 0 && selected < lastData.SourceItems.Count)
                return new StrategyStatusTarget(planet, lastData.SourceItems[selected]);
        }

        ISceneNode item = GetItemAtIndex(contextItemIndex);
        return item == null ? null : new StrategyStatusTarget(planet, item);
    }

    StrategyStatusTarget IStrategyWindowStatusTargetView.GetStatusTarget(GalaxyMapPlanet planet)
    {
        return GetStatusTarget(planet);
    }

    public ISceneNode GetDestinationTargetItem()
    {
        if (lastData?.SelectedItems?.Count == 1)
            return GetItemAtIndex(lastData.SelectedItems.First());

        return GetItemAtIndex(contextItemIndex);
    }

    public void ClearSelection()
    {
        selectedItems.Clear();
        contextItemIndex = -1;
        dragItemIndex = -1;
    }

    private bool TrySelectTarget(int itemIndex)
    {
        if (uiRuntime?.Targeting.IsTargeting != true)
            return false;

        if (lastData?.GalaxyMapPlanet == null)
            return false;

        return uiRuntime.Targeting.TrySelectTarget(
            new StrategyMissionTarget(lastData.GalaxyMapPlanet, GetItemAtIndex(itemIndex))
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
        IReadOnlyList<ISceneNode> selectedItems,
        ISceneNode hitItem,
        bool canMove,
        bool playerControlsItem,
        bool canCreateMission,
        bool canRetire
    )
    {
        if (selectedItems == null || selectedItems.Count == 0)
        {
            return new List<StrategyMenuCommand>
            {
                new StrategyMenuCommand(
                    StrategyContextMenuActions.Encyclopedia,
                    "Encyclopedia",
                    false
                ),
                new StrategyMenuCommand(StrategyContextMenuActions.Status, "Status", false),
            };
        }

        if (selectedItems.All(item => item is Officer || item is SpecialForces))
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
                    StrategyContextMenuActions.CreateMission,
                    "Mission",
                    canCreateMission
                ),
                new StrategyMenuCommand(
                    StrategyContextMenuActions.Encyclopedia,
                    "Encyclopedia",
                    true
                ),
                new StrategyMenuCommand(StrategyContextMenuActions.Status, "Status", true),
                new StrategyMenuCommand(StrategyContextMenuActions.Retire, "Retire", canRetire),
            };
        }

        if (hitItem is Regiment || hitItem is Starfighter)
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
                    true
                ),
                new StrategyMenuCommand(StrategyContextMenuActions.Status, "Status", true),
                new StrategyMenuCommand(StrategyContextMenuActions.Scrap, "Scrap", canMove),
            };
        }

        return new List<StrategyMenuCommand>
        {
            new StrategyMenuCommand(StrategyContextMenuActions.Encyclopedia, "Encyclopedia", true),
            new StrategyMenuCommand(StrategyContextMenuActions.Status, "Status", true),
            new StrategyMenuCommand(StrategyContextMenuActions.Scrap, "Scrap", playerControlsItem),
        };
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

        if (itemsScrollArea != null)
        {
            itemsScrollArea.Dragged += DispatchScrollAreaDrag;
            itemsScrollArea.DragEnded += DispatchScrollAreaDragEnd;
        }

        scrollAreaDragEventsBound = true;
    }

    private void UnbindScrollAreaDragEvents()
    {
        if (!scrollAreaDragEventsBound)
            return;

        if (itemsScrollArea != null)
        {
            itemsScrollArea.Dragged -= DispatchScrollAreaDrag;
            itemsScrollArea.DragEnded -= DispatchScrollAreaDragEnd;
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

    private void InitializeState(int initialTab)
    {
        if (stateInitialized)
            return;

        activeTab = Mathf.Max(0, initialTab);
        stateInitialized = true;
    }

    private void SetActiveTab(int tab)
    {
        if (tab == activeTab)
            return;

        activeTab = tab;
        selectedItems.Clear();
        contextItemIndex = -1;
        dragItemIndex = -1;
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

    private static List<ISceneNode> GetPersonnel(Planet planet)
    {
        return planet
            .Officers.Cast<ISceneNode>()
            .Concat(planet.SpecialForces.Cast<ISceneNode>())
            .ToList();
    }

    private static List<ISceneNode> GetBuildings(Planet planet, bool shields)
    {
        return planet
            .Buildings.Where(building =>
                shields
                    ? building.DefenseFacilityClass
                        is DefenseFacilityClass.Shield
                            or DefenseFacilityClass.DeathStarShield
                    : building.DefenseFacilityClass
                        is DefenseFacilityClass.KDY
                            or DefenseFacilityClass.LNR
            )
            .Cast<ISceneNode>()
            .ToList();
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

    private void RenderTabs(DefenseWindowRenderData data)
    {
        for (int i = 0; i < tabImages.Length; i++)
        {
            bool active = i == data.ActiveTab;
            bool enabled = GetTabCount(data, i) > 0;
            UILayout.SetInteractiveImageTexture(
                tabImages[i],
                GetTabTexture(data.OwnerFactionId, i, enabled, active)
            );
        }
    }

    private void RenderItems(IReadOnlyList<StrategyUnitCardRenderData> items)
    {
        IReadOnlyList<StrategyUnitCardRenderData> safeItems =
            items ?? System.Array.Empty<StrategyUnitCardRenderData>();
        bool resetScroll = ItemsChanged(safeItems);
        itemsScrollArea.SetContentHeight(
            GetItemScrollContentHeight(safeItems.Count),
            GetItemScrollStep(),
            resetScroll
        );

        for (int i = 0; i < safeItems.Count; i++)
        {
            StrategyUnitCardRenderData item = safeItems[i];
            StrategyUnitCardView card = GetItemCard(i);
            card.SetIndex(i);
            card.Render(item);
        }

        LayoutRebuilder.ForceRebuildLayoutImmediate(itemsScrollArea.ContentRoot);

        for (int i = safeItems.Count; i < itemCards.Count; i++)
            itemCards[i].gameObject.SetActive(false);

        renderedAnyItems = true;
        renderedActiveTab = activeTab;
        renderedItemNames.Clear();
        for (int i = 0; i < safeItems.Count; i++)
            renderedItemNames.Add(safeItems[i].Name ?? string.Empty);
    }

    private int GetButtonAction(int index)
    {
        return index >= 0 && index < buttonActions.Length ? buttonActions[index] : 0;
    }

    private int GetTabCount(DefenseWindowRenderData data, int index)
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

    private Texture2D GetTabTexture(string ownerFactionId, int tab, bool enabled, bool active)
    {
        return tab switch
        {
            0 => GetThemedTabTexture(
                GetDefenseTheme(ownerFactionId)?.PersonnelTab,
                enabled,
                active
            ),
            1 => GetThemedTabTexture(GetDefenseTheme(ownerFactionId)?.TroopTab, enabled, active),
            2 => GetThemedTabTexture(GetDefenseTheme(ownerFactionId)?.FighterTab, enabled, active),
            3 => GetStateTexture(
                enabled,
                active,
                shieldAvailableTabTexture,
                shieldActiveTabTexture,
                shieldDisabledTabTexture
            ),
            4 => GetStateTexture(
                enabled,
                active,
                batteryAvailableTabTexture,
                batteryActiveTabTexture,
                batteryDisabledTabTexture
            ),
            _ => null,
        };
    }

    private Texture2D GetThemedTabTexture(WindowTabImageTheme theme, bool enabled, bool active)
    {
        return uiContext?.GetTexture(theme?.GetImagePath(enabled, active));
    }

    private static Texture2D GetStateTexture(
        bool enabled,
        bool active,
        Texture2D availableTexture,
        Texture2D activeTexture,
        Texture2D disabledTexture
    )
    {
        if (active)
            return activeTexture;

        return enabled ? availableTexture : disabledTexture;
    }

    private Texture2D GetItemBackgroundTexture(ISceneNode item)
    {
        if (IsItemInTransit(item))
            return GetPersonnelEnrouteBackgroundTexture(item) ?? enrouteBackgroundTexture;

        if (item is Officer || item is SpecialForces)
            return personnelBackgroundTexture;

        return null;
    }

    private Texture2D GetItemConstructionOverlayTexture(ISceneNode item)
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

    private Texture2D GetItemEnrouteOverlayTexture(ISceneNode item)
    {
        if (!IsItemInTransit(item) || item is Regiment or Officer or SpecialForces)
            return null;

        if (item is IManufacturable { ManufacturingStatus: ManufacturingStatus.Building })
            return null;

        string path = SelectStatusPath(item.InTransitSmallImagePath, item.InTransitImagePath);
        return string.IsNullOrEmpty(path) ? null : uiContext?.GetTexture(path);
    }

    private Texture2D GetPersonnelEnrouteBackgroundTexture(ISceneNode item)
    {
        if (item is not Officer and not SpecialForces)
            return null;

        string path = SelectStatusPath(item.InTransitSmallImagePath, item.InTransitImagePath);
        return string.IsNullOrEmpty(path) ? null : uiContext?.GetTexture(path);
    }

    private Texture2D GetItemDamagedOverlayTexture(ISceneNode item)
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

    private static bool IsItemInTransit(ISceneNode item)
    {
        return item is IMovable { Movement: not null };
    }

    private static bool CanDragItem(ISceneNode item)
    {
        return item is IMovable movable && item is not Building && movable.IsMovable();
    }

    private bool CanDragSelectedItems()
    {
        if (selectedItems.Count == 0 || lastData?.SourceItems == null)
            return false;

        foreach (int selected in selectedItems)
        {
            if (selected < 0 || selected >= lastData.SourceItems.Count)
                return false;

            if (!CanDragItem(lastData.SourceItems[selected]))
                return false;
        }

        return true;
    }

    private static string SelectStatusPath(string preferredPath, string fallbackPath)
    {
        return !string.IsNullOrEmpty(preferredPath) ? preferredPath : fallbackPath;
    }

    private Texture2D GetSelectionTexture(string ownerFactionId)
    {
        return uiContext?.GetTexture(GetDefenseTheme(ownerFactionId)?.SelectionImagePath);
    }

    private DefenseWindowTheme GetDefenseTheme(string ownerFactionId)
    {
        return uiContext?.GetTheme(ownerFactionId)?.StrategyWindows?.Defense;
    }

    private Color32 GetFactionColor(string ownerFactionId)
    {
        FactionTheme theme = uiContext?.GetTheme(ownerFactionId);
        return theme?.GetPrimaryColor() ?? Color.white;
    }

    private StrategyUnitCardView GetItemCard(int index)
    {
        while (itemCards.Count <= index)
            itemCards.Add(CreateItemCard(itemCards.Count));

        return itemCards[index];
    }

    private StrategyUnitCardView CreateItemCard(int index)
    {
        StrategyUnitCardView card = Instantiate(itemCardTemplate, itemsScrollArea.ContentRoot);
        card.name = $"ItemCard{index}";
        card.gameObject.SetActive(false);
        card.Pressed += HandleItemCardPressed;
        card.Released += HandleItemCardReleased;
        card.Dropped += HandleItemCardReleased;
        return card;
    }

    private void HandleItemCardPressed(StrategyUnitCardView card, PointerEventData eventData)
    {
        if (card == null || lastData == null)
            return;

        int itemIndex = card.Index;
        if (itemIndex < 0 || itemIndex >= lastData.SourceItems.Count)
            return;

        if (
            uiRuntime?.Targeting.IsTargeting == true
            && eventData.button == PointerEventData.InputButton.Left
        )
            return;

        contextItemIndex = itemIndex;
        if (eventData.button == PointerEventData.InputButton.Right)
        {
            SelectContextItem(selectedItems, itemIndex);
            uiContext?.Dispatcher.Send(new StrategyUIRequests.RequestRender());
            return;
        }

        if (eventData.button != PointerEventData.InputButton.Left)
            return;

        dragItemIndex = itemIndex;
        SelectIndexedItemForDrag(
            selectedItems,
            itemIndex,
            GetTabItems(lastData.Planet, activeTab).Count,
            GetItemColumnCount()
        );

        if (CanDragSelectedItems() && TryGetDesktopPosition(eventData, out int x, out int y))
            uiContext?.Dispatcher.Send(
                new StrategyUIRequests.StartWindowItemDrag(GetWindowId(), x, y)
            );
        else
            uiContext?.Dispatcher.Send(new StrategyUIRequests.RequestRender());
    }

    private void HandleItemCardReleased(StrategyUnitCardView card, PointerEventData eventData)
    {
        if (card != null)
            TrySelectTarget(card.Index);
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

    private ISceneNode GetItemAtIndex(int itemIndex)
    {
        if (
            lastData?.SourceItems == null
            || itemIndex < 0
            || itemIndex >= lastData.SourceItems.Count
        )
            return null;

        return lastData.SourceItems[itemIndex];
    }

    private int GetItemColumnCount()
    {
        return itemsGridLayout == null ? 1 : Mathf.Max(1, itemsGridLayout.constraintCount);
    }

    private int GetItemCellHeight()
    {
        return itemsGridLayout == null
            ? UILayout.GetSourceRect(itemCardTemplate.transform as RectTransform).height
            : Mathf.RoundToInt(itemsGridLayout.cellSize.y);
    }

    private int GetItemScrollStep()
    {
        return GetItemCellHeight();
    }

    private RectInt GetVisibleContentRect(ScrollAreaView scrollArea, RectInt contentRect)
    {
        RectInt scrollAreaRect = UILayout.GetSourceRect(scrollArea.transform as RectTransform);
        RectInt viewportRect = UILayout.GetSourceRect(scrollArea.ViewportRoot);
        int scrollY = Mathf.RoundToInt(scrollArea.ContentRoot.anchoredPosition.y);
        return new RectInt(
            lastData.X + scrollAreaRect.x + viewportRect.x + contentRect.x,
            lastData.Y + scrollAreaRect.y + viewportRect.y + contentRect.y - scrollY,
            contentRect.width,
            contentRect.height
        );
    }

    private bool ItemsChanged(IReadOnlyList<StrategyUnitCardRenderData> items)
    {
        if (
            !renderedAnyItems
            || renderedActiveTab != activeTab
            || renderedItemNames.Count != items.Count
        )
            return true;

        for (int i = 0; i < items.Count; i++)
        {
            if (renderedItemNames[i] != (items[i].Name ?? string.Empty))
                return true;
        }

        return false;
    }

    private static RectInt OffsetRect(RectInt rect, RectInt offset)
    {
        return new RectInt(offset.x + rect.x, offset.y + rect.y, rect.width, rect.height);
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
        if (tabTitleTextField == null)
            throw new MissingReferenceException($"{name}/TabTitleTextField is missing.");
        if (itemsScrollArea == null)
            throw new MissingReferenceException($"{name}/ItemsScrollArea is missing.");
        if (itemsGridLayout == null)
            throw new MissingReferenceException($"{name}/ItemsGridLayout is missing.");
        if (itemCardTemplate == null)
            throw new MissingReferenceException($"{name}/ItemCardTemplate is missing.");
        if (personnelBackgroundTexture == null)
            throw new MissingReferenceException($"{name}/PersonnelBackgroundTexture is missing.");
        if (enrouteBackgroundTexture == null)
            throw new MissingReferenceException($"{name}/EnrouteBackgroundTexture is missing.");
        itemCardTemplate.gameObject.SetActive(false);
    }

    private void VerifyTabTextures()
    {
        VerifyStateTextures(
            "Shield",
            shieldAvailableTabTexture,
            shieldActiveTabTexture,
            shieldDisabledTabTexture
        );
        VerifyStateTextures(
            "Battery",
            batteryAvailableTabTexture,
            batteryActiveTabTexture,
            batteryDisabledTabTexture
        );
    }

    private void VerifyStateTextures(
        string label,
        Texture2D availableTexture,
        Texture2D activeTexture,
        Texture2D disabledTexture
    )
    {
        if (availableTexture == null)
            throw new MissingReferenceException($"{name}/{label}AvailableTabTexture is missing.");
        if (activeTexture == null)
            throw new MissingReferenceException($"{name}/{label}ActiveTabTexture is missing.");
        if (disabledTexture == null)
            throw new MissingReferenceException($"{name}/{label}DisabledTabTexture is missing.");
    }

    private static void SetImage(RawImage image, Texture texture)
    {
        image.texture = texture;
        image.enabled = texture != null;
        image.gameObject.SetActive(texture != null);
        image.raycastTarget = false;
    }

    private static void SetImageFromTemplate(RawImage image, Texture texture)
    {
        UILayout.SetImageTexture(image, texture);
    }

    private static void SetTemplateText(TextMeshProUGUI textField, string text)
    {
        UILayout.SetTextContent(textField, text, textField.color);
    }
}

public sealed class DefenseWindowRenderData
{
    public int X;
    public int Y;
    internal GalaxyMapPlanet GalaxyMapPlanet;
    public Planet Planet;
    public string OwnerFactionId;
    public bool Active;
    public int ActiveTab;
    public string Caption;
    public string TabTitle;
    public IReadOnlyCollection<int> SelectedItems;
    public List<int> TabCounts = new List<int>();
    public List<ISceneNode> SourceItems = new List<ISceneNode>();
    public List<StrategyUnitCardRenderData> Items = new List<StrategyUnitCardRenderData>();
}
