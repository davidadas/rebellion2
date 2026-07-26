using System;
using System.IO;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

public sealed class ExternalContentBuildProcessor : IPostprocessBuildWithReport
{
    public int callbackOrder => 0;

    public void OnPostprocessBuild(BuildReport report)
    {
        ExternalContentExporter.CopyToBuild(report.summary.outputPath);
    }
}

internal static class ExternalContentExporter
{
    private const string _contentDirectoryName = "Content";

    internal static void CopyToBuild(string playerPath)
    {
        string sourcePath = Path.Combine(Application.dataPath, _contentDirectoryName);
        if (!Directory.Exists(sourcePath))
            throw new DirectoryNotFoundException($"Content directory not found: {sourcePath}");

        string destinationPath = GetDestinationPath(playerPath);
        CopyDirectory(sourcePath, destinationPath);
    }

    internal static string GetDestinationPath(string playerPath)
    {
        if (string.IsNullOrWhiteSpace(playerPath))
            throw new ArgumentException("A player output path is required.", nameof(playerPath));

        string absolutePlayerPath = Path.GetFullPath(playerPath);
        string outputDirectory = Path.GetDirectoryName(absolutePlayerPath);
        if (string.IsNullOrWhiteSpace(outputDirectory))
            throw new InvalidOperationException(
                "The player output directory could not be resolved."
            );

        return Path.Combine(outputDirectory, _contentDirectoryName);
    }

    internal static void CopyDirectory(string sourcePath, string destinationPath)
    {
        if (Directory.Exists(destinationPath))
            Directory.Delete(destinationPath, true);

        Directory.CreateDirectory(destinationPath);
        foreach (
            string directoryPath in Directory.EnumerateDirectories(
                sourcePath,
                "*",
                SearchOption.AllDirectories
            )
        )
        {
            string relativePath = Path.GetRelativePath(sourcePath, directoryPath);
            Directory.CreateDirectory(Path.Combine(destinationPath, relativePath));
        }

        foreach (
            string filePath in Directory.EnumerateFiles(
                sourcePath,
                "*",
                SearchOption.AllDirectories
            )
        )
        {
            if (ShouldSkip(filePath))
                continue;

            string relativePath = Path.GetRelativePath(sourcePath, filePath);
            string destinationFilePath = Path.Combine(destinationPath, relativePath);
            Directory.CreateDirectory(Path.GetDirectoryName(destinationFilePath));
            File.Copy(filePath, destinationFilePath, true);
        }
    }

    private static bool ShouldSkip(string filePath)
    {
        return filePath.EndsWith(".meta", StringComparison.OrdinalIgnoreCase)
            || string.Equals(
                Path.GetFileName(filePath),
                ".DS_Store",
                StringComparison.OrdinalIgnoreCase
            );
    }
}
