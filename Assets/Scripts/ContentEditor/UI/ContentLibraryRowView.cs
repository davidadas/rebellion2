using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Reuses shared list selection for installed and community content metadata.
/// </summary>
public sealed class ContentLibraryRowView : SelectableListRowView
{
    [SerializeField]
    private RawImage hitArea;

    [SerializeField]
    private TextMeshProUGUI title;

    [SerializeField]
    private TextMeshProUGUI summary;

    [SerializeField]
    private TextMeshProUGUI status;
    public string Key { get; private set; }

    /// <summary>
    /// Applies a cached metadata row using the current editor palette and shared navigation contract.
    /// </summary>
    /// <param name="row">The projected entry.</param>
    /// <param name="index">The list position.</param>
    /// <param name="selected">Whether this entry is selected.</param>
    public void Render(ContentLibraryRow row, int index, bool selected)
    {
        gameObject.SetActive(true);
        Key = row.Key;
        ConfigureSelectableRow(index, hitArea);
        title.SetText(row.Name ?? string.Empty);
        title
            .GetComponent<ContentEditorColorBinding>()
            .SetRole(selected ? ContentEditorColorRole.Accent : ContentEditorColorRole.Text);
        summary
            .GetComponent<ContentEditorColorBinding>()
            .SetRole(selected ? ContentEditorColorRole.Text : ContentEditorColorRole.Muted);
        status
            .GetComponent<ContentEditorColorBinding>()
            .SetRole(selected ? ContentEditorColorRole.Text : ContentEditorColorRole.Muted);
        summary.SetText(row.Summary ?? string.Empty);
        status.SetText(row.Status ?? string.Empty);
        ContentEditorColorBinding binding = hitArea.GetComponent<ContentEditorColorBinding>();
        binding.SetRole(
            selected ? ContentEditorColorRole.Selection : ContentEditorColorRole.Transparent
        );
    }
}
