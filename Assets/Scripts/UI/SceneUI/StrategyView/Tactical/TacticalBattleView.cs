using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Draws the tactical battle arena and its information panel, and raises pointer and control
/// requests.
/// </summary>
public sealed class TacticalBattleView
    : MonoBehaviour,
        IPointerDownHandler,
        IPointerUpHandler,
        IDragHandler,
        IScrollHandler
{
    private static readonly Color _onlineColor = new Color(0.25f, 0.75f, 0.3f, 1f);
    private static readonly Color _offlineColor = new Color(0.85f, 0.25f, 0.25f, 1f);
    private static readonly Color _absentColor = new Color(0.3f, 0.3f, 0.36f, 1f);
    private static readonly Color _hullHealthyColor = new Color(0.35f, 0.9f, 0.35f, 1f);
    private static readonly Color _hullCriticalColor = new Color(0.95f, 0.3f, 0.25f, 1f);
    private static readonly Color _starfieldTint = new Color(0.4f, 0.43f, 0.52f, 1f);
    private const int _rosterSlotWidth = 34;
    private const int _rosterSlotHeight = 30;
    private const int _rosterSlotGap = 3;
    private const int _rosterGroupGap = 10;
    private const int _rosterControlWidth = 64;
    private const int _rosterClassWidth = 108;

    [Header("Arena")]
    [SerializeField]
    private RawImage backgroundImage;

    [SerializeField]
    private RectTransform arenaLayer;

    [SerializeField]
    private RawImage starfieldImage;

    [SerializeField]
    private RawImage planetImage;

    [SerializeField]
    private RawImage sceneImage;

    [SerializeField]
    private TacticalBattleScene sceneView;

    [SerializeField]
    private RectTransform unitLayer;

    [SerializeField]
    private RectTransform shotLayer;

    [SerializeField]
    private TacticalUnitView unitTemplate;

    [SerializeField]
    private RawImage shotTemplate;

    [SerializeField]
    private RawImage selectionBoxImage;

    [SerializeField]
    private TextMeshProUGUI titleTextField;

    [Header("Roster")]
    [SerializeField]
    private RectTransform rosterLayer;

    [SerializeField]
    private Button rosterSideButton;

    [SerializeField]
    private TextMeshProUGUI rosterSideLabel;

    [SerializeField]
    private Button rosterClassButton;

    [SerializeField]
    private TextMeshProUGUI rosterClassLabel;

    [SerializeField]
    private TacticalRosterSlotView rosterSlotTemplate;

    [Header("Panel")]
    [SerializeField]
    private RawImage portraitImage;

    [SerializeField]
    private TextMeshProUGUI nameTextField;

    [SerializeField]
    private TextMeshProUGUI subtitleTextField;

    [SerializeField]
    private RawImage shieldBarBackgroundImage;

    [SerializeField]
    private RawImage shieldBarImage;

    [SerializeField]
    private TextMeshProUGUI shieldTextField;

    [SerializeField]
    private RawImage hullBarBackgroundImage;

    [SerializeField]
    private RawImage hullBarImage;

    [SerializeField]
    private TextMeshProUGUI hullTextField;

    [SerializeField]
    private RawImage[] systemImages = Array.Empty<RawImage>();

    [SerializeField]
    private TextMeshProUGUI[] systemLabels = Array.Empty<TextMeshProUGUI>();

    [SerializeField]
    private RawImage[] arcBarBackgroundImages = Array.Empty<RawImage>();

    [SerializeField]
    private RawImage[] arcBarImages = Array.Empty<RawImage>();

    [SerializeField]
    private RawImage[] personnelImages = Array.Empty<RawImage>();

    [SerializeField]
    private TextMeshProUGUI personnelTextField;

    [SerializeField]
    private TextMeshProUGUI ordersTextField;

    [SerializeField]
    private TextMeshProUGUI tacticsTextField;

    [SerializeField]
    private Button targetPriorityButton;

    [SerializeField]
    private TextMeshProUGUI targetPriorityLabel;

    [Header("Battle")]
    [SerializeField]
    private TextMeshProUGUI attackerStatusTextField;

    [SerializeField]
    private TextMeshProUGUI defenderStatusTextField;

    [SerializeField]
    private TextMeshProUGUI statusTextField;

    [SerializeField]
    private TextMeshProUGUI selectionTextField;

    [SerializeField]
    private Button retreatButton;

    [SerializeField]
    private TextMeshProUGUI retreatLabel;

    [SerializeField]
    private Button autoButton;

    [SerializeField]
    private TextMeshProUGUI autoLabel;

    [SerializeField]
    private Button pauseButton;

    [SerializeField]
    private TextMeshProUGUI pauseLabel;

    [SerializeField]
    private Button speedButton;

    [SerializeField]
    private TextMeshProUGUI speedLabel;

    private readonly List<TacticalUnitView> _unitViews = new List<TacticalUnitView>();
    private readonly List<RawImage> _shotImages = new List<RawImage>();
    private readonly List<TacticalRosterSlotView> _rosterShipSlots =
        new List<TacticalRosterSlotView>();
    private readonly List<TacticalRosterSlotView> _rosterSquadronSlots =
        new List<TacticalRosterSlotView>();
    private readonly List<(Button button, UnityAction listener)> _buttonListeners =
        new List<(Button, UnityAction)>();
    private bool _bound;

    /// <summary>
    /// Gets the 3D scene that draws the units into the arena.
    /// </summary>
    internal TacticalBattleScene Scene
    {
        get
        {
            VerifyReferences();
            return sceneView;
        }
    }

    /// <summary>
    /// Occurs when the player requests a retreat.
    /// </summary>
    internal event Action RetreatRequested;

    /// <summary>
    /// Occurs when the player toggles automatic control.
    /// </summary>
    internal event Action AutoToggleRequested;

    /// <summary>
    /// Occurs when the player toggles pause.
    /// </summary>
    internal event Action PauseToggleRequested;

    /// <summary>
    /// Occurs when the player cycles the playback speed.
    /// </summary>
    internal event Action SpeedCycleRequested;

    /// <summary>
    /// Occurs when a control is pressed.
    /// </summary>
    internal event Action ControlPressed;

    /// <summary>
    /// Occurs when a pointer button is pressed on the arena at a source-space point.
    /// </summary>
    internal event Action<Vector2, PointerEventData.InputButton> ArenaPressed;

    /// <summary>
    /// Occurs when the pointer drags across the arena to a source-space point.
    /// </summary>
    internal event Action<Vector2, PointerEventData.InputButton> ArenaDragged;

    /// <summary>
    /// Raised when the mouse wheel scrolls over the arena, with the wheel delta.
    /// </summary>
    internal event Action<float> ArenaScrolled;

    /// <summary>
    /// Raised when the panel's target-priority button is pressed.
    /// </summary>
    internal event Action TargetPriorityToggleRequested;

    /// <summary>
    /// Raised when the roster's side toggle is pressed.
    /// </summary>
    internal event Action RosterSidePressed;

    /// <summary>
    /// Raised when the roster's fighter-class selector is pressed.
    /// </summary>
    internal event Action RosterClassPressed;

    /// <summary>
    /// Raised when a roster ship slot is pressed, with the slot index.
    /// </summary>
    internal event Action<int> RosterShipPressed;

    /// <summary>
    /// Raised when a roster squadron slot is pressed, with the slot index.
    /// </summary>
    internal event Action<int> RosterSquadronPressed;

    /// <summary>
    /// Occurs when a pointer button is released over the arena at a source-space point.
    /// </summary>
    internal event Action<Vector2, PointerEventData.InputButton> ArenaReleased;

    /// <summary>
    /// Verifies authored references and binds controls once.
    /// </summary>
    private void Awake()
    {
        VerifyReferences();
        BindControls();
    }

    /// <summary>
    /// Releases control bindings.
    /// </summary>
    private void OnDestroy()
    {
        foreach ((Button button, UnityAction listener) in _buttonListeners)
        {
            if (button != null)
                button.onClick.RemoveListener(listener);
        }

        _buttonListeners.Clear();
    }

    /// <summary>
    /// Returns the source-space bounds of the arena within this view.
    /// </summary>
    /// <returns>The arena bounds.</returns>
    internal RectInt GetArenaSourceRect()
    {
        VerifyReferences();
        return UILayout.GetSourceRect(arenaLayer);
    }

    /// <summary>
    /// Shows the battle layer.
    /// </summary>
    internal void Show()
    {
        VerifyReferences();
        BindControls();
        gameObject.SetActive(true);
        sceneView.SetVisible(true);
    }

    /// <summary>
    /// Shows or hides the layer and its scene without discarding the battle presentation.
    /// </summary>
    /// <param name="visible">Whether the layer is visible.</param>
    internal void SetVisible(bool visible)
    {
        VerifyReferences();
        gameObject.SetActive(visible);
        sceneView.SetVisible(visible);
    }

    /// <summary>
    /// Hides the battle layer and releases pooled unit and shot visuals.
    /// </summary>
    internal void Hide()
    {
        foreach (TacticalUnitView unitView in _unitViews)
        {
            if (unitView != null)
                unitView.gameObject.SetActive(false);
        }

        foreach (RawImage shot in _shotImages)
        {
            if (shot != null)
                shot.gameObject.SetActive(false);
        }

        foreach (TacticalRosterSlotView slot in _rosterShipSlots)
        {
            if (slot != null)
                slot.gameObject.SetActive(false);
        }

        foreach (TacticalRosterSlotView slot in _rosterSquadronSlots)
        {
            if (slot != null)
                slot.gameObject.SetActive(false);
        }

        if (selectionBoxImage != null)
            selectionBoxImage.gameObject.SetActive(false);
        if (sceneView)
            sceneView.SetVisible(false);
        gameObject.SetActive(false);
    }

    /// <summary>
    /// Applies one complete battle presentation.
    /// </summary>
    /// <param name="data">The presentation to apply.</param>
    internal void Render(TacticalBattleRenderData data)
    {
        if (data == null)
            throw new ArgumentNullException(nameof(data));

        VerifyReferences();
        BindControls();
        gameObject.SetActive(true);
        UILayout.SetTextContent(titleTextField, data.Title);
        UILayout.SetTextContent(attackerStatusTextField, data.AttackerStatus);
        UILayout.SetTextContent(defenderStatusTextField, data.DefenderStatus);
        UILayout.SetTextContent(statusTextField, data.ClockStatus);
        UILayout.SetTextContent(selectionTextField, data.Hint);
        UILayout.SetTextContent(retreatLabel, data.RetreatLabel);
        retreatButton.interactable = data.RetreatInteractable;
        UILayout.SetTextContent(autoLabel, data.AutoLabel);
        autoButton.interactable = data.AutoInteractable;
        UILayout.SetTextContent(pauseLabel, data.PauseLabel);
        UILayout.SetTextContent(speedLabel, data.SpeedLabel);

        starfieldImage.texture = data.StarfieldTexture;
        starfieldImage.color = _starfieldTint;
        starfieldImage.enabled = data.StarfieldTexture != null;
        planetImage.texture = data.PlanetTexture;
        planetImage.gameObject.SetActive(data.PlanetTexture != null);
        if (data.PlanetTexture != null)
        {
            UILayout.SetSourceRect(
                planetImage.rectTransform,
                data.PlanetBounds.x,
                data.PlanetBounds.y,
                data.PlanetBounds.width,
                data.PlanetBounds.height
            );
        }

        RenderShots(data.Shots);
        RenderUnits(data.Units);
        RenderSelectionBox(data.SelectionBox);
        RenderPanel(data.Panel);
        RenderRoster(data.Roster);
    }

    /// <summary>
    /// Lays out the roster bar: the side toggle, the ship slots, the fighter-class selector, and
    /// the squadron slots, left to right.
    /// </summary>
    /// <param name="roster">The roster presentation, or null to clear the bar.</param>
    private void RenderRoster(TacticalRosterRenderData roster)
    {
        if (roster == null)
        {
            rosterLayer.gameObject.SetActive(false);
            return;
        }

        rosterLayer.gameObject.SetActive(true);
        int x = 0;
        UILayout.SetSourceRect(
            rosterSideButton.transform as RectTransform,
            x,
            0,
            _rosterControlWidth,
            _rosterSlotHeight
        );
        UILayout.SetTextContent(rosterSideLabel, roster.SideLabel);
        x += _rosterControlWidth + _rosterGroupGap;
        x = RenderRosterSlots(_rosterShipSlots, roster.Ships, x, RosterShipPressed);
        x += _rosterGroupGap;
        UILayout.SetSourceRect(
            rosterClassButton.transform as RectTransform,
            x,
            0,
            _rosterClassWidth,
            _rosterSlotHeight
        );
        UILayout.SetTextContent(rosterClassLabel, roster.FighterClassLabel);
        rosterClassButton.interactable = roster.FighterClassInteractable;
        x += _rosterClassWidth + _rosterSlotGap;
        RenderRosterSlots(_rosterSquadronSlots, roster.Squadrons, x, RosterSquadronPressed);
    }

    /// <summary>
    /// Renders one group of roster slots from a pool, left to right from a starting position.
    /// </summary>
    /// <param name="pool">The pooled slot views for this group.</param>
    /// <param name="entries">The entries to show.</param>
    /// <param name="x">The source-space position of the first slot.</param>
    /// <param name="pressed">The event raised with a slot's index when it is clicked.</param>
    /// <returns>The source-space position just past the last slot.</returns>
    private int RenderRosterSlots(
        List<TacticalRosterSlotView> pool,
        IReadOnlyList<TacticalRosterSlotRenderData> entries,
        int x,
        Action<int> pressed
    )
    {
        while (pool.Count < entries.Count)
        {
            TacticalRosterSlotView slot = Instantiate(rosterSlotTemplate, rosterLayer);
            slot.name = $"{rosterSlotTemplate.name}{pool.Count}";
            pool.Add(slot);
        }

        for (int index = 0; index < pool.Count; index++)
        {
            if (index >= entries.Count)
            {
                pool[index].gameObject.SetActive(false);
                continue;
            }

            int slotIndex = index;
            UILayout.SetSourceRect(
                pool[index].transform as RectTransform,
                x,
                0,
                _rosterSlotWidth,
                _rosterSlotHeight
            );
            pool[index].Render(entries[index], () => pressed?.Invoke(slotIndex));
            x += _rosterSlotWidth + _rosterSlotGap;
        }

        return x;
    }

    /// <summary>
    /// Raises an arena press at the pointer's source-space position.
    /// </summary>
    /// <param name="eventData">The pointer event.</param>
    public void OnPointerDown(PointerEventData eventData)
    {
        if (TryGetArenaPoint(eventData, clamp: false, out Vector2 point))
            ArenaPressed?.Invoke(point, eventData.button);
    }

    /// <summary>
    /// Raises an arena release at the pointer's source-space position.
    /// </summary>
    /// <param name="eventData">The pointer event.</param>
    public void OnPointerUp(PointerEventData eventData)
    {
        if (TryGetArenaPoint(eventData, clamp: true, out Vector2 point))
            ArenaReleased?.Invoke(point, eventData.button);
    }

    /// <summary>
    /// Raises an arena drag at the pointer's source-space position.
    /// </summary>
    /// <param name="eventData">The pointer event.</param>
    public void OnDrag(PointerEventData eventData)
    {
        if (TryGetArenaPoint(eventData, clamp: true, out Vector2 point))
            ArenaDragged?.Invoke(point, eventData.button);
    }

    /// <summary>
    /// Raises an arena scroll when the wheel turns over the arena.
    /// </summary>
    /// <param name="eventData">The pointer event.</param>
    public void OnScroll(PointerEventData eventData)
    {
        if (eventData != null && TryGetArenaPoint(eventData, clamp: false, out _))
            ArenaScrolled?.Invoke(eventData.scrollDelta.y);
    }

    /// <summary>
    /// Converts a pointer event into a source-space point on the arena.
    /// </summary>
    /// <param name="eventData">The pointer event.</param>
    /// <param name="clamp">
    /// Whether points outside the arena are clamped to its edge instead of rejected.
    /// </param>
    /// <param name="point">Receives the source-space point.</param>
    /// <returns>True when the pointer could be projected onto the arena.</returns>
    private bool TryGetArenaPoint(PointerEventData eventData, bool clamp, out Vector2 point)
    {
        point = Vector2.zero;
        RectTransform surface = transform as RectTransform;
        if (
            eventData == null
            || surface == null
            || !RectTransformUtility.ScreenPointToLocalPointInRectangle(
                surface,
                eventData.position,
                eventData.pressEventCamera,
                out Vector2 localPosition
            )
        )
            return false;

        Vector2 size = surface.rect.size;
        RectInt arena = GetArenaSourceRect();
        float x = localPosition.x + size.x / 2f;
        float y = size.y / 2f - localPosition.y;
        if (!clamp && (x < arena.xMin || x >= arena.xMax || y < arena.yMin || y >= arena.yMax))
            return false;

        point = new Vector2(
            Mathf.Clamp(x, arena.xMin, arena.xMax),
            Mathf.Clamp(y, arena.yMin, arena.yMax)
        );
        return true;
    }

    /// <summary>
    /// Draws the information panel for the inspected unit, or its empty state.
    /// </summary>
    /// <param name="panel">The panel presentation, or null when nothing is inspected.</param>
    private void RenderPanel(TacticalPanelRenderData panel)
    {
        if (panel == null)
        {
            portraitImage.gameObject.SetActive(false);
            UILayout.SetTextContent(nameTextField, "No ship selected");
            UILayout.SetTextContent(subtitleTextField, string.Empty);
            RenderBar(shieldBarImage, shieldBarBackgroundImage, 0f, _absentColor);
            UILayout.SetTextContent(shieldTextField, "—");
            RenderBar(hullBarImage, hullBarBackgroundImage, 0f, _absentColor);
            UILayout.SetTextContent(hullTextField, "—");
            RenderSystems(Array.Empty<TacticalSystemRenderData>());
            RenderArcs(Array.Empty<float>());
            RenderPersonnel(Array.Empty<TacticalPersonnelRenderData>());
            UILayout.SetTextContent(personnelTextField, string.Empty);
            UILayout.SetTextContent(ordersTextField, string.Empty);
            UILayout.SetTextContent(tacticsTextField, string.Empty);
            UILayout.SetTextContent(targetPriorityLabel, "—");
            targetPriorityButton.interactable = false;
            return;
        }

        portraitImage.texture = panel.Portrait;
        portraitImage.gameObject.SetActive(panel.Portrait != null);
        UILayout.SetTextContent(nameTextField, panel.Name);
        UILayout.SetTextContent(subtitleTextField, panel.Subtitle);
        RenderBar(
            shieldBarImage,
            shieldBarBackgroundImage,
            panel.ShowShields ? panel.ShieldFraction : 0f,
            new Color(0.35f, 0.65f, 1f, 1f)
        );
        UILayout.SetTextContent(shieldTextField, panel.ShieldText);
        RenderBar(
            hullBarImage,
            hullBarBackgroundImage,
            panel.HullFraction,
            Color.Lerp(_hullCriticalColor, _hullHealthyColor, Mathf.Clamp01(panel.HullFraction))
        );
        UILayout.SetTextContent(hullTextField, panel.HullText);
        RenderSystems(panel.Systems);
        RenderArcs(panel.ArcCharges);
        RenderPersonnel(panel.Personnel);
        UILayout.SetTextContent(personnelTextField, panel.PersonnelText);
        UILayout.SetTextContent(ordersTextField, panel.OrdersText);
        UILayout.SetTextContent(tacticsTextField, panel.TacticsText);
        UILayout.SetTextContent(targetPriorityLabel, panel.TargetPriorityLabel);
        targetPriorityButton.interactable = panel.TargetPriorityInteractable;
    }

    /// <summary>
    /// Sizes a horizontal fill bar against its authored background.
    /// </summary>
    /// <param name="fill">The fill image.</param>
    /// <param name="background">The background image defining the full width.</param>
    /// <param name="fraction">The filled fraction.</param>
    /// <param name="color">The fill color.</param>
    private static void RenderBar(RawImage fill, RawImage background, float fraction, Color color)
    {
        RectInt bounds = UILayout.GetSourceRect(background.rectTransform);
        int width = Mathf.RoundToInt(bounds.width * Mathf.Clamp01(fraction));
        UILayout.SetSourceRect(
            fill.rectTransform,
            bounds.x,
            bounds.y,
            Mathf.Max(width, 0),
            bounds.height
        );
        fill.color = color;
        fill.enabled = width > 0;
    }

    /// <summary>
    /// Colors the system indicators.
    /// </summary>
    /// <param name="systems">The system indicators, or empty to clear them.</param>
    private void RenderSystems(IReadOnlyList<TacticalSystemRenderData> systems)
    {
        for (int index = 0; index < systemImages.Length; index++)
        {
            TacticalSystemRenderData system = index < systems.Count ? systems[index] : null;
            systemImages[index].color =
                system == null ? _absentColor : GetSystemColor(system.State);
            if (index < systemLabels.Length)
                UILayout.SetTextContent(systemLabels[index], system?.Label ?? "—");
        }
    }

    /// <summary>
    /// Sizes the weapon-arc charge bars.
    /// </summary>
    /// <param name="arcCharges">The arc charge fractions, or empty to clear them.</param>
    private void RenderArcs(IReadOnlyList<float> arcCharges)
    {
        int count = Mathf.Min(arcBarImages.Length, arcBarBackgroundImages.Length);
        for (int index = 0; index < count; index++)
        {
            float charge = index < arcCharges.Count ? arcCharges[index] : 0f;
            RenderBar(
                arcBarImages[index],
                arcBarBackgroundImages[index],
                charge,
                charge >= 0.999f
                    ? new Color(1f, 0.85f, 0.35f, 1f)
                    : new Color(0.75f, 0.6f, 0.25f, 1f)
            );
        }
    }

    /// <summary>
    /// Shows the personnel portraits.
    /// </summary>
    /// <param name="personnel">The personnel entries, or empty to clear them.</param>
    private void RenderPersonnel(IReadOnlyList<TacticalPersonnelRenderData> personnel)
    {
        for (int index = 0; index < personnelImages.Length; index++)
        {
            RawImage image = personnelImages[index];
            Texture2D portrait = index < personnel.Count ? personnel[index].Portrait : null;
            image.texture = portrait;
            image.gameObject.SetActive(portrait != null);
        }
    }

    /// <summary>
    /// Returns the indicator color for a system state.
    /// </summary>
    /// <param name="state">The system state.</param>
    /// <returns>The indicator color.</returns>
    private static Color GetSystemColor(TacticalSystemState state)
    {
        return state switch
        {
            TacticalSystemState.Online => _onlineColor,
            TacticalSystemState.Offline => _offlineColor,
            _ => _absentColor,
        };
    }

    /// <summary>
    /// Draws every unit presentation through pooled unit views.
    /// </summary>
    /// <param name="units">The unit presentations.</param>
    private void RenderUnits(IReadOnlyList<TacticalUnitRenderData> units)
    {
        while (_unitViews.Count < units.Count)
        {
            TacticalUnitView view = Instantiate(unitTemplate, unitLayer);
            view.name = $"Unit{_unitViews.Count}";
            _unitViews.Add(view);
        }

        for (int index = 0; index < _unitViews.Count; index++)
        {
            if (index < units.Count)
                _unitViews[index].Render(units[index]);
            else
                _unitViews[index].gameObject.SetActive(false);
        }
    }

    /// <summary>
    /// Draws every shot presentation through pooled line images.
    /// </summary>
    /// <param name="shots">The shot presentations.</param>
    private void RenderShots(IReadOnlyList<TacticalShotRenderData> shots)
    {
        while (_shotImages.Count < shots.Count)
        {
            RawImage shot = Instantiate(shotTemplate, shotLayer);
            shot.name = $"Shot{_shotImages.Count}";
            _shotImages.Add(shot);
        }

        for (int index = 0; index < _shotImages.Count; index++)
        {
            RawImage shot = _shotImages[index];
            if (index >= shots.Count)
            {
                shot.gameObject.SetActive(false);
                continue;
            }

            TacticalShotRenderData data = shots[index];
            RectTransform rect = shot.rectTransform;
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = new Vector2(data.X, -data.Y);
            rect.sizeDelta = new Vector2(Mathf.Max(data.Length, 1f), Mathf.Max(data.Thickness, 1f));
            rect.localEulerAngles = new Vector3(0f, 0f, data.RotationDegrees);
            shot.color = data.Color;
            shot.gameObject.SetActive(true);
        }
    }

    /// <summary>
    /// Draws or hides the drag-selection rectangle.
    /// </summary>
    /// <param name="selectionBox">The source-space selection bounds, when dragging.</param>
    private void RenderSelectionBox(RectInt? selectionBox)
    {
        if (selectionBox == null)
        {
            selectionBoxImage.gameObject.SetActive(false);
            return;
        }

        RectInt bounds = selectionBox.Value;
        UILayout.SetSourceRect(
            selectionBoxImage.rectTransform,
            bounds.x,
            bounds.y,
            Mathf.Max(bounds.width, 1),
            Mathf.Max(bounds.height, 1)
        );
        selectionBoxImage.gameObject.SetActive(true);
    }

    /// <summary>
    /// Binds the panel buttons to semantic requests once.
    /// </summary>
    private void BindControls()
    {
        if (_bound)
            return;

        _bound = true;
        Bind(retreatButton, () => RetreatRequested?.Invoke());
        Bind(autoButton, () => AutoToggleRequested?.Invoke());
        Bind(pauseButton, () => PauseToggleRequested?.Invoke());
        Bind(speedButton, () => SpeedCycleRequested?.Invoke());
        Bind(targetPriorityButton, () => TargetPriorityToggleRequested?.Invoke());
        Bind(rosterSideButton, () => RosterSidePressed?.Invoke());
        Bind(rosterClassButton, () => RosterClassPressed?.Invoke());
    }

    /// <summary>
    /// Binds one button to a request and the shared control-press notification.
    /// </summary>
    /// <param name="button">The button to bind.</param>
    /// <param name="request">The request raised when the button is clicked.</param>
    private void Bind(Button button, Action request)
    {
        UnityAction listener = () =>
        {
            ControlPressed?.Invoke();
            request();
        };
        button.onClick.AddListener(listener);
        _buttonListeners.Add((button, listener));
    }

    /// <summary>
    /// Verifies the authored battle layer references.
    /// </summary>
    private void VerifyReferences()
    {
        Require(backgroundImage, nameof(backgroundImage));
        Require(arenaLayer, nameof(arenaLayer));
        Require(starfieldImage, nameof(starfieldImage));
        Require(planetImage, nameof(planetImage));
        Require(sceneImage, nameof(sceneImage));
        Require(sceneView, nameof(sceneView));
        Require(unitLayer, nameof(unitLayer));
        Require(shotLayer, nameof(shotLayer));
        Require(unitTemplate, nameof(unitTemplate));
        Require(shotTemplate, nameof(shotTemplate));
        Require(selectionBoxImage, nameof(selectionBoxImage));
        Require(titleTextField, nameof(titleTextField));
        Require(rosterLayer, nameof(rosterLayer));
        Require(rosterSideButton, nameof(rosterSideButton));
        Require(rosterSideLabel, nameof(rosterSideLabel));
        Require(rosterClassButton, nameof(rosterClassButton));
        Require(rosterClassLabel, nameof(rosterClassLabel));
        Require(rosterSlotTemplate, nameof(rosterSlotTemplate));
        Require(portraitImage, nameof(portraitImage));
        Require(nameTextField, nameof(nameTextField));
        Require(subtitleTextField, nameof(subtitleTextField));
        Require(shieldBarBackgroundImage, nameof(shieldBarBackgroundImage));
        Require(shieldBarImage, nameof(shieldBarImage));
        Require(shieldTextField, nameof(shieldTextField));
        Require(hullBarBackgroundImage, nameof(hullBarBackgroundImage));
        Require(hullBarImage, nameof(hullBarImage));
        Require(hullTextField, nameof(hullTextField));
        RequireAll(systemImages, nameof(systemImages));
        RequireAll(systemLabels, nameof(systemLabels));
        RequireAll(arcBarBackgroundImages, nameof(arcBarBackgroundImages));
        RequireAll(arcBarImages, nameof(arcBarImages));
        RequireAll(personnelImages, nameof(personnelImages));
        Require(personnelTextField, nameof(personnelTextField));
        Require(ordersTextField, nameof(ordersTextField));
        Require(tacticsTextField, nameof(tacticsTextField));
        Require(targetPriorityButton, nameof(targetPriorityButton));
        Require(targetPriorityLabel, nameof(targetPriorityLabel));
        Require(attackerStatusTextField, nameof(attackerStatusTextField));
        Require(defenderStatusTextField, nameof(defenderStatusTextField));
        Require(statusTextField, nameof(statusTextField));
        Require(selectionTextField, nameof(selectionTextField));
        Require(retreatButton, nameof(retreatButton));
        Require(retreatLabel, nameof(retreatLabel));
        Require(autoButton, nameof(autoButton));
        Require(autoLabel, nameof(autoLabel));
        Require(pauseButton, nameof(pauseButton));
        Require(pauseLabel, nameof(pauseLabel));
        Require(speedButton, nameof(speedButton));
        Require(speedLabel, nameof(speedLabel));
    }

    /// <summary>
    /// Throws when one authored reference is missing.
    /// </summary>
    /// <param name="reference">The authored reference.</param>
    /// <param name="fieldName">The serialized field name.</param>
    private void Require(UnityEngine.Object reference, string fieldName)
    {
        if (reference == null)
            throw new MissingReferenceException($"{name}/{fieldName} is missing.");
    }

    /// <summary>
    /// Throws when an authored reference array is empty or contains a missing entry.
    /// </summary>
    /// <typeparam name="T">The referenced component type.</typeparam>
    /// <param name="references">The authored references.</param>
    /// <param name="fieldName">The serialized field name.</param>
    private void RequireAll<T>(T[] references, string fieldName)
        where T : UnityEngine.Object
    {
        if (references == null || references.Length == 0)
            throw new MissingReferenceException($"{name}/{fieldName} is missing.");
        foreach (T reference in references)
            Require(reference, fieldName);
    }
}
