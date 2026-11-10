using System;
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
                Building headquarters = _game.GetSceneNodeByInstanceID<Building>(
                    faction.HeadquartersUnitInstanceID
                );
                return headquarters != null
                    && headquarters.OwnerInstanceID == faction.InstanceID
                    && !faction.HasLostHeadquarters;
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
        /// Resolves a faction identifier against the active game.
        /// </summary>
        /// <param name="factionInstanceId">The faction identifier.</param>
        /// <returns>The registered faction, or null when the identifier is absent or unknown.</returns>
        public Faction GetFaction(string factionInstanceId)
        {
            return string.IsNullOrEmpty(factionInstanceId)
                ? null
                : _game
                    .GetFactions()
                    .FirstOrDefault(faction => faction?.InstanceID == factionInstanceId);
        }

        /// <summary>
        /// Resolves the only opposing faction when a result has no responsible attacker.
        /// </summary>
        /// <param name="defender">The faction whose opponent is required.</param>
        /// <returns>The sole opposing faction, or null when the result is ambiguous.</returns>
        public Faction GetSoleOpponent(Faction defender)
        {
            Faction liveDefender = GetFaction(defender?.InstanceID);
            if (liveDefender == null)
                return null;

            Faction[] opponents = _game
                .GetFactions()
                .Where(faction => faction != null && faction != liveDefender)
                .Take(2)
                .ToArray();
            return opponents.Length == 1 ? opponents[0] : null;
        }

        /// <summary>
        /// Determines whether every opponent has lost its headquarters and, in conquest games,
        /// every opposing conquest officer has been captured.
        /// </summary>
        /// <param name="candidate">The potential winning faction.</param>
        /// <returns>True when the faction has met every victory requirement.</returns>
        public bool HasDefeatedEveryOpponent(Faction candidate)
        {
            Faction liveCandidate = GetFaction(candidate?.InstanceID);
            if (liveCandidate == null)
                return false;

            Faction[] opponents = _game
                .GetFactions()
                .Where(faction => faction != null && faction != liveCandidate)
                .ToArray();
            if (opponents.Length == 0 || opponents.Any(opponent => !HasLostHeadquarters(opponent)))
                return false;

            if (!IsConquestGame())
                return true;

            return opponents.All(opponent =>
                opponent.Settings.ConquestOfficerInstanceIDs.All(officerInstanceID =>
                    _game.GetSceneNodeByInstanceID<Officer>(officerInstanceID) is Officer officer
                    && officer.GetOwnerInstanceID() == opponent.InstanceID
                    && officer.IsCaptured
                )
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
