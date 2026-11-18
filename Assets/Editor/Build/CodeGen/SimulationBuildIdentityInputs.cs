using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

/// <summary>
/// Defines the input files and normalized digest shared by compilation and editor invalidation.
/// </summary>
public static class SimulationBuildIdentityInputs
{
    public const string MetadataKey = "SimulationBuild";
    public const string SourceRoot = "Assets/Scripts";
    private const string _legacyGeneratedPath = "Assets/Scripts/AssemblyBuildIdentity.g.cs";
    private static readonly string[] _dependencyInputs =
    {
        "Assets/Scripts/GameAssembly.asmdef",
        "Assets/Input/InputActions.asmdef",
        "Assets/Input/PlayerInputActions.cs",
        "Packages/manifest.json",
        "Packages/packages-lock.json",
        "ProjectSettings/ProjectVersion.txt",
    };

    public static IReadOnlyList<string> DependencyInputs => _dependencyInputs;

    /// <summary>
    /// Hashes normalized source and dependency inputs independently of checkout root and line endings.
    /// </summary>
    /// <param name="projectDirectory">The project root containing the source and dependency inputs.</param>
    /// <returns>The compiled source-input digest.</returns>
    public static string ComputeIdentity(string projectDirectory)
    {
        using MemoryStream bytes = new();
        foreach (
            string input in Directory
                .GetFiles(
                    Path.Combine(projectDirectory, SourceRoot),
                    "*",
                    SearchOption.AllDirectories
                )
                .Select(path => Path.GetRelativePath(projectDirectory, path).Replace('\\', '/'))
                .Where(path =>
                    path != _legacyGeneratedPath
                    && (
                        path.EndsWith(".cs", StringComparison.Ordinal)
                        || path.EndsWith(".asmdef", StringComparison.Ordinal)
                    )
                )
                .Concat(_dependencyInputs)
                .Distinct(StringComparer.Ordinal)
                .OrderBy(path => path, StringComparer.Ordinal)
        )
        {
            byte[] part = Encoding.UTF8.GetBytes(
                input
                    + "\0"
                    + File.ReadAllText(Path.Combine(projectDirectory, input))
                        .Replace("\r\n", "\n")
                        .Replace('\r', '\n')
                    + "\0"
            );
            bytes.Write(part, 0, part.Length);
        }
        bytes.Position = 0;
        using SHA256 hash = SHA256.Create();
        return BitConverter.ToString(hash.ComputeHash(bytes)).Replace("-", string.Empty);
    }
}
