using System;
using System.Collections.Generic;
using System.Linq;
using Rebellion.Game;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Missions;
using Rebellion.Game.Units;
using Rebellion.Simulation;
using Rebellion.Util.Mathematics;

namespace Rebellion.AI
{
    /// <summary>
    /// Turn-scoped allocation decisions shared by strategic planners and proposals.
    /// </summary>
    public sealed class AIStrategicPlan
    {
        private readonly GameRoot _game;
        private readonly AIAssessment _assessment;
        private readonly int _targetBattleFleetCount;
        private readonly int _targetMobileCombatStrength;
        private readonly Dictionary<
            (
                string SystemId,
                IReadOnlyDictionary<Officer, Planet> Commitments,
                int CommitmentCount
            ),
            double
        > _diplomacyConcentrationValues = new();
        private readonly Dictionary<string, int> _specialForcesReserveTargets = new();
        private readonly Dictionary<BuildingType, int> _infrastructureCounts = new();
        private readonly Dictionary<BuildingType, int> _infrastructureTargets = new();
        private readonly Dictionary<BuildingType, int> _infrastructureDeficits = new();
        private readonly Dictionary<string, double> _infrastructureAcquisitionValues = new(
            StringComparer.Ordinal
        );
        private readonly Dictionary<
            string,
            Dictionary<BuildingType, int>
        > _infrastructureTargetsByPlanetId = new(StringComparer.Ordinal);
        private readonly Dictionary<string, AIPlanetDefenseCommitment> _defenseByPlanetId =
            new Dictionary<string, AIPlanetDefenseCommitment>(StringComparer.Ordinal);
        private readonly Dictionary<
            (string PlanetId, BuildingType Type, int Target, int Needed),
            IReadOnlyList<Building>
        > _resourceReclamationCache = new();
        private readonly Dictionary<
            string,
            IReadOnlyList<Building>
        > _resourceReclamationByPlanetId = new(StringComparer.Ordinal);
        private readonly Dictionary<
            string,
            (int MaintenanceHeadroom, double RawOutputPerTick, double RefinedOutputPerTick)
        > _resourceEconomyCache = new(StringComparer.Ordinal);

        /// <summary>
        /// Gets discounted maintenance growth from known neutral worlds under active diplomacy.
        /// </summary>
        public int ExpectedDiplomacyMaintenanceGain { get; }

        /// <summary>
        /// Gets economy-planning headroom without authorizing spending against unowned capacity.
        /// </summary>
        public int EconomyPlanningMaintenanceHeadroom =>
            _assessment.ProjectedEconomyMaintenanceHeadroom + ExpectedDiplomacyMaintenanceGain;

        /// <summary>
        /// Returns the number of independently deployable battle fleets allocated for this turn.
        /// Fleet count is a deployment constraint; it does not define total desired combat power.
        /// </summary>
        public int TargetBattleFleetCount => _targetBattleFleetCount;

        /// <summary>
        /// Returns total desired mobile combat power, independently of deployment fleet count.
        /// </summary>
        public int TargetMobileCombatStrength => _targetMobileCombatStrength;

        /// <summary>
        /// Returns the combat allocation for one fleet assembling without a campaign target.
        /// </summary>
        public int AssemblyFleetCombatStrength =>
            _targetBattleFleetCount > 0
                ? IntegerMath.DivideRoundedUp(_targetMobileCombatStrength, _targetBattleFleetCount)
                : 0;

        /// <summary>
        /// Returns the reserve required by a special-forces mission role.
        /// </summary>
        /// <param name="template">The role's unit template.</param>
        /// <returns>The desired role supply, excluding a dedicated headquarters agent.</returns>
        public int GetSpecialForcesReserveTarget(SpecialForces template)
        {
            string role = string.Join(
                "|",
                template.AllowedMissionTypeIDs.OrderBy(id => id, StringComparer.Ordinal)
            );
            if (_specialForcesReserveTargets.TryGetValue(role, out int target))
                return target;
            GameConfig.AIInfrastructureConfig config = _game.Config.AI.Infrastructure;
            bool sabotage = template.CanPerformMission(MissionTypeIDs.Sabotage);
            bool espionage = template.CanPerformMission(MissionTypeIDs.Espionage);
            if (sabotage || espionage)
                target =
                    (
                        sabotage
                            ? _assessment.EnemyPlanets.Sum(planet =>
                                planet
                                    .GetAllBuildings()
                                    .Count(building =>
                                        building.IsDefenseFacility()
                                        && building.ManufacturingStatus
                                            == ManufacturingStatus.Complete
                                    )
                            ) * config.SaboteursPerEnemyDefense
                            : 0
                    )
                    + (espionage ? _assessment.EnemyPlanets.Count * config.SpiesPerEnemyPlanet : 0);
            else
            {
                int missions = _assessment.ActiveMissions.Count(mission =>
                    _assessment.IsEnemyPlanet(mission.GetParentOfType<Planet>())
                    && mission.GetMainParticipants().OfType<Officer>().Any()
                    && template.AllowedMissionTypeIDs.Contains(mission.ConfigKey)
                );
                int coverage = Math.Clamp(config.SpecialForcesMissionCoveragePercent, 0, 100);
                target = (missions * coverage + 99) / 100;
            }
            _specialForcesReserveTargets.Add(role, target);
            return target;
        }

        /// <summary>
        /// Estimates discounted resource-pair growth from active diplomacy against visible neutral worlds.
        /// </summary>
        /// <returns>The expected net maintenance gain used only when prioritizing economic construction.</returns>
        private int CalculateExpectedDiplomacyMaintenanceGain()
        {
            double confidence =
                Math.Clamp(_game.Config.AI.Selection.DiplomacyEconomyConfidencePercent, 0, 100)
                / 100d;
            int threshold = _game.Config.SupportShift.OwnershipTransferThreshold;
            if (confidence <= 0 || threshold <= 0)
                return 0;

            double nodes = _assessment.ProjectedResourceNodeCapacity;
            double mines = _assessment.ProjectedMinedResourceCapacity;
            double refineries = _assessment.ProjectedRefinementCapacity;
            double maintenance = 0;
            foreach (
                IGrouping<string, Mission> group in _assessment
                    .ActiveMissions.Where(mission => mission.ConfigKey == MissionTypeIDs.Diplomacy)
                    .GroupBy(mission => mission.LocationInstanceID, StringComparer.Ordinal)
            )
            {
                Planet planet = _assessment.GetKnownPlanet(group.Key);
                if (
                    planet == null
                    || !_assessment.IsNeutralPlanet(planet)
                    || !planet.IsColonized
                    || planet.IsDestroyed
                    || planet.IsInUprising
                    || _assessment.HasKnownHostileFleet(planet)
                )
                    continue;
                double readiness = Math.Clamp(
                    (double)_assessment.GetFactionPopularSupport(planet) / threshold,
                    0,
                    1
                );
                if (readiness <= 0)
                    continue;
                double likelihood =
                    group
                        .Select(mission =>
                        {
                            IReadOnlyList<IMissionParticipant> participants =
                                mission.GetMainParticipants();
                            int transit = participants
                                .Select(participant => participant.Movement?.TicksRemaining() ?? 0)
                                .DefaultIfEmpty()
                                .Max();
                            int remaining = Math.Max(
                                0,
                                mission.MaxProgress - mission.CurrentProgress
                            );
                            return mission.GetObjectiveSuccessProbability(
                                    participants,
                                    _game,
                                    planet
                                )
                                / 100d
                                * GetDiplomacyTravelWeight(transit + remaining);
                        })
                        .DefaultIfEmpty()
                        .Max()
                    * readiness
                    * confidence;
                List<Building> buildings = _assessment
                    .GetPlanetBuildings(planet)
                    .Where(building =>
                        building.ManufacturingStatus == ManufacturingStatus.Complete
                        && building.Movement == null
                    )
                    .ToList();
                int resourceNodes = Math.Max(0, planet.GetRawResourceNodes());
                nodes += resourceNodes * likelihood;
                mines +=
                    Math.Min(
                        resourceNodes,
                        buildings.Count(building => building.BuildingType == BuildingType.Mine)
                    ) * likelihood;
                refineries +=
                    buildings.Count(building => building.BuildingType == BuildingType.Refinery)
                    * likelihood;
                maintenance +=
                    buildings.Sum(building => Math.Max(0, building.MaintenanceCost)) * likelihood;
            }
            double addedCapacity = Math.Max(
                0,
                Math.Min(nodes, Math.Min(mines, refineries)) - _assessment.ProjectedMaterialCapacity
            );
            return (int)
                Math.Min(
                    int.MaxValue,
                    Math.Max(
                        0,
                        addedCapacity * _assessment.MaintenanceCapacityPerResourcePair - maintenance
                    )
                );
        }

        /// <summary>
        /// Returns the opportunity for nearby diplomats to work separate neutral worlds in a sector.
        /// </summary>
        /// <param name="planet">The proposed diplomacy target.</param>
        /// <param name="movementQueries">The travel-duration queries.</param>
        /// <param name="participant">The proposed diplomat, or null when ordering sector opportunities.</param>
        /// <param name="commitments">Diplomats and destinations already selected during this turn.</param>
        /// <returns>A normalized value based on known targets and their existing popular support.</returns>
        public double GetDiplomacyConcentrationValue(
            Planet planet,
            MovementQueries movementQueries,
            IMissionParticipant participant = null,
            IReadOnlyDictionary<Officer, Planet> commitments = null
        )
        {
            Planet knownPlanet =
                planet == null ? null : _assessment.GetKnownPlanet(planet.InstanceID);
            if (
                knownPlanet == null
                || movementQueries == null
                || !_assessment.IsNeutralPlanet(knownPlanet)
                || !knownPlanet.IsColonized
                || knownPlanet.IsDestroyed
                || knownPlanet.IsInUprising
                || _assessment.HasKnownHostileFleet(knownPlanet)
                || _assessment.GetFactionPopularSupport(knownPlanet) <= 0
            )
                return 0;

            string systemId = _assessment.GetPlanetSystemId(knownPlanet);
            if (string.IsNullOrEmpty(systemId))
                return 0;

            var key = (systemId, commitments, commitments?.Count ?? 0);
            if (!_diplomacyConcentrationValues.TryGetValue(key, out double value))
            {
                value = CalculateDiplomacyConcentrationValue(
                    knownPlanet,
                    movementQueries,
                    commitments
                );
                _diplomacyConcentrationValues.Add(key, value);
            }
            if (participant == null)
                return value;
            if (participant.GetParentOfType<Planet>() is not Planet origin)
                return 0;

            int transitTicks =
                origin.InstanceID == knownPlanet.InstanceID
                    ? 0
                    : movementQueries.CalculateTransitTicks(participant, origin, knownPlanet);
            return value * GetDiplomacyTravelWeight(transitTicks);
        }

        /// <summary>
        /// Measures a known neutral-sector opportunity without projecting hidden defenses or ownership.
        /// </summary>
        /// <param name="planet">The sector's candidate target.</param>
        /// <param name="movementQueries">The travel-duration queries.</param>
        /// <param name="commitments">Diplomats and destinations already selected during this turn.</param>
        /// <returns>A bounded opportunity weighted by diplomatic strength, travel, and remaining work.</returns>
        private double CalculateDiplomacyConcentrationValue(
            Planet planet,
            MovementQueries movementQueries,
            IReadOnlyDictionary<Officer, Planet> commitments
        )
        {
            GameConfig.AIDiplomacyUtilityConfig utility = _game
                .Config
                .AI
                .MissionPlanning
                .Utility
                .Diplomacy;
            IReadOnlyList<Planet> known = _assessment.GetKnownSystemPlanets(
                _assessment.GetPlanetSystemId(planet)
            );
            if (known.Count == 0)
                return 0;

            List<Mission> diplomacyMissions = _assessment
                .ActiveMissions.Where(mission => mission.ConfigKey == MissionTypeIDs.Diplomacy)
                .ToList();
            HashSet<string> assignedTargets = diplomacyMissions
                .Select(mission => mission.LocationInstanceID)
                .ToHashSet(StringComparer.Ordinal);
            if (commitments != null)
                assignedTargets.UnionWith(commitments.Values.Select(target => target.InstanceID));
            List<Planet> targets = known
                .Where(candidate =>
                    candidate.IsColonized
                    && !candidate.IsInUprising
                    && !candidate.IsDestroyed
                    && _assessment.IsNeutralPlanet(candidate)
                    && _assessment.GetFactionPopularSupport(candidate) > 0
                    && _assessment.GetFactionPopularSupport(candidate) < 100
                    && !_assessment.HasKnownHostileFleet(candidate)
                )
                .ToList();
            int openTargetCount = targets.Count(target =>
                !assignedTargets.Contains(target.InstanceID)
            );
            if (openTargetCount == 0)
                return 0;

            IEnumerable<double> availableStrength = _assessment
                .AvailableMissionParticipants.OfType<Officer>()
                .Where(officer =>
                    commitments?.ContainsKey(officer) != true
                    && officer.CanPerformMission(MissionTypeIDs.Diplomacy)
                    && officer.GetEffectiveRating(SkillRating.Diplomacy)
                        >= _game.Config.AI.DiplomacyMinimumSkill
                    && officer.GetParentOfType<Planet>() != null
                )
                .Select(officer =>
                    Math.Max(0, officer.GetEffectiveRating(SkillRating.Diplomacy))
                    * GetDiplomacyTravelWeight(
                        targets.Min(target =>
                            officer.GetParentOfType<Planet>().InstanceID == target.InstanceID
                                ? 0
                                : movementQueries.CalculateTransitTicks(
                                    officer,
                                    officer.GetParentOfType<Planet>(),
                                    target
                                )
                        )
                    )
                )
                .OrderByDescending(strength => strength)
                .Take(openTargetCount);
            double committedStrength = diplomacyMissions
                .Where(mission =>
                    _assessment
                        .GetKnownPlanet(mission.LocationInstanceID)
                        ?.GetParentOfType<PlanetSector>()
                        ?.InstanceID == _assessment.GetPlanetSystemId(planet)
                )
                .SelectMany(mission => mission.GetMainParticipants())
                .OfType<Officer>()
                .GroupBy(officer => officer.InstanceID, StringComparer.Ordinal)
                .Select(group => group.First())
                .Sum(officer =>
                    Math.Max(0, officer.GetEffectiveRating(SkillRating.Diplomacy))
                    * GetDiplomacyTravelWeight(
                        officer.Movement == null
                            ? 0
                            : Math.Max(
                                0,
                                officer.Movement.TransitTicks - officer.Movement.TicksElapsed
                            )
                    )
                );
            if (commitments != null)
                committedStrength += commitments
                    .Where(pair =>
                        _assessment.GetPlanetSystemId(pair.Value)
                        == _assessment.GetPlanetSystemId(planet)
                    )
                    .Sum(pair =>
                        Math.Max(0, pair.Key.GetEffectiveRating(SkillRating.Diplomacy))
                        * GetDiplomacyTravelWeight(
                            pair.Key.GetParentOfType<Planet>()?.InstanceID == pair.Value.InstanceID
                                ? 0
                                : movementQueries.CalculateTransitTicks(
                                    pair.Key,
                                    pair.Key.GetParentOfType<Planet>(),
                                    pair.Value
                                )
                        )
                    );
            double strength = availableStrength.Sum() + committedStrength;
            double concentration =
                strength / (strength + Math.Max(1, utility.Concentration.SaturationValue));

            int planetCount =
                planet.GetParentOfType<PlanetSector>()?.GetChildren<Planet>().Count ?? 0;
            int ownershipThreshold = _game.Config.SupportShift.OwnershipTransferThreshold;
            if (planetCount == 0 || ownershipThreshold <= 0)
                return 0;

            double supportReadiness = targets.Average(target =>
                Math.Min(
                    1,
                    (double)_assessment.GetFactionPopularSupport(target) / ownershipThreshold
                )
            );
            double uncontestedShare =
                1 - (double)known.Count(_assessment.IsEnemyPlanet) / planetCount;
            return (double)targets.Count
                / planetCount
                * supportReadiness
                * concentration
                * uncontestedShare;
        }

        /// <summary>
        /// Reduces diplomatic influence gradually as its travel time increases.
        /// </summary>
        /// <param name="transitTicks">The remaining or proposed travel duration.</param>
        /// <returns>A travel weight from zero through one, without a maximum journey cutoff.</returns>
        private double GetDiplomacyTravelWeight(int transitTicks)
        {
            double scale = Math.Max(
                1,
                _game.Config.AI.MissionPlanning.Utility.Diplomacy.TravelTime.SaturationValue
            );
            return scale / (scale + Math.Max(0, transitTicks));
        }

        /// <summary>
        /// Finds the smallest complete resource retirement that makes a requested hub fit.
        /// </summary>
        /// <param name="planet">The prospective industrial destination.</param>
        /// <param name="buildingType">The requested production category.</param>
        /// <param name="targetCount">The full hub target.</param>
        /// <param name="refresh">Whether live state must be reevaluated before execution.</param>
        /// <returns>The exact facilities to reclaim, or none if the whole expansion cannot fit safely.</returns>
        internal IReadOnlyList<Building> GetResourceFacilitiesToReclaim(
            Planet planet,
            BuildingType buildingType,
            int targetCount,
            bool refresh = false
        )
        {
            if (
                !IsInfrastructureType(buildingType)
                || planet == null
                || !_assessment.IsOwnedPlanet(planet)
                || !planet.IsColonized
                || planet.IsDestroyed
                || planet.IsInUprising
                || planet.IsBlockadedFor(planet.GetOwnerInstanceID())
            )
                return Array.Empty<Building>();
            int needed = Math.Max(
                0,
                targetCount
                    - planet.GetTotalBuildingTypeCount(buildingType)
                    - GetConstructionSpace(planet, buildingType, false)
            );
            if (needed == 0)
                return Array.Empty<Building>();
            var key = (planet.InstanceID, buildingType, targetCount, needed);
            if (!refresh && _resourceReclamationCache.TryGetValue(key, out var cached))
                return cached;
            if (refresh)
                _resourceEconomyCache.Clear();
            if (
                _assessment.ProjectedMaintenanceHeadroom
                < _game.Config.AI.Selection.MaintenanceHeadroomTarget
            )
            {
                _resourceReclamationCache[key] = Array.Empty<Building>();
                return Array.Empty<Building>();
            }
            List<Building> candidates = planet
                .GetChildren<Building>()
                .Where(building =>
                    building.BuildingType is BuildingType.Mine or BuildingType.Refinery
                    && building.GetOwnerInstanceID() == planet.GetOwnerInstanceID()
                    && building.IsEnabled
                    && building.ManufacturingStatus == ManufacturingStatus.Complete
                    && building.Movement == null
                )
                .OrderByDescending(building =>
                    building.BuildingType == BuildingType.Mine
                        ? _assessment.AvailableMinedResourceCapacity
                            - _assessment.AvailableMaterialCapacity
                        : _assessment.AvailableRefinementCapacity
                            - _assessment.AvailableMaterialCapacity
                )
                .ThenByDescending(building => building.ProcessRate)
                .ThenBy(building => building.InstanceID, StringComparer.Ordinal)
                .ToList();
            if (candidates.Count < needed)
            {
                _resourceReclamationCache[key] = Array.Empty<Building>();
                return Array.Empty<Building>();
            }
            List<Building> selected = new();
            List<Building> otherRetirements = _resourceReclamationByPlanetId
                .Where(entry => entry.Key != planet.InstanceID)
                .SelectMany(entry => entry.Value)
                .ToList();
            ResourceProductionQueries economy = new(_game);
            var faction = _game.GetFactionByOwnerInstanceID(planet.GetOwnerInstanceID());
            int missingFacilities = Math.Max(
                0,
                targetCount - planet.GetTotalBuildingTypeCount(buildingType)
            );
            List<Building> templates = faction
                .GetUnlockedTechnologies(ManufacturingType.Building)
                .Select(technology => technology.GetReference())
                .OfType<Building>()
                .Where(building => building.BuildingType == buildingType)
                .ToList();
            if (templates.Count == 0)
            {
                _resourceReclamationCache[key] = Array.Empty<Building>();
                return Array.Empty<Building>();
            }
            int hubMaintenance =
                missingFacilities * templates.Max(building => building.MaintenanceCost);
            int hubMaterials =
                missingFacilities * templates.Max(building => building.ConstructionCost);
            foreach (Building candidate in candidates)
            {
                var excluded = otherRetirements.Concat(selected).Append(candidate).ToList();
                string economyKey = string.Join(
                    "|",
                    excluded
                        .Select(building => building.InstanceID)
                        .Distinct()
                        .OrderBy(id => id, StringComparer.Ordinal)
                );
                if (!_resourceEconomyCache.TryGetValue(economyKey, out var after))
                {
                    after = economy.GetOperatingEconomy(faction, excluded);
                    _resourceEconomyCache[economyKey] = after;
                }
                int horizon = Math.Max(
                    0,
                    _game.Config.AI.Selection.RefinedMaterialCommitmentHorizonTicks
                );
                double production = Math.Min(after.RawOutputPerTick, after.RefinedOutputPerTick);
                double materials =
                    faction.RefinedMaterialStockpile
                    + production * horizon
                    - _assessment.NearTermRefinedMaterialCommitment
                    - hubMaterials;
                double materialReserve =
                    _assessment.RefinedMaterialSupply
                    * _game.Config.AI.Selection.RefinedMaterialEconomyWarningPercent
                    / 100d;
                if (
                    after.MaintenanceHeadroom - hubMaintenance
                        < _game.Config.AI.Selection.MaintenanceHeadroomTarget
                    || after.RawOutputPerTick <= 0
                    || after.RefinedOutputPerTick <= 0
                    || materials < materialReserve
                )
                    continue;
                selected.Add(candidate);
                if (selected.Count == needed)
                    break;
            }
            IReadOnlyList<Building> result =
                selected.Count == needed ? selected : Array.Empty<Building>();
            _resourceReclamationCache[key] = result;
            return result;
        }

        /// <summary>
        /// Returns capacity available to a full hub after affordable resource reclamation.
        /// </summary>
        /// <param name="planet">The proposed hub destination.</param>
        /// <param name="buildingType">The production category being assigned.</param>
        /// <param name="targetCount">The full hub target.</param>
        /// <returns>Actual free capacity plus the exact eligible resource reclamation.</returns>
        internal int GetReclaimableInfrastructureSpace(
            Planet planet,
            BuildingType buildingType,
            int targetCount
        )
        {
            return GetAvailableConstructionSpace(planet, buildingType)
                + GetResourceFacilitiesToReclaim(planet, buildingType, targetCount).Count;
        }

        /// <summary>
        /// Returns the projected owned count for one production-infrastructure category.
        /// </summary>
        /// <param name="buildingType">Infrastructure category to inspect.</param>
        /// <returns>The completed and committed facility count.</returns>
        public int GetInfrastructureCount(BuildingType buildingType)
        {
            return _infrastructureCounts.TryGetValue(buildingType, out int count) ? count : 0;
        }

        /// <summary>
        /// Returns the strategic target for one production-infrastructure category.
        /// </summary>
        /// <param name="buildingType">Infrastructure category to inspect.</param>
        /// <returns>The faction-wide target count.</returns>
        public int GetInfrastructureTarget(BuildingType buildingType)
        {
            return _infrastructureTargets.TryGetValue(buildingType, out int target) ? target : 0;
        }

        /// <summary>
        /// Returns the remaining strategic deficit for one production-infrastructure category.
        /// </summary>
        /// <param name="buildingType">Infrastructure category to inspect.</param>
        /// <returns>The nonnegative target shortfall.</returns>
        public int GetInfrastructureDeficit(BuildingType buildingType)
        {
            return _infrastructureDeficits.TryGetValue(buildingType, out int deficit) ? deficit : 0;
        }

        /// <summary>
        /// Returns the ordered per-planet hub targets for one production-infrastructure category.
        /// </summary>
        /// <param name="buildingType">Infrastructure category to inspect.</param>
        /// <returns>The primary target followed by any secondary target.</returns>
        public IReadOnlyList<int> GetInfrastructureHubTargets(BuildingType buildingType)
        {
            GameConfig.AIInfrastructureConfig config = _game.Config.AI.Infrastructure;
            return buildingType switch
            {
                BuildingType.ConstructionFacility => new[]
                {
                    config.ConstructionFacilityPrimaryHubTargetCount,
                    config.ConstructionFacilitySecondaryHubTargetCount,
                },
                BuildingType.Shipyard => new[]
                {
                    config.ShipyardPrimaryHubTargetCount,
                    config.ShipyardSecondaryHubTargetCount,
                },
                BuildingType.TrainingFacility => new[] { config.TrainingFacilityHubTargetCount },
                _ => Array.Empty<int>(),
            };
        }

        /// <summary>
        /// Returns the remaining hub deficit for one sector and infrastructure category.
        /// </summary>
        /// <param name="planets">Eligible owned planets in the sector.</param>
        /// <param name="buildingType">Infrastructure category to inspect.</param>
        /// <returns>The facilities still required on the sector's strongest hub planets.</returns>
        public int GetInfrastructureDeficit(IEnumerable<Planet> planets, BuildingType buildingType)
        {
            List<int> facilityCounts =
                planets
                    ?.Where(planet => planet != null)
                    .Select(planet => planet.GetTotalBuildingTypeCount(buildingType))
                    .OrderByDescending(count => count)
                    .ToList()
                ?? new List<int>();
            IReadOnlyList<int> hubTargets = GetInfrastructureHubTargets(buildingType);
            int hubCount = Math.Min(facilityCounts.Count, hubTargets.Count);
            int deficit = 0;
            for (int index = 0; index < hubCount; index++)
                deficit += Math.Max(0, hubTargets[index] - facilityCounts[index]);

            return deficit;
        }

        /// <summary>
        /// Returns owned planets that can host one infrastructure category.
        /// </summary>
        /// <param name="buildingType">Infrastructure category to inspect.</param>
        /// <returns>The eligible owned planets.</returns>
        public IEnumerable<Planet> GetInfrastructurePlanets(BuildingType buildingType)
        {
            return _assessment.OwnedPlanets.Where(planet =>
                planet?.IsDestroyed == false
                && (
                    planet.IsColonized
                    || (
                        buildingType == BuildingType.ConstructionFacility
                        && planet.GetParentOfType<PlanetSector>()?.SectorType
                            == PlanetSectorType.OuterRim
                    )
                )
            );
        }

        /// <summary>
        /// Returns the normalized strategic deficit for one production-infrastructure category.
        /// </summary>
        /// <param name="buildingType">Infrastructure category to inspect.</param>
        /// <returns>The target shortfall from zero through one.</returns>
        public double GetInfrastructureDeficitRatio(BuildingType buildingType)
        {
            int target = GetInfrastructureTarget(buildingType);
            return target > 0 ? GetInfrastructureDeficit(buildingType) / (double)target : 0;
        }

        /// <summary>
        /// Values known neutral industrial capacity and missing hub sites in established sectors.
        /// </summary>
        /// <param name="planet">The neutral planet being considered for acquisition.</param>
        /// <returns>The greatest industrial benefit from zero through one.</returns>
        public double GetInfrastructureAcquisitionValue(Planet planet)
        {
            planet = _assessment.GetKnownPlanet(planet?.InstanceID);
            if (
                planet?.IsDestroyed != false
                || !planet.IsColonized
                || !_assessment.IsNeutralPlanet(planet)
                || _assessment.GetHostileFleets(planet).Any()
            )
                return 0;
            if (_infrastructureAcquisitionValues.TryGetValue(planet.InstanceID, out double value))
                return value;

            string systemId = _assessment.GetPlanetSystemId(planet);
            foreach (BuildingType type in _infrastructureTargets.Keys)
            {
                List<Planet> owned = GetInfrastructurePlanets(type)
                    .Where(candidate => _assessment.GetPlanetSystemId(candidate) == systemId)
                    .ToList();
                int target = GetInfrastructureHubTargets(type).FirstOrDefault(count => count > 0);
                int deficit = GetInfrastructureDeficit(owned, type);
                if (target <= 0 || deficit <= 0)
                    continue;

                ManufacturingType productionType = type switch
                {
                    BuildingType.Shipyard => ManufacturingType.Ship,
                    BuildingType.TrainingFacility => ManufacturingType.Troop,
                    _ => ManufacturingType.Building,
                };
                double benefit = Math.Min(
                    1,
                    Math.Min(planet.GetProductionFacilityCount(productionType), deficit)
                        / (double)target
                );
                if (
                    !owned.Any(candidate => CanAllocateInfrastructureHub(candidate, type, target))
                    && GetConstructionSpace(planet, type, false, acquiring: true)
                        >= Math.Max(0, target - planet.GetTotalBuildingTypeCount(type))
                )
                    benefit = 1;
                value = Math.Max(value, benefit * Math.Min(1, deficit / (double)target));
            }
            _infrastructureAcquisitionValues[planet.InstanceID] = value;
            return value;
        }

        /// <summary>
        /// Returns the industrial target assigned to one planet for this turn.
        /// </summary>
        /// <param name="planet">The planned industrial destination.</param>
        /// <param name="buildingType">The production category.</param>
        /// <returns>The assigned target, or zero when this category has no allocation.</returns>
        public int GetAllocatedInfrastructureTarget(Planet planet, BuildingType buildingType)
        {
            return
                planet != null
                && _infrastructureTargetsByPlanetId.TryGetValue(planet.InstanceID, out var targets)
                && targets.TryGetValue(buildingType, out int target)
                ? target
                : 0;
        }

        /// <summary>
        /// Identifies the next sector investment from built, queued, and selected primary hub capacity.
        /// </summary>
        /// <param name="destination">A planet identifying the sector to develop.</param>
        /// <param name="selectedBuildings">Additional facilities reserved during this selection pass.</param>
        /// <returns>The next facility category and the fraction of the paired hubs already covered.</returns>
        internal (BuildingType NextType, double Completion) GetInfrastructureDevelopmentStep(
            Planet destination,
            IReadOnlyDictionary<string, Dictionary<BuildingType, int>> selectedBuildings
        )
        {
            if (destination == null)
                return default;

            string sectorId = _assessment.GetPlanetSystemId(destination);
            List<Planet> planets = _assessment
                .OwnedPlanets.Where(planet => _assessment.GetPlanetSystemId(planet) == sectorId)
                .ToList();
            var construction = GetPrimaryInfrastructureProgress(
                planets,
                BuildingType.ConstructionFacility,
                selectedBuildings
            );
            var shipyards = GetPrimaryInfrastructureProgress(
                planets,
                BuildingType.Shipyard,
                selectedBuildings
            );
            if (construction.Target <= 0 || shipyards.Target <= 0)
                return default;

            BuildingType next =
                construction.Committed < construction.Target ? BuildingType.ConstructionFacility
                : shipyards.Committed < shipyards.Target ? BuildingType.Shipyard
                : BuildingType.None;
            double completion =
                (
                    Math.Min(1, construction.Committed / (double)construction.Target)
                    + Math.Min(1, shipyards.Committed / (double)shipyards.Target)
                ) / 2;
            return (next, completion);
        }

        /// <summary>
        /// Measures the strongest feasible primary hub, including queued and selected facilities.
        /// </summary>
        /// <param name="planets">Owned planets in one sector.</param>
        /// <param name="buildingType">The industrial category to measure.</param>
        /// <param name="selectedBuildings">Additional facilities reserved during this selection pass.</param>
        /// <returns>The committed count and achievable primary target.</returns>
        private (int Committed, int Target) GetPrimaryInfrastructureProgress(
            IEnumerable<Planet> planets,
            BuildingType buildingType,
            IReadOnlyDictionary<string, Dictionary<BuildingType, int>> selectedBuildings
        )
        {
            int preferred = GetInfrastructureHubTargets(buildingType).FirstOrDefault();
            List<Planet> candidates = planets
                .Where(planet => !planet.IsDestroyed && planet.IsColonized)
                .ToList();
            bool hasAllocation = candidates.Any(planet =>
                GetAllocatedInfrastructureTarget(planet, buildingType) > 0
            );
            (int Committed, int Target) best = default;
            foreach (Planet planet in candidates)
            {
                int target = GetAllocatedInfrastructureTarget(planet, buildingType);
                int committed = planet.GetTotalBuildingTypeCount(buildingType);
                if (
                    selectedBuildings != null
                    && selectedBuildings.TryGetValue(planet.InstanceID, out var selected)
                    && selected.TryGetValue(buildingType, out int count)
                )
                    committed += count;
                if (preferred > 0 && committed >= preferred)
                    return (committed, preferred);
                if (hasAllocation && target <= 0)
                    continue;
                if (target <= 0)
                    target = GetAchievableInfrastructureHubTarget(planet, buildingType, preferred);
                if (target > best.Target || target == best.Target && committed > best.Committed)
                    best = (committed, target);
            }
            return best;
        }

        /// <summary>
        /// Returns the preferred facility count capped by the space available for its layout.
        /// </summary>
        /// <param name="planet">The proposed hub destination.</param>
        /// <param name="buildingType">The production category being assigned.</param>
        /// <param name="preferredCount">The preferred completed facility count.</param>
        /// <returns>The achievable count, including facilities already committed to the planet.</returns>
        public int GetAchievableInfrastructureHubTarget(
            Planet planet,
            BuildingType buildingType,
            int preferredCount
        )
        {
            int committed = planet?.GetTotalBuildingTypeCount(buildingType) ?? 0;
            return Math.Min(
                Math.Max(0, preferredCount),
                committed + GetAvailableConstructionSpace(planet, buildingType)
            );
        }

        /// <summary>
        /// Checks whether an industrial hub and its required defenses fit together.
        /// </summary>
        /// <param name="planet">The proposed hub destination.</param>
        /// <param name="buildingType">The production category being assigned.</param>
        /// <param name="targetCount">The finished industrial target.</param>
        /// <returns>True when the whole allocation fits without removing completed buildings.</returns>
        public bool CanAllocateInfrastructureHub(
            Planet planet,
            BuildingType buildingType,
            int targetCount
        )
        {
            return IsInfrastructureType(buildingType)
                && targetCount > 0
                && planet?.IsDestroyed == false
                && _assessment.IsOwnedPlanet(planet)
                && GetConstructionSpace(planet, buildingType, false)
                    >= Math.Max(0, targetCount - planet.GetTotalBuildingTypeCount(buildingType));
        }

        /// <summary>
        /// Allocates the finished industrial target while retaining its defense space.
        /// </summary>
        /// <param name="planet">The chosen hub destination.</param>
        /// <param name="buildingType">The production category being assigned.</param>
        /// <param name="targetCount">The finished industrial target.</param>
        /// <param name="reclaimResources">Whether surplus resources may be reserved for retirement.</param>
        /// <returns>True when the allocation fits and has been recorded.</returns>
        public bool TryAllocateInfrastructureHub(
            Planet planet,
            BuildingType buildingType,
            int targetCount,
            bool reclaimResources = false
        )
        {
            IReadOnlyList<Building> resources = Array.Empty<Building>();
            if (
                !CanAllocateInfrastructureHub(planet, buildingType, targetCount) && reclaimResources
            )
                resources = GetResourceFacilitiesToReclaim(planet, buildingType, targetCount);
            if (
                !CanAllocateInfrastructureHub(planet, buildingType, targetCount)
                && resources.Count == 0
            )
                return false;
            if (!_infrastructureTargetsByPlanetId.TryGetValue(planet.InstanceID, out var targets))
            {
                targets = new Dictionary<BuildingType, int>();
                _infrastructureTargetsByPlanetId.Add(planet.InstanceID, targets);
            }
            targets[buildingType] = targetCount;
            if (resources.Count > 0)
            {
                _resourceReclamationByPlanetId[planet.InstanceID] = resources;
                _resourceReclamationCache.Clear();
            }
            return true;
        }

        /// <summary>
        /// Returns space usable by a building without consuming the rest of the planned layout.
        /// </summary>
        /// <param name="planet">The construction destination.</param>
        /// <param name="buildingType">The building category being placed.</param>
        /// <param name="isEmergencyDefense">Whether an immediate defense may precede unfinished industry.</param>
        /// <param name="selectedBuildings">Building counts already accepted for this destination during selection.</param>
        /// <returns>The uncommitted space available to this building category.</returns>
        public int GetAvailableConstructionSpace(
            Planet planet,
            BuildingType buildingType,
            bool isEmergencyDefense = false,
            IReadOnlyDictionary<BuildingType, int> selectedBuildings = null
        )
        {
            return Math.Max(
                0,
                GetConstructionSpace(planet, buildingType, isEmergencyDefense, selectedBuildings)
            );
        }

        /// <summary>
        /// Returns the ground reserve sufficient to release fleets from a political hold.
        /// </summary>
        /// <param name="planet">The owned planet.</param>
        /// <returns>The stability requirement with a reserve against garrison losses.</returns>
        public int GetPlanetaryGarrisonReserve(Planet planet)
        {
            if (!_assessment.IsOwnedPlanet(planet))
                return 0;

            int stabilityTarget = UprisingQueries.CalculateGarrisonRequirement(
                planet,
                _game.GetFactionByOwnerInstanceID(planet.GetOwnerInstanceID()),
                _game.Config.AI.Garrison
            );
            int reserve = stabilityTarget > 0 ? stabilityTarget + 1 : 0;
            if (_assessment.HasEnemyControlSupport(planet))
                reserve = Math.Max(reserve, 2);
            return planet.IsColonized ? reserve : Math.Max(1, reserve);
        }

        /// <summary>
        /// Returns the preferred garrison size for planetary troop production.
        /// </summary>
        /// <param name="planet">The owned planet.</param>
        /// <returns>The production target, including the ground reserve and capture allocation.</returns>
        public int GetPlanetaryGarrisonTarget(Planet planet)
        {
            if (!_assessment.IsOwnedPlanet(planet))
                return 0;

            int captureFloor = _game.Config.Combat.PlanetaryAssault.CaptureGarrisonCount;
            if (!planet.IsHeadquarters && !_assessment.IsPlanetThreatened(planet))
                captureFloor = IntegerMath.ScaleByPercent(
                    captureFloor,
                    _game.Config.AI.Garrison.InteriorCaptureFloorPercent
                );
            return Math.Max(GetPlanetaryGarrisonReserve(planet), captureFloor);
        }

        /// <summary>
        /// Returns the planetary fighter reserve for current industry and known threats.
        /// </summary>
        /// <param name="planet">The owned planet being protected.</param>
        /// <returns>The desired squadron count.</returns>
        public int GetPlanetaryStarfighterTarget(Planet planet)
        {
            GameConfig.AINonCapitalSummaryConfig config = _game.Config.AI.NonCapitalSummary;
            bool hasShipProduction =
                _assessment.GetPlanetProductionFacilityCount(planet, ManufacturingType.Ship) > 0;
            int baseline =
                planet.IsHeadquarters ? config.StarfighterRequirementHeadquarters
                : hasShipProduction ? config.StarfighterRequirementInfrastructure
                : config.StarfighterRequirementDefault;
            if (
                !planet.IsHeadquarters
                && !_assessment.IsPlanetThreatened(planet)
                && !hasShipProduction
            )
                baseline = IntegerMath.ScaleByPercent(
                    baseline,
                    config.InteriorStarfighterBaselinePercent
                );
            var faction = _game.GetFactionByOwnerInstanceID(planet.GetOwnerInstanceID());
            int strength = faction
                .GetUnlockedTechnologies(ManufacturingType.Ship)
                .Select(technology => technology.GetReference())
                .OfType<Starfighter>()
                .Where(fighter => IManufacturable.CanBeManufacturedBy(fighter, faction.InstanceID))
                .Select(fighter => fighter.GetWeaponStrength())
                .DefaultIfEmpty()
                .Max();
            return baseline
                + (
                    strength > 0
                        ? IntegerMath.DivideRoundedUp(GetPlanetDefenseStrength(planet), strength)
                        : 0
                );
        }

        /// <summary>
        /// Returns the shield target for a planet's current or planned strategic role.
        /// </summary>
        /// <param name="planet">The owned planet.</param>
        /// <param name="plannedInfrastructure">Whether the proposed layout adds production infrastructure.</param>
        /// <returns>The required shield-generator count.</returns>
        public int GetPlanetaryShieldTarget(Planet planet, bool plannedInfrastructure = false)
        {
            if (
                _game?.Config == null
                || planet?.IsDestroyed != false
                || !_assessment.IsOwnedPlanet(planet)
            )
                return 0;
            int limit = _game.Config.Combat.PlanetaryAssault.ShieldGeneratorLimit;
            if (
                _assessment.IsPriorityDefensePlanet(planet)
                || _assessment.IsPlanetThreatened(planet)
                || _assessment.GetHostileFleets(planet).Any()
                || GetDefenseCommitment(planet).HoldAllLocalFleets
                    && _assessment
                        .GetFriendlyFleets(planet)
                        .Any(fleet =>
                            fleet.RoleType == FleetRoleType.Battle
                            && fleet.Movement == null
                            && !fleet.IsInCombat
                            && fleet.HasOperationalCapitalShips()
                        )
            )
                return limit;
            bool hasInfrastructure =
                plannedInfrastructure
                || _infrastructureTargetsByPlanetId.ContainsKey(planet.InstanceID)
                || planet
                    .GetAllBuildings()
                    .Any(building =>
                        building.GetOwnerInstanceID() == planet.GetOwnerInstanceID()
                        && IsInfrastructureType(building.BuildingType)
                    );
            if (
                _assessment.GetFactionPopularSupport(planet)
                    < _game.Config.AI.Garrison.SupportThreshold
                || _assessment.GetDefensiveSupportRisk(planet) > 0
            )
                return Math.Min(1, limit);
            return hasInfrastructure
                ? Math.Clamp(_game.Config.AI.Infrastructure.IndustrialShieldTargetCount, 0, limit)
                : 0;
        }

        /// <summary>
        /// Returns the battery target for a planet's strategic exposure.
        /// </summary>
        /// <param name="planet">The owned planet.</param>
        /// <returns>The configured battery target when the planet requires protection, otherwise zero.</returns>
        public int GetPlanetaryWeaponTarget(Planet planet)
        {
            return
                _game?.Config != null
                && planet?.IsDestroyed == false
                && _assessment.IsOwnedPlanet(planet)
                && (
                    _assessment.IsPriorityDefensePlanet(planet)
                    || _assessment.IsPlanetThreatened(planet)
                    || _assessment.GetHostileFleets(planet).Any()
                )
                ? _game.Config.AI.Infrastructure.PlanetaryWeaponTargetCount
                : 0;
        }

        /// <summary>
        /// Calculates remaining space after the other industrial and defensive commitments.
        /// </summary>
        /// <param name="planet">The construction destination.</param>
        /// <param name="buildingType">The building category being placed.</param>
        /// <param name="isEmergencyDefense">Whether an immediate defense takes priority over unfinished industry.</param>
        /// <param name="selectedBuildings">Building counts already accepted for this destination.</param>
        /// <param name="acquiring">Whether to evaluate a neutral planet's industrial layout after acquisition.</param>
        /// <returns>The signed remaining space before clamping an overcommitted layout.</returns>
        private int GetConstructionSpace(
            Planet planet,
            BuildingType buildingType,
            bool isEmergencyDefense,
            IReadOnlyDictionary<BuildingType, int> selectedBuildings = null,
            bool acquiring = false
        )
        {
            if (
                _game?.Config == null
                || planet?.IsDestroyed != false
                || !(
                    _assessment.IsOwnedPlanet(planet)
                    || acquiring && _assessment.IsNeutralPlanet(planet)
                )
            )
                return 0;
            int available = _assessment.GetAvailableBuildingSpace(planet);
            available -= selectedBuildings?.Values.Sum() ?? 0;
            if (
                !isEmergencyDefense
                && _infrastructureTargetsByPlanetId.TryGetValue(planet.InstanceID, out var targets)
            )
                foreach (var target in targets)
                    if (target.Key != buildingType)
                        available -= Math.Max(
                            0,
                            target.Value
                                - planet.GetTotalBuildingTypeCount(target.Key)
                                - GetSelectedBuildingCount(selectedBuildings, target.Key)
                        );

            bool protectsLayout =
                IsInfrastructureType(buildingType)
                || _infrastructureTargetsByPlanetId.ContainsKey(planet.InstanceID)
                || planet
                    .GetAllBuildings()
                    .Any(building =>
                        building.GetOwnerInstanceID() == planet.GetOwnerInstanceID()
                        && IsInfrastructureType(building.BuildingType)
                    )
                || _assessment.IsPriorityDefensePlanet(planet)
                || _assessment.IsPlanetThreatened(planet)
                || _assessment.GetHostileFleets(planet).Any();
            int shieldDeficit = protectsLayout
                ? Math.Max(
                    0,
                    (
                        acquiring
                            ? Math.Clamp(
                                _game.Config.AI.Infrastructure.IndustrialShieldTargetCount,
                                0,
                                _game.Config.Combat.PlanetaryAssault.ShieldGeneratorLimit
                            )
                            : GetPlanetaryShieldTarget(planet, IsInfrastructureType(buildingType))
                    )
                        - CountCommittedDefense(planet, true)
                        - GetSelectedBuildingCount(selectedBuildings, BuildingType.Defense)
                )
                : 0;
            int weaponDeficit = protectsLayout
                ? Math.Max(
                    0,
                    GetPlanetaryWeaponTarget(planet)
                        - CountCommittedDefense(planet, false)
                        - GetSelectedBuildingCount(selectedBuildings, BuildingType.Weapon)
                )
                : 0;
            if (buildingType != BuildingType.Defense)
                available -= shieldDeficit;
            if (buildingType is not BuildingType.Defense and not BuildingType.Weapon)
                available -= weaponDeficit;
            return available;
        }

        /// <summary>
        /// Counts defenses whose occupied space cannot be reclaimed by another order.
        /// </summary>
        /// <param name="planet">The defense destination.</param>
        /// <param name="shield">Whether to count shield generators instead of batteries.</param>
        /// <returns>The completed and protected queued defense count.</returns>
        private int CountCommittedDefense(Planet planet, bool shield)
        {
            return planet
                .GetAllBuildings()
                .Count(building =>
                {
                    if (
                        building.GetOwnerInstanceID() != planet.GetOwnerInstanceID()
                        || (
                            shield
                                ? !building.IsPlanetaryShieldGenerator()
                                : building.BuildingType != BuildingType.Weapon
                        )
                    )
                        return false;
                    if (
                        !building.IsDefenseFacility()
                        || string.IsNullOrEmpty(building.ProducerPlanetID)
                    )
                        return true;
                    Planet producer = _game.GetSceneNodeByInstanceID<Planet>(
                        building.ProducerPlanetID
                    );
                    return !ReferenceEquals(
                        building,
                        _assessment.GetReplaceableManufacturingOrder(
                            producer,
                            ManufacturingType.Building
                        )
                    );
                });
        }

        /// <summary>
        /// Returns the accepted count for one building category.
        /// </summary>
        /// <param name="selectedBuildings">Accepted destination building counts.</param>
        /// <param name="buildingType">The building category.</param>
        /// <returns>The accepted count, or zero when none have been selected.</returns>
        private static int GetSelectedBuildingCount(
            IReadOnlyDictionary<BuildingType, int> selectedBuildings,
            BuildingType buildingType
        )
        {
            return
                selectedBuildings != null
                && selectedBuildings.TryGetValue(buildingType, out int count)
                ? count
                : 0;
        }

        /// <summary>
        /// Identifies the industrial categories that require planned defense space.
        /// </summary>
        /// <param name="buildingType">The building category.</param>
        /// <returns>True for construction, shipbuilding, and troop-training infrastructure.</returns>
        private static bool IsInfrastructureType(BuildingType buildingType)
        {
            return buildingType
                is BuildingType.ConstructionFacility
                    or BuildingType.Shipyard
                    or BuildingType.TrainingFacility;
        }

        /// <summary>
        /// Returns the fleet strength allocated to defend a planet.
        /// </summary>
        /// <param name="planet">Planet to inspect.</param>
        /// <returns>The required defense strength.</returns>
        public int GetDefenseStrength(Planet planet)
        {
            return _assessment.IsPriorityDefensePlanet(planet)
                ? GetHeadquartersDefenseStrength(planet)
                : GetPlanetDefenseStrength(planet);
        }

        /// <summary>
        /// Returns the fleet strength allocated to defend a headquarters planet.
        /// </summary>
        /// <param name="planet">Headquarters planet to inspect.</param>
        /// <returns>The required defense strength.</returns>
        public int GetHeadquartersDefenseStrength(Planet planet)
        {
            if (!_assessment.IsPriorityDefensePlanet(planet) || _game?.Config == null)
                return 0;

            var config = _game.Config.AI.FleetDeployment;
            int hostileFleetRequirement = IntegerMath.ScaleByPercent(
                _assessment.GetStrongestKnownHostileFleetStrength(),
                config.HeadquartersDefensePercentOfStrongestHostileFleet > 0
                    ? config.HeadquartersDefensePercentOfStrongestHostileFleet
                    : config.AttackStrengthPercentOfStrongestHostileFleet
            );
            int affordableDefense = IntegerMath.ScaleByPercent(
                _assessment.GetTotalFleetCombatStrength(),
                config.HeadquartersDefenseCombatPercent
            );
            int defenseTarget = Math.Min(
                hostileFleetRequirement,
                Math.Max(config.MinimumDefenseStrength, affordableDefense)
            );
            return Math.Max(config.MinimumDefenseStrength, defenseTarget);
        }

        /// <summary>
        /// Returns the fleet strength allocated to defend an ordinary planet.
        /// </summary>
        /// <param name="planet">Planet to inspect.</param>
        /// <returns>The required defense strength.</returns>
        public int GetPlanetDefenseStrength(Planet planet)
        {
            if (!_assessment.IsOwnedPlanet(planet) || _game?.Config == null)
                return 0;

            int hostileStrength = _assessment.GetPlanetDefenseThreatStrength(planet);
            return hostileStrength > 0
                ? IntegerMath.ScaleByPercent(
                    hostileStrength,
                    _game.Config.AI.FleetDeployment.AttackStrengthPercentOfStrongestHostileFleet
                )
                : 0;
        }

        /// <summary>
        /// Returns whether a fleet satisfies a planet's defense allocation.
        /// </summary>
        /// <param name="fleet">Candidate defense fleet.</param>
        /// <param name="planet">Planet to defend.</param>
        /// <returns>True when the fleet is sufficient.</returns>
        public bool CanDefend(Fleet fleet, Planet planet)
        {
            int required = GetDefenseStrength(planet);
            return required > 0
                && fleet?.HasOperationalCapitalShips() == true
                && _assessment.GetReadyFleetCombatValue(fleet) >= required;
        }

        /// <summary>
        /// Returns whether an assigned attack can occupy its now-neutral destination.
        /// </summary>
        /// <param name="fleet">The stationary fleet carrying occupation troops.</param>
        /// <param name="planet">The existing attack destination.</param>
        /// <returns>True only for an existing local attack with ready troops at a neutral colony.</returns>
        public bool CanCompleteOccupation(Fleet fleet, Planet planet)
        {
            return planet?.IsDestroyed == false
                && planet.IsColonized
                && string.IsNullOrEmpty(planet.GetOwnerInstanceID())
                && fleet?.RoleType == FleetRoleType.Battle
                && _assessment.OwnedFleets.Contains(fleet)
                && fleet.Order?.OrderType == FleetOrderType.Attack
                && fleet.Order.TargetPlanetId == planet.InstanceID
                && fleet.Order.Status != FleetOrderStatus.Returning
                && fleet.GetParentOfType<Planet>()?.InstanceID == planet.InstanceID
                && fleet.Movement == null
                && !fleet.IsInCombat
                && PlanetaryAssaultResolver.HasReadyAttackers(new[] { fleet });
        }

        /// <summary>
        /// Returns whether a stationary fleet can receive additional units at its current
        /// location without feeding an opposing fleet at an owned planet.
        /// </summary>
        /// <param name="fleet">Fleet that would receive the reinforcements.</param>
        /// <returns>
        /// True when the fleet is stationary and its owned planet has no opposing stationary
        /// fleet.
        /// </returns>
        public bool CanReinforce(Fleet fleet)
        {
            if (fleet == null || fleet.Movement != null || fleet.IsInCombat)
                return false;

            Planet currentPlanet = _assessment.GetFleetPlanet(fleet);
            if (currentPlanet == null)
                return false;

            return !_assessment.IsOwnedPlanet(currentPlanet)
                || _assessment.GetStrongestHostileFleetStrength(currentPlanet) <= 0;
        }

        /// <summary>
        /// Returns whether a completed hull can leave an idle or locally defending fleet
        /// without violating its defense commitment.
        /// </summary>
        /// <param name="fleet">The potential donor fleet.</param>
        /// <param name="capitalShip">The hull to release, including its carried units.</param>
        /// <param name="committedTransfers">Other hulls already selected to leave this turn.</param>
        /// <returns>True when the remaining ready force can retain its assignment.</returns>
        public bool CanReleaseCapitalShip(
            Fleet fleet,
            CapitalShip capitalShip,
            IReadOnlyCollection<CapitalShip> committedTransfers = null
        )
        {
            if (
                fleet == null
                || capitalShip == null
                || fleet.RoleType == FleetRoleType.Colonization
                || fleet.Movement != null
                || fleet.IsInCombat
                || capitalShip.GetParent() != fleet
                || capitalShip.GetOwnerInstanceID() != fleet.GetOwnerInstanceID()
                || capitalShip.ManufacturingStatus != ManufacturingStatus.Complete
                || capitalShip.Movement != null
                || committedTransfers?.Contains(capitalShip) == true
            )
                return false;

            Planet planet = fleet.GetParentOfType<Planet>();
            if (
                !_assessment.IsOwnedPlanet(planet)
                || fleet.GetOwnerInstanceID() != planet.GetOwnerInstanceID()
                || fleet.Order != null
                    && (
                        fleet.Order.OrderType != FleetOrderType.Defend
                        || fleet.Order.TargetPlanetId != planet.InstanceID
                    )
                || GetDefenseCommitment(planet).HoldAllLocalFleets
            )
                return false;

            List<CapitalShip> remaining = fleet
                .GetChildren<CapitalShip>()
                .Where(ship =>
                    ship != capitalShip
                    && ship.ManufacturingStatus == ManufacturingStatus.Complete
                    && ship.Movement == null
                    && committedTransfers?.Contains(ship) != true
                )
                .ToList();
            if (
                remaining.Count == 0
                || !remaining.Any(SpaceCombatStrengthCalculator.IsArmedEscort)
                || capitalShip.HasGravityWell && !remaining.Any(ship => ship.HasGravityWell)
            )
                return false;

            int remainingCombat = remaining.Sum(_assessment.GetReadyCapitalShipCombatValue);
            int requiredDefense = GetDefenseStrength(planet);
            if (fleet.Order?.OrderType == FleetOrderType.Defend)
                return remainingCombat >= requiredDefense;

            int otherLocalDefense = _assessment
                .GetFriendlyFleets(planet)
                .Where(candidate =>
                    candidate != fleet
                    && candidate.Movement == null
                    && !candidate.IsInCombat
                    && candidate.RoleType != FleetRoleType.Colonization
                    && candidate.Order?.OrderType == FleetOrderType.Defend
                    && candidate.Order.TargetPlanetId == planet.InstanceID
                )
                .Select(candidate =>
                    candidate
                        .GetChildren<CapitalShip>()
                        .Where(ship => committedTransfers?.Contains(ship) != true)
                        .Sum(_assessment.GetReadyCapitalShipCombatValue)
                )
                .DefaultIfEmpty()
                .Max();
            return Math.Max(remainingCombat, otherLocalDefense) >= requiredDefense;
        }

        /// <summary>
        /// Builds the allocations shared by one faction AI turn.
        /// </summary>
        /// <param name="game">The game whose strategy is being planned.</param>
        /// <param name="assessment">The cached faction assessment.</param>
        public AIStrategicPlan(GameRoot game, AIAssessment assessment)
        {
            _game = game;
            _assessment = assessment;
            if (_game?.Config == null)
                return;

            ExpectedDiplomacyMaintenanceGain = CalculateExpectedDiplomacyMaintenanceGain();
            var config = _game.Config.AI.FleetDeployment;
            int operationalPlanetCount = _assessment.OwnedPlanets.Count(planet =>
                planet.IsColonized && !planet.IsDestroyed
            );
            _targetBattleFleetCount = Math.Max(
                config.MinimumBattleFleetCount,
                IntegerMath.DivideRoundedUp(operationalPlanetCount, config.PlanetsPerBattleFleet)
            );
            int minimumMobileCombatStrength =
                config.MinimumMobileCombatStrength > 0
                    ? config.MinimumMobileCombatStrength
                    : config.MinimumBattleFleetCount * config.MinimumAttackStrength;
            int mobileCombatStrengthPerPlanet =
                config.MobileCombatStrengthPerPlanet > 0
                    ? config.MobileCombatStrengthPerPlanet
                    : IntegerMath.DivideRoundedUp(
                        config.MinimumAttackStrength,
                        config.PlanetsPerBattleFleet
                    );
            _targetMobileCombatStrength = Math.Max(
                _targetBattleFleetCount * config.MinimumAttackStrength,
                Math.Max(
                    minimumMobileCombatStrength,
                    operationalPlanetCount * mobileCombatStrengthPerPlanet
                )
            );
            BuildInfrastructurePlan();
        }

        /// <summary>
        /// Builds cached production-infrastructure counts and targets for this turn.
        /// </summary>
        private void BuildInfrastructurePlan()
        {
            BuildingType[] types =
            {
                BuildingType.ConstructionFacility,
                BuildingType.Shipyard,
                BuildingType.TrainingFacility,
            };
            foreach (BuildingType type in types)
            {
                _infrastructureCounts[type] = 0;
                _infrastructureTargets[type] = 0;
                _infrastructureDeficits[type] = 0;
            }

            foreach (Planet planet in _assessment.OwnedPlanets)
            {
                foreach (Building building in _assessment.GetPlanetBuildings(planet))
                {
                    BuildingType type = building.GetBuildingType();
                    if (
                        building.GetOwnerInstanceID() == planet.GetOwnerInstanceID()
                        && _infrastructureCounts.ContainsKey(type)
                    )
                        _infrastructureCounts[type]++;
                }
            }

            foreach (BuildingType type in types)
            {
                foreach (
                    IGrouping<string, Planet> sector in GetInfrastructurePlanets(type)
                        .GroupBy(planet =>
                            planet.GetParentOfType<PlanetSector>()?.InstanceID ?? string.Empty
                        )
                )
                {
                    IReadOnlyList<int> hubTargets = GetInfrastructureHubTargets(type);
                    int hubCount = Math.Min(sector.Count(), hubTargets.Count);
                    _infrastructureTargets[type] += hubTargets.Take(hubCount).Sum();
                    _infrastructureDeficits[type] += GetInfrastructureDeficit(sector, type);
                }
            }
        }

        /// <summary>
        /// Returns whether the fleet may leave without violating this turn's defense allocation.
        /// Fleets in hostile territory are never trapped by friendly-defense commitments.
        /// </summary>
        /// <param name="fleet">The fleet.</param>
        /// <returns>True when the fleet depart condition is met; otherwise false.</returns>
        public bool CanFleetDepart(Fleet fleet)
        {
            Planet planet = _assessment?.GetFleetPlanet(fleet);
            if (!_assessment.IsOwnedPlanet(planet))
                return true;

            AIPlanetDefenseCommitment commitment = GetDefenseCommitment(planet);
            if (commitment.HoldAllLocalFleets)
                return false;

            if (commitment.RequiredStrength <= 0)
                return true;

            int remainingStrength = _assessment
                .GetFriendlyFleets(planet)
                .Where(candidate => candidate != fleet && candidate.Movement == null)
                .Sum(_assessment.GetFleetCombatValue);
            return remainingStrength >= commitment.RequiredStrength;
        }

        /// <summary>
        /// Returns the cached defense allocation for an owned planet.
        /// </summary>
        /// <param name="planet">The planet to inspect.</param>
        /// <returns>The planet's defense commitment.</returns>
        private AIPlanetDefenseCommitment GetDefenseCommitment(Planet planet)
        {
            if (planet == null)
                return AIPlanetDefenseCommitment.None;

            if (_defenseByPlanetId.TryGetValue(planet.InstanceID, out var commitment))
                return commitment;

            bool holdAllLocalFleets =
                _game?.Config != null
                && _assessment.GetFactionPopularSupport(planet)
                    < _game.Config.AI.Garrison.SupportThreshold
                && !_assessment.HasFullShields(planet)
                && _assessment.GetDefensiveSupportRisk(planet) > 1
                && (
                    planet.IsInUprising
                    || _assessment.GetActiveGarrisonCount(planet, planet.GetOwnerInstanceID())
                        < GetPlanetaryGarrisonReserve(planet)
                );
            int requiredStrength = GetHeadquartersDefenseStrength(planet);
            commitment = new AIPlanetDefenseCommitment(holdAllLocalFleets, requiredStrength);
            _defenseByPlanetId[planet.InstanceID] = commitment;
            return commitment;
        }

        private readonly struct AIPlanetDefenseCommitment
        {
            internal static readonly AIPlanetDefenseCommitment None = new AIPlanetDefenseCommitment(
                false,
                0
            );

            internal bool HoldAllLocalFleets { get; }
            internal int RequiredStrength { get; }

            /// <summary>
            /// Initializes a new instance of the AIPlanetDefenseCommitment class.
            /// </summary>
            /// <param name="holdAllLocalFleets">Whether hold all local fleets.</param>
            /// <param name="requiredStrength">The required strength.</param>
            internal AIPlanetDefenseCommitment(bool holdAllLocalFleets, int requiredStrength)
            {
                HoldAllLocalFleets = holdAllLocalFleets;
                RequiredStrength = requiredStrength;
            }
        }
    }
}
