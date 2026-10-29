using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Owns selection-independent squadron launch cues and their short-lived, bounded playback queue.
/// </summary>
public sealed class TacticalFlightAudio : IDisposable
{
    private const int _maximumConcurrentEffects = 5;
    private const float _maximumQueueAge = 0.225f;
    private readonly AudioManager _audio;
    private readonly Queue<float> _pending = new Queue<float>();
    private readonly AudioSource[] _sources = new AudioSource[_maximumConcurrentEffects];
    private GameObject _root;
    private float _elapsed;

    /// <summary>
    /// Creates scene-owned two-dimensional sources for preloaded tactical launch audio.
    /// </summary>
    /// <param name="parent">The owning tactical scene.</param>
    /// <param name="launchClip">The application-owned, preloaded squadron launch sound.</param>
    /// <param name="audio">The application volume owner.</param>
    public TacticalFlightAudio(Transform parent, AudioClip launchClip, AudioManager audio)
    {
        if (!parent)
            throw new ArgumentNullException(nameof(parent));
        if (!launchClip)
            throw new ArgumentNullException(nameof(launchClip));
        _audio = audio ? audio : throw new ArgumentNullException(nameof(audio));
        _root = new GameObject("Carrier Flight Audio");
        _root.transform.SetParent(parent, false);
        for (int index = 0; index < _sources.Length; index++)
        {
            AudioSource source = _root.AddComponent<AudioSource>();
            source.clip = launchClip;
            source.playOnAwake = false;
            source.spatialBlend = 0f;
            source.dopplerLevel = 0f;
            _sources[index] = source;
        }
    }

    /// <summary>
    /// Receives one successful squadron release without depending on player selection or camera state.
    /// </summary>
    /// <param name="carrier">The carrier that released the squadron.</param>
    public void PlayLaunch(Rebellion.Game.Combat.SpaceCombatUnit carrier)
    {
        if (carrier == null)
            throw new ArgumentNullException(nameof(carrier));
        if (_root == null)
            return;
        _pending.Enqueue(_elapsed);
        Advance(0f);
    }

    /// <summary>
    /// Refreshes volume and starts recent queued effects as their audio sources become available.
    /// </summary>
    /// <param name="deltaTime">Elapsed real seconds, independent of tactical pause.</param>
    public void Advance(float deltaTime)
    {
        if (!float.IsFinite(deltaTime) || deltaTime < 0f)
            throw new ArgumentOutOfRangeException(nameof(deltaTime));
        if (_root == null)
            return;
        _elapsed += deltaTime;
        while (_pending.Count > 0 && _elapsed - _pending.Peek() > _maximumQueueAge)
            _pending.Dequeue();
        foreach (AudioSource source in _sources)
        {
            source.volume = _audio.MasterVolume * _audio.SfxVolume;
            if (source.isPlaying || _pending.Count == 0)
                continue;
            _pending.Dequeue();
            source.Play();
        }
    }

    /// <summary>
    /// Stops only this scene's effects and releases their sources without destroying shared clips.
    /// </summary>
    public void Dispose()
    {
        _pending.Clear();
        if (!_root)
            return;
        foreach (AudioSource source in _sources)
            source.Stop();
        if (Application.isPlaying)
            UnityEngine.Object.Destroy(_root);
        else
            UnityEngine.Object.DestroyImmediate(_root);
        _root = null;
    }
}
