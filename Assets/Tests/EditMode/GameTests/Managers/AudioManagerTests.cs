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
        Assert.AreEqual(1f, manager.VideoVolume);
        Assert.DoesNotThrow(() => manager.SetMasterVolume(0.75f));
        Assert.DoesNotThrow(() => manager.SetMusicVolume(0.25f));
        Assert.DoesNotThrow(() => manager.SetSfxVolume(0.5f));
        Assert.DoesNotThrow(() => manager.SetAmbienceVolume(0.625f));
        Assert.DoesNotThrow(() => manager.SetVideoVolume(0.875f));
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
            VideoVolume = 0.875f,
        };

        manager.ApplySettings(settings);
        UserAudioSettings snapshot = manager.CreateSettingsSnapshot();

        Assert.AreEqual(0.75f, snapshot.MasterVolume);
        Assert.AreEqual(0.25f, snapshot.MusicVolume);
        Assert.AreEqual(0.5f, snapshot.SfxVolume);
        Assert.AreEqual(0.625f, snapshot.AmbienceVolume);
        Assert.AreEqual(0.875f, snapshot.VideoVolume);
        Assert.AreEqual(0.65625f, manager.EffectiveVideoVolume);
        Assert.AreEqual(0.375f, GetAudioSource(manager, "sfxSource").volume);
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
        Assert.AreEqual(1f, manager.VideoVolume);
    }

    [Test]
    public void SetVolume_ValuesOutsideRange_ClampsSnapshot()
    {
        AudioManager manager = AudioManager.EnsureExists();

        manager.SetMasterVolume(-1f);
        manager.SetMusicVolume(2f);
        manager.SetSfxVolume(-0.5f);
        manager.SetAmbienceVolume(1.5f);
        manager.SetVideoVolume(2f);
        UserAudioSettings snapshot = manager.CreateSettingsSnapshot();

        Assert.AreEqual(0f, snapshot.MasterVolume);
        Assert.AreEqual(1f, snapshot.MusicVolume);
        Assert.AreEqual(0f, snapshot.SfxVolume);
        Assert.AreEqual(1f, snapshot.AmbienceVolume);
        Assert.AreEqual(1f, snapshot.VideoVolume);
    }

    [Test]
    public void PreloadSfx_NullPaths_DoesNotThrow()
    {
        AudioManager manager = AudioManager.EnsureExists();

        Assert.DoesNotThrow(() => manager.PreloadSfx(null));
    }

    [Test]
    public void PreloadSfx_MissingRequiredPath_Throws()
    {
        AudioManager manager = AudioManager.EnsureExists();
        manager.InitializeContent(TestContent.Assets);

        Assert.Throws<System.InvalidOperationException>(() =>
            manager.PreloadSfx(new[] { "Application/MainMenu/Audio/missing-required-clip" })
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
    public void PlaySfx_RetainedOnDemandPath_ReusesLoadedClip()
    {
        AudioManager manager = AudioManager.EnsureExists();
        AudioClip clip = AudioClip.Create("RetainedOnDemand", 1, 1, 44100, false);
        try
        {
            GetLoadedSfx(manager).Add("Audio/SFX/Missing/retained_on_demand", clip);

            Assert.DoesNotThrow(() => manager.PlaySfx(" Audio/SFX/Missing/retained_on_demand "));
        }
        finally
        {
            Object.DestroyImmediate(clip);
        }
    }

    [Test]
    public void PlaySfxInstance_PreloadedPaths_UsesIndependentSources()
    {
        AudioManager manager = AudioManager.EnsureExists();
        AudioClip firstClip = AudioClip.Create("First", 1, 1, 44100, false);
        AudioClip secondClip = AudioClip.Create("Second", 1, 1, 44100, false);
        try
        {
            GetPreloadedSfx(manager).Add("Audio/SFX/first", firstClip);
            GetPreloadedSfx(manager).Add("Audio/SFX/second", secondClip);

            AudioPlaybackHandle firstPlayback = manager.PlaySfxInstance("Audio/SFX/first");
            AudioPlaybackHandle secondPlayback = manager.PlaySfxInstance("Audio/SFX/second");

            Assert.AreSame(firstClip, firstPlayback.Source.clip);
            Assert.AreSame(secondClip, secondPlayback.Source.clip);
            Assert.AreNotSame(firstPlayback.Source, secondPlayback.Source);

            firstPlayback.Stop();

            Assert.IsNull(firstPlayback.Source);
            Assert.AreSame(secondClip, secondPlayback.Source.clip);
            secondPlayback.Stop();
        }
        finally
        {
            Object.DestroyImmediate(firstClip);
            Object.DestroyImmediate(secondClip);
        }
    }

    [Test]
    public void PlaySfxInstance_ReusedSource_AppliesCurrentSfxVolume()
    {
        AudioManager manager = AudioManager.EnsureExists();
        AudioClip clip = AudioClip.Create("Tracked", 1, 1, 44100, false);
        try
        {
            GetPreloadedSfx(manager).Add("Audio/SFX/tracked", clip);
            AudioPlaybackHandle firstPlayback = manager.PlaySfxInstance("Audio/SFX/tracked");
            AudioSource source = firstPlayback.Source;
            firstPlayback.Stop();
            manager.SetMasterVolume(0.5f);
            manager.SetSfxVolume(0.25f);

            AudioPlaybackHandle secondPlayback = manager.PlaySfxInstance("Audio/SFX/tracked");

            Assert.AreSame(source, secondPlayback.Source);
            Assert.AreEqual(0.125f, secondPlayback.Source.volume);
            secondPlayback.Stop();
        }
        finally
        {
            Object.DestroyImmediate(clip);
        }
    }

    [Test]
    public void PlaySfxInstance_CompletedPlayback_ReleasesAndReusesSource()
    {
        AudioManager manager = AudioManager.EnsureExists();
        AudioClip clip = AudioClip.Create("Tracked", 1, 1, 44100, false);
        try
        {
            GetPreloadedSfx(manager).Add("Audio/SFX/tracked", clip);
            AudioPlaybackHandle firstPlayback = manager.PlaySfxInstance("Audio/SFX/tracked");
            AudioSource source = firstPlayback.Source;

            source.Stop();
            InvokeUpdate(manager);

            Assert.IsNull(firstPlayback.Source);

            AudioPlaybackHandle secondPlayback = manager.PlaySfxInstance("Audio/SFX/tracked");

            Assert.AreSame(source, secondPlayback.Source);
            secondPlayback.Stop();
        }
        finally
        {
            Object.DestroyImmediate(clip);
        }
    }

    [Test]
    public void PlaySfxInstance_PausedPlayback_RemainsActiveUntilResumedOrStopped()
    {
        AudioManager manager = AudioManager.EnsureExists();
        AudioClip clip = AudioClip.Create("Tracked", 1, 1, 44100, false);
        try
        {
            GetPreloadedSfx(manager).Add("Audio/SFX/tracked", clip);
            AudioPlaybackHandle playback = manager.PlaySfxInstance("Audio/SFX/tracked");
            AudioSource source = playback.Source;
            playback.Paused = true;
            source.Stop();

            InvokeUpdate(manager);

            Assert.AreSame(source, playback.Source);
            playback.Stop();
        }
        finally
        {
            Object.DestroyImmediate(clip);
        }
    }

    [Test]
    public void PlaySfxInstance_MissingClipOrPath_ReturnsNull()
    {
        AudioManager manager = AudioManager.EnsureExists();

        AudioPlaybackHandle clipPlayback = manager.PlaySfxInstance((AudioClip)null);
        AudioPlaybackHandle pathPlayback = manager.PlaySfxInstance(" ");

        Assert.IsNull(clipPlayback);
        Assert.IsNull(pathPlayback);
    }

    [Test]
    public void StopSfx_ActiveSfxInstances_StopsEveryInstance()
    {
        AudioManager manager = AudioManager.EnsureExists();
        AudioClip clip = AudioClip.Create("Tracked", 1, 1, 44100, false);
        try
        {
            GetPreloadedSfx(manager).Add("Audio/SFX/tracked", clip);
            AudioPlaybackHandle firstPlayback = manager.PlaySfxInstance("Audio/SFX/tracked");
            AudioPlaybackHandle secondPlayback = manager.PlaySfxInstance("Audio/SFX/tracked");

            manager.StopSfx();

            Assert.IsNull(firstPlayback.Source);
            Assert.IsNull(secondPlayback.Source);
        }
        finally
        {
            Object.DestroyImmediate(clip);
        }
    }

    [Test]
    public void PlayTrack_LoadedClip_AssignsLoopingMusicUntilStopped()
    {
        AudioManager manager = AudioManager.EnsureExists();
        AudioClip clip = AudioClip.Create("Track", 1, 1, 44100, false);
        try
        {
            manager.PlayTrack(clip, true);
            AudioSource source = GetAudioSource(manager, "musicSource");

            Assert.AreSame(clip, source.clip);
            Assert.IsTrue(source.loop);

            manager.StopMusic();

            Assert.IsNull(source.clip);
            Assert.IsFalse(source.isPlaying);
        }
        finally
        {
            Object.DestroyImmediate(clip);
        }
    }

    [Test]
    public void PlayTrack_BlankPath_DoesNotReplaceLoadedClip()
    {
        AudioManager manager = AudioManager.EnsureExists();
        AudioClip clip = AudioClip.Create("Track", 1, 1, 44100, false);
        try
        {
            manager.PlayTrack(clip);
            AudioSource source = GetAudioSource(manager, "musicSource");

            manager.PlayTrack(" ");

            Assert.AreSame(clip, source.clip);
        }
        finally
        {
            Object.DestroyImmediate(clip);
        }
    }

    [Test]
    public void PlayPlaylist_EmptyPaths_DoesNotReplaceLoadedClip()
    {
        AudioManager manager = AudioManager.EnsureExists();
        AudioClip clip = AudioClip.Create("Track", 1, 1, 44100, false);
        try
        {
            manager.PlayTrack(clip);
            AudioSource source = GetAudioSource(manager, "musicSource");

            manager.PlayPlaylist(new[] { null, string.Empty, " " });

            Assert.AreSame(clip, source.clip);
        }
        finally
        {
            Object.DestroyImmediate(clip);
        }
    }

    [Test]
    public void PlayAmbience_LoadedClip_ConfiguresAmbienceChannel()
    {
        AudioManager manager = AudioManager.EnsureExists();
        AudioClip clip = AudioClip.Create("Ambience", 1, 1, 44100, false);
        try
        {
            manager.SetMasterVolume(0.5f);
            manager.SetAmbienceVolume(0.25f);

            manager.PlayAmbience(clip, true);
            AudioSource source = GetAudioSource(manager, "ambienceSource");

            Assert.AreSame(clip, source.clip);
            Assert.IsTrue(source.loop);
            Assert.AreEqual(0.125f, source.volume);
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

    private static Dictionary<string, AudioClip> GetLoadedSfx(AudioManager manager)
    {
        return (Dictionary<string, AudioClip>)
            typeof(AudioManager)
                .GetField("_loadedSfx", BindingFlags.Instance | BindingFlags.NonPublic)
                .GetValue(manager);
    }

    private static AudioSource GetAudioSource(AudioManager manager, string fieldName)
    {
        return (AudioSource)
            typeof(AudioManager)
                .GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic)
                .GetValue(manager);
    }

    private static void InvokeUpdate(AudioManager manager)
    {
        typeof(AudioManager)
            .GetMethod("Update", BindingFlags.Instance | BindingFlags.NonPublic)
            .Invoke(manager, null);
    }
}
