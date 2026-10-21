using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;

/// <summary>
/// Installs the early-loading bootstrap required by DLSS Frame Generation in Windows players.
/// </summary>
public sealed class FrameGenerationPlayerBuild
    : IPreprocessBuildWithReport,
        IPostprocessBuildWithReport
{
    private const string _bootstrapRelativePath =
        "Native/FrameGeneration/Bootstrap/UnityPlayerFrameGenerationProxy.dll";

    /// <summary>
    /// Gets the order in which the Windows player bootstrap is installed after a build.
    /// </summary>
    public int callbackOrder => 1000;

    /// <summary>
    /// Removes player libraries left by an earlier build so Unity regenerates its own library
    /// before the Frame Generation bootstrap is installed again.
    /// </summary>
    /// <param name="report">The pending Unity player build report.</param>
    public void OnPreprocessBuild(BuildReport report)
    {
        if (!IsWindowsPlayer(report.summary.platform))
        {
            return;
        }

        RemovePlayerLibraries(report.summary.outputPath);
    }

    /// <summary>
    /// Installs the Frame Generation bootstrap after Unity has finished producing a Windows player.
    /// </summary>
    /// <param name="report">The completed Unity player build report.</param>
    public void OnPostprocessBuild(BuildReport report)
    {
        if (!IsWindowsPlayer(report.summary.platform))
        {
            return;
        }

        InstallBootstrap(report.summary.outputPath);
    }

    /// <summary>
    /// Preserves Unity's player library and installs the proxy that preloads the Frame Generation provider.
    /// </summary>
    /// <param name="executablePath">The generated Windows player executable.</param>
    private static void InstallBootstrap(string executablePath)
    {
        string projectRoot =
            Directory.GetParent(UnityEngine.Application.dataPath)?.FullName
            ?? throw new BuildFailedException("Could not resolve the Unity project directory.");
        string bootstrapPath = Path.Combine(projectRoot, _bootstrapRelativePath);
        if (!File.Exists(bootstrapPath))
        {
            throw new BuildFailedException(
                $"Frame Generation bootstrap not found at '{bootstrapPath}'."
            );
        }

        string outputDirectory =
            Path.GetDirectoryName(executablePath)
            ?? throw new BuildFailedException("Could not resolve the player output directory.");
        string unityPlayerPath = Path.Combine(outputDirectory, "UnityPlayer.dll");
        string unityPlayerCorePath = Path.Combine(outputDirectory, "UnityPlayerCore.dll");
        string dataDirectory = Path.Combine(
            outputDirectory,
            $"{Path.GetFileNameWithoutExtension(executablePath)}_Data"
        );
        string providerPath = Path.Combine(
            dataDirectory,
            "Plugins",
            "x86_64",
            "DLSSFrameGenPluginD3D.dll"
        );
        if (!File.Exists(unityPlayerPath))
        {
            throw new BuildFailedException(
                $"Generated Unity player library not found at '{unityPlayerPath}'."
            );
        }

        if (!File.Exists(providerPath))
        {
            throw new BuildFailedException(
                $"Frame Generation provider not found at '{providerPath}'."
            );
        }

        if (FilesEqual(unityPlayerPath, bootstrapPath))
        {
            throw new BuildFailedException(
                $"Generated Unity player library at '{unityPlayerPath}' is still the Frame Generation bootstrap."
            );
        }

        File.Copy(unityPlayerPath, unityPlayerCorePath, true);
        File.Copy(bootstrapPath, unityPlayerPath, true);
    }

    /// <summary>
    /// Determines whether a build target produces a Windows standalone player.
    /// </summary>
    /// <param name="target">The Unity build target.</param>
    /// <returns><see langword="true"/> for Windows standalone targets.</returns>
    private static bool IsWindowsPlayer(BuildTarget target)
    {
        return target is BuildTarget.StandaloneWindows or BuildTarget.StandaloneWindows64;
    }

    /// <summary>
    /// Removes generated player libraries from an earlier Windows build.
    /// </summary>
    /// <param name="executablePath">The Windows player executable that will be rebuilt.</param>
    private static void RemovePlayerLibraries(string executablePath)
    {
        string outputDirectory = Path.GetDirectoryName(executablePath);
        if (string.IsNullOrWhiteSpace(outputDirectory) || !Directory.Exists(outputDirectory))
        {
            return;
        }

        string unityPlayerPath = Path.Combine(outputDirectory, "UnityPlayer.dll");
        string unityPlayerCorePath = Path.Combine(outputDirectory, "UnityPlayerCore.dll");
        File.Delete(unityPlayerPath);
        File.Delete(unityPlayerCorePath);
    }

    /// <summary>
    /// Determines whether two files contain the same bytes.
    /// </summary>
    /// <param name="firstPath">The first file to compare.</param>
    /// <param name="secondPath">The second file to compare.</param>
    /// <returns><see langword="true"/> when both files contain identical bytes.</returns>
    private static bool FilesEqual(string firstPath, string secondPath)
    {
        FileInfo first = new(firstPath);
        FileInfo second = new(secondPath);
        if (first.Length != second.Length)
        {
            return false;
        }

        using FileStream firstStream = first.OpenRead();
        using FileStream secondStream = second.OpenRead();
        int firstByte;
        while ((firstByte = firstStream.ReadByte()) != -1)
        {
            if (firstByte != secondStream.ReadByte())
            {
                return false;
            }
        }

        return secondStream.ReadByte() == -1;
    }
}
