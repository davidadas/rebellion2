using System;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;

/// <summary>
/// Applies the committed game-icon texture to the standalone player icon slots so the
/// built executable (Rebellion2.exe) carries the game icon rather than the default Unity one.
/// </summary>
public static class SetPlayerIcon
{
    private const string _iconPath = "Assets/GameIcon/rebellion2_icon.png";

    /// <summary>
    /// Sets the default and standalone player icons from the game-icon texture.
    /// </summary>
    public static void Apply()
    {
        AssetDatabase.ImportAsset(_iconPath, ImportAssetOptions.ForceSynchronousImport);
        Texture2D icon = AssetDatabase.LoadAssetAtPath<Texture2D>(_iconPath);
        if (icon == null)
        {
            throw new InvalidOperationException($"Icon texture not found at {_iconPath}.");
        }

        ApplyToTarget(NamedBuildTarget.Unknown, icon);
        ApplyToTarget(NamedBuildTarget.Standalone, icon);

        AssetDatabase.SaveAssets();
        Debug.Log($"Player icon applied from {_iconPath}.");
    }

    /// <summary>
    /// Fills every icon slot for a build target with the supplied texture.
    /// </summary>
    /// <param name="target">The named build target whose icons are set.</param>
    /// <param name="icon">The texture to place in each icon size slot.</param>
    private static void ApplyToTarget(NamedBuildTarget target, Texture2D icon)
    {
        int[] sizes = PlayerSettings.GetIconSizes(target, IconKind.Any);
        int count = sizes.Length == 0 ? 1 : sizes.Length;
        Texture2D[] icons = Enumerable.Repeat(icon, count).ToArray();
        PlayerSettings.SetIcons(target, icons, IconKind.Any);
    }
}
