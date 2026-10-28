using System;
using System.Collections.Generic;
using Rebellion.Game.Combat;
using Rebellion.Game.Units;
using Rebellion.SceneGraph;
using Rebellion.Tactical;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

/// <summary>
/// Identifies the contextual tactical pointer shown over a world-space target.
/// </summary>
public enum TacticalPointerMode
{
    None,
    Dock,
}

/// <summary>
/// Owns the fixed bottom dock, pooled roster cards, and projected selection brackets.
/// </summary>
public sealed class TacticalSelectionView : MonoBehaviour
{
    private const float _officerMarkerSize = 40f;
    private const float _officerMarkerSpacing = 2f;
    private const float _officerMarkerHullGap = 6f;

    [SerializeField]
    private RectTransform selectionFrame;

    [SerializeField]
    private RectTransform attackTargetFrame;

    [SerializeField]
    private RectTransform hoverFrame;

    [SerializeField]
    private RectTransform hoverHullFill;

    [SerializeField]
    private RectTransform hoverShieldFill;

    [SerializeField]
    private RectTransform marquee;

    [SerializeField]
    private TacticalUnitCardView cardTemplate;

    [SerializeField]
    private RectTransform rosterContent;

    [SerializeField]
    private ScrollRect rosterScroll;

    [SerializeField]
    private TMP_Text rosterTitle;

    [SerializeField]
    private TacticalRadarView radar;

    [SerializeField]
    private TacticalHudGraphic[] housings;

    [SerializeField]
    private TacticalHudGraphic[] keys;

    [SerializeField]
    private TacticalCommandButton moveButton;

    [SerializeField]
    private TacticalCommandButton attackButton;

    [SerializeField]
    private TacticalCommandButton stopButton;

    [SerializeField]
    private TacticalCommandButton launchButton;

    [SerializeField]
    private TacticalCommandButton recallButton;

    [SerializeField]
    private TacticalCommandButton dockButton;

    [SerializeField]
    private TacticalCommandButton followButton;

    [SerializeField]
    private TMP_Text followLabel;

    [SerializeField]
    private Button[] controlGroupButtons;

    [SerializeField]
    private TMP_Text[] controlGroupLabels;

    [SerializeField]
    private TacticalHudGraphic contextualCursor;

    [SerializeField]
    private RectTransform officerMarkerTemplate;

    private readonly List<RectTransform> _frames = new List<RectTransform>();
    private readonly List<RectTransform> _frameHullFills = new List<RectTransform>();
    private readonly List<RectTransform> _frameShieldFills = new List<RectTransform>();
    private readonly List<TacticalUnitCardView> _cards = new List<TacticalUnitCardView>();
    private readonly List<SpaceCombatUnit> _singleSelection = new List<SpaceCombatUnit>(1);
    private readonly Dictionary<SpaceCombatUnit, Transform> _singlePresentation =
        new Dictionary<SpaceCombatUnit, Transform>();
    private IContentAssetSource _assets;
    private readonly List<SpaceCombatUnit> _selectionSnapshot = new List<SpaceCombatUnit>();
    private readonly List<List<SpaceCombatUnit>> _groups = new List<List<SpaceCombatUnit>>();
    private readonly List<SpaceCombatUnit> _roster = new List<SpaceCombatUnit>();
    private readonly HashSet<SpaceCombatUnit> _rosterSeen = new HashSet<SpaceCombatUnit>();
    private TacticalCarrierOperations _carrierOperations;
    private string _orderingFaction;
    private int _flightRevision = -1;
    private UnityAction[] _controlGroupActions;
    private bool _ownsCursorVisibility;
    private bool _previousCursorVisibility;
    private IReadOnlyDictionary<SpaceCombatUnit, Transform> _battlePresentations;
    private Camera _battleCamera;
    private string _viewerFaction;
    private readonly List<OfficerMarker> _officerMarkers = new List<OfficerMarker>();
    private TacticalPointerMode _pointerMode;
    private readonly List<RectTransform> _targetFrames = new List<RectTransform>();

    public TacticalCommandButton MoveButton => moveButton;
    public TacticalCommandButton AttackButton => attackButton;
    public TacticalCommandButton StopButton => stopButton;
    public TacticalCommandButton LaunchButton => launchButton;
    public TacticalCommandButton RecallButton => recallButton;
    public TacticalCommandButton DockButton => dockButton;
    public TacticalCommandButton FollowButton => followButton;

    public event Action<IReadOnlyList<SpaceCombatUnit>> CardClicked;
    public event Action<IReadOnlyList<SpaceCombatUnit>> CardDoubleClicked;
    public event Action<int> ControlGroupClicked;
    public event Action<SpaceCombatUnit> OfficerMarkerClicked;

    /// <summary>
    /// Connects the authored control-group buttons to the shared selection owner.
    /// </summary>
    private void Awake()
    {
        if (
            controlGroupButtons == null
            || controlGroupLabels == null
            || controlGroupButtons.Length != TacticalSelectionController.ControlGroupCount
            || controlGroupLabels.Length != controlGroupButtons.Length
        )
            throw new MissingReferenceException($"{name} has incomplete tactical control groups.");

        _controlGroupActions = new UnityAction[controlGroupButtons.Length];
        for (int index = 0; index < controlGroupButtons.Length; index++)
        {
            int groupIndex = index;
            _controlGroupActions[index] = () => ControlGroupClicked?.Invoke(groupIndex);
            controlGroupButtons[index].onClick.AddListener(_controlGroupActions[index]);
        }
    }

    /// <summary>
    /// Renders persistent group slots above the selected-unit roster.
    /// </summary>
    /// <param name="memberCounts">The current live member count in each group.</param>
    /// <param name="activeGroup">The selected group index, or negative when selection is independent.</param>
    public void RenderControlGroups(IReadOnlyList<int> memberCounts, int activeGroup)
    {
        if (memberCounts == null || memberCounts.Count != controlGroupButtons.Length)
            throw new ArgumentException(
                "Control-group counts must match the authored slots.",
                nameof(memberCounts)
            );
        for (int index = 0; index < controlGroupButtons.Length; index++)
        {
            bool populated = memberCounts[index] > 0;
            controlGroupButtons[index].interactable = true;
            controlGroupLabels[index].color =
                index == activeGroup ? new Color(0.3f, 0.86f, 1f)
                : populated ? Color.white
                : new Color(0.38f, 0.43f, 0.5f);
        }
    }

    /// <summary>
    /// Replaces the platform pointer with the applicable tactical world-target symbol.
    /// </summary>
    /// <param name="screenPosition">The pointer position in screen pixels.</param>
    /// <param name="mode">The available contextual action.</param>
    public void RenderContextualCursor(Vector2 screenPosition, TacticalPointerMode mode)
    {
        if (!contextualCursor)
            return;
        bool visible = mode != TacticalPointerMode.None;
        if (contextualCursor.gameObject.activeSelf != visible)
            contextualCursor.gameObject.SetActive(visible);
        if (!visible)
        {
            _pointerMode = TacticalPointerMode.None;
            RestoreCursorVisibility();
            return;
        }
        if (_pointerMode != mode)
        {
            contextualCursor.SetArtwork(TacticalHudGraphic.Artwork.Dock);
            contextualCursor.color = new Color(0.35f, 1f, 0.62f);
            _pointerMode = mode;
        }
        RectTransform cursorRect = contextualCursor.rectTransform;
        RectTransform parent = (RectTransform)cursorRect.parent;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            parent,
            screenPosition,
            null,
            out Vector2 localPosition
        );
        cursorRect.anchoredPosition = localPosition;
        if (_ownsCursorVisibility)
            return;
        _previousCursorVisibility = Cursor.visible;
        Cursor.visible = false;
        _ownsCursorVisibility = true;
    }

    /// <summary>
    /// Refreshes the independent camera-follow control without changing the command grid.
    /// </summary>
    /// <param name="available">Whether the current primary selection can be followed.</param>
    /// <param name="following">Whether the camera is already tracking a unit.</param>
    public void RenderFollowState(bool available, bool following)
    {
        followButton.interactable = available || following;
        string text = following ? "UNFOLLOW" : "FOLLOW SELECTED";
        if (followLabel.text != text)
            followLabel.text = text;
    }

    /// <summary>
    /// Binds carrier manifests and command availability to the same battle operations as the scene.
    /// </summary>
    /// <param name="operations">The battle's indexed carrier operations, or null when unbound.</param>
    /// <param name="owner">The viewing player faction.</param>
    public void BindCarrierOperations(TacticalCarrierOperations operations, string owner)
    {
        _carrierOperations = operations;
        _orderingFaction = owner;
        _flightRevision = int.MinValue;
    }

    /// <summary>
    /// Displays drag-selection feedback without intercepting world input.
    /// </summary>
    /// <param name="start">The first pointer position.</param>
    /// <param name="end">The current pointer position.</param>
    /// <param name="visible">Whether a drag-selection gesture is active.</param>
    public void RenderMarquee(Vector2 start, Vector2 end, bool visible)
    {
        marquee.gameObject.SetActive(visible);
        if (!visible)
            return;
        RectTransform parent = (RectTransform)marquee.parent;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(parent, start, null, out Vector2 a);
        RectTransformUtility.ScreenPointToLocalPointInRectangle(parent, end, null, out Vector2 b);
        marquee.anchoredPosition = (a + b) * 0.5f;
        marquee.sizeDelta = new Vector2(Mathf.Abs(a.x - b.x), Mathf.Abs(a.y - b.y));
    }

    /// <summary>
    /// Projects ordered attack targets and the hostile hull currently under the pointer.
    /// </summary>
    /// <param name="targets">The distinct targets assigned to selected friendly ships.</param>
    /// <param name="hovered">The hostile hull under the pointer, or null.</param>
    /// <param name="presentations">The loaded presentation index.</param>
    /// <param name="camera">The battle camera.</param>
    public void RenderContacts(
        IReadOnlyList<SpaceCombatUnit> targets,
        SpaceCombatUnit hovered,
        IReadOnlyDictionary<SpaceCombatUnit, Transform> presentations,
        Camera camera
    )
    {
        int count = targets?.Count ?? 0;
        while (_targetFrames.Count < count)
        {
            RectTransform frame =
                _targetFrames.Count == 0
                    ? attackTargetFrame
                    : Instantiate(attackTargetFrame, attackTargetFrame.parent);
            frame.name =
                _targetFrames.Count == 0
                    ? "AttackTargetFrame"
                    : $"AttackTargetFrame{_targetFrames.Count}";
            _targetFrames.Add(frame);
        }
        for (int index = 0; index < _targetFrames.Count; index++)
            _targetFrames[index]
                .gameObject.SetActive(
                    index < count
                        && RenderContactFrame(
                            _targetFrames[index],
                            targets[index],
                            presentations,
                            camera
                        )
                );
        if (count == 0 && attackTargetFrame)
            attackTargetFrame.gameObject.SetActive(false);

        bool visible = RenderContactFrame(hoverFrame, hovered, presentations, camera);
        hoverFrame.gameObject.SetActive(visible);
        if (!visible)
            return;
        RenderDurability(hovered, hoverHullFill, hoverShieldFill);
    }

    /// <summary>
    /// Projects a living visible combatant into one screen-space feedback rectangle.
    /// </summary>
    /// <param name="frame">The projected feedback rectangle.</param>
    /// <param name="unit">The contact to present, or null.</param>
    /// <param name="presentations">The loaded presentation index.</param>
    /// <param name="camera">The battle camera.</param>
    /// <returns>Whether the contact can be shown on screen.</returns>
    private static bool RenderContactFrame(
        RectTransform frame,
        SpaceCombatUnit unit,
        IReadOnlyDictionary<SpaceCombatUnit, Transform> presentations,
        Camera camera
    ) =>
        frame
        && unit?.HasRetreated == false
        && TacticalCombat.IsAlive(unit)
        && camera
        && presentations != null
        && presentations.TryGetValue(unit, out Transform root)
        && root
        && root.gameObject.activeInHierarchy
        && RenderFrame(frame, root, unit.ModelSize, camera);

    /// <summary>
    /// Applies a faction's authored dock palette.
    /// </summary>
    /// <param name="theme">The faction's battle presentation theme.</param>
    /// <param name="assets">The content source resolving portraits.</param>
    public void ApplyTheme(BattleHudTheme theme, IContentAssetSource assets)
    {
        _assets = assets;
        if (theme == null)
            throw new ArgumentNullException(nameof(theme));
        Color housing = ThemeColorParser.Parse(theme.HousingColorHex, Color.black);
        Color key = ThemeColorParser.Parse(theme.KeyColorHex, Color.white);
        foreach (TacticalHudGraphic graphic in housings)
            graphic.color = housing;
        foreach (TacticalHudGraphic graphic in keys)
            graphic.color = key;
        Color contact = ThemeColorParser.Parse(theme.ContactColorHex, Color.white);
        radar.SetContactColor(contact);
        if (officerMarkerTemplate)
            officerMarkerTemplate.GetComponent<Image>().color = contact;
        foreach (TacticalUnitCardView card in _cards)
            if (card.Combatant != null)
                card.BindGroup(card.Combatants, GetPortrait(card.Combatant));
        RebuildOfficerMarkers();
    }

    /// <summary>
    /// Connects the radar to the same loaded battle and selection as the roster.
    /// </summary>
    /// <param name="presentations">The loaded model roots.</param>
    /// <param name="bounds">The map's playable volume.</param>
    /// <param name="camera">The battle camera.</param>
    /// <param name="viewer">The viewing faction identifier.</param>
    /// <param name="selected">The live selection collection.</param>
    public void BindBattle(
        IReadOnlyDictionary<SpaceCombatUnit, Transform> presentations,
        BattleMapBounds bounds,
        Camera camera,
        string viewer,
        IReadOnlyList<SpaceCombatUnit> selected
    )
    {
        _battlePresentations = presentations;
        _battleCamera = camera;
        _viewerFaction = viewer;
        radar.Bind(presentations, bounds, camera, viewer, selected);
        RebuildOfficerMarkers();
    }

    /// <summary>
    /// Renders one inspected unit through the same roster contract as multiselection.
    /// </summary>
    /// <param name="combatant">The selected unit, or null.</param>
    /// <param name="presentation">The model root, or null.</param>
    /// <param name="camera">The battle camera.</param>
    public void Render(SpaceCombatUnit combatant, Transform presentation, Camera camera)
    {
        _singleSelection.Clear();
        _singlePresentation.Clear();
        if (combatant != null && presentation != null && camera != null)
        {
            _singleSelection.Add(combatant);
            _singlePresentation.Add(combatant, presentation);
        }
        RenderSelection(_singleSelection, _singlePresentation, camera);
    }

    /// <summary>
    /// Refreshes selected cards and projected brackets without resizing the roster well.
    /// </summary>
    /// <param name="selected">The current ordered selection.</param>
    /// <param name="presentations">The loaded presentation index.</param>
    /// <param name="camera">The battle camera.</param>
    public void RenderSelection(
        IReadOnlyList<SpaceCombatUnit> selected,
        IReadOnlyDictionary<SpaceCombatUnit, Transform> presentations,
        Camera camera
    )
    {
        int count = selected?.Count ?? 0;
        bool changed =
            _selectionSnapshot.Count != count
            || _flightRevision != (_carrierOperations?.Revision ?? -1);
        for (int i = 0; !changed && i < count; i++)
            changed = _selectionSnapshot[i] != selected[i];
        if (changed)
        {
            _flightRevision = _carrierOperations?.Revision ?? -1;
            RebuildGroups(selected);
            bool launch =
                _carrierOperations?.CanLaunch(
                    selected ?? Array.Empty<SpaceCombatUnit>(),
                    _orderingFaction
                ) == true;
            bool recall =
                _carrierOperations?.CanRecall(
                    selected ?? Array.Empty<SpaceCombatUnit>(),
                    _orderingFaction
                ) == true;
            if (launchButton)
                launchButton.interactable = launch;
            if (recallButton)
                recallButton.interactable =
                    recall
                    && _selectionSnapshot.Exists(unit =>
                        unit is { Kind: SpaceCombatUnitKind.CapitalShip }
                    );
            if (dockButton)
                dockButton.interactable =
                    recall
                    && _selectionSnapshot.Exists(unit =>
                        unit is { Kind: SpaceCombatUnitKind.Starfighter }
                    );
        }
        while (_cards.Count < _groups.Count)
        {
            TacticalUnitCardView card = Instantiate(cardTemplate, rosterContent);
            card.name = $"UnitCard{_cards.Count}";
            card.Clicked += HandleCardClicked;
            card.DoubleClicked += HandleCardDoubleClicked;
            _cards.Add(card);
        }
        while (_frames.Count < count)
        {
            RectTransform frame =
                _frames.Count == 0
                    ? selectionFrame
                    : Instantiate(selectionFrame, selectionFrame.parent);
            frame.name = _frames.Count == 0 ? "SelectionFrame" : $"SelectionFrame{_frames.Count}";
            _frames.Add(frame);
            _frameShieldFills.Add((RectTransform)frame.Find("ShieldBar/Fill"));
            _frameHullFills.Add((RectTransform)frame.Find("HullBar/Fill"));
        }
        for (int i = 0; i < _cards.Count; i++)
        {
            bool visible = i < _groups.Count;
            _cards[i].gameObject.SetActive(visible);
            if (!visible)
            {
                _cards[i].Unbind();
                continue;
            }
            if (changed)
                _cards[i].BindGroup(_groups[i], GetPortrait(_groups[i][0]));
            _cards[i].Refresh();
        }
        for (int i = 0; i < _frames.Count; i++)
        {
            bool frameVisible =
                i < count
                && camera
                && presentations != null
                && presentations.TryGetValue(selected[i], out Transform root)
                && root
                && root.gameObject.activeInHierarchy
                && RenderFrame(_frames[i], root, selected[i].ModelSize, camera);
            _frames[i].gameObject.SetActive(frameVisible);
            if (frameVisible)
                RenderDurability(selected[i], _frameHullFills[i], _frameShieldFills[i]);
        }
        if (changed)
        {
            GridLayoutGroup layout = rosterContent.GetComponent<GridLayoutGroup>();
            int columns = (_groups.Count + layout.constraintCount - 1) / layout.constraintCount;
            float width = Mathf.Max(
                0f,
                columns * (layout.cellSize.x + layout.spacing.x) - layout.spacing.x
            );
            rosterContent.sizeDelta = new Vector2(width, 0f);
            rosterScroll.horizontalNormalizedPosition = 0f;
            rosterTitle.text =
                _roster.Count > count
                    ? $"SELECTED / CARRIED  //  {_roster.Count:000}"
                    : $"SELECTED UNITS  //  {count:000}";
        }
        if (count == 0)
            selectionFrame.gameObject.SetActive(false);
        RenderOfficerMarkers();
    }

    /// <summary>
    /// Refreshes the compact durability bars attached to a selected friendly hull.
    /// </summary>
    /// <param name="combatant">The selected battle unit.</param>
    /// <param name="hullFill">The hull bar fill.</param>
    /// <param name="shieldFill">The shield bar fill.</param>
    private static void RenderDurability(
        SpaceCombatUnit combatant,
        RectTransform hullFill,
        RectTransform shieldFill
    )
    {
        SpaceCombatUnit unit = combatant;
        int hull = combatant.Condition.CurrentHullStrength;
        int maximumHull = unit.MaxHullStrength;
        int maximumShields = unit.MaxShieldStrength;
        hullFill.anchorMax = new Vector2(
            maximumHull > 0 ? Mathf.Clamp01((float)hull / maximumHull) : 0f,
            1f
        );
        shieldFill.anchorMax = new Vector2(
            maximumShields > 0 && combatant.Condition.CurrentShieldStrength.HasValue
                ? Mathf.Clamp01(
                    (float)combatant.Condition.CurrentShieldStrength.Value / maximumShields
                )
                : 0f,
            1f
        );
    }

    /// <summary>
    /// Groups only a changed selection by faction and unit definition while retaining every
    /// represented combatant for group commands.
    /// </summary>
    /// <param name="selected">The selection owner's ordered battle units.</param>
    private void RebuildGroups(IReadOnlyList<SpaceCombatUnit> selected)
    {
        _selectionSnapshot.Clear();
        _groups.Clear();
        _roster.Clear();
        _rosterSeen.Clear();
        Dictionary<(string Owner, string Type), List<SpaceCombatUnit>> index =
            new Dictionary<(string, string), List<SpaceCombatUnit>>();
        if (selected == null)
            return;
        _selectionSnapshot.AddRange(selected);
        List<SpaceCombatUnit> ordered = new List<SpaceCombatUnit>(selected);
        ordered.Sort(CompareRosterUnits);
        int visibleCapacity = GetVisibleRosterCapacity();
        foreach (SpaceCombatUnit combatant in ordered)
        {
            if (_rosterSeen.Add(combatant))
                _roster.Add(combatant);
            if (_carrierOperations != null && combatant.OwnerInstanceID == _orderingFaction)
                foreach (SpaceCombatUnit carried in _carrierOperations.GetManifest(combatant))
                    if (
                        _roster.Count < visibleCapacity
                        && !carried.HasRetreated
                        && (
                            carried.Flight.Phase == FighterFlightState.Docked
                            || carried.Flight.Phase == FighterFlightState.Queued
                            || (
                                carried.Flight.Phase == FighterFlightState.Launching
                                && carried.Flight.FlightDelay > 0f
                            )
                        )
                        && _rosterSeen.Add(carried)
                    )
                        _roster.Add(carried);
        }
        _roster.Sort(CompareRosterUnits);
        foreach (SpaceCombatUnit combatant in _roster)
        {
            SpaceCombatUnit unit = combatant;
            // Missing type identifiers cannot establish that two units share a definition.
            if (string.IsNullOrWhiteSpace(unit.TypeID))
            {
                _groups.Add(new List<SpaceCombatUnit> { combatant });
                continue;
            }
            var key = (unit.OwnerInstanceID, unit.TypeID);
            if (!index.TryGetValue(key, out List<SpaceCombatUnit> group))
            {
                group = new List<SpaceCombatUnit>();
                index.Add(key, group);
                _groups.Add(group);
            }
            group.Add(combatant);
        }
    }

    /// <summary>
    /// Orders deployed starfighters before capital ships while keeping each category deterministic.
    /// </summary>
    /// <param name="left">The first combatant.</param>
    /// <param name="right">The second combatant.</param>
    /// <returns>A stable roster sort comparison.</returns>
    private static int CompareRosterUnits(SpaceCombatUnit left, SpaceCombatUnit right)
    {
        int kind = (left is { Kind: SpaceCombatUnitKind.CapitalShip } ? 1 : 0).CompareTo(
            right is { Kind: SpaceCombatUnitKind.CapitalShip } ? 1 : 0
        );
        if (kind != 0)
            return kind;
        int name = string.Compare(
            left.DisplayName ?? left.TypeID,
            right.DisplayName ?? right.TypeID,
            StringComparison.Ordinal
        );
        return name != 0
            ? name
            : string.Compare(
                left.SourceUnitInstanceID,
                right.SourceUnitInstanceID,
                StringComparison.Ordinal
            );
    }

    /// <summary>
    /// Calculates the number of complete two-row cards visible without horizontal scrolling.
    /// </summary>
    /// <returns>The visible card capacity, with at least one slot.</returns>
    private int GetVisibleRosterCapacity()
    {
        GridLayoutGroup layout = rosterContent.GetComponent<GridLayoutGroup>();
        float width = rosterScroll.viewport.rect.width;
        int columns = Mathf.Max(
            1,
            Mathf.FloorToInt((width + layout.spacing.x) / (layout.cellSize.x + layout.spacing.x))
        );
        return columns * layout.constraintCount;
    }

    /// <summary>
    /// Resolves a portrait only when the associated card binding changes.
    /// </summary>
    /// <param name="combatant">The represented unit.</param>
    /// <returns>The configured display portrait, or null when none exists.</returns>
    private Texture2D GetPortrait(SpaceCombatUnit combatant)
    {
        string path = combatant.GetDisplayImagePath();
        return _assets != null && !string.IsNullOrWhiteSpace(path)
            ? _assets.GetTexture(path)
            : null;
    }

    /// <summary>
    /// Forwards an authored roster button to the selection controller.
    /// </summary>
    /// <param name="combatants">The clicked card's members.</param>
    private void HandleCardClicked(IReadOnlyList<SpaceCombatUnit> combatants) =>
        CardClicked?.Invoke(combatants);

    /// <summary>
    /// Forwards a roster double click to the tactical camera owner.
    /// </summary>
    /// <param name="combatants">The double-clicked card's members.</param>
    private void HandleCardDoubleClicked(IReadOnlyList<SpaceCombatUnit> combatants) =>
        CardDoubleClicked?.Invoke(combatants);

    /// <summary>
    /// Fits white corner brackets around a visible projected hull.
    /// </summary>
    /// <param name="frame">The pooled bracket rectangle.</param>
    /// <param name="presentation">The centered model root.</param>
    /// <param name="size">The measured local model dimensions.</param>
    /// <param name="camera">The battle camera.</param>
    /// <returns>Whether the projected envelope is on screen.</returns>
    private static bool RenderFrame(
        RectTransform frame,
        Transform presentation,
        ModelDimensions size,
        Camera camera
    )
    {
        if (
            !TryGetProjectedHull(
                presentation,
                size,
                camera,
                out Vector2 minimum,
                out Vector2 maximum
            )
        )
            return false;
        RectTransform parent = (RectTransform)frame.parent;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            parent,
            minimum,
            null,
            out Vector2 localMinimum
        );
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            parent,
            maximum,
            null,
            out Vector2 localMaximum
        );
        frame.anchoredPosition = (localMinimum + localMaximum) * 0.5f;
        frame.sizeDelta = Vector2.Max(
            localMaximum - localMinimum + Vector2.one * 12f,
            Vector2.one * 24f
        );
        return true;
    }

    /// <summary>
    /// Projects a measured hull into a clipped screen-space envelope.
    /// </summary>
    /// <param name="presentation">The centered model root.</param>
    /// <param name="size">The measured local model dimensions.</param>
    /// <param name="camera">The battle camera.</param>
    /// <param name="minimum">Receives the lower screen-space corner.</param>
    /// <param name="maximum">Receives the upper screen-space corner.</param>
    /// <returns>Whether any part of the complete hull envelope is visible.</returns>
    private static bool TryGetProjectedHull(
        Transform presentation,
        ModelDimensions size,
        Camera camera,
        out Vector2 minimum,
        out Vector2 maximum
    )
    {
        minimum = new Vector2(float.PositiveInfinity, float.PositiveInfinity);
        maximum = new Vector2(float.NegativeInfinity, float.NegativeInfinity);
        for (int corner = 0; corner < 8; corner++)
        {
            Vector3 local = new Vector3(
                (corner & 1) == 0 ? -size.Width * 0.5f : size.Width * 0.5f,
                (corner & 2) == 0 ? -size.Height * 0.5f : size.Height * 0.5f,
                (corner & 4) == 0 ? -size.Depth * 0.5f : size.Depth * 0.5f
            );
            Vector3 screen = camera.WorldToScreenPoint(presentation.TransformPoint(local));
            if (screen.z < camera.nearClipPlane || screen.z > camera.farClipPlane)
                return false;
            minimum = Vector2.Min(minimum, screen);
            maximum = Vector2.Max(maximum, screen);
        }
        Rect viewport = camera.pixelRect;
        if (
            maximum.x < viewport.xMin
            || minimum.x > viewport.xMax
            || maximum.y < viewport.yMin
            || minimum.y > viewport.yMax
        )
            return false;
        minimum = Vector2.Max(minimum, viewport.min);
        maximum = Vector2.Min(maximum, viewport.max);
        return true;
    }

    /// <summary>
    /// Rebuilds player-visible officer portraits from the battle copies already attached to ships.
    /// </summary>
    private void RebuildOfficerMarkers()
    {
        foreach (OfficerMarker marker in _officerMarkers)
            if (marker.Root)
            {
                if (Application.isPlaying)
                    Destroy(marker.Root.gameObject);
                else
                    DestroyImmediate(marker.Root.gameObject);
            }
        _officerMarkers.Clear();
        if (
            _assets == null
            || _battlePresentations == null
            || !_battleCamera
            || string.IsNullOrWhiteSpace(_viewerFaction)
            || !officerMarkerTemplate
        )
            return;

        foreach (KeyValuePair<SpaceCombatUnit, Transform> presentation in _battlePresentations)
        {
            if (
                presentation.Key is not { Kind: SpaceCombatUnitKind.CapitalShip } ship
                || !string.Equals(ship.OwnerInstanceID, _viewerFaction, StringComparison.Ordinal)
            )
                continue;
            IReadOnlyList<Officer> officers = presentation.Key.Officers;
            for (int index = 0; index < officers.Count; index++)
            {
                string path = string.IsNullOrWhiteSpace(officers[index].SmallDisplayImagePath)
                    ? officers[index].GetDisplayImagePath()
                    : officers[index].SmallDisplayImagePath;
                Texture2D texture = string.IsNullOrWhiteSpace(path)
                    ? null
                    : _assets.GetTexture(path);
                if (!texture)
                    continue;
                RectTransform root = Instantiate(
                    officerMarkerTemplate,
                    officerMarkerTemplate.parent
                );
                RawImage image = root.Find("PortraitViewport/PortraitImage")
                    .GetComponent<RawImage>();
                root.name =
                    $"OfficerMarker {officers[index].InstanceID ?? officers[index].DisplayName}";
                image.texture = texture;
                image.uvRect = GetCenteredSquareUv(texture);
                SpaceCombatUnit markedCombatant = presentation.Key;
                Image frame = root.GetComponent<Image>();
                frame.raycastTarget = true;
                Button button =
                    root.GetComponent<Button>() ?? root.gameObject.AddComponent<Button>();
                button.targetGraphic = frame;
                button.navigation = new Navigation { mode = Navigation.Mode.None };
                button.onClick.AddListener(() => OfficerMarkerClicked?.Invoke(markedCombatant));
                root.gameObject.SetActive(true);
                _officerMarkers.Add(
                    new OfficerMarker(
                        presentation.Key,
                        presentation.Value,
                        root,
                        index,
                        officers.Count
                    )
                );
            }
        }
    }

    /// <summary>
    /// Keeps each officer portrait immediately above its visible capital-ship hull.
    /// </summary>
    private void RenderOfficerMarkers()
    {
        foreach (OfficerMarker marker in _officerMarkers)
        {
            Vector2 minimum = default;
            Vector2 maximum = default;
            bool visible =
                marker.Presentation
                && marker.Presentation.gameObject.activeInHierarchy
                && !marker.Combatant.HasRetreated
                && TryGetProjectedHull(
                    marker.Presentation,
                    marker.Combatant.ModelSize,
                    _battleCamera,
                    out minimum,
                    out maximum
                );
            marker.Root.gameObject.SetActive(visible);
            if (!visible)
                continue;
            float markerStep = _officerMarkerSize + _officerMarkerSpacing;
            float rowWidth = marker.Count * markerStep - _officerMarkerSpacing;
            Vector2 screenPosition = new Vector2(
                (minimum.x + maximum.x - rowWidth) * 0.5f
                    + marker.Index * markerStep
                    + _officerMarkerSize * 0.5f,
                maximum.y + _officerMarkerHullGap + _officerMarkerSize * 0.5f
            );
            RectTransform parent = (RectTransform)marker.Root.parent;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                parent,
                screenPosition,
                null,
                out Vector2 localPosition
            );
            marker.Root.anchoredPosition = localPosition;
        }
    }

    /// <summary>
    /// Crops rectangular officer artwork to a centered square before rendering it inside the
    /// square portrait frame.
    /// </summary>
    /// <param name="texture">The source portrait texture.</param>
    /// <returns>The centered square UV rectangle.</returns>
    private static Rect GetCenteredSquareUv(Texture texture)
    {
        if (texture == null || texture.width <= 0 || texture.height <= 0)
            return new Rect(0f, 0f, 1f, 1f);

        if (texture.width > texture.height)
        {
            float width = (float)texture.height / texture.width;
            return new Rect((1f - width) / 2f, 0f, width, 1f);
        }

        float height = (float)texture.width / texture.height;
        return new Rect(0f, (1f - height) / 2f, 1f, height);
    }

    /// <summary>
    /// Restores platform cursor visibility after targeted carrier recovery ends.
    /// </summary>
    private void RestoreCursorVisibility()
    {
        if (!_ownsCursorVisibility)
            return;
        Cursor.visible = _previousCursorVisibility;
        _ownsCursorVisibility = false;
    }

    /// <summary>
    /// Releases transient cursor ownership when the tactical overlay is hidden.
    /// </summary>
    private void OnDisable()
    {
        if (contextualCursor)
            contextualCursor.gameObject.SetActive(false);
        RestoreCursorVisibility();
    }

    /// <summary>
    /// Releases authored group-button callbacks and cursor ownership with the tactical overlay.
    /// </summary>
    private void OnDestroy()
    {
        if (_controlGroupActions != null)
            for (int index = 0; index < _controlGroupActions.Length; index++)
                if (controlGroupButtons[index])
                    controlGroupButtons[index].onClick.RemoveListener(_controlGroupActions[index]);
        RestoreCursorVisibility();
    }

    private sealed class OfficerMarker
    {
        public readonly SpaceCombatUnit Combatant;
        public readonly Transform Presentation;
        public readonly RectTransform Root;
        public readonly int Index;
        public readonly int Count;

        /// <summary>
        /// Captures one portrait's stable ship association and row position.
        /// </summary>
        /// <param name="combatant">The ship carrying the represented officer.</param>
        /// <param name="presentation">The ship's moving presentation root.</param>
        /// <param name="root">The complete framed portrait root.</param>
        /// <param name="index">The portrait's zero-based row index.</param>
        /// <param name="count">The total portraits displayed for this ship.</param>
        public OfficerMarker(
            SpaceCombatUnit combatant,
            Transform presentation,
            RectTransform root,
            int index,
            int count
        )
        {
            Combatant = combatant;
            Presentation = presentation;
            Root = root;
            Index = index;
            Count = count;
        }
    }
}
