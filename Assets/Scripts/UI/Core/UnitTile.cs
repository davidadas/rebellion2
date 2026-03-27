using System;
using Rebellion.SceneGraph;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Visual representation of a single game entity inside a grid/panel.
/// This class is intentionally dumb. It renders visuals and raises interaction events.
/// It does NOT modify game state.
/// </summary>
public sealed class UnitTile
    : MonoBehaviour,
        IPointerClickHandler,
        IBeginDragHandler,
        IDragHandler,
        IEndDragHandler
{
    [SerializeField]
    private Image iconImage;

    [SerializeField]
    private Image selectionOutline;

    [SerializeField]
    private TextMeshProUGUI nameText;

    private IGameEntity entity;
    private RectTransform rectTransform;
    private CanvasGroup canvasGroup;

    public IGameEntity Entity => entity;

    // Interaction Events
    public event Action<UnitTile> Clicked;
    public event Action<UnitTile> RightClicked;
    public event Action<UnitTile> DragStarted;
    public event Action<UnitTile, PointerEventData> Dragging;
    public event Action<UnitTile, PointerEventData> DragEnded;

    private void Awake()
    {
        rectTransform = transform as RectTransform;

        if (rectTransform == null)
            throw new InvalidOperationException("UnitTile requires RectTransform.");

        canvasGroup = GetComponent<CanvasGroup>();

        if (canvasGroup == null)
            canvasGroup = gameObject.AddComponent<CanvasGroup>();

        SetSelected(false);
    }

    /// <summary>
    /// Initializes the tile with its backing entity and visual sprite.
    /// </summary>
    public void Initialize(IGameEntity entity, Sprite sprite, Color factionColor)
    {
        if (entity == null)
            throw new ArgumentNullException(nameof(entity));

        if (sprite == null)
            throw new ArgumentNullException(nameof(sprite));

        this.entity = entity;

        iconImage.sprite = sprite;

        if (selectionOutline != null)
            selectionOutline.color = factionColor;

        if (nameText != null)
            nameText.SetText(entity.GetDisplayName());
    }

    /// <summary>
    /// Sets the selection visual state.
    /// </summary>
    public void SetSelected(bool selected)
    {
        if (selectionOutline != null)
            selectionOutline.enabled = selected;
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (eventData.button == PointerEventData.InputButton.Left)
        {
            Clicked?.Invoke(this);
        }
        else if (eventData.button == PointerEventData.InputButton.Right)
        {
            RightClicked?.Invoke(this);
        }
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        canvasGroup.blocksRaycasts = false;
        DragStarted?.Invoke(this);
    }

    public void OnDrag(PointerEventData eventData)
    {
        rectTransform.anchoredPosition += eventData.delta;
        Dragging?.Invoke(this, eventData);
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        canvasGroup.blocksRaycasts = true;
        DragEnded?.Invoke(this, eventData);
    }
}
