using System;
using System.Collections.Generic;
using System.Linq;
using Rebellion.Game;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Missions;
using Rebellion.Game.Results;
using Rebellion.Game.Units;
using Rebellion.Util.Random;

namespace Rebellion.Gameplay
{
    /// <summary>
    /// Resolves officer betrayal and discovery during mission execution.
    /// </summary>
    public sealed class MissionBetrayalResolver
    {
        private readonly GameRoot _game;
        private readonly IRandomNumberProvider _provider;

        /// <summary>
        /// Creates a mission-betrayal resolver for the active game.
        /// </summary>
        /// <param name="game">The active game state.</param>
        /// <param name="provider">The deterministic simulation random source.</param>
        public MissionBetrayalResolver(GameRoot game, IRandomNumberProvider provider)
        {
            _game = game ?? throw new ArgumentNullException(nameof(game));
            _provider = provider ?? throw new ArgumentNullException(nameof(provider));
        }

        /// <summary>
        /// Resolves whether an eligible mission participant betrays the mission.
        /// </summary>
        /// <param name="mission">The mission.</param>
        /// <param name="results">Receives any betrayal-discovery results.</param>
        /// <returns>True when an officer betrays the mission; otherwise false.</returns>
        public bool TryResolve(Mission mission, out List<GameResult> results)
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
        /// Rolls eligible participants in mission order and returns the first officer who betrays.
        /// </summary>
        /// <param name="mission">The mission.</param>
        /// <returns>The first matching betraying officer, or null when nobody betrays.</returns>
        private Officer FindBetrayingOfficer(Mission mission)
        {
            foreach (Officer officer in GetOfficersInResolutionOrder(mission))
            {
                if (BetraysMission(officer))
                    return officer;
            }

            return null;
        }

        /// <summary>
        /// Returns the first companion whose Force-rating roll discovers the betraying officer.
        /// </summary>
        /// <param name="mission">The mission containing the participants.</param>
        /// <param name="defector">The officer who betrayed the mission.</param>
        /// <returns>The discovering officer, or null when nobody discovers the traitor.</returns>
        private Officer FindOfficerWhoDiscoversBetrayal(Mission mission, Officer defector) =>
            GetOfficersInResolutionOrder(mission)
                .Where(officer => officer != defector)
                .FirstOrDefault(officer => _provider.NextInt(0, 100) < officer.ForceRank);

        /// <summary>
        /// Returns primary officers followed by decoy officers, using stable identity order within
        /// each role group.
        /// </summary>
        /// <param name="mission">The mission whose participant roles are inspected.</param>
        /// <returns>The officers in deterministic resolution order.</returns>
        private static IEnumerable<Officer> GetOfficersInResolutionOrder(Mission mission) =>
            mission
                .GetMainParticipants()
                .OfType<Officer>()
                .OrderBy(officer => officer.InstanceID, StringComparer.Ordinal)
                .Concat(
                    mission
                        .GetDecoyParticipants()
                        .OfType<Officer>()
                        .OrderBy(officer => officer.InstanceID, StringComparer.Ordinal)
                );

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
            discoverer.DiscoveredTraitorInstanceID = defector.InstanceID;
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
    }
}
