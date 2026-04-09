using System;
using System.Collections.Generic;
using Rebellion.Game;
using Rebellion.Game.Results;
using Rebellion.Util.Attributes;
using Rebellion.Util.Common;

[PersistableObject(Name = "RandomOutcome")]
public class RandomOutcomeAction : GameAction
{
    public List<GameAction> Actions { get; set; } = new List<GameAction>();

    [PersistableIgnore]
    private IRandomNumberProvider _provider;

    public RandomOutcomeAction()
        : base() { }

    public RandomOutcomeAction(IRandomNumberProvider provider)
        : base()
    {
        _provider = provider;
    }

    public void SetRandomProvider(IRandomNumberProvider provider)
    {
        _provider = provider;
    }

    public override List<GameResult> Execute(GameRoot game)
    {
        if (_provider == null)
        {
            throw new InvalidOperationException(
                "RandomProvider must be set before Execute is called. Call SetRandomProvider() after deserialization."
            );
        }

        double probability = Convert.ToDouble(this.GetActionValue());

        if (_provider.NextDouble() < probability)
        {
            return Actions[_provider.NextInt(0, Actions.Count)].Execute(game);
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
                Attackers = AttackerInstanceIDs.ConvertAll(id =>
                    game.GetSceneNodeByInstanceID<Officer>(id)
                ),
                Defenders = DefenderInstanceIDs.ConvertAll(id =>
                    game.GetSceneNodeByInstanceID<Officer>(id)
                ),
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
    private IRandomNumberProvider _provider;

    public TriggerEventAction()
        : base() { }

    public void SetRandomProvider(IRandomNumberProvider provider)
    {
        _provider = provider;
    }

    public override List<GameResult> Execute(GameRoot game)
    {
        GameEvent gameEvent = game.GetEventByInstanceID(EventInstanceID);
        IRandomNumberProvider eventProvider =
            _provider ?? new SystemRandomProvider(new Random().Next());
        return gameEvent.Execute(game, eventProvider);
    }
}
