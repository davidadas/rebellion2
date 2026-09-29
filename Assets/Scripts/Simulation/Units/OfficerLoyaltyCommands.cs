using System;
using System.Collections.Generic;
using System.Linq;
using Rebellion.Game;
using Rebellion.Game.Factions;
using Rebellion.Game.Galaxy;
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

        /// <summary>
        /// Applies the acquired planet's support-based loyalty shift after a faction gains control.
        /// </summary>
        /// <param name="planet">The acquired planet.</param>
        /// <param name="incomingFaction">The faction gaining a planet.</param>
        public void ApplyControlShift(Planet planet, Faction incomingFaction)
        {
            if (planet == null || incomingFaction == null)
                return;

            Faction opposingFaction = _game
                .GetFactions()
                .FirstOrDefault(faction => faction.InstanceID != incomingFaction.InstanceID);
            if (opposingFaction == null)
                return;

            int divisor = _game.Config.OfficerLoyalty.PlanetAcquisitionSupportDivisor;
            if (divisor <= 0)
                throw new InvalidOperationException(
                    $"{nameof(GameConfig.OfficerLoyaltyConfig.PlanetAcquisitionSupportDivisor)} must be greater than zero."
                );

            int incomingSupport = planet.GetPopularSupport(incomingFaction.InstanceID);
            int opposingSupport = planet.GetPopularSupport(opposingFaction.InstanceID);
            int loyaltyShift =
                (incomingSupport - opposingSupport) / divisor + incomingSupport / divisor;
            if (loyaltyShift == 0)
                return;

            foreach (Officer officer in _game.GetSceneNodesByType<Officer>())
            {
                int signedShift =
                    officer.GetOwnerInstanceID() == incomingFaction.InstanceID
                        ? loyaltyShift
                        : -loyaltyShift;
                officer.TryAdjustLoyalty(signedShift);
            }
        }
    }
}
