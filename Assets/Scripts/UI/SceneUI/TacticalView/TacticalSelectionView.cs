using System;
using System.Collections.Generic;
using Rebellion.Game.Combat;
using Rebellion.Game.Units;
using Rebellion.SceneGraph;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Owns the fixed bottom dock, pooled roster cards, and projected selection brackets.
/// </summary>
public sealed class TacticalSelectionView : MonoBehaviour
{
    [SerializeField]
    private RectTransform selectionFrame;

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
    private readonly List<RectTransform> _frames = new List<RectTransform>();
    private readonly List<TacticalUnitCardView> _cards = new List<TacticalUnitCardView>();
    private readonly List<CombatUnit> _singleSelection = new List<CombatUnit>(1);
    private readonly Dictionary<CombatUnit, Transform> _singlePresentation =
        new Dictionary<CombatUnit, Transform>();
    private IContentAssetSource _assets;
    private readonly List<CombatUnit> _selectionSnapshot = new List<CombatUnit>();
    private readonly List<List<CombatUnit>> _groups = new List<List<CombatUnit>>();

    public event Action<IReadOnlyList<CombatUnit>> CardClicked;

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
        foreach (TacticalUnitCardView card in _cards)
            if (card.Combatant != null)
                card.BindGroup(card.Combatants, GetPortrait(card.Combatant));
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
        IReadOnlyDictionary<CombatUnit, Transform> presentations,
        BattleMapBounds bounds,
        Camera camera,
        string viewer,
        IReadOnlyList<CombatUnit> selected
    ) => radar.Bind(presentations, bounds, camera, viewer, selected);

    /// <summary>
    /// Renders one inspected unit through the same roster contract as multiselection.
    /// </summary>
    /// <param name="combatant">The selected unit, or null.</param>
    /// <param name="presentation">The model root, or null.</param>
    /// <param name="camera">The battle camera.</param>
    public void Render(CombatUnit combatant, Transform presentation, Camera camera)
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
        IReadOnlyList<CombatUnit> selected,
        IReadOnlyDictionary<CombatUnit, Transform> presentations,
        Camera camera
    )
    {
        int count = selected?.Count ?? 0;
        bool changed = _selectionSnapshot.Count != count;
        for (int i = 0; !changed && i < count; i++)
            changed = _selectionSnapshot[i] != selected[i];
        if (changed)
            RebuildGroups(selected);
        while (_cards.Count < _groups.Count)
        {
            TacticalUnitCardView card = Instantiate(cardTemplate, rosterContent);
            card.name = $"UnitCard{_cards.Count}";
            card.Clicked += HandleCardClicked;
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
                && RenderFrame(_frames[i], root, selected[i].GetUnit().ModelSize, camera);
            _frames[i].gameObject.SetActive(frameVisible);
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
            rosterTitle.text = $"SELECTED UNITS  //  {count:000}";
        }
        if (count == 0)
            selectionFrame.gameObject.SetActive(false);
    }

    /// <summary>
    /// Groups only a changed selection, keeping fighter squadrons and factions distinct.
    /// </summary>
    /// <param name="selected">The selection owner's ordered battle units.</param>
    private void RebuildGroups(IReadOnlyList<CombatUnit> selected)
    {
        _selectionSnapshot.Clear();
        _groups.Clear();
        Dictionary<(string Owner, string Type, string Squadron), List<CombatUnit>> index =
            new Dictionary<(string, string, string), List<CombatUnit>>();
        if (selected == null)
            return;
        foreach (CombatUnit combatant in selected)
        {
            _selectionSnapshot.Add(combatant);
            BaseSceneNode unit = combatant.GetUnit();
            // Missing type identifiers cannot establish that two units share a definition.
            if (string.IsNullOrWhiteSpace(unit.TypeID))
            {
                _groups.Add(new List<CombatUnit> { combatant });
                continue;
            }
            var key = (
                unit.OwnerInstanceID,
                unit.TypeID,
                unit is Starfighter ? combatant.SourceUnitInstanceID : null
            );
            if (!index.TryGetValue(key, out List<CombatUnit> group))
            {
                group = new List<CombatUnit>();
                index.Add(key, group);
                _groups.Add(group);
            }
            group.Add(combatant);
        }
    }

    /// <summary>
    /// Resolves a portrait only when the associated card binding changes.
    /// </summary>
    /// <param name="combatant">The represented unit.</param>
    /// <returns>The configured display portrait, or null when none exists.</returns>
    private Texture2D GetPortrait(CombatUnit combatant)
    {
        string path = combatant.GetUnit().GetDisplayImagePath();
        return _assets != null && !string.IsNullOrWhiteSpace(path)
            ? _assets.GetTexture(path)
            : null;
    }

    /// <summary>
    /// Forwards an authored roster button to the selection controller.
    /// </summary>
    /// <param name="combatants">The clicked card's members.</param>
    private void HandleCardClicked(IReadOnlyList<CombatUnit> combatants) =>
        CardClicked?.Invoke(combatants);

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
        Vector2 minimum = new Vector2(float.PositiveInfinity, float.PositiveInfinity);
        Vector2 maximum = new Vector2(float.NegativeInfinity, float.NegativeInfinity);
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
}
