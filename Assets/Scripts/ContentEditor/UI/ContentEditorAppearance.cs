using System;
using UnityEngine;

/// <summary>
/// Selects the editor's presentation palette without changing authored content or input drafts.
/// </summary>
public sealed class ContentEditorAppearance : MonoBehaviour
{
    public ContentEditorColorMode Mode { get; private set; } = ContentEditorColorMode.Dark;

    /// <summary>
    /// Recolors authored and pooled controls without rebuilding the active form.
    /// </summary>
    /// <param name="mode">The requested editor presentation.</param>
    public void SetMode(ContentEditorColorMode mode)
    {
        if (!Enum.IsDefined(typeof(ContentEditorColorMode), mode))
            throw new ArgumentOutOfRangeException(nameof(mode));
        Mode = mode;
        foreach (
            ContentEditorColorBinding binding in GetComponentsInChildren<ContentEditorColorBinding>(
                true
            )
        )
            binding.Apply();
    }

    /// <summary>
    /// Resolves a semantic control color in the selected presentation.
    /// </summary>
    /// <param name="role">The control's visual purpose.</param>
    /// <returns>The corresponding palette color.</returns>
    public Color ColorFor(ContentEditorColorRole role) => ColorFor(Mode, role);

    /// <summary>
    /// Resolves a palette color independently of scene and persistence state.
    /// </summary>
    /// <param name="mode">The requested presentation.</param>
    /// <param name="role">The control's visual purpose.</param>
    /// <returns>The corresponding palette color.</returns>
    public static Color ColorFor(ContentEditorColorMode mode, ContentEditorColorRole role)
    {
        if (
            mode
            is ContentEditorColorMode.Empire
                or ContentEditorColorMode.Rebels
                or ContentEditorColorMode.Dagobah
        )
            return ThemedColorFor(mode, role);
        bool light = mode == ContentEditorColorMode.Light;
        return role switch
        {
            ContentEditorColorRole.Background => light
                ? new Color32(241, 244, 249, 255)
                : new Color32(24, 24, 26, 255),
            ContentEditorColorRole.Panel => light
                ? new Color32(227, 233, 242, 255)
                : new Color32(35, 36, 41, 255),
            ContentEditorColorRole.Input => light
                ? new Color32(252, 253, 255, 255)
                : new Color32(46, 46, 48, 255),
            ContentEditorColorRole.Muted => light
                ? new Color32(96, 99, 104, 255)
                : new Color32(150, 150, 151, 255),
            ContentEditorColorRole.Divider => light
                ? new Color32(202, 199, 192, 255)
                : new Color32(68, 68, 70, 255),
            ContentEditorColorRole.Border => light
                ? new Color32(132, 132, 133, 255)
                : new Color32(150, 150, 151, 255),
            ContentEditorColorRole.Button => light
                ? new Color32(212, 222, 236, 255)
                : new Color32(88, 88, 90, 255),
            ContentEditorColorRole.Selection => light
                ? new Color32(207, 222, 240, 255)
                : new Color32(88, 88, 90, 255),
            ContentEditorColorRole.Accent => light
                ? new Color32(116, 78, 15, 255)
                : new Color32(218, 178, 111, 255),
            ContentEditorColorRole.Checkmark => light
                ? ColorFor(mode, ContentEditorColorRole.Accent)
                : Color.white,
            ContentEditorColorRole.Error => light
                ? new Color32(163, 32, 32, 255)
                : new Color32(255, 150, 150, 255),
            ContentEditorColorRole.Success => light
                ? new Color32(28, 99, 64, 255)
                : new Color32(189, 236, 211, 255),
            ContentEditorColorRole.Transparent => Color.clear,
            ContentEditorColorRole.Warning => new Color32(238, 179, 65, 255),
            ContentEditorColorRole.WarningText => new Color32(32, 27, 19, 255),
            ContentEditorColorRole.ScrollTrack => light
                ? new Color32(211, 216, 225, 255)
                : new Color32(44, 45, 50, 255),
            ContentEditorColorRole.ScrollThumb => light
                ? new Color32(110, 77, 24, 255)
                : new Color32(218, 178, 111, 255),
            ContentEditorColorRole.TextSelection => light
                ? new Color(0.32f, 0.55f, 0.85f, 0.4f)
                : new Color(0.4f, 0.6f, 0.9f, 0.5f),
            _ => light ? new Color32(42, 46, 52, 255) : new Color32(208, 196, 169, 255),
        };
    }

    /// <summary>
    /// Resolves named palettes for surfaces, controls, text and interaction states.
    /// </summary>
    /// <param name="mode">The Empire, Rebels or Dagobah presentation.</param>
    /// <param name="role">The control's visual purpose.</param>
    /// <returns>The requested theme color or a contrast-appropriate shared status color.</returns>
    private static Color ThemedColorFor(ContentEditorColorMode mode, ContentEditorColorRole role)
    {
        if (mode == ContentEditorColorMode.Empire)
            return role switch
            {
                ContentEditorColorRole.Text
                or ContentEditorColorRole.TextButton
                or ContentEditorColorRole.ButtonText => new Color32(226, 232, 229, 255),
                ContentEditorColorRole.Accent => new Color32(139, 192, 151, 255),
                ContentEditorColorRole.Selection => new Color32(49, 76, 60, 255),
                ContentEditorColorRole.ScrollThumb => new Color32(119, 170, 133, 255),
                ContentEditorColorRole.TextSelection => new Color(0.34f, 0.60f, 0.42f, 0.45f),
                _ => ColorFor(ContentEditorColorMode.Dark, role),
            };
        bool rebels = mode == ContentEditorColorMode.Rebels;
        return role switch
        {
            ContentEditorColorRole.Background => rebels
                ? new Color32(250, 249, 248, 255)
                : new Color32(59, 99, 76, 255),
            ContentEditorColorRole.Panel => rebels
                ? new Color32(236, 236, 238, 255)
                : new Color32(44, 78, 58, 255),
            ContentEditorColorRole.Input => rebels
                ? new Color32(255, 255, 255, 255)
                : new Color32(32, 60, 43, 255),
            ContentEditorColorRole.Text or ContentEditorColorRole.TextButton => rebels
                ? new Color32(48, 49, 52, 255)
                : new Color32(235, 239, 237, 255),
            ContentEditorColorRole.ButtonText => rebels
                ? new Color32(255, 250, 249, 255)
                : new Color32(240, 246, 242, 255),
            ContentEditorColorRole.Muted => rebels
                ? new Color32(101, 101, 107, 255)
                : new Color32(207, 219, 212, 255),
            ContentEditorColorRole.Divider => rebels
                ? new Color32(211, 199, 196, 255)
                : new Color32(94, 101, 105, 255),
            ContentEditorColorRole.Border => rebels
                ? new Color32(174, 141, 137, 255)
                : new Color32(148, 155, 159, 255),
            ContentEditorColorRole.Button => rebels
                ? new Color32(163, 57, 53, 255)
                : new Color32(50, 56, 60, 255),
            ContentEditorColorRole.Selection => rebels
                ? new Color32(241, 214, 210, 255)
                : new Color32(62, 70, 72, 255),
            ContentEditorColorRole.Accent => rebels
                ? new Color32(156, 45, 42, 255)
                : new Color32(215, 222, 224, 255),
            ContentEditorColorRole.Checkmark => rebels
                ? ColorFor(mode, ContentEditorColorRole.Accent)
                : Color.white,
            ContentEditorColorRole.ScrollTrack => rebels
                ? new Color32(228, 223, 223, 255)
                : new Color32(38, 44, 48, 255),
            ContentEditorColorRole.ScrollThumb => rebels
                ? new Color32(167, 84, 79, 255)
                : new Color32(151, 161, 166, 255),
            ContentEditorColorRole.TextSelection => rebels
                ? new Color(0.77f, 0.25f, 0.21f, 0.23f)
                : new Color(0.55f, 0.60f, 0.63f, 0.5f),
            ContentEditorColorRole.Error => rebels
                ? ColorFor(ContentEditorColorMode.Light, role)
                : new Color32(255, 210, 210, 255),
            _ => ColorFor(
                rebels ? ContentEditorColorMode.Light : ContentEditorColorMode.Dark,
                role
            ),
        };
    }
}

public enum ContentEditorColorMode
{
    Dark,
    Light,
    Empire,
    Rebels,
    Dagobah,
}

public enum ContentEditorColorRole
{
    Background,
    Panel,
    Text,
    Muted,
    Divider,
    Border,
    Button,
    Input,
    TextButton,
    Selection,
    Accent,
    Error,
    Success,
    Transparent,
    Warning,
    WarningText,
    ScrollTrack,
    ScrollThumb,
    TextSelection,
    ButtonText,
    Checkmark,
}
