using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

[TestFixture]
public sealed class AudioContentLoadingTests
{
    private const int _requestedDspBufferSize = 512;
    private const string _audioSettingsPath = "ProjectSettings/AudioManager.asset";
    private const string _mainMenuPrefabPath = "Assets/Prefabs/UI/MainMenu/MainMenuRoot.prefab";

    [Test]
    public async Task PreloadAsync_MainMenuGroup_MakesMainMenuCuesResidentAsync()
    {
        ContentPack pack = ContentPackLoader.OpenActive();
        using ContentAssets assets = new ContentAssets(pack.ContentRootPath, pack.PackRootPath);
        await PreloadApplicationAndPackAsync(pack, assets, "main-menu");

        foreach (string resourcePath in GetMainMenuCuePaths())
        {
            AudioClip clip = assets.GetPreloadedAudio(resourcePath);
            Assert.IsNotNull(clip, resourcePath);
            Assert.AreEqual(AudioDataLoadState.Loaded, clip.loadState, resourcePath);
        }
    }

    [Test]
    public async Task PreloadAsync_StrategyGroup_MakesStrategyCuesResidentAsync()
    {
        ContentPack pack = ContentPackLoader.OpenActive();
        using ContentAssets assets = new ContentAssets(pack.ContentRootPath, pack.PackRootPath);
        await PreloadApplicationAndPackAsync(pack, assets, "strategy");

        FactionThemeLibrary themeLibrary = new FactionThemeLibrary(pack.GameData.FactionThemes);
        string[] paths = themeLibrary
            .GetAllThemes()
            .Append(themeLibrary.GetTheme(null))
            .SelectMany(StrategyUISoundPaths.GetPreloadPaths)
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        foreach (string path in paths)
        {
            Assert.IsNotNull(assets.GetPreloadedAudio(path), path);
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

    private static Task PreloadApplicationAndPackAsync(
        ContentPack pack,
        ContentAssets assets,
        string preloadID
    )
    {
        return Task.WhenAll(
            assets.PreloadAsync(
                ContentPackLoader.LoadApplicationPreloadManifest(pack.ContentRootPath, preloadID)
            ),
            assets.PreloadAsync(pack.GetPreloadManifest(preloadID))
        );
    }
}
