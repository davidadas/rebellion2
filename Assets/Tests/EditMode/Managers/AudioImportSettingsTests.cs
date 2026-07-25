using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

[TestFixture]
public sealed class AudioImportSettingsTests
{
    private const int _requestedDspBufferSize = 512;
    private const string _audioSettingsPath = "ProjectSettings/AudioManager.asset";
    private const string _mainMenuPrefabPath = "Assets/Prefabs/UI/MainMenu/MainMenuRoot.prefab";

    [Test]
    public void FixedCueAssets_ConfiguredForImmediatePlayback()
    {
        foreach (string resourcePath in GetFixedCuePaths())
        {
            AudioImporter importer = GetAudioImporter(resourcePath);
            AudioImporterSampleSettings settings = importer.defaultSampleSettings;

            Assert.AreEqual(AudioClipLoadType.DecompressOnLoad, settings.loadType, resourcePath);
            Assert.AreEqual(AudioCompressionFormat.PCM, settings.compressionFormat, resourcePath);
            Assert.IsTrue(settings.preloadAudioData, resourcePath);
            Assert.IsFalse(importer.loadInBackground, resourcePath);
        }
    }

    [Test]
    public void AudioProjectSettings_RequestsLowLatencyDspBuffer()
    {
        string audioSettings = File.ReadAllText(_audioSettingsPath);

        StringAssert.Contains(
            $"m_RequestedDSPBufferSize: {_requestedDspBufferSize}",
            audioSettings
        );
    }

    private static string[] GetFixedCuePaths()
    {
        FactionThemeLibrary themeLibrary = new FactionThemeLibrary();
        IEnumerable<FactionTheme> themes = themeLibrary
            .GetAllThemes()
            .Append(themeLibrary.GetTheme(null));

        return GetMainMenuCuePaths()
            .Concat(themes.SelectMany(StrategyUISoundPaths.GetPreloadPaths))
            .Distinct(StringComparer.Ordinal)
            .ToArray();
    }

    private static string[] GetMainMenuCuePaths()
    {
        GameObject prefabRoot = PrefabUtility.LoadPrefabContents(_mainMenuPrefabPath);
        if (prefabRoot == null)
            throw new InvalidOperationException($"Missing test prefab at {_mainMenuPrefabPath}.");

        try
        {
            MainMenuView view =
                prefabRoot.GetComponentInChildren<MainMenuView>(true)
                ?? throw new InvalidOperationException(
                    $"No main menu view exists in {_mainMenuPrefabPath}."
                );
            return view.GetAudioCuePaths().ToArray();
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(prefabRoot);
        }
    }

    private static AudioImporter GetAudioImporter(string resourcePath)
    {
        string assetPath = $"Assets/Resources/{resourcePath}.wav";
        return AssetImporter.GetAtPath(assetPath) as AudioImporter
            ?? throw new InvalidOperationException($"No audio importer exists at '{assetPath}'.");
    }
}
