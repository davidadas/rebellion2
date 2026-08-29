using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Rebellion.Util.Common;
using Rebellion.Util.Extensions;
using UnityEngine;

/// <summary>
/// Manages global music, sound effect, and ambience playback for the running application.
/// </summary>
public sealed class AudioManager : MonoBehaviour
{
    private const string _managerObjectName = "AudioManager";

    [SerializeField]
    private AudioSource musicSource;

    [SerializeField]
    private AudioSource sfxSource;

    [SerializeField]
    private AudioSource messageSource;

    [SerializeField]
    private AudioSource ambienceSource;

    [Range(0f, 1f)]
    [SerializeField]
    private float masterVolume = 1f;

    [Range(0f, 1f)]
    [SerializeField]
    private float musicVolume = 1f;

    [Range(0f, 1f)]
    [SerializeField]
    private float sfxVolume = 1f;

    [Range(0f, 1f)]
    [SerializeField]
    private float ambienceVolume = 1f;

    [Range(0f, 1f)]
    [SerializeField]
    private float videoVolume = 1f;

    private readonly Dictionary<string, AudioClip> _preloadedSfx = new Dictionary<
        string,
        AudioClip
    >(StringComparer.Ordinal);
    private readonly Dictionary<string, AudioClip> _loadedSfx = new Dictionary<string, AudioClip>(
        StringComparer.Ordinal
    );
    private readonly Dictionary<string, Task<AudioClip>> _sfxLoads = new Dictionary<
        string,
        Task<AudioClip>
    >(StringComparer.Ordinal);
    private AudioClip[] _clipPlaylist;
    private string[] _activePlaylistPaths;
    private string[] _requestedPlaylistPaths;
    private int _playlistIndex;
    private bool _shufflePlaylist;
    private string _activeTrackPath;
    private Func<string> _nextTrackPathProvider;
    private Coroutine _playlistCoroutine;
    private Coroutine _musicLoadCoroutine;
    private Coroutine _messagePlaybackCoroutine;
    private Coroutine _fadeOutCoroutine;
    private ContentAssets _contentAssets;
    private Task<AudioClip> _musicClipLoad;
    private bool _activeTrackLoop;
    private bool _messageAudioPaused;

    /// <summary>
    /// Gets the active global audio manager instance.
    /// </summary>
    public static AudioManager Instance { get; private set; }

    /// <summary>
    /// Gets the master volume used by all audio channels.
    /// </summary>
    public float MasterVolume => masterVolume;

    /// <summary>
    /// Gets the music channel volume.
    /// </summary>
    public float MusicVolume => musicVolume;

    /// <summary>
    /// Gets the sound effect channel volume.
    /// </summary>
    public float SfxVolume => sfxVolume;

    /// <summary>
    /// Gets the unscaled video volume.
    /// </summary>
    public float VideoVolume => videoVolume;

    /// <summary>
    /// Gets the video volume after applying the master channel.
    /// </summary>
    public float EffectiveVideoVolume => videoVolume * masterVolume;

    /// <summary>
    /// Gets the ambience channel volume.
    /// </summary>
    public float AmbienceVolume => ambienceVolume;

    /// <summary>
    /// Binds this manager to the application-owned external content assets.
    /// </summary>
    /// <param name="contentAssets">The active content asset store.</param>
    internal void InitializeContent(ContentAssets contentAssets)
    {
        if (contentAssets == null)
            throw new ArgumentNullException(nameof(contentAssets));
        if (_contentAssets != null && _contentAssets != contentAssets)
            throw new InvalidOperationException(
                "AudioManager is already bound to a content asset store."
            );

        _contentAssets = contentAssets;
    }

    /// <summary>
    /// Returns the global audio manager, creating one when the scene does not already contain it.
    /// </summary>
    /// <param name="parent">The optional parent to use for a newly created manager.</param>
    /// <returns>The active audio manager.</returns>
    public static AudioManager EnsureExists(Transform parent = null)
    {
        if (Instance != null)
            return Instance;

        AudioManager existing = UnityEngine.Object.FindAnyObjectByType<AudioManager>();
        if (existing != null)
        {
            existing.BecomeInstance();
            return existing;
        }

        GameObject audioObject = new GameObject(_managerObjectName);
        if (parent != null)
            audioObject.transform.SetParent(parent);

        AudioManager manager = audioObject.AddComponent<AudioManager>();
        if (Instance != manager)
            manager.BecomeInstance();

        return manager;
    }

    /// <summary>
    /// Initializes the singleton when Unity creates the component.
    /// </summary>
    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        BecomeInstance();
    }

    /// <summary>
    /// Clears the singleton reference when Unity destroys the active manager.
    /// </summary>
    private void OnDestroy()
    {
        _musicClipLoad = null;
        _sfxLoads.Clear();
        _loadedSfx.Clear();

        if (Instance == this)
            Instance = null;
    }

    /// <summary>
    /// Plays one music track loaded from a content address.
    /// </summary>
    /// <param name="resourcePath">The content address for the music clip.</param>
    /// <param name="loop">Whether the track should loop.</param>
    public void PlayTrack(string resourcePath, bool loop = false)
    {
        if (string.IsNullOrWhiteSpace(resourcePath))
            return;

        string normalizedPath = resourcePath.Trim();
        EnsureAudioSources();
        if (
            IsPlayingResourceTrack(normalizedPath, loop)
            || (
                _musicLoadCoroutine != null
                && _activeTrackPath == normalizedPath
                && _activeTrackLoop == loop
            )
        )
        {
            ApplyVolumes();
            return;
        }

        StopPlaylist();
        StopFadeOut();
        StopMusicLoad();
        StopCurrentMusicClip();
        _activeTrackPath = normalizedPath;
        _activeTrackLoop = loop;
        _musicLoadCoroutine = StartCoroutine(LoadAndPlayTrack(normalizedPath, loop));
    }

    /// <summary>
    /// Plays one music track from an already loaded clip.
    /// </summary>
    /// <param name="clip">The music clip to play.</param>
    /// <param name="loop">Whether the track should loop.</param>
    public void PlayTrack(AudioClip clip, bool loop = false)
    {
        if (clip == null)
            return;

        EnsureAudioSources();
        StopPlaylist();
        StopFadeOut();
        StopMusicLoad();
        StopCurrentMusicClip();

        _activeTrackPath = null;
        _activeTrackLoop = false;
        musicSource.clip = clip;
        musicSource.loop = loop;
        ApplyVolumes();
        musicSource.Play();
    }

    /// <summary>
    /// Plays a playlist from content addresses.
    /// </summary>
    /// <param name="resourcePaths">The content addresses for the playlist tracks.</param>
    /// <param name="shuffle">Whether the playlist should be shuffled before playback.</param>
    public void PlayPlaylist(string[] resourcePaths, bool shuffle = false)
    {
        string[] playablePaths = GetPlayablePaths(resourcePaths);
        if (playablePaths.Length == 0)
            return;

        EnsureAudioSources();
        if (IsPlayingResourcePlaylist(playablePaths, shuffle))
        {
            ApplyVolumes();
            return;
        }

        StopPlaylist();
        StopFadeOut();
        StopMusicLoad();
        StopCurrentMusicClip();

        _activeTrackPath = null;
        _activeTrackLoop = false;
        _clipPlaylist = null;
        _requestedPlaylistPaths = playablePaths.ToArray();
        _activePlaylistPaths = shuffle
            ? playablePaths.Shuffle().ToArray()
            : playablePaths.ToArray();
        _playlistIndex = 0;
        _shufflePlaylist = shuffle;

        _playlistCoroutine = StartCoroutine(RunPathPlaylist());
    }

    /// <summary>
    /// Plays tracks selected on demand by a dynamic content-path provider.
    /// </summary>
    /// <param name="nextTrackPathProvider">The provider invoked before each track load.</param>
    internal void PlayDynamicPlaylist(Func<string> nextTrackPathProvider)
    {
        if (nextTrackPathProvider == null)
            throw new ArgumentNullException(nameof(nextTrackPathProvider));

        EnsureAudioSources();
        if (_nextTrackPathProvider == nextTrackPathProvider && _playlistCoroutine != null)
        {
            ApplyVolumes();
            return;
        }

        StopPlaylist();
        StopFadeOut();
        StopMusicLoad();
        StopCurrentMusicClip();

        _activeTrackPath = null;
        _activeTrackLoop = false;
        _nextTrackPathProvider = nextTrackPathProvider;
        _playlistCoroutine = StartCoroutine(RunDynamicPathPlaylist());
    }

    /// <summary>
    /// Stops playlist sequencing without stopping the currently assigned music clip.
    /// </summary>
    public void StopPlaylist()
    {
        StopPlaylistCoroutine();

        _clipPlaylist = null;
        _activePlaylistPaths = null;
        _requestedPlaylistPaths = null;
        _nextTrackPathProvider = null;
        _playlistIndex = 0;
        _shufflePlaylist = false;
    }

    /// <summary>
    /// Stops all music playback.
    /// </summary>
    public void StopMusic()
    {
        EnsureAudioSources();
        StopPlaylist();
        StopFadeOut();
        StopMusicLoad();

        _activeTrackPath = null;
        _activeTrackLoop = false;
        StopCurrentMusicClip();
    }

    /// <summary>
    /// Fades out the current music source.
    /// </summary>
    /// <param name="duration">The fade duration in seconds.</param>
    public void FadeOutMusic(float duration)
    {
        EnsureAudioSources();
        StopFadeOut();

        _fadeOutCoroutine = StartCoroutine(FadeOut(musicSource, duration));
    }

    /// <summary>
    /// Loads and retains sound-effect clips for immediate resource-path playback.
    /// </summary>
    /// <param name="resourcePaths">The sound-effect resource paths to preload.</param>
    internal void PreloadSfx(IEnumerable<string> resourcePaths)
    {
        if (resourcePaths == null)
            return;

        foreach (string resourcePath in resourcePaths)
        {
            if (string.IsNullOrWhiteSpace(resourcePath))
                continue;

            string normalizedPath = resourcePath.Trim();
            if (_preloadedSfx.ContainsKey(normalizedPath))
                continue;

            AudioClip clip = GetContentAssets().GetPreloadedAudio(normalizedPath);
            if (
                clip.loadState == AudioDataLoadState.Unloaded
                && (!clip.LoadAudioData() || clip.loadState == AudioDataLoadState.Failed)
            )
            {
                throw new InvalidOperationException(
                    $"Audio data could not be loaded at: {normalizedPath}"
                );
            }

            if (clip.loadState != AudioDataLoadState.Loaded)
            {
                throw new InvalidOperationException(
                    $"Audio data was not ready at: {normalizedPath}"
                );
            }

            _preloadedSfx.Add(normalizedPath, clip);
        }
    }

    /// <summary>
    /// Plays one sound effect loaded from a content address.
    /// </summary>
    /// <param name="resourcePath">The content address for the sound effect clip.</param>
    public void PlaySfx(string resourcePath)
    {
        if (string.IsNullOrWhiteSpace(resourcePath))
            return;

        string normalizedPath = resourcePath.Trim();
        if (_preloadedSfx.TryGetValue(normalizedPath, out AudioClip preloadedClip))
        {
            PlaySfx(preloadedClip);
            return;
        }

        if (_loadedSfx.TryGetValue(normalizedPath, out AudioClip loadedClip))
        {
            PlaySfx(loadedClip);
            return;
        }

        if (!_sfxLoads.ContainsKey(normalizedPath))
            StartCoroutine(LoadAndPlaySfx(normalizedPath));
    }

    /// <summary>
    /// Plays one already loaded sound effect clip.
    /// </summary>
    /// <param name="clip">The sound effect clip to play.</param>
    /// <param name="volumeScale">The per-effect volume scale.</param>
    public void PlaySfx(AudioClip clip, float volumeScale = 1f)
    {
        if (clip == null)
            return;

        EnsureAudioSources();
        sfxSource.PlayOneShot(clip, volumeScale);
    }

    /// <summary>
    /// Stops sound-effect playback immediately.
    /// </summary>
    public void StopSfx()
    {
        EnsureAudioSources();
        sfxSource.Stop();
        StopMessageAudio();
    }

    /// <summary>
    /// Replaces message-detail audio with an ordered sequence of addressed clips.
    /// </summary>
    /// <param name="resourcePaths">The content addresses to play in order.</param>
    public void PlayMessageAudio(IReadOnlyList<string> resourcePaths)
    {
        StopMessageAudio();

        string[] playablePaths =
            resourcePaths
                ?.Where(resourcePath => !string.IsNullOrWhiteSpace(resourcePath))
                .Select(resourcePath => resourcePath.Trim())
                .ToArray()
            ?? Array.Empty<string>();
        if (playablePaths.Length == 0)
            return;

        _messagePlaybackCoroutine = StartCoroutine(PlayMessageAudioSequence(playablePaths));
    }

    /// <summary>
    /// Stops message-detail audio and discards its remaining sequence.
    /// </summary>
    public void StopMessageAudio()
    {
        if (_messagePlaybackCoroutine != null)
        {
            StopCoroutine(_messagePlaybackCoroutine);
            _messagePlaybackCoroutine = null;
        }

        EnsureAudioSources();
        _messageAudioPaused = false;
        messageSource.Stop();
        messageSource.clip = null;
    }

    /// <summary>
    /// Pauses active sound-effect playback without resetting its position.
    /// </summary>
    public void PauseSfx()
    {
        EnsureAudioSources();
        sfxSource.Pause();
        _messageAudioPaused = messageSource.isPlaying;
        messageSource.Pause();
    }

    /// <summary>
    /// Resumes sound-effect playback from its paused position.
    /// </summary>
    public void ResumeSfx()
    {
        EnsureAudioSources();
        sfxSource.UnPause();
        messageSource.UnPause();
        _messageAudioPaused = false;
    }

    /// <summary>
    /// Plays ambience from an already loaded clip.
    /// </summary>
    /// <param name="clip">The ambience clip to play.</param>
    /// <param name="loop">Whether the ambience clip should loop.</param>
    public void PlayAmbience(AudioClip clip, bool loop = false)
    {
        if (clip == null)
            return;

        EnsureAudioSources();
        ambienceSource.clip = clip;
        ambienceSource.loop = loop;
        ApplyVolumes();
        ambienceSource.Play();
    }

    /// <summary>
    /// Applies persisted audio settings to the active audio sources.
    /// </summary>
    /// <param name="settings">The settings to apply.</param>
    public void ApplySettings(UserAudioSettings settings)
    {
        settings ??= new UserAudioSettings();
        settings.Normalize();

        masterVolume = settings.MasterVolume;
        musicVolume = settings.MusicVolume;
        sfxVolume = settings.SfxVolume;
        ambienceVolume = settings.AmbienceVolume;
        videoVolume = settings.VideoVolume;

        ApplyVolumes();
    }

    /// <summary>
    /// Captures the current audio state as persisted settings.
    /// </summary>
    /// <returns>The current audio settings.</returns>
    public UserAudioSettings CreateSettingsSnapshot()
    {
        return new UserAudioSettings
        {
            MasterVolume = masterVolume,
            MusicVolume = musicVolume,
            SfxVolume = sfxVolume,
            AmbienceVolume = ambienceVolume,
            VideoVolume = videoVolume,
        };
    }

    /// <summary>
    /// Sets the master volume.
    /// </summary>
    /// <param name="volume">The requested volume.</param>
    public void SetMasterVolume(float volume)
    {
        masterVolume = Mathf.Clamp01(volume);
        ApplyVolumes();
    }

    /// <summary>
    /// Sets the music channel volume.
    /// </summary>
    /// <param name="volume">The requested volume.</param>
    public void SetMusicVolume(float volume)
    {
        musicVolume = Mathf.Clamp01(volume);
        ApplyVolumes();
    }

    /// <summary>
    /// Sets the sound effect channel volume.
    /// </summary>
    /// <param name="volume">The requested volume.</param>
    public void SetSfxVolume(float volume)
    {
        sfxVolume = Mathf.Clamp01(volume);
        ApplyVolumes();
    }

    /// <summary>
    /// Sets the ambience channel volume.
    /// </summary>
    /// <param name="volume">The requested volume.</param>
    public void SetAmbienceVolume(float volume)
    {
        ambienceVolume = Mathf.Clamp01(volume);
        ApplyVolumes();
    }

    /// <summary>
    /// Sets the video channel volume.
    /// </summary>
    /// <param name="volume">The requested volume.</param>
    public void SetVideoVolume(float volume)
    {
        videoVolume = Mathf.Clamp01(volume);
    }

    /// <summary>
    /// Makes this component the active singleton and initializes its audio sources.
    /// </summary>
    private void BecomeInstance()
    {
        Instance = this;
        if (GetComponentInParent<AppBootstrap>() == null)
        {
            transform.SetParent(null);
            if (Application.isPlaying)
                DontDestroyOnLoad(gameObject);
        }

        EnsureAudioSources();
        ApplyVolumes();
    }

    /// <summary>
    /// Ensures every playback channel has an AudioSource.
    /// </summary>
    private void EnsureAudioSources()
    {
        if (musicSource == null)
            musicSource = CreateAudioSource();
        if (sfxSource == null)
            sfxSource = CreateAudioSource();
        if (messageSource == null)
            messageSource = CreateAudioSource();
        if (ambienceSource == null)
            ambienceSource = CreateAudioSource();
    }

    /// <summary>
    /// Creates an AudioSource configured for two-dimensional UI and music playback.
    /// </summary>
    /// <returns>The configured audio source.</returns>
    private AudioSource CreateAudioSource()
    {
        AudioSource source = gameObject.AddComponent<AudioSource>();
        source.playOnAwake = false;
        source.spatialBlend = 0f;
        return source;
    }

    /// <summary>
    /// Applies the current volume settings to every persistent channel source.
    /// </summary>
    private void ApplyVolumes()
    {
        EnsureAudioSources();
        musicSource.volume = musicVolume * masterVolume;
        ambienceSource.volume = ambienceVolume * masterVolume;
        sfxSource.volume = sfxVolume * masterVolume;
        messageSource.volume = sfxVolume * masterVolume;
    }

    /// <summary>
    /// Returns normalized, playable content addresses from a requested playlist.
    /// </summary>
    /// <param name="resourcePaths">The requested playlist paths.</param>
    /// <returns>The playable paths.</returns>
    private static string[] GetPlayablePaths(string[] resourcePaths)
    {
        return resourcePaths
                ?.Where(resourcePath => !string.IsNullOrWhiteSpace(resourcePath))
                .ToArray()
            ?? new string[0];
    }

    /// <summary>
    /// Returns whether the requested content track is already playing.
    /// </summary>
    /// <param name="resourcePath">The requested content address.</param>
    /// <param name="loop">The requested loop state.</param>
    /// <returns>True when the requested track is already active.</returns>
    private bool IsPlayingResourceTrack(string resourcePath, bool loop)
    {
        return musicSource?.isPlaying == true
            && _activeTrackPath == resourcePath
            && musicSource.loop == loop
            && _activePlaylistPaths == null;
    }

    /// <summary>
    /// Returns whether the requested path playlist is already playing.
    /// </summary>
    /// <param name="resourcePaths">The requested playlist paths.</param>
    /// <param name="shuffle">The requested shuffle state.</param>
    /// <returns>True when the requested playlist is already active.</returns>
    private bool IsPlayingResourcePlaylist(string[] resourcePaths, bool shuffle)
    {
        return musicSource?.isPlaying == true
            && _activePlaylistPaths != null
            && _requestedPlaylistPaths != null
            && _shufflePlaylist == shuffle
            && resourcePaths.SequenceEqual(_requestedPlaylistPaths);
    }

    /// <summary>
    /// Stops the active playlist coroutine.
    /// </summary>
    private void StopPlaylistCoroutine()
    {
        if (_playlistCoroutine == null)
            return;

        StopCoroutine(_playlistCoroutine);
        _playlistCoroutine = null;
    }

    /// <summary>
    /// Stops the active fade coroutine.
    /// </summary>
    private void StopFadeOut()
    {
        if (_fadeOutCoroutine == null)
            return;

        StopCoroutine(_fadeOutCoroutine);
        _fadeOutCoroutine = null;
    }

    /// <summary>
    /// Plays the active clip playlist until it is stopped or replaced.
    /// </summary>
    /// <returns>The coroutine enumerator.</returns>
    private IEnumerator RunClipPlaylist()
    {
        while (HasClipPlaylist())
        {
            musicSource.clip = _clipPlaylist[_playlistIndex];
            musicSource.loop = false;
            ApplyVolumes();
            musicSource.Play();

            while (musicSource.isPlaying)
                yield return null;

            AdvancePlaylistIndex(_clipPlaylist.Length);
        }

        _playlistCoroutine = null;
    }

    /// <summary>
    /// Plays the active path playlist until it is stopped or replaced.
    /// </summary>
    /// <returns>The coroutine enumerator.</returns>
    private IEnumerator RunPathPlaylist()
    {
        while (HasPathPlaylist())
        {
            string path = _activePlaylistPaths[_playlistIndex];
            BeginMusicClipLoad(path);
            while (!_musicClipLoad.IsCompleted)
                yield return null;

            if (!AssignLoadedMusicClip())
            {
                AdvancePlaylistIndex(_activePlaylistPaths.Length);
                yield return null;
                continue;
            }

            musicSource.loop = false;
            ApplyVolumes();
            musicSource.Play();

            while (musicSource.isPlaying)
                yield return null;

            StopCurrentMusicClip();
            AdvancePlaylistIndex(_activePlaylistPaths.Length);
        }

        _playlistCoroutine = null;
    }

    /// <summary>
    /// Plays tracks selected by the active dynamic playlist provider until it is stopped.
    /// </summary>
    /// <returns>The playlist coroutine.</returns>
    private IEnumerator RunDynamicPathPlaylist()
    {
        while (_nextTrackPathProvider != null)
        {
            string resourcePath = _nextTrackPathProvider();
            if (string.IsNullOrWhiteSpace(resourcePath))
            {
                yield return null;
                continue;
            }

            BeginMusicClipLoad(resourcePath.Trim());
            while (!_musicClipLoad.IsCompleted)
                yield return null;
            if (!AssignLoadedMusicClip())
            {
                yield return null;
                continue;
            }

            musicSource.loop = false;
            ApplyVolumes();
            musicSource.Play();

            yield return null;
            while (musicSource.isPlaying && _nextTrackPathProvider != null)
                yield return null;

            if (musicSource.isPlaying)
                yield break;

            StopCurrentMusicClip();
        }

        _nextTrackPathProvider = null;
        _playlistCoroutine = null;
    }

    /// <summary>
    /// Loads one addressed music track and starts playback when it is ready.
    /// </summary>
    /// <param name="path">The music content address.</param>
    /// <param name="loop">Whether the loaded track should loop.</param>
    /// <returns>The loading coroutine.</returns>
    private IEnumerator LoadAndPlayTrack(string path, bool loop)
    {
        BeginMusicClipLoad(path);
        while (!_musicClipLoad.IsCompleted)
            yield return null;

        if (AssignLoadedMusicClip())
        {
            musicSource.loop = loop;
            ApplyVolumes();
            musicSource.Play();
        }
        else
        {
            _activeTrackPath = null;
            _activeTrackLoop = false;
        }

        _musicLoadCoroutine = null;
    }

    /// <summary>
    /// Loads one addressed sound effect, caches it, and plays it once.
    /// </summary>
    /// <param name="path">The sound-effect content address.</param>
    /// <returns>The loading coroutine.</returns>
    private IEnumerator LoadAndPlaySfx(string path)
    {
        Task<AudioClip> load = GetContentAssets().LoadAudioAsync(path);
        _sfxLoads.Add(path, load);
        while (!load.IsCompleted)
            yield return null;

        _sfxLoads.Remove(path);
        if (load.IsCompletedSuccessfully && load.Result != null)
        {
            _loadedSfx[path] = load.Result;
            PlaySfx(load.Result);
            yield break;
        }

        if (load.IsCanceled)
            yield break;

        string failureReason = load.IsFaulted
            ? $": {load.Exception?.GetBaseException().Message}"
            : ": the loader returned no audio clip";
        GameLogger.Warning($"Failed to load sound effect '{path}'{failureReason}.");
    }

    /// <summary>
    /// Loads and plays message-detail clips one at a time in their requested order.
    /// </summary>
    /// <param name="paths">The normalized content addresses to play.</param>
    /// <returns>The playback coroutine.</returns>
    private IEnumerator PlayMessageAudioSequence(IReadOnlyList<string> paths)
    {
        foreach (string path in paths)
        {
            AudioClip clip = null;
            if (
                !_preloadedSfx.TryGetValue(path, out clip)
                && !_loadedSfx.TryGetValue(path, out clip)
            )
            {
                Task<AudioClip> load = GetContentAssets().LoadAudioAsync(path);
                while (!load.IsCompleted)
                    yield return null;

                if (load.IsCompletedSuccessfully && load.Result != null)
                {
                    clip = load.Result;
                    _loadedSfx[path] = clip;
                }
                else if (!load.IsCanceled)
                {
                    string failureReason = load.IsFaulted
                        ? $": {load.Exception?.GetBaseException().Message}"
                        : ": the loader returned no audio clip";
                    GameLogger.Warning($"Failed to load message audio '{path}'{failureReason}.");
                }
            }

            if (clip == null)
                continue;

            messageSource.clip = clip;
            messageSource.loop = false;
            messageSource.Play();
            while (messageSource.isPlaying || _messageAudioPaused)
                yield return null;
        }

        messageSource.clip = null;
        _messagePlaybackCoroutine = null;
    }

    /// <summary>
    /// Starts the shared content-asset task for one music address.
    /// </summary>
    /// <param name="path">The music content address.</param>
    private void BeginMusicClipLoad(string path)
    {
        _musicClipLoad = GetContentAssets().LoadAudioAsync(path);
    }

    /// <summary>
    /// Assigns a successfully loaded music clip to the music source.
    /// </summary>
    /// <returns>True when the completed load produced a clip.</returns>
    private bool AssignLoadedMusicClip()
    {
        if (_musicClipLoad?.IsCompletedSuccessfully != true || _musicClipLoad.Result == null)
        {
            _musicClipLoad = null;
            return false;
        }

        musicSource.clip = _musicClipLoad.Result;
        return true;
    }

    /// <summary>
    /// Stops the active music-loading coroutine and releases its pending task reference.
    /// </summary>
    private void StopMusicLoad()
    {
        if (_musicLoadCoroutine == null)
            return;

        StopCoroutine(_musicLoadCoroutine);
        _musicLoadCoroutine = null;
        _musicClipLoad = null;
    }

    /// <summary>
    /// Stops and unassigns the currently active music clip.
    /// </summary>
    private void StopCurrentMusicClip()
    {
        if (musicSource != null)
        {
            musicSource.Stop();
            musicSource.clip = null;
        }

        _musicClipLoad = null;
    }

    /// <summary>
    /// Gets the content asset store required for addressed audio operations.
    /// </summary>
    /// <returns>The active content asset store.</returns>
    private ContentAssets GetContentAssets()
    {
        return _contentAssets
            ?? throw new InvalidOperationException(
                "AudioManager has not been initialized with a content asset store."
            );
    }

    /// <summary>
    /// Returns whether a clip playlist is active.
    /// </summary>
    /// <returns>True when a clip playlist is active.</returns>
    private bool HasClipPlaylist()
    {
        return _clipPlaylist?.Length > 0;
    }

    /// <summary>
    /// Returns whether a path playlist is active.
    /// </summary>
    /// <returns>True when a path playlist is active.</returns>
    private bool HasPathPlaylist()
    {
        return _activePlaylistPaths?.Length > 0;
    }

    /// <summary>
    /// Advances the active playlist index.
    /// </summary>
    /// <param name="playlistLength">The active playlist length.</param>
    private void AdvancePlaylistIndex(int playlistLength)
    {
        _playlistIndex++;
        if (_playlistIndex >= playlistLength)
            _playlistIndex = 0;
    }

    /// <summary>
    /// Fades out one audio source.
    /// </summary>
    /// <param name="source">The source to fade.</param>
    /// <param name="duration">The fade duration in seconds.</param>
    /// <returns>The coroutine enumerator.</returns>
    private IEnumerator FadeOut(AudioSource source, float duration)
    {
        if (duration <= 0f)
        {
            source.Stop();
            ApplyVolumes();
            _fadeOutCoroutine = null;
            yield break;
        }

        float startVolume = source.volume;
        float time = 0f;

        while (time < duration)
        {
            time += Time.unscaledDeltaTime;
            source.volume = Mathf.Lerp(startVolume, 0f, time / duration);
            yield return null;
        }

        source.Stop();
        ApplyVolumes();
        _fadeOutCoroutine = null;
    }
}
