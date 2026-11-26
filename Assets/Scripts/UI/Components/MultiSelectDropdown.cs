using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Presents an attached checkbox dropdown whose selections apply without closing its list.
/// </summary>
public sealed class MultiSelectDropdown : MonoBehaviour
{
    private const int _rowHeight = 32;
    private const int _visibleRows = 8;

    [SerializeField]
    private Button control;

    [SerializeField]
    private TMP_Text caption;

    [SerializeField]
    private RectTransform popup;

    [SerializeField]
    private ScrollAreaLayout scroll;

    [SerializeField]
    private UICheckboxView rowTemplate;

    [SerializeField]
    private Button clear;

    private ListView<UICheckboxView> list;
    private readonly Dictionary<
        UICheckboxView,
        (UnityAction<bool> Changed, Action Selected)
    > handlers = new();
    private IReadOnlyList<UICheckboxView> Rows => list.Rows;
    private readonly HashSet<string> selected = new(StringComparer.OrdinalIgnoreCase);
    private KeyValuePair<string, string>[] options = Array.Empty<KeyValuePair<string, string>>();
    private string emptyCaption;
    private GameObject blocker;
    private bool bound;
    public event Action Changed;
    public IReadOnlyCollection<string> SelectedValues => selected;
    public bool IsExpanded => popup && popup.gameObject.activeSelf;

    /// <summary>
    /// Connects the authored dropdown once its references are available.
    /// </summary>
    private void Awake() => Bind();

    /// <summary>
    /// Connects controls for runtime interaction and native authoring previews.
    /// </summary>
    private void Bind()
    {
        if (bound)
            return;
        if (
            control == null
            || caption == null
            || popup == null
            || scroll == null
            || rowTemplate == null
            || clear == null
        )
            throw new MissingReferenceException(
                name + " has incomplete multi-select dropdown references."
            );
        list = new ListView<UICheckboxView>(
            scroll.ScrollArea,
            rowTemplate,
            "Option",
            BindRow,
            UnbindRow
        );
        control.onClick.AddListener(TogglePopup);
        clear.onClick.AddListener(ClearSelection);
        bound = true;
    }

    /// <summary>
    /// Replaces option metadata while retaining the control’s selected values.
    /// </summary>
    /// <param name="choices">The available option keys and display names.</param>
    /// <param name="empty">The caption for an empty selection.</param>
    public void SetOptions(IReadOnlyDictionary<string, string> choices, string empty)
    {
        Bind();
        options = choices
            .OrderBy(option => option.Value, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        emptyCaption = empty;
        RenderCaption();
        if (IsExpanded)
            RenderRows();
    }

    /// <summary>
    /// Removes values excluded by a related selector without emitting another user action.
    /// </summary>
    /// <param name="values">The values that may no longer be selected.</param>
    public void DeselectWithoutNotify(IEnumerable<string> values)
    {
        int count = selected.Count;
        selected.ExceptWith(values);
        if (count == selected.Count)
            return;
        RenderCaption();
        if (IsExpanded)
            RenderRows();
    }

    /// <summary>
    /// Resets the selection as part of a containing form's single clear action.
    /// </summary>
    public void ClearWithoutNotify()
    {
        selected.Clear();
        RenderCaption();
        if (IsExpanded)
            RenderRows();
    }

    /// <summary>
    /// Controls availability and closes the popup when its owner locks input.
    /// </summary>
    /// <param name="value">Whether the dropdown accepts input.</param>
    public void SetInteractable(bool value)
    {
        control.interactable = value;
        if (!value)
            Hide();
    }

    /// <summary>
    /// Dismisses an open list before its containing screen handles Escape.
    /// </summary>
    /// <returns>Whether an expanded list was closed.</returns>
    public bool TryCancel()
    {
        if (!IsExpanded)
            return false;
        Hide();
        return true;
    }

    /// <summary>
    /// Opens the checkbox list below its field or closes an already open list.
    /// </summary>
    private void TogglePopup()
    {
        if (IsExpanded)
        {
            Hide();
            return;
        }
        if (!control.interactable)
            return;
        Canvas root = GetComponentInParent<Canvas>().rootCanvas;
        Canvas popupCanvas = popup.GetComponent<Canvas>();
        popupCanvas.sortingLayerID = root.sortingLayerID;
        popup.gameObject.SetActive(true);
        RenderRows();
        scroll.SetContentHeight(options.Length * _rowHeight, true);
        CreateBlocker(root, popupCanvas.sortingOrder - 1);
        if (Rows.Count > 0 && EventSystem.current != null)
            EventSystem.current.SetSelectedGameObject(Rows[0].gameObject);
    }

    /// <summary>
    /// Reuses compact checkbox rows and fits the attached list to the available canvas space.
    /// </summary>
    private void RenderRows()
    {
        int width = Mathf.RoundToInt(((RectTransform)transform).rect.width);
        int height = Mathf.Min(options.Length, _visibleRows) * _rowHeight + 48;
        UILayout.SetSourceRect(
            popup,
            0,
            Mathf.RoundToInt(((RectTransform)transform).rect.height) + 4,
            width,
            height
        );
        UILayout.SetSourceRect((RectTransform)scroll.transform, 8, 8, width - 16, height - 48);
        UILayout.SetSourceRect((RectTransform)clear.transform, 8, height - 36, width - 16, 28);
        scroll.SetContentHeight(options.Length * _rowHeight);
        int rowWidth = Mathf.RoundToInt(scroll.ScrollArea.ViewportWidth);
        list.Render(
            options,
            options.Length * _rowHeight,
            _rowHeight,
            false,
            _rowHeight,
            (row, option, index) =>
            {
                UILayout.SetSourceRect(
                    (RectTransform)row.transform,
                    0,
                    index * _rowHeight,
                    rowWidth,
                    _rowHeight
                );
                TMP_Text label = row.GetComponentInChildren<TMP_Text>(true);
                label.text = option.Value;
                UILayout.SetSourceRect(label.rectTransform, 32, 0, rowWidth - 36, _rowHeight);
                row.SetIsCheckedWithoutNotify(selected.Contains(option.Key));
                Navigation navigation = new()
                {
                    mode = Navigation.Mode.Explicit,
                    selectOnUp = index == 0 ? control : Rows[index - 1].GetComponent<Toggle>(),
                    selectOnDown = clear,
                };
                row.GetComponent<Toggle>().navigation = navigation;
                if (index > 0)
                {
                    Toggle previous = Rows[index - 1].GetComponent<Toggle>();
                    navigation = previous.navigation;
                    navigation.selectOnDown = row.GetComponent<Toggle>();
                    previous.navigation = navigation;
                }
            }
        );
        clear.interactable = selected.Count > 0;
        RectTransform canvas = (RectTransform)GetComponentInParent<Canvas>().rootCanvas.transform;
        Vector3 bottom = canvas.InverseTransformPoint(popup.TransformPoint(popup.rect.min));
        if (bottom.y < canvas.rect.yMin + 8)
            UILayout.SetSourceRect(popup, 0, -height - 4, width, height);
    }

    /// <summary>
    /// Binds an option row to its current list position.
    /// </summary>
    /// <param name="row">The new checkbox row.</param>
    /// <param name="index">Its option position.</param>
    private void BindRow(UICheckboxView row, int index)
    {
        UnityAction<bool> changed = value => ChangeOption(index, value);
        Action selectedRow = () =>
            scroll.ScrollArea.RevealContentRect(index * _rowHeight, _rowHeight);
        handlers.Add(row, (changed, selectedRow));
        row.GetComponent<Toggle>().onValueChanged.AddListener(changed);
        row.Selected += selectedRow;
    }

    /// <summary>
    /// Releases a removed checkbox row's event handler.
    /// </summary>
    /// <param name="row">The removed checkbox row.</param>
    private void UnbindRow(UICheckboxView row)
    {
        row.GetComponent<Toggle>().onValueChanged.RemoveListener(handlers[row].Changed);
        row.Selected -= handlers[row].Selected;
        handlers.Remove(row);
    }

    /// <summary>
    /// Applies one checkbox change while retaining the expanded list.
    /// </summary>
    /// <param name="index">The option's current row index.</param>
    /// <param name="value">Whether the option is checked.</param>
    private void ChangeOption(int index, bool value)
    {
        if (index >= options.Length)
            return;
        if (value)
            selected.Add(options[index].Key);
        else
            selected.Remove(options[index].Key);
        RenderCaption();
        clear.interactable = selected.Count > 0;
        Changed?.Invoke();
    }

    /// <summary>
    /// Removes all checked values without closing the dropdown.
    /// </summary>
    private void ClearSelection()
    {
        selected.Clear();
        RenderCaption();
        RenderRows();
        Changed?.Invoke();
    }

    /// <summary>
    /// Shows readable selected names and their count on the closed field.
    /// </summary>
    private void RenderCaption() =>
        caption.text =
            selected.Count == 0
                ? emptyCaption
                : "("
                    + selected.Count
                    + ") "
                    + string.Join(
                        ", ",
                        options
                            .Where(option => selected.Contains(option.Key))
                            .Select(option => option.Value)
                    );

    /// <summary>
    /// Captures clicks outside the list without dimming the underlying editor.
    /// </summary>
    /// <param name="root">The canvas containing the dropdown.</param>
    /// <param name="order">The sorting order immediately behind the list.</param>
    private void CreateBlocker(Canvas root, int order)
    {
        blocker = new GameObject(
            "DropdownBlocker",
            typeof(RectTransform),
            typeof(Canvas),
            typeof(GraphicRaycaster),
            typeof(Image),
            typeof(Button)
        );
        blocker.layer = root.gameObject.layer;
        RectTransform rect = (RectTransform)blocker.transform;
        rect.SetParent(root.transform, false);
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.sizeDelta = Vector2.zero;
        Canvas canvas = blocker.GetComponent<Canvas>();
        canvas.overrideSorting = true;
        canvas.sortingLayerID = root.sortingLayerID;
        canvas.sortingOrder = order;
        Image image = blocker.GetComponent<Image>();
        image.color = Color.clear;
        image.canvasRenderer.cullTransparentMesh = false;
        Button dismiss = blocker.GetComponent<Button>();
        dismiss.targetGraphic = image;
        dismiss.transition = Selectable.Transition.None;
        dismiss.onClick.AddListener(Hide);
    }

    /// <summary>
    /// Closes the list and releases its outside-click capture.
    /// </summary>
    private void Hide()
    {
        bool expanded = IsExpanded;
        if (popup != null)
            popup.gameObject.SetActive(false);
        if (blocker != null)
        {
            if (Application.isPlaying)
                Destroy(blocker);
            else
                DestroyImmediate(blocker);
            blocker = null;
        }
        if (
            expanded
            && control
            && control.interactable
            && control.IsActive()
            && EventSystem.current != null
        )
            EventSystem.current.SetSelectedGameObject(control.gameObject);
    }

    /// <summary>
    /// Removes a popup when its owning filter strip or page is hidden.
    /// </summary>
    private void OnDisable() => Hide();

    /// <summary>
    /// Releases owned control subscriptions and any remaining outside-click capture.
    /// </summary>
    private void OnDestroy()
    {
        Hide();
        if (!bound)
            return;
        control.onClick.RemoveListener(TogglePopup);
        clear.onClick.RemoveListener(ClearSelection);
        list.Clear();
    }
}
