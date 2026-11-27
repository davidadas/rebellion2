using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Previews and applies the enabled mod order for one era.
/// </summary>
public sealed class ContentEditorModOrderView : MonoBehaviour, ICancelable
{
    private const int _rowPitch = SortableRowView.RowHeight + SortableRowView.Gap;

    [SerializeField]
    private RectTransform panel;

    [SerializeField]
    private ScrollAreaLayout scroll;

    [SerializeField]
    private SortableRowView rowTemplate;

    [SerializeField]
    private TMP_Text era;

    [SerializeField]
    private TMP_Text empty;

    [SerializeField]
    private TMP_Text status;

    [SerializeField]
    private Button apply;

    [SerializeField]
    private Button close;

    private readonly List<SortableRowView> rows = new();
    private IReadOnlyList<ContentModDefinition> original;
    private Dictionary<string, ContentModDefinition> modsByID;
    private Action<IReadOnlyList<string>> save;
    private SortableRowView held;
    private CardReorderDrag drag;
    private CardReorderMenu moveMenu;
    private ContentEditorAppearance appearance;
    private Canvas canvas;
    private float listScale;

    /// <summary>
    /// Wires the dialog's two footer actions.
    /// </summary>
    private void Awake()
    {
        apply.onClick.AddListener(Apply);
        close.onClick.AddListener(Close);
    }

    /// <summary>
    /// Fits the dialog within the resized toolkit window.
    /// </summary>
    private void OnRectTransformDimensionsChange()
    {
        if (panel == null)
            return;
        if (canvas != null && original != null && gameObject.activeInHierarchy)
        {
            float scale = ResolveListScale();
            if (!Mathf.Approximately(scale, listScale))
            {
                CancelDrag();
                moveMenu?.TryCancel();
                FitList(scale, resetScroll: false);
                PositionRows();
                RefreshApply();
                return;
            }
        }
        UILayout.FitPanel(panel);
    }

    /// <summary>
    /// Shows the active era and its enabled mods in application order.
    /// </summary>
    /// <param name="displayName">The selected era.</param>
    /// <param name="mods">The currently applied mod releases.</param>
    /// <param name="onApply">Called with the proposed order. A failure leaves the dialog open.</param>
    internal void Show(
        string displayName,
        IReadOnlyList<ContentModDefinition> mods,
        Action<IReadOnlyList<string>> onApply
    )
    {
        appearance = GetComponentInParent<ContentEditorAppearance>();
        canvas = GetComponentInParent<Canvas>()?.rootCanvas;
        drag ??= new CardReorderDrag(scroll.ScrollArea, (RectTransform)transform)
        {
            CheckMove = CheckMove,
            ShowRestriction = restriction => status.text = restriction ?? string.Empty,
        };
        moveMenu ??= new CardReorderMenu((RectTransform)transform, close, 152, 30);
        original = mods;
        modsByID = mods.ToDictionary(mod => mod.ID, StringComparer.Ordinal);
        save = onApply;
        era.text = displayName;
        status.text = string.Empty;
        empty.gameObject.SetActive(mods.Count == 0);
        gameObject.SetActive(true);
        transform.SetAsLastSibling();
        FitList(ResolveListScale(), resetScroll: true);
        foreach (ContentModDefinition mod in mods)
        {
            SortableRowView row = Instantiate(rowTemplate, scroll.ScrollArea.ContentRoot);
            row.Render(mod.ID, mod.DisplayName, mod.Version, string.Empty, mods.Count > 1);
            row.DragStarted += input => BeginDrag(row, input);
            row.Dragged += input => drag.PointAt(row.Rect, input.position);
            row.DragEnded += input => EndDrag(row, input);
            row.onClick.AddListener(() => ShowMoveMenu(row));
            row.gameObject.SetActive(true);
            rows.Add(row);
        }
        PositionRows();
        RefreshApply();
        close.Select();
    }

    /// <summary>
    /// Converts the game menu's row scale to units on the editor canvas.
    /// </summary>
    /// <returns>The editor list's scale relative to its dialog.</returns>
    private float ResolveListScale()
    {
        if (canvas == null || canvas.renderMode == RenderMode.WorldSpace)
            return UILayout.ScaleForMenuContent(UILayout.ContentEditorReferenceSize);
        return UILayout.ScaleForMenuContent(canvas.pixelRect.size) / canvas.scaleFactor;
    }

    /// <summary>
    /// Fits the converted list in the dialog while preserving the shared row coordinates.
    /// </summary>
    /// <param name="scale">The scale from row units to editor dialog units.</param>
    /// <param name="resetScroll">Whether to return to the top of the list.</param>
    private void FitList(float scale, bool resetScroll)
    {
        listScale = scale;
        int contentHeight = original.Count * _rowPitch;
        panel.sizeDelta = new Vector2(680, Mathf.Clamp(259 + contentHeight * scale, 325, 579));
        UILayout.FitPanel(panel);

        var rect = (RectTransform)scroll.transform;
        Vector2 bounds = ((RectTransform)rect.parent).rect.size / scale;
        UILayout.SetSourceRect(rect, 0, 0, Mathf.RoundToInt(bounds.x), Mathf.RoundToInt(bounds.y));
        rect.localScale = new Vector3(scale, scale, 1f);
        scroll.SetContentHeight(contentHeight, resetScroll);
    }

    /// <summary>
    /// Handles Escape within the dialog.
    /// </summary>
    /// <returns>Whether the dialog handled Escape.</returns>
    public bool TryCancel()
    {
        if (!gameObject.activeSelf)
            return false;
        if (held != null)
        {
            CancelDrag();
            RefreshApply();
        }
        else if (!moveMenu.TryCancel())
            Close();
        return true;
    }

    /// <summary>
    /// Starts a floating card at the point where it was grabbed.
    /// </summary>
    /// <param name="row">The held card.</param>
    /// <param name="eventData">The initiating pointer.</param>
    private void BeginDrag(SortableRowView row, PointerEventData eventData)
    {
        if (held != null)
            return;
        moveMenu.Hide();
        held = row;
        status.text = string.Empty;
        drag.Accent = ColorFor(ContentEditorColorRole.Accent);
        drag.Error = ColorFor(ContentEditorColorRole.Error);
        drag.Begin(rows, rows.IndexOf(row), eventData);
        RefreshApply();
    }

    /// <summary>
    /// Drops a card and stages its final position.
    /// </summary>
    /// <param name="row">The released row.</param>
    /// <param name="eventData">The final pointer position.</param>
    private void EndDrag(SortableRowView row, PointerEventData eventData)
    {
        if (row != held)
            return;
        int destination = drag.Drop(row.Rect, eventData.position);
        CancelDrag();
        if (destination >= 0)
            Move(row, destination);
        RefreshApply();
    }

    /// <summary>
    /// Offers one-position move commands on click or keyboard submit.
    /// </summary>
    /// <param name="row">The card requesting movement.</param>
    private void ShowMoveMenu(SortableRowView row)
    {
        if (held != null)
            return;
        moveMenu.Show(
            row,
            rows.IndexOf(row),
            rows.Count,
            ColorFor(ContentEditorColorRole.Panel),
            destination =>
            {
                if (Move(row, destination))
                    scroll.ScrollArea.RevealContentRect(
                        destination * _rowPitch,
                        SortableRowView.RowHeight
                    );
            }
        );
    }

    /// <summary>
    /// Resolves a reorder color from the currently selected editor palette.
    /// </summary>
    /// <param name="role">The color's role in the interaction.</param>
    /// <returns>The color for the role.</returns>
    private Color ColorFor(ContentEditorColorRole role) =>
        appearance?.ColorFor(role)
        ?? ContentEditorAppearance.ColorFor(ContentEditorColorMode.Dark, role);

    /// <summary>
    /// Animates the shared drag preview and scrolls near an edge.
    /// </summary>
    private void Update() => drag.Update(Time.unscaledDeltaTime);

    /// <summary>
    /// Clears the temporary interaction when the dialog is hidden.
    /// </summary>
    private void OnDisable()
    {
        CancelDrag();
        moveMenu?.Hide();
    }

    /// <summary>
    /// Restores the held card and its idle presentation.
    /// </summary>
    private void CancelDrag()
    {
        if (held == null)
            return;
        drag.Cancel();
        held = null;
    }

    /// <summary>
    /// Gets the author rule that prevents a move, if any.
    /// </summary>
    /// <param name="origin">The current index.</param>
    /// <param name="destination">The requested index.</param>
    /// <returns>The reason a move is blocked or an empty result.</returns>
    private string CheckMove(int origin, int destination) =>
        ContentModLoadOrder.GetMoveRestriction(
            rows.Select(item => modsByID[item.ItemID]).ToArray(),
            origin,
            destination
        );

    /// <summary>
    /// Stages a valid move and immediately positions the affected cards.
    /// </summary>
    /// <param name="row">The moving card.</param>
    /// <param name="destination">Its proposed zero-based position.</param>
    /// <returns>Whether the move was staged.</returns>
    private bool Move(SortableRowView row, int destination)
    {
        int origin = rows.IndexOf(row);
        string restriction = CheckMove(origin, destination);
        status.text = restriction ?? string.Empty;
        if (!string.IsNullOrEmpty(restriction))
            return false;
        rows.RemoveAt(origin);
        rows.Insert(destination, row);
        PositionRows();
        RefreshApply();
        return true;
    }

    /// <summary>
    /// Sets each card's bounds in the proposed order.
    /// </summary>
    private void PositionRows()
    {
        int width = Mathf.FloorToInt(scroll.ScrollArea.ViewportWidth);
        for (int index = 0; index < rows.Count; index++)
            rows[index].Place(0, index * _rowPitch, width);
    }

    /// <summary>
    /// Enables Apply once the player drops a card in a different order.
    /// </summary>
    private void RefreshApply() =>
        apply.interactable =
            held == null
            && !rows.Select(row => row.ItemID).SequenceEqual(original.Select(mod => mod.ID));

    /// <summary>
    /// Commits the proposed order and keeps validation errors next to its controls.
    /// </summary>
    private void Apply()
    {
        try
        {
            save(rows.Select(row => row.ItemID).ToArray());
            Close();
        }
        catch (Exception exception)
        {
            status.text = exception.Message;
        }
    }

    /// <summary>
    /// Releases the current list and dismisses the dialog.
    /// </summary>
    internal void Close()
    {
        if (!gameObject.activeSelf)
            return;
        CancelDrag();
        moveMenu.Hide();
        foreach (SortableRowView row in rows)
        {
            row.gameObject.SetActive(false);
            if (Application.isPlaying)
                Destroy(row.gameObject);
            else
                DestroyImmediate(row.gameObject);
        }
        rows.Clear();
        original = null;
        save = null;
        gameObject.SetActive(false);
    }
}
