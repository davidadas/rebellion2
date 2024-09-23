using System.Collections.Generic;
using System.Linq;
using System;

/// <summary>
/// Manager for handling missions in the game.
/// This includes scheduling missions and rescheduling mission events.
/// </summary>
public class MissionManager
{
    protected EventManager eventManager;

    /// <summary>
    /// Initializes a new instance of the <see cref="MissionManager"/> class.
    /// </summary>
    /// <param name="eventManager">The event manager.</param>
    public MissionManager(EventManager eventManager)
    {
        this.eventManager = eventManager;
        InitializeMissions();
    }

    /// <summary>
    /// Starts a mission by generating a random tick for the mission and scheduling a mission event.
    /// </summary>
    /// <param name="mission">The mission to start.</param>
    private void InitializeMissions()
    {
        // Register event handlers for mission events.
        List<MissionEvent> missionEvents = eventManager
            .GetEventsByType<GameEvent>()
            .OfType<MissionEvent>()
            .ToList();
        
        // Register event handlers for mission events.
        foreach (MissionEvent missionEvent in missionEvents)
        {
            missionEvent.OnEventTriggered += RescheduleEvent;
        }
    }

    /// <summary>
    /// Starts a mission by generating a random tick for the mission and scheduling a mission event.
    /// </summary>
    /// <param name="mission">The mission to start.</param>
    public void StartMission(Mission mission)
    {
        // Generate a random tick for the mission.
        int tick = new Random().Next(mission.MinTicks, mission.MaxTicks + 1);
        MissionEvent missionEvent = new MissionEvent(tick, mission);

        // Schedule next mission event.
        eventManager.ScheduleEvent(missionEvent);
    }

    /// <summary>
    /// Reschedules a mission event (when the mission is repeatable).
    /// </summary>
    /// <param name="gameEvent">The game event to reschedule.</param>
    protected void RescheduleEvent(GameEvent gameEvent)
    {
        MissionEvent missionEvent = gameEvent as MissionEvent;
        if (missionEvent != null)
        {
            // Reschedule the mission event.
            eventManager.ScheduleEvent(missionEvent);
        }
    }
}
