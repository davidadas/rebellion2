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

public sealed class PlanetSystemWindowView
    : MonoBehaviour,
        IStrategyUIRuntimeReceiver,
        IStrategyWindowSelectionView,
        IStrategyWindowStatusTargetView,
        IStrategyWindowDragImageView,
        IStrategyWindowContextItemsView,
        IStrategyWindowContent,
        IGalaxyMapSectorWindowView
{
    private static readonly Color32 SectorTitle = new Color32(231, 243, 83, 255);
    private readonly List<PlanetSystemPlanetView> planetViews = new List<PlanetSystemPlanetView>();

    [SerializeField]
    private RawImage dimPanelImage;

    [SerializeField]
    private RawImage borderTopImage;

    [SerializeField]
    private RawImage borderBottomImage;

    [SerializeField]
    private RawImage borderLeftImage;

    [SerializeField]
    private RawImage borderRightImage;

    [SerializeField]
    private TextMeshProUGUI systemNameTextField;

    [SerializeField]
    private RawImage swapButtonImage;

    [SerializeField]
    private Texture2D swapButtonUpTexture;

    [SerializeField]
    private Texture2D swapButtonDownTexture;

    [SerializeField]
    private RawImage closeButtonImage;

    [SerializeField]
    private Texture2D closeButtonUpTexture;

    [SerializeField]
    private Texture2D closeButtonDownTexture;

    [SerializeField]
    private RectTransform planetsRoot;

    [SerializeField]
    private PlanetSystemPlanetView planetPrefab;

    [SerializeField]
    private float sectorCoordinateRange;

    [SerializeField]
    private float sectorCoordinateScaleX;

    [SerializeField]
    private float sectorCoordinateScaleY;

    private PlanetSystemWindowRenderData lastData;
    private StrategyUIRuntime uiRuntime;
    private UIContext uiContext;
    private string hoveredPlanetInstanceId;
    private PlanetIcon hoveredIcon;
    private string selectedPlanetInstanceId;
    private PlanetIcon selectedIcon;
    private string contextPlanetInstanceId;
    private PlanetIcon contextIcon;
    private bool contextPlanetImage;

    public GalaxyMapSector Sector { get; private set; }
    public int SectorPosition { get; private set; }

    public void InitializeWindow(GalaxyMapSector sector, int sectorPosition)
    {
        Sector = sector;
        SectorPosition = sectorPosition;
    }

    public void ReconcileSector(GalaxyMapSector sector)
    {
        InitializeWindow(sector, SectorPosition);
    }

    public void RefreshWindow(StrategyWindowRenderContext context, UIWindow window, bool active)
    {
        if (window == null)
            return;

        Render(
            new PlanetSystemWindowRenderData
            {
                X = window.X,
                Y = window.Y,
                System = Sector?.System,
                GalaxyMapPlanets =
                    Sector?.Planets
                    ?? (IReadOnlyList<GalaxyMapPlanet>)Array.Empty<GalaxyMapPlanet>(),
            }
        );
    }

    public void Initialize(StrategyUIRuntime uiRuntime)
    {
        if (uiRuntime == null)
            throw new ArgumentNullException(nameof(uiRuntime));

        this.uiRuntime = uiRuntime;
        uiContext = uiRuntime.Context;
        foreach (PlanetSystemPlanetView planetView in planetViews)
            planetView.Initialize(uiContext);
    }

    public void Render(PlanetSystemWindowRenderData data)
    {
        VerifyReferences();
        data = CreateRenderData(data);
        lastData = data;

        RectTransform rect = transform as RectTransform;
        UILayout.SetSourcePosition(rect, data.X, data.Y);
        int width = Mathf.RoundToInt(rect.sizeDelta.x);
        int height = Mathf.RoundToInt(rect.sizeDelta.y);

        SetStretch(dimPanelImage.rectTransform);

        SetBorder(borderTopImage.rectTransform, 0, 0, width, 1);
        SetBorder(borderBottomImage.rectTransform, 0, height - 1, width, 1);
        SetBorder(borderLeftImage.rectTransform, 0, 0, 1, height);
        SetBorder(borderRightImage.rectTransform, width - 1, 0, 1, height);

        UILayout.SetTextContent(systemNameTextField, data.Title, data.TitleColor);

        SetButtonAtTemplateOrigin(swapButtonImage, swapButtonUpTexture);
        SetButtonAtTemplateOrigin(closeButtonImage, closeButtonUpTexture);

        planetsRoot.gameObject.SetActive(true);
        SetStretch(planetsRoot);
        for (int i = 0; i < data.Planets.Count; i++)
        {
            PlanetSystemPlanetView view = GetPlanetView(i);
            view.Render(
                data.Planets[i],
                CreatePlanetPosition(data, data.Planets[i], width, height)
            );
        }

        for (int i = data.Planets.Count; i < planetViews.Count; i++)
            planetViews[i].gameObject.SetActive(false);

        ApplyHover();
        gameObject.SetActive(true);
    }

    private PlanetSystemWindowRenderData CreateRenderData(PlanetSystemWindowRenderData state)
    {
        PlanetSystem system = state.System;
        List<PlanetSystemWindowPlanetRenderData> planets =
            new List<PlanetSystemWindowPlanetRenderData>();
        IReadOnlyList<GalaxyMapPlanet> strategyPlanets =
            state.GalaxyMapPlanets ?? Array.Empty<GalaxyMapPlanet>();
        foreach (GalaxyMapPlanet planet in strategyPlanets)
            planets.Add(CreatePlanetData(planet));

        return new PlanetSystemWindowRenderData
        {
            X = state.X,
            Y = state.Y,
            System = system,
            GalaxyMapPlanets = strategyPlanets,
            SystemPositionX = system?.PositionX ?? state.SystemPositionX,
            SystemPositionY = system?.PositionY ?? state.SystemPositionY,
            Title = system?.GetDisplayName() ?? state.Title ?? string.Empty,
            TitleColor = SectorTitle,
            Planets = planets,
        };
    }

    private PlanetSystemWindowPlanetRenderData CreatePlanetData(GalaxyMapPlanet strategyPlanet)
    {
        Planet planet = strategyPlanet.Planet;
        return new PlanetSystemWindowPlanetRenderData
        {
            GalaxyMapPlanet = strategyPlanet,
            Planet = planet,
            PlanetTexture = uiContext?.GetPlanetTexture(planet, strategyPlanet.PlanetIconPath),
            PopularSupport = GetPlayerSupport(planet),
            IsUnexplored = planet.IsUnexploredView,
            SelectedIcon = string.Equals(
                selectedPlanetInstanceId,
                planet.InstanceID,
                StringComparison.Ordinal
            )
                ? selectedIcon
                : PlanetIcon.None,
        };
    }

    private int GetPlayerSupport(Planet planet)
    {
        string playerFactionId = uiContext?.GetPlayerFactionInstanceID();
        return !string.IsNullOrEmpty(playerFactionId)
            ? planet.GetPopularSupport(playerFactionId)
            : 50;
    }

    public bool ClearHover()
    {
        return SetHoveredHit(null, PlanetIcon.None);
    }

    internal StrategyStatusTarget GetStatusTarget()
    {
        PlanetSystemWindowHit hit = GetContextHit() ?? GetSelectedHit();
        if (hit?.Icon == PlanetIcon.Fleet)
        {
            List<ISceneNode> fleetItems = GetPlayerFleetItems(hit.Planet);
            return hit.GalaxyMapPlanet != null && fleetItems.Count == 1
                ? new StrategyStatusTarget(hit.GalaxyMapPlanet, fleetItems[0])
                : null;
        }

        return hit?.GalaxyMapPlanet != null
            ? new StrategyStatusTarget(hit.GalaxyMapPlanet, hit.GalaxyMapPlanet.Planet)
            : null;
    }

    StrategyStatusTarget IStrategyWindowStatusTargetView.GetStatusTarget(GalaxyMapPlanet planet)
    {
        return GetStatusTarget();
    }

    internal List<StrategyMenuCommand> BuildContextMenu()
    {
        PlanetSystemWindowHit hit = GetContextHit();
        if (hit?.GalaxyMapPlanet == null)
            return BuildPlanetInformationContextMenu(false);

        if (hit.PlanetImage || hit.Icon == PlanetIcon.None)
            return BuildPlanetInformationContextMenu(true);

        return hit.Icon switch
        {
            PlanetIcon.Facility => BuildPlanetInformationContextMenu(true),
            PlanetIcon.Defense => BuildPlanetInformationContextMenu(true),
            PlanetIcon.Fleet => BuildFleetContextMenu(hit.Planet),
            PlanetIcon.Mission => new List<StrategyMenuCommand>
            {
                new StrategyMenuCommand(
                    StrategyContextMenuActions.Encyclopedia,
                    "Encyclopedia",
                    false
                ),
                new StrategyMenuCommand(StrategyContextMenuActions.Status, "Status", false),
                new StrategyMenuCommand(StrategyContextMenuActions.Abort, "Abort", false),
            },
            _ => BuildPlanetInformationContextMenu(false),
        };
    }

    private static List<StrategyMenuCommand> BuildPlanetInformationContextMenu(bool enabled)
    {
        return new List<StrategyMenuCommand>
        {
            new StrategyMenuCommand(
                StrategyContextMenuActions.Encyclopedia,
                "Encyclopedia",
                enabled
            ),
            new StrategyMenuCommand(StrategyContextMenuActions.Status, "Status", enabled),
        };
    }

    internal void CaptureContextTarget(PointerEventData eventData)
    {
        PlanetSystemPlanetView view = GetRaycastTarget(eventData)
            ?.GetComponentInParent<PlanetSystemPlanetView>();
        if (
            view != null
            && planetViews.Contains(view)
            && view.TryCreateHit(eventData, out PlanetSystemWindowHit hit)
        )
        {
            StoreContextHit(hit);
            SelectHit(hit);
            return;
        }

        ClearContextHit();
    }

    public void Hide()
    {
        gameObject.SetActive(false);
    }

    public void ClearSelection()
    {
        selectedPlanetInstanceId = null;
        selectedIcon = PlanetIcon.None;
        ClearContextHit();
    }

    private void Awake()
    {
        VerifyReferences();
    }

    private void HandlePlanetHovered(
        PlanetSystemPlanetView view,
        PlanetSystemWindowHit hit,
        PointerEventData eventData
    )
    {
        if (hit == null)
            ClearHover();
        else if (SetHoveredHit(hit.Planet, hit.Icon))
            uiContext?.Dispatcher.Send(new StrategyUIRequests.RequestRender());
    }

    private void HandlePlanetHoverCleared(PlanetSystemPlanetView view)
    {
        if (ClearHover())
            uiContext?.Dispatcher.Send(new StrategyUIRequests.RequestRender());
    }

    private void HandlePlanetPressed(
        PlanetSystemPlanetView view,
        PlanetSystemWindowHit hit,
        PointerEventData eventData
    )
    {
        if (hit == null)
            return;

        if (
            uiRuntime?.Targeting.IsTargeting == true
            && eventData.button == PointerEventData.InputButton.Left
        )
            return;

        StoreContextHit(hit);
        bool selected = SelectHit(hit);
        if (
            selected
            && hit.Icon == PlanetIcon.Fleet
            && eventData.button == PointerEventData.InputButton.Left
            && TryGetDesktopPosition(eventData, out int x, out int y)
            && GetComponent<UIWindow>() is UIWindow window
        )
        {
            uiContext?.Dispatcher.Send(new StrategyUIRequests.StartWindowItemDrag(window.Id, x, y));
        }
        else
        {
            uiContext?.Dispatcher.Send(new StrategyUIRequests.RequestRender());
        }
    }

    private void HandlePlanetReleased(
        PlanetSystemPlanetView view,
        PlanetSystemWindowHit hit,
        PointerEventData eventData
    )
    {
        if (hit != null)
            TrySelectTarget(hit);
    }

    private void HandlePlanetClicked(
        PlanetSystemPlanetView view,
        PlanetSystemWindowHit hit,
        PointerEventData eventData
    )
    {
        if (uiRuntime?.Targeting.IsTargeting == true)
            return;

        if (hit?.Icon == PlanetIcon.None || uiContext == null)
            return;

        if (!TryGetDesktopPosition(eventData, out int x, out int y))
            return;

        uiContext.Dispatcher.Send(
            new StrategyUIRequests.OpenPlanetWindow(hit.Planet, hit.Icon, x, y)
        );
    }

    private bool TryGetDesktopPosition(PointerEventData eventData, out int x, out int y)
    {
        x = 0;
        y = 0;
        if (eventData == null)
            return false;

        UIWindow window = GetComponent<UIWindow>();
        return window != null
            && window.TryGetDesktopPosition(eventData, eventData.position, out x, out y);
    }

    private bool SetHoveredHit(Planet planet, PlanetIcon icon)
    {
        string planetInstanceId = planet?.InstanceID;
        if (
            string.Equals(hoveredPlanetInstanceId, planetInstanceId, StringComparison.Ordinal)
            && hoveredIcon == icon
        )
            return false;

        hoveredPlanetInstanceId = planetInstanceId;
        hoveredIcon = icon;
        ApplyHover();
        return true;
    }

    private void ApplyHover()
    {
        for (int i = 0; i < planetViews.Count; i++)
        {
            PlanetSystemPlanetView view = planetViews[i];
            PlanetIcon icon = string.Equals(
                view.Planet?.InstanceID,
                hoveredPlanetInstanceId,
                StringComparison.Ordinal
            )
                ? hoveredIcon
                : PlanetIcon.None;
            view.SetHoveredIcon(icon);
        }
    }

    private bool SelectHit(PlanetSystemWindowHit hit)
    {
        if (hit?.Planet == null || hit.Icon == PlanetIcon.None)
            return false;

        selectedPlanetInstanceId = hit.Planet.InstanceID;
        selectedIcon = hit.Icon;
        return true;
    }

    private void StoreContextHit(PlanetSystemWindowHit hit)
    {
        contextPlanetInstanceId = hit?.Planet?.InstanceID;
        contextIcon = hit?.Icon ?? PlanetIcon.None;
        contextPlanetImage = hit?.PlanetImage == true;
    }

    private void ClearContextHit()
    {
        contextPlanetInstanceId = null;
        contextIcon = PlanetIcon.None;
        contextPlanetImage = false;
    }

    private PlanetSystemWindowHit GetContextHit()
    {
        return GetStoredHit(contextPlanetInstanceId, contextIcon, contextPlanetImage);
    }

    private PlanetSystemWindowHit GetSelectedHit()
    {
        return GetStoredHit(selectedPlanetInstanceId, selectedIcon, false);
    }

    private PlanetSystemWindowHit GetStoredHit(
        string planetInstanceId,
        PlanetIcon icon,
        bool planetImage
    )
    {
        if (lastData?.Planets == null || string.IsNullOrEmpty(planetInstanceId))
            return null;

        PlanetSystemWindowPlanetRenderData data = lastData.Planets.FirstOrDefault(item =>
            string.Equals(item.Planet?.InstanceID, planetInstanceId, StringComparison.Ordinal)
        );
        return data == null
            ? null
            : new PlanetSystemWindowHit(data.GalaxyMapPlanet, data.Planet, icon, planetImage);
    }

    private bool TrySelectTarget(PlanetSystemWindowHit hit)
    {
        if (uiRuntime?.Targeting.IsTargeting != true)
            return false;

        StrategyMissionTarget target = CreateTargetForHit(
            hit,
            uiRuntime.Targeting.ActiveRequest,
            GetPlayerFleetTarget(hit?.Planet)
        );
        if (target == null)
            return false;

        return uiRuntime.Targeting.TrySelectTarget(target);
    }

    internal static StrategyMissionTarget CreateTargetForHit(
        PlanetSystemWindowHit hit,
        TargetingRequest request,
        ISceneNode fleetTarget
    )
    {
        if (hit?.GalaxyMapPlanet == null)
            return null;

        if (hit.Icon == PlanetIcon.Fleet && IsMoveTargetingRequest(request))
            return new StrategyMissionTarget(hit.GalaxyMapPlanet, fleetTarget);

        if (hit.Icon != PlanetIcon.None || hit.PlanetImage)
            return new StrategyMissionTarget(hit.GalaxyMapPlanet, null);

        return null;
    }

    private static bool IsMoveTargetingRequest(TargetingRequest request)
    {
        if (request?.Source is not StrategyWindowTargetingSource source)
            return false;

        return source.Action
            is StrategyContextMenuActions.Move
                or StrategyContextMenuActions.MoveConfirm;
    }

    private static GameObject GetRaycastTarget(PointerEventData eventData)
    {
        if (eventData == null)
            return null;

        return eventData.pointerCurrentRaycast.gameObject
            ?? eventData.pointerPressRaycast.gameObject;
    }

    public List<ISceneNode> GetContextItems()
    {
        PlanetSystemWindowHit hit = GetContextHit() ?? GetSelectedHit();
        if (hit?.Icon != PlanetIcon.Fleet || hit.Planet?.Fleets == null)
            return new List<ISceneNode>();

        return GetPlayerFleetItems(hit.Planet);
    }

    private Fleet GetPlayerFleetTarget(Planet planet)
    {
        string playerFactionId = uiContext?.GetPlayerFactionInstanceID();
        return planet?.Fleets.FirstOrDefault(fleet =>
            StrategyContextMenuAvailability.PlayerControlsItem(fleet, playerFactionId)
        );
    }

    public bool TryGetDragPreview(int sourceX, int sourceY, out DragPreview preview)
    {
        preview = null;
        PlanetSystemWindowHit hit = GetContextHit() ?? GetSelectedHit();
        if (
            hit?.Icon != PlanetIcon.Fleet
            || hit.GalaxyMapPlanet == null
            || GetContextItems().Count == 0
        )
            return false;

        PlanetSystemPlanetView view = GetPlanetView(hit.Planet);
        if (view == null || !view.TryGetFleetDragImage(out Texture texture, out RectTransform rect))
            return false;

        RectInt planetRect = UILayout.GetSourceRect(view.transform as RectTransform);
        RectInt iconRect = UILayout.GetSourceRect(rect);
        RectInt sourceRect = new RectInt(
            lastData.X + planetRect.x + iconRect.x,
            lastData.Y + planetRect.y + iconRect.y,
            iconRect.width,
            iconRect.height
        );
        preview = UILayout.CreateDragPreview(texture, sourceRect, sourceX, sourceY);
        return preview != null;
    }

    private List<StrategyMenuCommand> BuildFleetContextMenu(Planet planet)
    {
        List<ISceneNode> fleetItems = GetPlayerFleetItems(planet);
        string playerFactionId = uiContext?.GetPlayerFactionInstanceID();
        bool canCommandFleets = StrategyContextMenuAvailability.CanMoveItems(
            fleetItems,
            playerFactionId
        );
        bool canShowSingleFleetInfo = fleetItems.Count == 1;
        return new List<StrategyMenuCommand>
        {
            new StrategyMenuCommand(StrategyContextMenuActions.Move, "Move", canCommandFleets),
            new StrategyMenuCommand(
                StrategyContextMenuActions.MoveConfirm,
                "Confirmed Move",
                canCommandFleets
            ),
            new StrategyMenuCommand(
                StrategyContextMenuActions.Submenu,
                "Planetary Bombardment",
                false
            ),
            new StrategyMenuCommand(
                StrategyContextMenuActions.DestroySystem,
                "Destroy System",
                false
            ),
            new StrategyMenuCommand(StrategyContextMenuActions.Encyclopedia, "Encyclopedia", false),
            new StrategyMenuCommand(
                StrategyContextMenuActions.Status,
                "Status",
                canShowSingleFleetInfo
            ),
            new StrategyMenuCommand(StrategyContextMenuActions.Scrap, "Scrap", canCommandFleets),
        };
    }

    private List<ISceneNode> GetPlayerFleetItems(Planet planet)
    {
        string playerFactionId = uiContext?.GetPlayerFactionInstanceID();
        return planet
                ?.Fleets.Cast<ISceneNode>()
                .Where(fleet =>
                    StrategyContextMenuAvailability.PlayerControlsItem(fleet, playerFactionId)
                )
                .ToList()
            ?? new List<ISceneNode>();
    }

    private PlanetSystemPlanetView GetPlanetView(Planet planet)
    {
        string instanceId = planet?.InstanceID;
        if (string.IsNullOrEmpty(instanceId))
            return null;

        for (int i = 0; i < planetViews.Count; i++)
        {
            PlanetSystemPlanetView view = planetViews[i];
            if (string.Equals(view.Planet?.InstanceID, instanceId, StringComparison.Ordinal))
                return view;
        }

        return null;
    }

    private void VerifyReferences()
    {
        if (dimPanelImage == null)
            throw new MissingReferenceException($"{name}/DimPanelImage is missing.");
        if (borderTopImage == null)
            throw new MissingReferenceException($"{name}/BorderTopImage is missing.");
        if (borderBottomImage == null)
            throw new MissingReferenceException($"{name}/BorderBottomImage is missing.");
        if (borderLeftImage == null)
            throw new MissingReferenceException($"{name}/BorderLeftImage is missing.");
        if (borderRightImage == null)
            throw new MissingReferenceException($"{name}/BorderRightImage is missing.");
        if (systemNameTextField == null)
            throw new MissingReferenceException($"{name}/SystemNameTextField is missing.");
        if (swapButtonImage == null)
            throw new MissingReferenceException($"{name}/SwapButtonImage is missing.");
        if (swapButtonUpTexture == null)
            throw new MissingReferenceException($"{name}/SwapButtonUpTexture is missing.");
        if (swapButtonDownTexture == null)
            throw new MissingReferenceException($"{name}/SwapButtonDownTexture is missing.");
        if (closeButtonImage == null)
            throw new MissingReferenceException($"{name}/CloseButtonImage is missing.");
        if (closeButtonUpTexture == null)
            throw new MissingReferenceException($"{name}/CloseButtonUpTexture is missing.");
        if (closeButtonDownTexture == null)
            throw new MissingReferenceException($"{name}/CloseButtonDownTexture is missing.");
        if (planetsRoot == null)
            throw new MissingReferenceException($"{name}/Planets is missing.");
        if (planetPrefab == null)
            throw new MissingReferenceException($"{name}/PlanetSystemPlanet prefab is missing.");
    }

    private PlanetSystemPlanetView GetPlanetView(int index)
    {
        while (planetViews.Count <= index)
        {
            PlanetSystemPlanetView view = Instantiate(planetPrefab, planetsRoot);
            view.name = $"Planet{planetViews.Count}";
            view.Hovered += HandlePlanetHovered;
            view.HoverCleared += HandlePlanetHoverCleared;
            view.Pressed += HandlePlanetPressed;
            view.Released += HandlePlanetReleased;
            view.Clicked += HandlePlanetClicked;
            if (uiContext != null)
                view.Initialize(uiContext);
            planetViews.Add(view);
        }

        return planetViews[index];
    }

    private Vector2Int CreatePlanetPosition(
        PlanetSystemWindowRenderData data,
        PlanetSystemWindowPlanetRenderData planet,
        int width,
        int height
    )
    {
        int x = Mathf.FloorToInt(
            ((planet.Planet.PositionX - data.SystemPositionX) / sectorCoordinateRange)
                * sectorCoordinateScaleX
                * width
        );
        int y =
            Mathf.FloorToInt(
                ((planet.Planet.PositionY - data.SystemPositionY) / sectorCoordinateRange)
                    * sectorCoordinateScaleY
                    * height
            ) - 1;

        return new Vector2Int(x, y);
    }

    private static void SetButtonAtTemplateOrigin(RawImage image, Texture2D texture)
    {
        image.texture = texture;
        image.enabled = texture != null;
        image.raycastTarget = texture != null;
        image.gameObject.SetActive(texture != null);
    }

    private static void SetStretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        rect.localScale = Vector3.one;
    }

    private static void SetBorder(RectTransform rect, int x, int y, int width, int height)
    {
        SetSourceRect(rect, x, y, width, height);
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

public sealed class PlanetSystemWindowHit
{
    public PlanetSystemWindowHit(
        GalaxyMapPlanet strategyPlanet,
        Planet planet,
        PlanetIcon icon,
        bool planetImage
    )
    {
        GalaxyMapPlanet = strategyPlanet;
        Planet = planet;
        Icon = icon;
        PlanetImage = planetImage;
    }

    public GalaxyMapPlanet GalaxyMapPlanet { get; }
    public Planet Planet { get; }
    public PlanetIcon Icon { get; }
    public bool PlanetImage { get; }
}

public sealed class PlanetSystemWindowRenderData
{
    public int X;
    public int Y;
    public PlanetSystem System;
    public int SystemPositionX;
    public int SystemPositionY;
    public string Title;
    public Color32 TitleColor;
    internal IReadOnlyList<GalaxyMapPlanet> GalaxyMapPlanets;
    public IReadOnlyList<PlanetSystemWindowPlanetRenderData> Planets;
}
