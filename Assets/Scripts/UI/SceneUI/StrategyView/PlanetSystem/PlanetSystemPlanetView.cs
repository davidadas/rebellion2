using System;
using System.Collections.Generic;
using System.Linq;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Units;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public sealed class PlanetSystemPlanetView
    : MonoBehaviour,
        IPointerEnterHandler,
        IPointerExitHandler,
        IPointerMoveHandler,
        IPointerDownHandler,
        IPointerClickHandler,
        IDropHandler
{
    private static readonly Color32 White = new Color32(255, 255, 255, 255);
    private static readonly Color32 BarGray = new Color32(160, 160, 160, 255);
    private static readonly Color32 Orange = new Color32(236, 106, 46, 255);
    private static readonly Color32 Yellow = new Color32(255, 255, 84, 255);

    [SerializeField]
    private RawImage hitAreaImage;

    [SerializeField]
    private RawImage planetImage;

    [SerializeField]
    private RawImage facilityImage;

    [SerializeField]
    private RawImage defenseImage;

    [SerializeField]
    private RawImage fleetImage;

    [SerializeField]
    private RawImage missionImage;

    [SerializeField]
    private RawImage headquartersImage;

    [SerializeField]
    private TextMeshProUGUI planetNameTextField;

    [SerializeField]
    private RectTransform energyBarRoot;

    [SerializeField]
    private RectTransform rawBarRoot;

    [SerializeField]
    private RectTransform supportBarRoot;

    [SerializeField]
    private Image energyBarBackgroundImage;

    [SerializeField]
    private Image energyBarFillImage;

    [SerializeField]
    private Image[] energyBarCellImages;

    [SerializeField]
    private Image rawBarBackgroundImage;

    [SerializeField]
    private Image rawBarFillImage;

    [SerializeField]
    private Image[] rawBarCellImages;

    [SerializeField]
    private Image supportBarBackgroundImage;

    [SerializeField]
    private Image supportBarFillImage;

    private BarView energyBar;
    private BarView rawBar;
    private BarView supportBar;
    private UIContext uiContext;
    private PlanetSystemWindowPlanetRenderData lastData;
    private PlanetIcon hoveredIcon;

    internal event Action<PlanetSystemPlanetView, PlanetSystemWindowHit, PointerEventData> Hovered;
    internal event Action<PlanetSystemPlanetView> HoverCleared;
    internal event Action<PlanetSystemPlanetView, PlanetSystemWindowHit, PointerEventData> Pressed;
    internal event Action<PlanetSystemPlanetView, PlanetSystemWindowHit, PointerEventData> Released;
    internal event Action<PlanetSystemPlanetView, PlanetSystemWindowHit, PointerEventData> Clicked;

    public Planet Planet => lastData?.Planet;

    public void Initialize(UIContext uiContext)
    {
        if (uiContext == null)
            throw new ArgumentNullException(nameof(uiContext));

        this.uiContext = uiContext;
    }

    public void Render(PlanetSystemWindowPlanetRenderData data, Vector2Int position)
    {
        VerifyReferences();
        EnsureBars();
        lastData = data;

        Texture2D planetTexture = data.PlanetTexture;
        RectInt planetTemplate = GetSourceRect(planetImage.rectTransform);
        int planetWidth = planetTemplate.width;

        RectTransform rect = transform as RectTransform;
        RectInt rootTemplate = GetSourceRect(rect);
        SetSourceRect(
            rect,
            position.x - planetTemplate.x,
            position.y - planetTemplate.y,
            rootTemplate.width,
            rootTemplate.height
        );
        SetHitArea(rootTemplate.width, rootTemplate.height);

        SetImage(planetImage, planetTexture, false);
        RefreshIconImages();
        SetImage(
            headquartersImage,
            !data.IsUnexplored && data.Planet?.IsHeadquarters == true
                ? GetHeadquartersTexture(data.Planet.OwnerInstanceID)
                : null,
            false
        );

        energyBar.Render(
            data.IsUnexplored ? HiddenBar(planetWidth) : CreateEnergyBar(data.Planet, planetWidth)
        );
        rawBar.Render(
            data.IsUnexplored
                ? HiddenBar(planetWidth)
                : CreateRawResourceBar(data.Planet, planetWidth)
        );
        supportBar.Render(
            data.IsUnexplored
                ? HiddenBar(planetWidth)
                : CreateSupportBar(data.Planet, planetWidth, data.PopularSupport)
        );

        planetNameTextField.text = data.Planet.GetDisplayName();
        planetNameTextField.color = GetFactionColor(data.Planet.OwnerInstanceID);

        gameObject.SetActive(true);
    }

    internal bool SetHoveredIcon(PlanetIcon icon)
    {
        if (hoveredIcon == icon)
            return false;

        hoveredIcon = icon;
        RefreshIconImages();
        return true;
    }

    internal bool TryGetIconSourceRect(PlanetIcon icon, out RectTransform rect)
    {
        rect = default;
        if (!IsIconVisible(icon))
            return false;

        RawImage image = GetIconImage(icon);
        if (image == null)
            return false;

        rect = image.rectTransform;
        return true;
    }

    internal bool TryGetFleetDragImage(out Texture texture, out RectTransform rect)
    {
        texture = null;
        rect = default;
        if (!TryGetIconSourceRect(PlanetIcon.Fleet, out rect) || lastData == null)
            return false;

        texture = GetFleetTexture(lastData, true) ?? GetFleetTexture(lastData, false);
        return texture != null;
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        DispatchHover(eventData);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        HoverCleared?.Invoke(this);
    }

    public void OnPointerMove(PointerEventData eventData)
    {
        DispatchHover(eventData);
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        PlanetSystemWindowHit hit = CreateHit(null, eventData);
        if (hit == null)
            return;

        UIWindow window = GetComponentInParent<UIWindow>();
        if (eventData.button == PointerEventData.InputButton.Right)
        {
            Pressed?.Invoke(this, hit, eventData);
            window?.RequestContext(eventData);
        }
        else if (eventData.button == PointerEventData.InputButton.Left)
        {
            window?.RequestFocus();
            Pressed?.Invoke(this, hit, eventData);
        }
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (eventData.button != PointerEventData.InputButton.Left)
            return;

        PlanetSystemWindowHit hit = CreateHit(null, eventData);
        if (hit == null)
            return;

        if (eventData.clickCount > 1)
            Clicked?.Invoke(this, hit, eventData);

        Released?.Invoke(this, hit, eventData);
    }

    public void OnDrop(PointerEventData eventData)
    {
        PlanetSystemWindowHit hit = CreateHit(null, eventData, false);
        if (hit != null)
            Released?.Invoke(this, hit, eventData);
    }

    private void Awake()
    {
        VerifyReferences();
        EnsureBars();
    }

    private void VerifyReferences()
    {
        if (hitAreaImage == null)
            throw new MissingReferenceException($"{name}/HitAreaImage is missing.");
        if (planetImage == null)
            throw new MissingReferenceException($"{name}/PlanetImage is missing.");
        if (facilityImage == null)
            throw new MissingReferenceException($"{name}/FacilityImage is missing.");
        if (defenseImage == null)
            throw new MissingReferenceException($"{name}/DefenseImage is missing.");
        if (fleetImage == null)
            throw new MissingReferenceException($"{name}/FleetImage is missing.");
        if (missionImage == null)
            throw new MissingReferenceException($"{name}/MissionImage is missing.");
        if (headquartersImage == null)
            throw new MissingReferenceException($"{name}/HeadquartersImage is missing.");
        if (planetNameTextField == null)
            throw new MissingReferenceException($"{name}/PlanetNameTextField is missing.");
        if (energyBarRoot == null)
            throw new MissingReferenceException($"{name}/EnergyBar is missing.");
        if (rawBarRoot == null)
            throw new MissingReferenceException($"{name}/RawBar is missing.");
        if (supportBarRoot == null)
            throw new MissingReferenceException($"{name}/SupportBar is missing.");
        if (energyBarBackgroundImage == null)
            throw new MissingReferenceException($"{name}/EnergyBar BackgroundImage is missing.");
        if (energyBarFillImage == null)
            throw new MissingReferenceException($"{name}/EnergyBar FillImage is missing.");
        if (energyBarCellImages == null || energyBarCellImages.Length == 0)
            throw new MissingReferenceException($"{name}/EnergyBar cell images are missing.");
        if (rawBarBackgroundImage == null)
            throw new MissingReferenceException($"{name}/RawBar BackgroundImage is missing.");
        if (rawBarFillImage == null)
            throw new MissingReferenceException($"{name}/RawBar FillImage is missing.");
        if (rawBarCellImages == null || rawBarCellImages.Length == 0)
            throw new MissingReferenceException($"{name}/RawBar cell images are missing.");
        if (supportBarBackgroundImage == null)
            throw new MissingReferenceException($"{name}/SupportBar BackgroundImage is missing.");
        if (supportBarFillImage == null)
            throw new MissingReferenceException($"{name}/SupportBar FillImage is missing.");
    }

    private void EnsureBars()
    {
        energyBar ??= new BarView(
            energyBarRoot,
            energyBarBackgroundImage,
            energyBarFillImage,
            energyBarCellImages
        );
        rawBar ??= new BarView(
            rawBarRoot,
            rawBarBackgroundImage,
            rawBarFillImage,
            rawBarCellImages
        );
        supportBar ??= new BarView(
            supportBarRoot,
            supportBarBackgroundImage,
            supportBarFillImage,
            Array.Empty<Image>()
        );
    }

    private void RefreshIconImages()
    {
        if (lastData == null)
            return;
        SetImage(
            facilityImage,
            lastData.IsUnexplored
                ? null
                : GetFacilityTexture(lastData, IsIconPressed(PlanetIcon.Facility)),
            false
        );
        SetImage(
            defenseImage,
            lastData.IsUnexplored
                ? null
                : GetDefenseTexture(lastData, IsIconPressed(PlanetIcon.Defense)),
            false
        );
        SetImage(fleetImage, GetFleetTexture(lastData, IsIconPressed(PlanetIcon.Fleet)), false);
        SetImage(
            missionImage,
            GetMissionTexture(lastData, IsIconPressed(PlanetIcon.Mission)),
            false
        );
    }

    private bool IsIconPressed(PlanetIcon icon)
    {
        return lastData.SelectedIcon == icon;
    }

    private static void SetImage(RawImage image, Texture2D texture, bool resizeToTexture)
    {
        image.texture = texture;
        image.enabled = texture != null;
        image.gameObject.SetActive(texture != null);
        image.raycastTarget = texture != null;
        if (texture != null && resizeToTexture)
            image.rectTransform.sizeDelta = new Vector2(texture.width, texture.height);
    }

    private void SetHitArea(int width, int height)
    {
        hitAreaImage.color = Color.clear;
        hitAreaImage.raycastTarget = false;
        hitAreaImage.canvasRenderer.cullTransparentMesh = false;
        SetSourceRect(hitAreaImage.rectTransform, 0, 0, width, height);
        hitAreaImage.gameObject.SetActive(true);
    }

    private Texture2D GetFacilityTexture(PlanetSystemWindowPlanetRenderData data, bool pressed)
    {
        if (!HasFacilities(data.Planet))
            return null;

        return GetFactionOverlayTexture(data.Planet?.OwnerInstanceID, PlanetIcon.Facility, pressed);
    }

    private Texture2D GetDefenseTexture(PlanetSystemWindowPlanetRenderData data, bool pressed)
    {
        if (!HasDefenses(data.Planet))
            return null;

        return GetFactionOverlayTexture(data.Planet?.OwnerInstanceID, PlanetIcon.Defense, pressed);
    }

    private Texture2D GetFleetTexture(PlanetSystemWindowPlanetRenderData data, bool pressed)
    {
        return GetFactionOverlayTexture(
            SelectPresentFactionId(
                GetFleetOwnerFactionIds(data.Planet),
                data.Planet?.OwnerInstanceID
            ),
            PlanetIcon.Fleet,
            pressed
        );
    }

    private Texture2D GetMissionTexture(PlanetSystemWindowPlanetRenderData data, bool pressed)
    {
        return GetFactionOverlayTexture(
            SelectPresentFactionId(
                GetMissionOwnerFactionIds(data.Planet),
                data.Planet?.OwnerInstanceID
            ),
            PlanetIcon.Mission,
            pressed
        );
    }

    private Texture2D GetFactionOverlayTexture(string factionId, PlanetIcon icon, bool pressed)
    {
        if (uiContext == null)
            return null;

        OverlayIconTheme theme = GetOverlayIconTheme(uiContext.GetTheme(factionId), icon);
        string path = pressed ? theme?.HoverImagePath : theme?.NormalImagePath;
        return uiContext.GetTexture(path);
    }

    private Texture2D GetHeadquartersTexture(string factionId)
    {
        return uiContext?.GetTexture(
            uiContext.GetTheme(factionId)?.PlanetOverlayTheme?.PlanetSystemHeadquartersImagePath
        );
    }

    private static OverlayIconTheme GetOverlayIconTheme(FactionTheme theme, PlanetIcon icon)
    {
        PlanetOverlayIcons icons = theme?.PlanetOverlayTheme?.PlanetOverlayIcons;
        return icon switch
        {
            PlanetIcon.Facility => icons?.Buildings,
            PlanetIcon.Defense => icons?.Defenses,
            PlanetIcon.Fleet => icons?.Fleets,
            PlanetIcon.Mission => icons?.Missions,
            _ => null,
        };
    }

    private static PlanetIcon ToPlanetIcon(PlanetSystemWindowIcon icon)
    {
        return icon switch
        {
            PlanetSystemWindowIcon.Facility => PlanetIcon.Facility,
            PlanetSystemWindowIcon.Defense => PlanetIcon.Defense,
            _ => PlanetIcon.None,
        };
    }

    private static BarData CreateEnergyBar(Planet planet, int width)
    {
        if (planet.EnergyCapacity <= 0)
            return new BarData(
                true,
                width,
                0,
                0,
                width,
                new Color32(0, 0, 255, 255),
                Color.clear,
                Color.clear
            );

        return new BarData(
            true,
            width,
            planet.EnergyCapacity,
            Mathf.Min(planet.Buildings.Count, planet.EnergyCapacity),
            0,
            White,
            new Color32(64, 132, 255, 255),
            BarGray
        );
    }

    private static BarData HiddenBar(int width)
    {
        return new BarData(false, width, 0, 0, 0, Color.clear, Color.clear, Color.clear);
    }

    private static BarData CreateRawResourceBar(Planet planet, int width)
    {
        if (planet.NumRawResourceNodes <= 0)
            return new BarData(true, width, 0, 0, width, Orange, Color.clear, Color.clear);

        int mines = planet.GetRawMinedResources();

        return new BarData(
            true,
            width,
            planet.NumRawResourceNodes,
            Mathf.Min(mines, planet.NumRawResourceNodes),
            0,
            Yellow,
            Orange,
            BarGray
        );
    }

    private BarData CreateSupportBar(Planet planet, int width, int support)
    {
        if (!planet.IsPopulated())
            return new BarData(false, width, 0, 0, 0, Color.clear, Color.clear, Color.clear);

        return new BarData(
            true,
            width,
            0,
            0,
            Mathf.RoundToInt((support / 100f) * width),
            GetPlayerSupportColor(),
            Color.clear,
            GetOpposingSupportColor()
        );
    }

    private Color GetPlayerSupportColor()
    {
        return uiContext?.GetPlayerFactionTheme()?.GetPrimaryColor() ?? Color.white;
    }

    private Color GetOpposingSupportColor()
    {
        string playerFactionId = uiContext?.GetPlayerFactionInstanceID();
        string opposingFactionId = uiContext
            ?.Game?.GetFactions()
            ?.FirstOrDefault(faction =>
                !string.Equals(faction.InstanceID, playerFactionId, StringComparison.Ordinal)
            )
            ?.InstanceID;
        return uiContext?.GetTheme(opposingFactionId)?.GetPrimaryColor() ?? Color.white;
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

    private static void SetBarRect(RectTransform rect, int width, int height)
    {
        rect.sizeDelta = new Vector2(width, height);
    }

    private void DispatchHover(PointerEventData eventData)
    {
        PlanetSystemWindowHit hit = CreateHit(null, eventData);
        if (hit == null)
            HoverCleared?.Invoke(this);
        else
            Hovered?.Invoke(this, hit, eventData);
    }

    internal bool TryCreateHit(PointerEventData eventData, out PlanetSystemWindowHit hit)
    {
        hit = CreateHit(null, eventData);
        return hit != null;
    }

    internal bool TryCreateHit(
        GameObject target,
        PointerEventData eventData,
        out PlanetSystemWindowHit hit
    )
    {
        hit = CreateHit(target, eventData);
        return hit != null;
    }

    private PlanetSystemWindowHit CreateHit(
        GameObject target,
        PointerEventData eventData,
        bool allowPressFallback = true
    )
    {
        if (lastData == null || eventData == null || !gameObject.activeInHierarchy)
            return null;

        target ??= eventData.pointerCurrentRaycast.gameObject;
        if (target == null && allowPressFallback)
            target = eventData.pointerPressRaycast.gameObject;
        PlanetIcon icon = PlanetIcon.None;
        bool planetImageHit = false;
        if (TryGetPointerSourcePosition(eventData, out int sourceX, out int sourceY))
        {
            icon = GetSourceIcon(sourceX, sourceY);
            planetImageHit = icon == PlanetIcon.None && IsPlanetImageSourcePoint(sourceX, sourceY);
        }

        if (icon == PlanetIcon.None && !planetImageHit)
        {
            icon = GetTargetIcon(target);
            planetImageHit = IsPlanetImageTarget(target);
        }

        if (icon == PlanetIcon.None && !planetImageHit)
            return null;

        return new PlanetSystemWindowHit(
            lastData.GalaxyMapPlanet,
            lastData.Planet,
            icon,
            planetImageHit
        );
    }

    private PlanetIcon GetTargetIcon(GameObject target)
    {
        if (target == null)
            return PlanetIcon.None;
        if (IsTargetOrChild(target, facilityImage) && IsIconVisible(PlanetIcon.Facility))
            return PlanetIcon.Facility;
        if (IsTargetOrChild(target, defenseImage) && IsIconVisible(PlanetIcon.Defense))
            return PlanetIcon.Defense;
        if (IsTargetOrChild(target, fleetImage) && IsIconVisible(PlanetIcon.Fleet))
            return PlanetIcon.Fleet;
        if (IsTargetOrChild(target, missionImage) && IsIconVisible(PlanetIcon.Mission))
            return PlanetIcon.Mission;

        return PlanetIcon.None;
    }

    private PlanetIcon GetSourceIcon(int x, int y)
    {
        PlanetIcon[] icons =
        {
            PlanetIcon.Facility,
            PlanetIcon.Defense,
            PlanetIcon.Fleet,
            PlanetIcon.Mission,
        };
        for (int i = 0; i < icons.Length; i++)
        {
            PlanetIcon icon = icons[i];
            if (IsIconVisible(icon) && IsImageSourcePoint(GetIconImage(icon), x, y))
                return icon;
        }

        return PlanetIcon.None;
    }

    private bool IsPlanetImageTarget(GameObject target)
    {
        if (target == null)
            return false;

        return IsTargetOrChild(target, planetImage) || IsTargetOrChild(target, headquartersImage);
    }

    private bool IsPlanetImageSourcePoint(int x, int y)
    {
        return IsImageSourcePoint(planetImage, x, y) || IsImageSourcePoint(headquartersImage, x, y);
    }

    private bool TryGetPointerSourcePosition(PointerEventData eventData, out int x, out int y)
    {
        x = 0;
        y = 0;
        RectTransform rect = transform as RectTransform;
        if (
            rect == null
            || !RectTransformUtility.ScreenPointToLocalPointInRectangle(
                rect,
                eventData.position,
                eventData.pressEventCamera ?? eventData.enterEventCamera,
                out Vector2 local
            )
        )
            return false;

        RectInt root = GetSourceRect(rect);
        x = Mathf.RoundToInt(local.x);
        y = Mathf.RoundToInt(-local.y);
        return x >= 0 && x < root.width && y >= 0 && y < root.height;
    }

    private static bool IsImageSourcePoint(RawImage image, int x, int y)
    {
        return image != null
            && image.isActiveAndEnabled
            && Contains(GetSourceRect(image.rectTransform), x, y);
    }

    private static bool Contains(RectInt rect, int x, int y)
    {
        return x >= rect.x && x < rect.xMax && y >= rect.y && y < rect.yMax;
    }

    private static bool IsTargetOrChild(GameObject target, Component component)
    {
        if (target == null || component == null)
            return false;

        return target.transform == component.transform
            || target.transform.IsChildOf(component.transform);
    }

    private bool IsIconVisible(PlanetIcon icon)
    {
        if (lastData == null)
            return false;

        return icon switch
        {
            PlanetIcon.Facility => !lastData.IsUnexplored && HasFacilities(lastData.Planet),
            PlanetIcon.Defense => !lastData.IsUnexplored && HasDefenses(lastData.Planet),
            PlanetIcon.Fleet => GetFleetOwnerFactionIds(lastData.Planet).Count > 0,
            PlanetIcon.Mission => GetMissionOwnerFactionIds(lastData.Planet).Count > 0,
            _ => false,
        };
    }

    private Color GetFactionColor(string factionId)
    {
        return uiContext?.GetTheme(factionId)?.GetPrimaryColor() ?? Color.white;
    }

    private static bool HasFacilities(Planet planet)
    {
        return planet?.Buildings?.Any(building =>
                building.GetBuildingType()
                    is BuildingType.Mine
                        or BuildingType.Refinery
                        or BuildingType.Shipyard
                        or BuildingType.TrainingFacility
                        or BuildingType.ConstructionFacility
            ) == true;
    }

    private static bool HasDefenses(Planet planet)
    {
        return planet != null
            && (
                planet.Buildings.Count(building =>
                    building.GetBuildingType() is BuildingType.Defense or BuildingType.Weapon
                ) > 0
                || planet.Regiments.Count > 0
                || planet.Starfighters.Count > 0
            );
    }

    private static List<string> GetFleetOwnerFactionIds(Planet planet)
    {
        return planet
                ?.Fleets?.Select(fleet => fleet.OwnerInstanceID)
                .Where(factionId => !string.IsNullOrEmpty(factionId))
                .Distinct(StringComparer.Ordinal)
                .ToList()
            ?? new List<string>();
    }

    private static List<string> GetMissionOwnerFactionIds(Planet planet)
    {
        return planet
                ?.Missions?.Select(mission => mission.OwnerInstanceID)
                .Where(factionId => !string.IsNullOrEmpty(factionId))
                .Distinct(StringComparer.Ordinal)
                .ToList()
            ?? new List<string>();
    }

    private static string SelectPresentFactionId(
        IReadOnlyList<string> presentFactionIds,
        string ownerFactionId
    )
    {
        if (presentFactionIds == null || presentFactionIds.Count == 0)
            return null;

        if (presentFactionIds.Count == 1)
            return presentFactionIds[0];

        return presentFactionIds.FirstOrDefault(factionId =>
                !string.Equals(factionId, ownerFactionId, StringComparison.Ordinal)
            ) ?? presentFactionIds[0];
    }

    private RawImage GetIconImage(PlanetIcon icon)
    {
        return icon switch
        {
            PlanetIcon.Facility => facilityImage,
            PlanetIcon.Defense => defenseImage,
            PlanetIcon.Fleet => fleetImage,
            PlanetIcon.Mission => missionImage,
            _ => null,
        };
    }

    private static RectInt GetSourceRect(RectTransform rect)
    {
        return new RectInt(
            Mathf.RoundToInt(rect.anchoredPosition.x),
            -Mathf.RoundToInt(rect.anchoredPosition.y),
            Mathf.RoundToInt(rect.sizeDelta.x),
            Mathf.RoundToInt(rect.sizeDelta.y)
        );
    }

    private sealed class BarView
    {
        private const int _meterCellStride = 3;
        private const int _meterCellStartOffset = 1;
        private const int _meterCellWidth = 2;

        private readonly RectTransform root;
        private readonly Image background;
        private readonly Image fill;
        private readonly List<Image> cells;

        public BarView(RectTransform root, Image background, Image fill, IReadOnlyList<Image> cells)
        {
            this.root = root;
            this.background = background;
            this.fill = fill;
            this.cells = cells?.Where(image => image != null).ToList() ?? new List<Image>();
        }

        public void Render(BarData data)
        {
            root.gameObject.SetActive(data.Visible);
            if (!data.Visible)
                return;

            RectInt rootTemplate = GetSourceRect(root);
            RectInt backgroundTemplate = GetSourceRect(background.rectTransform);
            RectInt fillTemplate = GetSourceRect(fill.rectTransform);
            int barWidth = data.CellCount > 0 ? data.CellCount * _meterCellStride : data.Width;

            SetBarRect(root, barWidth, rootTemplate.height);
            background.color = data.BackgroundColor;
            SetSourceRect(
                background.rectTransform,
                backgroundTemplate.x,
                backgroundTemplate.y,
                barWidth,
                backgroundTemplate.height
            );

            if (data.CellCount <= 0)
            {
                bool hasFill = data.FillWidth > 0 && data.FillColor.a > 0;
                fill.gameObject.SetActive(hasFill);
                if (hasFill)
                {
                    fill.color = data.FillColor;
                    SetSourceRect(
                        fill.rectTransform,
                        fillTemplate.x,
                        fillTemplate.y,
                        data.FillWidth,
                        fillTemplate.height
                    );
                }

                HideCells();
                return;
            }

            fill.gameObject.SetActive(false);
            for (int i = 0; i < data.CellCount; i++)
            {
                RectInt cellRect = GetCellRect(i, backgroundTemplate);
                Image cell = GetCell(i);
                cell.color = i < data.LitCells ? data.FillColor : data.EmptyColor;
                SetSourceRect(
                    cell.rectTransform,
                    cellRect.x,
                    cellRect.y,
                    cellRect.width,
                    cellRect.height
                );
                cell.gameObject.SetActive(true);
            }

            for (int i = data.CellCount; i < cells.Count; i++)
                cells[i].gameObject.SetActive(false);
        }

        private Image GetCell(int index)
        {
            while (cells.Count <= index)
            {
                if (cells.Count == 0)
                    throw new MissingReferenceException($"{root.name}/Cell0Image is missing.");

                Image cell = UnityEngine.Object.Instantiate(cells[0], root);
                cell.name = $"Cell{cells.Count}Image";
                cell.raycastTarget = false;
                cells.Add(cell);
            }

            return cells[index];
        }

        private RectInt GetCellRect(int index, RectInt backgroundTemplate)
        {
            RectInt template = GetSourceRect(cells[0].rectTransform);
            return new RectInt(
                backgroundTemplate.x + _meterCellStartOffset + index * _meterCellStride,
                template.y,
                _meterCellWidth,
                template.height
            );
        }

        private void HideCells()
        {
            foreach (Image cell in cells)
                cell.gameObject.SetActive(false);
        }
    }

    private enum PlanetSystemWindowIcon
    {
        Facility,
        Defense,
    }

    private readonly struct BarData
    {
        public BarData(
            bool visible,
            int width,
            int cellCount,
            int litCells,
            int fillWidth,
            Color32 fillColor,
            Color32 emptyColor,
            Color32 backgroundColor
        )
        {
            Visible = visible;
            Width = width;
            CellCount = cellCount;
            LitCells = litCells;
            FillWidth = fillWidth;
            FillColor = fillColor;
            EmptyColor = emptyColor;
            BackgroundColor = backgroundColor;
        }

        public bool Visible { get; }
        public int Width { get; }
        public int CellCount { get; }
        public int LitCells { get; }
        public int FillWidth { get; }
        public Color32 FillColor { get; }
        public Color32 EmptyColor { get; }
        public Color32 BackgroundColor { get; }
    }
}

public sealed class PlanetSystemWindowPlanetRenderData
{
    internal GalaxyMapPlanet GalaxyMapPlanet;
    public Planet Planet;
    public Texture2D PlanetTexture;
    public int PopularSupport;
    public bool IsUnexplored;
    internal PlanetIcon SelectedIcon;
}
