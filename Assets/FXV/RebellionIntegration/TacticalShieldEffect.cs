using System;
using FXV;
using UnityEngine;
using UnityEngine.Rendering.HighDefinition;

/// <summary>
/// Adapts the installed shield effect to tactical combat's presentation contract.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Shield))]
public sealed class TacticalShieldEffect : MonoBehaviour, ITacticalShieldEffect
{
    private const string _postprocessObjectName = "Tactical Shield Postprocess";
    private const float _minimumProjectionDenominator = 0.0001f;

    private Shield _shield;
    private MeshFilter _surface;
    private bool _initialized;
    private bool _active;

    /// <inheritdoc />
    public void Configure(Camera camera)
    {
        if (!camera)
            throw new ArgumentNullException(nameof(camera));
        _shield = GetComponent<Shield>();
        if (!_shield)
            throw new MissingComponentException($"{name} is missing its shield effect.");
        _surface = _shield.GetComponentInChildren<MeshFilter>(true);
        if (!_surface || !_surface.sharedMesh)
            throw new MissingComponentException($"{name} is missing its shield surface mesh.");
        _shield.SetShieldVisuallyActive(false);
        EnsurePostprocess(camera);
    }

    /// <inheritdoc />
    public void SetActive(bool active)
    {
        if (!_shield)
            throw new InvalidOperationException("The tactical shield effect is not configured.");
        if (!_initialized)
        {
            _shield.SetShieldActive(active, false);
            _active = active;
            _initialized = true;
            return;
        }
        if (_active == active)
            return;
        _shield.SetShieldActive(active);
        _active = active;
    }

    /// <inheritdoc />
    public void ShowHit(Vector3 position, Vector3 normal, float scale, float duration)
    {
        if (!_shield || !_active)
            return;
        ProjectOntoSurface(
            position,
            normal,
            out Vector3 surfacePosition,
            out Vector3 surfaceNormal
        );
        _shield.OnHit(surfacePosition, surfaceNormal, scale, duration);
    }

    /// <summary>
    /// Projects a hull contact radially onto the authored shield mesh and resolves its outward normal.
    /// </summary>
    /// <param name="position">The world-space hull contact.</param>
    /// <param name="fallbackNormal">The outward weapon-contact normal.</param>
    /// <param name="surfacePosition">The resolved world-space shield contact.</param>
    /// <param name="surfaceNormal">The resolved world-space shield normal.</param>
    private void ProjectOntoSurface(
        Vector3 position,
        Vector3 fallbackNormal,
        out Vector3 surfacePosition,
        out Vector3 surfaceNormal
    )
    {
        Transform surfaceTransform = _surface.transform;
        Bounds bounds = _surface.sharedMesh.bounds;
        Vector3 localDirection = surfaceTransform.InverseTransformPoint(position) - bounds.center;
        if (localDirection.sqrMagnitude < _minimumProjectionDenominator)
            localDirection = surfaceTransform.worldToLocalMatrix.MultiplyVector(fallbackNormal);

        Vector3 extents = bounds.extents;
        float denominator = Mathf.Sqrt(
            Square(localDirection.x / extents.x)
                + Square(localDirection.y / extents.y)
                + Square(localDirection.z / extents.z)
        );
        if (denominator < _minimumProjectionDenominator)
        {
            surfacePosition = position;
            surfaceNormal = fallbackNormal;
            return;
        }

        Vector3 localSurface = bounds.center + localDirection / denominator;
        surfacePosition = surfaceTransform.TransformPoint(localSurface);
        Vector3 offset = localSurface - bounds.center;
        Vector3 localNormal = new Vector3(
            offset.x / Square(extents.x),
            offset.y / Square(extents.y),
            offset.z / Square(extents.z)
        ).normalized;
        surfaceNormal = surfaceTransform
            .worldToLocalMatrix.transpose.MultiplyVector(localNormal)
            .normalized;
    }

    /// <summary>
    /// Squares one scalar without invoking a general exponent function.
    /// </summary>
    /// <param name="value">The value to square.</param>
    /// <returns>The squared value.</returns>
    private static float Square(float value)
    {
        return value * value;
    }

    /// <summary>
    /// Installs one camera-local HDRP postprocess pass shared by every shield in the battle.
    /// </summary>
    /// <param name="camera">The tactical battle camera.</param>
    private static void EnsurePostprocess(Camera camera)
    {
        if (!camera.GetComponent<ShieldPostprocess>())
            camera.gameObject.AddComponent<ShieldPostprocess>();
        Transform existing = camera.transform.Find(_postprocessObjectName);
        if (existing)
            return;
        GameObject host = new GameObject(_postprocessObjectName);
        host.transform.SetParent(camera.transform, false);
        CustomPassVolume volume = host.AddComponent<CustomPassVolume>();
        volume.isGlobal = true;
        volume.injectionPoint = CustomPassInjectionPoint.BeforePostProcess;
        volume.AddPassOfType<ShieldPostprocessHDRPPass>().name = "Shield Postprocess";
    }
}
