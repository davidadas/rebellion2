using System.Collections.Generic;
using System.Linq;
using Rebellion.Game.Results;
using Rebellion.Util.Serialization;

namespace Rebellion.Game.Events
{
    [PersistableObject(Name = "Outcome")]
    public sealed class RandomOutcome
    {
        [PersistableAttribute]
        public int Weight { get; set; } = 1;

        public List<GameConditional> Conditions { get; set; } = new List<GameConditional>();

        [PersistableInlineCollection]
        public List<GameAction> Actions { get; set; } = new List<GameAction>();
    }

    [PersistableObject(Name = "Random")]
    public sealed class RandomAction : GameAction
    {
        [PersistableInlineCollection]
        public List<RandomOutcome> Outcomes { get; set; } = new List<RandomOutcome>();

        public override List<GameResult> Execute(GameActionContext context)
        {
            List<RandomOutcome> eligible = Outcomes
                .Where(outcome =>
                    outcome.Weight > 0
                    && outcome.Conditions.All(condition =>
                        condition.IsMet(context.Game, context.Activation)
                    )
                )
                .ToList();
            if (eligible.Count == 0)
                return new List<GameResult>();

            int roll = context.Random.NextInt(0, eligible.Sum(outcome => outcome.Weight));
            RandomOutcome selected = null;
            foreach (RandomOutcome outcome in eligible)
            {
                roll -= outcome.Weight;
                if (roll < 0)
                {
                    selected = outcome;
                    break;
                }
            }

            return GameAction.ExecuteAll(selected.Actions, context);
        }
    }

    [PersistableObject(Name = "If")]
    public sealed class IfAction : GameAction
    {
        public List<GameConditional> Conditions { get; set; } = new List<GameConditional>();
        public List<GameAction> Actions { get; set; } = new List<GameAction>();
        public List<GameAction> Else { get; set; } = new List<GameAction>();

        public override List<GameResult> Execute(GameActionContext context)
        {
            IEnumerable<GameAction> selected = Conditions.TrueForAll(condition =>
                condition.IsMet(context.Game, context.Activation)
            )
                ? Actions
                : Else;
            return GameAction.ExecuteAll(selected, context);
        }
    }
}
