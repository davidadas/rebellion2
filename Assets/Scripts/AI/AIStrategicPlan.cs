using System;
using System.Collections.Generic;
using System.Linq;
using Rebellion.Game;
using Rebellion.Game.Galaxy;
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
        private readonly Dictionary<BuildingType, int> _infrastructureCounts = new();
        private readonly Dictionary<BuildingType, int> _infrastructureTargets = new();
        private readonly Dictionary<BuildingType, int> _infrastructureDeficits = new();
        private readonly Dictionary<
            string,
            Dictionary<BuildingType, int>
        > _infrastructureTargetsByPlanetId = new(StringComparer.Ordinal);
        private readonly Dictionary<string, AIPlanetDefenseCommitment> _defenseByPlanetId =
            new Dictionary<string, AIPlanetDefenseCommitment>(StringComparer.Ordinal);

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
        /// <returns>True when the allocation fits and has been recorded.</returns>
        public bool TryAllocateInfrastructureHub(
            Planet planet,
            BuildingType buildingType,
            int targetCount
        )
        {
            if (!CanAllocateInfrastructureHub(planet, buildingType, targetCount))
                return false;
            if (!_infrastructureTargetsByPlanetId.TryGetValue(planet.InstanceID, out var targets))
            {
                targets = new Dictionary<BuildingType, int>();
                _infrastructureTargetsByPlanetId.Add(planet.InstanceID, targets);
            }
            targets[buildingType] = targetCount;
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
            return
                hasInfrastructure
                || _assessment.GetFactionPopularSupport(planet)
                    < _game.Config.AI.Garrison.SupportThreshold
                || _assessment.GetDefensiveSupportRisk(planet) > 0
                ? Math.Min(1, limit)
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
        /// <returns>The signed remaining space before clamping an overcommitted layout.</returns>
        private int GetConstructionSpace(
            Planet planet,
            BuildingType buildingType,
            bool isEmergencyDefense,
            IReadOnlyDictionary<BuildingType, int> selectedBuildings = null
        )
        {
            if (
                _game?.Config == null
                || planet?.IsDestroyed != false
                || !_assessment.IsOwnedPlanet(planet)
            )
                return 0;
            int available = ManufacturingQueries.GetAvailableBuildingSpace(_game, planet, true);
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
                    GetPlanetaryShieldTarget(planet, IsInfrastructureType(buildingType))
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
                        !building.IsAIProductionFiller
                        || string.IsNullOrEmpty(building.ProducerPlanetID)
                    )
                        return true;
                    Planet producer = _game.GetSceneNodeByInstanceID<Planet>(
                        building.ProducerPlanetID
                    );
                    return !ReferenceEquals(
                        building,
                        ManufacturingQueries.GetInterruptibleManufacturingOrder(
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
                config.AttackStrengthPercentOfStrongestHostileFleet
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
                && _assessment.GetDefensiveSupportRisk(planet) > 1;
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
