using System;
using System.IO;
using UnityEditor;
using UnityEngine;

public static class ApplicationContentPrefabBuilder
{
    private const string _editorContentPath = "Assets/Editor/ContentPreview";

    [MenuItem("Rebellion/Rebuild All UI Prefabs")]
    public static void Rebuild()
    {
        UIAuthoringGuard.EnsureEditMode();
        SyncEditorContent();
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        CommonUIPrefabBuilder.RebuildSharedControlPrefabs();
        SaveMenuPrefabBuilder.RebuildAllSaveMenuPrefabs(false);
        StrategyViewPrefabBuilder.RebuildAllStrategyViewPrefabs(false);
        MainMenuPrefabAuthoring.RebuildMainMenuViewBindings();

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("Rebuilt all UI prefabs.");
    }

    private static void SyncEditorContent()
    {
        string projectRoot =
            Directory.GetParent(Application.dataPath)?.FullName
            ?? throw new InvalidOperationException("Could not resolve the project directory.");
        string sourceRoot = Path.Combine(projectRoot, "Content");
        string destinationRoot = Path.Combine(projectRoot, _editorContentPath);
        if (!Directory.Exists(sourceRoot))
            throw new DirectoryNotFoundException($"External content not found: {sourceRoot}");

        foreach (
            string sourcePath in Directory.EnumerateFiles(
                sourceRoot,
                "*",
                SearchOption.AllDirectories
            )
        )
        {
            string extension = Path.GetExtension(sourcePath);
            if (
                !string.Equals(extension, ".png", StringComparison.OrdinalIgnoreCase)
                && !string.Equals(extension, ".jpg", StringComparison.OrdinalIgnoreCase)
                && !string.Equals(extension, ".jpeg", StringComparison.OrdinalIgnoreCase)
                && !string.Equals(extension, ".meta", StringComparison.OrdinalIgnoreCase)
            )
            {
                continue;
            }

            string relativePath = Path.GetRelativePath(sourceRoot, sourcePath);
            string destinationPath = Path.Combine(destinationRoot, relativePath);
            FileInfo source = new FileInfo(sourcePath);
            FileInfo destination = new FileInfo(destinationPath);
            if (
                destination.Exists
                && destination.Length == source.Length
                && destination.LastWriteTimeUtc == source.LastWriteTimeUtc
            )
            {
                continue;
            }

            Directory.CreateDirectory(Path.GetDirectoryName(destinationPath));
            File.Copy(sourcePath, destinationPath, true);
            File.SetLastWriteTimeUtc(destinationPath, source.LastWriteTimeUtc);
        }
    }
}
