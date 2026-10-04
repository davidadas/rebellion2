using System.Collections.Generic;
using System.Linq;
using Rebellion.Game;
using Rebellion.Game.Factions;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Results;
using Rebellion.Game.Units;
using Rebellion.Util.Logging;
using Rebellion.Util.Random;

namespace Rebellion.Simulation
{
    /// <summary>Resolves the risk of moving a unit through an opposing blockade.</summary>
    internal sealed class EvacuationLossResolver
    {
        private readonly GameRoot _game;
        private readonly IRandomNumberProvider _random;

        /// <summary>Creates blockade evacuation resolution.</summary>
        /// <param name="game">The active game graph.</param>
        /// <param name="random">The random source used for loss rolls.</param>
        internal EvacuationLossResolver(GameRoot game, IRandomNumberProvider random)
        {
            _game = game ?? throw new System.ArgumentNullException(nameof(game));
            _random = random ?? throw new System.ArgumentNullException(nameof(random));
        }

        /// <summary>Resolves losses for a unit leaving through an opposing blockade.</summary>
        /// <param name="unit">The departing unit.</param>
        /// <param name="origin">The departure planet.</param>
        /// <returns>The loss result, or null when the unit survives.</returns>
        internal EvacuationLossesResult Resolve(IMovable unit, Planet origin)
        {
            if (
                !origin.IsBlockadedFor(unit.GetOwnerInstanceID())
                || origin.HasOperationalIonCannon()
                || unit is not Regiment regiment
                || _random.NextInt(0, 100) >= _game.Config.Blockade.EvacuationLossPercent
            )
                return null;

            Faction faction = _game
                .GetFactions()
                .FirstOrDefault(candidate => candidate.InstanceID == unit.GetOwnerInstanceID());
            _game.DeleteNode(unit);
            GameLogger.Log(
                $"{unit.GetDisplayName()} destroyed running blockade at {origin.GetDisplayName()}"
            );
            return new EvacuationLossesResult
            {
                Faction = faction,
                Location = origin,
                LostRegiments = new List<Regiment> { regiment },
                Tick = _game.CurrentTick,
            };
        }
    }
}
