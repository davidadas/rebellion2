using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Offers the same one-position move commands to pointer, keyboard, and controller users.
/// </summary>
public sealed class CardReorderMenu
{
    private readonly RectTransform _surface;
    private readonly Button _template;
    private readonly int _width;
    private readonly int _commandHeight;
    private GameObject _root;
    private Image _panel;
    private Button _earlier;
    private Button _later;
    private Button _source;
    private Action<int> _move;

    /// <summary>
    /// Sets up a movement menu using buttons from the owning screen.
    /// </summary>
    /// <param name="surface">The screen overlay hosting the menu.</param>
    /// <param name="template">The button whose artwork and text style are copied.</param>
    /// <param name="width">The width of the popup.</param>
    /// <param name="commandHeight">The height of each command.</param>
    public CardReorderMenu(
        RectTransform surface,
        Button template,
        int width = 104,
        int commandHeight = 22
    )
    {
        _surface = surface;
        _template = template;
        _width = width;
        _commandHeight = commandHeight;
    }

    /// <summary>
    /// Opens the available move commands beside the initiating grip or card.
    /// </summary>
    /// <param name="source">The button that opened the menu.</param>
    /// <param name="index">The current position of the item.</param>
    /// <param name="count">The number of movable items.</param>
    /// <param name="background">The popup's background color.</param>
    /// <param name="onMove">Receives the requested new index.</param>
    public void Show(Button source, int index, int count, Color background, Action<int> onMove)
    {
        if (_root == null)
            Build();

        RectTransform anchor = (RectTransform)source.transform;
        Vector2 bottom = _surface.InverseTransformPoint(anchor.TransformPoint(anchor.rect.min));
        int height = 10 + 2 * _commandHeight;
        int x = Mathf.Clamp(
            Mathf.RoundToInt(bottom.x - _surface.rect.xMin),
            0,
            Mathf.Max(0, Mathf.FloorToInt(_surface.rect.width) - _width)
        );
        int y = Mathf.RoundToInt(_surface.rect.yMax - bottom.y);
        if (y + height > _surface.rect.height)
            y = Mathf.Max(0, y - Mathf.RoundToInt(anchor.rect.height) - height);
        UILayout.SetSourceRect(_panel.rectTransform, x, y, _width, height);
        _panel.color = background;
        _source = source;
        _move = direction => onMove(index + direction);
        _earlier.interactable = index > 0;
        _later.interactable = index < count - 1;
        _root.transform.SetAsLastSibling();
        _root.SetActive(true);
        (_earlier.interactable ? _earlier : _later).Select();
    }

    /// <summary>
    /// Dismisses the menu and restores focus to its source.
    /// </summary>
    /// <returns>Whether a menu was open.</returns>
    public bool TryCancel()
    {
        if (_source == null)
            return false;
        _source.Select();
        Hide();
        return true;
    }

    /// <summary>
    /// Dismisses the menu when its owning screen changes.
    /// </summary>
    public void Hide()
    {
        _source = null;
        _move = null;
        _root?.SetActive(false);
    }

    /// <summary>
    /// Creates a dismissible overlay containing the two move commands.
    /// </summary>
    private void Build()
    {
        _root = new GameObject(
            "Card move menu",
            typeof(RectTransform),
            typeof(Image),
            typeof(Button)
        );
        RectTransform overlay = (RectTransform)_root.transform;
        overlay.SetParent(_surface, false);
        overlay.anchorMin = Vector2.zero;
        overlay.anchorMax = Vector2.one;
        overlay.offsetMin = Vector2.zero;
        overlay.offsetMax = Vector2.zero;
        Image blocker = _root.GetComponent<Image>();
        blocker.color = Color.clear;
        blocker.canvasRenderer.cullTransparentMesh = false;
        Button dismiss = _root.GetComponent<Button>();
        dismiss.navigation = new Navigation { mode = Navigation.Mode.None };
        dismiss.onClick.AddListener(Dismiss);

        var panel = new GameObject("Move commands", typeof(RectTransform), typeof(Image));
        panel.transform.SetParent(overlay, false);
        _panel = panel.GetComponent<Image>();
        _earlier = CreateCommand("Move earlier", 4, -1);
        _later = CreateCommand("Move later", 6 + _commandHeight, 1);
        _earlier.navigation = new Navigation
        {
            mode = Navigation.Mode.Explicit,
            selectOnDown = _later,
        };
        _later.navigation = new Navigation
        {
            mode = Navigation.Mode.Explicit,
            selectOnUp = _earlier,
        };
        _root.SetActive(false);
    }

    /// <summary>
    /// Copies the host button's style for a single movement command.
    /// </summary>
    /// <param name="label">The command's visible label.</param>
    /// <param name="y">The top of the command in the popup.</param>
    /// <param name="direction">The direction in which the command moves the item.</param>
    /// <returns>The focusable command button.</returns>
    private Button CreateCommand(string label, int y, int direction)
    {
        Button button = UnityEngine.Object.Instantiate(_template, _panel.transform, false);
        button.name = label;
        UILayout.SetSourceRect((RectTransform)button.transform, 4, y, _width - 8, _commandHeight);
        UILayout.SetTextContent(button.GetComponentInChildren<TextMeshProUGUI>(true), label);
        button.onClick.AddListener(() => Move(direction));
        return button;
    }

    /// <summary>
    /// Returns focus to the card and dispatches the selected command.
    /// </summary>
    /// <param name="direction">The one-position offset requested by the user.</param>
    private void Move(int direction)
    {
        Action<int> move = _move;
        TryCancel();
        move?.Invoke(direction);
    }

    /// <summary>
    /// Dismisses the popup after an outside click.
    /// </summary>
    private void Dismiss() => TryCancel();
}
