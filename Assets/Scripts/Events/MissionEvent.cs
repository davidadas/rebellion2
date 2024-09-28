/// <summary>
/// 
/// </summary>
public class MissionEvent : GameEvent
{
    public Mission Mission { get; private set; }
    
    /// <summary>
    /// Default constructor used for serialization.
    /// </summary>
    public MissionEvent() { }

    /// <summary>
    /// Initializes a <see cref="MissionEvent"/> with the specified parameters. 
    /// </summary>
    /// <param name="scheduledTick">The tick at which the event is scheduled to occur.</param>
    /// <param name="mission">The mission that is being executed.</param>
    public MissionEvent(int scheduledTick, Mission mission) : base(scheduledTick)
    {
        Mission = mission;
    }

    /// <summary>
    /// Executes the mission.
    /// </summary>
    /// <param name="game"></param>
    protected override void TriggerEvent(Game game)
    {
        // Execute the mission.
        Mission.Perform(game);
    }
}
