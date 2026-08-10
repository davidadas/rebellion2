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
    /// Resolves loyalty-driven betrayal and Force-assisted discovery mechanics.
    /// </summary>
    public class BetrayalSystem : IGameResultHandler<PlanetOwnershipChangedResult>
    {
        private readonly GameRoot _game;
        private readonly IRandomNumberProvider _provider;

        /// <summary>
        /// Creates a new BetrayalSystem.
        /// </summary>
        /// <param name="game">The game instance.</param>
        /// <param name="provider">The deterministic simulation random source.</param>
        public BetrayalSystem(GameRoot game, IRandomNumberProvider provider = null)
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
        /// Resolves whether an eligible officer betrays a completed mission.
        /// </summary>
        /// <param name="mission">The mission whose participants are being resolved.</param>
        /// <param name="provider">The deterministic simulation random source.</param>
        /// <param name="results">Receives any traitor-discovery result.</param>
        /// <returns>True when a participant betrays and foils the mission.</returns>
        public bool TryResolveMissionBetrayal(
            Mission mission,
            IRandomNumberProvider provider,
            out List<GameResult> results
        )
        {
            if (mission == null)
                throw new ArgumentNullException(nameof(mission));
            if (provider == null)
                throw new ArgumentNullException(nameof(provider));

            results = new List<GameResult>();
            Officer traitor = mission
                .GetAllParticipants()
                .OfType<Officer>()
                .FirstOrDefault(officer => BetraysMission(officer, provider));
            if (traitor == null)
                return false;

            Officer discoverer = mission
                .GetAllParticipants()
                .OfType<Officer>()
                .Where(officer => officer != traitor && CanDiscoverTraitor(officer))
                .FirstOrDefault(officer => provider.NextInt(0, 100) < officer.ForceRank);
            if (discoverer != null)
            {
                traitor.IsTraitor = true;
                results.Add(
                    new TraitorDiscoveredResult
                    {
                        Officer = traitor,
                        DiscoveredBy = discoverer,
                        Context = mission.GetParent() as Planet,
                        Tick = _game.CurrentTick,
                    }
                );
            }

            return true;
        }

        /// <summary>
        /// Resolves one eligible officer's loyalty-based betrayal roll.
        /// </summary>
        /// <param name="officer">The participating officer.</param>
        /// <param name="provider">The deterministic simulation random source.</param>
        /// <returns>True when the officer betrays the mission.</returns>
        private static bool BetraysMission(Officer officer, IRandomNumberProvider provider)
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

            int betrayalProbability = 100 - Math.Max(0, Math.Min(100, officer.Loyalty));
            return provider.NextInt(0, 100) < betrayalProbability;
        }

        /// <summary>
        /// Returns whether a companion can attempt to detect a traitor.
        /// </summary>
        /// <param name="officer">The companion to inspect.</param>
        /// <returns>True when the officer is active and Force-capable.</returns>
        private static bool CanDiscoverTraitor(Officer officer)
        {
            return officer is { IsCaptured: false, IsKilled: false } && officer.ForceRank > 0;
        }

        /// <summary>
        /// Applies one deterministic loyalty shift after a faction gains control.
        /// </summary>
        /// <param name="incomingFaction">The faction gaining a planet.</param>
        private void ApplyIncomingControlLoyaltyShift(Faction incomingFaction)
        {
            if (incomingFaction == null)
                return;

            int minimum = Math.Max(0, _game.Config.Betrayal.IncomingControlLoyaltyRollMinimum);
            int maximum = Math.Max(
                minimum,
                _game.Config.Betrayal.IncomingControlLoyaltyRollMaximum
            );
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
