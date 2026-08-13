using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Owns the authored controls and local scrolling behavior for a reusable vertical scroll area.
/// </summary>
public sealed class ScrollAreaView : MonoBehaviour
{
    [SerializeField]
    private ScrollRect scrollRect;

    [SerializeField]
    private RectTransform contentRoot;

    [SerializeField]
    private Scrollbar scrollbar;

    [SerializeField]
    private RectTransform trackBackgroundRoot;

    [SerializeField]
    private RectTransform slidingAreaRoot;

    [SerializeField]
    private Button scrollUpButton;

    [SerializeField]
    private Button scrollDownButton;

    [SerializeField]
    private ScrollAreaDragRelay dragRelay;

    private float scrollStep;
    private bool updatingScrollbar;

    /// <summary>
    /// Occurs while the control is dragged.
    /// </summary>
    public event Action<PointerEventData> Dragged;

    /// <summary>
    /// Occurs when dragging the scroll area ends.
    /// </summary>
    public event Action<PointerEventData> DragEnded;

    /// <summary>
    /// Occurs when a pointer drop is received by the control.
    /// </summary>
    public event Action<PointerEventData> Dropped;

    public RectTransform ContentRoot
    {
        get
        {
            VerifyRequiredReferences();
            return contentRoot;
        }
    }

    public RectTransform ViewportRoot
    {
        get
        {
            VerifyRequiredReferences();
            return scrollRect.viewport;
        }
    }

    public RectTransform ScrollRoot
    {
        get
        {
            VerifyRequiredReferences();
            return scrollRect.transform as RectTransform;
        }
    }

    public float ViewportWidth
    {
        get
        {
            VerifyRequiredReferences();
            return GetRectWidth(scrollRect.viewport);
        }
    }

    public float ViewportHeight
    {
        get
        {
            VerifyRequiredReferences();
            return GetViewportHeight();
        }
    }

    /// <summary>
    /// Applies feature-specific source-coordinate bounds to the shared scroll controls.
    /// </summary>
    /// <param name="viewportPosition">The viewport's top-left position.</param>
    /// <param name="viewportSize">The viewport size.</param>
    /// <param name="scrollbarPosition">The scrollbar's top-left position.</param>
    /// <param name="scrollbarSize">The scrollbar size.</param>
    public void SetLayout(
        Vector2 viewportPosition,
        Vector2 viewportSize,
        Vector2 scrollbarPosition,
        Vector2 scrollbarSize
    )
    {
        VerifyRequiredReferences();
        SetTopLeftRect(
            ScrollRoot,
            viewportPosition.x,
            viewportPosition.y,
            viewportSize.x,
            viewportSize.y
        );
        SetTopLeftRect(ViewportRoot, 0f, 0f, viewportSize.x, viewportSize.y);

        if (scrollbar == null)
            return;

        SetTopLeftRect(
            scrollbar.transform as RectTransform,
            scrollbarPosition.x,
            scrollbarPosition.y,
            scrollbarSize.x,
            scrollbarSize.y
        );
        SetScrollbarChildrenLayout(scrollbarSize.x, scrollbarSize.y);
    }

    /// <summary>
    /// Updates the content extent, scroll increment, and scrollbar visibility.
    /// </summary>
    /// <param name="contentHeight">The required content height.</param>
    /// <param name="scrollStep">The source-unit distance moved by one scroll action.</param>
    /// <param name="resetScroll">Whether to return to the top of the content.</param>
    public void SetContentHeight(float contentHeight, float scrollStep, bool resetScroll)
    {
        VerifyRequiredReferences();
        ConfigureScrollRectForScrollbarOnly();
        this.scrollStep = scrollStep;
        scrollRect.scrollSensitivity = scrollStep;

        float viewportHeight = GetViewportHeight();
        float appliedContentHeight = Mathf.Max(viewportHeight, contentHeight);
        float normalizedPosition = resetScroll
            ? 1f
            : Mathf.Clamp01(scrollRect.verticalNormalizedPosition);

        contentRoot.sizeDelta = new Vector2(
            GetRectWidth(scrollRect.viewport),
            appliedContentHeight
        );
        SetTopLeftRect(
            contentRoot,
            0f,
            0f,
            GetRectWidth(scrollRect.viewport),
            appliedContentHeight
        );

        bool scrollbarVisible = appliedContentHeight > viewportHeight;
        SetOptionalScrollControlsActive(scrollbarVisible);

        if (scrollbarVisible)
        {
            if (scrollbar != null)
                scrollbar.size = Mathf.Clamp01(viewportHeight / appliedContentHeight);
            SetNormalizedScrollPosition(normalizedPosition);
        }
        else if (!scrollbarVisible)
        {
            contentRoot.anchoredPosition = Vector2.zero;
            SetScrollbarValue(1f);
        }
    }

    /// <summary>
    /// Scrolls the minimum distance required to expose a content rectangle.
    /// </summary>
    /// <param name="contentTop">The rectangle's top offset inside the content.</param>
    /// <param name="contentHeight">The rectangle's height.</param>
    public void RevealContentRect(float contentTop, float contentHeight)
    {
        VerifyRequiredReferences();

        float maxOffset = GetMaxScrollOffset();
        if (maxOffset <= 0f)
            return;

        float viewportHeight = GetViewportHeight();
        float currentOffset = Mathf.Clamp(contentRoot.anchoredPosition.y, 0f, maxOffset);
        float targetTop = Mathf.Max(0f, contentTop);
        float targetBottom = Mathf.Max(targetTop, contentTop + contentHeight);
        float nextOffset = currentOffset;

        if (targetTop < currentOffset)
            nextOffset = targetTop;
        else if (targetBottom > currentOffset + viewportHeight)
            nextOffset = targetBottom - viewportHeight;

        nextOffset = Mathf.Clamp(nextOffset, 0f, maxOffset);
        if (Mathf.Approximately(nextOffset, currentOffset))
            return;

        SetNormalizedScrollPosition(1f - nextOffset / maxOffset);
    }

    /// <summary>
    /// Validates the authored control graph before runtime use.
    /// </summary>
    private void Awake()
    {
        if (!Application.isPlaying)
            return;

        VerifyRequiredReferences();
    }

    /// <summary>
    /// Binds local input controls while the scroll area is active.
    /// </summary>
    private void OnEnable()
    {
        if (!Application.isPlaying)
            return;

        VerifyRequiredReferences();
        ConfigureScrollRectForScrollbarOnly();
        dragRelay.Initialize(this);
        scrollbar.onValueChanged.AddListener(HandleScrollbarValueChanged);
        scrollUpButton.onClick.AddListener(ScrollUp);
        scrollDownButton.onClick.AddListener(ScrollDown);
    }

    /// <summary>
    /// Removes local input listeners when the scroll area is inactive.
    /// </summary>
    private void OnDisable()
    {
        if (!Application.isPlaying)
            return;

        if (scrollUpButton != null)
            scrollUpButton.onClick.RemoveListener(ScrollUp);
        if (scrollDownButton != null)
            scrollDownButton.onClick.RemoveListener(ScrollDown);
        if (scrollbar != null)
            scrollbar.onValueChanged.RemoveListener(HandleScrollbarValueChanged);
        if (dragRelay)
            dragRelay.Clear(this);
    }

    /// <summary>
    /// Forwards a drag from the authored relay to feature listeners.
    /// </summary>
    /// <param name="eventData">The pointer event.</param>
    internal void RelayDrag(PointerEventData eventData)
    {
        Dragged?.Invoke(eventData);
    }

    /// <summary>
    /// Forwards the end of a drag to feature listeners.
    /// </summary>
    /// <param name="eventData">The pointer event.</param>
    internal void RelayDragEnd(PointerEventData eventData)
    {
        DragEnded?.Invoke(eventData);
    }

    /// <summary>
    /// Forwards a drop to feature listeners.
    /// </summary>
    /// <param name="eventData">The pointer event.</param>
    internal void RelayDrop(PointerEventData eventData)
    {
        Dropped?.Invoke(eventData);
    }

    /// <summary>
    /// Converts a pointer-wheel event into a local scroll step.
    /// </summary>
    /// <param name="eventData">The pointer event.</param>
    internal void RelayScroll(PointerEventData eventData)
    {
        if (eventData == null)
            return;

        ScrollBy(-eventData.scrollDelta.y * scrollStep);
    }

    /// <summary>
    /// Disables ScrollRect's direct dragging so the authored controls own vertical movement.
    /// </summary>
    private void ConfigureScrollRectForScrollbarOnly()
    {
        if (scrollRect == null)
            return;

        scrollRect.StopMovement();
        scrollRect.horizontal = false;
        scrollRect.vertical = false;
        scrollRect.verticalScrollbar = null;
    }

    /// <summary>
    /// Moves the content upward by one configured step.
    /// </summary>
    private void ScrollUp()
    {
        ScrollBy(-scrollStep);
    }

    /// <summary>
    /// Moves the content downward by one configured step.
    /// </summary>
    private void ScrollDown()
    {
        ScrollBy(scrollStep);
    }

    /// <summary>
    /// Moves the content by a signed source-unit distance.
    /// </summary>
    /// <param name="delta">The signed content offset.</param>
    private void ScrollBy(float delta)
    {
        float maxOffset = GetMaxScrollOffset();
        if (maxOffset <= 0f || delta == 0f)
            return;

        float offset = Mathf.Clamp(contentRoot.anchoredPosition.y + delta, 0f, maxOffset);
        SetNormalizedScrollPosition(1f - offset / maxOffset);
    }

    /// <summary>
    /// Applies a user-selected scrollbar value to the content.
    /// </summary>
    /// <param name="value">The normalized scrollbar value.</param>
    private void HandleScrollbarValueChanged(float value)
    {
        if (updatingScrollbar)
            return;

        ApplyNormalizedScrollPosition(value);
    }

    /// <summary>
    /// Synchronizes the content and scrollbar to a normalized position.
    /// </summary>
    /// <param name="value">The normalized scroll position.</param>
    private void SetNormalizedScrollPosition(float value)
    {
        value = Mathf.Clamp01(value);
        ApplyNormalizedScrollPosition(value);
        SetScrollbarValue(value);
    }

    /// <summary>
    /// Applies a normalized position to the content without updating the scrollbar control.
    /// </summary>
    /// <param name="value">The normalized scroll position.</param>
    private void ApplyNormalizedScrollPosition(float value)
    {
        scrollRect.verticalNormalizedPosition = Mathf.Clamp01(value);
    }

    /// <summary>
    /// Updates the scrollbar without re-entering its value-change callback.
    /// </summary>
    /// <param name="value">The normalized scrollbar value.</param>
    private void SetScrollbarValue(float value)
    {
        if (scrollbar == null)
            return;

        updatingScrollbar = true;
        scrollbar.value = value;
        updatingScrollbar = false;
    }

    /// <summary>
    /// Shows or hides the authored controls according to content overflow.
    /// </summary>
    /// <param name="active">Whether scrolling is available.</param>
    private void SetOptionalScrollControlsActive(bool active)
    {
        if (scrollbar != null)
            scrollbar.gameObject.SetActive(active);
        if (scrollUpButton != null)
            scrollUpButton.gameObject.SetActive(active);
        if (scrollDownButton != null)
            scrollDownButton.gameObject.SetActive(active);
    }

    /// <summary>
    /// Fits the authored scrollbar children to a feature-specific scrollbar rectangle.
    /// </summary>
    /// <param name="width">The scrollbar width.</param>
    /// <param name="height">The scrollbar height.</param>
    private void SetScrollbarChildrenLayout(float width, float height)
    {
        RectTransform upRect =
            scrollUpButton == null ? null : scrollUpButton.transform as RectTransform;
        RectTransform downRect =
            scrollDownButton == null ? null : scrollDownButton.transform as RectTransform;
        float upArrowHeight = GetRectHeight(upRect);
        float downArrowHeight = GetRectHeight(downRect);
        float trackHeight = Mathf.Max(0f, height - upArrowHeight - downArrowHeight);

        SetTopLeftRect(upRect, 0f, 0f, width, upArrowHeight);
        SetTopLeftRect(downRect, 0f, height - downArrowHeight, width, downArrowHeight);
        SetTopLeftRect(trackBackgroundRoot, 0f, upArrowHeight, width, trackHeight);
        SetTopLeftRect(slidingAreaRoot, 0f, upArrowHeight, width, trackHeight);
    }

    /// <summary>
    /// Applies a top-left anchored rectangle.
    /// </summary>
    /// <param name="rectTransform">The transform to update.</param>
    /// <param name="x">The left coordinate.</param>
    /// <param name="y">The top coordinate.</param>
    /// <param name="width">The width.</param>
    /// <param name="height">The height.</param>
    private static void SetTopLeftRect(
        RectTransform rectTransform,
        float x,
        float y,
        float width,
        float height
    )
    {
        if (rectTransform == null)
            return;

        rectTransform.anchorMin = new Vector2(0f, 1f);
        rectTransform.anchorMax = new Vector2(0f, 1f);
        rectTransform.pivot = new Vector2(0f, 1f);
        rectTransform.anchoredPosition = new Vector2(x, -y);
        rectTransform.sizeDelta = new Vector2(width, height);
        rectTransform.localScale = Vector3.one;
    }

    /// <summary>
    /// Gets the maximum vertical content offset.
    /// </summary>
    /// <returns>The maximum vertical offset.</returns>
    private float GetMaxScrollOffset()
    {
        return Mathf.Max(0f, GetContentHeight() - GetViewportHeight());
    }

    /// <summary>
    /// Gets the current viewport height.
    /// </summary>
    /// <returns>The viewport height.</returns>
    private float GetViewportHeight()
    {
        return GetRectHeight(scrollRect.viewport);
    }

    /// <summary>
    /// Gets the current content height.
    /// </summary>
    /// <returns>The content height.</returns>
    private float GetContentHeight()
    {
        return GetRectHeight(contentRoot);
    }

    /// <summary>
    /// Gets a RectTransform height from its resolved rectangle or authored size.
    /// </summary>
    /// <param name="rectTransform">The transform to measure.</param>
    /// <returns>The measured height.</returns>
    private static float GetRectHeight(RectTransform rectTransform)
    {
        if (rectTransform == null)
            return 0f;

        float height = rectTransform.rect.height;
        return height > 0f ? height : Mathf.Abs(rectTransform.sizeDelta.y);
    }

    /// <summary>
    /// Gets a RectTransform width from its resolved rectangle or authored size.
    /// </summary>
    /// <param name="rectTransform">The transform to measure.</param>
    /// <returns>The measured width.</returns>
    private static float GetRectWidth(RectTransform rectTransform)
    {
        if (rectTransform == null)
            return 0f;

        float width = rectTransform.rect.width;
        return width > 0f ? width : Mathf.Abs(rectTransform.sizeDelta.x);
    }

    /// <summary>
    /// Verifies the complete authored hierarchy required for local scrolling.
    /// </summary>
    private void VerifyRequiredReferences()
    {
        if (scrollRect == null)
            throw new MissingReferenceException($"{name}/ScrollRect is missing.");
        if (scrollRect.viewport == null)
            throw new MissingReferenceException($"{name}/ScrollRect viewport is missing.");
        if (contentRoot == null)
            throw new MissingReferenceException($"{name}/ContentRoot is missing.");
        if (scrollbar == null)
            throw new MissingReferenceException($"{name}/Scrollbar is missing.");
        if (scrollUpButton == null || scrollDownButton == null)
            throw new MissingReferenceException($"{name} scroll buttons are missing.");
        if (dragRelay == null)
            throw new MissingReferenceException($"{name}/ScrollRect drag relay is missing.");
    }
}
