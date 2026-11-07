using System;
using System.Collections.Generic;
using Rebellion.Gameplay;
using UnityEngine;
using Vector3N = System.Numerics.Vector3;

/// <summary>
/// Owns the four independently visible navigation rings and their selectable tetrahedra.
/// </summary>
internal sealed class TacticalCombatNavigationRenderer : IDisposable
{
    private readonly GameObject _root;
    private readonly Mesh _mesh;
    private readonly Vector3[] _vertices;
    private readonly int[] _triangles = { 2, 1, 0, 3, 0, 1, 3, 1, 2, 3, 2, 0 };
    private readonly Material[] _materials = new Material[4];
    private readonly List<Point> _points = new();
    private HashSet<Vector3N> _selected = new();
    private HashSet<Vector3N> _nextSelected = new();
    private readonly bool[] _rings = new bool[4];

    /// <summary>
    /// Creates shared marker resources and initially hidden points from the encounter's navigation lattice.
    /// </summary>
    /// <param name="parent">The isolated battle hierarchy.</param>
    /// <param name="shader">The tactical unlit shader.</param>
    /// <param name="navigation">The encounter's authoritative lattice.</param>
    public TacticalCombatNavigationRenderer(
        Transform parent,
        Shader shader,
        TacticalCombatNavigation navigation
    )
    {
        _root = new GameObject("Navigation points");
        _root.transform.SetParent(parent, false);
        _vertices = new[]
        {
            new Vector3(
                0,
                BitConverter.Int32BitsToSingle(unchecked((int)0xbe50ff97)),
                BitConverter.Int32BitsToSingle(0x3f13d07d)
            ) * 1.5f,
            new Vector3(
                .5f,
                BitConverter.Int32BitsToSingle(unchecked((int)0xbe50ff97)),
                BitConverter.Int32BitsToSingle(unchecked((int)0xbe93d07d))
            ) * 1.5f,
            new Vector3(
                -.5f,
                BitConverter.Int32BitsToSingle(unchecked((int)0xbe50ff97)),
                BitConverter.Int32BitsToSingle(unchecked((int)0xbe93d07d))
            ) * 1.5f,
            new Vector3(0, BitConverter.Int32BitsToSingle(0x3f1cc63f), 0) * 1.5f,
        };
        _mesh = new Mesh
        {
            name = "Navigation marker",
            vertices = _vertices,
            triangles = _triangles,
        };
        _mesh.RecalculateBounds();
        _mesh.RecalculateNormals();
        Color[] colors =
        {
            new(0, BitConverter.Int32BitsToSingle(0x3f0a7efa), 1),
            Color.red,
            Color.white,
            Color.magenta,
        };
        for (int index = 0; index < colors.Length; index++)
            _materials[index] = new Material(shader) { color = colors[index] };
        foreach (Vector3N position in navigation.Waypoints)
        {
            int index = _points.Count;
            int ring = index < 27 ? 0 : (index - 27) / 26 + 1;
            int color =
                position.Y < 0 ? 0
                : position.Y == 0 ? 1
                : 2;
            GameObject marker = new GameObject(
                "Navigation point",
                typeof(MeshFilter),
                typeof(MeshRenderer)
            );
            marker.layer = PresentationLayers.TacticalCombat;
            marker.transform.SetParent(_root.transform, false);
            marker.transform.localPosition = new Vector3(position.X, position.Y, position.Z);
            marker.GetComponent<MeshFilter>().sharedMesh = _mesh;
            MeshRenderer renderer = marker.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = _materials[color];
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            _points.Add(new Point(marker, renderer, position, ring, color));
            marker.SetActive(false);
        }
    }

    /// <summary>
    /// Toggles one navigation ring without changing any issued movement orders.
    /// </summary>
    /// <param name="ring">The zero-based navigation ring.</param>
    public void ToggleRing(int ring)
    {
        if (ring < 0 || ring >= _rings.Length)
            throw new ArgumentOutOfRangeException(nameof(ring));
        _rings[ring] = !_rings[ring];
        foreach (Point point in _points)
            if (point.Ring == ring)
                point.Root.SetActive(_rings[ring]);
    }

    /// <summary>
    /// Highlights the requested routes of selected groups using one indexed selection snapshot.
    /// </summary>
    /// <param name="groups">The selected groups in encounter order.</param>
    public void UpdateSelection(IReadOnlyList<TacticalCombatGroup> groups)
    {
        _nextSelected.Clear();
        foreach (TacticalCombatGroup group in groups)
        foreach (Vector3N position in group.RequestedWaypoints)
            _nextSelected.Add(position);
        if (_selected.SetEquals(_nextSelected))
            return;
        (_selected, _nextSelected) = (_nextSelected, _selected);
        foreach (Point point in _points)
            point.Renderer.sharedMaterial = _materials[
                _selected.Contains(point.Position) ? 3 : point.Color
            ];
    }

    /// <summary>
    /// Resolves a pointer ray only against visible markers owned by this encounter.
    /// </summary>
    /// <param name="ray">The viewport camera ray.</param>
    /// <param name="distance">The camera's visible depth.</param>
    /// <param name="position">Receives the selected lattice point.</param>
    /// <returns>Whether the ray intersects a visible navigation marker.</returns>
    public bool TryPick(Ray ray, float distance, out Vector3N position)
    {
        position = default;
        Vector3 origin = _root.transform.InverseTransformPoint(ray.origin);
        Vector3 direction = _root.transform.InverseTransformVector(ray.direction);
        bool found = false;
        foreach (Point point in _points)
        {
            if (!point.Root.activeInHierarchy)
                continue;
            Vector3 local = origin - point.Root.transform.localPosition;
            for (int face = 0; face < _triangles.Length; face += 3)
                if (
                    TacticalCombatMeshPicker.TryIntersect(
                        local,
                        direction,
                        _vertices[_triangles[face]],
                        _vertices[_triangles[face + 1]],
                        _vertices[_triangles[face + 2]],
                        out float hit
                    )
                    && hit < distance
                )
                {
                    distance = hit;
                    position = point.Position;
                    found = true;
                }
        }
        return found;
    }

    /// <summary>
    /// Releases this encounter's marker hierarchy, shared mesh, and materials.
    /// </summary>
    public void Dispose()
    {
        UnityEngine.Object.Destroy(_root);
        UnityEngine.Object.Destroy(_mesh);
        foreach (Material material in _materials)
            UnityEngine.Object.Destroy(material);
    }

    private readonly struct Point
    {
        public GameObject Root { get; }
        public MeshRenderer Renderer { get; }
        public Vector3N Position { get; }
        public int Ring { get; }
        public int Color { get; }

        /// <summary>
        /// Captures the owned scene marker and its immutable navigation metadata.
        /// </summary>
        /// <param name="root">The owned marker.</param>
        /// <param name="renderer">The marker's material owner.</param>
        /// <param name="position">The encounter-local navigation point.</param>
        /// <param name="ring">The visibility ring.</param>
        /// <param name="color">The vertical-plane color.</param>
        public Point(GameObject root, MeshRenderer renderer, Vector3N position, int ring, int color)
        {
            Root = root;
            Renderer = renderer;
            Position = position;
            Ring = ring;
            Color = color;
        }
    }
}
