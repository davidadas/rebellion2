using System;
using UnityEngine;

/// <summary>
/// Identifies a command for the battle camera's primary view.
/// </summary>
public enum TacticalCombatCameraCommand
{
    ZoomIn,
    ZoomOut,
    RotateLeft,
    RotateRight,
    TiltUp,
    TiltDown,
    Save,
    Restore,
    Focus,
}

/// <summary>
/// Owns the primary battle camera's zoom, angular increments, saved view, and tracked focus.
/// </summary>
public sealed class TacticalCombatCameraState
{
    private readonly int _defaultYaw;
    private readonly float _distance;
    private Func<Vector3> _focus;
    private Snapshot? _saved;
    public int? TrackedObjectIndex { get; private set; }
    public float Field { get; private set; } = 0.2f;
    public uint ZoomLevel { get; private set; } = 5;
    public int AngularStep { get; private set; } = 5;
    public int Pitch { get; private set; } = 30;
    public int Yaw { get; private set; }

    /// <summary>
    /// Initializes the encounter's primary camera distance and faction-facing angle.
    /// </summary>
    /// <param name="distance">The camera's distance from the battle origin.</param>
    /// <param name="factionIndex">The player's authored faction index.</param>
    public TacticalCombatCameraState(float distance, int factionIndex)
    {
        _distance = distance;
        _defaultYaw = factionIndex == 0 ? -30 : 150;
        Yaw = _defaultYaw;
    }

    /// <summary>
    /// Applies a primary-camera command without changing encounter simulation time.
    /// </summary>
    /// <param name="command">The camera operation.</param>
    public void Apply(TacticalCombatCameraCommand command)
    {
        switch (command)
        {
            case TacticalCombatCameraCommand.ZoomIn:
                Zoom(true);
                break;
            case TacticalCombatCameraCommand.ZoomOut:
                Zoom(false);
                break;
            case TacticalCombatCameraCommand.RotateLeft:
                Yaw = unchecked(Yaw - AngularStep);
                break;
            case TacticalCombatCameraCommand.RotateRight:
                Yaw = unchecked(Yaw + AngularStep);
                break;
            case TacticalCombatCameraCommand.TiltUp:
                if (Pitch < 90)
                    Pitch = unchecked(Pitch + AngularStep);
                break;
            case TacticalCombatCameraCommand.TiltDown:
                if (Pitch > -90)
                    Pitch = unchecked(Pitch - AngularStep);
                break;
            case TacticalCombatCameraCommand.Save:
                _saved = new Snapshot(this);
                break;
            case TacticalCombatCameraCommand.Restore:
                Restore();
                break;
        }
    }

    /// <summary>
    /// Tracks the selected render object's live world position until another focus is requested.
    /// </summary>
    /// <param name="position">Resolves the object's current world position, or null to clear tracking.</param>
    /// <param name="objectIndex">The tracked unit's battle registration identifier, or null for a point.</param>
    public void Track(Func<Vector3> position, int? objectIndex = null)
    {
        _focus = position;
        TrackedObjectIndex = position == null ? null : objectIndex;
    }

    /// <summary>
    /// Computes the camera transform, keeping tracking separate from its orbital position.
    /// </summary>
    /// <param name="origin">The battle scene's world origin.</param>
    /// <param name="position">Receives the camera's world position.</param>
    /// <param name="rotation">Receives the camera's world orientation.</param>
    public void GetPose(Vector3 origin, out Vector3 position, out Quaternion rotation)
    {
        if (Yaw > 180)
            Yaw = unchecked(Yaw - 360);
        if (Yaw < -180)
            Yaw = unchecked(Yaw + 360);
        rotation = Quaternion.Euler(Pitch, Yaw, 0);
        position = origin - rotation * Vector3.forward * _distance;
        if (_focus == null)
            return;
        Vector3 direction = _focus() - position;
        if (direction != Vector3.zero)
            rotation = Quaternion.LookRotation(direction, Vector3.up);
    }

    /// <summary>
    /// Changes the field and the angular increment together while retaining the full zoom-level counter.
    /// </summary>
    /// <param name="inward">Whether to narrow the viewport field.</param>
    private void Zoom(bool inward)
    {
        double scaled = Field * (inward ? 0.9 : 1.1);
        Field = (float)scaled;
        if (inward && !(scaled >= 0.005f))
            Field = 0.005f;
        else if (!inward && scaled > 1.5f)
            Field = 1.5f;
        ZoomLevel = inward ? unchecked(ZoomLevel - 1) : unchecked(ZoomLevel + 1);
        AngularStep = unchecked((int)ZoomLevel) < 1 ? 1 : (int)Math.Min(ZoomLevel, 5);
    }

    /// <summary>
    /// Restores a saved primary view, or resets its authored angles and field before the first save.
    /// </summary>
    private void Restore()
    {
        if (!_saved.HasValue)
        {
            Pitch = 30;
            Yaw = _defaultYaw;
            Field = 0.2f;
            return;
        }
        Snapshot saved = _saved.Value;
        Pitch = saved.Pitch;
        Yaw = saved.Yaw;
        Field = saved.Field;
        ZoomLevel = saved.ZoomLevel;
        AngularStep = saved.AngularStep;
        _focus = saved.Focus;
        TrackedObjectIndex = saved.TrackedObjectIndex;
    }

    private readonly struct Snapshot
    {
        public int Pitch { get; }
        public int Yaw { get; }
        public float Field { get; }
        public uint ZoomLevel { get; }
        public int AngularStep { get; }
        public Func<Vector3> Focus { get; }
        public int? TrackedObjectIndex { get; }

        /// <summary>
        /// Captures the primary view while retaining its tracked object's live position provider.
        /// </summary>
        /// <param name="camera">The view being saved.</param>
        public Snapshot(TacticalCombatCameraState camera)
        {
            Pitch = camera.Pitch;
            Yaw = camera.Yaw;
            Field = camera.Field;
            ZoomLevel = camera.ZoomLevel;
            AngularStep = camera.AngularStep;
            Focus = camera._focus;
            TrackedObjectIndex = camera.TrackedObjectIndex;
        }
    }
}
