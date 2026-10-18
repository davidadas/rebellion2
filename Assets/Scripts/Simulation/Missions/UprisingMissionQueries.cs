using System;
using System.Collections.Generic;
using System.Linq;
using Rebellion.Game;
using Rebellion.Game.Factions;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Missions;
using Rebellion.Game.Units;

namespace Rebellion.Simulation
{
    /// <summary>
    /// Estimates next-execution uprising completion from observed state without advancing the
    /// simulation. Operational planning can value intermediate progress separately.
    /// </summary>
    internal sealed class UprisingMissionQueries
    {
        private readonly GameRoot _game;
        private readonly PlanetaryControlQueries _control;
        private readonly PlanetaryControlCommands _support;

        /// <summary>
        /// Creates completion queries sharing the game's support and control rules.
        /// </summary>
        /// <param name="game">The game supplying factions and configuration.</param>
        internal UprisingMissionQueries(GameRoot game)
        {
            _game = game;
            _control = new PlanetaryControlQueries(game);
            _support = new PlanetaryControlCommands(game, _control, new FogOfWarQueries(game));
        }

        /// <summary>
        /// Returns the conditional completion chance after a participant passes its skill roll.
        /// Travel, hidden missions, and subsequent ticks are outside this observed-state estimate.
        /// </summary>
        /// <param name="mission">The proposed mission.</param>
        /// <param name="planet">The observed destination.</param>
        /// <returns>A probability between zero and one; one for other mission types.</returns>
        internal double GetCompletionProbability(Mission mission, Planet planet)
        {
            if (mission is not InciteUprisingMission && mission is not SubdueUprisingMission)
                return 1;

            Dictionary<string, int> regiments = planet
                .GetAllRegiments()
                .Where(regiment =>
                    regiment.ManufacturingStatus == ManufacturingStatus.Complete
                    && regiment.Movement == null
                    && !string.IsNullOrEmpty(regiment.OwnerInstanceID)
                )
                .GroupBy(regiment => regiment.OwnerInstanceID)
                .ToDictionary(group => group.Key, group => group.Count());
            return mission is InciteUprisingMission
                ? GetInciteCompletionProbability(mission, planet, regiments)
                : GetSubdueCompletionProbability(mission, planet, regiments);
        }

        /// <summary>
        /// Enumerates the two-die distribution and evaluates the resulting garrison losses.
        /// Collections are resolved once, outside the roll enumeration.
        /// </summary>
        /// <param name="mission">The proposed incite mission.</param>
        /// <param name="planet">The observed destination.</param>
        /// <param name="regiments">Completed stationary regiments grouped by owner.</param>
        /// <returns>The conditional chance of meeting the control objective.</returns>
        private double GetInciteCompletionProbability(
            Mission mission,
            Planet planet,
            Dictionary<string, int> regiments
        )
        {
            string opponent = _game
                .GetFactions()
                .Select(faction => faction.InstanceID)
                .FirstOrDefault(id => id != mission.OwnerInstanceID);
            if (regiments.ContainsKey(mission.OwnerInstanceID))
                return 0;

            Faction controller =
                string.IsNullOrEmpty(planet.OwnerInstanceID) || !planet.IsPopulated()
                    ? null
                    : _game.GetFactionByOwnerInstanceID(planet.OwnerInstanceID);
            if (controller == null)
                return GetProjectedController(planet, regiments, null, 0) != opponent ? 1 : 0;

            GameConfig.UprisingConfig config = _game.Config.Uprising;
            Planet projection = CreateProjection(planet);
            _support.ChangePopularSupport(projection, controller, config.InciteMissionSupportShift);
            int troopCount = regiments.TryGetValue(controller.InstanceID, out int count)
                ? count
                : 0;
            bool[] completesAfterLosses = new bool[3];
            for (int losses = 0; losses < completesAfterLosses.Length; losses++)
                completesAfterLosses[losses] =
                    GetProjectedController(projection, regiments, controller.InstanceID, losses)
                    != opponent;

            int support = planet.GetPopularSupport(controller.InstanceID);
            GameConfig.GarrisonConfig garrison = _game.Config.AI.Garrison;
            int threshold =
                support >= garrison.SupportThreshold
                    ? 0
                    : (int)
                        Math.Ceiling(
                            (garrison.SupportThreshold - support) / (double)garrison.GarrisonDivisor
                        );
            int multiplier =
                planet.GetParentOfType<PlanetSector>()?.SectorType == PlanetSectorType.Core
                && controller.Settings.UprisingResistance > 1
                    ? controller.Settings.UprisingResistance
                    : 1;
            int adjustment = GetMissionAdjustment(planet, mission, config.MissionLeadershipDivisor);
            KeyValuePair<int, int>[] primary = config
                .PrimaryConsequenceTable.OrderBy(entry => entry.Key)
                .ToArray();
            KeyValuePair<int, int>[] secondary = config
                .SecondaryConsequenceTable.OrderBy(entry => entry.Key)
                .ToArray();
            double successes = 0;
            for (int sum = 0; sum <= 2 * config.DiceRange; sum++)
            {
                int score =
                    sum + 2 * config.DiceAddend + threshold - multiplier * troopCount + adjustment;
                int effect = GetConsequence(primary, score);
                int severity = effect > 0 ? GetConsequence(secondary, score) : 0;
                int losses = (effect == 2 ? 1 : 0) + (severity == 2 ? 1 : 0);
                if (completesAfterLosses[losses])
                    successes += config.DiceRange + 1 - Math.Abs(sum - config.DiceRange);
            }
            return successes / ((config.DiceRange + 1d) * (config.DiceRange + 1d));
        }

        /// <summary>
        /// Evaluates every configured support shift against control and the active garrison requirement.
        /// </summary>
        /// <param name="mission">The proposed subdue mission.</param>
        /// <param name="planet">The observed destination.</param>
        /// <param name="regiments">Completed stationary regiments grouped by owner.</param>
        /// <returns>The conditional chance that the uprising ends.</returns>
        private double GetSubdueCompletionProbability(
            Mission mission,
            Planet planet,
            Dictionary<string, int> regiments
        )
        {
            GameConfig.UprisingConfig config = _game.Config.Uprising;
            Faction faction = _game.GetFactionByOwnerInstanceID(mission.OwnerInstanceID);
            int successes = 0;
            for (int roll = 0; roll <= config.SubdueOwnedSupportRange; roll++)
            {
                Planet projection = CreateProjection(planet);
                _support.ChangePopularSupport(
                    projection,
                    faction,
                    config.SubdueOwnedSupportBase + roll
                );
                string owner = GetProjectedController(projection, regiments, null, 0);
                if (owner != planet.OwnerInstanceID)
                {
                    successes++;
                    continue;
                }
                if (string.IsNullOrEmpty(owner) || !projection.IsPopulated())
                    continue;
                int troops = regiments.TryGetValue(owner, out int count) ? count : 0;
                if (
                    troops
                    >= UprisingQueries.CalculateGarrisonRequirement(
                        projection,
                        _game.GetFactionByOwnerInstanceID(owner),
                        _game.Config.AI.Garrison
                    )
                )
                    successes++;
            }
            return successes / (config.SubdueOwnedSupportRange + 1d);
        }

        /// <summary>
        /// Resolves control after removing up to two controller regiments from an observed snapshot.
        /// </summary>
        /// <param name="planet">The projected support and colonization state.</param>
        /// <param name="regiments">The initial stationary regiment counts.</param>
        /// <param name="losingOwner">The owner whose regiments may be destroyed.</param>
        /// <param name="losses">The maximum regiment losses.</param>
        /// <returns>The projected controlling faction identifier.</returns>
        private string GetProjectedController(
            Planet planet,
            Dictionary<string, int> regiments,
            string losingOwner,
            int losses
        )
        {
            List<string> owners = regiments
                .Where(entry => entry.Value > (entry.Key == losingOwner ? losses : 0))
                .Select(entry => entry.Key)
                .ToList();
            if (!planet.IsColonized)
                return owners.Contains(planet.OwnerInstanceID) ? planet.OwnerInstanceID : null;
            return _control.GetPlanetController(planet, owners)?.InstanceID;
        }

        /// <summary>
        /// Copies mutable support state while retaining the sector used by garrison rules.
        /// </summary>
        /// <param name="planet">The observed planet to copy.</param>
        /// <returns>A detached projection whose mutations cannot affect the observed or live planet.</returns>
        private static Planet CreateProjection(Planet planet)
        {
            Planet projection = (Planet)planet.CreateCopy();
            projection.SetParent(planet.GetParent());
            return projection;
        }

        /// <summary>
        /// Combines visible active mission adjustments with the proposed mission's arriving team.
        /// </summary>
        /// <param name="planet">The observed destination.</param>
        /// <param name="proposed">The proposed mission, counted exactly once.</param>
        /// <param name="divisor">The configured leadership divisor.</param>
        /// <returns>The signed incident-score adjustment.</returns>
        private static int GetMissionAdjustment(Planet planet, Mission proposed, int divisor)
        {
            int adjustment = 0;
            foreach (
                Mission mission in planet
                    .GetChildren<Mission>()
                    .Where(mission =>
                        mission.HasInitiated
                        && !mission.IsWaitingForParticipants()
                        && mission.InstanceID != proposed.InstanceID
                    )
                    .Append(proposed)
            )
            {
                if (mission is not InciteUprisingMission && mission is not SubdueUprisingMission)
                    continue;
                IReadOnlyList<IMissionParticipant> participants = mission.GetMainParticipants();
                int average =
                    participants.Count == 0
                        ? 0
                        : participants.Sum(participant =>
                            participant.GetEffectiveRating(SkillRating.Leadership)
                        ) / participants.Count;
                adjustment += (mission is InciteUprisingMission ? 1 : -1) * (average / divisor);
            }
            return adjustment;
        }

        /// <summary>
        /// Looks up the consequence at the highest satisfied threshold.
        /// </summary>
        /// <param name="table">The consequence table sorted by threshold.</param>
        /// <param name="score">The incident score.</param>
        /// <returns>The matching consequence, or zero below the first threshold.</returns>
        private static int GetConsequence(KeyValuePair<int, int>[] table, int score)
        {
            int consequence = 0;
            foreach (KeyValuePair<int, int> entry in table)
            {
                if (entry.Key > score)
                    break;
                consequence = entry.Value;
            }
            return consequence;
        }
    }
}
