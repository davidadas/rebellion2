using System;
using System.Collections.Generic;
using System.Linq;
using Rebellion.Game;
using Rebellion.Game.Factions;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Units;

namespace Rebellion.Simulation
{
    /// <summary>
    /// Evaluates whether configured victory requirements are satisfied.
    /// </summary>
    public sealed class VictoryQueries
    {
        private readonly GameRoot _game;

        /// <summary>
        /// Creates victory queries for the active game.
        /// </summary>
        /// <param name="game">The active game graph and victory configuration.</param>
        public VictoryQueries(GameRoot game)
        {
            _game = game ?? throw new ArgumentNullException(nameof(game));
        }

        /// <summary>
        /// Gets whether the active game uses conquest victory conditions.
        /// </summary>
        /// <returns>True when conquest objectives are active.</returns>
        public bool IsConquestGame()
        {
            return _game.Setup?.VictoryCondition == GameVictoryCondition.Conquest;
        }

        /// <summary>
        /// Gets whether a configured faction currently owns a configured planet.
        /// </summary>
        /// <param name="planetInstanceId">The planet identifier.</param>
        /// <param name="factionInstanceId">The expected owner identifier.</param>
        /// <returns>True when the planet is owned by the faction.</returns>
        public bool IsPlanetOwnedByFaction(string planetInstanceId, string factionInstanceId)
        {
            Planet planet = _game.GetSceneNodeByInstanceID<Planet>(planetInstanceId);
            return planet != null
                && string.Equals(
                    planet.GetOwnerInstanceID(),
                    factionInstanceId,
                    StringComparison.Ordinal
                );
        }

        /// <summary>
        /// Gets whether a faction still controls its configured headquarters.
        /// </summary>
        /// <param name="factionInstanceId">The faction identifier.</param>
        /// <returns>True when the faction headquarters remains under its control.</returns>
        public bool IsHeadquartersOwnedByFaction(string factionInstanceId)
        {
            Faction faction = _game
                .GetFactions()
                .FirstOrDefault(candidate => candidate.InstanceID == factionInstanceId);
            if (faction?.Settings?.Headquarters?.IsMobile == true)
            {
                return _game
                    .GetSceneNodesByType<Building>()
                    .Any(building =>
                        building.TypeID == faction.Settings.Headquarters.FacilityTypeID
                        && building.OwnerInstanceID == faction.InstanceID
                    );
            }

            return faction != null
                && IsPlanetOwnedByFaction(faction.HQInstanceID, faction.InstanceID);
        }

        /// <summary>
        /// Gets whether a configured officer is currently captured.
        /// </summary>
        /// <param name="officerInstanceId">The officer identifier.</param>
        /// <returns>True when the officer exists and is captured.</returns>
        public bool IsOfficerCaptured(string officerInstanceId)
        {
            return _game.GetSceneNodeByInstanceID<Officer>(officerInstanceId)?.IsCaptured == true;
        }

        /// <summary>
        /// Determines whether losing headquarters satisfies the configured game mode.
        /// </summary>
        /// <param name="defender">The faction that lost its headquarters.</param>
        /// <returns>True when the headquarters loss may declare victory.</returns>
        public bool CanDeclareVictoryAfterHeadquartersLoss(Faction defender)
        {
            return CanDeclareVictoryAfterHeadquartersLoss(
                defender,
                _game.GetSceneNodesByType<Officer>()
            );
        }

        /// <summary>
        /// Evaluates headquarters loss against an existing officer assessment.
        /// </summary>
        /// <param name="defender">The faction that lost its headquarters.</param>
        /// <param name="officers">The officers in the active game.</param>
        /// <returns>True when the headquarters loss may declare victory.</returns>
        internal bool CanDeclareVictoryAfterHeadquartersLoss(
            Faction defender,
            IEnumerable<Officer> officers
        )
        {
            if (defender == null)
                return false;
            if (_game.Setup.VictoryCondition != GameVictoryCondition.Conquest)
                return true;

            foreach (Officer officer in officers ?? Array.Empty<Officer>())
            {
                if (
                    officer.GetOwnerInstanceID() == defender.InstanceID
                    && officer.IsMain
                    && !officer.IsCaptured
                )
                    return false;
            }

            return true;
        }
    }
}
