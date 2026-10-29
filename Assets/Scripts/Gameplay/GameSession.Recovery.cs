using System;
using System.Collections.Generic;
using System.Linq;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Missions;
using Rebellion.Game.Units;
using Rebellion.SceneGraph;
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
        internal bool CanCaptureRecoveryState => CanExecuteCommands;

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
            List<ISceneNode> registered = Game.CaptureRecoveryNodes();
            return new GameSessionRecoveryState
            {
                Game = Game,
                Config = Game.Config,
                UseDeterministicRuntimeIdentifiers = Game.UseDeterministicRuntimeIdentifiers,
                RandomSeed = random.Seed,
                RandomPosition = random.CallCount,
                Tick = Tick.CaptureRecoveryState(),
                PendingCombat = GetService<SpaceCombatCommands>().CaptureRecoveryDecision(),
                PendingMovementResults = GetService<MovementCommands>().CaptureRecoveryResults(),
                PendingManufacturingResults = GetService<ManufacturingCommands>()
                    .CaptureRecoveryResults(),
                Maintenance = GetService<MaintenanceCommands>().CaptureRecoveryState(),
                Smuggling = GetService<SmugglingCommands>().CaptureRecoveryState(),
                GarrisonSurplus = GetService<UprisingResolver>().CaptureRecoveryState(),
                PlanetaryControl = GetService<PlanetaryControlObserver>().CaptureRecoveryState(),
                ObservedOwnershipChanges = GetService<FogOfWarObserver>()
                    .CaptureRecoveryOwnershipChanges(),
                VictoryDeclared = GetService<VictoryCommands>().IsDeclared,
                RegisteredNodes = registered,
                Ownership = Game.GetFactions()
                    .ConvertAll(faction => new FactionOwnershipRecoveryState
                    {
                        Faction = faction,
                        Units = faction.CaptureRecoveryOwnership(),
                    }),
                ManufacturingQueues = registered
                    .OfType<Planet>()
                    .Select(planet => new ManufacturingQueueRecoveryState
                    {
                        Planet = planet,
                        Lanes = planet
                            .ManufacturingQueue.Select(lane => new ManufacturingLaneRecoveryState
                            {
                                Type = lane.Key,
                                Items = new List<IManufacturable>(lane.Value),
                            })
                            .ToList(),
                    })
                    .ToList(),
                Fleets = registered
                    .OfType<Fleet>()
                    .Select(fleet => new FleetCombatRecoveryState
                    {
                        Fleet = fleet,
                        IsInCombat = fleet.IsInCombat,
                    })
                    .ToList(),
                Missions = registered
                    .OfType<Mission>()
                    .Select(mission => new MissionParticipantRecoveryState
                    {
                        Mission = mission,
                        ParticipantIDs = mission.CaptureRecoveryParticipantIDs(),
                        WasCaptured = mission.HasRecoveryParticipantRoster,
                    })
                    .ToList(),
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
            Game.SetConfig(state.Config);
            Game.UseDeterministicRuntimeIdentifiers = state.UseDeterministicRuntimeIdentifiers;
            Game.Random = new SystemRandomProvider(state.RandomSeed, state.RandomPosition);
            Game.RestoreRecoveryNodes(state.RegisteredNodes);
            RestoreModelRecoveryFacts(state);
            _randomProvider = Game.Random;
            Action[] previousConnections = _disconnect.ToArray();
            ServiceLocator previousServiceScope = _serviceScope;
            try
            {
                InitializeComponents(Tick.ConnectRecoveryRuntime);
                RebuildContentBindings();
                GetService<MovementCommands>().RestoreRecoveryResults(state.PendingMovementResults);
                GetService<ManufacturingCommands>()
                    .RestoreRecoveryResults(state.PendingManufacturingResults);
                GetService<MaintenanceCommands>().RestoreRecoveryState(state.Maintenance);
                GetService<SmugglingCommands>().RestoreRecoveryState(state.Smuggling);
                GetService<UprisingResolver>().RestoreRecoveryState(state.GarrisonSurplus);
                GetService<PlanetaryControlObserver>().RestoreRecoveryState(state.PlanetaryControl);
                GetService<FogOfWarObserver>()
                    .RestoreRecoveryOwnershipChanges(state.ObservedOwnershipChanges);
                GetService<VictoryCommands>().RestoreRecoveryState(state.VictoryDeclared);
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
            foreach (ManufacturingQueueRecoveryState queue in state.ManufacturingQueues)
            {
                Dictionary<ManufacturingType, List<IManufacturable>> lanes =
                    new Dictionary<ManufacturingType, List<IManufacturable>>();
                foreach (ManufacturingLaneRecoveryState lane in queue.Lanes)
                    lanes.Add(lane.Type, new List<IManufacturable>(lane.Items));
                queue.Planet.ManufacturingQueue = lanes;
            }
            foreach (FleetCombatRecoveryState fleet in state.Fleets)
                fleet.Fleet.RestoreRecoveryCombatState(fleet.IsInCombat);
            foreach (MissionParticipantRecoveryState mission in state.Missions)
                mission.Mission.RestoreRecoveryParticipants(
                    mission.ParticipantIDs,
                    mission.WasCaptured
                );
            foreach (PlanetSector sector in state.RegisteredNodes.OfType<PlanetSector>())
                sector.ClearRecoverySuppressionSources();
            foreach (CapitalShip ship in state.RegisteredNodes.OfType<CapitalShip>())
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
            if (state.RandomPosition < 0)
                throw new InvalidOperationException(
                    "A recovered random position cannot be negative."
                );
            if (
                state.Game == null
                || state.Config == null
                || state.Tick == null
                || state.PendingMovementResults == null
                || state.PendingManufacturingResults == null
                || state.Maintenance == null
                || state.Smuggling == null
                || state.GarrisonSurplus == null
                || state.PlanetaryControl == null
                || state.ObservedOwnershipChanges == null
                || state.RegisteredNodes == null
                || state.Ownership == null
                || state.ManufacturingQueues == null
                || state.Fleets == null
                || state.Missions == null
            )
                throw new ArgumentException(
                    "Complete session recovery facts are required.",
                    nameof(state)
                );
        }
    }
}
