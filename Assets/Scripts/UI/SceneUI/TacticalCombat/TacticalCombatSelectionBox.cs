using System;
using UnityEngine;

/// <summary>
/// Owns a capital ship's local bounding-box wireframe and its selection colors.
/// </summary>
internal sealed class TacticalCombatSelectionBox : IDisposable
{
    private readonly GameObject _root;
    private readonly Mesh _mesh;
    private readonly Material _material;
    private readonly Color _factionColor;
    private int _state = 1;
    public Bounds Bounds { get; }

    /// <summary>
    /// Captures the middle-detail model's bounds in the unit's coordinate system.
    /// </summary>
    /// <param name="parent">The capital ship's presentation transform.</param>
    /// <param name="model">The middle-detail model hierarchy.</param>
    /// <param name="shader">The battle's unlit shader.</param>
    /// <param name="factionIndex">The ship's authored faction index.</param>
    public TacticalCombatSelectionBox(
        Transform parent,
        Transform model,
        Shader shader,
        int factionIndex
    )
    {
        Vector3 minimum = new(
            float.PositiveInfinity,
            float.PositiveInfinity,
            float.PositiveInfinity
        );
        Vector3 maximum = new(
            float.NegativeInfinity,
            float.NegativeInfinity,
            float.NegativeInfinity
        );
        bool populated = false;
        foreach (MeshFilter filter in model.GetComponentsInChildren<MeshFilter>(true))
        {
            if (filter.sharedMesh == null)
                continue;
            Matrix4x4 transform = parent.worldToLocalMatrix * filter.transform.localToWorldMatrix;
            foreach (Vector3 vertex in filter.sharedMesh.vertices)
            {
                Vector3 point = transform.MultiplyPoint3x4(vertex);
                minimum = Vector3.Min(minimum, point);
                maximum = Vector3.Max(maximum, point);
                populated = true;
            }
        }
        if (!populated)
            throw new ArgumentException("A selection box requires model geometry.", nameof(model));
        Bounds = new Bounds((minimum + maximum) * 0.5f, maximum - minimum);
        Vector3[] vertices = new Vector3[8];
        for (int index = 0; index < vertices.Length; index++)
            vertices[index] = new Vector3(
                (index & 4) == 0 ? minimum.x : maximum.x,
                (index & 2) == 0 ? minimum.y : maximum.y,
                (index & 1) == 0 ? minimum.z : maximum.z
            );
        _mesh = new Mesh { name = "Capital selection box", vertices = vertices };
        _mesh.SetIndices(
            new[] { 0, 1, 0, 2, 0, 4, 1, 3, 1, 5, 2, 3, 2, 6, 3, 7, 4, 5, 4, 6, 5, 7, 6, 7 },
            MeshTopology.Lines,
            0
        );
        _mesh.RecalculateBounds();
        _material = new Material(shader);
        _factionColor = factionIndex == 1 ? Color.green : Color.red;
        _root = new GameObject("Selection box", typeof(MeshFilter), typeof(MeshRenderer));
        _root.layer = PresentationLayers.TacticalCombat;
        _root.transform.SetParent(parent, false);
        _root.GetComponent<MeshFilter>().sharedMesh = _mesh;
        MeshRenderer renderer = _root.GetComponent<MeshRenderer>();
        renderer.sharedMaterial = _material;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        _root.SetActive(false);
    }

    /// <summary>
    /// Applies one render state while retaining an existing state during tracked-camera suppression.
    /// </summary>
    /// <param name="state">One for no box, two for yellow, three for faction color, or four for white.</param>
    /// <param name="tracked">Whether the camera currently tracks this capital ship.</param>
    public void SetState(int state, bool tracked)
    {
        if (_state == state || state != 1 && tracked)
            return;
        _state = state;
        _root.SetActive(state != 1);
        if (state != 1)
            _material.color =
                state == 2 ? Color.yellow
                : state == 4 ? Color.white
                : _factionColor;
    }

    /// <summary>
    /// Releases the owned wireframe, material, and hierarchy.
    /// </summary>
    public void Dispose()
    {
        UnityEngine.Object.Destroy(_root);
        UnityEngine.Object.Destroy(_mesh);
        UnityEngine.Object.Destroy(_material);
    }
}
