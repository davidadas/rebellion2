using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Presents one reusable package card with artwork, metadata, and an explicit selection state.
/// </summary>
public sealed class ContentEditorModCardView : MonoBehaviour
{
    [SerializeField]
    private Button button;

    [SerializeField]
    private RawImage artwork;

    [SerializeField]
    private TextMeshProUGUI initials;

    [SerializeField]
    private TextMeshProUGUI caption;

    [SerializeField]
    private TextMeshProUGUI author;

    [SerializeField]
    private TextMeshProUGUI description;

    [SerializeField]
    private TextMeshProUGUI metadata;

    [SerializeField]
    private TextMeshProUGUI state;

    [SerializeField]
    private Image selectionStripe;
    private Action<string> select;
    private bool bound;
    internal ContentEditorModEntry Entry { get; private set; }

    /// <summary>
    /// Binds a card without retaining a callback or image belonging to its previous package.
    /// </summary>
    /// <param name="entry">The combined public listing and installation state.</param>
    /// <param name="selected">Whether this package is selected.</param>
    /// <param name="interactive">Whether another operation permits selection.</param>
    /// <param name="onSelect">The package-selection callback.</param>
    /// <param name="texture">The cached package artwork, or null while loading.</param>
    internal void Render(
        ContentEditorModEntry entry,
        bool selected,
        bool interactive,
        Action<string> onSelect,
        Texture2D texture
    )
    {
        if (!bound)
        {
            button.onClick.AddListener(Select);
            bound = true;
        }
        Entry = entry;
        select = onSelect;
        caption.text = entry.Name;
        caption
            .GetComponent<ContentEditorColorBinding>()
            .SetRole(selected ? ContentEditorColorRole.Accent : ContentEditorColorRole.Text);
        author.text = "by " + entry.Author;
        description.text = entry.Description;
        metadata.text =
            entry.Listing == null
                ? entry.Version + " · Local content"
                : entry.Downloads.ToString("N0")
                    + " downloads"
                    + (
                        entry.Categories.Length > 0
                            ? " · " + string.Join(" · ", entry.Categories)
                            : string.Empty
                    );
        state.text = entry.Installation != null || entry.IsDeprecated ? entry.State : string.Empty;
        state
            .GetComponent<ContentEditorColorBinding>()
            .SetRole(
                entry.HasUpdate ? ContentEditorColorRole.Accent : ContentEditorColorRole.Muted
            );
        button.GetComponent<ContentEditorColorBinding>().SetRole(ContentEditorColorRole.Panel);
        selectionStripe.gameObject.SetActive(selected);
        button.interactable = interactive;
        initials.text = string.IsNullOrEmpty(entry.Name)
            ? "?"
            : entry.Name.Substring(0, Math.Min(2, entry.Name.Length)).ToUpperInvariant();
        SetArtwork(texture);
        gameObject.SetActive(true);
    }

    /// <summary>
    /// Applies a cache-owned icon without retaining a texture lifetime in the row.
    /// </summary>
    /// <param name="texture">The shared artwork, or null for the initial-letter fallback.</param>
    internal void SetArtwork(Texture2D texture)
    {
        artwork.texture = texture;
        artwork.enabled = texture != null;
        initials.gameObject.SetActive(texture == null);
    }

    /// <summary>
    /// Updates interaction while preserving the selected card's visual styling.
    /// </summary>
    /// <param name="value">Whether the row may be selected.</param>
    internal void SetInteractable(bool value) => button.interactable = value;

    /// <summary>
    /// Emits the current package identity through the one registered button callback.
    /// </summary>
    private void Select() => select?.Invoke(Entry.ID);

    /// <summary>
    /// Releases the button listener when the owning manager is destroyed.
    /// </summary>
    private void OnDestroy()
    {
        if (bound)
            button.onClick.RemoveListener(Select);
    }
}
