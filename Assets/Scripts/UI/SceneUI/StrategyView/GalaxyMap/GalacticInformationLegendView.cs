using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Presents the retained draggable galactic-information legend and emits its close request.
/// </summary>
public sealed class GalacticInformationLegendView : MonoBehaviour, IBeginDragHandler, IDragHandler
{
    [SerializeField]
    private Image backgroundImage;

    [SerializeField]
    private TextMeshProUGUI titleTextField;

    [SerializeField]
    private TextMeshProUGUI keyHeadingTextField;

    [SerializeField]
    private RectTransform rowsRoot;

    [SerializeField]
    private GalacticInformationLegendRowView rowTemplate;

    [SerializeField]
    private GalacticInformationFrameView frameView;

    [SerializeField]
    private TextMeshProUGUI closeTextField;

    [SerializeField]
    private UIRaycastArea closeHitArea;

    private Vector2 dragPointerOffset;
    private Vector2Int savedSourcePosition;
    private bool closePressed;
    private bool eventsBound;
    private bool hasSavedSourcePosition;
    private GalacticInformationLegendRenderData renderData;
    private readonly List<GalacticInformationLegendRowView> rows = new();

    public bool Visible => gameObject.activeSelf;

    /// <summary>
    /// Raised when the retained legend's close control is selected.
    /// </summary>
    public event Action CloseRequested;

    /// <summary>
    /// Raised when Unity destroys the authored legend view.
    /// </summary>
    public event Action<GalacticInformationLegendView> Destroyed;

    /// <summary>
    /// Validates authored references and subscribes close input when Unity creates the view.
    /// </summary>
    private void Awake()
    {
        VerifyReferences();
        BindEvents();
    }

    /// <summary>
    /// Releases close input subscriptions and informs the feature controller of destruction.
    /// </summary>
    private void OnDestroy()
    {
        UnbindEvents();
        Destroyed?.Invoke(this);
    }

    /// <summary>
    /// Applies immutable legend presentation data while retaining its user-moved position.
    /// </summary>
    /// <param name="data">The legend presentation snapshot.</param>
    public void Render(GalacticInformationLegendRenderData data)
    {
        if (data == null)
        {
            Hide();
            return;
        }

        VerifyReferences();
        renderData = data;
        if (!hasSavedSourcePosition)
        {
            savedSourcePosition = data.Bounds.position;
            hasSavedSourcePosition = true;
        }

        savedSourcePosition = ClampSourcePosition(
            savedSourcePosition,
            data.Bounds.width,
            data.Bounds.height
        );
        UILayout.SetSourceRect(
            transform as RectTransform,
            savedSourcePosition.x,
            savedSourcePosition.y,
            data.Bounds.width,
            data.Bounds.height
        );
        backgroundImage.color = data.BackgroundColor;
        backgroundImage.raycastTarget = true;
        UILayout.SetSourceRect(
            backgroundImage.rectTransform,
            0,
            0,
            data.Bounds.width,
            data.Bounds.height
        );
        UILayout.SetSourceRect(rowsRoot, 0, 0, data.Bounds.width, data.Bounds.height);
        RenderText(titleTextField, data.Title);
        RenderText(keyHeadingTextField, data.KeyHeading);
        for (int index = 0; index < data.Rows.Count; index++)
        {
            if (index == rows.Count)
            {
                GalacticInformationLegendRowView row = Instantiate(rowTemplate, rowsRoot);
                row.name = $"LegendRow-{index}";
                rows.Add(row);
            }
            rows[index].Render(data.Rows[index]);
        }
        for (int index = data.Rows.Count; index < rows.Count; index++)
            rows[index].gameObject.SetActive(false);
        frameView.Render(data.Frame);
        closeHitArea.Render(data.CloseBounds);
        UILayout.SetSourceRect(
            closeTextField.rectTransform,
            data.CloseBounds.x,
            data.CloseBounds.y,
            data.CloseBounds.width,
            data.CloseBounds.height
        );
        closePressed = false;
        RenderCloseControl();
        gameObject.SetActive(true);
        transform.SetAsLastSibling();
    }

    /// <summary>
    /// Applies one native text field's projected contents, color, and bounds.
    /// </summary>
    /// <param name="field">The authored text field.</param>
    /// <param name="data">The projected text presentation.</param>
    private static void RenderText(TextMeshProUGUI field, GalacticInformationTextRenderData data)
    {
        UILayout.SetTextContent(field, data.Text);
        field.color = data.Color;
        UILayout.SetSourceRect(
            field.rectTransform,
            data.Bounds.x,
            data.Bounds.y,
            data.Bounds.width,
            data.Bounds.height
        );
    }

    /// <summary>
    /// Hides the retained legend without discarding its user-moved position.
    /// </summary>
    public void Hide()
    {
        closePressed = false;
        gameObject.SetActive(false);
    }

    /// <summary>
    /// Captures pointer offset before a legend drag begins.
    /// </summary>
    /// <param name="eventData">The originating pointer event.</param>
    public void OnBeginDrag(PointerEventData eventData)
    {
        if (!TryGetParentPoint(eventData, out Vector2 pointerPoint))
            return;

        RectTransform rect = transform as RectTransform;
        dragPointerOffset = pointerPoint - rect.anchoredPosition;
    }

    /// <summary>
    /// Moves the legend within its authored parent bounds during a drag.
    /// </summary>
    /// <param name="eventData">The current pointer event.</param>
    public void OnDrag(PointerEventData eventData)
    {
        if (!TryGetParentPoint(eventData, out Vector2 pointerPoint))
            return;

        RectTransform rect = transform as RectTransform;
        RectTransform parentRect = rect.parent as RectTransform;
        Vector2 position = pointerPoint - dragPointerOffset;
        float maximumX = Mathf.Max(0f, parentRect.rect.width - rect.rect.width);
        float minimumY = Mathf.Min(0f, -parentRect.rect.height + rect.rect.height);
        position.x = Mathf.Clamp(position.x, 0f, maximumX);
        position.y = Mathf.Clamp(position.y, minimumY, 0f);
        rect.anchoredPosition = position;
        savedSourcePosition = new Vector2Int(
            Mathf.RoundToInt(position.x),
            -Mathf.RoundToInt(position.y)
        );
    }

    /// <summary>
    /// Subscribes the authored close-control input exactly once.
    /// </summary>
    private void BindEvents()
    {
        if (eventsBound)
            return;

        closeHitArea.Clicked += HandleCloseClicked;
        closeHitArea.Pressed += HandleClosePressed;
        closeHitArea.Released += HandleCloseReleased;
        eventsBound = true;
    }

    /// <summary>
    /// Releases the authored close-control input subscriptions.
    /// </summary>
    private void UnbindEvents()
    {
        if (!eventsBound)
            return;

        closeHitArea.Clicked -= HandleCloseClicked;
        closeHitArea.Pressed -= HandleClosePressed;
        closeHitArea.Released -= HandleCloseReleased;
        eventsBound = false;
    }

    /// <summary>
    /// Applies pressed close-control presentation.
    /// </summary>
    /// <param name="area">The close-control hit area.</param>
    /// <param name="eventData">The originating pointer event.</param>
    private void HandleClosePressed(UIRaycastArea area, PointerEventData eventData)
    {
        closePressed = true;
        RenderCloseControl();
    }

    /// <summary>
    /// Restores idle close-control presentation.
    /// </summary>
    /// <param name="area">The close-control hit area.</param>
    /// <param name="eventData">The originating pointer event.</param>
    private void HandleCloseReleased(UIRaycastArea area, PointerEventData eventData)
    {
        closePressed = false;
        RenderCloseControl();
    }

    /// <summary>
    /// Emits the semantic legend close request.
    /// </summary>
    /// <param name="area">The close-control hit area.</param>
    /// <param name="eventData">The originating pointer event.</param>
    private void HandleCloseClicked(UIRaycastArea area, PointerEventData eventData)
    {
        CloseRequested?.Invoke();
    }

    /// <summary>
    /// Applies the native close control's pressed state.
    /// </summary>
    private void RenderCloseControl()
    {
        closeTextField.color = closePressed ? renderData.Title.Color : Color.white;
        closeTextField.raycastTarget = false;
    }

    /// <summary>
    /// Converts a pointer event to the legend parent's local coordinates.
    /// </summary>
    /// <param name="eventData">The current pointer event.</param>
    /// <param name="point">Receives the parent-local pointer position.</param>
    /// <returns>True when the pointer position can be resolved.</returns>
    private bool TryGetParentPoint(PointerEventData eventData, out Vector2 point)
    {
        point = default;
        RectTransform parentRect = (transform as RectTransform)?.parent as RectTransform;
        return parentRect != null
            && eventData != null
            && RectTransformUtility.ScreenPointToLocalPointInRectangle(
                parentRect,
                eventData.position,
                eventData.pressEventCamera,
                out point
            );
    }

    /// <summary>
    /// Clamps a retained source-space legend position within its authored parent.
    /// </summary>
    /// <param name="position">The requested source-space position.</param>
    /// <param name="width">The source-space legend width.</param>
    /// <param name="height">The source-space legend height.</param>
    /// <returns>The clamped source-space position.</returns>
    private Vector2Int ClampSourcePosition(Vector2Int position, int width, int height)
    {
        RectTransform parentRect = (transform as RectTransform)?.parent as RectTransform;
        if (parentRect == null)
            return position;

        return new Vector2Int(
            Mathf.Clamp(
                position.x,
                0,
                Mathf.Max(0, Mathf.RoundToInt(parentRect.rect.width) - width)
            ),
            Mathf.Clamp(
                position.y,
                0,
                Mathf.Max(0, Mathf.RoundToInt(parentRect.rect.height) - height)
            )
        );
    }

    /// <summary>
    /// Verifies every authored reference required for legend presentation and input.
    /// </summary>
    private void VerifyReferences()
    {
        if (
            backgroundImage == null
            || frameView == null
            || titleTextField == null
            || keyHeadingTextField == null
            || rowsRoot == null
            || rowTemplate == null
        )
            throw new MissingReferenceException($"{name}/Legend panel is incomplete.");
        if (closeTextField == null || closeHitArea == null)
            throw new MissingReferenceException($"{name}/Close control is missing.");
    }
}
