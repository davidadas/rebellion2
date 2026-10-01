using System;
using System.Collections.Generic;
using System.Linq;
using Rebellion.Game;
using Rebellion.Game.Factions;
using Rebellion.Game.Missions;
using Rebellion.Game.Results;
using Rebellion.Game.Units;
using Rebellion.Util.Random;

namespace Rebellion.Simulation
{
    /// <summary>
    /// Applies strategic events that change officer loyalty.
    /// </summary>
    public class OfficerLoyaltyCommands
    {
        private readonly GameRoot _game;
        private readonly IRandomNumberProvider _provider;

        /// <summary>
        /// Creates officer loyalty operations for the active game.
        /// </summary>
        /// <param name="game">The game instance.</param>
        /// <param name="provider">The deterministic simulation random source.</param>
        public OfficerLoyaltyCommands(GameRoot game, IRandomNumberProvider provider = null)
        {
            _game = game ?? throw new ArgumentNullException(nameof(game));
            _provider = provider ?? game.Random;
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

        /// <summary>
        /// Resolves whether an eligible mission participant betrays the mission.
        /// </summary>
        /// <param name="mission">The mission.</param>
        /// <param name="results">Receives the results.</param>
        /// <returns>True when the officer betrays the mission; otherwise false.</returns>
        public bool TryResolveMissionBetrayal(Mission mission, out List<GameResult> results)
        {
            if (mission == null)
                throw new ArgumentNullException(nameof(mission));

            results = new List<GameResult>();
            Officer defector = FindBetrayingOfficer(mission);
            if (defector == null)
                return false;

            return true;
        }

        /// <summary>
        /// Returns the first eligible participant whose loyalty roll causes them to betray the mission.
        /// </summary>
        /// <param name="mission">The mission.</param>
        /// <returns>The matching betraying officer.</returns>
        private Officer FindBetrayingOfficer(Mission mission) =>
            mission.GetAllParticipants().OfType<Officer>().FirstOrDefault(BetraysMission);

        /// <summary>
        /// Determines whether an eligible officer betrays a mission using inverse loyalty as
        /// the percentage chance.
        /// </summary>
        /// <param name="officer">The officer.</param>
        /// <returns>True when the officer betrays the mission; otherwise false.</returns>
        private bool BetraysMission(Officer officer)
        {
            if (officer is not { IsCaptured: false, IsKilled: false })
                return false;

            int probability = 100 - Math.Clamp(officer.Loyalty, 0, 100);
            return _provider.NextInt(0, 100) < probability;
        }
    }
}
