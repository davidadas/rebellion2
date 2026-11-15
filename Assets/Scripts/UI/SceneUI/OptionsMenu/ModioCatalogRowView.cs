using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Displays one community mod using the Options menu's text and action controls.
/// </summary>
public sealed class ModioCatalogRowView : MonoBehaviour
{
    [SerializeField]
    private TextMeshProUGUI _nameText;

    [SerializeField]
    private TextMeshProUGUI _summaryText;

    [SerializeField]
    private Button _installButton;

    [SerializeField]
    private TextMeshProUGUI _actionText;

    private int _index;
    private bool _bound;

    public event Action<int> InstallRequested;

    /// <summary>
    /// Renders one catalog entry and its currently available installation action.
    /// </summary>
    /// <param name="index">The entry's index on the current catalog page.</param>
    /// <param name="row">The entry's presentation.</param>
    /// <param name="busy">Whether another browser operation is running.</param>
    public void Render(int index, ModioBrowserRow row, bool busy)
    {
        _index = index;
        if (!_bound)
        {
            _installButton.onClick.AddListener(HandleInstall);
            _bound = true;
        }
        UILayout.SetTextContent(
            _nameText,
            string.IsNullOrWhiteSpace(row.Version) ? row.Name : $"{row.Name} · {row.Version}"
        );
        UILayout.SetTextContent(_summaryText, row.Summary ?? string.Empty);
        UILayout.SetTextContent(_actionText, row.Action);
        _installButton.interactable = row.CanInstall && !busy;
    }

    /// <summary>
    /// Emits the current row index when its action is selected.
    /// </summary>
    private void HandleInstall() => InstallRequested?.Invoke(_index);
}
