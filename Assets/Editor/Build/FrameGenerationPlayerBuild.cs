using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;

/// <summary>
/// Installs the early-loading bootstrap required by DLSS Frame Generation in Windows players.
/// </summary>
public sealed class FrameGenerationPlayerBuild : IPostprocessBuildWithReport
{
    private const string _bootstrapRelativePath =
        "Native/FrameGeneration/Bootstrap/UnityPlayerFrameGenerationProxy.dll";

    /// <summary>
    /// Gets the order in which the Windows player bootstrap is installed after a build.
    /// </summary>
    public int callbackOrder => 1000;

    /// <summary>
    /// Installs the Frame Generation bootstrap after Unity has finished producing a Windows player.
    /// </summary>
    /// <param name="report">The completed Unity player build report.</param>
    public void OnPostprocessBuild(BuildReport report)
    {
        if (
            report.summary.platform
            is not (BuildTarget.StandaloneWindows or BuildTarget.StandaloneWindows64)
        )
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

        if (!FilesEqual(unityPlayerPath, bootstrapPath))
        {
            File.Copy(unityPlayerPath, unityPlayerCorePath, true);
        }
        else if (!File.Exists(unityPlayerCorePath))
        {
            throw new BuildFailedException(
                $"Unity player library backup not found at '{unityPlayerCorePath}'."
            );
        }

        File.Copy(bootstrapPath, unityPlayerPath, true);
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
