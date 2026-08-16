using System;
using System.Collections.Generic;
using System.Linq;
using Rebellion.Game;
using Rebellion.Game.Factions;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Missions;
using Rebellion.Game.Results;
using Rebellion.Game.Units;
using Rebellion.Util.Common;

namespace Rebellion.Systems
{
    /// <summary>
    /// Applies strategic events that change officer loyalty.
    /// </summary>
    public class OfficerLoyaltySystem : IGameResultHandler<PlanetOwnershipChangedResult>
    {
        private readonly GameRoot _game;
        private readonly IRandomNumberProvider _provider;

        /// <summary>
        /// Creates a new officer loyalty system.
        /// </summary>
        /// <param name="game">The game instance.</param>
        /// <param name="provider">The deterministic simulation random source.</param>
        public OfficerLoyaltySystem(GameRoot game, IRandomNumberProvider provider = null)
        {
            _game = game ?? throw new ArgumentNullException(nameof(game));
            _provider = provider ?? game.Random;
        }

        /// <summary>
        /// Applies the galaxy-wide loyalty reaction when a faction gains a planet.
        /// </summary>
        /// <param name="results">The ownership changes to apply in their resolved order.</param>
        /// <returns>No follow-up results; loyalty changes are authoritative state updates.</returns>
        public List<GameResult> HandleResults(IReadOnlyList<PlanetOwnershipChangedResult> results)
        {
            foreach (
                PlanetOwnershipChangedResult result in results
                    ?? Array.Empty<PlanetOwnershipChangedResult>()
            )
            {
                ApplyIncomingControlLoyaltyShift(result?.NewOwner);
            }

            return new List<GameResult>();
        }

        /// <summary>
        /// Resolves whether an eligible mission participant betrays the mission.
        /// </summary>
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
        /// Returns the first eligible participant whose loyalty roll causes them to betray the mission.
        /// </summary>
        private Officer FindBetrayingOfficer(Mission mission) =>
            mission.GetAllParticipants().OfType<Officer>().FirstOrDefault(BetraysMission);

        /// <summary>
        /// Rolls an eligible companion's Force rank to determine who discovers the betrayal.
        /// </summary>
        private Officer FindOfficerWhoDiscoversBetrayal(Mission mission, Officer defector) =>
            mission
                .GetAllParticipants()
                .OfType<Officer>()
                .Where(officer => officer != defector && CanDiscoverMissionBetrayal(officer))
                .FirstOrDefault(officer => _provider.NextInt(0, 100) < officer.ForceRank);

        /// <summary>
        /// Determines whether an eligible officer betrays a mission using inverse loyalty as
        /// the percentage chance.
        /// </summary>
        private bool BetraysMission(Officer officer)
        {
            if (
                officer
                is not {
                    CanBetray: true,
                    CurrentRank: OfficerRank.None,
                    IsCaptured: false,
                    IsKilled: false,
                }
            )
                return false;

            int probability = 100 - Math.Clamp(officer.Loyalty, 0, 100);
            return _provider.NextInt(0, 100) < probability;
        }

        /// <summary>
        /// Returns whether an active Force-ranked officer can discover another participant's betrayal.
        /// </summary>
        private static bool CanDiscoverMissionBetrayal(Officer officer) =>
            officer is { IsCaptured: false, IsKilled: false } && officer.ForceRank > 0;

        /// <summary>
        /// Marks a discovered betrayer as a known traitor and records who exposed them and where.
        /// </summary>
        private void RevealTraitor(
            Mission mission,
            Officer defector,
            Officer discoverer,
            ICollection<GameResult> results
        )
        {
            defector.IsTraitor = true;
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
        /// Applies one deterministic loyalty shift after a faction gains control.
        /// </summary>
        /// <param name="incomingFaction">The faction gaining a planet.</param>
        private void ApplyIncomingControlLoyaltyShift(Faction incomingFaction)
        {
            if (incomingFaction == null)
                return;

            GameConfig.RandomRangeConfig range = _game
                .Config
                .OfficerLoyalty
                .PlanetAcquisitionLoyaltyShift;
            int minimum = Math.Max(0, range.Minimum);
            int maximum = Math.Max(minimum, range.Maximum);
            int loyaltyShift = _provider.NextInt(minimum, maximum + 1);
            if (loyaltyShift == 0)
                return;

            foreach (
                Officer officer in _game.GetSceneNodesByType<Officer>().Where(IsFreeLivingOfficer)
            )
            {
                int signedShift =
                    officer.GetOwnerInstanceID() == incomingFaction.InstanceID
                        ? loyaltyShift
                        : -loyaltyShift;
                officer.Loyalty = Math.Max(0, Math.Min(100, officer.Loyalty + signedShift));
            }
        }

        /// <summary>
        /// Returns whether an officer participates in galaxy-wide loyalty shifts.
        /// </summary>
        /// <param name="officer">The officer to inspect.</param>
        /// <returns>True for living, uncaptured officers without command rank.</returns>
        private static bool IsFreeLivingOfficer(Officer officer)
        {
            return officer is { CurrentRank: OfficerRank.None, IsCaptured: false, IsKilled: false };
        }
    }
}
