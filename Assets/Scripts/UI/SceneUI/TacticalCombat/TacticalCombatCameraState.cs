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
    private Func<TacticalCombatCameraFocus?> _categoryFocus;
    private float _primaryField = 0.2f;
    private float _categoryField;
    private int? _trackedObjectIndex;
    private int? _categoryObjectIndex;
    private Quaternion _lastRotation = Quaternion.identity;
    public int? TrackedObjectIndex => IsCategoryView ? _categoryObjectIndex : _trackedObjectIndex;
    public int? Category { get; private set; }
    public bool IsCategoryView => _categoryFocus != null;
    public float Field => IsCategoryView ? _categoryField : _primaryField;
    public float NearPlane => IsCategoryView ? 0.1f : 1;
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
        if (IsCategoryView)
        {
            if (
                command is TacticalCombatCameraCommand.ZoomIn or TacticalCombatCameraCommand.ZoomOut
            )
            {
                ZoomCategory(command == TacticalCombatCameraCommand.ZoomIn);
                return;
            }
            if (command == TacticalCombatCameraCommand.Focus)
            {
                Restore();
                return;
            }
            if (
                command
                is not TacticalCombatCameraCommand.Save
                    and not TacticalCombatCameraCommand.Restore
            )
                return;
        }
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
        if (IsCategoryView)
            Restore();
        _focus = position;
        _trackedObjectIndex = position == null ? null : objectIndex;
    }

    /// <summary>
    /// Opens a live category view while retaining the primary view independently of its close-up field.
    /// </summary>
    /// <param name="category">The object category from zero through seven, or fighter category from eight through eleven.</param>
    /// <param name="focus">Resolves the current category's presentation objects, or null when it no longer has a primary.</param>
    public void OpenCategory(int category, Func<TacticalCombatCameraFocus?> focus)
    {
        if ((uint)category >= 12)
            throw new ArgumentOutOfRangeException(nameof(category));
        _categoryFocus = focus ?? throw new ArgumentNullException(nameof(focus));
        Category = category;
        _categoryField = 0.02f;
        _categoryObjectIndex = null;
        _saved ??= new Snapshot(this);
    }

    /// <summary>
    /// Computes the camera transform, keeping tracking separate from its orbital position.
    /// </summary>
    /// <param name="origin">The battle scene's world origin.</param>
    /// <param name="position">Receives the camera's world position.</param>
    /// <param name="rotation">Receives the camera's world orientation.</param>
    public void GetPose(Vector3 origin, out Vector3 position, out Quaternion rotation)
    {
        if (IsCategoryView)
        {
            TacticalCombatCameraFocus? focus = _categoryFocus();
            if (focus.HasValue)
            {
                GetCategoryPose(focus.Value, out position, out rotation);
                _lastRotation = rotation;
                return;
            }
            CloseCategory();
        }
        if (Yaw > 180)
            Yaw = unchecked(Yaw - 360);
        if (Yaw < -180)
            Yaw = unchecked(Yaw + 360);
        rotation = Quaternion.Euler(Pitch, Yaw, 0);
        position = origin - rotation * Vector3.forward * _distance;
        if (_focus != null)
            rotation = LookAt(position, _focus(), rotation);
        _lastRotation = rotation;
    }

    /// <summary>
    /// Positions the close-up in its primary object's frame and retains the previous camera up direction when aiming.
    /// </summary>
    /// <param name="focus">The current category presentation references.</param>
    /// <param name="position">Receives the world position.</param>
    /// <param name="rotation">Receives the world orientation.</param>
    private void GetCategoryPose(
        TacticalCombatCameraFocus focus,
        out Vector3 position,
        out Quaternion rotation
    )
    {
        _categoryObjectIndex = focus.ObjectIndex;
        float span = focus.Span;
        Vector3 offset = new(-span, span, span + span);
        if (focus.SecondaryPosition.HasValue)
        {
            Vector3 relative =
                Quaternion.Inverse(focus.Rotation)
                * (focus.SecondaryPosition.Value - focus.Position);
            double distance = Math.Sqrt(
                (double)relative.y * relative.y + (double)relative.x * relative.x
            );
            if (0.001 < distance)
            {
                offset.x = (float)((double)relative.x * span / distance);
                offset.y = (float)((double)relative.y * span / distance);
            }
        }
        position = focus.Position + focus.Rotation * offset;
        rotation = focus.SecondaryPosition.HasValue
            ? LookAt(position, focus.SecondaryPosition.Value, _lastRotation)
            : focus.Rotation * Quaternion.LookRotation(Vector3.back, Vector3.up);
    }

    /// <summary>
    /// Reorients the camera while preserving its existing orientation for a coincident destination.
    /// </summary>
    /// <param name="position">The camera position.</param>
    /// <param name="destination">The addressed object's position.</param>
    /// <param name="previous">The camera's current orientation.</param>
    /// <returns>The updated orientation, or the existing orientation for a degenerate direction.</returns>
    private static Quaternion LookAt(Vector3 position, Vector3 destination, Quaternion previous)
    {
        Vector3 direction = Normalize(destination - position);
        if (Vector3.Dot(direction, direction) < 0.5f)
            return previous;
        Vector3 up = Normalize(previous * Vector3.up);
        if (Mathf.Abs(Vector3.Dot(up, direction)) > 0.999f)
            up = Vector3.right;
        Vector3 right = Normalize(Vector3.Cross(up, direction));
        if (Vector3.Dot(right, right) < 0.5f)
            return previous;
        return Quaternion.LookRotation(direction, Vector3.Cross(direction, right));
    }

    /// <summary>
    /// Normalizes a camera-frame vector using its explicit zero-length boundary.
    /// </summary>
    /// <param name="value">The vector to normalize.</param>
    /// <returns>The normalized vector, or zero below the camera frame's length boundary.</returns>
    private static Vector3 Normalize(Vector3 value)
    {
        float length = Mathf.Sqrt(Vector3.Dot(value, value));
        return length > 1e-12f ? value / length : Vector3.zero;
    }

    /// <summary>
    /// Changes the field and the angular increment together while retaining the full zoom-level counter.
    /// </summary>
    /// <param name="inward">Whether to narrow the viewport field.</param>
    private void Zoom(bool inward)
    {
        double scaled = _primaryField * (inward ? 0.9 : 1.1);
        _primaryField = (float)scaled;
        if (inward && !(scaled >= 0.005f))
            _primaryField = 0.005f;
        else if (!inward && scaled > 1.5f)
            _primaryField = 1.5f;
        ZoomLevel = inward ? unchecked(ZoomLevel - 1) : unchecked(ZoomLevel + 1);
        AngularStep = unchecked((int)ZoomLevel) < 1 ? 1 : (int)Math.Min(ZoomLevel, 5);
    }

    /// <summary>
    /// Changes only the category field, with its independent close-up bounds.
    /// </summary>
    /// <param name="inward">Whether to narrow the close-up field.</param>
    private void ZoomCategory(bool inward)
    {
        double scaled = _categoryField * (inward ? 0.9 : 1.1);
        _categoryField = (float)scaled;
        if (inward && !(scaled >= 0.005f))
            _categoryField = 0.005f;
        else if (!inward && scaled > 0.2f)
            _categoryField = 0.2f;
    }

    /// <summary>
    /// Releases the transient category view without restoring a different primary snapshot.
    /// </summary>
    private void CloseCategory()
    {
        _categoryFocus = null;
        _categoryObjectIndex = null;
        Category = null;
    }

    /// <summary>
    /// Restores a saved primary view, or resets its authored angles and field before the first save.
    /// </summary>
    private void Restore()
    {
        CloseCategory();
        if (!_saved.HasValue)
        {
            Pitch = 30;
            Yaw = _defaultYaw;
            _primaryField = 0.2f;
            return;
        }
        Snapshot saved = _saved.Value;
        Pitch = saved.Pitch;
        Yaw = saved.Yaw;
        _primaryField = saved.Field;
        ZoomLevel = saved.ZoomLevel;
        AngularStep = saved.AngularStep;
        _focus = saved.Focus;
        _trackedObjectIndex = saved.TrackedObjectIndex;
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
            Field = camera._primaryField;
            ZoomLevel = camera.ZoomLevel;
            AngularStep = camera.AngularStep;
            Focus = camera._focus;
            TrackedObjectIndex = camera._trackedObjectIndex;
        }
    }
}
