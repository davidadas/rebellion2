using UnityEngine;

/// <summary>
/// Continuously rotates the transform it is attached to. A generic presentation spin used by any
/// object that should idly rotate (menu medallions, difficulty icons, showcase models). Speed and
/// axis are editable in the Inspector or via <see cref="Configure"/>.
/// </summary>
public sealed class AutoRotate : MonoBehaviour
{
    [Tooltip("Spin speed in degrees per second. Negative reverses the direction.")]
    [SerializeField]
    private float degreesPerSecond = 45f;

    [Tooltip("Axis to spin around (Y = upright turn).")]
    [SerializeField]
    private Vector3 axis = Vector3.up;

    [Tooltip("Rotate in the object's local space rather than world space.")]
    [SerializeField]
    private bool localSpace = true;

    [Tooltip("Keep spinning while the game is paused (Time.timeScale = 0). Useful for menus.")]
    [SerializeField]
    private bool ignoreTimeScale = true;

    [Tooltip("Start at a random angle so several spinners are not in lockstep.")]
    [SerializeField]
    private bool randomizeStartAngle;

    /// <summary>
    /// Gets the configured spin speed in degrees per second.
    /// </summary>
    public float DegreesPerSecond => degreesPerSecond;

    /// <summary>
    /// Gets the configured spin axis.
    /// </summary>
    public Vector3 Axis => axis;

    /// <summary>
    /// Sets the spin speed and axis.
    /// </summary>
    /// <param name="speed">The spin speed in degrees per second (negative reverses direction).</param>
    /// <param name="spinAxis">The axis to spin around.</param>
    public void Configure(float speed, Vector3 spinAxis)
    {
        degreesPerSecond = speed;
        axis = spinAxis;
    }

    /// <summary>
    /// Applies an optional random starting angle so multiple spinners are not synchronized.
    /// </summary>
    private void Start()
    {
        if (randomizeStartAngle)
        {
            transform.Rotate(axis.normalized, Random.Range(0f, 360f), RotationSpace());
        }
    }

    /// <summary>
    /// Advances the rotation by one frame's worth of spin.
    /// </summary>
    private void Update()
    {
        float delta = ignoreTimeScale ? Time.unscaledDeltaTime : Time.deltaTime;
        transform.Rotate(axis.normalized, degreesPerSecond * delta, RotationSpace());
    }

    /// <summary>
    /// Resolves the configured rotation space.
    /// </summary>
    /// <returns>Local space when configured; otherwise world space.</returns>
    private Space RotationSpace()
    {
        return localSpace ? Space.Self : Space.World;
    }
}
