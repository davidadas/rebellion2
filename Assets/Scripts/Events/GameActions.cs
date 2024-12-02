using System;
using System.Collections.Generic;
using System.Linq;

[PersistableObject(Name = "MoveUnits")]
public class MoveUnitsAction : GameAction
{
    public List<string> UnitInstanceIDs { get; set; } = new List<string>();
    public string TargetInstanceID { get; set; }

    /// <summary>
    /// Default constructor used for deserialization.
    /// </summary>
    public MoveUnitsAction()
        : base() { }

    public override void Execute(Game game)
    {
        // Get the parameters for the action.
        IMovable movable = game.GetSceneNodeByInstanceID<IMovable>(UnitInstanceIDs[0]) as IMovable;
        ISceneNode target = game.GetSceneNodeByInstanceID<ISceneNode>(TargetInstanceID);

        movable.MoveTo(target);
    }
}

[PersistableObject(Name = "RandomOutcome")]
public class RandomOutcomeAction : GameAction
{
    public List<GameAction> Actions { get; set; } = new List<GameAction>();
    private Random random = new Random();

    /// <summary>
    /// Default constructor used for deserialization.
    /// </summary>
    public RandomOutcomeAction()
        : base() { }

    public override void Execute(Game game)
    {
        double probability = Convert.ToDouble(this.GetActionValue());

        // Execute a random action.
        if (random.NextDouble() < probability)
        {
            Actions[random.Next(Actions.Count)].Execute(game);
        }
    }
}

[PersistableObject(Name = "TriggerDuel")]
public class TriggerDuelAction : GameAction
{
    public List<string> AttackerInstanceIDs { get; set; }
    public List<string> DefenderInstanceIDs { get; set; }

    /// <summary>
    /// Default constructor used for deserialization.
    /// </summary>
    public TriggerDuelAction()
        : base() { }

    public override void Execute(Game game)
    {
        // @TODO: Implement
    }
}

[PersistableObject(Name = "TriggerEvent")]
public class TriggerEventAction : GameAction
{
    public string EventInstanceID { get; set; }

    /// <summary>
    /// Default constructor used for deserialization.
    /// </summary>
    public TriggerEventAction()
        : base() { }

    public override void Execute(Game game)
    {
        GameEvent gameEvent = game.GetEventByInstanceID(EventInstanceID);

        gameEvent.Execute(game);
    }
}
