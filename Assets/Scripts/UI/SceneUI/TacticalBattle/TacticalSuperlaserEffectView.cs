using System;
using UnityEngine;

/// <summary>
/// Presents the Death Star's expanding source-to-target superlaser effect.
/// </summary>
public sealed class TacticalSuperlaserEffectView : MonoBehaviour
{
    private const float _beamRadius = 0.08f;
    private const float _flareRadius = 0.8f;
    private const int _radialSegments = 8;
    private float elapsedTime;
    private float lifetime;
    private Material material;
    private Mesh beamMesh;
    private Mesh flareMesh;
    private Transform beam;
    private Transform flare;
    private float beamLength;

    /// <summary>
    /// Configures the procedural beam and terminal flare between two tactical positions.
    /// </summary>
    /// <param name="ownedMaterial">The effect material destroyed with this view.</param>
    /// <param name="source">The beam origin in the parent's coordinate space.</param>
    /// <param name="target">The beam destination in the parent's coordinate space.</param>
    /// <param name="duration">The beam expansion time in seconds.</param>
    public void Initialize(Material ownedMaterial, Vector3 source, Vector3 target, float duration)
    {
        material = ownedMaterial ?? throw new ArgumentNullException(nameof(ownedMaterial));
        if (duration <= 0f)
            throw new ArgumentOutOfRangeException(nameof(duration));

        Vector3 direction = target - source;
        if (direction.sqrMagnitude <= Mathf.Epsilon)
            throw new ArgumentException(
                "The superlaser requires distinct endpoints.",
                nameof(target)
            );

        lifetime = duration;
        beamLength = direction.magnitude;
        transform.localPosition = source;
        transform.localRotation = Quaternion.LookRotation(direction.normalized, Vector3.up);

        beamMesh = CreateBeamMesh();
        GameObject beamObject = CreateMeshObject("Beam", beamMesh);
        beam = beamObject.transform;
        beam.localScale = new Vector3(1f, 1f, 0f);

        flareMesh = CreateFlareMesh();
        GameObject flareObject = CreateMeshObject("Terminal Flare", flareMesh);
        flare = flareObject.transform;
        flare.localPosition = Vector3.forward * beamLength;
        flare.localScale = Vector3.zero;
    }

    /// <summary>
    /// Advances the beam expansion and releases the completed effect.
    /// </summary>
    private void Update()
    {
        if (material == null)
            return;

        elapsedTime += Time.deltaTime;
        float progress = Mathf.Clamp01(elapsedTime / lifetime);
        beam.localScale = new Vector3(1f, 1f, beamLength * progress);
        flare.localScale = Vector3.one * progress;

        if (elapsedTime >= lifetime)
            Destroy(gameObject);
    }

    /// <summary>
    /// Creates one child render object owned by the effect.
    /// </summary>
    /// <param name="objectName">The child object's display name.</param>
    /// <param name="mesh">The procedural mesh rendered by the child.</param>
    /// <returns>The initialized child object.</returns>
    private GameObject CreateMeshObject(string objectName, Mesh mesh)
    {
        GameObject child = new GameObject(objectName);
        child.transform.SetParent(transform, false);
        child.AddComponent<MeshFilter>().sharedMesh = mesh;
        child.AddComponent<MeshRenderer>().sharedMaterial = material;
        return child;
    }

    /// <summary>
    /// Creates the narrow eight-sided beam prism expanded along its local Z axis.
    /// </summary>
    /// <returns>The owned procedural beam mesh.</returns>
    private static Mesh CreateBeamMesh()
    {
        Vector3[] vertices = new Vector3[_radialSegments * 2];
        int[] triangles = new int[_radialSegments * 6];
        for (int index = 0; index < _radialSegments; index++)
        {
            float angle = index * Mathf.PI * 2f / _radialSegments;
            Vector3 radial = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f) * _beamRadius;
            vertices[index] = radial;
            vertices[index + _radialSegments] = radial + Vector3.forward;

            int next = (index + 1) % _radialSegments;
            int triangle = index * 6;
            triangles[triangle] = index;
            triangles[triangle + 1] = next;
            triangles[triangle + 2] = index + _radialSegments;
            triangles[triangle + 3] = next;
            triangles[triangle + 4] = next + _radialSegments;
            triangles[triangle + 5] = index + _radialSegments;
        }

        Mesh mesh = new Mesh { name = "Tactical superlaser beam" };
        mesh.vertices = vertices;
        mesh.triangles = triangles;
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }

    /// <summary>
    /// Creates the eight-point terminal flare surrounding the beam impact.
    /// </summary>
    /// <returns>The owned procedural flare mesh.</returns>
    private static Mesh CreateFlareMesh()
    {
        Vector3[] vertices = new Vector3[_radialSegments + 1];
        int[] triangles = new int[_radialSegments * 3];
        vertices[0] = Vector3.zero;
        for (int index = 0; index < _radialSegments; index++)
        {
            float angle = index * Mathf.PI * 2f / _radialSegments;
            vertices[index + 1] =
                new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f) * _flareRadius;
            int triangle = index * 3;
            triangles[triangle] = 0;
            triangles[triangle + 1] = index + 1;
            triangles[triangle + 2] = ((index + 1) % _radialSegments) + 1;
        }

        Mesh mesh = new Mesh { name = "Tactical superlaser flare" };
        mesh.vertices = vertices;
        mesh.triangles = triangles;
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }

    /// <summary>
    /// Releases the material and procedural meshes owned by this effect.
    /// </summary>
    private void OnDestroy()
    {
        if (material != null)
            Destroy(material);
        if (beamMesh != null)
            Destroy(beamMesh);
        if (flareMesh != null)
            Destroy(flareMesh);
    }
}
