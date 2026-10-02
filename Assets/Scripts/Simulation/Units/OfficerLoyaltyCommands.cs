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

            Officer discoverer = FindOfficerWhoDiscoversBetrayal(mission, defector);
            if (discoverer != null)
                RevealTraitor(mission, defector, discoverer, results);

            return true;
        }

        /// <summary>
        /// Rolls every eligible participant and returns the last officer who betrays the mission.
        /// </summary>
        /// <param name="mission">The mission.</param>
        /// <returns>The last matching betraying officer, or null when nobody betrays.</returns>
        private Officer FindBetrayingOfficer(Mission mission)
        {
            Officer defector = null;
            foreach (Officer officer in mission.GetAllParticipants().OfType<Officer>())
            {
                if (BetraysMission(officer))
                    defector = officer;
            }

            return defector;
        }

        /// <summary>
        /// Returns the first companion whose Force-rating roll discovers the betraying officer.
        /// </summary>
        /// <param name="mission">The mission containing the participants.</param>
        /// <param name="defector">The officer who betrayed the mission.</param>
        /// <returns>The discovering officer, or null when nobody discovers the traitor.</returns>
        private Officer FindOfficerWhoDiscoversBetrayal(Mission mission, Officer defector) =>
            mission
                .GetAllParticipants()
                .OfType<Officer>()
                .Where(officer => officer != defector)
                .FirstOrDefault(officer => _provider.NextInt(0, 100) < officer.ForceRank);

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
        /// Records the officer who exposed a mission betrayal and the mission location.
        /// </summary>
        /// <param name="mission">The betrayed mission.</param>
        /// <param name="defector">The officer who betrayed the mission.</param>
        /// <param name="discoverer">The officer who discovered the betrayal.</param>
        /// <param name="results">The result collection receiving the discovery.</param>
        private void RevealTraitor(
            Mission mission,
            Officer defector,
            Officer discoverer,
            ICollection<GameResult> results
        )
        {
            results.Add(
                new TraitorDiscoveredResult
                {
                    Officer = defector,
                    DiscoveredBy = discoverer,
                    Context = mission.GetParent() as Planet,
                    Tick = _game.CurrentTick,
                }
            );
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
