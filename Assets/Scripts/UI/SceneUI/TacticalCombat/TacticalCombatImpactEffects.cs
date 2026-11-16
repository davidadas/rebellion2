using System;
using System.Collections.Generic;
using Rebellion.Game.SpaceCombat;
using Rebellion.Gameplay;
using UnityEngine;

/// <summary>
/// Owns authored impact sequences independently of combat rules.
/// </summary>
internal sealed class TacticalCombatImpactEffects : IDisposable
{
    private readonly Transform _parent;
    private readonly Camera _camera;
    private readonly Func<TacticalCombatUnit, Vector3> _getPosition;
    private readonly Func<int?> _getCloseUpPrimary;
    private readonly Dictionary<TacticalCombatEffectKind, Animation> _animations = new();
    private readonly Dictionary<TacticalCombatUnit, Visual> _visuals = new();

    /// <summary>
    /// Prepares shared animation resources before the encounter starts advancing.
    /// </summary>
    /// <param name="parent">The isolated encounter hierarchy.</param>
    /// <param name="camera">The camera used to face animation billboards.</param>
    /// <param name="shader">The tactical unlit shader.</param>
    /// <param name="definitions">The authored frame sequences and image anchors.</param>
    /// <param name="assets">The selected pack's media source.</param>
    /// <param name="getPosition">Resolves a participant's current encounter-local rendering position.</param>
    /// <param name="getCloseUpPrimary">Resolves the current category close-up's primary participant, or null.</param>
    public TacticalCombatImpactEffects(
        Transform parent,
        Camera camera,
        Shader shader,
        IReadOnlyList<TacticalCombatEffectDefinition> definitions,
        ContentAssets assets,
        Func<TacticalCombatUnit, Vector3> getPosition,
        Func<int?> getCloseUpPrimary
    )
    {
        _parent = parent;
        _camera = camera;
        _getPosition = getPosition ?? throw new ArgumentNullException(nameof(getPosition));
        _getCloseUpPrimary =
            getCloseUpPrimary ?? throw new ArgumentNullException(nameof(getCloseUpPrimary));
        try
        {
            foreach (TacticalCombatEffectDefinition definition in definitions)
            {
                if (_animations.ContainsKey(definition.Kind))
                    throw new ArgumentException(
                        "Tactical effect categories must be unique.",
                        nameof(definitions)
                    );
                _animations.Add(definition.Kind, new Animation(definition, shader, assets));
            }
        }
        catch
        {
            Dispose();
            throw;
        }
    }

    /// <summary>
    /// Requests the effect associated with the weapon and the surface reached by its applied impact.
    /// </summary>
    /// <param name="unit">The impacted encounter participant.</param>
    /// <param name="weapon">The impacting weapon.</param>
    /// <param name="impact">The surface reached by the simulation's damage calculation.</param>
    public void ShowImpact(
        TacticalCombatUnit unit,
        TacticalWeaponType weapon,
        TacticalCombatImpactKind impact
    )
    {
        if (unit.IsFighter || unit.IsBattleStation || impact == TacticalCombatImpactKind.None)
            return;
        (TacticalCombatEffectKind Kind, int Priority) effect = impact switch
        {
            TacticalCombatImpactKind.Destroyed => (TacticalCombatEffectKind.Destruction, 128),
            TacticalCombatImpactKind.Shield => weapon == TacticalWeaponType.IonCannon
                ? (TacticalCombatEffectKind.IonShieldHit, 4)
                : (TacticalCombatEffectKind.ShieldHit, 8),
            _ => weapon switch
            {
                TacticalWeaponType.IonCannon => (TacticalCombatEffectKind.IonHullHit, 16),
                TacticalWeaponType.Torpedo => (TacticalCombatEffectKind.TorpedoHullHit, 64),
                _ => (TacticalCombatEffectKind.HullHit, 32),
            },
        };
        Show(unit, effect.Kind, effect.Priority);
    }

    /// <summary>
    /// Requests a capital-ship destruction sequence for scuttling and direct destruction commands.
    /// </summary>
    /// <param name="unit">The destroyed encounter participant.</param>
    public void ShowDestruction(TacticalCombatUnit unit)
    {
        if (!unit.IsFighter && !unit.IsBattleStation)
            Show(unit, TacticalCombatEffectKind.Destruction, 128);
    }

    /// <summary>
    /// Applies a tractor or gravity lock transition without restarting an unchanged selector.
    /// </summary>
    /// <param name="unit">The capital ship receiving the visual transition.</param>
    /// <param name="kind">The tractor-restraint or gravity-lock animation.</param>
    /// <param name="enabled">Whether the addressed lock selector is enabled.</param>
    public void SetLock(TacticalCombatUnit unit, TacticalCombatEffectKind kind, bool enabled)
    {
        if (
            kind
            is not TacticalCombatEffectKind.TractorRestraint
                and not TacticalCombatEffectKind.GravityLock
        )
            throw new ArgumentOutOfRangeException(nameof(kind));
        if (unit.IsFighter || unit.IsBattleStation || !_animations.ContainsKey(kind))
            return;
        if (enabled)
            GetVisual(unit).SetLock(kind, true, _animations);
        else if (_visuals.TryGetValue(unit, out Visual visual))
            visual.SetLock(kind, false, _animations);
    }

    /// <summary>
    /// Detaches both lock selectors when a capital ship's presentation is reset.
    /// </summary>
    /// <param name="unit">The capital ship whose lock visuals are released.</param>
    public void ClearLocks(TacticalCombatUnit unit)
    {
        if (_visuals.TryGetValue(unit, out Visual visual))
        {
            visual.SetLock(TacticalCombatEffectKind.TractorRestraint, false, _animations);
            visual.SetLock(TacticalCombatEffectKind.GravityLock, false, _animations);
        }
    }

    /// <summary>
    /// Reattaches enabled lock selectors after a capital model changes detail level.
    /// </summary>
    /// <param name="unit">The capital ship whose model detail changed.</param>
    public void RestartLocks(TacticalCombatUnit unit)
    {
        if (_visuals.TryGetValue(unit, out Visual visual))
            visual.RestartLocks(_animations);
    }

    /// <summary>
    /// Keeps a lock billboard behind the ship's live presentation pose during temporary movement.
    /// </summary>
    /// <param name="unit">The capital ship owning the lock billboard.</param>
    /// <param name="position">Its current position in the encounter's rendering frame.</param>
    public void SetLockPosition(TacticalCombatUnit unit, Vector3 position)
    {
        if (_visuals.TryGetValue(unit, out Visual visual))
            visual.SetLockPosition(position, _parent, _camera);
    }

    /// <summary>
    /// Checks whether a destruction sequence is still showing its underlying capital model.
    /// </summary>
    /// <param name="unit">The participant whose model visibility is being projected.</param>
    /// <returns>Whether the destruction sequence has not yet detached its model.</returns>
    public bool KeepsModelVisible(TacticalCombatUnit unit) =>
        _visuals.TryGetValue(unit, out Visual visual) && visual.KeepsModelVisible;

    /// <summary>
    /// Advances the active presentation sequences and faces their images toward the battle camera.
    /// </summary>
    /// <param name="seconds">The unscaled presentation interval.</param>
    public void Advance(float seconds)
    {
        foreach (KeyValuePair<TacticalCombatUnit, Visual> pair in _visuals)
            pair.Value.Advance(
                seconds,
                _getPosition(pair.Key),
                _getCloseUpPrimary() == pair.Key.Index,
                _parent,
                _camera,
                _animations
            );
    }

    /// <summary>
    /// Applies effect precedence without restarting another impact in the same effect family.
    /// </summary>
    /// <param name="unit">The affected capital ship.</param>
    /// <param name="kind">The authored animation category.</param>
    /// <param name="priority">The effect's ordering and family bits.</param>
    private void Show(TacticalCombatUnit unit, TacticalCombatEffectKind kind, int priority)
    {
        if (_animations.TryGetValue(kind, out Animation animation))
        {
            Visual visual = GetVisual(unit);
            if (visual.Show(animation, priority) && _getCloseUpPrimary() == unit.Index)
                visual.SuppressInitialImpact(_animations);
        }
    }

    /// <summary>
    /// Creates at most one reusable impact owner per affected capital ship.
    /// </summary>
    /// <param name="unit">The affected participant.</param>
    /// <returns>The unit's reusable effect owner.</returns>
    private Visual GetVisual(TacticalCombatUnit unit)
    {
        if (!_visuals.TryGetValue(unit, out Visual visual))
        {
            visual = new Visual(_parent);
            _visuals.Add(unit, visual);
        }
        return visual;
    }

    /// <summary>
    /// Releases owned render objects, materials, and image-anchored meshes.
    /// </summary>
    public void Dispose()
    {
        foreach (Visual visual in _visuals.Values)
            visual.Dispose();
        foreach (Animation animation in _animations.Values)
            animation.Dispose();
        _visuals.Clear();
        _animations.Clear();
    }

    private sealed class Animation : IDisposable
    {
        public Mesh Mesh { get; }
        public Material[] Frames { get; }

        /// <summary>
        /// Captures one shared animation's frames and image-anchored quad.
        /// </summary>
        /// <param name="definition">The authored frames and drawing geometry.</param>
        /// <param name="shader">The tactical unlit shader.</param>
        /// <param name="assets">The addressed texture source.</param>
        public Animation(
            TacticalCombatEffectDefinition definition,
            Shader shader,
            ContentAssets assets
        )
        {
            float x = definition.OriginX;
            float y = definition.OriginY;
            float size = definition.Size;
            Mesh = new Mesh
            {
                name = "Tactical impact",
                vertices = new[]
                {
                    new Vector3(-x, -y, 0) * size,
                    new Vector3(1 - x, -y, 0) * size,
                    new Vector3(-x, 1 - y, 0) * size,
                    new Vector3(1 - x, 1 - y, 0) * size,
                },
                uv = new[] { Vector2.zero, Vector2.right, Vector2.up, Vector2.one },
                triangles = new[] { 0, 2, 1, 2, 3, 1 },
            };
            Mesh.RecalculateBounds();
            Frames = new Material[definition.FramePaths.Count];
            try
            {
                for (int index = 0; index < Frames.Length; index++)
                {
                    Texture texture =
                        assets.GetTexture(definition.FramePaths[index])
                        ?? throw new InvalidOperationException(
                            $"Tactical effect frame is missing: {definition.FramePaths[index]}"
                        );
                    Frames[index] = new Material(shader) { mainTexture = texture };
                }
            }
            catch
            {
                Dispose();
                throw;
            }
        }

        /// <summary>
        /// Releases this sequence's shared materials and mesh.
        /// </summary>
        public void Dispose()
        {
            UnityEngine.Object.Destroy(Mesh);
            foreach (Material frame in Frames)
                UnityEngine.Object.Destroy(frame);
        }
    }

    private sealed class Visual : IDisposable
    {
        private readonly GameObject _root;
        private readonly MeshFilter _impactMesh;
        private readonly MeshRenderer _impactRenderer;
        private readonly MeshFilter _lockMesh;
        private readonly MeshRenderer _lockRenderer;
        private Animation _impact;
        private Animation _lock;
        private int _priority;
        private int _frame;
        private float _elapsed;
        private int _lockFlags;
        private bool _lockRegistered;
        private int _lockFrame;
        private float _lockElapsed;
        public bool KeepsModelVisible => _priority == 128 && _frame < 12;

        /// <summary>
        /// Creates one reusable impact billboard.
        /// </summary>
        /// <param name="parent">The isolated battle hierarchy.</param>
        public Visual(Transform parent)
        {
            _root = new GameObject("Impact effects");
            _root.transform.SetParent(parent, false);
            (_impactMesh, _impactRenderer) = CreateBillboard("Impact", _root.transform);
            (_lockMesh, _lockRenderer) = CreateBillboard("Lock", _root.transform);
        }

        /// <summary>
        /// Retains selector ownership while attaching or releasing its shared animation.
        /// </summary>
        /// <param name="kind">The addressed lock animation.</param>
        /// <param name="enabled">Whether that selector is enabled.</param>
        /// <param name="animations">The encounter's authored animation resources.</param>
        public void SetLock(
            TacticalCombatEffectKind kind,
            bool enabled,
            IReadOnlyDictionary<TacticalCombatEffectKind, Animation> animations
        )
        {
            int selector = kind == TacticalCombatEffectKind.TractorRestraint ? 1 : 2;
            if (enabled && selector == 2 && (_lockFlags & 1) != 0)
                SetLock(TacticalCombatEffectKind.TractorRestraint, false, animations);
            if (!enabled)
            {
                _lockFlags &= ~selector;
                _lock = null;
                _lockRenderer.enabled = false;
                if (
                    selector == 1 && (_lockFlags & 2) == 0
                    || selector == 2 && (_lockFlags & 1) == 0
                )
                    _lockRegistered = false;
            }
            else
            {
                if (selector == (_lockFlags & 3))
                    return;
                if (!animations.TryGetValue(kind, out Animation animation))
                    return;
                if ((_lockFlags & 3) == 0)
                    _lockRegistered = true;
                _lockFlags |= selector;
                _lockFrame = 0;
                _lockElapsed = 0;
                _lock = animation;
                _lockMesh.sharedMesh = animation.Mesh;
                _lockRenderer.sharedMaterial = animation.Frames[0];
                _lockRenderer.enabled = true;
            }
            if (!enabled && selector == 2 && (_lockFlags & 1) != 0)
                SetLock(TacticalCombatEffectKind.TractorRestraint, true, animations);
        }

        /// <summary>
        /// Preserves selector reattachment order across a model detail transition.
        /// </summary>
        /// <param name="animations">The encounter's authored animation resources.</param>
        public void RestartLocks(
            IReadOnlyDictionary<TacticalCombatEffectKind, Animation> animations
        )
        {
            bool gravity = (_lockFlags & 2) != 0;
            if (gravity)
                SetLock(TacticalCombatEffectKind.GravityLock, false, animations);
            bool tractor = (_lockFlags & 1) != 0;
            if (tractor)
                SetLock(TacticalCombatEffectKind.TractorRestraint, false, animations);
            if (gravity)
                SetLock(TacticalCombatEffectKind.GravityLock, true, animations);
            if (tractor)
                SetLock(TacticalCombatEffectKind.TractorRestraint, true, animations);
        }

        /// <summary>
        /// Positions the active loop behind the presented capital relative to the camera.
        /// </summary>
        /// <param name="position">The capital's live encounter-local rendering position.</param>
        /// <param name="parent">The encounter's world-coordinate frame.</param>
        /// <param name="camera">The active battle camera.</param>
        public void SetLockPosition(Vector3 position, Transform parent, Camera camera)
        {
            if (!_lockRenderer.enabled)
                return;
            Vector3 towardCamera = (
                parent.InverseTransformPoint(camera.transform.position) - position
            ).normalized;
            _lockMesh.transform.position = parent.TransformPoint(position - towardCamera);
        }

        /// <summary>
        /// Creates a render-only quad owner without adding a physics collider.
        /// </summary>
        /// <param name="name">The renderer's authored role.</param>
        /// <param name="parent">The effect owner.</param>
        /// <returns>The mesh and renderer pair.</returns>
        private static (MeshFilter, MeshRenderer) CreateBillboard(string name, Transform parent)
        {
            GameObject root = new(name, typeof(MeshFilter), typeof(MeshRenderer));
            root.layer = PresentationLayers.TacticalCombat;
            root.transform.SetParent(parent, false);
            MeshRenderer renderer = root.GetComponent<MeshRenderer>();
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.enabled = false;
            return (root.GetComponent<MeshFilter>(), renderer);
        }

        /// <summary>
        /// Applies one impact when its ordering and effect-family rules allow replacement.
        /// </summary>
        /// <param name="animation">The shared authored sequence.</param>
        /// <param name="priority">The effect's precedence and family bits.</param>
        /// <returns>Whether the requested effect replaced the current sequence.</returns>
        public bool Show(Animation animation, int priority)
        {
            if (
                priority <= _priority
                || (priority & 0x70) != 0 && (_priority & 0x70) != 0
                || (priority & 12) != 0 && (_priority & 12) != 0
            )
                return false;
            _impact = animation;
            _priority = priority;
            _frame = 0;
            _elapsed = 0;
            _impactMesh.sharedMesh = animation.Mesh;
            _impactRenderer.sharedMaterial = animation.Frames[0];
            _impactRenderer.enabled = true;
            return true;
        }

        /// <summary>
        /// Leaves a newly admitted close-up impact unattached while clearing both lock selectors.
        /// </summary>
        /// <param name="animations">The encounter's authored impact and lock animations.</param>
        public void SuppressInitialImpact(
            IReadOnlyDictionary<TacticalCombatEffectKind, Animation> animations
        )
        {
            _impactRenderer.enabled = false;
            SetLock(TacticalCombatEffectKind.TractorRestraint, false, animations);
            SetLock(TacticalCombatEffectKind.GravityLock, false, animations);
        }

        /// <summary>
        /// Advances the frame-driven impact sequence with its independent presentation timer.
        /// </summary>
        /// <param name="seconds">The unscaled frame interval.</param>
        /// <param name="position">The moving effect owner's live encounter-local rendering position.</param>
        /// <param name="isCloseUpPrimary">Whether the category close-up currently owns this participant.</param>
        /// <param name="parent">The encounter's coordinate frame.</param>
        /// <param name="camera">The battle camera.</param>
        /// <param name="animations">The encounter's authored impact and lock animations.</param>
        public void Advance(
            float seconds,
            Vector3 position,
            bool isCloseUpPrimary,
            Transform parent,
            Camera camera,
            IReadOnlyDictionary<TacticalCombatEffectKind, Animation> animations
        )
        {
            _root.transform.localPosition = position;
            _root.transform.rotation = camera.transform.rotation;
            Vector3 towardCamera = (
                parent.InverseTransformPoint(camera.transform.position) - position
            ).normalized;
            _impactMesh.transform.position = parent.TransformPoint(position + towardCamera);
            _lockMesh.transform.position = parent.TransformPoint(position - towardCamera);
            if (_lockRegistered)
            {
                _lockElapsed += seconds;
                if (_lockElapsed > .1f)
                {
                    if (_lock == null)
                    {
                        SetLock(TacticalCombatEffectKind.TractorRestraint, false, animations);
                        SetLock(TacticalCombatEffectKind.GravityLock, false, animations);
                    }
                    else
                    {
                        _lockRenderer.sharedMaterial = _lock.Frames[_lockFrame];
                        _lockFrame = (_lockFrame + 1) & 7;
                    }
                    _lockElapsed = 0;
                }
            }
            if (_impact != null)
            {
                if (_impactRenderer.enabled && isCloseUpPrimary)
                {
                    _priority = 0;
                    _impact = null;
                    _impactRenderer.enabled = false;
                    _frame = 0;
                    _lockFlags = 0;
                    return;
                }
                _elapsed += seconds;
                if (_frame >= _impact.Frames.Length)
                {
                    _priority = 0;
                    _impact = null;
                    _impactRenderer.enabled = false;
                }
                else
                {
                    if (_frame == 0 || _elapsed > .1f)
                    {
                        _impactRenderer.sharedMaterial = _impact.Frames[_frame];
                        _elapsed = 0;
                    }
                    if (_priority == 128 && _frame == 11)
                    {
                        SetLock(TacticalCombatEffectKind.TractorRestraint, false, animations);
                        SetLock(TacticalCombatEffectKind.GravityLock, false, animations);
                    }
                    _frame++;
                }
            }
        }

        /// <summary>
        /// Releases this unit's reusable render hierarchy.
        /// </summary>
        public void Dispose() => UnityEngine.Object.Destroy(_root);
    }
}
