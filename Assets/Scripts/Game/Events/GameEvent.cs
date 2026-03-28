using System.Collections.Generic;
using Rebellion.Core.Simulation;
using Rebellion.Game;
using Rebellion.Game.Results;
using Rebellion.SceneGraph;

/// <summary>
/// Represents a triggered game event: a set of conditions that, when met, execute a set of actions.
/// Execute returns the results of those actions for notification and logging.
/// </summary>
public class GameEvent : BaseGameEntity
{
    public bool IsRepeatable { get; set; }
    public List<GameConditional> Conditionals { get; set; } = new List<GameConditional>();
    public List<GameAction> Actions { get; set; } = new List<GameAction>();

    public GameEvent() { }

    public GameEvent(List<GameConditional> conditionals, List<GameAction> actions)
    {
        Conditionals = conditionals;
        Actions = actions;
    }

    /// <summary>
    /// Returns true if all conditions are met.
    /// </summary>
    public bool AreConditionsMet(GameRoot game)
    {
        foreach (GameConditional conditional in Conditionals)
        {
            if (!conditional.IsMet(game))
                return false;
        }
        return true;
    }

    /// <summary>
    /// Executes the event's actions and returns all results.
    /// </summary>
    public List<GameResult> Execute(GameRoot game, IRandomNumberProvider provider)
    {
        List<GameResult> results = new List<GameResult>();

        foreach (GameAction action in Actions)
        {
            if (action is RandomOutcomeAction randomAction)
                randomAction.SetRandomProvider(provider);
            else if (action is TriggerEventAction triggerAction)
                triggerAction.SetRandomProvider(provider);

            results.AddRange(action.Execute(game));
        }

        return results;
    }
}
