using System;
using System.Collections.Generic;
using System.IO;
using Rebellion.Game.SpaceCombat;
using Rebellion.Gameplay;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// Identifies a player action exposed by the tactical battle HUD.
/// </summary>
public enum TacticalCombatControl
{
    Pause,
    AttackFighters,
    AttackCapitalShips,
    HoldFire,
    Stop,
    Recover,
    Withdraw,
    CloseAttack,
    RangedAttack,
    LineFormation,
    SurroundFormation,
    LeftHook,
    RightHook,
    Hammer,
    Anvil,
    AttackBattleStation,
    Superlaser,
    Group,
    Continue,
    NavigationRing1,
    NavigationRing2,
    NavigationRing3,
    NavigationRing4,
    SaveCamera,
    RestoreCamera,
    FocusCamera,
    ObjectGroup1,
    ObjectGroup2,
    ObjectGroup3,
    ObjectGroup4,
    ObjectGroup5,
    ObjectGroup6,
    ObjectGroup7,
    ObjectGroup8,
    FighterGroup1,
    FighterGroup2,
    FighterGroup3,
    FighterGroup4,
    ToggleAutomation,
    CameraZoomIn,
    CameraZoomOut,
    CameraRotateLeft,
    CameraRotateRight,
    CameraTiltUp,
    CameraTiltDown,
    OpenMenu,
    CloseMenu,
    ResolveImmediately,
    OpenOptions,
    OpenOrders,
    OpenTactics,
    ConfirmPanel,
    CancelPanel,
    PreviousObject,
    NextObject,
    HighlightAlliance,
    HighlightEmpire,
    ConfirmWithdrawal,
    CancelWithdrawal,
}

/// <summary>
/// Exposes authored tactical controls and viewport input without owning battle rules.
/// </summary>
[DisallowMultipleComponent]
public sealed class TacticalCombatView : MonoBehaviour, IPointerDownHandler, IScrollHandler
{
    [Serializable]
    public sealed class ControlBinding
    {
        public TacticalCombatControl Control;
        public TacticalCombatHudButton Button;
        public TacticalCombatHudPanel Panel;
        public string ArtworkKey;
        public bool BitmapHitTest;
        public TextMeshProUGUI Label;
        public string Tooltip;
        public RectInt SourceBounds;

        [NonSerialized]
        public Sprite Up;

        [NonSerialized]
        public Sprite Down;

        [NonSerialized]
        public Sprite Disabled;

        [NonSerialized]
        public bool Selected;
    }

    [Serializable]
    public sealed class PanelBinding
    {
        public TacticalCombatHudPanel Panel;
        public GameObject Root;
    }

    [SerializeField]
    private RectTransform _frame;

    [SerializeField]
    private RawImage _frameImage;

    [SerializeField]
    private RawImage _objectHeader;

    [SerializeField]
    private RawImage _fighterHeader;

    [SerializeField]
    private RawImage _panelImage;

    [SerializeField]
    private RawImage _diagram;

    [SerializeField]
    private RawImage _objectPreview;

    [SerializeField]
    private RawImage _fighterPreview;
    private TacticalCombatHudPanel _panel;

    [SerializeField]
    private TextMeshProUGUI _assignment;

    [SerializeField]
    private TextMeshProUGUI _orders;

    [SerializeField]
    private TextMeshProUGUI _tactics;

    [SerializeField]
    private TextMeshProUGUI _fighterCounts;

    [SerializeField]
    private RawImage[] _systemIcons;
    private string _singleTactics = string.Empty;
    private string _groupTactics = string.Empty;

    [SerializeField]
    private Image _hullBar;

    [SerializeField]
    private Image _shieldBar;

    [SerializeField]
    private RawImage _stationCharge;

    [SerializeField]
    private Image _stationChargeBar;

    [SerializeField]
    private PanelBinding[] _panels;
    private readonly Dictionary<string, string> _imagePaths = new(StringComparer.Ordinal);
    private ContentAssets _assets;
    private HoverTooltip _tooltip;

    [SerializeField]
    private RawImage _viewport;

    [SerializeField]
    private RawImage _background;

    [SerializeField]
    private TextMeshProUGUI _status;

    [SerializeField]
    private TextMeshProUGUI _selection;

    [SerializeField]
    private TacticalCombatRenderer _battleRenderer;

    [SerializeField]
    private ControlBinding[] _controls;
    public RectTransform Viewport => _viewport.rectTransform;
    public Vector2Int PixelSize =>
        new Vector2Int(
            Mathf.RoundToInt(Viewport.rect.width * _viewport.canvas.scaleFactor),
            Mathf.RoundToInt(Viewport.rect.height * _viewport.canvas.scaleFactor)
        );
    public TacticalCombatRenderer Renderer => _battleRenderer;
    public event Action<TacticalCombatControl> ControlRequested;
    public event Action<PointerEventData> PointerPressed;
    public event Action<float> ZoomRequested;

    /// <summary>
    /// Verifies authored references and subscribes each semantic control exactly once.
    /// </summary>
    private void Awake()
    {
        if (
            _viewport == null
            || _status == null
            || _selection == null
            || _battleRenderer == null
            || _controls == null
        )
            throw new MissingReferenceException(
                $"{name} is missing tactical presentation references."
            );
        _tooltip = new HoverTooltip(_frame, "Tactical tooltip", 200, true);
        string[] systemTooltips =
        {
            "Shield Recharge",
            "Weapon Recharge",
            "Tractor Beam Power",
            "Engines",
            "Hyperspace Engines",
        };
        for (int index = 0; index < _systemIcons.Length; index++)
            _systemIcons[index]
                .GetComponent<HoverTooltipRegion>()
                .Configure(
                    _tooltip,
                    systemTooltips[index],
                    new RectInt(492 + index * 27, 131, 22, 15)
                );
        foreach (ControlBinding binding in _controls)
        {
            if (binding.Button == null)
                throw new MissingReferenceException($"{name} has an unbound tactical control.");
            TacticalCombatControl control = binding.Control;
            binding.Button.onClick.AddListener(() => ControlRequested?.Invoke(control));
            binding.Button.HoverChanged += hovering =>
            {
                if (hovering)
                    _tooltip.Begin(
                        () => new HoverTooltipContent(binding.Tooltip, binding.SourceBounds),
                        Time.unscaledTime
                    );
                else
                    _tooltip.Cancel();
            };
        }
        SetPanel(TacticalCombatHudPanel.Empty);
        _stationCharge.gameObject.SetActive(false);
    }

    /// <summary>
    /// Presents loading, battle progress, or completion without changing encounter state.
    /// </summary>
    /// <param name="value">The status text.</param>
    public void SetStatus(string value) => _status.text = value;

    /// <summary>
    /// Presents the selected units' current condition and orders.
    /// </summary>
    /// <param name="value">The selection text.</param>
    public void SetSelection(string value) => _selection.text = value;

    /// <summary>
    /// Routes an inspection key transition through the panel that is currently displayed.
    /// </summary>
    /// <param name="key">The key whose state changed.</param>
    /// <param name="keyDown">Whether this is a press rather than a release.</param>
    /// <param name="shiftDown">Whether Shift is down at this transition.</param>
    public void HandleInspectionKey(Key key, bool keyDown, bool shiftDown)
    {
        if (
            TacticalCombatKeyboard.TryGetPanelControl(
                _panel,
                key,
                keyDown,
                shiftDown,
                out TacticalCombatControl control
            )
        )
            ApplyKeyboardActivation(control, keyDown);
    }

    /// <summary>
    /// Activates or releases an inspection image belonging to the currently displayed panel.
    /// </summary>
    /// <param name="control">The addressed previous or next image.</param>
    /// <param name="activate">Whether to complete a click rather than clear its pending press.</param>
    private void ApplyKeyboardActivation(TacticalCombatControl control, bool activate)
    {
        foreach (ControlBinding binding in _controls)
            if (binding.Panel == _panel && binding.Control == control)
            {
                binding.Button.ApplyKeyboardActivation(activate);
                return;
            }
    }

    /// <summary>
    /// Enables one semantic control according to the controller's live encounter assessment.
    /// </summary>
    /// <param name="control">The authored control.</param>
    /// <param name="enabled">Whether the action is available.</param>
    public void SetControlEnabled(TacticalCombatControl control, bool enabled)
    {
        foreach (ControlBinding binding in _controls)
            if (binding.Control == control)
                binding.Button.interactable = enabled;
    }

    /// <summary>
    /// Resolves the frame and image-button states through the selected content pack.
    /// </summary>
    /// <param name="definition">The encounter's authored common images.</param>
    /// <param name="faction">The player's image overrides.</param>
    /// <param name="assets">The content owner retaining the loaded textures and sprites.</param>
    public void PrepareArtwork(
        TacticalCombatDefinition definition,
        TacticalCombatFactionDefinition faction,
        ContentAssets assets
    )
    {
        _assets = assets ?? throw new ArgumentNullException(nameof(assets));
        AddImages(definition.HudImages);
        AddImages(faction.HudImages);
        _frameImage.texture = Texture("frame");
        _frameImage.enabled = true;
        _objectHeader.enabled = true;
        _fighterHeader.enabled = true;
        _panelImage.enabled = true;
        _stationCharge.enabled = true;
        _objectHeader.texture = Texture("object-header");
        _fighterHeader.texture = Texture("fighter-header");
        _stationCharge.texture = Texture("station-charge");
        foreach (ControlBinding binding in _controls)
        {
            binding.Up = Sprite(binding.ArtworkKey + "-up");
            if (binding.BitmapHitTest)
                binding.Button.SetHitMask(
                    _assets.GetReadableTexture(ImagePath(binding.ArtworkKey + "-up"))
                        ?? throw new InvalidDataException(
                            $"Tactical control mask '{binding.ArtworkKey}' could not be loaded."
                        )
                );
            binding.Down = Sprite(binding.ArtworkKey + "-down");
            binding.Disabled = Sprite(binding.ArtworkKey + "-disabled");
            binding.Button.transition = Selectable.Transition.SpriteSwap;
            SetButtonState(binding);
        }
        _status.gameObject.SetActive(false);
        SetPanel(TacticalCombatHudPanel.Empty);
    }

    /// <summary>
    /// Displays one right-panel context while preserving its fixed source-space bounds.
    /// </summary>
    /// <param name="panel">The selected inspection, editor, or command panel.</param>
    public void SetPanel(TacticalCombatHudPanel panel)
    {
        if (_panel == panel && _panelImage.texture != null)
            return;
        _panel = panel;
        _objectPreview.gameObject.SetActive(
            panel is TacticalCombatHudPanel.SingleObject or TacticalCombatHudPanel.ObjectGroup
        );
        _fighterPreview.gameObject.SetActive(
            panel == TacticalCombatHudPanel.FighterGroup && _fighterPreview.texture != null
        );
        foreach (PanelBinding binding in _panels)
            binding.Root.SetActive(binding.Panel == panel);
        bool inspection =
            panel
            is TacticalCombatHudPanel.SingleObject
                or TacticalCombatHudPanel.ObjectGroup
                or TacticalCombatHudPanel.FighterGroup;
        _selection.gameObject.SetActive(inspection);
        UILayout.SetSourceRect(
            _selection.rectTransform,
            26,
            panel == TacticalCombatHudPanel.FighterGroup ? 9 : 7,
            97,
            panel == TacticalCombatHudPanel.ObjectGroup ? 13 : 9
        );
        _selection.alignment =
            panel == TacticalCombatHudPanel.FighterGroup
                ? TextAlignmentOptions.Top
                : TextAlignmentOptions.TopLeft;
        bool capital =
            panel is TacticalCombatHudPanel.SingleObject or TacticalCombatHudPanel.ObjectGroup;
        _assignment.gameObject.SetActive(capital);
        _orders.gameObject.SetActive(inspection);
        _tactics.gameObject.SetActive(inspection);
        _fighterCounts.gameObject.SetActive(panel == TacticalCombatHudPanel.FighterGroup);
        foreach (RawImage icon in _systemIcons)
            icon.gameObject.SetActive(capital);
        bool fighter = panel == TacticalCombatHudPanel.FighterGroup;
        UILayout.SetSourceRect(
            _orders.rectTransform,
            fighter ? 20 : 15,
            fighter ? 20 : 182,
            fighter ? 102 : 121,
            panel == TacticalCombatHudPanel.ObjectGroup ? 13 : 9
        );
        UILayout.SetSourceRect(
            _tactics.rectTransform,
            fighter ? 20 : 15,
            fighter ? 31 : 193,
            fighter ? 102 : 121,
            9
        );
        _hullBar.transform.parent.gameObject.SetActive(capital);
        _shieldBar.transform.parent.gameObject.SetActive(capital);
        if (_assets == null)
            return;
        string key = panel switch
        {
            TacticalCombatHudPanel.SingleObject => "single-panel",
            TacticalCombatHudPanel.ObjectGroup => "object-panel",
            TacticalCombatHudPanel.FighterGroup => "fighter-panel",
            TacticalCombatHudPanel.Orders => "orders-panel",
            TacticalCombatHudPanel.Tactics => "tactics-panel",
            TacticalCombatHudPanel.Commands => "commands-panel",
            TacticalCombatHudPanel.WithdrawalConfirmation => "withdrawal-panel",
            _ => "empty-panel",
        };
        _panelImage.texture = Texture(key);
        _diagram.gameObject.SetActive(false);
    }

    /// <summary>
    /// Presents the addressed order or maneuver illustration inside the active editor.
    /// </summary>
    /// <param name="key">The semantic illustration key, or null to leave the panel's backing visible.</param>
    public void SetDiagram(string key)
    {
        _diagram.gameObject.SetActive(key != null);
        if (key != null)
        {
            _diagram.enabled = true;
            _diagram.texture = Texture(key);
        }
    }

    /// <summary>
    /// Presents the addressed participant's image, status rows, system levels, and current pools.
    /// </summary>
    /// <param name="unit">The inspected participant.</param>
    /// <param name="data">The formatted update from the encounter's inspection owner.</param>
    public void SetInspection(TacticalCombatUnit unit, TacticalCombatInspectionData data)
    {
        _selection.text = data.Name;
        _assignment.text = data.Assignment;
        _orders.text = data.Orders;
        if (_panel == TacticalCombatHudPanel.ObjectGroup)
        {
            if (data.Tactics != null)
                _groupTactics = data.Tactics;
            _tactics.text = _groupTactics;
        }
        else
        {
            if (data.Tactics != null)
                _singleTactics = data.Tactics;
            _tactics.text = _singleTactics;
        }
        if (_assets != null)
        {
            Texture2D image = Texture("unit-preview-" + data.PreviewIndex.Value);
            _objectPreview.enabled = true;
            UILayout.SetImage(_objectPreview, image, 26, 17);
            SetSystemIcon(0, "shield", data.Shields);
            SetSystemIcon(1, "weapon", data.Weapons);
            SetSystemIcon(2, "tractor", data.Tractor);
            SetSystemIcon(3, "engine", data.Movement);
            SetSystemIcon(4, "hyperdrive", data.Hyperdrive);
        }
        _hullBar.rectTransform.anchorMax = new Vector2((float)unit.Systems.HullFraction, 1);
        _shieldBar.rectTransform.anchorMax = new Vector2(
            (float)unit.Systems.GetShieldFraction(),
            1
        );
    }

    /// <summary>
    /// Displays the fighter category's title, routing, tactics, and requested and applied counts.
    /// </summary>
    /// <param name="data">The current category details, including optional retained preview and count updates.</param>
    public void SetFighterInspection(TacticalCombatInspectionData data)
    {
        _selection.text = data.Name;
        _orders.text = data.Orders;
        _tactics.text = data.Tactics;
        if (data.FighterCounts != null)
            _fighterCounts.text = data.FighterCounts;
        if (data.PreviewIndex.HasValue && _assets != null)
        {
            _fighterPreview.enabled = true;
            _fighterPreview.gameObject.SetActive(true);
            _fighterPreview.texture = Texture("fighter-preview-" + data.PreviewIndex.Value);
        }
    }

    /// <summary>
    /// Resolves one system's current five-state image through the retained content owner.
    /// </summary>
    /// <param name="index">The fixed system slot.</param>
    /// <param name="key">The semantic system image prefix.</param>
    /// <param name="level">The current image level from zero through four.</param>
    private void SetSystemIcon(int index, string key, int level)
    {
        _systemIcons[index].enabled = true;
        _systemIcons[index].texture = Texture("status-" + key + "-" + level);
    }

    /// <summary>
    /// Updates the owning faction's battle-station charge header and its target control.
    /// </summary>
    /// <param name="visible">Whether this encounter owns the charge header.</param>
    /// <param name="charge">The current charge percentage.</param>
    public void SetStationCharge(bool visible, float charge)
    {
        _stationCharge.gameObject.SetActive(visible);
        _stationChargeBar.rectTransform.anchorMax = new Vector2(charge * 0.01f, 1);
        foreach (ControlBinding binding in _controls)
            if (binding.Control == TacticalCombatControl.Superlaser)
                binding.Button.gameObject.SetActive(visible);
    }

    /// <summary>
    /// Projects persistent selection independently of transient pointer-press state.
    /// </summary>
    /// <param name="control">The semantic control.</param>
    /// <param name="selected">Whether the control is selected.</param>
    /// <param name="occupied">Whether an object-category slot has an assigned group.</param>
    public void SetControlSelected(
        TacticalCombatControl control,
        bool selected,
        bool occupied = true
    )
    {
        foreach (ControlBinding binding in _controls)
            if (binding.Control == control)
            {
                binding.Selected = selected;
                if (_assets != null)
                    SetButtonState(binding, occupied);
            }
    }

    /// <summary>
    /// Assigns one control's normal, selected, pressed, and disabled sprites.
    /// </summary>
    /// <param name="binding">The authored control.</param>
    /// <param name="occupied">Whether a task-force category is populated.</param>
    private static void SetButtonState(ControlBinding binding, bool occupied = true)
    {
        bool empty = binding.ArtworkKey == "object-slot" && !occupied;
        Sprite normal =
            empty ? binding.Disabled
            : binding.Selected ? binding.Down
            : binding.Up;
        if (binding.Label != null)
        {
            binding.Label.gameObject.SetActive(!empty);
            binding.Label.color = binding.Selected ? Color.white : Color.black;
        }
        binding.Button.targetGraphic.enabled = true;
        ((Image)binding.Button.targetGraphic).sprite = normal;
        binding.Button.spriteState = new SpriteState
        {
            highlightedSprite = normal,
            selectedSprite = normal,
            pressedSprite = empty ? binding.Disabled : binding.Down,
            disabledSprite = binding.Disabled,
        };
    }

    /// <summary>
    /// Registers semantic image addresses, allowing the faction to override common artwork.
    /// </summary>
    /// <param name="images">The authored image records.</param>
    private void AddImages(IEnumerable<TacticalCombatHudImage> images)
    {
        foreach (TacticalCombatHudImage image in images)
            _imagePaths[image.Key] = image.Path;
    }

    /// <summary>
    /// Resolves a required authored image address with an actionable content error.
    /// </summary>
    /// <param name="key">The semantic image key.</param>
    /// <returns>The required content address.</returns>
    private string ImagePath(string key) =>
        _imagePaths.TryGetValue(key, out string path)
            ? path
            : throw new InvalidDataException(
                $"Tactical combat HUD image '{key}' is not configured."
            );

    /// <summary>
    /// Resolves a required texture through the active content owner.
    /// </summary>
    /// <param name="key">The semantic image key.</param>
    /// <returns>The retained texture.</returns>
    private Texture2D Texture(string key) =>
        _assets.GetTexture(ImagePath(key))
        ?? throw new InvalidDataException(
            $"Tactical combat HUD image '{key}' could not be loaded."
        );

    /// <summary>
    /// Resolves a required sprite through the active content owner.
    /// </summary>
    /// <param name="key">The semantic image key.</param>
    /// <returns>The retained image sprite.</returns>
    private Sprite Sprite(string key) =>
        _assets.GetSprite(ImagePath(key))
        ?? throw new InvalidDataException(
            $"Tactical combat HUD image '{key}' could not be loaded."
        );

    /// <summary>
    /// Advances the shared tooltip using unscaled presentation time.
    /// </summary>
    private void Update() => _tooltip?.Advance(Time.unscaledTime);

    /// <summary>
    /// Releases the view-owned shared tooltip hierarchy.
    /// </summary>
    private void OnDestroy() => _tooltip?.Destroy();

    /// <summary>
    /// Assigns the camera's owned render target to the authored viewport.
    /// </summary>
    /// <param name="texture">The current render target.</param>
    public void SetViewportTexture(RenderTexture texture)
    {
        _viewport.texture = texture;
        _viewport.color = texture != null ? Color.white : Color.clear;
    }

    /// <summary>
    /// Displays the addressed starfield behind the transparent battle camera.
    /// </summary>
    /// <param name="texture">The selected pack's starfield.</param>
    public void SetBackgroundTexture(Texture2D texture)
    {
        _background.texture = texture;
        _background.color = texture != null ? Color.white : Color.black;
    }

    /// <summary>
    /// Routes a press originating inside the battle viewport.
    /// </summary>
    /// <param name="eventData">The viewport pointer event.</param>
    public void OnPointerDown(PointerEventData eventData)
    {
        if (
            RectTransformUtility.RectangleContainsScreenPoint(
                Viewport,
                eventData.position,
                eventData.pressEventCamera
            )
        )
            PointerPressed?.Invoke(eventData);
    }

    /// <summary>
    /// Routes zoom input while the pointer is over the battle viewport.
    /// </summary>
    /// <param name="eventData">The viewport scroll event.</param>
    public void OnScroll(PointerEventData eventData) =>
        ZoomRequested?.Invoke(eventData.scrollDelta.y);
}
