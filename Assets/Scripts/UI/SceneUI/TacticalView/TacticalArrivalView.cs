using System;
using System.Collections.Generic;
using Rebellion.Game.Combat;
using Rebellion.Game.Units;
using Rebellion.SceneGraph;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

/// <summary>
/// Owns transient hyperspace fly-ins, trails, endpoint bursts, and sound without changing battle state.
/// </summary>
public sealed class TacticalArrivalView : IDisposable
{
    private const float _startTime = 0.72f;
    private const float _duration = 0.86f;
    private const float _travelDistance = 135f;
    private const float _trailLifetime = 0.42f;
    private const float _burstLifetime = 0.85f;
    private const float _soundLeadTime = 0.08f;
    private static readonly Color _trailColor = new Color32(0x33, 0xb3, 0xff, 0xd1);
    private static readonly Color _burstColor = new Color32(0x33, 0xb8, 0xff, 0xff);

    private readonly Dictionary<CombatUnit, Arrival> _arrivals =
        new Dictionary<CombatUnit, Arrival>();
    private readonly AudioManager _audioManager;
    private GameObject _effectsRoot;
    private AudioSource _sound;
    private AudioClip _clip;
    private Texture2D _glow;
    private Material _trailMaterial;
    private Material _burstMaterial;
    private float _elapsed;
    private float _endTime;
    private bool _soundPlayed;
    private bool _paused;

    /// <summary>
    /// Stores the presentation resources and timing for one deployed unit.
    /// </summary>
    private sealed class Arrival
    {
        public Transform Root;
        public Vector3 Destination;
        public Vector3 Forward;
        public float StartTime;
        public TrailRenderer Trail;
        public ParticleSystem Burst;
        public bool Complete;
    }

    /// <summary>
    /// Prepares arrival effects after models, deployment, and final camera framing are ready.
    /// </summary>
    /// <param name="parent">The scene owning these temporary effects.</param>
    /// <param name="presentations">The deployed units and their loaded model roots.</param>
    /// <param name="clip">The resident arrival sound, loaded before the sequence starts.</param>
    /// <param name="audioManager">The application sound-volume owner.</param>
    public TacticalArrivalView(
        Transform parent,
        IReadOnlyDictionary<CombatUnit, Transform> presentations,
        AudioClip clip,
        AudioManager audioManager
    )
    {
        if (parent == null)
            throw new ArgumentNullException(nameof(parent));
        if (presentations == null)
            throw new ArgumentNullException(nameof(presentations));
        _clip = clip != null ? clip : throw new ArgumentNullException(nameof(clip));
        _audioManager =
            audioManager != null
                ? audioManager
                : throw new ArgumentNullException(nameof(audioManager));
        try
        {
            _trailMaterial = CreateMaterial(_trailColor);
            _burstMaterial = CreateMaterial(_burstColor);
            _effectsRoot = new GameObject("Hyperspace Arrival");
            _effectsRoot.transform.SetParent(parent, false);
            _sound = _effectsRoot.AddComponent<AudioSource>();
            _sound.playOnAwake = false;
            _sound.spatialBlend = 0f;
            _sound.dopplerLevel = 0f;
            int index = 0;
            foreach (KeyValuePair<CombatUnit, Transform> presentation in presentations)
            {
                if (
                    presentation.Key.HasRetreated
                    || presentation.Key.FlightState != FighterFlightState.Deployed
                    || presentation.Key.GetUnit() is Starfighter { Hyperdrive: <= 0 }
                )
                    continue;
                Transform root = presentation.Value;
                ModelDimensions size = presentation.Key.GetUnit().ModelSize;
                float selectionRadius = Mathf.Max(
                    0.65f,
                    Mathf.Max(size.Width, Mathf.Max(size.Height, size.Depth)) * 0.63f
                );
                TrailRenderer trail = root.gameObject.AddComponent<TrailRenderer>();
                trail.time = _trailLifetime;
                trail.minVertexDistance = 0.8f;
                trail.startWidth = selectionRadius * 0.6f;
                trail.endWidth = 0f;
                trail.startColor = _trailColor;
                trail.endColor = new Color(_trailColor.r, _trailColor.g, _trailColor.b, 0f);
                trail.sharedMaterial = _trailMaterial;
                trail.shadowCastingMode = ShadowCastingMode.Off;
                trail.receiveShadows = false;
                Arrival arrival = new Arrival
                {
                    Root = root,
                    Destination = root.localPosition,
                    Forward = root.localRotation * Vector3.forward,
                    StartTime = _startTime + index % 5 * 0.025f,
                    Trail = trail,
                };
                _arrivals.Add(presentation.Key, arrival);
                root.localPosition = arrival.Destination - arrival.Forward * _travelDistance;
                trail.Clear();
                trail.emitting = true;
                _endTime = Mathf.Max(
                    _endTime,
                    arrival.StartTime + _duration + _burstLifetime + 0.25f
                );
                index++;
            }
        }
        catch
        {
            Dispose();
            throw;
        }
    }

    /// <summary>
    /// Reports whether arrival still owns a unit's presentation pose.
    /// </summary>
    /// <param name="unit">The battle unit being presented.</param>
    /// <returns>Whether the normal movement presenter must wait for this unit's fly-in.</returns>
    public bool IsArriving(CombatUnit unit) =>
        _arrivals.TryGetValue(unit, out Arrival arrival) && !arrival.Complete;

    /// <summary>
    /// Advances arrival presentation and sound on the scene's pausable clock.
    /// </summary>
    /// <param name="deltaTime">The elapsed unpaused battle time.</param>
    /// <param name="paused">Whether the tactical options menu is open.</param>
    /// <returns>Whether the sequence still owns live effects.</returns>
    public bool Advance(float deltaTime, bool paused)
    {
        if (_effectsRoot == null)
            return false;
        _sound.volume = _audioManager.MasterVolume * _audioManager.SfxVolume;
        if (_paused != paused)
        {
            if (paused)
                _sound.Pause();
            else
                _sound.UnPause();
            _paused = paused;
        }
        if (paused)
            return true;
        _elapsed += deltaTime;
        if (!_soundPlayed && _elapsed >= _startTime - _soundLeadTime)
        {
            _sound.PlayOneShot(_clip, 0.95f);
            _soundPlayed = true;
        }
        foreach (Arrival arrival in _arrivals.Values)
        {
            if (arrival.Complete)
            {
                arrival.Burst.Simulate(deltaTime, true, false);
                continue;
            }
            if (_elapsed < arrival.StartTime)
                continue;
            float progress = Mathf.Clamp01((_elapsed - arrival.StartTime) / _duration);
            float easedProgress = 1f - Mathf.Pow(1f - progress, 4f);
            arrival.Root.localPosition =
                arrival.Destination - arrival.Forward * (_travelDistance * (1f - easedProgress));
            if (progress < 1f)
                continue;
            arrival.Complete = true;
            arrival.Trail.emitting = false;
            arrival.Burst = CreateBurst(arrival.Root.position);
        }
        return _elapsed < _endTime || _sound.isPlaying;
    }

    /// <summary>
    /// Stops sound and releases every temporary trail, particle system, material, and texture.
    /// </summary>
    public void Dispose()
    {
        if (_sound)
            _sound.Stop();
        foreach (Arrival arrival in _arrivals.Values)
        {
            if (!arrival.Complete && arrival.Root != null)
                arrival.Root.localPosition = arrival.Destination;
            if (arrival.Trail != null)
            {
                arrival.Trail.emitting = false;
                arrival.Trail.Clear();
                DestroyOwned(arrival.Trail);
            }
        }
        _arrivals.Clear();
        DestroyOwned(_effectsRoot);
        _effectsRoot = null;
        _sound = null;
        _clip = null;
        DestroyOwned(_trailMaterial);
        _trailMaterial = null;
        DestroyOwned(_burstMaterial);
        _burstMaterial = null;
        DestroyOwned(_glow);
        _glow = null;
    }

    /// <summary>
    /// Creates the short world-space particle burst at a completed fly-in.
    /// </summary>
    /// <param name="position">The unit's world-space arrival point.</param>
    /// <returns>The stopped particle system advanced by the arrival clock.</returns>
    private ParticleSystem CreateBurst(Vector3 position)
    {
        GameObject burstObject = new GameObject("Arrival Burst");
        burstObject.transform.SetParent(_effectsRoot.transform, false);
        burstObject.transform.position = position;
        ParticleSystem particles = burstObject.AddComponent<ParticleSystem>();
        particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        ParticleSystem.MainModule main = particles.main;
        main.loop = false;
        main.playOnAwake = false;
        main.duration = 0.1f;
        main.startLifetime = new ParticleSystem.MinMaxCurve(_burstLifetime * 0.5f, _burstLifetime);
        main.startSpeed = new ParticleSystem.MinMaxCurve(6.5f * 0.35f, 6.5f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.08f, 0.42f);
        main.startColor = _burstColor;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        ParticleSystem.EmissionModule emission = particles.emission;
        emission.enabled = false;
        ParticleSystem.ShapeModule shape = particles.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = 0.45f;
        ParticleSystemRenderer renderer = particles.GetComponent<ParticleSystemRenderer>();
        renderer.renderMode = ParticleSystemRenderMode.Stretch;
        renderer.velocityScale = 0.16f;
        renderer.lengthScale = 2.2f;
        renderer.sharedMaterial = _burstMaterial;
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        particles.Emit(12);
        return particles;
    }

    /// <summary>
    /// Creates an additive material using the sequence's shared glow texture.
    /// </summary>
    /// <param name="color">The material tint.</param>
    /// <returns>The sequence-owned material.</returns>
    private Material CreateMaterial(Color color)
    {
        Shader shader = Shader.Find("Rebellion/Unlit Additive");
        if (shader == null)
            throw new InvalidOperationException(
                "The hyperspace arrival additive shader is unavailable."
            );
        if (_glow == null)
            _glow = CreateGlowTexture();
        return new Material(shader) { color = color, mainTexture = _glow };
    }

    /// <summary>
    /// Builds the soft radial alpha texture shared by trails and bursts.
    /// </summary>
    /// <returns>The sequence-owned radial glow texture.</returns>
    private static Texture2D CreateGlowTexture()
    {
        const int size = 32;
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            name = "Arrival Glow",
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp,
        };
        Color[] pixels = new Color[size * size];
        Vector2 center = new Vector2((size - 1) * 0.5f, (size - 1) * 0.5f);
        for (int y = 0; y < size; y++)
        for (int x = 0; x < size; x++)
        {
            float distance = Vector2.Distance(new Vector2(x, y), center) / center.x;
            float alpha = Mathf.Pow(Mathf.Clamp01(1f - distance), 2.4f);
            pixels[y * size + x] = new Color(1f, 1f, 1f, alpha);
        }
        texture.SetPixels(pixels);
        texture.Apply(false, true);
        return texture;
    }

    /// <summary>
    /// Destroys a transient Unity resource safely in play mode or editor previews.
    /// </summary>
    /// <param name="resource">The owned resource to release.</param>
    private static void DestroyOwned(Object resource)
    {
        if (resource == null)
            return;
        if (Application.isPlaying)
            Object.Destroy(resource);
        else
            Object.DestroyImmediate(resource);
    }
}
