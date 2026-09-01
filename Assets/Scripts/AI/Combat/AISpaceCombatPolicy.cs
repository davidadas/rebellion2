using System;
using System.Linq;
using Rebellion.Game;
using Rebellion.Game.Factions;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Units;

namespace Rebellion.AI.Combat
{
    /// <summary>
    /// Chooses tactical actions for automatically controlled space-combat participants.
    /// </summary>
    internal sealed class AISpaceCombatPolicy
    {
        private readonly GameRoot _game;

        /// <summary>
        /// Creates an AI space-combat policy for the active game.
        /// </summary>
        /// <param name="game">Active game state.</param>
        public AISpaceCombatPolicy(GameRoot game)
        {
            _game = game ?? throw new ArgumentNullException(nameof(game));
        }

        /// <summary>
        /// Determines whether an automatically controlled fleet may withdraw from combat.
        /// </summary>
        /// <param name="fleet">Fleet considering withdrawal.</param>
        /// <param name="planet">Planet where combat occurs.</param>
        /// <returns>True when the fleet may withdraw.</returns>
        public bool CanWithdraw(Fleet fleet, Planet planet)
        {
            if (fleet == null || planet == null)
                return false;

            Faction faction = _game.GetFactionByOwnerInstanceID(fleet.GetOwnerInstanceID());
            bool defendingFixedHeadquarters =
                faction != null
                && planet.GetOwnerInstanceID() == faction.InstanceID
                && _game
                    .GetFactions()
                    .Any(candidate =>
                        candidate != null
                        && candidate.HQInstanceID == planet.InstanceID
                        && candidate.Settings?.Headquarters?.IsMobile != true
                    );

            return !defendingFixedHeadquarters;
        }
    }
}
