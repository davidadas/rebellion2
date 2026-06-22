using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[RequireComponent(typeof(RectTransform))]
public sealed class UIWindow : MonoBehaviour, IPointerDownHandler
{
    private UIWindowManager windowManager;
    private RectTransform rectTransform;
    private CanvasGroup canvasGroup;
    private MonoBehaviour content;
    private bool activeWindow;
    private readonly List<Button> boundActionButtons = new List<Button>();
    private readonly List<UnityAction> boundActionListeners = new List<UnityAction>();

    [SerializeField]
    private Button[] actionButtons = Array.Empty<Button>();

    [SerializeField]
    private int[] buttonActions = Array.Empty<int>();

    public event Action<UIWindow> FocusRequested;
    public event Action<UIWindow, int> ButtonRequested;
    public event Action<UIWindow, PointerEventData, int, int> ContextRequested;
    public event Action<UIWindow, RectInt> MovePreviewChanged;
    public event Action<UIWindow> MovePreviewEnded;
    public event Action<UIWindow> Moved;
    public event Action<UIWindow> CloseRequested;

    public int Id { get; private set; }
    public bool Modal { get; private set; }
    public bool CanFocus { get; private set; }
    public bool CanMove { get; private set; }
    public bool ActiveWindow => activeWindow;
    public MonoBehaviour Content => content;

    public int X => Mathf.RoundToInt(GetRectTransform().anchoredPosition.x);
    public int Y => Mathf.RoundToInt(-GetRectTransform().anchoredPosition.y);
    public int Width => Mathf.RoundToInt(GetRectTransform().sizeDelta.x);
    public int Height => Mathf.RoundToInt(GetRectTransform().sizeDelta.y);
    public RectInt Bounds => new RectInt(X, Y, Width, Height);

    private void Awake()
    {
        BindActionButtons();
    }

    private void OnDestroy()
    {
        UnbindActionButtons();
    }

    public void Configure(
        int id,
        int x,
        int y,
        int width,
        int height,
        bool modal,
        bool canFocus,
        bool canMove
    )
    {
        Id = id;
        Modal = modal;
        CanFocus = canFocus;
        CanMove = canMove;
        Resize(width, height);
        MoveTo(x, y);
    }

    public void Attach(UIWindowManager windowManager)
    {
        this.windowManager = windowManager;
    }

    public void SetContent(MonoBehaviour content)
    {
        this.content = content;
    }

    public bool TryGetContent<TContent>(out TContent typedContent)
        where TContent : class
    {
        typedContent = content as TContent;
        return typedContent != null;
    }

    public void MoveTo(int x, int y)
    {
        RectTransform rect = GetRectTransform();
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = new Vector2(x, -y);
        rect.localScale = Vector3.one;
    }

    public void Resize(int width, int height)
    {
        RectTransform rect = GetRectTransform();
        rect.sizeDelta = new Vector2(Mathf.Max(0, width), Mathf.Max(0, height));
    }

    public void SetActiveWindow(bool active)
    {
        activeWindow = active;
    }

    public void SetInputBlocked(bool blocked)
    {
        CanvasGroup group = GetCanvasGroup();
        group.blocksRaycasts = !blocked;
        group.interactable = !blocked;
    }

    public bool TryGetDesktopPosition(
        PointerEventData eventData,
        Vector2 screenPosition,
        out int x,
        out int y
    )
    {
        x = 0;
        y = 0;
        return windowManager != null
            && windowManager.TryGetPosition(eventData, screenPosition, out x, out y);
    }

    public Vector2Int ClampPosition(int x, int y)
    {
        if (windowManager == null)
            return new Vector2Int(x, y);

        return windowManager.ClampPosition(x, y, new Vector2Int(Width, Height));
    }

    public bool RequestFocus()
    {
        if (!CanFocus)
            return false;

        if (windowManager != null && !windowManager.Focus(this))
            return false;

        FocusRequested?.Invoke(this);
        return true;
    }

    public void RequestButton(int action)
    {
        if (action == 0)
            return;

        if (!CanSendRequest())
            return;

        ButtonRequested?.Invoke(this, action);
    }

    public void RequestContext(PointerEventData eventData)
    {
        if (
            eventData == null
            || !TryGetDesktopPosition(eventData, eventData.position, out int x, out int y)
        )
            return;

        if (!CanSendRequest())
            return;

        ContextRequested?.Invoke(this, eventData, x, y);
    }

    public bool TryCancel()
    {
        if (!CanCancel())
            return false;

        if (content is ICancelable cancelable && cancelable.TryCancel())
            return true;

        if (CloseRequested == null)
            return false;

        CloseRequested?.Invoke(this);
        return true;
    }

    public void NotifyMoved()
    {
        Moved?.Invoke(this);
    }

    public void NotifyMovePreviewChanged(RectInt bounds)
    {
        MovePreviewChanged?.Invoke(this, bounds);
    }

    public void NotifyMovePreviewEnded()
    {
        MovePreviewEnded?.Invoke(this);
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        RequestFocus();
    }

    private bool CanSendRequest()
    {
        if (windowManager != null && !windowManager.CanInteractWithWindow(this))
            return false;

        return !CanFocus || RequestFocus();
    }

    private bool CanCancel()
    {
        if (!activeWindow)
            return false;

        return windowManager == null || windowManager.CanInteractWithWindow(this);
    }

    private RectTransform GetRectTransform()
    {
        if (rectTransform == null)
            rectTransform = transform as RectTransform;

        return rectTransform;
    }

    private CanvasGroup GetCanvasGroup()
    {
        if (canvasGroup == null && !TryGetComponent(out canvasGroup))
            canvasGroup = gameObject.AddComponent<CanvasGroup>();

        return canvasGroup;
    }

    private void BindActionButtons()
    {
        if (actionButtons == null || buttonActions == null)
            return;

        int count = Mathf.Min(actionButtons.Length, buttonActions.Length);
        for (int i = 0; i < count; i++)
        {
            Button button = actionButtons[i];
            int action = buttonActions[i];
            if (button == null || action == 0)
                continue;

            UnityAction listener = () => RequestButton(action);
            button.onClick.AddListener(listener);
            boundActionButtons.Add(button);
            boundActionListeners.Add(listener);
        }
    }

    private void UnbindActionButtons()
    {
        int count = Mathf.Min(boundActionButtons.Count, boundActionListeners.Count);
        for (int i = 0; i < count; i++)
        {
            if (boundActionButtons[i] != null)
                boundActionButtons[i].onClick.RemoveListener(boundActionListeners[i]);
        }

        boundActionButtons.Clear();
        boundActionListeners.Clear();
    }
}
