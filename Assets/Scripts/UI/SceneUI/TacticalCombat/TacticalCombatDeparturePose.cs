using System.Numerics;

/// <summary>
/// Calculates the temporary departure transform independently from a unit's simulation position.
/// </summary>
public readonly struct TacticalCombatDeparturePose
{
    private static readonly float[] _coefficients =
    {
        CalculateCoefficient(400, 0),
        CalculateCoefficient(575, 250),
        CalculateCoefficient(650, 75),
        CalculateCoefficient(325, 275),
    };

    public Vector3 Position { get; }
    public Vector3 Heading { get; }

    /// <summary>
    /// Captures one departure transform without changing the participant that supplied it.
    /// </summary>
    /// <param name="position">The temporary presentation position.</param>
    /// <param name="heading">The temporary presentation heading.</param>
    private TacticalCombatDeparturePose(Vector3 position, Vector3 heading)
    {
        Position = position;
        Heading = heading;
    }

    /// <summary>
    /// Applies the registered participant's quadratic departure displacement along its side lane.
    /// The elapsed interval has an upper bound of four seconds and no lower bound.
    /// </summary>
    /// <param name="unitIndex">The encounter registration identifier.</param>
    /// <param name="presentationOrdinal">The authored presentation ordinal.</param>
    /// <param name="position">The unchanged simulation position.</param>
    /// <param name="sideLane">The departing side's captured forward lane.</param>
    /// <param name="elapsedMilliseconds">The departure timer already advanced by its encounter owner.</param>
    /// <returns>The temporary render position and opposite-lane heading.</returns>
    public static TacticalCombatDeparturePose Evaluate(
        int unitIndex,
        int presentationOrdinal,
        Vector3 position,
        Vector3 sideLane,
        float elapsedMilliseconds
    )
    {
        if (4000 <= elapsedMilliseconds)
            elapsedMilliseconds = 4000;
        float seconds = (float)((double)elapsedMilliseconds * (double)0.001f);
        int selector = unchecked(unitIndex + presentationOrdinal) & 3;
        float squaredSeconds = (float)((double)seconds * seconds);
        float distance = (float)((double)squaredSeconds * _coefficients[selector]);
        float displacement = (float)((double)sideLane.Z * -distance);
        position.Z = (float)((double)displacement + position.Z);
        return new TacticalCombatDeparturePose(position, -sideLane);
    }

    /// <summary>
    /// Computes the stored acceleration for one departure staggering interval.
    /// </summary>
    /// <param name="distance">The displacement numerator.</param>
    /// <param name="offsetMilliseconds">The staggering offset within the four-second interval.</param>
    /// <returns>The rounded acceleration coefficient.</returns>
    private static float CalculateCoefficient(float distance, float offsetMilliseconds)
    {
        double seconds = (4000d - offsetMilliseconds) * (double)0.001f;
        return (float)(distance / (seconds * seconds));
    }
}
