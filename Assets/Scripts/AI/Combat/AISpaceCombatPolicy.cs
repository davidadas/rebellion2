using System;
using System.Linq;
using Rebellion.Game;
using Rebellion.Game.Factions;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Units;

namespace Rebellion.AI.Combat
{
    /// <summary>
    /// Chooses tactical actions for AI-controlled space-combat participants.
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
        /// Determines whether an AI fleet should withdraw before combat.
        /// </summary>
        /// <param name="fleet">Fleet considering withdrawal.</param>
        /// <param name="planet">Planet where combat occurs.</param>
        /// <param name="fleetPower">Combat strength available to the fleet.</param>
        /// <param name="opponentPower">Combat strength available to its opponent.</param>
        /// <returns>True when the fleet should attempt to retreat.</returns>
        public bool ShouldRetreat(Fleet fleet, Planet planet, int fleetPower, int opponentPower)
        {
            if (fleet == null || planet == null || fleetPower > opponentPower)
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
