/// <summary>
/// Identifies one command accepted by the tactical camera controls.
/// </summary>
public enum TacticalCameraCommand
{
    /// <summary>Moves the camera toward its subject.</summary>
    ZoomIn,

    /// <summary>Moves the camera away from its subject.</summary>
    ZoomOut,

    /// <summary>Rotates the camera left around its subject.</summary>
    RotateLeft,

    /// <summary>Rotates the camera right around its subject.</summary>
    RotateRight,

    /// <summary>Tilts the camera upward around its subject.</summary>
    TiltUp,

    /// <summary>Tilts the camera downward around its subject.</summary>
    TiltDown,

    /// <summary>Stores the current camera view.</summary>
    RememberView,

    /// <summary>Restores the stored or default camera view.</summary>
    ResetView,

    /// <summary>Centers the camera on the selected subject.</summary>
    ResetSubject,
}
