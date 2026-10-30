using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Presents a named catalog entry using shared selectable-list navigation.
/// </summary>
public sealed class ContentEditorRecordRowView : SelectableListRowView
{
    [SerializeField]
    private RawImage hitArea;

    [SerializeField]
    private TextMeshProUGUI caption;

    [SerializeField]
    private TextMeshProUGUI disclosure;

    [SerializeField]
    private Button disclosureButton;

    [SerializeField]
    private RectTransform thumbnailSlot;

    [SerializeField]
    private RawImage thumbnail;
    private Action<ContentEditorTreeNode> expandRequested;
    private bool bound;
    public ContentEditorTreeNode Node { get; private set; }

    /// <summary>
    /// Keeps branch expansion separate from row selection.
    /// </summary>
    private void Awake()
    {
        BindControls();
    }

    /// <summary>
    /// Binds disclosure actions once for both authored previews and the player.
    /// </summary>
    private void BindControls()
    {
        if (bound)
            return;
        if (
            hitArea == null
            || caption == null
            || disclosure == null
            || disclosureButton == null
            || thumbnailSlot == null
            || thumbnail == null
        )
            throw new MissingReferenceException(name + " has incomplete content-row references.");
        disclosureButton.onClick.AddListener(() => expandRequested?.Invoke(Node));
        bound = true;
    }

    /// <summary>
    /// Releases document and image references when a pooled row is hidden.
    /// </summary>
    private void OnDisable()
    {
        Node = null;
        expandRequested = null;
        if (thumbnail != null)
            thumbnail.texture = null;
    }

    /// <summary>
    /// Binds a catalog entry and selection state to the row.
    /// </summary>
    /// <param name="node">The represented hierarchy node.</param>
    /// <param name="index">The source index.</param>
    /// <param name="selected">Whether this record is selected.</param>
    /// <param name="expanded">Whether the branch is expanded.</param>
    /// <param name="assets">The project's media store.</param>
    /// <param name="onExpand">The branch expansion request.</param>
    public void Render(
        ContentEditorTreeNode node,
        int index,
        bool selected,
        bool expanded,
        ContentAssets assets,
        Action<ContentEditorTreeNode> onExpand
    )
    {
        BindControls();
        Node = node;
        expandRequested = onExpand;
        caption.text = node.Caption;
        caption
            .GetComponent<ContentEditorColorBinding>()
            .SetRole(
                node.CreationCatalog != null
                    ? ContentEditorColorRole.Accent
                    : ContentEditorColorRole.Text
            );
        float indent = 10 + node.Depth * 18;
        disclosureButton.gameObject.SetActive(node.Children.Count > 0 && onExpand != null);
        ((RectTransform)disclosureButton.transform).anchoredPosition = new Vector2(indent, -3);
        disclosure.rectTransform.localRotation = Quaternion.Euler(0, 0, expanded ? -90 : 0);
        string address = node.Record?.ImageAddress;
        try
        {
            thumbnail.texture = string.IsNullOrEmpty(address) ? null : assets.GetTexture(address);
        }
        catch (System.ArgumentException)
        {
            thumbnail.texture = null;
        }
        thumbnail.enabled = thumbnail.texture != null;
        thumbnailSlot.anchoredPosition = new Vector2(indent + 22, -6);
        if (thumbnail.texture != null)
            thumbnail.GetComponent<AspectRatioFitter>().aspectRatio =
                (float)thumbnail.texture.width / thumbnail.texture.height;
        caption.rectTransform.offsetMin = new Vector2(indent + (thumbnail.enabled ? 62 : 24), 0);
        hitArea
            .GetComponent<ContentEditorColorBinding>()
            .SetRole(
                selected ? ContentEditorColorRole.Selection : ContentEditorColorRole.Transparent
            );
        ConfigureSelectableRow(index, hitArea);
        gameObject.SetActive(true);
    }
}
