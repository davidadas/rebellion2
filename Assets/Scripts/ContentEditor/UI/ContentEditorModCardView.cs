using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Presents one reusable mod card with artwork and a short description.
/// </summary>
public sealed class ContentEditorModCardView : MonoBehaviour
{
    [SerializeField]
    private Button button;

    [SerializeField]
    private Button primaryAction;

    [SerializeField]
    private Image actionInset;

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
    private TextMeshProUGUI popularity;

    private Action<string> select;
    private Action<string> install;
    private bool requiresInstallation;
    private bool bound;
    internal ContentEditorModEntry Entry { get; private set; }

    /// <summary>
    /// Binds a card without retaining a callback or image belonging to its previous package.
    /// </summary>
    /// <param name="entry">The combined public listing and installation state.</param>
    /// <param name="interactive">Whether another operation permits selection.</param>
    /// <param name="canInstall">Whether the controller can start a file operation.</param>
    /// <param name="onSelect">The package-selection callback.</param>
    /// <param name="onInstall">The callback for installing the latest online release.</param>
    /// <param name="texture">The cached package artwork, or null while loading.</param>
    internal void Render(
        ContentEditorModEntry entry,
        bool interactive,
        bool canInstall,
        Action<string> onSelect,
        Action<string> onInstall,
        Texture2D texture
    )
    {
        if (!bound)
        {
            button.onClick.AddListener(Select);
            primaryAction.onClick.AddListener(PerformPrimaryAction);
            bound = true;
        }
        Entry = entry;
        select = onSelect;
        install = onInstall;
        requiresInstallation =
            entry.Listing != null && (entry.Installation == null || entry.HasUpdate);
        actionInset.gameObject.SetActive(!requiresInstallation);
        primaryAction
            .GetComponent<ContentEditorColorBinding>()
            .SetRole(
                requiresInstallation
                    ? ContentEditorColorRole.Accent
                    : ContentEditorColorRole.Divider
            );
        primaryAction
            .GetComponentInChildren<TMP_Text>()
            .GetComponent<ContentEditorColorBinding>()
            .SetRole(
                requiresInstallation
                    ? ContentEditorColorRole.Background
                    : ContentEditorColorRole.Text
            );
        primaryAction.GetComponentInChildren<TMP_Text>().text =
            entry.HasUpdate ? "Update"
            : requiresInstallation ? "Install"
            : "Manage";
        caption.text = entry.Name;
        caption.GetComponent<ContentEditorColorBinding>().SetRole(ContentEditorColorRole.Text);
        author.text =
            "by "
            + entry.Author
            + (
                entry.Installation != null || entry.IsDeprecated
                    ? " · " + entry.State
                    : string.Empty
            );
        description.text = entry.Description;
        popularity.text =
            entry.Listing == null ? string.Empty : entry.DownloadCount + "\n" + entry.LikeCount;
        button.GetComponent<ContentEditorColorBinding>().SetRole(ContentEditorColorRole.Panel);
        SetInteractable(interactive, canInstall);
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
    /// <param name="canInstall">Whether a new installation can begin.</param>
    internal void SetInteractable(bool value, bool canInstall)
    {
        button.interactable = value;
        primaryAction.interactable = value && (!requiresInstallation || canInstall);
    }

    /// <summary>
    /// Emits the current package identity through the one registered button callback.
    /// </summary>
    private void Select() => select?.Invoke(Entry.ID);

    /// <summary>
    /// Starts an installation or opens management for an already installed package.
    /// </summary>
    private void PerformPrimaryAction()
    {
        if (requiresInstallation)
            install?.Invoke(Entry.ID);
        else
            Select();
    }

    /// <summary>
    /// Releases the button listener when the owning manager is destroyed.
    /// </summary>
    private void OnDestroy()
    {
        if (bound)
        {
            button.onClick.RemoveListener(Select);
            primaryAction.onClick.RemoveListener(PerformPrimaryAction);
        }
    }
}
