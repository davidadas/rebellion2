using TMPro;

/// <summary>
/// Keeps a runtime dropdown label with the value selected by that label.
/// </summary>
/// <typeparam name="T">The semantic option value.</typeparam>
public sealed class DropdownOption<T> : TMP_Dropdown.OptionData
{
    public T Value { get; }

    /// <summary>
    /// Creates an option whose meaning is independent of its position in the list.
    /// </summary>
    /// <param name="value">The value selected by this option.</param>
    /// <param name="label">The displayed option label.</param>
    public DropdownOption(T value, string label)
        : base(label) => Value = value;
}
