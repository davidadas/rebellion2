using System;
using System.Collections.Generic;
using System.Linq;
using Rebellion.Game;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Units;
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
