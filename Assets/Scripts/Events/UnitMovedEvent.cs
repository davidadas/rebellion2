/// <summary>
/// Represents an event that occurs when a unit is moved.
/// </summary>
public class UnitMovedEvent : GameEvent
{
    public string UnitInstanceID { get; private set; }
    public string DestinationInstanceID { get; private set; }
    
    /// <summary>
    /// Initializes a new instance of the UnitMovedEvent class.
    /// </summary>
    /// <param name="scheduledTick">The tick at which the event is scheduled to occur.</param>
    /// <param name="unitInstanceId">The instance ID of the unit that is moved.</param>
    /// <param name="destinationInstanceId">The instance ID of the destination to which the unit is moved.</param>
    public UnitMovedEvent(int scheduledTick, string unitInstanceId, string destinationInstanceId) : base(scheduledTick)
    {
        UnitInstanceID = unitInstanceId;
        DestinationInstanceID = destinationInstanceId;
    }

    /// <summary>
    /// Moves the unit to the destination. If the destination is not accessible, 
    /// the unit is returned to the nearest location.
    /// </summary>
    /// <game>The game in which the event occurs.</game>
    protected override void TriggerEvent(Game game)
    {
        SceneNode unit = game.GetSceneNodeByInstanceID(UnitInstanceID);
        SceneNode destination = game.GetSceneNodeByInstanceID(DestinationInstanceID);

        // Move the unit to the destination.
        try
        {
            // Move the unit to the destination.
            if (unit.GetParent() != destination)
            {
                game.MoveNode(unit, destination);
            }
            // Update the unit's movement status.
            if (unit is IMovable movableUnit)
            {
                movableUnit.MovementStatus = MovementStatus.Stationary;
            }
        }
        catch (SceneAccessException ex)
        {
            // @TODO: Return unit to nearest location.
        }
    }
}
