using System;
using System.Collections.Generic;
using System.Linq;
using Rebellion.Game;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Messages;
using Rebellion.Game.Results;
using Rebellion.Game.Units;

namespace Rebellion.Simulation
{
    /// <summary>
    /// Executes player planetary combat from visible identifiers and returns detached reports.
    /// </summary>
    public sealed class PlayerPlanetaryCombatCommands
    {
        private readonly BombardmentCommands _bombardmentCommands;
        private readonly BombardmentQueries _bombardmentQueries;
        private readonly CombatPresentationQueries _presentationQueries;
        private readonly GameRoot _game;
        private readonly PlanetaryAssaultCommands _planetaryAssaultCommands;
        private readonly PlanetaryAssaultQueries _planetaryAssaultQueries;

        /// <summary>
        /// Creates player planetary-combat commands.
        /// </summary>
        /// <param name="game">The authoritative game state.</param>
        /// <param name="bombardmentCommands">The bombardment executor.</param>
        /// <param name="bombardmentQueries">The bombardment eligibility rules.</param>
        /// <param name="planetaryAssaultCommands">The assault executor.</param>
        /// <param name="planetaryAssaultQueries">The assault eligibility rules.</param>
        /// <param name="presentationQueries">The detached combat-report projector.</param>
        public PlayerPlanetaryCombatCommands(
            GameRoot game,
            BombardmentCommands bombardmentCommands,
            BombardmentQueries bombardmentQueries,
            PlanetaryAssaultCommands planetaryAssaultCommands,
            PlanetaryAssaultQueries planetaryAssaultQueries,
            CombatPresentationQueries presentationQueries
        )
        {
            _game = game ?? throw new ArgumentNullException(nameof(game));
            _bombardmentCommands =
                bombardmentCommands ?? throw new ArgumentNullException(nameof(bombardmentCommands));
            _bombardmentQueries =
                bombardmentQueries ?? throw new ArgumentNullException(nameof(bombardmentQueries));
            _planetaryAssaultCommands =
                planetaryAssaultCommands
                ?? throw new ArgumentNullException(nameof(planetaryAssaultCommands));
            _planetaryAssaultQueries =
                planetaryAssaultQueries
                ?? throw new ArgumentNullException(nameof(planetaryAssaultQueries));
            _presentationQueries =
                presentationQueries ?? throw new ArgumentNullException(nameof(presentationQueries));
        }

        /// <summary>
        /// Determines whether the player's visible fleet selection can bombard a planet.
        /// </summary>
        /// <param name="fleetInstanceIds">The selected visible fleet identifiers.</param>
        /// <param name="planetInstanceId">The visible target planet identifier.</param>
        /// <param name="type">The bombardment target profile.</param>
        /// <returns>True when the command can execute.</returns>
        public bool CanBombard(
            IReadOnlyList<string> fleetInstanceIds,
            string planetInstanceId,
            BombardmentType type
        )
        {
            return TryResolvePlayerCommand(
                    fleetInstanceIds,
                    planetInstanceId,
                    out List<Fleet> fleets,
                    out Planet planet
                ) && _bombardmentQueries.CanExecute(fleets, planet, type);
        }

        /// <summary>
        /// Bombards a planet and returns the player's detached report.
        /// </summary>
        /// <param name="fleetInstanceIds">The selected visible fleet identifiers.</param>
        /// <param name="planetInstanceId">The visible target planet identifier.</param>
        /// <param name="type">The bombardment target profile.</param>
        /// <returns>The detached combat report, or null when the command cannot execute.</returns>
        public CombatReport TryBombard(
            IReadOnlyList<string> fleetInstanceIds,
            string planetInstanceId,
            BombardmentType type
        )
        {
            if (
                !TryResolvePlayerCommand(
                    fleetInstanceIds,
                    planetInstanceId,
                    out List<Fleet> fleets,
                    out Planet planet
                )
            )
                return null;

            BombardmentResult result = _bombardmentCommands.TryExecute(fleets, planet, type);
            return _presentationQueries.CreateReport(result, _game.GetPlayerFaction()?.InstanceID);
        }

        /// <summary>
        /// Determines whether the player's visible fleet selection can assault a planet.
        /// </summary>
        /// <param name="fleetInstanceIds">The selected visible fleet identifiers.</param>
        /// <param name="planetInstanceId">The visible target planet identifier.</param>
        /// <returns>True when the command can execute.</returns>
        public bool CanAssault(IReadOnlyList<string> fleetInstanceIds, string planetInstanceId)
        {
            return TryResolvePlayerCommand(
                    fleetInstanceIds,
                    planetInstanceId,
                    out List<Fleet> fleets,
                    out Planet planet
                ) && _planetaryAssaultQueries.CanExecute(fleets, planet);
        }

        /// <summary>
        /// Assaults a planet and returns the player's detached report.
        /// </summary>
        /// <param name="fleetInstanceIds">The selected visible fleet identifiers.</param>
        /// <param name="planetInstanceId">The visible target planet identifier.</param>
        /// <returns>The detached combat report, or null when the command cannot execute.</returns>
        public CombatReport TryAssault(
            IReadOnlyList<string> fleetInstanceIds,
            string planetInstanceId
        )
        {
            if (
                !TryResolvePlayerCommand(
                    fleetInstanceIds,
                    planetInstanceId,
                    out List<Fleet> fleets,
                    out Planet planet
                )
            )
                return null;

            PlanetaryAssaultResult result = _planetaryAssaultCommands.TryExecute(fleets, planet);
            return _presentationQueries.CreateReport(result, _game.GetPlayerFaction()?.InstanceID);
        }

        /// <summary>
        /// Resolves visible identifiers to an authoritative command only when every fleet belongs to the player.
        /// </summary>
        /// <param name="fleetInstanceIds">The selected visible fleet identifiers.</param>
        /// <param name="planetInstanceId">The visible target planet identifier.</param>
        /// <param name="fleets">Receives the authoritative fleets.</param>
        /// <param name="planet">Receives the authoritative planet.</param>
        /// <returns>True when the complete player command could be resolved.</returns>
        private bool TryResolvePlayerCommand(
            IReadOnlyList<string> fleetInstanceIds,
            string planetInstanceId,
            out List<Fleet> fleets,
            out Planet planet
        )
        {
            fleets = new List<Fleet>();
            planet = null;
            string playerFactionId = _game.GetPlayerFaction()?.InstanceID;
            if (
                string.IsNullOrEmpty(playerFactionId)
                || string.IsNullOrEmpty(planetInstanceId)
                || fleetInstanceIds?.Count < 1
                || fleetInstanceIds.Any(string.IsNullOrEmpty)
                || fleetInstanceIds.Distinct().Count() != fleetInstanceIds.Count
            )
                return false;

            planet = _game.GetSceneNodeByInstanceID<Planet>(planetInstanceId);
            if (planet == null)
                return false;

            foreach (string fleetInstanceId in fleetInstanceIds)
            {
                Fleet fleet = _game.GetSceneNodeByInstanceID<Fleet>(fleetInstanceId);
                if (fleet?.GetOwnerInstanceID() != playerFactionId)
                    return false;
                fleets.Add(fleet);
            }

            return true;
        }
    }
}
