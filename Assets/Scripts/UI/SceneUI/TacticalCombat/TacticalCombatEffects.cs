using System;
using System.Collections.Generic;
using Rebellion.Gameplay;
using UnityEngine;
using Vector3N = System.Numerics.Vector3;

/// <summary>
/// Owns pooled weapon effects and their independent presentation lifetimes.
/// </summary>
internal sealed class TacticalCombatEffects : IDisposable
{
    private readonly Transform _parent;
    private readonly Mesh[] _meshes;
    private readonly Material[] _materials;
    private readonly List<Beam> _active = new();
    private readonly Stack<Beam> _available = new();

    /// <summary>
    /// Creates the shared wireframe geometry and faction-colored beam materials.
    /// </summary>
    /// <param name="parent">The encounter's isolated rendering hierarchy.</param>
    /// <param name="shader">The unlit tactical effect shader.</param>
    /// <param name="battleDepth">The deployment span used by the station emitter geometry.</param>
    public TacticalCombatEffects(Transform parent, Shader shader, float battleDepth)
    {
        _parent = parent;
        _meshes = new[]
        {
            CreateMesh(new[] { 0, 1, 2, 2, 1, 0 }),
            CreateMesh(new[] { 0, 3, 2, 2, 3, 0, 4, 1, 5, 5, 1, 4 }),
            CreateMesh(new[] { 6, 7, 8, 8, 7, 6, 9, 10, 11, 11, 10, 9 }),
            CreateSuperlaserLine(),
            CreateSuperlaserEmitter(battleDepth),
        };
        Color[] colors = { Color.red, Color.green, Color.blue, Color.white };
        _materials = new Material[colors.Length];
        for (int index = 0; index < colors.Length; index++)
            _materials[index] = new Material(shader)
            {
                mainTexture = Texture2D.whiteTexture,
                color = colors[index],
            };
    }

    /// <summary>
    /// Displays one applied weapon discharge without altering the simulation's impact schedule.
    /// </summary>
    /// <param name="shot">The applied discharge.</param>
    /// <param name="factionIndex">The firing faction's authored color index.</param>
    public void ShowShot(TacticalCombatShot shot, int factionIndex)
    {
        int variant = 0;
        float length;
        switch (shot.Weapon)
        {
            case TacticalWeaponType.IonCannon:
                length = shot.Strength >= 32 ? 1 : 0.4f;
                variant = shot.Strength >= 32 ? 2 : 0;
                break;
            case TacticalWeaponType.Torpedo:
                length = 0.2f;
                variant = shot.Strength >= 12.8f ? 2 : 0;
                break;
            case TacticalWeaponType.Turbolaser:
                if (shot.Strength >= 34.666668f)
                {
                    bool split = shot.Target.Definition?.SplitTurbolaserBeams == true;
                    length = split ? 0.75f : 1;
                    variant = split ? 1 : 2;
                }
                else
                    length = 0.65f;
                break;
            default:
                length = 0.5f;
                variant = shot.Strength >= 28.8f ? 1 : 0;
                break;
        }
        int color = shot.Weapon switch
        {
            TacticalWeaponType.IonCannon => 2,
            TacticalWeaponType.Torpedo => 3,
            _ => factionIndex == 1 ? 1 : 0,
        };
        Beam beam = Acquire();
        beam.Configure(
            ToUnity(shot.Source.Position),
            shot.Target,
            _meshes[variant],
            _materials[color],
            length,
            shot.Weapon == TacticalWeaponType.Torpedo ? 2 : 1,
            false,
            null
        );
    }

    /// <summary>
    /// Displays the station's growing superlaser from its rendered emitter position.
    /// </summary>
    /// <param name="origin">The emitter in encounter-local coordinates.</param>
    /// <param name="target">The selected enemy capital ship.</param>
    public void ShowSuperlaser(Vector3 origin, TacticalCombatUnit target)
    {
        Beam beam = Acquire();
        beam.Configure(origin, target, _meshes[3], _materials[1], 1, 1, true, _meshes[4]);
    }

    /// <summary>
    /// Advances effects using their presentation clock and recycles completed objects.
    /// </summary>
    /// <param name="seconds">The unscaled frame interval.</param>
    public void Advance(float seconds)
    {
        for (int index = _active.Count - 1; index >= 0; index--)
        {
            Beam beam = _active[index];
            if (beam.Advance(seconds))
                continue;
            beam.Root.SetActive(false);
            _active.RemoveAt(index);
            _available.Push(beam);
        }
    }

    /// <summary>
    /// Reuses an inactive effect or creates one additional renderer-owned object.
    /// </summary>
    /// <returns>The newly active beam.</returns>
    private Beam Acquire()
    {
        Beam beam = _available.Count > 0 ? _available.Pop() : new Beam(_parent);
        beam.Root.SetActive(true);
        _active.Add(beam);
        return beam;
    }

    /// <summary>
    /// Creates the selected wireframe face stream from the shared beam vertices.
    /// </summary>
    /// <param name="faces">The triangle vertex indices in drawing order.</param>
    /// <returns>The shared beam mesh.</returns>
    private static Mesh CreateMesh(int[] faces)
    {
        Vector3[] vertices =
        {
            new(0, -0.001f, 0),
            new(0, 0, 1),
            new(0, 0.001f, 0),
            new(0, 0, 0.333f),
            new(0, -0.001f, 0.666f),
            new(0, 0.001f, 0.666f),
            new(0.1f, -0.001f, 0),
            new(0.1f, 0, 1),
            new(0.1f, 0.001f, 1),
            new(-0.1f, -0.001f, 0),
            new(-0.1f, 0, 1),
            new(-0.1f, 0.001f, 1),
        };
        int[] lines = new int[faces.Length * 2];
        for (int index = 0; index < faces.Length; index += 3)
        {
            int destination = index * 2;
            lines[destination] = faces[index];
            lines[destination + 1] = faces[index + 1];
            lines[destination + 2] = faces[index + 1];
            lines[destination + 3] = faces[index + 2];
            lines[destination + 4] = faces[index + 2];
            lines[destination + 5] = faces[index];
        }
        Mesh mesh = new Mesh
        {
            name = "Tactical beam",
            vertices = vertices,
            uv = new Vector2[vertices.Length],
        };
        mesh.SetIndices(lines, MeshTopology.Lines, 0);
        mesh.RecalculateBounds();
        return mesh;
    }

    /// <summary>
    /// Creates the superlaser's narrow rectangular wireframe.
    /// </summary>
    /// <returns>The shared growing beam geometry.</returns>
    private static Mesh CreateSuperlaserLine()
    {
        Vector3[] vertices =
        {
            new(-0.005f, -0.005f, 0),
            new(-0.005f, -0.005f, 1),
            new(-0.005f, 0.005f, 1),
            new(-0.005f, 0.005f, 0),
            new(0.005f, -0.005f, 0),
            new(0.005f, -0.005f, 1),
            new(0.005f, 0.005f, 1),
            new(0.005f, 0.005f, 0),
        };
        return CreatePolygonMesh(
            vertices,
            new[]
            {
                4,
                0,
                3,
                2,
                1,
                4,
                0,
                4,
                7,
                3,
                4,
                0,
                1,
                5,
                4,
                4,
                6,
                7,
                4,
                5,
                4,
                6,
                5,
                1,
                2,
                4,
                6,
                2,
                3,
                7,
                0,
            }
        );
    }

    /// <summary>
    /// Creates the station's octagonal focusing emitter independently of the growing beam.
    /// </summary>
    /// <param name="depth">The deployment span.</param>
    /// <returns>The shared fixed emitter geometry.</returns>
    private static Mesh CreateSuperlaserEmitter(float depth)
    {
        Vector3[] vertices =
        {
            new(1, 0, 0),
            new(0.70710677f, 0.70710677f, 0),
            new(0, 1, 0),
            new(-0.70710677f, 0.70710677f, 0),
            new(-1, 0, 0),
            new(-0.70710677f, -0.70710677f, 0),
            new(0, -1, 0),
            new(0.70710677f, -0.70710677f, 0),
            new(0, 0, 1),
        };
        for (int index = 0; index < vertices.Length; index++)
            vertices[index] = new Vector3(
                vertices[index].x * depth * 0.06f,
                vertices[index].y * depth * 0.06f,
                vertices[index].z * depth * 0.15f - 10
            );
        return CreatePolygonMesh(
            vertices,
            new[]
            {
                3,
                1,
                8,
                0,
                3,
                2,
                8,
                1,
                3,
                3,
                8,
                2,
                3,
                4,
                8,
                3,
                3,
                5,
                8,
                4,
                3,
                6,
                8,
                5,
                3,
                7,
                8,
                6,
                3,
                0,
                8,
                7,
                0,
            }
        );
    }

    /// <summary>
    /// Converts a counted polygon stream into wireframe edges without adding triangulation diagonals.
    /// </summary>
    /// <param name="vertices">The shared vertex positions.</param>
    /// <param name="faces">The counted polygon stream, terminated by zero.</param>
    /// <returns>The wireframe mesh.</returns>
    private static Mesh CreatePolygonMesh(Vector3[] vertices, int[] faces)
    {
        List<int> lines = new();
        for (int offset = 0; faces[offset] != 0; )
        {
            int count = faces[offset++];
            for (int edge = 0; edge < count; edge++)
            {
                lines.Add(faces[offset + edge]);
                lines.Add(faces[offset + (edge + 1) % count]);
            }
            offset += count;
        }
        Mesh mesh = new Mesh
        {
            name = "Station beam",
            vertices = vertices,
            uv = new Vector2[vertices.Length],
        };
        mesh.SetIndices(lines, MeshTopology.Lines, 0);
        mesh.RecalculateBounds();
        return mesh;
    }

    /// <summary>
    /// Converts a simulation position into an encounter-local render position.
    /// </summary>
    /// <param name="value">The simulation position.</param>
    /// <returns>The render position.</returns>
    private static Vector3 ToUnity(Vector3N value) => new(value.X, value.Y, value.Z);

    /// <summary>
    /// Releases the owned objects, shared meshes, and materials.
    /// </summary>
    public void Dispose()
    {
        foreach (Beam beam in _active)
            UnityEngine.Object.Destroy(beam.Root);
        foreach (Beam beam in _available)
            UnityEngine.Object.Destroy(beam.Root);
        _active.Clear();
        _available.Clear();
        foreach (Mesh mesh in _meshes)
            UnityEngine.Object.Destroy(mesh);
        foreach (Material material in _materials)
            UnityEngine.Object.Destroy(material);
    }

    /// <summary>
    /// Holds one reusable effect's captured origin, target, and timer.
    /// </summary>
    private sealed class Beam
    {
        private readonly MeshFilter _mesh;
        private readonly MeshRenderer _renderer;
        private GameObject _emitter;
        private Vector3 _origin;
        private TacticalCombatUnit _target;
        private float _duration;
        private float _elapsed;
        private bool _superlaser;
        public GameObject Root { get; }

        /// <summary>
        /// Creates the reusable render object beneath the battle's isolated hierarchy.
        /// </summary>
        /// <param name="parent">The encounter's rendering root.</param>
        public Beam(Transform parent)
        {
            Root = new GameObject("Weapon beam");
            Root.layer = PresentationLayers.TacticalCombat;
            Root.transform.SetParent(parent, false);
            GameObject geometry = new GameObject(
                "Beam geometry",
                typeof(MeshFilter),
                typeof(MeshRenderer)
            );
            geometry.layer = PresentationLayers.TacticalCombat;
            geometry.transform.SetParent(Root.transform, false);
            _mesh = geometry.GetComponent<MeshFilter>();
            _renderer = geometry.GetComponent<MeshRenderer>();
            _renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _renderer.receiveShadows = false;
        }

        /// <summary>
        /// Captures one discharge's immutable drawing parameters and initial direction.
        /// </summary>
        /// <param name="origin">The initial emitter position.</param>
        /// <param name="target">The moving target.</param>
        /// <param name="mesh">The shared wireframe pattern.</param>
        /// <param name="material">The shared beam color.</param>
        /// <param name="length">The effect's initial length.</param>
        /// <param name="duration">The presentation lifetime in seconds.</param>
        /// <param name="superlaser">Whether the effect grows from its origin instead of traveling.</param>
        /// <param name="emitter">The optional fixed focusing emitter.</param>
        public void Configure(
            Vector3 origin,
            TacticalCombatUnit target,
            Mesh mesh,
            Material material,
            float length,
            float duration,
            bool superlaser,
            Mesh emitter
        )
        {
            _origin = origin;
            _target = target;
            _duration = duration;
            _elapsed = 0;
            _superlaser = superlaser;
            _mesh.sharedMesh = mesh;
            _renderer.sharedMaterial = material;
            Root.transform.localPosition = origin;
            _mesh.transform.localScale = new Vector3(1, 1, length);
            if (emitter != null)
            {
                if (_emitter == null)
                {
                    _emitter = new GameObject(
                        "Focusing emitter",
                        typeof(MeshFilter),
                        typeof(MeshRenderer)
                    );
                    _emitter.layer = PresentationLayers.TacticalCombat;
                    _emitter.transform.SetParent(Root.transform, false);
                }
                _emitter.GetComponent<MeshFilter>().sharedMesh = emitter;
                _emitter.GetComponent<MeshRenderer>().sharedMaterial = material;
            }
            _emitter?.SetActive(emitter != null);
            Vector3 delta = ToUnity(target.Position) - origin;
            Root.transform.localRotation =
                delta.sqrMagnitude > 0 ? Quaternion.LookRotation(delta) : Quaternion.identity;
        }

        /// <summary>
        /// Moves a traveling beam toward its live target or grows a superlaser from its fixed origin.
        /// </summary>
        /// <param name="seconds">The frame interval in seconds.</param>
        /// <returns>Whether the effect's lifetime remains active.</returns>
        public bool Advance(float seconds)
        {
            _elapsed += seconds;
            if (_elapsed > _duration)
                return false;
            Vector3 target = ToUnity(_target.Position);
            if (_superlaser)
            {
                Vector3 delta = target - _origin;
                if (delta.sqrMagnitude > 0)
                    Root.transform.localRotation = Quaternion.LookRotation(delta);
                _mesh.transform.localScale = new Vector3(1, 1, delta.magnitude * _elapsed);
            }
            else
                Root.transform.localPosition = Vector3.LerpUnclamped(
                    _origin,
                    target,
                    _elapsed / _duration
                );
            return true;
        }
    }
}
