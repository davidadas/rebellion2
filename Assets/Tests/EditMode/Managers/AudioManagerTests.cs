using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

[TestFixture]
public sealed class AudioManagerTests
{
    [SetUp]
    public void SetUp()
    {
        DestroyAudioManagers();
    }

    [TearDown]
    public void TearDown()
    {
        DestroyAudioManagers();
    }

    [Test]
    public void EnsureExists_WhenMissing_CreatesUsableManager()
    {
        AudioManager manager = AudioManager.EnsureExists();

        Assert.IsNotNull(manager);
        Assert.AreSame(manager, AudioManager.Instance);
        Assert.GreaterOrEqual(manager.GetComponents<AudioSource>().Length, 3);
        Assert.AreEqual(1f, manager.MasterVolume);
        Assert.AreEqual(1f, manager.MusicVolume);
        Assert.AreEqual(1f, manager.SfxVolume);
        Assert.AreEqual(1f, manager.AmbienceVolume);
        Assert.DoesNotThrow(() => manager.SetMasterVolume(0.75f));
        Assert.DoesNotThrow(() => manager.SetMusicVolume(0.25f));
        Assert.DoesNotThrow(() => manager.SetSfxVolume(0.5f));
        Assert.DoesNotThrow(() => manager.SetAmbienceVolume(0.625f));
    }

    [Test]
    public void ApplySettings_ValidAudioSettings_UpdatesVolumeState()
    {
        AudioManager manager = AudioManager.EnsureExists();
        UserAudioSettings settings = new UserAudioSettings
        {
            MasterVolume = 0.75f,
            MusicVolume = 0.25f,
            SfxVolume = 0.5f,
            AmbienceVolume = 0.625f,
        };

        manager.ApplySettings(settings);
        UserAudioSettings snapshot = manager.CreateSettingsSnapshot();

        Assert.AreEqual(0.75f, snapshot.MasterVolume);
        Assert.AreEqual(0.25f, snapshot.MusicVolume);
        Assert.AreEqual(0.5f, snapshot.SfxVolume);
        Assert.AreEqual(0.625f, snapshot.AmbienceVolume);
    }

    [Test]
    public void ApplySettings_NullAudioSettings_AppliesDefaultVolumeState()
    {
        AudioManager manager = AudioManager.EnsureExists();
        manager.SetMasterVolume(0.25f);

        manager.ApplySettings(null);

        Assert.AreEqual(1f, manager.MasterVolume);
        Assert.AreEqual(1f, manager.MusicVolume);
        Assert.AreEqual(1f, manager.SfxVolume);
        Assert.AreEqual(1f, manager.AmbienceVolume);
    }

    [Test]
    public void SetVolume_ValuesOutsideRange_ClampsSnapshot()
    {
        AudioManager manager = AudioManager.EnsureExists();

        manager.SetMasterVolume(-1f);
        manager.SetMusicVolume(2f);
        manager.SetSfxVolume(-0.5f);
        manager.SetAmbienceVolume(1.5f);
        UserAudioSettings snapshot = manager.CreateSettingsSnapshot();

        Assert.AreEqual(0f, snapshot.MasterVolume);
        Assert.AreEqual(1f, snapshot.MusicVolume);
        Assert.AreEqual(0f, snapshot.SfxVolume);
        Assert.AreEqual(1f, snapshot.AmbienceVolume);
    }

    [Test]
    public void PreloadSfx_NullPaths_DoesNotThrow()
    {
        AudioManager manager = AudioManager.EnsureExists();

        Assert.DoesNotThrow(() => manager.PreloadSfx(null));
    }

    [Test]
    public void PreloadSfx_DuplicateAndBlankPaths_CachesOneLoadedClip()
    {
        AudioManager manager = AudioManager.EnsureExists();
        const string path = "Audio/SFX/StrategyView/sfx_strategyview_control_press";

        manager.PreloadSfx(new[] { null, string.Empty, " ", path, $" {path} " });

        Dictionary<string, AudioClip> clips = GetPreloadedSfx(manager);
        Assert.AreEqual(1, clips.Count);
        Assert.AreEqual(AudioDataLoadState.Loaded, clips[path].loadState);
    }

    [Test]
    public void PreloadSfx_MissingRequiredPath_Throws()
    {
        AudioManager manager = AudioManager.EnsureExists();

        Assert.Throws<System.Exception>(() =>
            manager.PreloadSfx(new[] { "Audio/SFX/Missing/required_clip" })
        );
    }

    [Test]
    public void PlaySfx_NullClip_DoesNotThrow()
    {
        AudioManager manager = AudioManager.EnsureExists();

        Assert.DoesNotThrow(() => manager.PlaySfx((AudioClip)null));
    }

    [Test]
    public void PlaySfx_PreloadedPath_DoesNotLoadResourceAgain()
    {
        AudioManager manager = AudioManager.EnsureExists();
        AudioClip clip = AudioClip.Create("PreloadedOnly", 1, 1, 44100, false);
        try
        {
            GetPreloadedSfx(manager).Add("Audio/SFX/Missing/preloaded_only", clip);

            Assert.DoesNotThrow(() => manager.PlaySfx(" Audio/SFX/Missing/preloaded_only "));
        }
        finally
        {
            Object.DestroyImmediate(clip);
        }
    }

    [Test]
    public void PlayDynamicPlaylist_NullProvider_ThrowsArgumentNullException()
    {
        AudioManager manager = AudioManager.EnsureExists();

        Assert.Throws<System.ArgumentNullException>(() => manager.PlayDynamicPlaylist(null));
    }

    [Test]
    public void EnsureExists_WhenExistingSceneManagerIsParented_MovesItToPersistentRoot()
    {
        GameObject parent = new GameObject("SceneRoot");
        GameObject audioObject = new GameObject("AudioManager");
        audioObject.transform.SetParent(parent.transform);

        AudioManager manager = audioObject.AddComponent<AudioManager>();
        AudioManager ensuredManager = AudioManager.EnsureExists();

        Assert.AreSame(manager, ensuredManager);
        Assert.AreSame(manager, AudioManager.Instance);
        Assert.IsNull(manager.transform.parent);
    }

    [Test]
    public void EnsureExists_WhenInstanceExists_ReturnsExistingManager()
    {
        AudioManager globalManager = AudioManager.EnsureExists();

        AudioManager ensuredManager = AudioManager.EnsureExists();

        Assert.AreSame(globalManager, ensuredManager);
        Assert.AreSame(globalManager, AudioManager.Instance);
    }

    private static void DestroyAudioManagers()
    {
        foreach (AudioManager manager in Object.FindObjectsByType<AudioManager>())
        {
            Object.DestroyImmediate(manager.gameObject);
        }
    }

    private static Dictionary<string, AudioClip> GetPreloadedSfx(AudioManager manager)
    {
        return (Dictionary<string, AudioClip>)
            typeof(AudioManager)
                .GetField("_preloadedSfx", BindingFlags.Instance | BindingFlags.NonPublic)
                .GetValue(manager);
    }
}
