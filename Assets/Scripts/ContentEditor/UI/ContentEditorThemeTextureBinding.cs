using System;
using System.Reflection;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Replaces one prefab-preview texture with the corresponding selected-faction theme image.
/// </summary>
[RequireComponent(typeof(RawImage))]
public sealed class ContentEditorThemeTextureBinding : MonoBehaviour
{
    [SerializeField]
    private string memberPath;

    private RawImage image;

    /// <summary>
    /// Gets the dotted faction-theme member path represented by this preview image.
    /// </summary>
    public string MemberPath => memberPath;

    /// <summary>
    /// Configures the dotted faction-theme member path used by this image.
    /// </summary>
    /// <param name="path">The dotted public-property path from <see cref="FactionTheme"/>.</param>
    public void SetMemberPath(string path)
    {
        memberPath = string.IsNullOrWhiteSpace(path)
            ? throw new ArgumentException("A faction-theme member path is required.", nameof(path))
            : path;
    }

    /// <summary>
    /// Resolves and applies the selected faction's image for this binding.
    /// </summary>
    /// <param name="theme">The selected faction theme.</param>
    /// <param name="assets">The working pack's resolved artwork.</param>
    public void Apply(FactionTheme theme, ContentAssets assets)
    {
        if (theme == null)
            throw new ArgumentNullException(nameof(theme));
        if (assets == null)
            throw new ArgumentNullException(nameof(assets));

        string address = ResolveAddress(theme);
        image ??= GetComponent<RawImage>();
        image.texture = string.IsNullOrWhiteSpace(address) ? null : assets.GetTexture(address);
    }

    /// <summary>
    /// Traverses the configured public-property path on one faction theme.
    /// </summary>
    /// <param name="theme">The selected faction theme.</param>
    /// <returns>The configured image address, or null when an optional member is absent.</returns>
    private string ResolveAddress(FactionTheme theme)
    {
        object value = theme;
        foreach (string member in memberPath.Split('.'))
        {
            if (value == null)
                return null;
            PropertyInfo property = value
                .GetType()
                .GetProperty(member, BindingFlags.Instance | BindingFlags.Public);
            if (property == null)
                throw new MissingMemberException(value.GetType().FullName, member);
            value = property.GetValue(value);
        }
        return value as string;
    }
}
