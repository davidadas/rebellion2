using System;
using System.Collections.Generic;
using System.Linq;

public class MoveUnitsAction : GameAction
{
    /// <summary>
    /// Default constructor used for serialization.
    /// </summary>
    public MoveUnitsAction()
        : base() { }

    public MoveUnitsAction(List<SceneNode> nodes, SceneNode target)
        : base(
            new Dictionary<string, object>
            {
                { "UnitInstanceIDs", nodes.Select(n => n.InstanceID).ToList() },
                { "TargetInstanceID", target.InstanceID },
            }
        ) { }

    public MoveUnitsAction(Dictionary<string, object> parameters)
        : base(parameters) { }

    public override void Execute(Game game)
    {
        // Get the parameters for the action.
        List<string> unitInstanceIds = (List<string>)Parameters["UnitInstanceIDs"];
        string targetInstanceId = (string)Parameters["TargetInstanceID"];

        IMovable movable = game.GetSceneNodeByInstanceID<IMovable>(unitInstanceIds[0]) as IMovable;
        SceneNode target = game.GetSceneNodeByInstanceID<SceneNode>(targetInstanceId);

        movable.MoveTo(target);
    }
}

public class RandomOutcomeAction : GameAction
{
    private Random random = new Random();

    /// <summary>
    /// Default constructor used for serialization.
    /// </summary>
    public RandomOutcomeAction()
        : base() { }

    public RandomOutcomeAction(Dictionary<string, object> parameters)
        : base(parameters) { }

    public override void Execute(Game game)
    {
        double probability = Convert.ToDouble(Value);

        // Get the parameters for the action.
        List<GameAction> actions = (List<GameAction>)Parameters["Actions"];

        // Execute a random action.
        if (random.NextDouble() < probability)
        {
            actions[random.Next(actions.Count)].Execute(game);
        }
    }
}

public class TriggerEventAction : GameAction
{
    /// <summary>
    /// Default constructor used for serialization.
    /// </summary>
    public TriggerEventAction()
        : base() { }

    public TriggerEventAction(Dictionary<string, object> parameters)
        : base(parameters) { }

    public override void Execute(Game game)
    {
        // Get the parameters for the action.
        string eventId = (string)Parameters["EventID"];

        GameEvent gameEvent = game.GetPoolEventByID(eventId);

        gameEvent.Execute(game);
    }
}
