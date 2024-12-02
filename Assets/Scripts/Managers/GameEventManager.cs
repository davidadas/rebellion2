using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Manages game events and their scheduling.
/// </summary>
public class GameEventManager
{
    private Game game;

    public GameEventManager(Game game)
    {
        this.game = game;
    }

    private void ProcessEvent(GameEvent gameEvent)
    {
        if (gameEvent.AreConditionsMet(game))
        {
            GameLogger.Log($"Executing game event: {gameEvent.GetDisplayName()}");
            gameEvent.Execute(game);
            game.AddCompletedEvent(gameEvent);
        }
    }

    /// <summary>
    ///
    /// </summary>
    /// <param name="gameEvenst"></param>
    public void ProcessEvents(List<GameEvent> gameEvents)
    {
        List<GameEvent> eventsToRemove = new List<GameEvent>();

        foreach (GameEvent gameEvent in gameEvents)
        {
            ProcessEvent(gameEvent);

            if (!gameEvent.IsRepeatable)
            {
                if (!gameEvent.IsRepeatable)
                {
                    eventsToRemove.Add(gameEvent);
                }
            }
        }

        // Remove events that are no longer needed.
        foreach (GameEvent eventToRemove in eventsToRemove)
        {
            game.RemoveEvent(eventToRemove);
        }
    }
}
