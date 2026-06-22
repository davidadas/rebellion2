using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

public sealed class ContextMenuHost : MonoBehaviour, ICancelable
{
    private bool bound;

    [SerializeField]
    private ContextMenuView contextMenuView;

    public bool Open => contextMenuView != null && contextMenuView.Open;
    public object Owner => contextMenuView?.Owner;
    public int HotspotX => contextMenuView?.HotspotX ?? 0;
    public int HotspotY => contextMenuView?.HotspotY ?? 0;

    public event System.Action<IContextMenuCommand> CommandSelected;
    public event System.Action<PointerEventData> DismissRequested;

    public void OpenAt(
        object owner,
        int x,
        int y,
        int width,
        IReadOnlyList<ContextMenuCommandItem> commands,
        ContextMenuView.ContextMenuVisuals visuals = null
    )
    {
        EnsureBound();
        contextMenuView.OpenAt(owner, x, y, width, commands, visuals);
    }

    public void Reset()
    {
        contextMenuView?.Reset();
    }

    public bool TryCancel()
    {
        return contextMenuView?.TryCancel() == true;
    }

    public void RenderCurrent()
    {
        contextMenuView?.RenderCurrent();
    }

    public ContextMenuMetrics GetMetrics()
    {
        EnsureBound();
        return contextMenuView.GetMetrics();
    }

    public int GetMenuWidth(int width, IReadOnlyList<ContextMenuCommandItem> commands)
    {
        EnsureBound();
        return contextMenuView.GetMenuWidth(width, commands);
    }

    private void Awake()
    {
        TryBind();
    }

    private void OnDestroy()
    {
        if (contextMenuView == null)
            return;

        contextMenuView.CommandSelected -= HandleCommandSelected;
        contextMenuView.DismissRequested -= HandleDismissRequested;
    }

    private void VerifyReferences()
    {
        if (contextMenuView == null)
            throw new MissingReferenceException($"{name}/ContextMenuView is missing.");
    }

    private void EnsureBound()
    {
        VerifyReferences();
        Bind();
    }

    private bool TryBind()
    {
        if (contextMenuView == null)
            return false;

        Bind();
        return true;
    }

    private void Bind()
    {
        if (bound)
            return;

        contextMenuView.CommandSelected -= HandleCommandSelected;
        contextMenuView.CommandSelected += HandleCommandSelected;
        contextMenuView.DismissRequested -= HandleDismissRequested;
        contextMenuView.DismissRequested += HandleDismissRequested;
        bound = true;
    }

    private void HandleCommandSelected(IContextMenuCommand command)
    {
        CommandSelected?.Invoke(command);
    }

    private void HandleDismissRequested(PointerEventData eventData)
    {
        DismissRequested?.Invoke(eventData);
    }
}
