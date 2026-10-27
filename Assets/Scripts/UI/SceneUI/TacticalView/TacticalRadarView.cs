using System.Collections.Generic;
using Rebellion.Game.Combat;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Displays loaded battle contacts on an X/Z instrument and navigates the battle camera.
/// </summary>
[RequireComponent(typeof(CanvasRenderer))]
public sealed class TacticalRadarView : MaskableGraphic, IPointerDownHandler, IDragHandler
{
    private IReadOnlyDictionary<CombatUnit, Transform> _presentations;
    private IReadOnlyList<CombatUnit> _selected;
    private BattleMapBounds _bounds;
    private string _viewer;
    private Camera _camera;
    private float _nextRefresh;
    private Color _friendly = new Color(0.55f, 0.85f, 0.72f);
    private readonly HashSet<CombatUnit> _selectedContacts = new HashSet<CombatUnit>();

    /// <summary>
    /// Binds contacts to this battle's already loaded presentation index.
    /// </summary>
    /// <param name="presentations">The loaded combatants and roots.</param>
    /// <param name="bounds">The authored playable bounds.</param>
    /// <param name="camera">The navigable battle camera.</param>
    /// <param name="viewer">The viewing participant's faction identifier.</param>
    /// <param name="selected">The selection owner's live read-only list.</param>
    public void Bind(
        IReadOnlyDictionary<CombatUnit, Transform> presentations,
        BattleMapBounds bounds,
        Camera camera,
        string viewer,
        IReadOnlyList<CombatUnit> selected
    )
    {
        _presentations = presentations;
        _bounds = bounds;
        _camera = camera;
        _viewer = viewer;
        _selected = selected;
        SetVerticesDirty();
    }

    /// <summary>
    /// Changes the faction's friendly-contact color.
    /// </summary>
    /// <param name="tint">The contact tint.</param>
    public void SetContactColor(Color tint)
    {
        _friendly = tint;
        SetVerticesDirty();
    }

    /// <summary>
    /// Samples the presentation index at five hertz, independently of simulation ticks.
    /// </summary>
    private void Update()
    {
        if (_presentations == null || Time.unscaledTime < _nextRefresh)
            return;
        _nextRefresh = Time.unscaledTime + 0.2f;
        SetVerticesDirty();
    }

    /// <summary>
    /// Draws battle contacts on a plain minimap background.
    /// </summary>
    /// <param name="mesh">The canvas mesh.</param>
    protected override void OnPopulateMesh(VertexHelper mesh)
    {
        mesh.Clear();
        Rect r = GetPixelAdjustedRect();
        TacticalHudGraphic.Quad(mesh, r, new Color(0.006f, 0.01f, 0.012f));
        if (_presentations == null || _bounds == null)
            return;
        Rect playableRect = GetPlayableRect(r);
        TacticalHudGraphic.Quad(mesh, playableRect, new Color(0.014f, 0.025f, 0.024f));
        _selectedContacts.Clear();
        if (_selected != null)
            for (int i = 0; i < _selected.Count; i++)
                _selectedContacts.Add(_selected[i]);
        foreach (KeyValuePair<CombatUnit, Transform> pair in _presentations)
        {
            if (!pair.Value || !pair.Value.gameObject.activeInHierarchy || pair.Key.HasRetreated)
                continue;
            Vector2 p = Project(pair.Value.position, playableRect);
            bool selected = _selectedContacts.Contains(pair.Key);
            Color tint =
                selected ? Color.white
                : pair.Key.GetUnit().OwnerInstanceID == _viewer ? _friendly
                : new Color(0.9f, 0.22f, 0.14f);
            float size = selected ? 3f : 2f;
            TacticalHudGraphic.Triangle(
                mesh,
                p + Vector2.up * size,
                p + new Vector2(-size, -size),
                p + new Vector2(size, -size),
                tint
            );
        }
    }

    /// <summary>
    /// Projects a battle location into the instrument rectangle.
    /// </summary>
    /// <param name="world">The world point.</param>
    /// <param name="rect">The instrument rectangle.</param>
    /// <returns>The local radar point.</returns>
    private Vector2 Project(Vector3 world, Rect rect) =>
        new Vector2(
            Mathf.Lerp(
                rect.xMin,
                rect.xMax,
                Mathf.InverseLerp(_bounds.MinimumX, _bounds.MaximumX, world.x)
            ),
            Mathf.Lerp(
                rect.yMin,
                rect.yMax,
                Mathf.InverseLerp(_bounds.MinimumZ, _bounds.MaximumZ, world.z)
            )
        );

    /// <summary>
    /// Fits the authored X/Z playable area inside the square instrument without distorting it.
    /// </summary>
    /// <param name="rect">The full local instrument rectangle.</param>
    /// <returns>The inset, aspect-correct playable rectangle.</returns>
    private Rect GetPlayableRect(Rect rect)
    {
        Rect available = Rect.MinMaxRect(
            rect.xMin + 4f,
            rect.yMin + 4f,
            rect.xMax - 4f,
            rect.yMax - 4f
        );
        float width = Mathf.Max(0.0001f, _bounds.MaximumX - _bounds.MinimumX);
        float depth = Mathf.Max(0.0001f, _bounds.MaximumZ - _bounds.MinimumZ);
        float playableAspect = width / depth;
        float availableAspect = available.width / available.height;
        if (playableAspect > availableAspect)
        {
            float height = available.width / playableAspect;
            available.y += (available.height - height) * 0.5f;
            available.height = height;
        }
        else
        {
            float fittedWidth = available.height * playableAspect;
            available.x += (available.width - fittedWidth) * 0.5f;
            available.width = fittedWidth;
        }
        return available;
    }

    /// <summary>
    /// Moves the camera pivot to a clicked location without moving ships.
    /// </summary>
    /// <param name="eventData">The pointer input.</param>
    public void OnPointerDown(PointerEventData eventData)
    {
        if (eventData.button != PointerEventData.InputButton.Left || _bounds == null || !_camera)
            return;
        if (
            !RectTransformUtility.ScreenPointToLocalPointInRectangle(
                rectTransform,
                eventData.position,
                eventData.pressEventCamera,
                out Vector2 local
            )
        )
            return;
        Rect r = GetPlayableRect(rectTransform.rect);
        if (!r.Contains(local))
            return;
        Vector3 point = new Vector3(
            Mathf.Lerp(
                _bounds.MinimumX,
                _bounds.MaximumX,
                Mathf.InverseLerp(r.xMin, r.xMax, local.x)
            ),
            (_bounds.MinimumY + _bounds.MaximumY) * 0.5f,
            Mathf.Lerp(
                _bounds.MinimumZ,
                _bounds.MaximumZ,
                Mathf.InverseLerp(r.yMin, r.yMax, local.y)
            )
        );
        _camera.GetComponent<TacticalViewCameraController>().Recenter(point);
        SetVerticesDirty();
    }

    /// <summary>
    /// Supports dragging the radar camera position.
    /// </summary>
    /// <param name="eventData">The drag input.</param>
    public void OnDrag(PointerEventData eventData) => OnPointerDown(eventData);
}
