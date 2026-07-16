using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Renders one authored strategy dropdown row and emits its semantic selection.
/// </summary>
public sealed class StrategyDropdownItemView : MonoBehaviour
{
    [SerializeField]
    private Button button;

    [SerializeField]
    private RawImage itemImage;

    [SerializeField]
    private TextMeshProUGUI itemTextField;

    private RectInt imageSlot;
    private bool hasImageSlot;

    /// <summary>
    /// Raised when this row's authored button is selected.
    /// </summary>
    public event Action<StrategyDropdownItemView> Clicked;

    /// <summary>
    /// Gets the stable visual index assigned by the owning dropdown.
    /// </summary>
    public int Index { get; private set; }

    /// <summary>
    /// Gets the authored row height in strategy source units.
    /// </summary>
    public int Height => UILayout.GetSourceRect(transform as RectTransform).height;

    /// <summary>
    /// Verifies authored references and binds the row button.
    /// </summary>
    private void Awake()
    {
        VerifyReferences();
        button.onClick.AddListener(HandleClicked);
    }

    /// <summary>
    /// Releases the row's authored button listener.
    /// </summary>
    private void OnDestroy()
    {
        if (button != null)
            button.onClick.RemoveListener(HandleClicked);
    }

    /// <summary>
    /// Assigns the row's stable visual index.
    /// </summary>
    /// <param name="index">The zero-based dropdown index.</param>
    public void SetIndex(int index)
    {
        Index = index;
    }

    /// <summary>
    /// Applies one complete dropdown-row presentation snapshot.
    /// </summary>
    /// <param name="data">The immutable row presentation.</param>
    public void Render(StrategyDropdownItemRenderData data)
    {
        if (data == null)
            throw new ArgumentNullException(nameof(data));

        VerifyReferences();
        UILayout.SetHorizontallyCenteredImage(itemImage, data.Texture, imageSlot);
        UILayout.SetTextContent(itemTextField, data.Label, data.LabelColor);
        gameObject.SetActive(true);
    }

    /// <summary>
    /// Emits this row's semantic selection.
    /// </summary>
    private void HandleClicked()
    {
        Clicked?.Invoke(this);
    }

    /// <summary>
    /// Verifies the complete authored row hierarchy and captures its image slot once.
    /// </summary>
    private void VerifyReferences()
    {
        if (button == null)
            throw new MissingReferenceException($"{name}/Button is missing.");
        if (itemImage == null)
            throw new MissingReferenceException($"{name}/ItemImage is missing.");
        if (itemTextField == null)
            throw new MissingReferenceException($"{name}/ItemTextField is missing.");

        if (!hasImageSlot)
        {
            imageSlot = UILayout.GetSourceRect(itemImage.rectTransform);
            hasImageSlot = true;
        }
    }
}

/// <summary>
/// Contains immutable presentation data for one shared strategy dropdown row.
/// </summary>
public sealed class StrategyDropdownItemRenderData
{
    /// <summary>
    /// Creates one complete dropdown-row presentation snapshot.
    /// </summary>
    /// <param name="texture">The displayed row image.</param>
    /// <param name="label">The displayed row label.</param>
    /// <param name="labelColor">The displayed label color.</param>
    public StrategyDropdownItemRenderData(Texture texture, string label, Color32 labelColor)
    {
        Texture = texture;
        Label = label ?? string.Empty;
        LabelColor = labelColor;
    }

    /// <summary>
    /// Gets the displayed row image.
    /// </summary>
    public Texture Texture { get; }

    /// <summary>
    /// Gets the displayed row label.
    /// </summary>
    public string Label { get; }

    /// <summary>
    /// Gets the displayed label color.
    /// </summary>
    public Color32 LabelColor { get; }
}
