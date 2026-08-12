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
            Officer defector = mission
                .GetAllParticipants()
                .OfType<Officer>()
                .FirstOrDefault(Defects);
            if (defector == null)
                return false;

            Officer discoverer = mission
                .GetAllParticipants()
                .OfType<Officer>()
                .Where(officer => officer != defector && CanDiscover(officer))
                .FirstOrDefault(officer => _provider.NextInt(0, 100) < officer.ForceRank);
            if (discoverer != null)
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

            return true;
        }

        private bool Defects(Officer officer)
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

        private static bool CanDiscover(Officer officer) =>
            officer is { IsCaptured: false, IsKilled: false } && officer.ForceRank > 0;

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
