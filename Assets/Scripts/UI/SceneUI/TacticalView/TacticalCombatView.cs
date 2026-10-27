using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Rebellion.Game.Combat;
using Rebellion.Game.ShipComponents;
using Rebellion.SceneGraph;
using Rebellion.Tactical;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Presents simulation-owned shots with pooled effects and bounded, data-addressed audio.
/// </summary>
public sealed class TacticalCombatView : IDisposable
{
    private const float _shieldEnvelopeScale = 1.08f;

    private readonly ActiveBattle _battle;
    private readonly TacticalCombat _combat;
    private readonly Camera _camera;
    private readonly Transform _space;
    private readonly AudioManager _audio;
    private readonly Shader _shader;
    private readonly Dictionary<string, AudioClip> _clips;
    private readonly Dictionary<Color, Material> _materials = new();
    private readonly Dictionary<long, LineRenderer> _shots = new();
    private readonly Stack<LineRenderer> _pool = new();
    private readonly List<long> _finished = new();
    private readonly HashSet<long> _live = new();
    private readonly List<Flash> _flashes = new();
    private readonly List<Beam> _beams = new();
    private readonly Dictionary<CombatUnit, ITacticalShieldEffect> _shields = new();
    private readonly List<GameObject> _shieldObjects = new();
    private readonly AudioSource[] _fireSources = new AudioSource[4];
    private readonly AudioSource[] _impactSources = new AudioSource[3];
    private GameObject _root;
    private float _time;
    private float _nextFireSound;
    private float _nextImpactSound;

    /// <summary>
    /// Loads distinct sound addresses before arming scene presentation.
    /// </summary>
    /// <param name="battle">The persistent battle containing weapon definitions.</param>
    /// <param name="assets">The application-owned addressed asset cache.</param>
    /// <returns>Preloaded sound clips, keyed by authored address.</returns>
    public static async Task<Dictionary<string, AudioClip>> LoadAudioAsync(
        ActiveBattle battle,
        ContentAssets assets
    )
    {
        HashSet<string> paths = new HashSet<string>(StringComparer.Ordinal);
        foreach (BattleParticipant participant in battle.GetParticipants())
        foreach (CombatUnit unit in participant.GetCombatants())
        foreach (HardpointGroup group in TacticalCombat.GetGroups(unit))
        foreach (Hardpoint hardpoint in group.GetHardpoints())
        {
            foreach (string path in hardpoint.Weapon.FireSoundPaths)
                paths.Add(path);
            foreach (string path in hardpoint.Weapon.Projectile.ImpactSoundPaths)
                paths.Add(path);
        }
        Dictionary<string, AudioClip> clips = new Dictionary<string, AudioClip>(
            StringComparer.Ordinal
        );
        foreach (string path in paths)
        {
            AudioClip clip = await assets.LoadAudioAsync(path);
            if (!clip)
                throw new InvalidOperationException($"Weapon audio was not found: {path}");
            clips.Add(path, clip);
        }
        return clips;
    }

    /// <summary>
    /// Subscribes one scene-owned presenter to the authoritative combat events.
    /// </summary>
    /// <param name="battle">The persistent battle.</param>
    /// <param name="combat">The simulation owner.</param>
    /// <param name="space">The transform defining battle coordinates.</param>
    /// <param name="camera">The battle camera used for readable shot sizing.</param>
    /// <param name="audio">The application volume owner.</param>
    /// <param name="clips">Preloaded application-owned sound clips.</param>
    /// <param name="shader">The scene-referenced weapon shader retained in player builds.</param>
    /// <param name="presentations">The battle's combatant presentation index.</param>
    /// <param name="shieldTemplate">The inactive authored shield template.</param>
    public TacticalCombatView(
        ActiveBattle battle,
        TacticalCombat combat,
        Transform space,
        Camera camera,
        AudioManager audio,
        Dictionary<string, AudioClip> clips,
        Shader shader,
        IReadOnlyDictionary<CombatUnit, Transform> presentations,
        GameObject shieldTemplate
    )
    {
        _battle = battle ?? throw new ArgumentNullException(nameof(battle));
        _combat = combat ?? throw new ArgumentNullException(nameof(combat));
        _space = space ? space : throw new ArgumentNullException(nameof(space));
        _camera = camera ? camera : throw new ArgumentNullException(nameof(camera));
        _audio = audio ? audio : throw new ArgumentNullException(nameof(audio));
        _clips = clips ?? throw new ArgumentNullException(nameof(clips));
        _shader = shader ? shader : throw new ArgumentNullException(nameof(shader));
        if (presentations == null)
            throw new ArgumentNullException(nameof(presentations));
        if (!shieldTemplate)
            throw new ArgumentNullException(nameof(shieldTemplate));
        _root = new GameObject("Weapon Effects");
        _root.transform.SetParent(space, false);
        CreateShields(presentations, shieldTemplate);
        CreateAudio(_fireSources, "Weapon", 180);
        CreateAudio(_impactSources, "Impact", 120);
        _combat.ShotFired += OnShotFired;
        _combat.ShotHit += OnShotHit;
    }

    /// <summary>
    /// Synchronizes live shots and fades pooled flashes without advancing game state.
    /// </summary>
    /// <param name="deltaTime">Unpaused elapsed battle seconds.</param>
    /// <param name="paused">Whether the scene's modal menu pauses presentation.</param>
    public void Advance(float deltaTime, bool paused)
    {
        if (!_root)
            return;
        foreach (KeyValuePair<CombatUnit, ITacticalShieldEffect> shield in _shields)
            shield.Value.SetActive(
                TacticalCombat.IsAlive(shield.Key) && HasShieldStrength(shield.Key)
            );
        foreach (AudioSource source in _fireSources)
            UpdateAudio(source, paused);
        foreach (AudioSource source in _impactSources)
            UpdateAudio(source, paused);
        if (paused)
            return;
        _time += deltaTime;
        _live.Clear();
        foreach (BattleProjectile shot in _battle.Projectiles)
            RenderShot(shot);
        for (int index = _beams.Count - 1; index >= 0; index--)
        {
            Beam beam = _beams[index];
            if (beam.RemainingSeconds <= 0f)
            {
                _beams.RemoveAt(index);
                continue;
            }
            RenderShot(beam.Shot);
            beam.RemainingSeconds -= deltaTime;
        }
        _finished.Clear();
        foreach (long id in _shots.Keys)
            if (!_live.Contains(id))
                _finished.Add(id);
        foreach (long id in _finished)
        {
            Release(_shots[id]);
            _shots.Remove(id);
        }
        for (int index = _flashes.Count - 1; index >= 0; index--)
        {
            Flash flash = _flashes[index];
            flash.Age += deltaTime;
            if (flash.Age >= flash.Lifetime)
            {
                Release(flash.Line);
                _flashes.RemoveAt(index);
                continue;
            }
            float fraction = flash.Age / flash.Lifetime;
            Color color = new Color(1f, 1f, 1f, 1f - fraction * fraction);
            flash.Line.startColor = color;
            flash.Line.endColor = color;
        }
    }

    /// <summary>
    /// Presents a physical projectile or a transient resolved beam through the same renderer pool.
    /// </summary>
    /// <param name="shot">The shot supplying its pose and frozen appearance.</param>
    private void RenderShot(BattleProjectile shot)
    {
        _live.Add(shot.ID);
        if (!_shots.TryGetValue(shot.ID, out LineRenderer line))
        {
            line = Acquire(shot.Projectile);
            _shots.Add(shot.ID, line);
        }
        Vector3 head = ToVector(shot.Position);
        Vector3 direction = ToVector(shot.Direction);
        float unitsPerPixel = GetWorldUnitsPerPixel(_space.TransformPoint(head));
        float width = Mathf.Max(shot.Projectile.Width, unitsPerPixel * 2.5f) * 4.2f;
        line.startWidth = width;
        line.endWidth = width;
        if (shot.Projectile.DeliveryMode == WeaponDeliveryMode.Hitscan)
        {
            line.SetPosition(0, head);
            line.SetPosition(1, head + direction * shot.DistanceTraveled);
        }
        else
        {
            float length = Mathf.Min(
                shot.DistanceTraveled,
                Mathf.Max(shot.Projectile.Length, unitsPerPixel * 18f)
            );
            line.SetPosition(0, head - direction * length);
            line.SetPosition(1, head);
        }
    }

    /// <summary>
    /// Creates a pooled muzzle flash and requests a data-selected firing cue.
    /// </summary>
    /// <param name="shot">The emitted shot.</param>
    /// <param name="weapon">The firing weapon supplying its launch audio.</param>
    private void OnShotFired(BattleProjectile shot, WeaponData weapon)
    {
        if (shot.Projectile.DeliveryMode == WeaponDeliveryMode.Hitscan)
            _beams.Add(
                new Beam { Shot = shot, RemainingSeconds = shot.Projectile.EffectDurationSeconds }
            );
        CreateFlash(
            shot,
            ToVector(shot.Position),
            0.14f,
            Mathf.Max(0.34f, shot.Projectile.Width * 4f)
        );
        Play(
            _fireSources,
            weapon.FireSoundPaths,
            ToVector(shot.Position),
            ref _nextFireSound,
            0.12f
        );
    }

    /// <summary>
    /// Presents a contact at the simulation's actual collision point, not its requested target center.
    /// </summary>
    /// <param name="shot">The contacting shot.</param>
    /// <param name="target">The contacted battle hull.</param>
    /// <param name="absorbedByShield">Whether the contact reached an active shield.</param>
    private void OnShotHit(BattleProjectile shot, CombatUnit target, bool absorbedByShield)
    {
        Vector3 position = ToVector(shot.Position);
        if (shot.Projectile.DeliveryMode == WeaponDeliveryMode.Hitscan)
            position += ToVector(shot.Direction) * shot.DistanceTraveled;
        if (absorbedByShield && _shields.TryGetValue(target, out ITacticalShieldEffect shield))
        {
            Vector3 worldPosition = _space.TransformPoint(position);
            Vector3 normal = -_space.TransformDirection(ToVector(shot.Direction)).normalized;
            float unitsPerPixel = GetWorldUnitsPerPixel(worldPosition);
            float projectileVisualWidth =
                Mathf.Max(shot.Projectile.Width, unitsPerPixel * 2.5f) * 4.2f;
            shield.ShowHit(worldPosition, normal, projectileVisualWidth * 1.5f, 1f);
        }
        CreateFlash(shot, position, 0.18f, 0.8f);
        Play(
            _impactSources,
            shot.Projectile.ImpactSoundPaths,
            position,
            ref _nextImpactSound,
            0.16f
        );
    }

    /// <summary>
    /// Creates one hull-following authored shield effect for each shield-capable combatant.
    /// </summary>
    /// <param name="presentations">The loaded combatant presentation index.</param>
    /// <param name="template">The inactive authored shield template.</param>
    private void CreateShields(
        IReadOnlyDictionary<CombatUnit, Transform> presentations,
        GameObject template
    )
    {
        foreach (KeyValuePair<CombatUnit, Transform> presentation in presentations)
        {
            CombatUnit unit = presentation.Key;
            if (TacticalCombat.GetMaximumShields(unit) <= 0)
                continue;
            GameObject instance = UnityEngine.Object.Instantiate(
                template,
                presentation.Value,
                false
            );
            instance.name = "Shield";
            instance.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
            ModelDimensions size = unit.Data.ModelSize;
            instance.transform.localScale =
                new Vector3(size.Width, size.Height, size.Depth) * _shieldEnvelopeScale;
            ITacticalShieldEffect effect = GetShieldEffect(instance);
            effect.Configure(_camera);
            instance.SetActive(true);
            effect.SetActive(HasShieldStrength(unit));
            _shields.Add(unit, effect);
            _shieldObjects.Add(instance);
        }
    }

    /// <summary>
    /// Resolves the installed shield adapter without coupling combat presentation to its provider.
    /// </summary>
    /// <param name="instance">The instantiated shield hierarchy.</param>
    /// <returns>The shield presentation adapter.</returns>
    private static ITacticalShieldEffect GetShieldEffect(GameObject instance)
    {
        foreach (MonoBehaviour component in instance.GetComponentsInChildren<MonoBehaviour>(true))
            if (component is ITacticalShieldEffect effect)
                return effect;
        DestroyOwned(instance);
        throw new MissingComponentException(
            "The tactical shield template is missing its effect adapter."
        );
    }

    /// <summary>
    /// Reports whether a shield-capable battle member currently has usable shield strength.
    /// </summary>
    /// <param name="unit">The battle member.</param>
    /// <returns>Whether its current or initial shield strength is positive.</returns>
    private static bool HasShieldStrength(CombatUnit unit) =>
        (unit.Condition.CurrentShieldStrength ?? TacticalCombat.GetMaximumShields(unit)) > 0;

    /// <summary>
    /// Converts one screen pixel to world units at a presented position.
    /// </summary>
    /// <param name="worldPosition">The world-space position being presented.</param>
    /// <returns>The world-space height of one screen pixel.</returns>
    private float GetWorldUnitsPerPixel(Vector3 worldPosition)
    {
        float distance = Vector3.Distance(_camera.transform.position, worldPosition);
        return 2f
            * distance
            * Mathf.Tan(_camera.fieldOfView * Mathf.Deg2Rad * 0.5f)
            / Mathf.Max(1, _camera.pixelHeight);
    }

    /// <summary>
    /// Acquires a camera-facing short flash from the shared line pool.
    /// </summary>
    /// <param name="shot">The shot supplying authored color.</param>
    /// <param name="position">The flash center.</param>
    /// <param name="lifetime">The visual lifetime in battle seconds.</param>
    /// <param name="diameter">The visual diameter.</param>
    private void CreateFlash(
        BattleProjectile shot,
        Vector3 position,
        float lifetime,
        float diameter
    )
    {
        LineRenderer line = Acquire(shot.Projectile);
        Vector3 right = _space.InverseTransformDirection(_camera.transform.right) * diameter * 0.5f;
        line.SetPosition(0, position - right);
        line.SetPosition(1, position + right);
        line.startWidth = diameter;
        line.endWidth = diameter;
        _flashes.Add(new Flash { Line = line, Lifetime = lifetime });
    }

    /// <summary>
    /// Reuses a disabled renderer or creates one when peak simultaneous effects grow.
    /// </summary>
    /// <param name="projectile">The authored effect appearance.</param>
    /// <returns>A scene-owned renderer with a shared color material.</returns>
    private LineRenderer Acquire(ProjectileData projectile)
    {
        LineRenderer line;
        if (_pool.Count > 0)
            line = _pool.Pop();
        else
        {
            GameObject effect = new GameObject("Weapon Effect");
            effect.transform.SetParent(_root.transform, false);
            line = effect.AddComponent<LineRenderer>();
            line.positionCount = 2;
            line.useWorldSpace = false;
            line.numCapVertices = 5;
            line.alignment = LineAlignment.View;
            line.shadowCastingMode = ShadowCastingMode.Off;
            line.receiveShadows = false;
        }
        Color color = ParseProjectileColor(projectile.ColorHex);
        if (!_materials.TryGetValue(color, out Material material))
        {
            material = new Material(_shader);
            material.SetColor("_Color", color);
            _materials.Add(color, material);
        }
        line.sharedMaterial = material;
        line.startColor = Color.white;
        line.endColor = Color.white;
        line.gameObject.SetActive(true);
        return line;
    }

    /// <summary>
    /// Converts one authored projectile color into its Unity presentation value.
    /// </summary>
    /// <param name="colorHex">The HTML hexadecimal color.</param>
    /// <returns>The configured projectile color.</returns>
    private static Color ParseProjectileColor(string colorHex)
    {
        if (!ColorUtility.TryParseHtmlString(colorHex, out Color color))
            throw new ArgumentException(
                $"Invalid projectile color '{colorHex}'.",
                nameof(colorHex)
            );
        return color;
    }

    /// <summary>
    /// Returns a renderer to the scene-local pool.
    /// </summary>
    /// <param name="line">The completed effect.</param>
    private void Release(LineRenderer line)
    {
        line.gameObject.SetActive(false);
        _pool.Push(line);
    }

    /// <summary>
    /// Creates bounded spatial voices for one effect category.
    /// </summary>
    /// <param name="sources">The fixed voice slots.</param>
    /// <param name="category">The scene object label.</param>
    /// <param name="priority">The Unity audio priority.</param>
    private void CreateAudio(AudioSource[] sources, string category, int priority)
    {
        for (int index = 0; index < sources.Length; index++)
        {
            GameObject voice = new GameObject(category);
            voice.transform.SetParent(_root.transform, false);
            AudioSource source = voice.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.spatialBlend = 0.4f;
            source.dopplerLevel = 0f;
            source.rolloffMode = AudioRolloffMode.Logarithmic;
            source.minDistance = 24f;
            source.maxDistance = 260f;
            source.priority = priority;
            sources[index] = source;
        }
    }

    /// <summary>
    /// Honors application volume and tactical pause without allocating additional voices.
    /// </summary>
    /// <param name="source">The owned audio voice.</param>
    /// <param name="paused">Whether battle playback is paused.</param>
    private void UpdateAudio(AudioSource source, bool paused)
    {
        source.volume = 0.1f * _audio.MasterVolume * _audio.SfxVolume;
        if (paused)
            source.Pause();
        else
            source.UnPause();
    }

    /// <summary>
    /// Plays a preloaded authored cue only when the category throttle and voice budget permit it.
    /// </summary>
    /// <param name="sources">The category's fixed voice budget.</param>
    /// <param name="paths">Authored eligible cue addresses.</param>
    /// <param name="position">The event position.</param>
    /// <param name="nextAllowed">The category's next permitted playback time.</param>
    /// <param name="interval">The category throttle interval.</param>
    private void Play(
        AudioSource[] sources,
        List<string> paths,
        Vector3 position,
        ref float nextAllowed,
        float interval
    )
    {
        if (paths.Count == 0 || _time < nextAllowed)
            return;
        foreach (AudioSource source in sources)
        {
            if (source.isPlaying)
                continue;
            source.transform.localPosition = position;
            source.clip = _clips[paths[UnityEngine.Random.Range(0, paths.Count)]];
            source.pitch = UnityEngine.Random.Range(0.94f, 1.06f);
            source.volume = 0.1f * _audio.MasterVolume * _audio.SfxVolume;
            source.Play();
            nextAllowed = _time + interval;
            return;
        }
    }

    /// <summary>
    /// Unsubscribes and releases owned renderers, voices, and materials but not cached clips.
    /// </summary>
    public void Dispose()
    {
        _combat.ShotFired -= OnShotFired;
        _combat.ShotHit -= OnShotHit;
        if (!_root)
            return;
        foreach (AudioSource source in _fireSources)
            source.Stop();
        foreach (AudioSource source in _impactSources)
            source.Stop();
        foreach (Material material in _materials.Values)
            DestroyOwned(material);
        foreach (GameObject shield in _shieldObjects)
            if (shield)
                DestroyOwned(shield);
        _beams.Clear();
        _shieldObjects.Clear();
        _shields.Clear();
        DestroyOwned(_root);
        _root = null;
    }

    /// <summary>
    /// Releases one presenter-owned Unity object in the correct editor or play context.
    /// </summary>
    /// <param name="value">The owned object.</param>
    private static void DestroyOwned(UnityEngine.Object value)
    {
        if (Application.isPlaying)
            UnityEngine.Object.Destroy(value);
        else
            UnityEngine.Object.DestroyImmediate(value);
    }

    /// <summary>
    /// Converts a battle coordinate into its Unity presentation value.
    /// </summary>
    /// <param name="value">The simulation coordinate.</param>
    /// <returns>The corresponding Unity vector.</returns>
    private static Vector3 ToVector(BattleVector3 value) => new Vector3(value.X, value.Y, value.Z);

    private sealed class Beam
    {
        public BattleProjectile Shot;
        public float RemainingSeconds;
    }

    private sealed class Flash
    {
        public LineRenderer Line;
        public float Lifetime;
        public float Age;
    }
}
