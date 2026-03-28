using System;
using System.Collections.Generic;
using Rebellion.Core.Simulation;
using Rebellion.Game;
using Rebellion.Game.Results;
using Rebellion.Util.Attributes;

[PersistableObject(Name = "RandomOutcome")]
public class RandomOutcomeAction : GameAction
{
    public List<GameAction> Actions { get; set; } = new List<GameAction>();

    [PersistableIgnore]
    private IRandomNumberProvider provider;

    public RandomOutcomeAction()
        : base() { }

    public RandomOutcomeAction(IRandomNumberProvider provider)
        : base()
    {
        this.provider = provider;
    }

    public void SetRandomProvider(IRandomNumberProvider provider)
    {
        this.provider = provider;
    }

    public override List<GameResult> Execute(GameRoot game)
    {
        if (provider == null)
        {
            throw new InvalidOperationException(
                "RandomProvider must be set before Execute is called. Call SetRandomProvider() after deserialization."
            );
        }

        double probability = Convert.ToDouble(this.GetActionValue());

        if (provider.NextDouble() < probability)
        {
            return Actions[provider.NextInt(0, Actions.Count)].Execute(game);
        }

        return new List<GameResult>();
    }
}

[PersistableObject(Name = "TriggerDuel")]
public class TriggerDuelAction : GameAction
{
    public List<string> AttackerInstanceIDs { get; set; } = new List<string>();
    public List<string> DefenderInstanceIDs { get; set; } = new List<string>();

    public TriggerDuelAction()
        : base() { }

    public override List<GameResult> Execute(GameRoot game)
    {
        // @TODO: Implement duel resolution
        return new List<GameResult>
        {
            new DuelTriggeredResult
            {
                AttackerInstanceIDs = AttackerInstanceIDs,
                DefenderInstanceIDs = DefenderInstanceIDs,
                Tick = game.CurrentTick,
            },
        };
    }
}

[PersistableObject(Name = "TriggerEvent")]
public class TriggerEventAction : GameAction
{
    public string EventInstanceID { get; set; }

    [PersistableIgnore]
    private IRandomNumberProvider provider;

    public TriggerEventAction()
        : base() { }

    public void SetRandomProvider(IRandomNumberProvider provider)
    {
        this.provider = provider;
    }

    public override List<GameResult> Execute(GameRoot game)
    {
        GameEvent gameEvent = game.GetEventByInstanceID(EventInstanceID);
        var eventProvider = provider ?? new SystemRandomProvider(new Random().Next());
        return gameEvent.Execute(game, eventProvider);
    }
}
