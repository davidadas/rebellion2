using System;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;

/// <summary>
/// Embeds a path-independent identity of production source and dependency inputs in the game assembly.
/// Runtime admission reads the compiled attribute rather than hashing platform-specific binaries.
/// </summary>
internal sealed class SimulationBuildIdentityGenerator
    : AssetPostprocessor,
        IPreprocessBuildWithReport
{
    private const string _generatedPath = "Assets/Scripts/AssemblyBuildIdentity.g.cs";
    private static readonly string[] _dependencyInputs =
    {
        "Assets/Scripts/GameAssembly.asmdef",
        "Packages/manifest.json",
        "Packages/packages-lock.json",
        "ProjectSettings/ProjectVersion.txt",
    };

    public int callbackOrder => 0;

    /// <summary>
    /// Regenerates the compiled identity after relevant imports or a completed domain reload.
    /// </summary>
    /// <param name="importedAssets">The imported project paths.</param>
    /// <param name="deletedAssets">The deleted project paths.</param>
    /// <param name="movedAssets">The destination paths of moved assets.</param>
    /// <param name="movedFromAssetPaths">The previous paths of moved assets.</param>
    /// <param name="didDomainReload">Whether a script domain was reloaded.</param>
    [SuppressMessage("Roslynator", "RCS1213", Justification = "Invoked by Unity.")]
    private static void OnPostprocessAllAssets(
        string[] importedAssets,
        string[] deletedAssets,
        string[] movedAssets,
        string[] movedFromAssetPaths,
        bool didDomainReload
    )
    {
        if (
            didDomainReload
            || importedAssets
                .Concat(deletedAssets)
                .Concat(movedAssets)
                .Concat(movedFromAssetPaths)
                .Any(IsIdentityInput)
        )
            WriteCurrentIdentity();
    }

    /// <summary>
    /// Requires a current compiled identity before producing a distributable player.
    /// </summary>
    /// <param name="report">The pending player build.</param>
    public void OnPreprocessBuild(BuildReport report)
    {
        if (WriteCurrentIdentity())
            throw new BuildFailedException(
                "Simulation identity changed. Let script compilation finish, then build again."
            );
    }

    /// <summary>
    /// Identifies imports that can change the compiled simulation contract.
    /// </summary>
    /// <param name="path">The project-relative imported path.</param>
    /// <returns>True for production scripts or declared dependency inputs.</returns>
    private static bool IsIdentityInput(string path)
    {
        return path != _generatedPath
            && (
                path.StartsWith("Assets/Scripts/", StringComparison.Ordinal)
                    && path.EndsWith(".cs", StringComparison.Ordinal)
                || _dependencyInputs.Contains(path, StringComparer.Ordinal)
            );
    }

    /// <summary>
    /// Writes the identity only when its source inputs changed, avoiding import loops.
    /// </summary>
    /// <returns>True when a script recompilation was requested.</returns>
    private static bool WriteCurrentIdentity()
    {
        string identity = ComputeIdentity();
        string source =
            "using System.Reflection;\n\n[assembly: AssemblyMetadata(\n"
            + "    \"SimulationBuild\",\n    \""
            + identity
            + "\"\n)]\n";
        if (
            File.Exists(_generatedPath)
            && File.ReadAllText(_generatedPath).Replace("\r\n", "\n") == source
        )
            return false;
        File.WriteAllText(_generatedPath, source, new UTF8Encoding(false));
        AssetDatabase.ImportAsset(_generatedPath, ImportAssetOptions.ForceUpdate);
        return true;
    }

    /// <summary>
    /// Hashes normalized source and dependency inputs independently of checkout root and line endings.
    /// </summary>
    /// <returns>The compiled source-input digest.</returns>
    private static string ComputeIdentity()
    {
        using MemoryStream bytes = new();
        foreach (
            string input in Directory
                .GetFiles("Assets/Scripts", "*.cs", SearchOption.AllDirectories)
                .Select(path => path.Replace('\\', '/'))
                .Where(path => path != _generatedPath)
                .Concat(_dependencyInputs)
                .OrderBy(path => path, StringComparer.Ordinal)
        )
        {
            byte[] part = Encoding.UTF8.GetBytes(
                input
                    + "\0"
                    + File.ReadAllText(input).Replace("\r\n", "\n").Replace('\r', '\n')
                    + "\0"
            );
            bytes.Write(part, 0, part.Length);
        }
        bytes.Position = 0;
        using SHA256 hash = SHA256.Create();
        return BitConverter.ToString(hash.ComputeHash(bytes)).Replace("-", string.Empty);
    }
}
