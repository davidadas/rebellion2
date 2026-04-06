using System;
using Rebellion.Game;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public sealed class PlanetPanel : MonoBehaviour, IPointerDownHandler
{
    [Header("Header")]
    [SerializeField]
    private TextMeshProUGUI titleText;

    [SerializeField]
    private Image headerBackground;

    [SerializeField]
    private Button closeButton;

    [SerializeField]
    private WindowDragHandle dragHandle;

    [Header("Panes")]
    [SerializeField]
    private FleetsPane fleetsPane;

    [SerializeField]
    private MissionsPane missionsPane;

    [SerializeField]
    private GarrisonPane garrisonPane;

    [SerializeField]
    private BuildingsPane buildingsPane;

    private RectTransform rectTransform;

    private Planet planet;
    private UIContext uiContext;
    private PlanetViewIconType currentView;

    private bool isInitialized;

    public event Action<PlanetPanel> OnClosed;

    private void Awake()
    {
        rectTransform = transform as RectTransform;

        if (rectTransform == null)
            throw new InvalidOperationException("PlanetPanel requires RectTransform.");

        if (dragHandle == null)
            throw new InvalidOperationException("PlanetPanel missing WindowDragHandle.");

        if (fleetsPane == null)
            throw new InvalidOperationException("PlanetPanel missing FleetsPane.");

        if (missionsPane == null)
            throw new InvalidOperationException("PlanetPanel missing MissionsPane.");

        if (garrisonPane == null)
            throw new InvalidOperationException("PlanetPanel missing GarrisonPane.");

        if (buildingsPane == null)
            throw new InvalidOperationException("PlanetPanel missing BuildingsPane.");

        if (closeButton != null)
            closeButton.onClick.AddListener(OnCloseClicked);
    }

    private void OnDestroy()
    {
        if (closeButton != null)
            closeButton.onClick.RemoveListener(OnCloseClicked);
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        transform.SetAsLastSibling();
    }

    public void Initialize(Planet planet, UIContext uiContext, PlanetViewIconType initialView)
    {
        if (isInitialized)
            throw new InvalidOperationException("PlanetPanel already initialized.");

        if (planet == null)
            throw new ArgumentNullException(nameof(planet));

        if (uiContext == null)
            throw new ArgumentNullException(nameof(uiContext));

        this.planet = planet;
        this.uiContext = uiContext;

        fleetsPane.Initialize(planet, uiContext);
        missionsPane.Initialize(planet, uiContext);
        garrisonPane.Initialize(planet, uiContext);
        buildingsPane.Initialize(planet, uiContext);

        isInitialized = true;

        Render();
        ShowView(initialView);
    }

    public void ConfigureDrag(RectTransform dragBounds)
    {
        if (dragBounds == null)
            throw new ArgumentNullException(nameof(dragBounds));

        dragHandle.SetWindowRoot(rectTransform);
        dragHandle.SetDragBounds(dragBounds);
    }

    public void ShowView(PlanetViewIconType view)
    {
        if (!isInitialized)
            throw new InvalidOperationException("PlanetPanel not initialized.");

        currentView = view;

        fleetsPane.gameObject.SetActive(view == PlanetViewIconType.Fleets);
        missionsPane.gameObject.SetActive(view == PlanetViewIconType.Missions);
        buildingsPane.gameObject.SetActive(view == PlanetViewIconType.Buildings);
        garrisonPane.gameObject.SetActive(view == PlanetViewIconType.Defenses);
    }

    public bool IsShowing(Planet planet, PlanetViewIconType view)
    {
        return this.planet == planet && this.currentView == view;
    }

    public void Refresh()
    {
        if (!isInitialized)
            return;

        Render();
        garrisonPane.Refresh();
        fleetsPane.Refresh();
        buildingsPane.Refresh();
    }

    private void OnCloseClicked()
    {
        OnClosed?.Invoke(this);
        Destroy(gameObject);
    }

    private void Render()
    {
        if (!isInitialized)
            throw new InvalidOperationException("PlanetPanel was not initialized.");

        if (titleText != null)
            titleText.SetText(planet.GetDisplayName());

        if (headerBackground != null)
        {
            string ownerId = planet.GetOwnerInstanceID();
            FactionTheme theme = uiContext.GetTheme(ownerId);
            headerBackground.color = theme.GetPrimaryColor();
        }
    }
}
