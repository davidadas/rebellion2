using System;
using System.Linq;
using Rebellion.Game;
using Rebellion.Game.Factions;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Messages;
using Rebellion.Game.Results;
using Rebellion.Game.Units;

namespace Rebellion.Simulation
{
    /// <summary>
    /// Projects combat state into detached, faction-visible data for presentation clients.
    /// </summary>
    public sealed class CombatPresentationQueries
    {
        private readonly FogOfWarQueries _fogOfWarQueries;
        private readonly GameRoot _game;
        private readonly MessageFactory _messageFactory;
        private readonly SpaceCombatCommands _spaceCombatCommands;

        /// <summary>
        /// Creates combat presentation queries for one active game.
        /// </summary>
        /// <param name="game">The authoritative game state.</param>
        /// <param name="fogOfWarQueries">The faction visibility projector.</param>
        /// <param name="spaceCombatCommands">The pending space-combat state.</param>
        /// <param name="messageFactory">The combat report factory.</param>
        public CombatPresentationQueries(
            GameRoot game,
            FogOfWarQueries fogOfWarQueries,
            SpaceCombatCommands spaceCombatCommands,
            MessageFactory messageFactory
        )
        {
            _game = game ?? throw new ArgumentNullException(nameof(game));
            _fogOfWarQueries =
                fogOfWarQueries ?? throw new ArgumentNullException(nameof(fogOfWarQueries));
            _spaceCombatCommands =
                spaceCombatCommands ?? throw new ArgumentNullException(nameof(spaceCombatCommands));
            _messageFactory =
                messageFactory ?? throw new ArgumentNullException(nameof(messageFactory));
        }

        /// <summary>
        /// Returns the current pending encounter through one faction's fog-of-war view.
        /// </summary>
        /// <param name="perspectiveOwnerInstanceId">The viewing faction identifier.</param>
        /// <returns>The detached pending encounter, or null when none is visible.</returns>
        public PendingCombatSnapshot GetPendingCombat(string perspectiveOwnerInstanceId)
        {
            if (
                string.IsNullOrEmpty(perspectiveOwnerInstanceId)
                || !_spaceCombatCommands.TryGetPendingCombat(out PendingCombatResult pending)
                || !IsParticipant(pending, perspectiveOwnerInstanceId)
            )
                return null;

            Faction perspective = _game
                .GetFactions()
                .FirstOrDefault(faction => faction.InstanceID == perspectiveOwnerInstanceId);
            if (perspective == null || string.IsNullOrEmpty(pending.Planet?.InstanceID))
                return null;

            GalaxyMap visibleGalaxy = _fogOfWarQueries.BuildFactionView(perspective);
            Planet visiblePlanet = visibleGalaxy
                .GetChildren<PlanetSector>()
                .SelectMany(sector => sector.GetChildren<Planet>())
                .FirstOrDefault(planet => planet.InstanceID == pending.Planet.InstanceID);
            if (visiblePlanet == null)
                return null;

            Planet detachedPlanet = (Planet)
                visiblePlanet.CreateCopy(recursive: true, includeDisabled: true);
            return new PendingCombatSnapshot
            {
                AttackerFleet = FindFleet(detachedPlanet, pending.AttackerFleet?.InstanceID),
                DefenderFleet = FindFleet(detachedPlanet, pending.DefenderFleet?.InstanceID),
                AttackerOwnerInstanceID = pending.AttackerOwnerInstanceID,
                DefenderOwnerInstanceID = pending.DefenderOwnerInstanceID,
                Planet = detachedPlanet,
                AttackerCanRetreat = pending.AttackerCanRetreat,
                DefenderCanRetreat = pending.DefenderCanRetreat,
                Tick = pending.Tick,
            };
        }

        /// <summary>
        /// Captures a completed result as a detached report for one participating faction.
        /// </summary>
        /// <param name="result">The authoritative completed result.</param>
        /// <param name="perspectiveOwnerInstanceId">The viewing faction identifier.</param>
        /// <returns>The detached faction report, or null when the faction did not participate.</returns>
        public CombatReport CreateReport(GameResult result, string perspectiveOwnerInstanceId) =>
            _messageFactory.CreateCombatReport(result, perspectiveOwnerInstanceId, _game);

        /// <summary>
        /// Returns whether one faction participates in a pending encounter.
        /// </summary>
        /// <param name="pending">The pending encounter.</param>
        /// <param name="factionInstanceId">The faction identifier.</param>
        /// <returns>True when the faction is an attacker or defender.</returns>
        private static bool IsParticipant(PendingCombatResult pending, string factionInstanceId)
        {
            return pending != null
                && (
                    pending.AttackerOwnerInstanceID == factionInstanceId
                    || pending.DefenderOwnerInstanceID == factionInstanceId
                );
        }

        /// <summary>
        /// Finds one fleet inside a detached visible planet.
        /// </summary>
        /// <param name="planet">The detached visible planet.</param>
        /// <param name="fleetInstanceId">The requested fleet identifier.</param>
        /// <returns>The detached fleet, or null when it is not visible.</returns>
        private static Fleet FindFleet(Planet planet, string fleetInstanceId)
        {
            return string.IsNullOrEmpty(fleetInstanceId)
                ? null
                : planet
                    ?.GetChildren<Fleet>()
                    .FirstOrDefault(fleet => fleet.InstanceID == fleetInstanceId);
        }
    }
}
