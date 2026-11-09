using Rebellion.Game.Units;
using Point = System.Drawing.Point;

/// <summary>
/// Constructs movement fixtures through the same transitions used by live journeys.
/// </summary>
public static class TestMovement
{
    /// <summary>
    /// Creates a movement fixture with explicit timing, context, and optional map positions.
    /// </summary>
    /// <param name="transitTicks">The running leg's duration.</param>
    /// <param name="ticksElapsed">The elapsed time to arrange.</param>
    /// <param name="originPosition">The departure position.</param>
    /// <param name="currentPosition">The current position, when relevant to the test.</param>
    /// <param name="movementGroupID">The travelling group.</param>
    /// <param name="sourceEventInstanceID">The originating event.</param>
    /// <param name="resolveEncounterOnArrival">Whether arrival checks personnel encounters.</param>
    /// <param name="previousLocationInstanceID">The previously visited container.</param>
    /// <param name="destinationPlanetInstanceID">The running leg's endpoint.</param>
    /// <param name="isObservation">Whether the fixture represents observed intelligence.</param>
    /// <param name="observedAtTick">The observation time, if known.</param>
    /// <returns>The arranged journey.</returns>
    public static MovementState Create(
        int transitTicks = 0,
        int ticksElapsed = 0,
        Point? originPosition = null,
        Point? currentPosition = null,
        string movementGroupID = null,
        string sourceEventInstanceID = null,
        bool resolveEncounterOnArrival = false,
        string previousLocationInstanceID = null,
        string destinationPlanetInstanceID = null,
        bool isObservation = false,
        int? observedAtTick = null
    )
    {
        MovementState movement = new MovementState(
            new MovementLeg(
                transitTicks,
                originPosition ?? Point.Empty,
                destinationPlanetInstanceID
            ),
            previousLocationInstanceID,
            movementGroupID,
            sourceEventInstanceID,
            resolveEncounterOnArrival
        );
        AdvanceTo(movement, ticksElapsed);
        movement.SetPosition(currentPosition ?? Point.Empty);
        if (isObservation)
            movement.MarkObserved(observedAtTick);
        return movement;
    }

    /// <summary>
    /// Advances a fixture's clock while preserving its independently arranged map position.
    /// </summary>
    /// <param name="movement">The journey to advance.</param>
    /// <param name="ticksElapsed">The elapsed time to arrange.</param>
    public static void AdvanceTo(MovementState movement, int ticksElapsed)
    {
        Point position = movement.CurrentPosition;
        movement.Advance(ticksElapsed - movement.TicksElapsed, position);
        movement.SetPosition(position);
    }

    /// <summary>
    /// Creates a test journey with a new duration from its current position, retaining its context.
    /// </summary>
    /// <param name="movement">The journey being arranged.</param>
    /// <param name="transitTicks">The replacement duration.</param>
    /// <returns>The arranged movement state.</returns>
    public static MovementState WithDuration(MovementState movement, int transitTicks)
    {
        MovementState arranged = Create(
            transitTicks: transitTicks,
            originPosition: movement.CurrentPosition,
            currentPosition: movement.CurrentPosition,
            movementGroupID: movement.MovementGroupID,
            sourceEventInstanceID: movement.SourceEventInstanceID,
            resolveEncounterOnArrival: movement.ResolveEncounterOnArrival,
            previousLocationInstanceID: movement.PreviousLocationInstanceID,
            destinationPlanetInstanceID: movement.DestinationPlanetInstanceID,
            isObservation: movement.IsObservation,
            observedAtTick: movement.ObservedAtTick
        );
        if (movement.NextLeg != null)
            arranged.Redirect(
                movement.NextLeg.CreateCopy(),
                movement.DestinationPlanetInstanceID,
                movement.PreviousLocationInstanceID
            );
        return arranged;
    }
}
