using System.Collections.Generic;
using Rebellion.Gameplay;
using UnityEngine;

/// <summary>
/// Resolves battle-unit selection against the visible model faces and billboard quads.
/// </summary>
internal sealed class TacticalCombatMeshPicker
{
    private readonly Dictionary<Mesh, Geometry> _geometry = new();
    private readonly List<Entry> _entries = new();

    /// <summary>
    /// Captures shared mesh geometry once while a unit's presentation is prepared.
    /// </summary>
    /// <param name="root">The unit's owned model hierarchy.</param>
    /// <param name="unit">The associated encounter unit.</param>
    public void Register(GameObject root, TacticalCombatUnit unit)
    {
        foreach (MeshFilter filter in root.GetComponentsInChildren<MeshFilter>(true))
        {
            Mesh mesh = filter.sharedMesh;
            MeshRenderer renderer = filter.GetComponent<MeshRenderer>();
            if (mesh == null || renderer == null)
                continue;
            if (!_geometry.TryGetValue(mesh, out Geometry geometry))
            {
                geometry = new Geometry(mesh.vertices, mesh.triangles);
                _geometry.Add(mesh, geometry);
            }
            _entries.Add(new Entry(unit, renderer, geometry));
        }
    }

    /// <summary>
    /// Finds the nearest visible participant intersected by a viewport ray.
    /// </summary>
    /// <param name="ray">The battle camera's world-space ray.</param>
    /// <param name="distance">The maximum distance, replaced with the closest hit distance.</param>
    /// <returns>The intersected unit, or null.</returns>
    public TacticalCombatUnit Pick(Ray ray, ref float distance)
    {
        TacticalCombatUnit selected = null;
        foreach (Entry entry in _entries)
        {
            MeshRenderer renderer = entry.Renderer;
            if (
                !entry.Unit.IsTargetable
                || !renderer.enabled
                || !renderer.gameObject.activeInHierarchy
                || !renderer.bounds.IntersectRay(ray, out float boundDistance)
                || boundDistance > distance
            )
                continue;
            Vector3 origin = renderer.transform.InverseTransformPoint(ray.origin);
            Vector3 direction = renderer.transform.InverseTransformVector(ray.direction);
            Vector3[] vertices = entry.Geometry.Vertices;
            int[] triangles = entry.Geometry.Triangles;
            for (int face = 0; face < triangles.Length; face += 3)
                if (
                    TryIntersect(
                        origin,
                        direction,
                        vertices[triangles[face]],
                        vertices[triangles[face + 1]],
                        vertices[triangles[face + 2]],
                        out float hit
                    )
                    && hit < distance
                )
                {
                    distance = hit;
                    selected = entry.Unit;
                }
        }
        return selected;
    }

    /// <summary>
    /// Intersects a ray with one triangle while retaining the world ray's distance parameter.
    /// </summary>
    /// <param name="origin">The ray origin in the mesh's coordinate system.</param>
    /// <param name="direction">The unnormalized transformed ray direction.</param>
    /// <param name="first">The first triangle vertex.</param>
    /// <param name="second">The second triangle vertex.</param>
    /// <param name="third">The third triangle vertex.</param>
    /// <param name="distance">Receives the nonnegative ray distance.</param>
    /// <returns>Whether the ray intersects the triangle.</returns>
    internal static bool TryIntersect(
        Vector3 origin,
        Vector3 direction,
        Vector3 first,
        Vector3 second,
        Vector3 third,
        out float distance
    )
    {
        distance = 0;
        Vector3 edge = second - first;
        Vector3 other = third - first;
        Vector3 cross = Vector3.Cross(direction, other);
        float determinant = Vector3.Dot(edge, cross);
        if (determinant == 0)
            return false;
        Vector3 delta = origin - first;
        float u = Vector3.Dot(delta, cross) / determinant;
        if (u < 0 || u > 1)
            return false;
        Vector3 normal = Vector3.Cross(delta, edge);
        float v = Vector3.Dot(direction, normal) / determinant;
        if (v < 0 || u + v > 1)
            return false;
        distance = Vector3.Dot(other, normal) / determinant;
        return distance >= 0;
    }

    /// <summary>
    /// Holds shared immutable mesh coordinates without retaining extra Unity resources.
    /// </summary>
    private sealed class Geometry
    {
        public Vector3[] Vertices { get; }
        public int[] Triangles { get; }

        /// <summary>
        /// Captures one mesh's face data for repeated pointer queries.
        /// </summary>
        /// <param name="vertices">The mesh-local positions.</param>
        /// <param name="triangles">The indexed faces.</param>
        public Geometry(Vector3[] vertices, int[] triangles)
        {
            Vertices = vertices;
            Triangles = triangles;
        }
    }

    /// <summary>
    /// Associates one visible mesh instance with its encounter participant.
    /// </summary>
    private readonly struct Entry
    {
        public TacticalCombatUnit Unit { get; }
        public MeshRenderer Renderer { get; }
        public Geometry Geometry { get; }

        /// <summary>
        /// Captures a renderer and its shared mesh data.
        /// </summary>
        /// <param name="unit">The selected encounter participant.</param>
        /// <param name="renderer">The visible mesh instance.</param>
        /// <param name="geometry">The shared face data.</param>
        public Entry(TacticalCombatUnit unit, MeshRenderer renderer, Geometry geometry)
        {
            Unit = unit;
            Renderer = renderer;
            Geometry = geometry;
        }
    }
}
