using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Renders one reusable authored Finder result row and exposes shared list interaction.
/// </summary>
public sealed class FinderWindowRowView : SelectableListRowView
{
    [SerializeField]
    private RawImage hitAreaImage;

    [SerializeField]
    private LayoutElement layoutElement;

    [SerializeField]
    private Color selectedNameColor = Color.white;

    [SerializeField]
    private Color unselectedNameColor = Color.gray;

    [SerializeField]
    private TextMeshProUGUI nameTextField;

    [SerializeField]
    private TextMeshProUGUI[] countTextFields = System.Array.Empty<TextMeshProUGUI>();

    /// <summary>
    /// Gets the stable domain identity represented by the current row presentation.
    /// </summary>
    public string RowId { get; private set; } = string.Empty;

    /// <summary>
    /// Validates the authored row references before presentation begins.
    /// </summary>
    private void Awake()
    {
        VerifyReferences();
    }

    /// <summary>
    /// Applies one Finder result presentation to this reusable row.
    /// </summary>
    /// <param name="index">The visible row index.</param>
    /// <param name="data">The immutable row presentation.</param>
    /// <param name="preferredHeight">The authored result-row pitch.</param>
    public void Render(int index, FinderWindowRowRenderData data, int preferredHeight)
    {
        if (data == null)
            throw new System.ArgumentNullException(nameof(data));

        VerifyReferences();
        RowId = data.RowId;
        ConfigureSelectableRow(index, hitAreaImage);
        layoutElement.preferredHeight = preferredHeight;
        UILayout.SetTextContent(
            nameTextField,
            data.Name,
            data.Selected ? selectedNameColor : unselectedNameColor
        );
        RenderCounts(data.Counts);
        gameObject.SetActive(true);
    }

    /// <summary>
    /// Renders non-zero count values into the authored count columns.
    /// </summary>
    /// <param name="counts">The count text values in display order.</param>
    private void RenderCounts(System.Collections.Generic.IReadOnlyList<string> counts)
    {
        int count = Mathf.Min(counts?.Count ?? 0, countTextFields.Length);
        for (int i = 0; i < count; i++)
            UILayout.SetTextContent(countTextFields[i], counts[i]);

        for (int i = count; i < countTextFields.Length; i++)
            countTextFields[i].gameObject.SetActive(false);
    }

    /// <summary>
    /// Verifies the complete authored graph required to render and interact with a row.
    /// </summary>
    private void VerifyReferences()
    {
        if (hitAreaImage == null)
            throw new MissingReferenceException($"{name}/HitAreaImage is missing.");
        if (layoutElement == null)
            throw new MissingReferenceException($"{name}/LayoutElement is missing.");
        if (nameTextField == null)
            throw new MissingReferenceException($"{name}/NameTextField is missing.");
        if (countTextFields == null || countTextFields.Length == 0)
            throw new MissingReferenceException($"{name}/CountTextFields are missing.");

        for (int i = 0; i < countTextFields.Length; i++)
        {
            if (countTextFields[i] == null)
                throw new MissingReferenceException(
                    $"{name}/CountColumnSlot{i}TextField is missing."
                );
        }
    }
}
