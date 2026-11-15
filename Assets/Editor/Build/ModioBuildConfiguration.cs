using System.IO;
using Rebellion.Content.Modio;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;

/// <summary>
/// Embeds public catalog configuration in players without checking credentials into source control.
/// </summary>
public sealed class ModioBuildConfiguration
    : IPreprocessBuildWithReport,
        IPostprocessBuildWithReport
{
    private const string _resourcePath = "Assets/Resources/ModioSettings.json";

    public int callbackOrder => 0;

    /// <summary>
    /// Generates the public catalog resource from the build environment when configured.
    /// </summary>
    /// <param name="report">The player build receiving the configuration.</param>
    public void OnPreprocessBuild(BuildReport report)
    {
        AssetDatabase.DeleteAsset(_resourcePath);
        ModioConfiguration configuration = ModioConfiguration.FromEnvironment();
        if (!configuration.IsConfigured)
            return;
        configuration.Write(_resourcePath);
        AssetDatabase.ImportAsset(_resourcePath, ImportAssetOptions.ForceSynchronousImport);
    }

    /// <summary>
    /// Removes the generated credential resource after packaging it into the player.
    /// </summary>
    /// <param name="report">The completed player build.</param>
    public void OnPostprocessBuild(BuildReport report)
    {
        if (File.Exists(_resourcePath))
            AssetDatabase.DeleteAsset(_resourcePath);
    }
}
