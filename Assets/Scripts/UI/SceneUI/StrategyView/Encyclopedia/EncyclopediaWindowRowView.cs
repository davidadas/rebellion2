using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Renders one selectable row in the Encyclopedia database index.
/// </summary>
public sealed class EncyclopediaWindowRowView : SelectableListRowView
{
    [SerializeField]
    private RawImage hitAreaImage;

    [SerializeField]
    private TextMeshProUGUI nameTextField;

    /// <summary>
    /// Gets the stable catalog identifier represented by the current row presentation.
    /// </summary>
    public string EntryTypeId { get; private set; } = string.Empty;

    /// <summary>
    /// Validates authored row references when the prefab instance awakens.
    /// </summary>
    private void Awake()
    {
        VerifyReferences();
    }

    /// <summary>
    /// Applies immutable row presentation data using the authored text template.
    /// </summary>
    /// <param name="index">The projected row index.</param>
    /// <param name="data">The immutable row presentation data.</param>
    /// <param name="template">The authored row text template.</param>
    public void Render(int index, EncyclopediaWindowRowRenderData data, TextMeshProUGUI template)
    {
        VerifyReferences();

        EntryTypeId = data.EntryTypeId;
        ConfigureSelectableRow(index, hitAreaImage);
        UILayout.SetTemplateText(
            nameTextField,
            template,
            data.Name,
            data.Selected ? Color.white : Color.gray,
            UILayout.GetSourceRect(template.rectTransform)
        );
        gameObject.SetActive(true);
    }

    /// <summary>
    /// Verifies every authored child reference required to render the row.
    /// </summary>
    private void VerifyReferences()
    {
        if (hitAreaImage == null)
            throw new MissingReferenceException($"{name}/HitAreaImage is missing.");
        if (nameTextField == null)
            throw new MissingReferenceException($"{name}/NameTextField is missing.");
    }
}
