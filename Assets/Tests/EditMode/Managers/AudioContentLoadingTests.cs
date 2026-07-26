using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

[TestFixture]
public sealed class AudioContentLoadingTests
{
    private const int _requestedDspBufferSize = 512;
    private const string _audioSettingsPath = "ProjectSettings/AudioManager.asset";
    private const string _mainMenuPrefabPath = "Assets/Prefabs/UI/MainMenu/MainMenuRoot.prefab";

    [TearDown]
    public void TearDown()
    {
        ResourceManager.SetContentRootPathForTests(null);
    }

    [UnityTest]
    public IEnumerator InitializeAsync_ConfiguredImmediateCues_AreResident()
    {
        ResourceManager.SetContentRootPathForTests(null);
        Task initialization = ResourceManager.InitializeAsync();
        while (!initialization.IsCompleted)
            yield return null;
        if (initialization.IsFaulted)
            throw initialization.Exception.GetBaseException();

        foreach (string resourcePath in GetFixedCuePaths())
        {
            AudioClip clip = ResourceManager.GetAudio(resourcePath);
            Assert.IsNotNull(clip, resourcePath);
            Assert.AreEqual(AudioDataLoadState.Loaded, clip.loadState, resourcePath);
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
}
