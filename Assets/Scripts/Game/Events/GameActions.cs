using System;
using System.Collections.Generic;
using System.Linq;
using Rebellion.Core.Simulation;
using Rebellion.Game;
using Rebellion.SceneGraph;
using Rebellion.Util.Attributes;

[PersistableObject(Name = "RandomOutcome")]
public class RandomOutcomeAction : GameAction
{
    public List<GameAction> Actions { get; set; } = new List<GameAction>();

    [PersistableIgnore]
    private IRandomNumberProvider provider;

    /// <summary>
    /// Default constructor used for deserialization.
    /// </summary>
    public RandomOutcomeAction()
        : base() { }

    /// <summary>
    /// Initializes a new instance with injected random provider.
    /// </summary>
    /// <param name="provider">Random number provider for action selection.</param>
    public RandomOutcomeAction(IRandomNumberProvider provider)
        : base()
    {
        this.provider = provider;
    }

    /// <summary>
    /// Sets the random number provider. Must be called after deserialization.
    /// </summary>
    /// <param name="provider">Random number provider for action selection.</param>
    public void SetRandomProvider(IRandomNumberProvider provider)
    {
        this.provider = provider;
    }

    public override void Execute(GameRoot game)
    {
        if (provider == null)
        {
            throw new InvalidOperationException(
                "RandomProvider must be set before Execute is called. Call SetRandomProvider() after deserialization."
            );
        }

        double probability = Convert.ToDouble(this.GetActionValue());

        // Execute a random action.
        if (provider.NextDouble() < probability)
        {
            Actions[provider.NextInt(0, Actions.Count)].Execute(game);
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

    public override void Execute(GameRoot game)
    {
        // @TODO: Implement
    }
}

[PersistableObject(Name = "TriggerEvent")]
public class TriggerEventAction : GameAction
{
    public string EventInstanceID { get; set; }

    [PersistableIgnore]
    private IRandomNumberProvider provider;

    /// <summary>
    /// Default constructor used for deserialization.
    /// </summary>
    public TriggerEventAction()
        : base() { }

    /// <summary>
    /// Sets the random number provider. Must be called before Execute.
    /// </summary>
    public void SetRandomProvider(IRandomNumberProvider provider)
    {
        this.provider = provider;
    }

    public override void Execute(GameRoot game)
    {
        GameEvent gameEvent = game.GetEventByInstanceID(EventInstanceID);

        // Use the provider that was set by GameEvent.Execute
        // If not set (shouldn't happen in normal flow), create a local one
        var eventProvider = provider ?? new SystemRandomProvider(new Random().Next());
        gameEvent.Execute(game, eventProvider);
    }
}
