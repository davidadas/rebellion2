using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Presents one selectable server-table row using the canonical menu selection artwork.
/// </summary>
public sealed class MenuServerRowView : MonoBehaviour, IContentInitializable
{
    [SerializeField]
    private TMP_Text nameText;

    [SerializeField]
    private TMP_Text contentText;

    [SerializeField]
    private TMP_Text playersText;

    [SerializeField]
    private MenuLockIcon accessIcon;

    [SerializeField]
    private Image background;

    [SerializeField]
    private Sprite idleSprite;

    [SerializeField]
    private Sprite selectedSprite;

    /// <summary>
    /// Resolves the same row sprites used by the shared menu save list.
    /// </summary>
    /// <param name="contentAssets">The application's content source.</param>
    public void InitializeContent(IContentAssetSource contentAssets)
    {
        Vector4 border = new Vector4(7, 7, 7, 7);
        idleSprite = ContentBindings.RequireSprite(
            contentAssets,
            "Application/OptionsMenu/UI/ui_settingsmenu_row_background",
            border
        );
        selectedSprite = ContentBindings.RequireSprite(
            contentAssets,
            "Application/OptionsMenu/UI/ui_settingsmenu_row_selected_background",
            border
        );
    }

    /// <summary>
    /// Updates a real or preview row without rebuilding its native controls.
    /// </summary>
    /// <param name="gameName">The displayed room name.</param>
    /// <param name="contentName">The displayed content-pack name.</param>
    /// <param name="players">The displayed player count.</param>
    /// <param name="passwordProtected">Whether the lock should be closed.</param>
    /// <param name="selected">Whether this row is selected.</param>
    public void Render(
        string gameName,
        string contentName,
        string players,
        bool passwordProtected,
        bool selected
    )
    {
        nameText.text = gameName;
        contentText.text = contentName;
        playersText.text = players;
        accessIcon.Locked = passwordProtected;
        background.sprite = selected ? selectedSprite : idleSprite;
    }
}
