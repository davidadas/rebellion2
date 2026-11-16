using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Owns the battle frame's paired blue wireframe boundary resources.
/// </summary>
internal sealed class TacticalCombatHolocube : IDisposable
{
    private readonly GameObject _root;
    private readonly Mesh[] _meshes;
    private readonly Material _material;

    /// <summary>
    /// Builds both polygon boundary orientations at the encounter's deployment scale.
    /// </summary>
    /// <param name="parent">The encounter's world-coordinate frame.</param>
    /// <param name="shader">The unlit tactical shader.</param>
    /// <param name="scale">The full boundary width, height, and depth.</param>
    /// <param name="visible">Whether the boundary resources are attached for this encounter.</param>
    public TacticalCombatHolocube(Transform parent, Shader shader, float scale, bool visible)
    {
        Vector3[] vertices =
        {
            new(-0.5f, -0.5f, -0.5f),
            new(-0.5f, -0.5f, 0.5f),
            new(-0.5f, 0.5f, 0.5f),
            new(-0.5f, 0.5f, -0.5f),
            new(0.5f, -0.5f, -0.5f),
            new(0.5f, -0.5f, 0.5f),
            new(0.5f, 0.5f, 0.5f),
            new(0.5f, 0.5f, -0.5f),
        };
        for (int index = 0; index < vertices.Length; index++)
            vertices[index] *= scale;
        int[][] faces =
        {
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
            },
            new[]
            {
                4,
                0,
                1,
                2,
                3,
                4,
                0,
                3,
                7,
                4,
                4,
                0,
                4,
                5,
                1,
                4,
                6,
                5,
                4,
                7,
                4,
                6,
                2,
                1,
                5,
                4,
                6,
                7,
                3,
                2,
                0,
            },
        };
        _root = new GameObject("Holocube");
        _root.layer = PresentationLayers.TacticalCombat;
        _root.transform.SetParent(parent, false);
        _material = new Material(shader)
        {
            color = Color.blue,
            mainTexture = Texture2D.whiteTexture,
        };
        _meshes = new Mesh[faces.Length];
        for (int index = 0; index < faces.Length; index++)
        {
            Mesh mesh = new Mesh { name = "Holocube boundary " + index, vertices = vertices };
            mesh.SetIndices(GetBoundaryIndices(faces[index]), MeshTopology.Lines, 0);
            mesh.RecalculateBounds();
            _meshes[index] = mesh;
            GameObject boundary = new GameObject(
                "Boundary " + index,
                typeof(MeshFilter),
                typeof(MeshRenderer)
            );
            boundary.layer = PresentationLayers.TacticalCombat;
            boundary.transform.SetParent(_root.transform, false);
            boundary.GetComponent<MeshFilter>().sharedMesh = mesh;
            MeshRenderer renderer = boundary.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = _material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.enabled = visible;
        }
    }

    /// <summary>
    /// Retains each polygon boundary once without adding triangulation diagonals.
    /// </summary>
    /// <param name="faces">The counted polygon faces followed by a zero terminator.</param>
    /// <returns>The ordered unique line endpoints.</returns>
    private static int[] GetBoundaryIndices(int[] faces)
    {
        List<int> indices = new();
        HashSet<ulong> edges = new();
        int offset = 0;
        while (faces[offset] != 0)
        {
            int count = faces[offset++];
            for (int index = 0; index < count; index++)
            {
                int first = faces[offset + index];
                int second = faces[offset + (index + 1) % count];
                ulong key = ((ulong)Math.Min(first, second) << 32) | (uint)Math.Max(first, second);
                if (first != second && edges.Add(key))
                {
                    indices.Add(first);
                    indices.Add(second);
                }
            }
            offset += count;
        }
        return indices.ToArray();
    }

    /// <summary>
    /// Detaches the boundary pair and releases its screen-owned geometry and material.
    /// </summary>
    public void Dispose()
    {
        UnityEngine.Object.Destroy(_root);
        foreach (Mesh mesh in _meshes)
            UnityEngine.Object.Destroy(mesh);
        UnityEngine.Object.Destroy(_material);
    }
}
