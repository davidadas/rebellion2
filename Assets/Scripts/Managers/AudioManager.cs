using System.Collections;
using System.Linq;
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

    private AudioClip[] _clipPlaylist;
    private string[] _activePlaylistPaths;
    private string[] _requestedPlaylistPaths;
    private int _playlistIndex;
    private bool _shufflePlaylist;
    private string _activeTrackPath;
    private Coroutine _playlistCoroutine;
    private Coroutine _fadeOutCoroutine;

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
    /// Gets the ambience channel volume.
    /// </summary>
    public float AmbienceVolume => ambienceVolume;

    /// <summary>
    /// Returns the global audio manager, creating one when the scene does not already contain it.
    /// </summary>
    /// <param name="parent">The optional parent to use for a newly created manager.</param>
    /// <returns>The active audio manager.</returns>
    public static AudioManager EnsureExists(Transform parent = null)
    {
        if (Instance != null)
            return Instance;

        AudioManager existing = Object.FindAnyObjectByType<AudioManager>();
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
        if (Instance == this)
            Instance = null;
    }

    /// <summary>
    /// Plays one music track loaded from a Resources path.
    /// </summary>
    /// <param name="resourcePath">The Resources path for the music clip.</param>
    /// <param name="loop">Whether the track should loop.</param>
    public void PlayTrack(string resourcePath, bool loop = false)
    {
        if (string.IsNullOrWhiteSpace(resourcePath))
            return;

        EnsureAudioSources();
        if (IsPlayingResourceTrack(resourcePath, loop))
        {
            ApplyVolumes();
            return;
        }

        AudioClip clip = ResourceManager.GetAudio(resourcePath);
        if (clip == null)
            return;

        PlayTrack(clip, loop);
        _activeTrackPath = resourcePath;
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

        _activeTrackPath = null;
        musicSource.clip = clip;
        musicSource.loop = loop;
        ApplyVolumes();
        musicSource.Play();
    }

    /// <summary>
    /// Plays a playlist from already loaded clips.
    /// </summary>
    /// <param name="clips">The clips to play.</param>
    /// <param name="shuffle">Whether the playlist should be shuffled before playback.</param>
    public void PlayPlaylist(AudioClip[] clips, bool shuffle = false)
    {
        AudioClip[] playableClips = clips?.Where(clip => clip != null).ToArray();
        if (playableClips == null || playableClips.Length == 0)
            return;

        EnsureAudioSources();
        StopPlaylist();
        StopFadeOut();

        _activeTrackPath = null;
        _activePlaylistPaths = null;
        _requestedPlaylistPaths = null;
        _clipPlaylist = shuffle ? playableClips.Shuffle().ToArray() : playableClips;
        _playlistIndex = 0;
        _shufflePlaylist = shuffle;

        _playlistCoroutine = StartCoroutine(RunClipPlaylist());
    }

    /// <summary>
    /// Plays a playlist from Resources paths.
    /// </summary>
    /// <param name="resourcePaths">The Resources paths for the playlist tracks.</param>
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

        _activeTrackPath = null;
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
    /// Stops playlist sequencing without stopping the currently assigned music clip.
    /// </summary>
    public void StopPlaylist()
    {
        StopPlaylistCoroutine();

        _clipPlaylist = null;
        _activePlaylistPaths = null;
        _requestedPlaylistPaths = null;
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

        _activeTrackPath = null;
        musicSource.Stop();
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
    /// Plays one sound effect loaded from a Resources path.
    /// </summary>
    /// <param name="resourcePath">The Resources path for the sound effect clip.</param>
    public void PlaySfx(string resourcePath)
    {
        if (string.IsNullOrWhiteSpace(resourcePath))
            return;

        PlaySfx(ResourceManager.GetAudio(resourcePath));
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
        sfxSource.PlayOneShot(clip, volumeScale * sfxVolume * masterVolume);
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
    }

    /// <summary>
    /// Returns normalized, playable resource paths from a requested playlist.
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
    /// Returns whether the requested resource track is already playing.
    /// </summary>
    /// <param name="resourcePath">The requested Resources path.</param>
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
            musicSource.clip = ResourceManager.GetAudio(_activePlaylistPaths[_playlistIndex]);
            musicSource.loop = false;
            ApplyVolumes();
            musicSource.Play();

            while (musicSource.isPlaying)
                yield return null;

            AdvancePlaylistIndex(_activePlaylistPaths.Length);
        }

        _playlistCoroutine = null;
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
