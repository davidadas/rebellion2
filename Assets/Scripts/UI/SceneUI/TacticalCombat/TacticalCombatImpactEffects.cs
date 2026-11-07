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
    public TacticalCombatImpactEffects(
        Transform parent,
        Camera camera,
        Shader shader,
        IReadOnlyList<TacticalCombatEffectDefinition> definitions,
        ContentAssets assets
    )
    {
        _parent = parent;
        _camera = camera;
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
            pair.Value.Advance(seconds, pair.Key, _parent, _camera);
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
            GetVisual(unit).Show(animation, priority);
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
        private Animation _impact;
        private int _priority;
        private int _frame;
        private float _elapsed;
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
        public void Show(Animation animation, int priority)
        {
            if (
                priority <= _priority
                || (priority & 0x70) != 0 && (_priority & 0x70) != 0
                || (priority & 12) != 0 && (_priority & 12) != 0
            )
                return;
            _impact = animation;
            _priority = priority;
            _frame = 0;
            _elapsed = 0;
            _impactMesh.sharedMesh = animation.Mesh;
            _impactRenderer.sharedMaterial = animation.Frames[0];
            _impactRenderer.enabled = true;
        }

        /// <summary>
        /// Advances the frame-driven impact sequence with its independent presentation timer.
        /// </summary>
        /// <param name="seconds">The unscaled frame interval.</param>
        /// <param name="unit">The moving effect owner.</param>
        /// <param name="parent">The encounter's coordinate frame.</param>
        /// <param name="camera">The battle camera.</param>
        public void Advance(float seconds, TacticalCombatUnit unit, Transform parent, Camera camera)
        {
            Vector3 position = new(unit.Position.X, unit.Position.Y, unit.Position.Z);
            _root.transform.localPosition = position;
            _root.transform.rotation = camera.transform.rotation;
            Vector3 towardCamera = (
                parent.InverseTransformPoint(camera.transform.position) - position
            ).normalized;
            _impactMesh.transform.position = parent.TransformPoint(position + towardCamera);
            if (_impact != null)
            {
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
