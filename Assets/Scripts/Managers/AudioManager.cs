using System.Collections;
using System.Linq;
using Rebellion.Util.Extensions;
using UnityEngine;

/// <summary>
/// Centralized audio controller responsible for music, SFX, ambience,
/// and playlist sequencing across the game. Supports both preloaded
/// AudioClip playlists and just-in-time loading using resource paths.
/// </summary>
public class AudioManager : MonoBehaviour
{
    public static AudioManager Instance { get; private set; }

    [Header("Sources")]
    [SerializeField]
    private AudioSource musicSource;

    [SerializeField]
    private AudioSource sfxSource;

    [SerializeField]
    private AudioSource ambienceSource;

    [Header("Settings")]
    [Range(0f, 1f)]
    public float masterVolume = 1f;

    [Range(0f, 1f)]
    public float musicVolume = 1f;

    [Range(0f, 1f)]
    public float sfxVolume = 1f;

    [Range(0f, 1f)]
    public float ambienceVolume = 1f;

    private AudioClip[] _currentPlaylist;
    private string[] _currentPlaylistPaths;
    private int _playlistIndex;
    private bool _playlistShuffle;

    /// <summary>
    /// Ensures singleton instance and persists across scene loads.
    /// </summary>
    private void Awake()
    {
        if (Instance != null)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
        ApplyVolumes();
    }

    /// <summary>
    /// Plays a single music track from a Resources path.
    /// </summary>
    public void PlayTrack(string resourcePath, bool loop = false)
    {
        AudioClip clip = ResourceManager.Instance.GetAudio(resourcePath);
        PlayTrack(clip, loop);
    }

    /// <summary>
    /// Plays a single music clip.
    /// </summary>
    public void PlayTrack(AudioClip clip, bool loop)
    {
        if (clip == null)
            return;

        StopPlaylist();

        musicSource.clip = clip;
        musicSource.loop = loop;

        ApplyVolumes(); // ← ADD THIS

        musicSource.Play();
    }

    /// <summary>
    /// Plays a playlist of preloaded AudioClips.
    /// </summary>
    public void PlayPlaylist(AudioClip[] tracks, bool shuffle = false)
    {
        if (tracks == null || tracks.Length == 0)
        {
            return;
        }

        StopPlaylist();

        _currentPlaylist = new AudioClip[tracks.Length];
        for (int i = 0; i < tracks.Length; i++)
        {
            _currentPlaylist[i] = tracks[i];
        }

        _playlistIndex = 0;
        _playlistShuffle = shuffle;

        if (_playlistShuffle)
        {
            _currentPlaylist = _currentPlaylist.Shuffle().ToArray();
        }

        PlayNextTrack();
    }

    /// <summary>
    /// Plays a playlist using resource paths (loaded just-in-time).
    /// </summary>
    public void PlayPlaylistPaths(string[] paths, bool shuffle = false)
    {
        if (paths == null || paths.Length == 0)
        {
            return;
        }

        StopPlaylist();

        _currentPlaylistPaths = new string[paths.Length];
        for (int i = 0; i < paths.Length; i++)
        {
            _currentPlaylistPaths[i] = paths[i];
        }

        _playlistIndex = 0;
        _playlistShuffle = shuffle;

        if (_playlistShuffle)
        {
            _currentPlaylistPaths = _currentPlaylistPaths.Shuffle().ToArray();
        }

        PlayNextTrackFromPaths();
    }

    /// <summary>
    /// Stops any active playlist and coroutines.
    /// </summary>
    public void StopPlaylist()
    {
        StopAllCoroutines();

        _currentPlaylist = null;
        _currentPlaylistPaths = null;
        _playlistIndex = 0;
    }

    /// <summary>
    /// Stops currently playing music.
    /// </summary>
    public void StopMusic()
    {
        musicSource.Stop();
        StopPlaylist();
    }

    /// <summary>
    /// Fades out music over a duration.
    /// </summary>
    public void FadeOutMusic(float duration)
    {
        StartCoroutine(FadeOut(musicSource, duration));
    }

    /// <summary>
    /// Coroutine to fade out an AudioSource.
    /// </summary>
    private IEnumerator FadeOut(AudioSource source, float duration)
    {
        float startVolume = source.volume;
        float time = 0f;

        while (time < duration)
        {
            time += Time.unscaledDeltaTime;

            float newVolume = Mathf.Lerp(startVolume, 0f, time / duration);
            source.volume = newVolume;

            yield return null;
        }

        source.Stop();

        // Restore proper volume from settings (NOT previous value)
        ApplyVolumes();
    }

    /// <summary>
    /// Plays a sound effect from a Resources path.
    /// </summary>
    public void PlaySFX(string resourcePath)
    {
        AudioClip clip = ResourceManager.Instance.GetAudio(resourcePath);
        // TODO: Pull volume from config.
        PlaySFX(clip, 1f);
    }

    /// <summary>
    /// Plays a sound effect clip.
    /// </summary>
    public void PlaySFX(AudioClip clip, float volumeScale = 1f)
    {
        if (clip == null)
        {
            return;
        }

        float volume = volumeScale * sfxVolume * masterVolume;
        sfxSource.PlayOneShot(clip, volume);
    }

    /// <summary>
    /// Plays looping ambience.
    /// </summary>
    public void PlayAmbience(AudioClip clip, bool loop)
    {
        if (clip == null)
        {
            return;
        }

        ambienceSource.clip = clip;
        ambienceSource.loop = loop;
        ambienceSource.Play();
    }

    /// <summary>
    /// Applies volume multipliers to audio sources.
    /// </summary>
    private void ApplyVolumes()
    {
        musicSource.volume = musicVolume * masterVolume;
        ambienceSource.volume = ambienceVolume * masterVolume;
        sfxSource.volume = sfxVolume * masterVolume;
    }

    /// <summary>
    /// Plays next track in AudioClip playlist.
    /// </summary>
    private void PlayNextTrack()
    {
        if (_currentPlaylist == null || _currentPlaylist.Length == 0)
        {
            return;
        }

        musicSource.clip = _currentPlaylist[_playlistIndex];
        musicSource.loop = false;
        musicSource.Play();

        StartCoroutine(WaitForTrackEndClip());
    }

    /// <summary>
    /// Plays next track in path-based playlist.
    /// </summary>
    private void PlayNextTrackFromPaths()
    {
        if (_currentPlaylistPaths == null || _currentPlaylistPaths.Length == 0)
        {
            return;
        }

        string path = _currentPlaylistPaths[_playlistIndex];
        AudioClip clip = ResourceManager.Instance.GetAudio(path);

        if (clip == null)
        {
            return;
        }

        musicSource.clip = clip;
        musicSource.loop = false;
        musicSource.Play();

        StartCoroutine(WaitForTrackEndPath());
    }

    /// <summary>
    /// Waits for clip playlist track to finish.
    /// </summary>
    private IEnumerator WaitForTrackEndClip()
    {
        while (musicSource.isPlaying)
        {
            yield return null;
        }

        _playlistIndex++;

        if (_playlistIndex >= _currentPlaylist.Length)
        {
            _playlistIndex = 0;
        }

        PlayNextTrack();
    }

    /// <summary>
    /// Waits for path playlist track to finish.
    /// </summary>
    private IEnumerator WaitForTrackEndPath()
    {
        while (musicSource.isPlaying)
        {
            yield return null;
        }

        _playlistIndex++;

        if (_playlistIndex >= _currentPlaylistPaths.Length)
        {
            _playlistIndex = 0;
        }

        PlayNextTrackFromPaths();
    }
}
