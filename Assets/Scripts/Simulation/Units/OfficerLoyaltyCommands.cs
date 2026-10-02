using System;
using Rebellion.Game;
using Rebellion.Game.Factions;
using Rebellion.Game.Units;

namespace Rebellion.Simulation
{
    /// <summary>
    /// Applies general-purpose officer loyalty changes.
    /// </summary>
    public class OfficerLoyaltyCommands
    {
        private readonly GameRoot _game;

        /// <summary>
        /// Creates officer loyalty operations for the active game.
        /// </summary>
        /// <param name="game">The game instance.</param>
        public OfficerLoyaltyCommands(GameRoot game)
        {
            _game = game ?? throw new ArgumentNullException(nameof(game));
        }

        /// <summary>
        /// Applies one signed global loyalty shift relative to the favored faction.
        /// Officers belonging to the favored faction receive the shift; officers belonging to
        /// every other faction receive its inverse. Officers whose loyalty cannot change ignore it.
        /// </summary>
        /// <param name="favoredFaction">The faction for which the shift is positive.</param>
        /// <param name="shift">The signed shift relative to the favored faction.</param>
        public void ApplyGlobalShift(Faction favoredFaction, int shift)
        {
            if (favoredFaction == null)
                throw new ArgumentNullException(nameof(favoredFaction));
            if (shift == 0)
                return;

            foreach (
                Officer officer in _game.GetRegisteredSceneNodesByType<Officer>(
                    includeDisabled: true
                )
            )
            {
                int officerShift =
                    officer.GetOwnerInstanceID() == favoredFaction.InstanceID ? shift : -shift;
                officer.TryAdjustLoyalty(officerShift);
            }
        }
    }
}
