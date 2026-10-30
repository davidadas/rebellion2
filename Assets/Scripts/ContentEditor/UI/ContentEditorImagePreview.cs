using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Inspects artwork at its authored aspect ratio without covering the current form fields.
/// </summary>
public sealed class ContentEditorImagePreview : MonoBehaviour, ICancelable
{
    [SerializeField]
    private RawImage image;

    [SerializeField]
    private TextMeshProUGUI caption;

    [SerializeField]
    private Button closeButton;
    private bool bound;

    /// <summary>
    /// Shows one cached texture in the focused artwork viewer.
    /// </summary>
    /// <param name="texture">The resolved artwork.</param>
    /// <param name="address">Its content address.</param>
    public void Show(Texture2D texture, string address)
    {
        if (!bound)
        {
            closeButton.onClick.AddListener(() => TryCancel());
            bound = true;
        }
        image.texture = texture;
        image.GetComponent<AspectRatioFitter>().aspectRatio = (float)texture.width / texture.height;
        caption.text = address + "  /  " + texture.width + " x " + texture.height;
        gameObject.SetActive(true);
        transform.SetAsLastSibling();
    }

    /// <summary>
    /// Dismisses the artwork viewer without changing its authored asset reference.
    /// </summary>
    /// <returns>True when the viewer was dismissed.</returns>
    public bool TryCancel()
    {
        if (!gameObject.activeSelf)
            return false;
        image.texture = null;
        gameObject.SetActive(false);
        return true;
    }
}
