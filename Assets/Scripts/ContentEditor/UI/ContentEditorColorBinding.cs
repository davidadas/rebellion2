using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Keeps a shared UI primitive in the editor palette, including after pooled controls are reused.
/// </summary>
public sealed class ContentEditorColorBinding : MonoBehaviour
{
    [SerializeField]
    private Graphic graphic;

    [SerializeField]
    private Selectable selectable;

    [SerializeField]
    private ContentEditorColorRole role;

    /// <summary>
    /// Associates an authored graphic and optional selectable with a semantic palette role.
    /// </summary>
    /// <param name="target">The primitive to recolor.</param>
    /// <param name="colorRole">Its visual purpose.</param>
    /// <param name="control">The optional control whose interaction colors must also change.</param>
    public void Bind(Graphic target, ContentEditorColorRole colorRole, Selectable control = null)
    {
        graphic = target;
        selectable = control;
        role = colorRole;
        Apply();
    }

    /// <summary>
    /// Changes presentation state without replacing the graphic or its content.
    /// </summary>
    /// <param name="colorRole">The current visual state.</param>
    public void SetRole(ContentEditorColorRole colorRole)
    {
        role = colorRole;
        Apply();
    }

    /// <summary>
    /// Applies the parent's current palette when a pooled control becomes visible.
    /// </summary>
    private void OnEnable() => Apply();

    /// <summary>
    /// Recolors the primitive and its existing interaction states.
    /// </summary>
    public void Apply()
    {
        if (graphic == null)
            return;
        ContentEditorColorMode mode =
            GetComponentInParent<ContentEditorAppearance>()?.Mode ?? ContentEditorColorMode.Dark;
        graphic.color =
            role == ContentEditorColorRole.TextButton
                ? Color.white
                : ContentEditorAppearance.ColorFor(mode, role);
        Outline outline = graphic.GetComponent<Outline>();
        if (outline != null)
            outline.effectColor = ContentEditorAppearance.ColorFor(
                mode,
                ContentEditorColorRole.Border
            );
        if (selectable == null)
            return;
        ColorBlock colors = selectable.colors;
        bool text = role == ContentEditorColorRole.TextButton;
        colors.normalColor = text
            ? ContentEditorAppearance.ColorFor(mode, ContentEditorColorRole.Text)
            : Color.white;
        colors.highlightedColor =
            text ? ContentEditorAppearance.ColorFor(mode, ContentEditorColorRole.Accent)
            : mode is ContentEditorColorMode.Light or ContentEditorColorMode.Rebels
                ? new Color(0.94f, 0.94f, 0.94f, 1)
            : new Color(1.25f, 1.25f, 1.25f, 1);
        colors.selectedColor = colors.normalColor;
        colors.pressedColor = text ? colors.highlightedColor : new Color(0.8f, 0.8f, 0.8f, 1);
        colors.disabledColor = text
            ? ContentEditorAppearance.ColorFor(mode, ContentEditorColorRole.Muted)
            : new Color(1, 1, 1, 0.5f);
        selectable.colors = colors;
        if (selectable is TMP_InputField input)
        {
            input.caretColor = ContentEditorAppearance.ColorFor(mode, ContentEditorColorRole.Text);
            input.selectionColor = ContentEditorAppearance.ColorFor(
                mode,
                ContentEditorColorRole.TextSelection
            );
        }
    }
}
