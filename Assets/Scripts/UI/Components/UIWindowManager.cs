using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;

public sealed class UIWindowManager : MonoBehaviour, ICancelable
{
    private readonly List<UIWindow> windows = new();
    private RectTransform rectTransform;

    public event Action<UIWindow> FocusChanged;
    public event Action<UIWindow> WindowClosed;
    public event Action<UIWindow> ModalOpened;

    public IReadOnlyList<UIWindow> Windows => windows;
    public UIWindow ActiveWindow { get; private set; }

    public void Register(UIWindow window, bool behind)
    {
        if (window == null)
            return;

        if (!windows.Contains(window))
        {
            if (behind)
                windows.Insert(0, window);
            else
                windows.Add(window);
        }

        window.Attach(this);

        if (behind)
            window.transform.SetAsFirstSibling();
        else
            window.transform.SetAsLastSibling();

        if (window.Modal)
            ModalOpened?.Invoke(window);

        if (!window.CanFocus || !Focus(window))
            ApplyActiveState();
    }

    public void Unregister(UIWindow window)
    {
        if (window == null || !windows.Remove(window))
            return;

        if (ActiveWindow == window)
            ActiveWindow = GetTopFocusableWindow();

        ApplyActiveState();
        WindowClosed?.Invoke(window);
    }

    public bool Focus(UIWindow window)
    {
        if (window == null || !window.CanFocus || !CanInteractWithWindow(window))
            return false;

        windows.Remove(window);
        windows.Add(window);
        window.transform.SetAsLastSibling();
        ActiveWindow = window;
        ApplyActiveState();
        FocusChanged?.Invoke(window);
        return true;
    }

    public bool HasModalWindow()
    {
        return GetTopModalWindow() != null;
    }

    public bool CanInteractWithWindow(UIWindow window)
    {
        if (window == null || !windows.Contains(window))
            return false;

        UIWindow modalWindow = GetTopModalWindow();
        return modalWindow == null || modalWindow == window;
    }

    public UIWindow GetWindowById(int windowId)
    {
        return windows.FirstOrDefault(window => window != null && window.Id == windowId);
    }

    public bool TryCancel()
    {
        return ActiveWindow != null
            && CanInteractWithWindow(ActiveWindow)
            && ActiveWindow.TryCancel();
    }

    public UIWindow GetWindow(PointerEventData eventData)
    {
        if (eventData == null)
            return null;

        UIWindow window = GetInteractableRegisteredWindow(
            eventData.pointerCurrentRaycast.gameObject
        );
        if (window != null)
            return window;

        if (EventSystem.current == null)
            return null;

        List<RaycastResult> raycastResults = new List<RaycastResult>();
        EventSystem.current.RaycastAll(eventData, raycastResults);
        foreach (RaycastResult result in raycastResults)
        {
            window = GetInteractableRegisteredWindow(result.gameObject);
            if (window != null)
                return window;
        }

        return null;
    }

    public Vector2Int ClampPosition(int x, int y, Vector2Int windowSize)
    {
        Vector2Int managerSize = GetSize();
        int maxX = Mathf.Max(0, managerSize.x - windowSize.x);
        int maxY = Mathf.Max(0, managerSize.y - windowSize.y);
        return new Vector2Int(Mathf.Clamp(x, 0, maxX), Mathf.Clamp(y, 0, maxY));
    }

    public bool TryGetPosition(
        PointerEventData eventData,
        Vector2 screenPosition,
        out int x,
        out int y
    )
    {
        x = 0;
        y = 0;

        if (eventData == null)
            return false;

        RectTransform rect = GetRectTransform();
        if (rect == null)
            return false;

        Camera camera = eventData.pressEventCamera;
        if (
            !RectTransformUtility.ScreenPointToLocalPointInRectangle(
                rect,
                screenPosition,
                camera,
                out Vector2 local
            )
        )
        {
            return false;
        }

        Vector2Int managerSize = GetSize();
        if (managerSize.x <= 0 || managerSize.y <= 0)
            return false;

        x = Mathf.RoundToInt(local.x + managerSize.x / 2f);
        y = Mathf.RoundToInt(managerSize.y / 2f - local.y);
        return x >= 0 && x < managerSize.x && y >= 0 && y < managerSize.y;
    }

    private void ApplyActiveState()
    {
        UIWindow modalWindow = GetTopModalWindow();
        foreach (UIWindow window in windows)
        {
            if (window == null)
                continue;

            window.SetActiveWindow(window == ActiveWindow);
            window.SetInputBlocked(modalWindow != null && window != modalWindow);
        }
    }

    private Vector2Int GetSize()
    {
        RectTransform rect = GetRectTransform();
        if (rect == null)
            return Vector2Int.zero;

        int width = Mathf.RoundToInt(rect.sizeDelta.x);
        int height = Mathf.RoundToInt(rect.sizeDelta.y);
        if (width <= 0)
            width = Mathf.RoundToInt(rect.rect.width);
        if (height <= 0)
            height = Mathf.RoundToInt(rect.rect.height);

        return new Vector2Int(width, height);
    }

    private UIWindow GetInteractableRegisteredWindow(GameObject target)
    {
        UIWindow window = target == null ? null : target.GetComponentInParent<UIWindow>();
        return CanInteractWithWindow(window) ? window : null;
    }

    private UIWindow GetTopModalWindow()
    {
        return windows.LastOrDefault(window => window != null && window.Modal);
    }

    private UIWindow GetTopFocusableWindow()
    {
        UIWindow modalWindow = GetTopModalWindow();
        if (modalWindow != null)
            return modalWindow.CanFocus ? modalWindow : null;

        return windows.LastOrDefault(window => window != null && window.CanFocus);
    }

    private RectTransform GetRectTransform()
    {
        if (rectTransform == null)
            rectTransform = transform as RectTransform;

        return rectTransform;
    }
}
