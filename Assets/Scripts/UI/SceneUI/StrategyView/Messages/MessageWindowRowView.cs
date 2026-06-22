using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class MessageWindowRowView : SelectableListRowView
{
    [SerializeField]
    private RawImage hitAreaImage;

    [SerializeField]
    private RawImage selectionImage;

    [SerializeField]
    private RawImage iconImage;

    [SerializeField]
    private TextMeshProUGUI headerTextField;

    public void Render(
        MessageWindowRowRenderData data,
        Texture selectionTexture,
        Texture iconTexture,
        Color32 headerColor
    )
    {
        VerifyReferences();

        ConfigureSelectableRow(data.Index, hitAreaImage);
        SetSelectionImage(selectionImage, data.Selected ? selectionTexture : null);
        SetImage(iconImage, iconTexture);
        UILayout.SetTextContent(headerTextField, data.Header, headerColor);
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
        if (selectionImage == null)
            throw new MissingReferenceException($"{name}/SelectionImage is missing.");
        if (iconImage == null)
            throw new MissingReferenceException($"{name}/IconImage is missing.");
        if (headerTextField == null)
            throw new MissingReferenceException($"{name}/HeaderTextField is missing.");
    }

    private static void SetImage(RawImage image, Texture texture)
    {
        RectInt rect = UILayout.GetSourceRect(image.rectTransform);
        UILayout.SetImage(image, texture, rect.x, rect.y);
    }

    private static void SetSelectionImage(RawImage image, Texture texture)
    {
        UILayout.SetImage(image, texture, 0, 0);
    }
}
