using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

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

    private float scrollStep;
    private ScrollAreaDragRelay dragRelay;
    private bool updatingScrollbar;

    public event Action<PointerEventData> Dragged;
    public event Action<PointerEventData> DragEnded;
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

    private void Awake()
    {
        VerifyRequiredReferences();
        ConfigureScrollRectForScrollbarOnly();
        BindDragRelay();
        BindScrollbarEvents();
        if (scrollUpButton != null)
            scrollUpButton.onClick.AddListener(ScrollUp);
        if (scrollDownButton != null)
            scrollDownButton.onClick.AddListener(ScrollDown);
    }

    private void OnDestroy()
    {
        if (scrollUpButton != null)
            scrollUpButton.onClick.RemoveListener(ScrollUp);
        if (scrollDownButton != null)
            scrollDownButton.onClick.RemoveListener(ScrollDown);
        if (scrollbar != null)
            scrollbar.onValueChanged.RemoveListener(HandleScrollbarValueChanged);
        if (dragRelay != null)
            dragRelay.Clear(this);
    }

    private void OnEnable()
    {
        VerifyRequiredReferences();
        ConfigureScrollRectForScrollbarOnly();
        BindDragRelay();
        BindScrollbarEvents();
    }

    internal void RelayDrag(PointerEventData eventData)
    {
        Dragged?.Invoke(eventData);
    }

    internal void RelayDragEnd(PointerEventData eventData)
    {
        DragEnded?.Invoke(eventData);
    }

    internal void RelayDrop(PointerEventData eventData)
    {
        Dropped?.Invoke(eventData);
    }

    private void BindDragRelay()
    {
        dragRelay = scrollRect.GetComponent<ScrollAreaDragRelay>();
        if (dragRelay == null)
            dragRelay = scrollRect.gameObject.AddComponent<ScrollAreaDragRelay>();

        dragRelay.Initialize(this);
    }

    private void BindScrollbarEvents()
    {
        if (scrollbar == null)
            return;

        scrollbar.onValueChanged.RemoveListener(HandleScrollbarValueChanged);
        scrollbar.onValueChanged.AddListener(HandleScrollbarValueChanged);
    }

    private void ConfigureScrollRectForScrollbarOnly()
    {
        if (scrollRect == null)
            return;

        scrollRect.StopMovement();
        scrollRect.horizontal = false;
        scrollRect.vertical = false;
        scrollRect.verticalScrollbar = null;
    }

    private void ScrollUp()
    {
        ScrollBy(-scrollStep);
    }

    private void ScrollDown()
    {
        ScrollBy(scrollStep);
    }

    private void ScrollBy(float delta)
    {
        float maxOffset = GetMaxScrollOffset();
        if (maxOffset <= 0f || delta == 0f)
            return;

        float offset = Mathf.Clamp(contentRoot.anchoredPosition.y + delta, 0f, maxOffset);
        SetNormalizedScrollPosition(1f - offset / maxOffset);
    }

    private void HandleScrollbarValueChanged(float value)
    {
        if (updatingScrollbar)
            return;

        ApplyNormalizedScrollPosition(value);
    }

    private void SetNormalizedScrollPosition(float value)
    {
        value = Mathf.Clamp01(value);
        ApplyNormalizedScrollPosition(value);
        SetScrollbarValue(value);
    }

    private void ApplyNormalizedScrollPosition(float value)
    {
        scrollRect.verticalNormalizedPosition = Mathf.Clamp01(value);
    }

    private void SetScrollbarValue(float value)
    {
        if (scrollbar == null)
            return;

        updatingScrollbar = true;
        scrollbar.value = value;
        updatingScrollbar = false;
    }

    private void SetOptionalScrollControlsActive(bool active)
    {
        if (scrollbar != null)
            scrollbar.gameObject.SetActive(active);
        if (scrollUpButton != null)
            scrollUpButton.gameObject.SetActive(active);
        if (scrollDownButton != null)
            scrollDownButton.gameObject.SetActive(active);
    }

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

    private float GetMaxScrollOffset()
    {
        return Mathf.Max(0f, GetContentHeight() - GetViewportHeight());
    }

    private float GetViewportHeight()
    {
        return GetRectHeight(scrollRect.viewport);
    }

    private float GetContentHeight()
    {
        return GetRectHeight(contentRoot);
    }

    private static float GetRectHeight(RectTransform rectTransform)
    {
        if (rectTransform == null)
            return 0f;

        float height = rectTransform.rect.height;
        return height > 0f ? height : Mathf.Abs(rectTransform.sizeDelta.y);
    }

    private static float GetRectWidth(RectTransform rectTransform)
    {
        if (rectTransform == null)
            return 0f;

        float width = rectTransform.rect.width;
        return width > 0f ? width : Mathf.Abs(rectTransform.sizeDelta.x);
    }

    private void VerifyRequiredReferences()
    {
        if (scrollRect == null)
            throw new MissingReferenceException($"{name}/ScrollRect is missing.");
        if (scrollRect.viewport == null)
            throw new MissingReferenceException($"{name}/ScrollRect viewport is missing.");
        if (contentRoot == null)
            throw new MissingReferenceException($"{name}/ContentRoot is missing.");
    }
}
