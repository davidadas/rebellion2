using System;
using System.Collections.Generic;
using System.Linq;
using Rebellion.Game;
using Rebellion.Game.Factions;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Results;
using Rebellion.Game.Units;

namespace Rebellion.Simulation
{
    /// <summary>Routes capture and ownership changes to custody operations in batch order.</summary>
    public sealed class CaptiveObserver
    {
        private readonly GameRoot _game;
        private readonly CaptiveCommands _commands;

        /// <summary>Creates the custody listener for the active game.</summary>
        /// <param name="game">The authoritative game used to resolve officer ownership.</param>
        /// <param name="commands">The custody and release operations.</param>
        public CaptiveObserver(GameRoot game, CaptiveCommands commands)
        {
            _game = game ?? throw new ArgumentNullException(nameof(game));
            _commands = commands ?? throw new ArgumentNullException(nameof(commands));
        }

        /// <summary>
        /// Establishes custody for newly captured officers and records the location revealed to
        /// their original factions.
        /// </summary>
        /// <param name="results">The capture-state changes to process.</param>
        /// <returns>Movement results produced while transferring captives.</returns>
        public List<GameResult> HandleResults(IReadOnlyList<OfficerCaptureStateResult> results)
        {
            List<GameResult> reactions = new List<GameResult>();
            if (results == null)
                return reactions;

            HashSet<string> handledOfficerIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (OfficerCaptureStateResult result in results)
            {
                Officer officer = result?.TargetOfficer;
                if (officer == null)
                    continue;

                Faction originalFaction = _game.GetFactionByOwnerInstanceID(
                    officer.OwnerInstanceID
                );
                if (result.IsCaptured == false)
                {
                    _commands.ClearReleaseTracking(officer, originalFaction);
                    continue;
                }

                if (
                    officer.IsCaptured != true
                    || string.IsNullOrEmpty(officer.CaptorInstanceID)
                    || !handledOfficerIds.Add(officer.InstanceID)
                )
                    continue;

                _commands.EstablishCustody(
                    officer,
                    originalFaction,
                    result.Context,
                    result.CapturingUnit,
                    result.Tick,
                    reactions
                );
            }

            return reactions;
        }

        /// <summary>
        /// Releases captured officers when their faction takes control of the planet holding them.
        /// </summary>
        /// <param name="results">The planet ownership changes to process.</param>
        /// <returns>Capture-state changes for the officers released by the ownership changes.</returns>
        public List<GameResult> HandleResults(IReadOnlyList<PlanetOwnershipChangedResult> results)
        {
            List<GameResult> reactions = new List<GameResult>();
            foreach (
                PlanetOwnershipChangedResult result in results
                    ?? Array.Empty<PlanetOwnershipChangedResult>()
            )
            {
                Planet planet = result?.Planet;
                string newOwnerInstanceID = result?.NewOwner?.InstanceID;
                if (planet == null || string.IsNullOrEmpty(newOwnerInstanceID))
                    continue;

                foreach (
                    Officer officer in planet
                        .GetAllOfficers()
                        .Where(officer =>
                            officer.IsCaptured
                            && !officer.IsKilled
                            && officer.GetOwnerInstanceID() == newOwnerInstanceID
                        )
                )
                {
                    reactions.Add(
                        _commands.ReleaseOfficer(
                            officer,
                            planet,
                            result.Tick,
                            officer.CaptorInstanceID
                        )
                    );
                }
            }

            return reactions;
        }
    }
}
