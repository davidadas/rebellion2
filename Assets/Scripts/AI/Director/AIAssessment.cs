using System;
using System.Collections.Generic;
using System.Linq;
using Rebellion.Game;
using Rebellion.Game.Missions;
using Rebellion.Game.Units;
using Rebellion.Game.World;
using Rebellion.Systems;

namespace Rebellion.AI.Director
{
    /// <summary>
    /// Derived faction view used during one AI turn.
    /// </summary>
    public sealed class AIAssessment
    {
        private readonly AITurnContext _context;
        private readonly Dictionary<string, double> _planetValues = new Dictionary<string, double>(
            StringComparer.Ordinal
        );
        private readonly Dictionary<string, int> _planetBuildingCounts = new Dictionary<
            string,
            int
        >(StringComparer.Ordinal);
        private readonly Dictionary<string, int> _planetDefenseStrengths = new Dictionary<
            string,
            int
        >(StringComparer.Ordinal);
        private readonly Dictionary<string, int> _planetMissionSupportPressures = new Dictionary<
            string,
            int
        >(StringComparer.Ordinal);
        private readonly Dictionary<string, int> _planetDefendingRegimentStrengths = new Dictionary<
            string,
            int
        >(StringComparer.Ordinal);
        private readonly Dictionary<string, int> _planetRequiredAttackCombatStrengths =
            new Dictionary<string, int>(StringComparer.Ordinal);
        private readonly Dictionary<string, int> _planetRequiredAttackRegimentCounts =
            new Dictionary<string, int>(StringComparer.Ordinal);
        private readonly Dictionary<string, int> _strongestHostileFleetStrengths = new Dictionary<
            string,
            int
        >(StringComparer.Ordinal);
        private readonly Dictionary<string, List<Fleet>> _friendlyFleetsByPlanetId = new Dictionary<
            string,
            List<Fleet>
        >(StringComparer.Ordinal);
        private readonly Dictionary<string, List<Fleet>> _hostileFleetsByPlanetId = new Dictionary<
            string,
            List<Fleet>
        >(StringComparer.Ordinal);
        private readonly Dictionary<string, Planet> _fleetPlanets = new Dictionary<string, Planet>(
            StringComparer.Ordinal
        );
        private readonly Dictionary<string, int> _fleetCombatValues = new Dictionary<string, int>(
            StringComparer.Ordinal
        );
        private readonly Dictionary<string, int> _fleetAssaultStrengths = new Dictionary<
            string,
            int
        >(StringComparer.Ordinal);
        private readonly Dictionary<string, int> _fleetBombardmentStrengths = new Dictionary<
            string,
            int
        >(StringComparer.Ordinal);
        private readonly Dictionary<string, int> _fleetLoadedRegimentAttackStrengths =
            new Dictionary<string, int>(StringComparer.Ordinal);
        private readonly Dictionary<ManufacturingType, int> _productionLaneCounts =
            new Dictionary<ManufacturingType, int>();
        private readonly Dictionary<ManufacturingType, int> _availableProductionLaneCounts =
            new Dictionary<ManufacturingType, int>();
        private readonly Dictionary<ManufacturingType, double> _productionThroughputs =
            new Dictionary<ManufacturingType, double>();
        private readonly Dictionary<ManufacturingType, double> _idleProductionThroughputs =
            new Dictionary<ManufacturingType, double>();
        private readonly Dictionary<ManufacturingType, int> _queuedProductionWork =
            new Dictionary<ManufacturingType, int>();
        private readonly Dictionary<ManufacturingType, int> _queuedProductionItemCounts =
            new Dictionary<ManufacturingType, int>();
        private readonly Dictionary<ManufacturingType, double> _queuedProductionClearTicks =
            new Dictionary<ManufacturingType, double>();
        private readonly Dictionary<
            ManufacturingType,
            double
        > _largestPlanetProductionSharePercents = new Dictionary<ManufacturingType, double>();
        private readonly Dictionary<
            ManufacturingType,
            double
        > _largestSystemProductionSharePercents = new Dictionary<ManufacturingType, double>();

        /// <summary>
        /// Creates an AI assessment for a turn context.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        public AIAssessment(AITurnContext context)
        {
            _context = context;
            KnownColonizedPlanets = BuildKnownColonizedPlanets();
            OwnedPlanets = BuildOwnedPlanets();
            EnemyPlanets = BuildEnemyPlanets();
            NeutralPlanets = BuildNeutralPlanets();
            AvailableMissionParticipants = BuildAvailableMissionParticipants();
            AvailableMainOfficers = BuildAvailableMainOfficers();
            TargetableEnemyOfficerMissionTargets = BuildTargetableEnemyOfficerMissionTargets();
            OwnedFleets = BuildOwnedFleets();
            IdleBattleFleets = BuildIdleBattleFleets();
            AttackOrderedFleets = BuildAttackOrderedFleets();
            StagingAttackFleets = BuildStagingAttackFleets();
        }

        public IReadOnlyList<Planet> KnownColonizedPlanets { get; }

        public IReadOnlyList<Planet> OwnedPlanets { get; }

        public IReadOnlyList<Planet> EnemyPlanets { get; }

        public IReadOnlyList<Planet> NeutralPlanets { get; }

        public IReadOnlyList<IMissionParticipant> AvailableMissionParticipants { get; }

        public IReadOnlyList<Officer> AvailableMainOfficers { get; }

        public IReadOnlyList<(
            Planet Planet,
            Officer TargetOfficer
        )> TargetableEnemyOfficerMissionTargets { get; }

        public IReadOnlyList<Fleet> OwnedFleets { get; }

        public IReadOnlyList<Fleet> IdleBattleFleets { get; }

        public IReadOnlyList<Fleet> AttackOrderedFleets { get; }

        public IReadOnlyList<Fleet> StagingAttackFleets { get; }

        /// <summary>
        /// Returns whether a planet is owned by the faction.
        /// </summary>
        /// <param name="planet">The planet to inspect.</param>
        /// <returns>True if the planet is owned by the faction.</returns>
        public bool IsOwnedPlanet(Planet planet)
        {
            return planet?.GetOwnerInstanceID() == _context?.Faction?.InstanceID;
        }

        /// <summary>
        /// Returns whether a planet is owned by an opposing faction.
        /// </summary>
        /// <param name="planet">The planet to inspect.</param>
        /// <returns>True if the planet is enemy owned.</returns>
        public bool IsEnemyPlanet(Planet planet)
        {
            string ownerId = planet?.GetOwnerInstanceID();
            return !string.IsNullOrEmpty(ownerId) && ownerId != _context?.Faction?.InstanceID;
        }

        /// <summary>
        /// Returns whether a planet has no owner.
        /// </summary>
        /// <param name="planet">The planet to inspect.</param>
        /// <returns>True if the planet is neutral.</returns>
        public bool IsNeutralPlanet(Planet planet)
        {
            return string.IsNullOrEmpty(planet?.GetOwnerInstanceID());
        }

        /// <summary>
        /// Returns the strategic value estimate for a planet.
        /// </summary>
        /// <param name="planet">The planet to inspect.</param>
        /// <returns>The planet value.</returns>
        public double GetPlanetValue(Planet planet)
        {
            if (planet == null)
                return 0;

            return GetOrAdd(
                _planetValues,
                planet.InstanceID,
                () =>
                    planet.GetRawResourceNodes()
                    + planet.GetEnergyCapacity()
                    + GetPlanetBuildingCount(planet)
                    + GetFactionPopularSupport(planet)
                    + planet.GetProductionRate(ManufacturingType.Building)
                    + planet.GetProductionRate(ManufacturingType.Ship)
                    + planet.GetProductionRate(ManufacturingType.Troop)
            );
        }

        /// <summary>
        /// Returns the highest enemy planet value.
        /// </summary>
        /// <returns>The highest enemy planet value.</returns>
        public double GetHighestEnemyPlanetValue()
        {
            return EnemyPlanets.Select(GetPlanetValue).DefaultIfEmpty().Max();
        }

        /// <summary>
        /// Returns the highest owned planet value.
        /// </summary>
        /// <returns>The highest owned planet value.</returns>
        public double GetHighestOwnedPlanetValue()
        {
            return OwnedPlanets.Select(GetPlanetValue).DefaultIfEmpty().Max();
        }

        /// <summary>
        /// Returns the faction's popular support on a planet.
        /// </summary>
        /// <param name="planet">The planet to inspect.</param>
        /// <returns>The popular support value.</returns>
        public int GetFactionPopularSupport(Planet planet)
        {
            if (planet == null || _context?.Faction == null)
                return 0;

            return planet.GetPopularSupport(_context.Faction.InstanceID);
        }

        /// <summary>
        /// Returns the total building count on a planet.
        /// </summary>
        /// <param name="planet">The planet to inspect.</param>
        /// <returns>The building count.</returns>
        public int GetPlanetBuildingCount(Planet planet)
        {
            if (planet == null)
                return 0;

            return GetOrAdd(
                _planetBuildingCounts,
                planet.InstanceID,
                () => planet.GetAllBuildings().Count
            );
        }

        /// <summary>
        /// Returns the regiment count on a planet.
        /// </summary>
        /// <param name="planet">The planet to inspect.</param>
        /// <returns>The regiment count.</returns>
        public int GetPlanetRegimentCount(Planet planet)
        {
            return planet?.GetAllRegiments().Count ?? 0;
        }

        /// <summary>
        /// Returns the mission support pressure on a planet.
        /// </summary>
        /// <param name="planet">The planet to inspect.</param>
        /// <returns>The mission support pressure.</returns>
        public int GetPlanetMissionSupportPressure(Planet planet)
        {
            if (planet == null)
                return 0;

            return GetOrAdd(
                _planetMissionSupportPressures,
                planet.InstanceID,
                () =>
                    planet
                        .GetAllRegiments()
                        .Count(regiment =>
                            regiment.ManufacturingStatus == ManufacturingStatus.Complete
                            && regiment.CountsForMissionSupportPressure
                        )
            );
        }

        /// <summary>
        /// Returns planetary defense strength.
        /// </summary>
        /// <param name="planet">The planet to inspect.</param>
        /// <returns>The defense strength.</returns>
        public int GetPlanetDefenseStrength(Planet planet)
        {
            if (planet == null)
                return 0;

            return GetOrAdd(_planetDefenseStrengths, planet.InstanceID, planet.GetDefenseStrength);
        }

        /// <summary>
        /// Returns completed defending regiment strength on a planet.
        /// </summary>
        /// <param name="planet">The planet to inspect.</param>
        /// <returns>The defending regiment strength.</returns>
        public int GetDefendingRegimentStrength(Planet planet)
        {
            if (planet == null)
                return 0;

            return GetOrAdd(
                _planetDefendingRegimentStrengths,
                planet.InstanceID,
                () =>
                    planet
                        .GetAllRegiments()
                        .Where(regiment =>
                            regiment.ManufacturingStatus == ManufacturingStatus.Complete
                        )
                        .Sum(regiment => regiment.DefenseRating + regiment.BombardmentDefense)
            );
        }

        /// <summary>
        /// Returns friendly fleets at a planet.
        /// </summary>
        /// <param name="planet">The planet to inspect.</param>
        /// <returns>Friendly fleets at the planet.</returns>
        public IReadOnlyList<Fleet> GetFriendlyFleets(Planet planet)
        {
            if (planet == null)
                return Array.Empty<Fleet>();

            return GetOrAdd(
                _friendlyFleetsByPlanetId,
                planet.InstanceID,
                () =>
                    planet
                        .GetFleets()
                        .Where(fleet => fleet.GetOwnerInstanceID() == _context?.Faction?.InstanceID)
                        .OrderBy(fleet => fleet.InstanceID)
                        .ToList()
            );
        }

        /// <summary>
        /// Returns hostile fleets at a planet.
        /// </summary>
        /// <param name="planet">The planet to inspect.</param>
        /// <returns>Hostile fleets at the planet.</returns>
        public IReadOnlyList<Fleet> GetHostileFleets(Planet planet)
        {
            if (planet == null)
                return Array.Empty<Fleet>();

            return GetOrAdd(
                _hostileFleetsByPlanetId,
                planet.InstanceID,
                () =>
                    planet
                        .GetFleets()
                        .Where(fleet =>
                            !string.IsNullOrEmpty(fleet.GetOwnerInstanceID())
                            && fleet.GetOwnerInstanceID() != _context?.Faction?.InstanceID
                        )
                        .OrderBy(fleet => fleet.InstanceID)
                        .ToList()
            );
        }

        /// <summary>
        /// Returns friendly fleet combat value at a planet.
        /// </summary>
        /// <param name="planet">The planet to inspect.</param>
        /// <returns>The friendly fleet combat value.</returns>
        public int GetFriendlyFleetCombatValue(Planet planet)
        {
            return GetFriendlyFleets(planet)
                .Where(fleet => fleet.Movement == null)
                .Sum(GetFleetCombatValue);
        }

        /// <summary>
        /// Returns hostile fleet combat value at a planet.
        /// </summary>
        /// <param name="planet">The planet to inspect.</param>
        /// <returns>The hostile fleet combat value.</returns>
        public int GetHostileFleetCombatValue(Planet planet)
        {
            return GetHostileFleets(planet)
                .Where(fleet => fleet.Movement == null)
                .Sum(GetFleetCombatValue);
        }

        /// <summary>
        /// Returns the strongest hostile fleet strength at a planet.
        /// </summary>
        /// <param name="planet">The planet to inspect.</param>
        /// <returns>The strongest hostile fleet strength.</returns>
        public int GetStrongestHostileFleetStrength(Planet planet)
        {
            if (planet == null)
                return 0;

            return GetOrAdd(
                _strongestHostileFleetStrengths,
                planet.InstanceID,
                () =>
                    GetHostileFleets(planet)
                        .Where(fleet => fleet.Movement == null)
                        .Select(GetFleetCombatValue)
                        .DefaultIfEmpty()
                        .Max()
            );
        }

        /// <summary>
        /// Returns combat strength required to attack a planet.
        /// </summary>
        /// <param name="planet">The planet to inspect.</param>
        /// <returns>The required attack combat strength.</returns>
        public int GetRequiredAttackCombatStrength(Planet planet)
        {
            if (planet == null || _context?.Game?.Config == null)
                return 0;

            return GetOrAdd(
                _planetRequiredAttackCombatStrengths,
                planet.InstanceID,
                () =>
                {
                    GameConfig.AIFleetDeploymentConfig config = _context
                        .Game
                        .Config
                        .AI
                        .FleetDeployment;
                    int shieldDefenseRequirement = ScaleByPercent(
                        GetPlanetDefenseStrength(planet),
                        config.AttackStrengthPercentOfDefense
                    );
                    int fleetDefenseRequirement = ScaleByPercent(
                        GetStrongestHostileFleetStrength(planet),
                        config.AttackStrengthPercentOfStrongestHostileFleet
                    );
                    return Math.Max(
                        config.MinimumAttackStrength,
                        Math.Max(shieldDefenseRequirement, fleetDefenseRequirement)
                    );
                }
            );
        }

        /// <summary>
        /// Returns regiment count required to attack a planet.
        /// </summary>
        /// <param name="planet">The planet to inspect.</param>
        /// <returns>The required attack regiment count.</returns>
        public int GetRequiredAttackRegimentCount(Planet planet)
        {
            if (planet == null || _context?.Game?.Config == null || _context.Faction == null)
                return 0;

            return GetOrAdd(
                _planetRequiredAttackRegimentCounts,
                planet.InstanceID,
                () =>
                    Math.Max(
                        _context.Game.Config.AI.FleetDeployment.MinimumCaptureRegimentCount,
                        GetPlanetRegimentCount(planet)
                            + UprisingSystem.CalculateGarrisonRequirement(
                                planet,
                                _context.Faction,
                                _context.Game.Config.AI.Garrison
                            )
                    )
            );
        }

        /// <summary>
        /// Returns the planet currently containing a fleet.
        /// </summary>
        /// <param name="fleet">The fleet to inspect.</param>
        /// <returns>The fleet planet, or null.</returns>
        public Planet GetFleetPlanet(Fleet fleet)
        {
            if (fleet == null)
                return null;

            return GetOrAdd(_fleetPlanets, fleet.InstanceID, fleet.GetParentOfType<Planet>);
        }

        /// <summary>
        /// Returns whether a fleet is an idle battle fleet.
        /// </summary>
        /// <param name="fleet">The fleet to inspect.</param>
        /// <returns>True if the fleet is an idle battle fleet.</returns>
        public bool IsIdleBattleFleet(Fleet fleet)
        {
            return fleet != null
                && fleet.RoleType == FleetRoleType.Battle
                && fleet.Movement == null
                && !fleet.IsInCombat
                && fleet.Order == null
                && fleet.HasOperationalCapitalShips();
        }

        /// <summary>
        /// Returns whether a fleet has an attack order.
        /// </summary>
        /// <param name="fleet">The fleet to inspect.</param>
        /// <returns>True if the fleet has an attack order.</returns>
        public bool HasAttackOrder(Fleet fleet)
        {
            return fleet?.Order?.OrderType == FleetOrderType.Attack;
        }

        /// <summary>
        /// Returns whether a fleet is staging for an attack.
        /// </summary>
        /// <param name="fleet">The fleet to inspect.</param>
        /// <returns>True if the fleet is staging for an attack.</returns>
        public bool IsStagingAttackFleet(Fleet fleet)
        {
            return HasAttackOrder(fleet) && fleet.Order.Status == FleetOrderStatus.Staging;
        }

        /// <summary>
        /// Returns total combat value for a fleet.
        /// </summary>
        /// <param name="fleet">The fleet to inspect.</param>
        /// <returns>The fleet combat value.</returns>
        public int GetFleetCombatValue(Fleet fleet)
        {
            if (fleet == null)
                return 0;

            return GetOrAdd(_fleetCombatValues, fleet.InstanceID, fleet.GetCombatValue);
        }

        /// <summary>
        /// Returns whether a fleet has enough ready force to attack a planet.
        /// </summary>
        /// <param name="fleet">The fleet to inspect.</param>
        /// <param name="targetPlanet">The attack target.</param>
        /// <returns>True if the fleet is ready to attack.</returns>
        public bool IsFleetReadyToAttack(Fleet fleet, Planet targetPlanet)
        {
            int requiredCombat = GetRequiredAttackCombatStrength(targetPlanet);
            int requiredRegiments = GetRequiredAttackRegimentCount(targetPlanet);
            return fleet?.HasOperationalCapitalShips() == true
                && GetReadyFleetCombatValue(fleet) >= requiredCombat
                && GetReadyFleetRegimentCount(fleet) >= requiredRegiments
                && GetReadyFleetRegimentCapacity(fleet) >= requiredRegiments;
        }

        /// <summary>
        /// Returns the number of attack readiness gates satisfied by a fleet.
        /// </summary>
        /// <param name="fleet">The fleet to inspect.</param>
        /// <param name="targetPlanet">The attack target.</param>
        /// <returns>The satisfied readiness gate count.</returns>
        public int GetFleetAttackReadinessGateCount(Fleet fleet, Planet targetPlanet)
        {
            int requiredCombat = GetRequiredAttackCombatStrength(targetPlanet);
            int requiredRegiments = GetRequiredAttackRegimentCount(targetPlanet);
            int gateCount = 0;

            if (fleet?.HasOperationalCapitalShips() == true)
                gateCount++;

            if (GetReadyFleetCombatValue(fleet) >= requiredCombat)
                gateCount++;

            if (GetReadyFleetRegimentCount(fleet) >= requiredRegiments)
                gateCount++;

            if (GetReadyFleetRegimentCapacity(fleet) >= requiredRegiments)
                gateCount++;

            return gateCount;
        }

        /// <summary>
        /// Returns combat value from ready units in a fleet.
        /// </summary>
        /// <param name="fleet">The fleet to inspect.</param>
        /// <returns>The ready combat value.</returns>
        public int GetReadyFleetCombatValue(Fleet fleet)
        {
            return fleet?.GetCombatValue() ?? 0;
        }

        /// <summary>
        /// Returns loaded ready regiments in a fleet.
        /// </summary>
        /// <param name="fleet">The fleet to inspect.</param>
        /// <returns>The ready regiment count.</returns>
        public int GetReadyFleetRegimentCount(Fleet fleet)
        {
            if (fleet == null)
                return 0;

            return fleet
                .CapitalShips.Where(IsReadyCapitalShip)
                .SelectMany(ship => ship.Regiments)
                .Count(IsReadyRegiment);
        }

        /// <summary>
        /// Returns ready regiment capacity in a fleet.
        /// </summary>
        /// <param name="fleet">The fleet to inspect.</param>
        /// <returns>The ready regiment capacity.</returns>
        public int GetReadyFleetRegimentCapacity(Fleet fleet)
        {
            if (fleet == null)
                return 0;

            return fleet
                .CapitalShips.Where(IsReadyCapitalShip)
                .Sum(ship => ship.GetRegimentCapacity());
        }

        /// <summary>
        /// Returns attack strength from ready loaded regiments in a fleet.
        /// </summary>
        /// <param name="fleet">The fleet to inspect.</param>
        /// <returns>The ready loaded regiment attack strength.</returns>
        public int GetReadyFleetLoadedRegimentAttackStrength(Fleet fleet)
        {
            if (fleet == null)
                return 0;

            return fleet
                .CapitalShips.Where(IsReadyCapitalShip)
                .SelectMany(ship => ship.Regiments)
                .Where(IsReadyRegiment)
                .Sum(regiment => regiment.AttackRating);
        }

        /// <summary>
        /// Returns loaded ready regiments on a capital ship.
        /// </summary>
        /// <param name="capitalShip">The capital ship to inspect.</param>
        /// <returns>The ready regiment count.</returns>
        public int GetReadyCapitalShipRegimentCount(CapitalShip capitalShip)
        {
            if (!IsReadyCapitalShip(capitalShip))
                return 0;

            return capitalShip.Regiments.Count(IsReadyRegiment);
        }

        /// <summary>
        /// Returns ready regiment capacity on a capital ship.
        /// </summary>
        /// <param name="capitalShip">The capital ship to inspect.</param>
        /// <returns>The ready regiment capacity.</returns>
        public int GetReadyCapitalShipRegimentCapacity(CapitalShip capitalShip)
        {
            if (!IsReadyCapitalShip(capitalShip))
                return 0;

            return capitalShip.GetRegimentCapacity();
        }

        /// <summary>
        /// Returns fleet assault strength.
        /// </summary>
        /// <param name="fleet">The fleet to inspect.</param>
        /// <returns>The fleet assault strength.</returns>
        public int GetFleetAssaultStrength(Fleet fleet)
        {
            if (fleet == null || _context?.Game?.Config == null)
                return 0;

            return GetOrAdd(
                _fleetAssaultStrengths,
                fleet.InstanceID,
                () => fleet.GetAssaultStrength(_context.Game.Config.Combat.AssaultPersonnelDivisor)
            );
        }

        /// <summary>
        /// Returns fleet bombardment strength.
        /// </summary>
        /// <param name="fleet">The fleet to inspect.</param>
        /// <returns>The fleet bombardment strength.</returns>
        public int GetFleetBombardmentStrength(Fleet fleet)
        {
            if (fleet == null || _context?.Game?.Config == null)
                return 0;

            return GetOrAdd(
                _fleetBombardmentStrengths,
                fleet.InstanceID,
                () =>
                {
                    int divisor = _context.Game.Config.Combat.AssaultPersonnelDivisor;
                    Officer commander = fleet
                        .GetOfficers()
                        .FirstOrDefault(officer => officer.CurrentRank == OfficerRank.General);
                    int personnel =
                        commander?.GetSkillValue(MissionParticipantSkill.Leadership) ?? 0;
                    int personnelMultiplier = personnel / divisor + 1;

                    return fleet
                        .CapitalShips.Where(ship =>
                            ship.ManufacturingStatus == ManufacturingStatus.Complete
                            && ship.Movement == null
                        )
                        .Sum(ship => personnelMultiplier * ship.Bombardment);
                }
            );
        }

        /// <summary>
        /// Returns attack strength from loaded regiments in a fleet.
        /// </summary>
        /// <param name="fleet">The fleet to inspect.</param>
        /// <returns>The loaded regiment attack strength.</returns>
        public int GetFleetLoadedRegimentAttackStrength(Fleet fleet)
        {
            if (fleet == null)
                return 0;

            return GetOrAdd(
                _fleetLoadedRegimentAttackStrengths,
                fleet.InstanceID,
                () =>
                    fleet
                        .GetRegiments()
                        .Where(regiment =>
                            regiment.ManufacturingStatus == ManufacturingStatus.Complete
                        )
                        .Sum(regiment => regiment.AttackRating)
            );
        }

        /// <summary>
        /// Returns loaded regiment count for a fleet.
        /// </summary>
        /// <param name="fleet">The fleet to inspect.</param>
        /// <returns>The loaded regiment count.</returns>
        public int GetFleetLoadedRegimentCount(Fleet fleet)
        {
            return fleet?.GetCurrentRegimentCount() ?? 0;
        }

        /// <summary>
        /// Returns regiment capacity for a fleet.
        /// </summary>
        /// <param name="fleet">The fleet to inspect.</param>
        /// <returns>The regiment capacity.</returns>
        public int GetFleetRegimentCapacity(Fleet fleet)
        {
            return fleet?.GetRegimentCapacity() ?? 0;
        }

        /// <summary>
        /// Returns excess regiment capacity for a fleet.
        /// </summary>
        /// <param name="fleet">The fleet to inspect.</param>
        /// <returns>The excess regiment capacity.</returns>
        public int GetFleetExcessRegimentCapacity(Fleet fleet)
        {
            return fleet?.GetExcessRegimentCapacity() ?? 0;
        }

        /// <summary>
        /// Returns loaded starfighter count for a fleet.
        /// </summary>
        /// <param name="fleet">The fleet to inspect.</param>
        /// <returns>The loaded starfighter count.</returns>
        public int GetFleetLoadedStarfighterCount(Fleet fleet)
        {
            return fleet?.GetCurrentStarfighterCount() ?? 0;
        }

        /// <summary>
        /// Returns starfighter capacity for a fleet.
        /// </summary>
        /// <param name="fleet">The fleet to inspect.</param>
        /// <returns>The starfighter capacity.</returns>
        public int GetFleetStarfighterCapacity(Fleet fleet)
        {
            return fleet?.GetStarfighterCapacity() ?? 0;
        }

        /// <summary>
        /// Returns excess starfighter capacity for a fleet.
        /// </summary>
        /// <param name="fleet">The fleet to inspect.</param>
        /// <returns>The excess starfighter capacity.</returns>
        public int GetFleetExcessStarfighterCapacity(Fleet fleet)
        {
            return fleet?.GetExcessStarfighterCapacity() ?? 0;
        }

        /// <summary>
        /// Returns owned planet count with production lanes for a manufacturing type.
        /// </summary>
        /// <param name="type">Manufacturing type to inspect.</param>
        /// <returns>The production lane count.</returns>
        public int GetProductionLaneCount(ManufacturingType type)
        {
            if (type == ManufacturingType.None)
                return 0;

            return GetOrAdd(
                _productionLaneCounts,
                type,
                () => OwnedPlanets.Count(planet => planet.GetProductionFacilityCount(type) > 0)
            );
        }

        /// <summary>
        /// Returns owned planet count with idle production lanes for a manufacturing type.
        /// </summary>
        /// <param name="type">Manufacturing type to inspect.</param>
        /// <returns>The available production lane count.</returns>
        public int GetAvailableProductionLaneCount(ManufacturingType type)
        {
            if (type == ManufacturingType.None)
                return 0;

            return GetOrAdd(
                _availableProductionLaneCounts,
                type,
                () =>
                    OwnedPlanets.Count(planet =>
                        planet.GetProductionFacilityCount(type) > 0
                        && GetQueuedProductionWork(planet, type) == 0
                    )
            );
        }

        /// <summary>
        /// Returns total production throughput for a manufacturing type.
        /// </summary>
        /// <param name="type">Manufacturing type to inspect.</param>
        /// <returns>The total production throughput.</returns>
        public double GetProductionThroughput(ManufacturingType type)
        {
            if (type == ManufacturingType.None)
                return 0;

            return GetOrAdd(
                _productionThroughputs,
                type,
                () => OwnedPlanets.Sum(planet => planet.GetProductionRate(type))
            );
        }

        /// <summary>
        /// Returns idle production throughput for a manufacturing type.
        /// </summary>
        /// <param name="type">Manufacturing type to inspect.</param>
        /// <returns>The idle production throughput.</returns>
        public double GetIdleProductionThroughput(ManufacturingType type)
        {
            if (type == ManufacturingType.None)
                return 0;

            return GetOrAdd(
                _idleProductionThroughputs,
                type,
                () =>
                    OwnedPlanets
                        .Where(planet => GetQueuedProductionWork(planet, type) == 0)
                        .Sum(planet => planet.GetProductionRate(type))
            );
        }

        /// <summary>
        /// Returns queued production work for a manufacturing type.
        /// </summary>
        /// <param name="type">Manufacturing type to inspect.</param>
        /// <returns>The queued production work.</returns>
        public int GetQueuedProductionWork(ManufacturingType type)
        {
            if (type == ManufacturingType.None)
                return 0;

            return GetOrAdd(
                _queuedProductionWork,
                type,
                () => OwnedPlanets.Sum(planet => GetQueuedProductionWork(planet, type))
            );
        }

        /// <summary>
        /// Returns queued production item count for a manufacturing type.
        /// </summary>
        /// <param name="type">Manufacturing type to inspect.</param>
        /// <returns>The queued production item count.</returns>
        public int GetQueuedProductionItemCount(ManufacturingType type)
        {
            if (type == ManufacturingType.None)
                return 0;

            return GetOrAdd(
                _queuedProductionItemCounts,
                type,
                () => OwnedPlanets.Sum(planet => GetQueuedProductionItemCount(planet, type))
            );
        }

        /// <summary>
        /// Returns estimated queue clear time for a manufacturing type.
        /// </summary>
        /// <param name="type">Manufacturing type to inspect.</param>
        /// <returns>The queued production clear ticks.</returns>
        public double GetQueuedProductionClearTicks(ManufacturingType type)
        {
            if (type == ManufacturingType.None)
                return 0;

            return GetOrAdd(
                _queuedProductionClearTicks,
                type,
                () =>
                {
                    int work = GetQueuedProductionWork(type);
                    if (work <= 0)
                        return 0;

                    double throughput = GetProductionThroughput(type);
                    if (throughput <= 0)
                        return double.PositiveInfinity;

                    return work / throughput;
                }
            );
        }

        /// <summary>
        /// Returns the largest single-planet production share for a manufacturing type.
        /// </summary>
        /// <param name="type">Manufacturing type to inspect.</param>
        /// <returns>The largest planet production share percent.</returns>
        public double GetLargestPlanetProductionSharePercent(ManufacturingType type)
        {
            if (type == ManufacturingType.None)
                return 0;

            return GetOrAdd(
                _largestPlanetProductionSharePercents,
                type,
                () =>
                {
                    double throughput = GetProductionThroughput(type);
                    if (throughput <= 0)
                        return 0;

                    double largestPlanetThroughput = OwnedPlanets
                        .Select(planet => planet.GetProductionRate(type))
                        .DefaultIfEmpty()
                        .Max();

                    return largestPlanetThroughput * 100 / throughput;
                }
            );
        }

        /// <summary>
        /// Returns the largest single-system production share for a manufacturing type.
        /// </summary>
        /// <param name="type">Manufacturing type to inspect.</param>
        /// <returns>The largest system production share percent.</returns>
        public double GetLargestSystemProductionSharePercent(ManufacturingType type)
        {
            if (type == ManufacturingType.None)
                return 0;

            return GetOrAdd(
                _largestSystemProductionSharePercents,
                type,
                () =>
                {
                    double throughput = GetProductionThroughput(type);
                    if (throughput <= 0)
                        return 0;

                    double largestSystemThroughput = OwnedPlanets
                        .GroupBy(GetProductionConcentrationSystemId, StringComparer.Ordinal)
                        .Select(group => group.Sum(planet => planet.GetProductionRate(type)))
                        .DefaultIfEmpty()
                        .Max();

                    return largestSystemThroughput * 100 / throughput;
                }
            );
        }

        /// <summary>
        /// Builds the known colonized planet list.
        /// </summary>
        /// <returns>Known colonized planets.</returns>
        private List<Planet> BuildKnownColonizedPlanets()
        {
            if (_context?.Game == null)
                return new List<Planet>();

            return _context
                .Game.GetSceneNodesByType<Planet>()
                .Where(planet => planet.IsColonized && !planet.IsDestroyed)
                .OrderBy(GetPlanetSystemPositionX)
                .ThenBy(planet => planet.InstanceID)
                .ToList();
        }

        /// <summary>
        /// Builds the owned planet list.
        /// </summary>
        /// <returns>Owned planets.</returns>
        private List<Planet> BuildOwnedPlanets()
        {
            if (_context?.Faction == null)
                return new List<Planet>();

            return _context
                .Faction.GetOwnedUnitsByType<Planet>()
                .Where(planet => planet != null)
                .OrderBy(GetPlanetSystemPositionX)
                .ThenBy(planet => planet.InstanceID)
                .ToList();
        }

        /// <summary>
        /// Builds the enemy planet list.
        /// </summary>
        /// <returns>Enemy planets.</returns>
        private List<Planet> BuildEnemyPlanets()
        {
            if (_context?.Game == null)
                return new List<Planet>();

            return _context
                .Game.GetSceneNodesByType<Planet>()
                .Where(IsEnemyPlanet)
                .OrderBy(GetPlanetSystemPositionX)
                .ThenBy(planet => planet.InstanceID)
                .ToList();
        }

        /// <summary>
        /// Builds the neutral planet list.
        /// </summary>
        /// <returns>Neutral planets.</returns>
        private List<Planet> BuildNeutralPlanets()
        {
            return KnownColonizedPlanets.Where(IsNeutralPlanet).ToList();
        }

        /// <summary>
        /// Builds the available mission participant list.
        /// </summary>
        /// <returns>Available mission participants.</returns>
        private List<IMissionParticipant> BuildAvailableMissionParticipants()
        {
            if (_context?.Faction == null)
                return new List<IMissionParticipant>();

            return _context.Faction.GetAvailableMissionParticipants();
        }

        /// <summary>
        /// Builds the available main officer list.
        /// </summary>
        /// <returns>Available main officers.</returns>
        private List<Officer> BuildAvailableMainOfficers()
        {
            return AvailableMissionParticipants
                .OfType<Officer>()
                .Where(officer => officer.IsMain)
                .OrderBy(officer => officer.InstanceID)
                .ToList();
        }

        /// <summary>
        /// Builds targetable enemy officer mission targets.
        /// </summary>
        /// <returns>Targetable enemy officer mission targets.</returns>
        private List<(
            Planet Planet,
            Officer TargetOfficer
        )> BuildTargetableEnemyOfficerMissionTargets()
        {
            if (_context?.Faction == null)
                return new List<(Planet Planet, Officer TargetOfficer)>();

            return KnownColonizedPlanets
                .Where(IsEnemyPlanet)
                .SelectMany(planet =>
                    planet
                        .GetAllOfficers()
                        .Where(IsTargetableEnemyOfficer)
                        .Select(officer => (Planet: planet, TargetOfficer: officer))
                )
                .OrderBy(candidate => GetPlanetSystemPositionX(candidate.Planet))
                .ThenBy(candidate => candidate.Planet.InstanceID)
                .ThenBy(candidate => candidate.TargetOfficer.InstanceID)
                .ToList();
        }

        /// <summary>
        /// Returns whether an officer can be targeted by hostile missions.
        /// </summary>
        /// <param name="officer">The officer to inspect.</param>
        /// <returns>True if the officer can be targeted.</returns>
        private bool IsTargetableEnemyOfficer(Officer officer)
        {
            return officer != null
                && officer.OwnerInstanceID != _context.Faction.InstanceID
                && !officer.IsCaptured
                && !officer.IsKilled;
        }

        /// <summary>
        /// Builds the owned fleet list.
        /// </summary>
        /// <returns>Owned fleets.</returns>
        private List<Fleet> BuildOwnedFleets()
        {
            if (_context?.Faction == null)
                return new List<Fleet>();

            return _context
                .Faction.GetOwnedUnitsByType<Fleet>()
                .Where(fleet => fleet != null)
                .OrderBy(fleet => fleet.InstanceID)
                .ToList();
        }

        /// <summary>
        /// Builds the idle battle fleet list.
        /// </summary>
        /// <returns>Idle battle fleets.</returns>
        private List<Fleet> BuildIdleBattleFleets()
        {
            return OwnedFleets.Where(IsIdleBattleFleet).ToList();
        }

        /// <summary>
        /// Builds the attack-ordered fleet list.
        /// </summary>
        /// <returns>Attack-ordered fleets.</returns>
        private List<Fleet> BuildAttackOrderedFleets()
        {
            return OwnedFleets.Where(HasAttackOrder).ToList();
        }

        /// <summary>
        /// Builds the staging attack fleet list.
        /// </summary>
        /// <returns>Staging attack fleets.</returns>
        private List<Fleet> BuildStagingAttackFleets()
        {
            return OwnedFleets.Where(IsStagingAttackFleet).ToList();
        }

        /// <summary>
        /// Returns a planet's system x position.
        /// </summary>
        /// <param name="planet">The planet to inspect.</param>
        /// <returns>The system x position.</returns>
        private int GetPlanetSystemPositionX(Planet planet)
        {
            return planet.GetParentOfType<PlanetSystem>()?.PositionX ?? 0;
        }

        /// <summary>
        /// Returns the system id used for production concentration checks.
        /// </summary>
        /// <param name="planet">The planet to inspect.</param>
        /// <returns>The concentration system id.</returns>
        private string GetProductionConcentrationSystemId(Planet planet)
        {
            return planet.GetParentOfType<PlanetSystem>()?.InstanceID ?? planet.InstanceID;
        }

        /// <summary>
        /// Returns queued production work on a planet for a manufacturing type.
        /// </summary>
        /// <param name="planet">The planet to inspect.</param>
        /// <param name="type">Manufacturing type to inspect.</param>
        /// <returns>The queued production work.</returns>
        private int GetQueuedProductionWork(Planet planet, ManufacturingType type)
        {
            if (
                planet == null
                || !planet
                    .GetManufacturingQueue()
                    .TryGetValue(type, out List<IManufacturable> manufacturingQueue)
            )
                return 0;

            return manufacturingQueue.Sum(item =>
                Math.Max(0, item.GetConstructionCost() - item.ManufacturingProgress)
            );
        }

        /// <summary>
        /// Returns queued production item count on a planet for a manufacturing type.
        /// </summary>
        /// <param name="planet">The planet to inspect.</param>
        /// <param name="type">Manufacturing type to inspect.</param>
        /// <returns>The queued production item count.</returns>
        private int GetQueuedProductionItemCount(Planet planet, ManufacturingType type)
        {
            if (
                planet == null
                || !planet
                    .GetManufacturingQueue()
                    .TryGetValue(type, out List<IManufacturable> manufacturingQueue)
            )
                return 0;

            return manufacturingQueue.Count;
        }

        /// <summary>
        /// Scales an integer by a percent value.
        /// </summary>
        /// <param name="value">Value to scale.</param>
        /// <param name="percent">Percent to apply.</param>
        /// <returns>The scaled value.</returns>
        private int ScaleByPercent(int value, int percent)
        {
            return value * percent / 100;
        }

        private static bool IsReadyCapitalShip(CapitalShip capitalShip)
        {
            return capitalShip != null
                && capitalShip.ManufacturingStatus == ManufacturingStatus.Complete
                && capitalShip.Movement == null;
        }

        private static bool IsReadyRegiment(Regiment regiment)
        {
            return regiment != null
                && regiment.ManufacturingStatus == ManufacturingStatus.Complete
                && regiment.Movement == null;
        }

        /// <summary>
        /// Returns a cached value by string key.
        /// </summary>
        /// <param name="cache">Cache to use.</param>
        /// <param name="key">Cache key.</param>
        /// <param name="createValue">Value factory used on cache miss.</param>
        /// <returns>The cached or created value.</returns>
        private static TValue GetOrAdd<TValue>(
            Dictionary<string, TValue> cache,
            string key,
            Func<TValue> createValue
        )
        {
            if (string.IsNullOrEmpty(key))
                return createValue();

            if (!cache.TryGetValue(key, out TValue value))
            {
                value = createValue();
                cache[key] = value;
            }

            return value;
        }

        /// <summary>
        /// Returns a cached value by manufacturing type.
        /// </summary>
        /// <param name="cache">Cache to use.</param>
        /// <param name="key">Cache key.</param>
        /// <param name="createValue">Value factory used on cache miss.</param>
        /// <returns>The cached or created value.</returns>
        private static TValue GetOrAdd<TValue>(
            Dictionary<ManufacturingType, TValue> cache,
            ManufacturingType key,
            Func<TValue> createValue
        )
        {
            if (!cache.TryGetValue(key, out TValue value))
            {
                value = createValue();
                cache[key] = value;
            }

            return value;
        }
    }
}
