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

    /// <summary>
    /// Injects the random-number provider used for both the probability gate and
    /// the outcome pick. Required after deserialization, since the provider is not persisted.
    /// </summary>
    /// <param name="provider">The RNG implementation to use.</param>
    public void SetRandomProvider(IRandomNumberProvider provider)
    {
        _provider = provider;
    }

    /// <summary>
    /// Rolls against the configured probability; on success, executes a uniformly-chosen
    /// child action and returns its results. Otherwise returns no results.
    /// </summary>
    /// <param name="game">The game state passed to the chosen child action.</param>
    /// <returns>The results produced by the chosen action, or an empty list if the roll failed.</returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown when <see cref="SetRandomProvider"/> has not been called.
    /// </exception>
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

    /// <summary>
    /// Resolves the referenced attacker and defender officers and emits a
    /// <see cref="DuelTriggeredResult"/>. Duel resolution itself is not yet implemented.
    /// </summary>
    /// <param name="game">The game state used to resolve officer references.</param>
    /// <returns>A single <see cref="DuelTriggeredResult"/> describing the participants.</returns>
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

    /// <summary>
    /// Injects the random-number provider used when executing the triggered event.
    /// Required after deserialization, since the provider is not persisted.
    /// </summary>
    /// <param name="provider">The RNG implementation to use.</param>
    public void SetRandomProvider(IRandomNumberProvider provider)
    {
        _provider = provider;
    }

    /// <summary>
    /// Resolves the referenced <see cref="GameEvent"/> and runs its action chain.
    /// Falls back to a freshly seeded RNG if no provider has been injected.
    /// </summary>
    /// <param name="game">The game state used to resolve the event.</param>
    /// <returns>The results produced by the triggered event's actions.</returns>
    public override List<GameResult> Execute(GameRoot game)
    {
        GameEvent gameEvent = game.GetEventByInstanceID(EventInstanceID);
        IRandomNumberProvider eventProvider =
            _provider ?? new SystemRandomProvider(new Random().Next());
        return gameEvent.Execute(game, eventProvider);
    }
}
