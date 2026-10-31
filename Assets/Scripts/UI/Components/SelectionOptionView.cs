using System;
using System.Globalization;
using TMPro;
using UnityEngine;

/// <summary>
/// Presents one named selection with an optional editable quantity.
/// </summary>
public sealed class SelectionOptionView : MonoBehaviour
{
    [SerializeField]
    private UICheckboxView checkbox;

    [SerializeField]
    private TMP_InputField quantity;

    [SerializeField]
    private TMP_Text error;
    private string key;
    private int count;
    private bool bound;
    public bool HasError => !string.IsNullOrEmpty(error.text);
    public event Action<string, int> Changed;

    /// <summary>
    /// Connects the authored checkbox and input once.
    /// </summary>
    private void Awake() => Bind();

    /// <summary>
    /// Connects required authored controls without application dependencies.
    /// </summary>
    private void Bind()
    {
        if (bound)
            return;
        if (checkbox == null || quantity == null || error == null)
            throw new MissingReferenceException(name + " has incomplete selection references.");
        checkbox.ValueChanged += Select;
        quantity.onEndEdit.AddListener(SetQuantity);
        bound = true;
    }

    /// <summary>
    /// Releases the listeners owned by this row.
    /// </summary>
    private void OnDestroy()
    {
        if (!bound)
            return;
        checkbox.ValueChanged -= Select;
        quantity.onEndEdit.RemoveListener(SetQuantity);
    }

    /// <summary>
    /// Displays one option without raising a change event.
    /// </summary>
    /// <param name="id">The stable selection key.</param>
    /// <param name="caption">The readable option name.</param>
    /// <param name="amount">Its existing quantity.</param>
    /// <param name="showQuantity">Whether this selection supports quantities.</param>
    public void Render(string id, string caption, int amount, bool showQuantity)
    {
        Bind();
        key = id;
        count = amount;
        checkbox.GetComponentInChildren<TMP_Text>(true).text = caption;
        checkbox.SetIsCheckedWithoutNotify(amount > 0);
        quantity.gameObject.SetActive(showQuantity);
        quantity.SetTextWithoutNotify(amount.ToString(CultureInfo.InvariantCulture));
        error.text = string.Empty;
        float width = ((RectTransform)transform).rect.width;
        UILayout.SetSourceRect(
            (RectTransform)checkbox.transform,
            4,
            8,
            Mathf.RoundToInt(width - (showQuantity ? 126 : 8)),
            28
        );
        TMP_Text label = checkbox.GetComponentInChildren<TMP_Text>(true);
        UILayout.SetSourceRect(
            label.rectTransform,
            34,
            0,
            Mathf.RoundToInt(width - (showQuantity ? 168 : 50)),
            28
        );
        label.overflowMode = TextOverflowModes.Ellipsis;
        UILayout.SetSourceRect(
            (RectTransform)quantity.transform,
            Mathf.RoundToInt(width - 112),
            4,
            104,
            34
        );
        UILayout.SetSourceRect(error.rectTransform, 38, 40, Mathf.RoundToInt(width - 46), 20);
        gameObject.SetActive(true);
    }

    /// <summary>
    /// Commits the visible quantity before the dialog is applied.
    /// </summary>
    /// <returns>Whether the row contains a valid quantity.</returns>
    public bool Commit()
    {
        if (quantity.gameObject.activeSelf)
            SetQuantity(quantity.text);
        return !HasError;
    }

    /// <summary>
    /// Toggles selection while retaining an existing positive quantity.
    /// </summary>
    /// <param name="selected">Whether the option is checked.</param>
    private void Select(bool selected)
    {
        count = selected ? Math.Max(1, count) : 0;
        quantity.SetTextWithoutNotify(count.ToString(CultureInfo.InvariantCulture));
        error.text = string.Empty;
        Changed?.Invoke(key, count);
    }

    /// <summary>
    /// Accepts a nonnegative integer or leaves an inline error beside the draft.
    /// </summary>
    /// <param name="text">The entered quantity.</param>
    private void SetQuantity(string text)
    {
        if (
            !int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out int value)
            || value < 0
        )
        {
            error.text = "Enter zero or a positive whole number.";
            return;
        }
        error.text = string.Empty;
        count = value;
        checkbox.SetIsCheckedWithoutNotify(count > 0);
        Changed?.Invoke(key, count);
    }
}
