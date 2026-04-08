using System;
using System.Collections.Generic;
using System.Linq;
using Rebellion.Game;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public enum PlanetViewIconType
{
    Buildings,
    Fleets,
    Defenses,
    Missions,
}

public enum PlanetIconInteractionType
{
    DoubleClick,
    RightClick,
    DragStart,
}

public sealed class PlanetView : MonoBehaviour
{
    [Header("Core")]
    [SerializeField]
    private Image planetImage;

    [SerializeField]
    private TextMeshProUGUI planetNameText;

    [SerializeField]
    private RectTransform popularSupportRow;

    [SerializeField]
    private RectTransform resourceRow;

    [SerializeField]
    private RectTransform buildingRow;

    [Header("Overlay Icons")]
    [SerializeField]
    private IconButton missionsIcon;

    [SerializeField]
    private IconButton buildingsIcon;

    [SerializeField]
    private IconButton defensesIcon;

    [SerializeField]
    private IconButton fleetsIcon;

    private Planet planet;
    private UIContext uiContext;

    public event Action<PlanetViewIconType, PlanetIconInteractionType> PlanetIconInteracted;

    private void Awake()
    {
        ValidateBindings();

        WireInteractions(buildingsIcon, PlanetViewIconType.Buildings);
        WireInteractions(defensesIcon, PlanetViewIconType.Defenses);
        WireInteractions(fleetsIcon, PlanetViewIconType.Fleets);
        WireInteractions(missionsIcon, PlanetViewIconType.Missions);
    }

    private void ValidateBindings()
    {
        if (planetImage == null)
            throw new InvalidOperationException("planetImage not assigned.");
        if (planetNameText == null)
            throw new InvalidOperationException("planetNameText not assigned.");
        if (popularSupportRow == null)
            throw new InvalidOperationException("popularSupportRow not assigned.");

        if (buildingsIcon == null)
            throw new InvalidOperationException("buildingsIcon not assigned.");
        if (defensesIcon == null)
            throw new InvalidOperationException("defensesIcon not assigned.");
        if (fleetsIcon == null)
            throw new InvalidOperationException("fleetsIcon not assigned.");
        if (missionsIcon == null)
            throw new InvalidOperationException("missionsIcon not assigned.");
    }

    public void Initialize(Planet planet, UIContext uiContext)
    {
        this.planet = planet ?? throw new ArgumentNullException(nameof(planet));
        this.uiContext = uiContext ?? throw new ArgumentNullException(nameof(uiContext));

        Refresh();
    }

    public void Refresh()
    {
        string playerFactionId = uiContext.GetPlayerFactionInstanceID();

        bool isVisited = planet.WasVisitedBy(playerFactionId);

        ApplyBaseVisuals();

        if (!isVisited)
        {
            HideUnknownState();
            return;
        }

        ApplyOverlays();
        ApplyPopularSupport();
    }

    private void HideUnknownState()
    {
        missionsIcon.gameObject.SetActive(false);
        buildingsIcon.gameObject.SetActive(false);
        defensesIcon.gameObject.SetActive(false);
        fleetsIcon.gameObject.SetActive(false);
        resourceRow.gameObject.SetActive(false);
        buildingRow.gameObject.SetActive(false);
        popularSupportRow.gameObject.SetActive(false);
    }

    private void ApplyBaseVisuals()
    {
        planetNameText.text = planet.GetDisplayName();
        planetImage.sprite = ResourceManager.GetSprite(planet.GetPlanetIconPath());

        FactionTheme ownerTheme = uiContext.GetTheme(planet.GetOwnerInstanceID());
        planetNameText.color = ownerTheme.GetPrimaryColor();
    }

    private void ApplyOverlays()
    {
        FactionTheme ownerTheme = uiContext.GetTheme(planet.GetOwnerInstanceID());
        PlanetOverlayTheme ownerOverlay = ownerTheme?.PlanetOverlayTheme;

        // Missions
        string missionFactionId = planet.GetMissionFactionInstanceIDs()?.FirstOrDefault();
        PlanetOverlayTheme missionTheme = uiContext.GetTheme(missionFactionId)?.PlanetOverlayTheme;

        SetOverlayState(
            missionsIcon,
            missionTheme?.PlanetOverlayIcons?.Missions,
            missionTheme != null
        );

        // Buildings
        bool hasBuildings = planet.GetAllBuildings().Count > 0;

        SetOverlayState(buildingsIcon, ownerOverlay?.PlanetOverlayIcons?.Buildings, hasBuildings);

        // Defenses
        SetOverlayState(
            defensesIcon,
            ownerOverlay?.PlanetOverlayIcons?.Defenses,
            planet.HasGarrison()
        );

        // Fleets
        string fleetOwnerId = planet.GetFleets()?.FirstOrDefault()?.GetOwnerInstanceID();
        PlanetOverlayTheme fleetTheme = uiContext.GetTheme(fleetOwnerId)?.PlanetOverlayTheme;

        SetOverlayState(fleetsIcon, fleetTheme?.PlanetOverlayIcons?.Fleets, fleetTheme != null);
    }

    private void SetOverlayState(IconButton button, OverlayIconTheme iconTheme, bool condition)
    {
        if (!condition || iconTheme == null || string.IsNullOrEmpty(iconTheme.NormalImagePath))
        {
            button.gameObject.SetActive(false);
            return;
        }

        Sprite normal = ResourceManager.GetSprite(iconTheme.NormalImagePath);

        Sprite hover = null;
        if (!string.IsNullOrEmpty(iconTheme.HoverImagePath))
            hover = ResourceManager.GetSprite(iconTheme.HoverImagePath);

        button.SetSprites(normal, hover, null, null, null);
        button.gameObject.SetActive(true);
    }

    private void WireInteractions(IconButton target, PlanetViewIconType iconType)
    {
        PlanetIconInteraction interaction =
            target.GetComponent<PlanetIconInteraction>()
            ?? target.gameObject.AddComponent<PlanetIconInteraction>();

        interaction.OnDoubleClick = () =>
            PlanetIconInteracted?.Invoke(iconType, PlanetIconInteractionType.DoubleClick);

        interaction.OnRightClick = () =>
            PlanetIconInteracted?.Invoke(iconType, PlanetIconInteractionType.RightClick);

        interaction.OnDragStart = () =>
            PlanetIconInteracted?.Invoke(iconType, PlanetIconInteractionType.DragStart);
    }

    private void ApplyPopularSupport()
    {
        foreach (Transform child in popularSupportRow)
            Destroy(child.gameObject);

        float totalWidth = popularSupportRow.rect.width;
        float currentX = 0f;

        string playerFactionId = uiContext.GetPlayerFactionInstanceID();
        string defaultFactionId = "DEFAULT";

        List<KeyValuePair<string, int>> sortedSupport = planet
            .PopularSupport.Where(pair => pair.Value > 0)
            .OrderByDescending(pair => pair.Key == playerFactionId)
            .ThenByDescending(pair => pair.Value)
            .ToList();

        int runningSupport = sortedSupport.Sum(pair => pair.Value);
        int remainder = Mathf.Clamp(100 - runningSupport, 0, 100);

        if (remainder > 0)
            sortedSupport.Add(new KeyValuePair<string, int>(defaultFactionId, remainder));

        for (int i = 0; i < sortedSupport.Count; i++)
        {
            KeyValuePair<string, int> entry = sortedSupport[i];

            FactionTheme theme = uiContext.GetTheme(entry.Key);
            Color color = theme.GetPrimaryColor();

            float segmentWidth =
                (i == sortedSupport.Count - 1)
                    ? totalWidth - currentX
                    : Mathf.Round(totalWidth * (entry.Value / 100f));

            GameObject segment = new GameObject($"Support_{entry.Key}", typeof(Image));
            segment.transform.SetParent(popularSupportRow, false);

            Image img = segment.GetComponent<Image>();
            img.color = color;

            RectTransform rect = segment.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0, 0);
            rect.anchorMax = new Vector2(0, 1);
            rect.pivot = new Vector2(0, 0.5f);
            rect.anchoredPosition = new Vector2(currentX, 0);
            rect.sizeDelta = new Vector2(segmentWidth, 0);

            currentX += segmentWidth;
        }
    }

    private sealed class PlanetIconInteraction
        : MonoBehaviour,
            IPointerClickHandler,
            IPointerEnterHandler,
            IPointerExitHandler,
            IBeginDragHandler
    {
        public Action OnDoubleClick;
        public Action OnRightClick;
        public Action OnDragStart;

        private float lastClickTime;
        private const float DoubleClickThreshold = 0.25f;

        public void OnPointerClick(PointerEventData eventData)
        {
            if (eventData.button == PointerEventData.InputButton.Right)
            {
                OnRightClick?.Invoke();
                return;
            }

            if (eventData.button != PointerEventData.InputButton.Left)
                return;

            float now = Time.unscaledTime;

            if (now - lastClickTime <= DoubleClickThreshold)
                OnDoubleClick?.Invoke();

            lastClickTime = now;
        }

        public void OnPointerEnter(PointerEventData eventData) { }

        public void OnPointerExit(PointerEventData eventData) { }

        public void OnBeginDrag(PointerEventData eventData) => OnDragStart?.Invoke();
    }
}
