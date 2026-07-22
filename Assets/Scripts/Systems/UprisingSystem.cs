using System;
using System.Collections.Generic;
using System.Linq;
using Rebellion.Game;
using Rebellion.Game.Factions;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Missions;
using Rebellion.Game.Results;
using Rebellion.Game.Units;
using Rebellion.SceneGraph;
using Rebellion.Util.Common;

namespace Rebellion.Systems
{
    /// <summary>
    /// Manages planetary uprisings based on garrison strength vs. popular support.
    /// </summary>
    public class UprisingSystem : IGameSystem
    {
        private const int _buildingDestroyedSeverity = 1;
        private const int _regimentDestroyedSeverity = 2;

        private readonly GameRoot _game;
        private readonly IRandomNumberProvider _provider;
        private readonly PlanetaryControlSystem _planetaryControl;
        private readonly Dictionary<Planet, int> _garrisonSurplusByPlanet =
            new Dictionary<Planet, int>();

        /// <summary>
        /// Creates a new UprisingSystem.
        /// </summary>
        /// <param name="game">The game instance.</param>
        /// <param name="provider">Random number provider for uprising rolls.</param>
        /// <param name="planetaryControl">Planetary control system for ownership changes.</param>
        public UprisingSystem(
            GameRoot game,
            IRandomNumberProvider provider,
            PlanetaryControlSystem planetaryControl
        )
        {
            _game = game;
            _provider = provider;
            _planetaryControl = planetaryControl;
            InitializeGarrisonSurplus();
        }

        /// <summary>
        /// Checks garrison levels and resolves active uprisings for all owned planets.
        /// </summary>
        /// <returns>Game results from uprising starts and consequence resolution.</returns>
        public List<GameResult> ProcessTick()
        {
            List<GameResult> results = new List<GameResult>();

            foreach (Planet planet in _game.GetSceneNodesByType<Planet>())
            {
                Faction faction = GetControllingFaction(planet);
                if (faction == null)
                {
                    _garrisonSurplusByPlanet[planet] = 0;
                    if (planet.IsInUprising)
                        planet.EndUprising();
                    continue;
                }

                if (planet.IsInUprising)
                    ResolveActiveUprising(planet, faction, results);
                else
                    ReconcileGarrison(planet, faction, results);
            }

            return results;
        }

        /// <summary>
        /// Reconciles uprising state for planets affected by regiment deployment results.
        /// </summary>
        /// <param name="results">The result batch to inspect.</param>
        /// <returns>Any uprising results caused by the deployment changes.</returns>
        internal List<GameResult> ProcessResults(IEnumerable<GameResult> results)
        {
            List<GameResult> uprisingResults = new List<GameResult>();
            if (results == null)
                return uprisingResults;

            IEnumerable<Planet> affectedPlanets = results
                .OfType<RegimentDeploymentChangedResult>()
                .Select(result => result.Planet)
                .Where(planet => planet != null)
                .Distinct();
            foreach (Planet planet in affectedPlanets)
                uprisingResults.AddRange(ReconcileGarrison(planet));

            return uprisingResults;
        }

        /// <summary>
        /// Reconciles uprising state for one planet after its garrison changes.
        /// </summary>
        /// <param name="planet">The planet whose garrison changed.</param>
        /// <returns>The uprising results produced by reconciliation.</returns>
        public List<GameResult> ReconcileGarrison(Planet planet)
        {
            List<GameResult> results = new List<GameResult>();
            Faction faction = GetControllingFaction(planet);
            if (faction == null)
            {
                _garrisonSurplusByPlanet[planet] = 0;
                return results;
            }

            if (planet.IsInUprising)
            {
                SynchronizeActiveUprising(planet, faction);
                return results;
            }

            ReconcileGarrison(planet, faction, results);
            return results;
        }

        /// <summary>
        /// Applies one successful incite-uprising attempt and reconciles planetary control.
        /// </summary>
        /// <param name="mission">The incite-uprising mission being resolved.</param>
        /// <param name="results">The result collection receiving uprising and control effects.</param>
        /// <returns>True when the mission's control objective is achieved.</returns>
        internal bool ResolveInciteMissionAttempt(
            InciteUprisingMission mission,
            List<GameResult> results
        )
        {
            Planet planet = mission?.GetParentOfType<Planet>();
            if (planet == null)
                return false;

            Faction controller = GetControllingFaction(planet);
            if (controller != null)
                ResolveUprisingIncident(planet, controller, results);

            results.AddRange(_planetaryControl.ReconcilePlanet(planet));
            results.AddRange(ReconcileGarrison(planet));

            string opposingFactionId = _game
                .GetFactions()
                .Select(faction => faction.InstanceID)
                .FirstOrDefault(instanceId => instanceId != mission.OwnerInstanceID);
            bool opposingFactionControlsPlanet = planet.OwnerInstanceID == opposingFactionId;
            bool missionFactionHasTroops = planet
                .GetAllRegiments()
                .Any(regiment =>
                    regiment.OwnerInstanceID == mission.OwnerInstanceID
                    && regiment.ManufacturingStatus == ManufacturingStatus.Complete
                    && regiment.Movement == null
                );
            return !opposingFactionControlsPlanet && !missionFactionHasTroops;
        }

        /// <summary>
        /// Applies one successful subdue-uprising attempt and reconciles planetary control.
        /// </summary>
        /// <param name="mission">The subdue-uprising mission being resolved.</param>
        /// <param name="results">The result collection receiving uprising and control effects.</param>
        /// <returns>True when the uprising ends.</returns>
        internal bool ResolveSubdueMissionAttempt(
            SubdueUprisingMission mission,
            List<GameResult> results
        )
        {
            Planet planet = mission?.GetParentOfType<Planet>();
            if (planet?.IsInUprising != true)
                return false;

            Faction missionFaction = _game.GetFactionByOwnerInstanceID(mission.OwnerInstanceID);
            int supportShift = RollSubdueSupportShift(mission, planet);
            ApplyUprisingSupportShift(planet, missionFaction, supportShift);
            results.AddRange(_planetaryControl.ReconcilePlanet(planet));

            if (!planet.IsInUprising)
                return true;

            Faction controller = GetControllingFaction(planet);
            if (controller == null || UpdateGarrisonSurplus(planet, controller) < 0)
                return false;

            planet.EndUprising();
            results.Add(
                new PlanetUprisingEndedResult
                {
                    Planet = planet,
                    Faction = controller,
                    Tick = _game.CurrentTick,
                }
            );
            return true;
        }

        /// <summary>
        /// Rolls the popular-support shift caused by a subdue-uprising attempt.
        /// </summary>
        /// <param name="mission">The subdue-uprising mission.</param>
        /// <param name="planet">The target planet.</param>
        /// <returns>The support shift for the planet's current ownership state.</returns>
        private int RollSubdueSupportShift(SubdueUprisingMission mission, Planet planet)
        {
            GameConfig.UprisingConfig config = _game.Config.Uprising;
            if (planet.OwnerInstanceID == mission.OwnerInstanceID)
            {
                return config.SubdueOwnedSupportBase
                    + _provider.NextInt(0, config.SubdueOwnedSupportRange + 1);
            }

            if (string.IsNullOrEmpty(planet.OwnerInstanceID))
            {
                return config.SubdueNeutralSupportBase
                    + _provider.NextInt(0, config.SubdueNeutralSupportRange + 1);
            }

            return 0;
        }

        /// <summary>
        /// Captures the initial garrison surplus for every planet.
        /// </summary>
        private void InitializeGarrisonSurplus()
        {
            foreach (Planet planet in _game.GetSceneNodesByType<Planet>())
            {
                Faction faction = GetControllingFaction(planet);
                _garrisonSurplusByPlanet[planet] =
                    faction == null ? 0 : CalculateGarrisonSurplus(planet, faction);
            }
        }

        /// <summary>
        /// Returns the controlling faction for a planet, or null if the planet is
        /// unowned, unpopulated, or its faction cannot be resolved.
        /// </summary>
        /// <param name="planet">The planet to evaluate.</param>
        /// <returns>The controlling faction, or null if no faction controls the planet.</returns>
        private Faction GetControllingFaction(Planet planet)
        {
            if (string.IsNullOrEmpty(planet.OwnerInstanceID))
                return null;
            if (!planet.IsPopulated())
                return null;
            return _game.GetFactionByOwnerInstanceID(planet.OwnerInstanceID);
        }

        /// <summary>
        /// Starts an uprising if the garrison is too weak to hold the planet.
        /// </summary>
        /// <param name="planet">The planet to evaluate.</param>
        /// <param name="faction">The controlling faction.</param>
        /// <param name="results">Collection to append uprising results to.</param>
        private void ReconcileGarrison(Planet planet, Faction faction, List<GameResult> results)
        {
            int previousSurplus = _garrisonSurplusByPlanet.TryGetValue(
                planet,
                out int storedSurplus
            )
                ? storedSurplus
                : CalculateGarrisonSurplus(planet, faction);
            int troopCount = CountControllingRegiments(planet, faction);
            int currentSurplus = UpdateGarrisonSurplus(planet, faction);

            if (currentSurplus < 0)
            {
                planet.BeginUprising();
                SynchronizeActiveUprising(planet, faction);
                results.Add(
                    new PlanetUprisingStartedResult
                    {
                        Planet = planet,
                        InstigatorFaction = FindLeadingOpposingFaction(planet, faction.InstanceID),
                        Tick = _game.CurrentTick,
                    }
                );
                return;
            }

            if (troopCount == 0 || currentSurplus != 0 || previousSurplus == 0)
                return;

            results.Add(new PlanetNearUprisingResult { Planet = planet, Tick = _game.CurrentTick });
        }

        /// <summary>
        /// Recalculates and stores one planet's garrison surplus.
        /// </summary>
        /// <param name="planet">The planet to evaluate.</param>
        /// <param name="faction">The controlling faction.</param>
        /// <returns>The updated garrison surplus.</returns>
        private int UpdateGarrisonSurplus(Planet planet, Faction faction)
        {
            int surplus = CalculateGarrisonSurplus(planet, faction);
            _garrisonSurplusByPlanet[planet] = surplus;
            return surplus;
        }

        /// <summary>
        /// Calculates available controlling regiments beyond the planet's requirement.
        /// </summary>
        /// <param name="planet">The planet to evaluate.</param>
        /// <param name="faction">The controlling faction.</param>
        /// <returns>The signed garrison surplus.</returns>
        private int CalculateGarrisonSurplus(Planet planet, Faction faction)
        {
            return CountControllingRegiments(planet, faction)
                - CalculateGarrisonRequirement(planet, faction, _game.Config.AI.Garrison);
        }

        /// <summary>
        /// Counts completed, stationary regiments belonging to a planet's controller.
        /// </summary>
        /// <param name="planet">The planet to inspect.</param>
        /// <param name="faction">The controlling faction.</param>
        /// <returns>The active controlling regiment count.</returns>
        private static int CountControllingRegiments(Planet planet, Faction faction)
        {
            return planet
                .GetAllRegiments()
                .Count(regiment =>
                    regiment.GetOwnerInstanceID() == faction.InstanceID
                    && regiment.ManufacturingStatus == ManufacturingStatus.Complete
                    && regiment.Movement == null
                );
        }

        /// <summary>
        /// Rolls uprising dice, applies consequences, and shifts controller support.
        /// </summary>
        /// <param name="planet">The planet in uprising.</param>
        /// <param name="faction">The controlling faction.</param>
        /// <param name="results">Collection to append uprising results to.</param>
        private void ResolveActiveUprising(Planet planet, Faction faction, List<GameResult> results)
        {
            results.AddRange(_planetaryControl.ReconcilePlanet(planet));
            if (!planet.IsInUprising)
                return;

            SynchronizeActiveUprising(planet, faction);

            while (planet.IsInUprising)
            {
                if (IsSupportDriftNext(planet))
                {
                    ResolveSupportDriftPulse(planet, faction, results);
                    continue;
                }

                if (IsIncidentNext(planet))
                {
                    ResolveIncidentPulse(planet, faction, results);
                    continue;
                }

                if (IsClearNext(planet))
                {
                    ResolveClearPulse(planet, faction, results);
                    continue;
                }

                break;
            }
        }

        /// <summary>
        /// Applies a due uprising support shift and schedules the next support pulse.
        /// </summary>
        /// <param name="planet">The planet in uprising.</param>
        /// <param name="faction">The controlling faction.</param>
        /// <param name="results">The result collection receiving control changes.</param>
        private void ResolveSupportDriftPulse(
            Planet planet,
            Faction faction,
            List<GameResult> results
        )
        {
            int scheduledTick = planet.NextUprisingSupportDriftTick;
            planet.NextUprisingSupportDriftTick = 0;
            planet.UprisingSupportDriftTimerOrder = 0;

            ApplyUprisingControllerSupportShift(planet, faction);
            results.AddRange(_planetaryControl.ReconcilePlanet(planet));
            if (!planet.IsInUprising)
                return;

            SynchronizeUprisingClearTimer(planet, faction);
            GameConfig.UprisingConfig config = _game.Config.Uprising;
            planet.NextUprisingSupportDriftTick = AdvanceTimer(
                scheduledTick,
                config.ActiveSupportDriftMinTicks,
                config.ActiveSupportDriftMaxTicks
            );
            planet.UprisingSupportDriftTimerOrder = ClaimTimerOrder(planet);
        }

        /// <summary>
        /// Resolves a due uprising incident and schedules the next incident pulse.
        /// </summary>
        /// <param name="planet">The planet in uprising.</param>
        /// <param name="faction">The controlling faction.</param>
        /// <param name="results">The result collection receiving incident and control effects.</param>
        private void ResolveIncidentPulse(Planet planet, Faction faction, List<GameResult> results)
        {
            int scheduledTick = planet.NextUprisingIncidentTick;
            planet.NextUprisingIncidentTick = 0;
            planet.UprisingIncidentTimerOrder = 0;

            ResolveUprisingIncident(planet, faction, results);
            results.AddRange(_planetaryControl.ReconcilePlanet(planet));
            if (!planet.IsInUprising)
                return;

            SynchronizeUprisingClearTimer(planet, faction);
            GameConfig.UprisingConfig config = _game.Config.Uprising;
            planet.NextUprisingIncidentTick = AdvanceTimer(
                scheduledTick,
                config.IncidentPulseMinTicks,
                config.IncidentPulseMaxTicks
            );
            planet.UprisingIncidentTimerOrder = ClaimTimerOrder(planet);
        }

        /// <summary>
        /// Ends an uprising when its garrison-clear timer becomes due.
        /// </summary>
        /// <param name="planet">The planet whose uprising is ending.</param>
        /// <param name="faction">The controlling faction.</param>
        /// <param name="results">The result collection receiving the uprising-ended result.</param>
        private void ResolveClearPulse(Planet planet, Faction faction, List<GameResult> results)
        {
            planet.NextUprisingClearTick = 0;
            planet.UprisingClearTimerOrder = 0;

            planet.EndUprising();
            results.Add(
                new PlanetUprisingEndedResult
                {
                    Planet = planet,
                    Faction = faction,
                    Tick = _game.CurrentTick,
                }
            );
        }

        /// <summary>
        /// Resolves one uprising incident and applies its consequences.
        /// </summary>
        /// <param name="planet">The planet in uprising.</param>
        /// <param name="faction">The controlling faction.</param>
        /// <param name="results">The result collection receiving incident effects.</param>
        private void ResolveUprisingIncident(
            Planet planet,
            Faction faction,
            List<GameResult> results
        )
        {
            int ownerSupport = planet.GetPopularSupport(faction.InstanceID);
            int troopCount = CountControllingRegiments(planet, faction);

            ResolveUprisingTableResults(
                planet,
                faction,
                ownerSupport,
                troopCount,
                out int uprisingEffect,
                out int uprisingSeverity
            );

            ApplyUprisingConsequence(planet, faction.InstanceID, uprisingEffect, results);
            ApplyUprisingConsequence(planet, faction.InstanceID, uprisingSeverity, results);

            if (HasActiveInciteMission(planet))
                ApplyUprisingSupportShift(
                    planet,
                    faction,
                    _game.Config.Uprising.InciteMissionSupportShift
                );
        }

        /// <summary>
        /// Ensures every timer required by an active uprising is armed.
        /// </summary>
        /// <param name="planet">The planet in uprising.</param>
        /// <param name="faction">The controlling faction.</param>
        private void SynchronizeActiveUprising(Planet planet, Faction faction)
        {
            GameConfig.UprisingConfig config = _game.Config.Uprising;
            SynchronizeUprisingClearTimer(planet, faction);

            if (planet.NextUprisingSupportDriftTick <= 0)
            {
                planet.NextUprisingSupportDriftTick = ArmTimer(
                    config.ActiveSupportDriftMinTicks,
                    config.ActiveSupportDriftMaxTicks
                );
                planet.UprisingSupportDriftTimerOrder = ClaimTimerOrder(planet);
            }

            if (planet.NextUprisingIncidentTick <= 0)
            {
                planet.NextUprisingIncidentTick = ArmTimer(
                    config.IncidentPulseMinTicks,
                    config.IncidentPulseMaxTicks
                );
                planet.UprisingIncidentTimerOrder = ClaimTimerOrder(planet);
            }
        }

        /// <summary>
        /// Arms or clears the uprising-end timer according to current garrison strength.
        /// </summary>
        /// <param name="planet">The planet in uprising.</param>
        /// <param name="faction">The controlling faction.</param>
        private void SynchronizeUprisingClearTimer(Planet planet, Faction faction)
        {
            int surplus = UpdateGarrisonSurplus(planet, faction);
            if (surplus < 0)
            {
                planet.NextUprisingClearTick = 0;
                planet.UprisingClearTimerOrder = 0;
                return;
            }

            if (planet.NextUprisingClearTick > 0)
                return;

            GameConfig.UprisingConfig config = _game.Config.Uprising;
            planet.NextUprisingClearTick = ArmTimer(
                config.ClearUprisingMinTicks,
                config.ClearUprisingMaxTicks
            );
            planet.UprisingClearTimerOrder = ClaimTimerOrder(planet);
        }

        /// <summary>
        /// Returns whether the support-drift timer is the next due uprising timer.
        /// </summary>
        /// <param name="planet">The planet whose timers are evaluated.</param>
        /// <returns>True when support drift should resolve next.</returns>
        private bool IsSupportDriftNext(Planet planet)
        {
            return IsNextDueTimer(
                planet.NextUprisingSupportDriftTick,
                planet.UprisingSupportDriftTimerOrder,
                planet.NextUprisingIncidentTick,
                planet.UprisingIncidentTimerOrder,
                planet.NextUprisingClearTick,
                planet.UprisingClearTimerOrder
            );
        }

        /// <summary>
        /// Returns whether the incident timer is the next due uprising timer.
        /// </summary>
        /// <param name="planet">The planet whose timers are evaluated.</param>
        /// <returns>True when an incident should resolve next.</returns>
        private bool IsIncidentNext(Planet planet)
        {
            return IsNextDueTimer(
                planet.NextUprisingIncidentTick,
                planet.UprisingIncidentTimerOrder,
                planet.NextUprisingSupportDriftTick,
                planet.UprisingSupportDriftTimerOrder,
                planet.NextUprisingClearTick,
                planet.UprisingClearTimerOrder
            );
        }

        /// <summary>
        /// Returns whether the uprising-clear timer is the next due uprising timer.
        /// </summary>
        /// <param name="planet">The planet whose timers are evaluated.</param>
        /// <returns>True when the uprising should clear next.</returns>
        private bool IsClearNext(Planet planet)
        {
            return IsNextDueTimer(
                planet.NextUprisingClearTick,
                planet.UprisingClearTimerOrder,
                planet.NextUprisingSupportDriftTick,
                planet.UprisingSupportDriftTimerOrder,
                planet.NextUprisingIncidentTick,
                planet.UprisingIncidentTimerOrder
            );
        }

        /// <summary>
        /// Returns whether a candidate timer is due before two competing timers.
        /// </summary>
        /// <param name="candidateTick">The candidate timer's scheduled tick.</param>
        /// <param name="candidateOrder">The candidate timer's tie-break order.</param>
        /// <param name="firstOtherTick">The first competing timer's scheduled tick.</param>
        /// <param name="firstOtherOrder">The first competing timer's tie-break order.</param>
        /// <param name="secondOtherTick">The second competing timer's scheduled tick.</param>
        /// <param name="secondOtherOrder">The second competing timer's tie-break order.</param>
        /// <returns>True when the candidate is due and no competing timer precedes it.</returns>
        private bool IsNextDueTimer(
            int candidateTick,
            int candidateOrder,
            int firstOtherTick,
            int firstOtherOrder,
            int secondOtherTick,
            int secondOtherOrder
        )
        {
            if (candidateTick <= 0 || candidateTick > _game.CurrentTick)
                return false;

            return !IsScheduledBefore(
                    firstOtherTick,
                    firstOtherOrder,
                    candidateTick,
                    candidateOrder
                )
                && !IsScheduledBefore(
                    secondOtherTick,
                    secondOtherOrder,
                    candidateTick,
                    candidateOrder
                );
        }

        /// <summary>
        /// Returns whether a scheduled timer is due before a candidate timer.
        /// </summary>
        /// <param name="scheduledTick">The competing timer's scheduled tick.</param>
        /// <param name="timerOrder">The competing timer's tie-break order.</param>
        /// <param name="candidateTick">The candidate timer's scheduled tick.</param>
        /// <param name="candidateOrder">The candidate timer's tie-break order.</param>
        /// <returns>True when the competing timer should resolve first.</returns>
        private bool IsScheduledBefore(
            int scheduledTick,
            int timerOrder,
            int candidateTick,
            int candidateOrder
        )
        {
            return scheduledTick > 0
                && scheduledTick <= _game.CurrentTick
                && (
                    scheduledTick < candidateTick
                    || scheduledTick == candidateTick && timerOrder < candidateOrder
                );
        }

        /// <summary>
        /// Schedules a new uprising timer from the current tick.
        /// </summary>
        /// <param name="minimumDelay">The minimum timer delay.</param>
        /// <param name="maximumDelay">The maximum timer delay.</param>
        /// <returns>The scheduled tick.</returns>
        private int ArmTimer(int minimumDelay, int maximumDelay)
        {
            return _game.CurrentTick + RollTimerDelay(minimumDelay, maximumDelay);
        }

        /// <summary>
        /// Advances a recurring uprising timer from its prior scheduled tick.
        /// </summary>
        /// <param name="scheduledTick">The timer's prior scheduled tick.</param>
        /// <param name="minimumDelay">The minimum recurrence delay.</param>
        /// <param name="maximumDelay">The maximum recurrence delay.</param>
        /// <returns>The next scheduled tick.</returns>
        private int AdvanceTimer(int scheduledTick, int minimumDelay, int maximumDelay)
        {
            return scheduledTick + RollTimerDelay(minimumDelay, maximumDelay);
        }

        /// <summary>
        /// Rolls an inclusive uprising timer delay within two configured bounds.
        /// </summary>
        /// <param name="minimumDelay">The first configured delay bound.</param>
        /// <param name="maximumDelay">The second configured delay bound.</param>
        /// <returns>The rolled delay.</returns>
        private int RollTimerDelay(int minimumDelay, int maximumDelay)
        {
            int lowerBound = Math.Min(minimumDelay, maximumDelay);
            int upperBound = Math.Max(minimumDelay, maximumDelay);
            return _provider.NextInt(lowerBound, upperBound + 1);
        }

        /// <summary>
        /// Claims the next deterministic tie-break order for a planet's uprising timers.
        /// </summary>
        /// <param name="planet">The planet owning the timers.</param>
        /// <returns>The claimed timer order.</returns>
        private static int ClaimTimerOrder(Planet planet)
        {
            planet.NextUprisingTimerOrder++;
            return planet.NextUprisingTimerOrder;
        }

        /// <summary>
        /// Resolves uprising outcome using dice rolls and consequence table lookups.
        /// </summary>
        /// <param name="planet">The planet in uprising.</param>
        /// <param name="faction">The controlling faction.</param>
        /// <param name="supportForController">Popular support value for the controlling faction.</param>
        /// <param name="controllerTroopCount">Number of controller's troops on the planet.</param>
        /// <param name="uprisingEffect">Output: primary consequence table result.</param>
        /// <param name="uprisingSeverity">Output: secondary consequence table result.</param>
        private void ResolveUprisingTableResults(
            Planet planet,
            Faction faction,
            int supportForController,
            int controllerTroopCount,
            out int uprisingEffect,
            out int uprisingSeverity
        )
        {
            uprisingEffect = 0;
            uprisingSeverity = 0;

            GameConfig.UprisingConfig config = _game.Config.Uprising;

            int rollA = _provider.NextInt(0, config.DiceRange + 1) + config.DiceAddend;
            int rollB = _provider.NextInt(0, config.DiceRange + 1) + config.DiceAddend;

            int troopMultiplier = GetUprisingTroopMultiplier(planet, faction);
            int threshold = CalculateUprisingThreshold(supportForController);
            int missionAdjustment = CalculateUprisingMissionAdjustment(planet, config);
            int uprisingResistanceRegimentCount = planet.GetActiveRegimentCount(
                config.ResistanceRegimentTypeID
            );

            int combinedScore =
                rollA
                + rollB
                + (threshold - troopMultiplier * controllerTroopCount)
                + missionAdjustment
                - uprisingResistanceRegimentCount;

            uprisingEffect = GetThresholdTableValue(config.PrimaryConsequenceTable, combinedScore);

            if (uprisingEffect > 0)
                uprisingSeverity = GetThresholdTableValue(
                    config.SecondaryConsequenceTable,
                    combinedScore
                );
        }

        /// <summary>
        /// Gets the troop multiplier applied to the controller's garrison strength.
        /// </summary>
        /// <param name="planet">The planet in uprising.</param>
        /// <param name="faction">The controlling faction.</param>
        /// <returns>The troop multiplier for this planet and faction.</returns>
        private static int GetUprisingTroopMultiplier(Planet planet, Faction faction)
        {
            PlanetSystem parentSystem = planet.GetParentOfType<PlanetSystem>();
            if (
                parentSystem != null
                && parentSystem.SystemType == PlanetSystemType.CoreSystem
                && faction.Settings.UprisingResistance > 1
            )
            {
                return faction.Settings.UprisingResistance;
            }

            return 1;
        }

        /// <summary>
        /// Calculates the net incident adjustment from active uprising missions.
        /// </summary>
        /// <param name="planet">The planet whose missions are evaluated.</param>
        /// <param name="config">The uprising configuration.</param>
        /// <returns>The signed mission adjustment.</returns>
        private int CalculateUprisingMissionAdjustment(
            Planet planet,
            GameConfig.UprisingConfig config
        )
        {
            int adjustment = 0;
            foreach (Mission mission in GetActiveUprisingMissions(planet))
            {
                int averageLeadership =
                    mission.MainParticipants.Count == 0
                        ? 0
                        : mission.MainParticipants.Sum(participant =>
                            participant.GetEffectiveRating(OfficerRating.Leadership)
                        ) / mission.MainParticipants.Count;
                int missionAdjustment = averageLeadership / config.MissionLeadershipDivisor;

                if (mission is InciteUprisingMission)
                    adjustment += missionAdjustment;
                else if (mission is SubdueUprisingMission)
                    adjustment -= missionAdjustment;
            }

            return adjustment;
        }

        /// <summary>
        /// Returns initiated uprising missions whose participants have reached a planet.
        /// </summary>
        /// <param name="planet">The planet whose missions are inspected.</param>
        /// <returns>The active incite and subdue uprising missions.</returns>
        private IEnumerable<Mission> GetActiveUprisingMissions(Planet planet)
        {
            return _game
                .GetSceneNodesByType<Mission>()
                .Where(mission =>
                    mission.GetParentOfType<Planet>() == planet
                    && mission.HasInitiated
                    && !mission.IsWaitingForParticipants()
                    && (mission is InciteUprisingMission || mission is SubdueUprisingMission)
                );
        }

        /// <summary>
        /// Returns whether a planet has an active incite-uprising mission.
        /// </summary>
        /// <param name="planet">The planet to inspect.</param>
        /// <returns>True when an active incite-uprising mission is present.</returns>
        private bool HasActiveInciteMission(Planet planet)
        {
            return GetActiveUprisingMissions(planet)
                .Any(mission => mission is InciteUprisingMission);
        }

        /// <summary>
        /// Dispatches an uprising consequence to its handler based on the table result code.
        /// </summary>
        /// <param name="planet">The planet experiencing the uprising.</param>
        /// <param name="controllerInstanceId">The controlling faction's instance ID.</param>
        /// <param name="consequence">The consequence code from the uprising table.</param>
        /// <param name="results">Result list to append events to.</param>
        private void ApplyUprisingConsequence(
            Planet planet,
            string controllerInstanceId,
            int consequence,
            List<GameResult> results
        )
        {
            switch (consequence)
            {
                case 1:
                    DestroyRandomBuilding(planet, controllerInstanceId, results);
                    return;
                case 2:
                    DestroyRandomRegiment(planet, controllerInstanceId, results);
                    return;
                case 3:
                    CaptureRandomOfficer(planet, controllerInstanceId, results);
                    return;
                case 4:
                    FreeRandomCapturedOfficer(planet, controllerInstanceId, results);
                    return;
                case 5:
                    FreeAllCapturedOfficers(planet, controllerInstanceId, results);
                    return;
            }
        }

        /// <summary>
        /// Destroys a random controller-owned building on the planet.
        /// </summary>
        /// <param name="planet">The planet experiencing the uprising.</param>
        /// <param name="controllerInstanceId">The controlling faction's instance ID.</param>
        /// <param name="results">Result list to append events to.</param>
        private void DestroyRandomBuilding(
            Planet planet,
            string controllerInstanceId,
            List<GameResult> results
        )
        {
            List<Building> facilities = planet
                .GetAllBuildings()
                .Where(b =>
                    b.GetOwnerInstanceID() == controllerInstanceId
                    && b.ManufacturingStatus == ManufacturingStatus.Complete
                    && b.Movement == null
                )
                .ToList();

            DestroyRandomIncidentTarget(facilities, planet, _buildingDestroyedSeverity, results);
        }

        /// <summary>
        /// Destroys a random controller-owned regiment on the planet.
        /// </summary>
        /// <param name="planet">The planet experiencing the uprising.</param>
        /// <param name="controllerInstanceId">The controlling faction's instance ID.</param>
        /// <param name="results">Result list to append events to.</param>
        private void DestroyRandomRegiment(
            Planet planet,
            string controllerInstanceId,
            List<GameResult> results
        )
        {
            List<Regiment> regiments = planet
                .GetAllRegiments()
                .Where(r =>
                    r.GetOwnerInstanceID() == controllerInstanceId
                    && r.ManufacturingStatus == ManufacturingStatus.Complete
                    && r.Movement == null
                )
                .ToList();

            DestroyRandomIncidentTarget(regiments, planet, _regimentDestroyedSeverity, results);
        }

        /// <summary>
        /// Destroys a random incident target and records the incident.
        /// </summary>
        /// <typeparam name="T">Type of scene node that can be destroyed.</typeparam>
        /// <param name="candidates">Possible incident targets.</param>
        /// <param name="planet">Planet where the incident occurs.</param>
        /// <param name="severity">Incident severity to report.</param>
        /// <param name="results">Result list to append events to.</param>
        private void DestroyRandomIncidentTarget<T>(
            List<T> candidates,
            Planet planet,
            int severity,
            List<GameResult> results
        )
            where T : class, ISceneNode
        {
            if (candidates.Count == 0)
                return;

            _game.DetachNode(candidates[_provider.NextInt(0, candidates.Count)]);
            results.Add(
                new PlanetIncidentResult
                {
                    Planet = planet,
                    IncidentType = IncidentType.Uprising,
                    Severity = severity,
                    Tick = _game.CurrentTick,
                }
            );
        }

        /// <summary>
        /// Captures a random uncaptured controller-owned officer on the planet.
        /// </summary>
        /// <param name="planet">The planet experiencing the uprising.</param>
        /// <param name="controllerInstanceId">The controlling faction's instance ID.</param>
        /// <param name="results">Result list to append events to.</param>
        private void CaptureRandomOfficer(
            Planet planet,
            string controllerInstanceId,
            List<GameResult> results
        )
        {
            List<Officer> candidates = planet
                .GetAllOfficers()
                .Where(o =>
                    o.GetOwnerInstanceID() == controllerInstanceId
                    && o.Movement == null
                    && !o.IsKilled
                    && !o.IsCaptured
                )
                .ToList();
            if (candidates.Count == 0)
                return;
            Officer target = candidates[_provider.NextInt(0, candidates.Count)];
            target.IsCaptured = true;
            Faction opposingFaction = FindLeadingOpposingFaction(planet, controllerInstanceId);
            target.CaptorInstanceID = opposingFaction?.InstanceID;
            target.CanEscape = true;
            results.Add(
                new OfficerCaptureStateResult
                {
                    TargetOfficer = target,
                    IsCaptured = true,
                    Context = planet,
                    Tick = _game.CurrentTick,
                }
            );
        }

        /// <summary>
        /// Frees one randomly selected captured controller-owned officer on the planet.
        /// </summary>
        /// <param name="planet">The planet experiencing the uprising.</param>
        /// <param name="controllerInstanceId">The controlling faction's instance ID.</param>
        /// <param name="results">Result list to append events to.</param>
        private void FreeRandomCapturedOfficer(
            Planet planet,
            string controllerInstanceId,
            List<GameResult> results
        )
        {
            List<Officer> candidates = planet
                .GetAllOfficers()
                .Where(o =>
                    o.GetOwnerInstanceID() == controllerInstanceId
                    && o.Movement == null
                    && !o.IsKilled
                    && o.IsCaptured
                )
                .ToList();
            if (candidates.Count == 0)
                return;
            Officer target = candidates[_provider.NextInt(0, candidates.Count)];
            target.IsCaptured = false;
            target.CaptorInstanceID = null;
            target.CanEscape = false;
            results.Add(
                new OfficerCaptureStateResult
                {
                    TargetOfficer = target,
                    IsCaptured = false,
                    Context = planet,
                    Tick = _game.CurrentTick,
                }
            );
        }

        /// <summary>
        /// Frees all captured controller-owned officers on the planet.
        /// </summary>
        /// <param name="planet">The planet experiencing the uprising.</param>
        /// <param name="controllerInstanceId">The controlling faction's instance ID.</param>
        /// <param name="results">Result list to append events to.</param>
        private void FreeAllCapturedOfficers(
            Planet planet,
            string controllerInstanceId,
            List<GameResult> results
        )
        {
            List<Officer> captured = planet
                .GetAllOfficers()
                .Where(o =>
                    o.GetOwnerInstanceID() == controllerInstanceId
                    && o.Movement == null
                    && !o.IsKilled
                    && o.IsCaptured
                )
                .ToList();
            foreach (Officer target in captured)
            {
                target.IsCaptured = false;
                target.CaptorInstanceID = null;
                target.CanEscape = false;
                results.Add(
                    new OfficerCaptureStateResult
                    {
                        TargetOfficer = target,
                        IsCaptured = false,
                        Context = planet,
                        Tick = _game.CurrentTick,
                    }
                );
            }
        }

        /// <summary>
        /// Applies a scheduled popular support shift to the controlling faction during uprising.
        /// On core systems the shift is halved when it moves against the faction's favor.
        /// </summary>
        /// <param name="planet">The planet in uprising.</param>
        /// <param name="faction">The controlling faction whose support is shifted.</param>
        private void ApplyUprisingControllerSupportShift(Planet planet, Faction faction)
        {
            ApplyUprisingSupportShift(
                planet,
                faction,
                _game.Config.Uprising.ControllerSupportShift
            );
        }

        /// <summary>
        /// Applies an uprising support shift with the controlling faction's configured penalty.
        /// </summary>
        /// <param name="planet">The planet whose support changes.</param>
        /// <param name="faction">The controlling faction.</param>
        /// <param name="shift">The signed support adjustment.</param>
        private void ApplyUprisingSupportShift(Planet planet, Faction faction, int shift)
        {
            if (shift == 0)
                return;

            PlanetSystem parentSystem = planet.GetParentOfType<PlanetSystem>();
            if (parentSystem != null && parentSystem.SystemType == PlanetSystemType.CoreSystem)
            {
                bool penaltyApplies = faction.Settings.WeakSupportPenaltyTrigger switch
                {
                    SupportShiftCondition.Positive => shift > 0,
                    SupportShiftCondition.Negative => shift < 0,
                    _ => false,
                };
                if (penaltyApplies)
                    shift /= _game.Config.SupportShift.WeakSupportPenaltyDivisor;
            }

            _planetaryControl.ShiftPopularSupport(planet, faction, shift);
        }

        /// <summary>
        /// Calculates how many garrison troops a planet requires for the given faction.
        /// Returns 0 when popular support is at or above the threshold.
        /// Core worlds with faction garrison efficiency receive a reduced requirement.
        /// Planets in active uprisings apply the uprising multiplier.
        /// </summary>
        /// <param name="planet">The planet to calculate garrison requirements for.</param>
        /// <param name="faction">The controlling faction.</param>
        /// <param name="config">Garrison configuration parameters.</param>
        /// <returns>The number of garrison troops required, or 0 if support is sufficient.</returns>
        public static int CalculateGarrisonRequirement(
            Planet planet,
            Faction faction,
            GameConfig.GarrisonConfig config
        )
        {
            int popularSupport = planet.GetPopularSupport(faction.InstanceID);

            if (popularSupport >= config.SupportThreshold)
                return 0;

            int garrison = (int)
                Math.Ceiling(
                    (config.SupportThreshold - popularSupport) / (double)config.GarrisonDivisor
                );

            PlanetSystem parentSystem = planet.GetParentOfType<PlanetSystem>();
            if (
                parentSystem != null
                && parentSystem.SystemType == PlanetSystemType.CoreSystem
                && faction.Settings.GarrisonEfficiency > 1
            )
            {
                garrison /= faction.Settings.GarrisonEfficiency;
            }

            if (planet.IsInUprising)
                garrison *= config.UprisingMultiplier;

            return garrison;
        }

        /// <summary>
        /// Calculates the garrison threshold for an uprising check.
        /// </summary>
        /// <param name="supportForController">Popular support for the controlling faction.</param>
        /// <returns>The garrison threshold.</returns>
        private int CalculateUprisingThreshold(int supportForController)
        {
            GameConfig.GarrisonConfig config = _game.Config.AI.Garrison;

            if (supportForController >= config.SupportThreshold)
                return 0;

            return (int)
                Math.Ceiling(
                    (config.SupportThreshold - supportForController)
                        / (double)config.GarrisonDivisor
                );
        }

        /// <summary>
        /// Looks up a value from an uprising table. Finds the highest threshold
        /// that the score meets or exceeds, and returns the associated value.
        /// </summary>
        /// <param name="table">The threshold-to-value lookup table.</param>
        /// <param name="score">The score to look up against the table thresholds.</param>
        /// <returns>The value associated with the highest matching threshold.</returns>
        private static int GetThresholdTableValue(Dictionary<int, int> table, int score)
        {
            int result = 0;
            foreach (KeyValuePair<int, int> entry in table.OrderBy(e => e.Key))
            {
                if (score >= entry.Key)
                    result = entry.Value;
                else
                    break;
            }
            return result;
        }

        /// <summary>
        /// Returns the opposing faction with the highest popular support on this planet,
        /// or null if no opposing faction has any support.
        /// </summary>
        /// <param name="planet">The planet to check support on.</param>
        /// <param name="ownerInstanceId">The current owner's instance ID to exclude.</param>
        /// <returns>The opposing faction with the most support, or null.</returns>
        private Faction FindLeadingOpposingFaction(Planet planet, string ownerInstanceId)
        {
            string opposingFactionId = null;
            int maxSupport = 0;
            foreach (KeyValuePair<string, int> kvp in planet.PopularSupport)
            {
                if (kvp.Key != ownerInstanceId && kvp.Value > maxSupport)
                {
                    maxSupport = kvp.Value;
                    opposingFactionId = kvp.Key;
                }
            }
            return opposingFactionId != null
                ? _game.GetFactionByOwnerInstanceID(opposingFactionId)
                : null;
        }
    }
}
