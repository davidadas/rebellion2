using System;
using System.Linq;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Units;
using Rebellion.Util.DependencyInjection;
using Rebellion.Util.Random;

namespace Rebellion.Gameplay
{
    /// <summary>
    /// Captures authoritative runtime facts and restores them without applying disk-load reconciliation.
    /// </summary>
    public sealed partial class GameSession
    {
        /// <summary>
        /// Gets whether a complete writable session boundary can be captured without interrupting dispatch.
        /// </summary>
        internal bool CanCaptureRecoveryState =>
            CanExecuteCommands && !HasPendingGameplayResults && !HasPendingOwnershipChanges;

        /// <summary>
        /// Captures the graph and its existing runtime continuation without publishing or consuming facts.
        /// </summary>
        /// <returns>A single-reference-graph envelope for an independent recovery decode.</returns>
        internal GameSessionRecoveryState CaptureRecoveryState()
        {
            if (!CanCaptureRecoveryState)
                throw new InvalidOperationException(
                    "An executing simulation operation cannot be captured for recovery."
                );
            if (
                _randomProvider is not SystemRandomProvider random
                || !ReferenceEquals(_randomProvider, Game.Random)
            )
                throw new InvalidOperationException(
                    "Recovery requires the session's active deterministic random stream."
                );
            return new GameSessionRecoveryState
            {
                Game = Game,
                UseDeterministicRuntimeIdentifiers = Game.UseDeterministicRuntimeIdentifiers,
                RandomSeed = random.Seed,
                Tick = Tick.CaptureRecoveryState(),
                PendingCombat = GetService<SpaceCombatCommands>().CaptureRecoveryDecision(),
                PlanetaryControl = GetService<PlanetaryControlObserver>().CaptureRecoveryState(),
                Ownership = Game.GetFactions()
                    .ConvertAll(faction => new FactionOwnershipRecoveryState
                    {
                        Faction = faction,
                        Units = faction.CaptureRecoveryOwnership(),
                    }),
            };
        }

        /// <summary>
        /// Constructs services against an independently decoded graph and reinstates its exact writable phase.
        /// No gameplay phase, migration, queue normalization, or intelligence refresh is performed.
        /// </summary>
        /// <param name="state">The complete independently decoded recovery envelope.</param>
        /// <param name="gameData">The verified exact installed content catalog.</param>
        /// <returns>The recovered session, ready for ordinary ordered operations.</returns>
        internal static GameSession RestoreRecoveryState(
            GameSessionRecoveryState state,
            GameDataCatalog gameData
        )
        {
            ValidateRecoveryState(state);
            GameSession session = new GameSession(gameData);
            try
            {
                session.ApplyRecoveryState(state);
                return session;
            }
            catch
            {
                session.Dispose();
                throw;
            }
        }

        /// <summary>
        /// Applies an independently decoded runtime boundary while preserving this session's public facades.
        /// Existing pipeline and tick event subscriptions remain attached to their stable owners.
        /// </summary>
        /// <param name="state">The complete independently decoded recovery envelope.</param>
        internal void ApplyRecoveryState(GameSessionRecoveryState state)
        {
            ValidateRecoveryState(state);
            PrepareGameReplacement();
            Game = state.Game;
            EnsureGameConfiguration();
            Game.UseDeterministicRuntimeIdentifiers = state.UseDeterministicRuntimeIdentifiers;
            Game.Random = new SystemRandomProvider(state.RandomSeed, Game.RandomIndex);
            Game.RebuildNodeIndex();
            RestoreModelRecoveryFacts(state);
            _randomProvider = Game.Random;
            Action[] previousConnections = _disconnect.ToArray();
            ServiceLocator previousServiceScope = _serviceScope;
            try
            {
                InitializeComponents(Tick.ConnectRecoveryRuntime);
                RebuildContentBindings();
                GetService<ManufacturingObserver>().RebuildQueues();
                GetService<PlanetaryControlObserver>().RestoreRecoveryState(state.PlanetaryControl);
                GetService<SpaceCombatCommands>().RestoreRecoveryDecision(state.PendingCombat);
                Tick.RestoreRecoveryState(state.Tick);
            }
            catch
            {
                if (previousServiceScope != null && previousServiceScope != _serviceScope)
                    _retiredServiceScopes.Add(previousServiceScope);
                throw;
            }
            CompleteScopeReplacement(previousConnections, previousServiceScope);
        }

        /// <summary>
        /// Restores index order and model continuation flags without invoking domain transitions.
        /// </summary>
        /// <param name="state">The decoded graph and model facts.</param>
        private static void RestoreModelRecoveryFacts(GameSessionRecoveryState state)
        {
            foreach (FactionOwnershipRecoveryState ownership in state.Ownership)
                ownership.Faction.RestoreRecoveryOwnership(ownership.Units);
            foreach (
                PlanetSector sector in state.Game.NodesByInstanceID.Values.OfType<PlanetSector>()
            )
                sector.ClearRecoverySuppressionSources();
            foreach (CapitalShip ship in state.Game.NodesByInstanceID.Values.OfType<CapitalShip>())
                ship.RestoreRecoverySuppressionMembership();
        }

        /// <summary>
        /// Rejects incomplete envelopes instead of substituting recalculated or initial runtime facts.
        /// </summary>
        /// <param name="state">The decoded recovery envelope.</param>
        private static void ValidateRecoveryState(GameSessionRecoveryState state)
        {
            if (state == null)
                throw new ArgumentNullException(nameof(state));
            if (state.Game?.RandomIndex < 0)
                throw new InvalidOperationException(
                    "A recovered random position cannot be negative."
                );
            if (
                state.Game == null
                || state.Tick == null
                || state.PlanetaryControl == null
                || state.Ownership == null
            )
                throw new ArgumentException(
                    "Complete session recovery facts are required.",
                    nameof(state)
                );
        }
    }
}
