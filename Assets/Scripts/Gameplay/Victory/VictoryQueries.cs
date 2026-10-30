using System;
using System.Collections.Generic;
using System.Linq;
using Rebellion.Game;
using Rebellion.Game.Factions;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Units;

namespace Rebellion.Gameplay
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
            return _game.Summary?.VictoryCondition == GameVictoryCondition.Conquest;
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
        /// Finds the faction that has defeated every opponent under the configured game mode.
        /// </summary>
        /// <returns>The winning faction, or null while no faction has met every requirement.</returns>
        public Faction FindWinner()
        {
            return FindWinner(_game.GetSceneNodesByType<Officer>());
        }

        /// <summary>
        /// Finds the faction that has defeated every opponent using an existing officer assessment.
        /// </summary>
        /// <param name="officers">The officers in the active game.</param>
        /// <returns>The winning faction, or null while no faction has met every requirement.</returns>
        internal Faction FindWinner(IEnumerable<Officer> officers)
        {
            List<Officer> officerList =
                officers?.Where(officer => officer != null).ToList() ?? new List<Officer>();
            return _game
                .GetFactions()
                .FirstOrDefault(candidate => HasDefeatedEveryOpponent(candidate, officerList));
        }

        /// <summary>
        /// Determines whether one faction retains its headquarters while every opponent has lost
        /// theirs and, in conquest games, every opposing main character has been captured.
        /// </summary>
        /// <param name="candidate">The potential winning faction.</param>
        /// <param name="officers">The officers in the active game.</param>
        /// <returns>True when the faction has met every victory requirement.</returns>
        private bool HasDefeatedEveryOpponent(
            Faction candidate,
            IReadOnlyCollection<Officer> officers
        )
        {
            if (
                candidate is not { HasLostHeadquarters: false }
                || !IsHeadquartersOwnedByFaction(candidate.InstanceID)
            )
                return false;

            List<Faction> opponents = _game
                .GetFactions()
                .Where(faction => faction != null && faction != candidate)
                .ToList();
            if (opponents.Count == 0 || opponents.Any(opponent => !HasLostHeadquarters(opponent)))
                return false;

            if (_game.Summary.VictoryCondition != GameVictoryCondition.Conquest)
                return true;

            HashSet<string> opponentIds = opponents
                .Select(opponent => opponent.InstanceID)
                .ToHashSet(StringComparer.Ordinal);

            return officers.All(officer =>
                !officer.IsMain
                || !opponentIds.Contains(officer.GetOwnerInstanceID())
                || officer.IsCaptured
            );
        }

        /// <summary>
        /// Determines whether a configured faction no longer controls its headquarters.
        /// </summary>
        /// <param name="faction">The faction whose headquarters state is evaluated.</param>
        /// <returns>True when the faction had a headquarters and has since lost it.</returns>
        private bool HasLostHeadquarters(Faction faction)
        {
            if (faction == null)
                return false;
            if (faction.HasLostHeadquarters)
                return true;

            bool isMobile = faction.Settings?.Headquarters?.IsMobile == true;
            if (isMobile)
                return false;

            return !string.IsNullOrEmpty(faction.HQInstanceID)
                && !IsHeadquartersOwnedByFaction(faction.InstanceID);
        }
    }
}
