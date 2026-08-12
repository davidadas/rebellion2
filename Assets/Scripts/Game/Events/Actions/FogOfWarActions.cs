using System.Collections.Generic;
using System.Linq;
using Rebellion.Game.Factions;
using Rebellion.Game.Results;
using Rebellion.SceneGraph;
using Rebellion.Util.Serialization;

namespace Rebellion.Game.Events
{
    /// <summary>
    /// Supplies current observations about selected objects to one faction.
    /// </summary>
    [PersistableObject(Name = "RevealToFaction")]
    public sealed class RevealToFactionAction : GameAction
    {
        [PersistableAttribute]
        public string FactionInstanceID { get; set; }

        [PersistableInlineCollection]
        public List<GameEventSelector> Selectors { get; set; } = new List<GameEventSelector>();

        public override List<GameResult> Execute(GameActionContext context)
        {
            Faction recipient = context.Game.GetFactionByOwnerInstanceID(FactionInstanceID);
            List<ISceneNode> observations = Selectors
                .SelectMany(selector =>
                    selector.Select(context.Game, context.Random, context.Activation)
                )
                .Distinct()
                .ToList();
            if (observations.Count == 0)
                return new List<GameResult>();

            return new List<GameResult>
            {
                new IntelligenceRevealedResult
                {
                    Recipient = recipient,
                    Observations = observations,
                    Tick = context.Game.CurrentTick,
                },
            };
        }
    }
}
