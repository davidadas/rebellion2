using System;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Applies the tactical camera's source-defined orbit, zoom, memory, and subject controls.
/// </summary>
public sealed class TacticalCameraRig : MonoBehaviour
{
    private const float _defaultPitch = 30f;
    private const float _defaultZoom = 0.2f;
    private const float _minimumZoom = 0.05f;
    private const float _maximumZoom = 0.8f;
    private const float _zoomInMultiplier = 0.8f;
    private const float _zoomOutMultiplier = 1.25f;
    private const float _zoomDistanceScale = 1200f;

    [SerializeField]
    private Camera battleCamera;

    [SerializeField]
    private Button[] controls = Array.Empty<Button>();

    private CameraState current;
    private CameraState defaults;
    private CameraState remembered;
    private bool hasRememberedView;
    private int zoomLevel = 3;
    private int adjustmentStep = 3;
    private Vector3 selectedSubject;

    /// <summary>
    /// Supplies the generated tactical camera and its nine source-ordered controls.
    /// </summary>
    /// <param name="camera">The camera positioned by this rig.</param>
    /// <param name="cameraControls">The controls ordered from zoom-in through reset-subject.</param>
    public void Configure(Camera camera, Button[] cameraControls)
    {
        battleCamera = camera ?? throw new ArgumentNullException(nameof(camera));
        controls = cameraControls ?? throw new ArgumentNullException(nameof(cameraControls));
    }

    /// <summary>
    /// Establishes the faction's default tactical view.
    /// </summary>
    /// <param name="initialYaw">The faction-specific starting yaw in degrees.</param>
    public void Initialize(float initialYaw)
    {
        defaults = new CameraState(Vector3.zero, _defaultPitch, initialYaw, _defaultZoom);
        current = defaults;
        selectedSubject = defaults.Subject;
        hasRememberedView = false;
        zoomLevel = 3;
        adjustmentStep = 3;
        ApplyCurrentState();
    }

    /// <summary>
    /// Updates the subject used by the reset-subject control without moving the current view.
    /// </summary>
    /// <param name="subject">The selected tactical subject's world position.</param>
    public void SetSelectedSubject(Vector3 subject)
    {
        selectedSubject = subject;
    }

    /// <summary>
    /// Centers the active view immediately on a tactical subject.
    /// </summary>
    /// <param name="subject">The tactical subject's world position.</param>
    public void FocusSubject(Vector3 subject)
    {
        selectedSubject = subject;
        current.Subject = subject;
        ApplyCurrentState();
    }

    /// <summary>
    /// Executes one source-defined camera command.
    /// </summary>
    /// <param name="command">The camera command to execute.</param>
    public void Execute(TacticalCameraCommand command)
    {
        switch (command)
        {
            case TacticalCameraCommand.ZoomIn:
                ZoomIn();
                break;
            case TacticalCameraCommand.ZoomOut:
                ZoomOut();
                break;
            case TacticalCameraCommand.RotateLeft:
                RotateLeft();
                break;
            case TacticalCameraCommand.RotateRight:
                RotateRight();
                break;
            case TacticalCameraCommand.TiltUp:
                TiltUp();
                break;
            case TacticalCameraCommand.TiltDown:
                TiltDown();
                break;
            case TacticalCameraCommand.RememberView:
                RememberView();
                break;
            case TacticalCameraCommand.ResetView:
                ResetView();
                break;
            case TacticalCameraCommand.ResetSubject:
                ResetSubject();
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(command));
        }
    }

    /// <summary>
    /// Verifies and connects the generated tactical camera controls.
    /// </summary>
    private void Awake()
    {
        if (battleCamera == null)
            throw new MissingReferenceException("The tactical camera rig requires a camera.");
        if (controls?.Length != 9)
            throw new MissingReferenceException(
                "The tactical camera rig requires nine source-ordered controls."
            );

        controls[0].onClick.AddListener(ZoomIn);
        controls[1].onClick.AddListener(ZoomOut);
        controls[2].onClick.AddListener(RotateLeft);
        controls[3].onClick.AddListener(RotateRight);
        controls[4].onClick.AddListener(TiltUp);
        controls[5].onClick.AddListener(TiltDown);
        controls[6].onClick.AddListener(RememberView);
        controls[7].onClick.AddListener(ResetView);
        controls[8].onClick.AddListener(ResetSubject);
    }

    /// <summary>
    /// Moves one source zoom level toward the subject.
    /// </summary>
    private void ZoomIn()
    {
        current.Zoom = Mathf.Max(_minimumZoom, current.Zoom * _zoomInMultiplier);
        SetZoomLevel(zoomLevel - 1);
        ApplyCurrentState();
    }

    /// <summary>
    /// Moves one source zoom level away from the subject.
    /// </summary>
    private void ZoomOut()
    {
        current.Zoom = Mathf.Min(_maximumZoom, current.Zoom * _zoomOutMultiplier);
        SetZoomLevel(zoomLevel + 1);
        ApplyCurrentState();
    }

    /// <summary>
    /// Rotates the view left by the current zoom-derived adjustment step.
    /// </summary>
    private void RotateLeft()
    {
        current.Yaw -= adjustmentStep;
        ApplyCurrentState();
    }

    /// <summary>
    /// Rotates the view right by the current zoom-derived adjustment step.
    /// </summary>
    private void RotateRight()
    {
        current.Yaw += adjustmentStep;
        ApplyCurrentState();
    }

    /// <summary>
    /// Tilts the view upward without crossing the source's vertical limit.
    /// </summary>
    private void TiltUp()
    {
        current.Pitch = Mathf.Min(90f, current.Pitch + adjustmentStep);
        ApplyCurrentState();
    }

    /// <summary>
    /// Tilts the view downward without crossing the source's vertical limit.
    /// </summary>
    private void TiltDown()
    {
        current.Pitch = Mathf.Max(-90f, current.Pitch - adjustmentStep);
        ApplyCurrentState();
    }

    /// <summary>
    /// Stores the current complete view for the reset-view control.
    /// </summary>
    private void RememberView()
    {
        remembered = current;
        hasRememberedView = true;
    }

    /// <summary>
    /// Restores the remembered view, or the faction default when none has been stored.
    /// </summary>
    private void ResetView()
    {
        current = hasRememberedView ? remembered : defaults;
        ApplyCurrentState();
    }

    /// <summary>
    /// Re-centers the current orbit on the selected tactical subject.
    /// </summary>
    private void ResetSubject()
    {
        current.Subject = selectedSubject;
        ApplyCurrentState();
    }

    /// <summary>
    /// Clamps the zoom level and derives the rotation and tilt adjustment step.
    /// </summary>
    /// <param name="level">The unbounded source zoom level.</param>
    private void SetZoomLevel(int level)
    {
        zoomLevel = level;
        adjustmentStep = Mathf.Clamp(level, 1, 5);
    }

    /// <summary>
    /// Projects the logical source camera state into a Unity orbit transform.
    /// </summary>
    private void ApplyCurrentState()
    {
        Quaternion rotation = Quaternion.Euler(current.Pitch, current.Yaw, 0f);
        battleCamera.transform.SetPositionAndRotation(
            current.Subject - rotation * Vector3.forward * (current.Zoom * _zoomDistanceScale),
            rotation
        );
    }

    private struct CameraState
    {
        public Vector3 Subject;
        public float Pitch;
        public float Yaw;
        public float Zoom;

        public CameraState(Vector3 subject, float pitch, float yaw, float zoom)
        {
            Subject = subject;
            Pitch = pitch;
            Yaw = yaw;
            Zoom = zoom;
        }
    }
}
