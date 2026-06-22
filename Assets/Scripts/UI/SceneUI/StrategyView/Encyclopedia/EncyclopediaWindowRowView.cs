using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class EncyclopediaWindowRowView : SelectableListRowView
{
    [SerializeField]
    private RawImage hitAreaImage;

    [SerializeField]
    private TextMeshProUGUI nameTextField;

    public void Render(int index, EncyclopediaWindowRowRenderData data, TextMeshProUGUI template)
    {
        VerifyReferences();

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

    private void Awake()
    {
        VerifyReferences();
    }

    private void VerifyReferences()
    {
        if (hitAreaImage == null)
            throw new MissingReferenceException($"{name}/HitAreaImage is missing.");
        if (nameTextField == null)
            throw new MissingReferenceException($"{name}/NameTextField is missing.");
    }
}
