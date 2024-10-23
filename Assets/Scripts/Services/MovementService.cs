using System;
using System.Collections.Generic;

/// <summary>
/// A specialized class for calculating travel time between two planets.
/// </summary>
internal class SpaceTravelCalculator
{
    // @TODO: Move these to a configuration file.
    const int DISTANCE_DIVISOR = 5;
    const int DISTANCE_BASE = 100;

    /// <summary>
    /// Calculate the travel time between two planets using a binary search approximation
    /// of the Euclidean distance. The travel time is calculated in ticks.
    /// </summary>
    /// <param name="planetA">The first planet.</param>
    /// <param name="planetB">The second planet.</param>
    /// <returns>Travel time in ticks.</returns>
    public static int CalculateTravelTime(Planet planetA, Planet planetB)
    {
        // Calculate squared Euclidean distance.
        int deltaX = planetA.PositionX - planetB.PositionX;
        int deltaY = planetA.PositionY - planetB.PositionY;
        int distanceSquared = deltaX * deltaX + deltaY * deltaY;

        // Calculate travel time based on the Euclidean distance.
        int distance = CalculateEuclideanDistance(distanceSquared);

        // Calculate travel time in days based on the distance and predefined constants.
        return (distance / DISTANCE_DIVISOR * DISTANCE_BASE) / 100;
    }

    /// <summary>
    /// A helper function to approximate the Euclidean distance using a binary search.
    /// </summary>
    /// <param name="targetSquare">The square of the distance to calculate the Euclidean distance from.</param>
    /// <returns>The approximate Euclidean distance.</returns>
    private static int CalculateEuclideanDistance(int targetSquare)
    {
        // Special case for small distances.
        if (targetSquare <= 3)
        {
            return targetSquare > 0 ? 1 : 0;
        }

        // Start with upper and lower bounds for binary search.
        int upperBound = 2;
        while (upperBound * upperBound < targetSquare)
        {
            upperBound *= 2;
        }

        int lowerBound = upperBound / 2;
        int midPoint, squareOfMidPoint;

        // Perform binary search to converge on the Euclidean distance.
        do
        {
            midPoint = (lowerBound + upperBound) / 2;
            squareOfMidPoint = midPoint * midPoint;

            if (squareOfMidPoint < targetSquare)
            {
                lowerBound = midPoint;
            }
            else
            {
                upperBound = midPoint;
            }
        }
        while (midPoint != upperBound);

        return midPoint;
    }
}

public interface IMovementService
{
    public void MoveUnits(List<string> unitInstanceIds, string targetInstanceId);
}

/// <summary>
/// 
/// </summary>
public class MovementService : IMovementService
{
    private ILookupService lookupService;
    private IEventService eventService;
    private Game game;

    /// <summary>
    /// 
    /// </summary>
    /// <param name="game"></param>
    public MovementService(ILookupService lookupService, IEventService eventService, Game game)
    {
        this.lookupService = lookupService;
        this.eventService = eventService;
        this.game = game;
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="unit"></param>
    /// <param name="target"></param>
    /// <returns></returns>
    public int GetTravelTime(SceneNode unit, SceneNode target)
    {
        Planet currentUnitLocation = unit.GetClosestParentOfType<Planet>();
        Planet targetLocation = target.GetClosestParentOfType<Planet>();

        return SpaceTravelCalculator.CalculateTravelTime(currentUnitLocation, targetLocation);
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="unitInstanceIds"></param>
    /// <param name="targetInstanceId"></param>
    public void MoveUnits(List<string> unitInstanceIds, string targetInstanceId)
    {
        List<SceneNode> units = lookupService.GetSceneNodesByInstanceIDs(unitInstanceIds);
        SceneNode target = lookupService.GetSceneNodeByInstanceID<SceneNode>(targetInstanceId);

        // Calculate the travel time for the first unit.
        int travelTime = GetTravelTime(units[0], target);

        // Create a new movement event.
        GameEvent movementEvent = new GameEvent()
        {
            IsRepeatable = false,
            Conditionals = new List<GameConditional>(),
            Actions = new List<GameAction>()
            {
                new MoveUnitsAction(units, target)
            }
        };

        // Schedule the movement event.
        eventService.ScheduleEvent(movementEvent, game.CurrentTick + travelTime);
    }
}
