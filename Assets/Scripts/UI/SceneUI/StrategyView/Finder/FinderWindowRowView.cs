using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class FinderWindowRowView : SelectableListRowView
{
    [SerializeField]
    private RawImage hitAreaImage;

    [SerializeField]
    private TextMeshProUGUI nameTextField;

    [SerializeField]
    private TextMeshProUGUI[] countTextFields = System.Array.Empty<TextMeshProUGUI>();

    public void SetPreferredHeight(int height)
    {
        LayoutElement layoutElement = GetComponent<LayoutElement>();
        if (layoutElement != null)
            layoutElement.preferredHeight = height;
    }

    public void Render(int index, FinderWindowRowRenderData data)
    {
        VerifyReferences();

        ConfigureSelectableRow(index, hitAreaImage);
        UILayout.SetTextContent(nameTextField, data.Name, data.Color);

        int count = Mathf.Min(data.Counts?.Count ?? 0, countTextFields.Length);
        for (int i = 0; i < count; i++)
        {
            TextMeshProUGUI text = countTextFields[i];
            UILayout.SetTextContent(text, data.Counts[i].Text, Color.white);
        }

        for (int i = count; i < countTextFields.Length; i++)
            countTextFields[i].gameObject.SetActive(false);

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
        if (countTextFields == null || countTextFields.Length == 0)
            throw new MissingReferenceException($"{name}/CountTextFields are missing.");
    }
}
