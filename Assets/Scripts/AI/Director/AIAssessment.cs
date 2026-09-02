using System;
using System.Collections.Generic;
using System.Linq;
using Rebellion.Game;
using Rebellion.Game.Factions;
using Rebellion.Game.FogOfWar;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Missions;
using Rebellion.Game.Units;
using Rebellion.Systems;
using Rebellion.Util.Common;

namespace Rebellion.AI.Director
{
    /// <summary>
    /// Derived faction view used during one AI turn.
    /// </summary>
    public sealed class AIAssessment
    {
        // Turn Context.
        private readonly AITurnContext _context;
        private readonly AISabotageTargetPolicy _sabotageTargets;

        // Cached Assessments.
        private readonly Dictionary<string, double> _planetValues = new Dictionary<string, double>(
            StringComparer.Ordinal
        );
        private readonly Dictionary<string, int> _planetBuildingCounts = new Dictionary<
            string,
            int
        >(StringComparer.Ordinal);
        private readonly Dictionary<string, IReadOnlyList<Building>> _planetBuildings =
            new Dictionary<string, IReadOnlyList<Building>>(StringComparer.Ordinal);
        private readonly Dictionary<string, IReadOnlyList<Regiment>> _planetRegiments =
            new Dictionary<string, IReadOnlyList<Regiment>>(StringComparer.Ordinal);
        private readonly Dictionary<string, IReadOnlyList<Starfighter>> _planetStarfighters =
            new Dictionary<string, IReadOnlyList<Starfighter>>(StringComparer.Ordinal);
        private readonly Dictionary<
            (string PlanetId, ManufacturingType ManufacturingType),
            int
        > _planetProductionFacilityCounts =
            new Dictionary<(string PlanetId, ManufacturingType ManufacturingType), int>();
        private readonly Dictionary<
            (string PlanetId, ManufacturingType ManufacturingType),
            double
        > _planetProductionRates =
            new Dictionary<(string PlanetId, ManufacturingType ManufacturingType), double>();
        private readonly Dictionary<string, int> _planetDefenseStrengths = new Dictionary<
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
        private readonly Dictionary<string, int> _hostilePlanetaryStarfighterStrengths =
            new Dictionary<string, int>(StringComparer.Ordinal);
        private readonly Dictionary<string, int> _planetDefenseThreatStrengths = new Dictionary<
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
        private readonly Dictionary<string, int> _fleetBombardmentStrengths = new Dictionary<
            string,
            int
        >(StringComparer.Ordinal);
        private readonly Dictionary<string, int> _projectedFleetBombardmentStrengths =
            new Dictionary<string, int>(StringComparer.Ordinal);
        private HashSet<string> _hostileSectorIds;
        private readonly Dictionary<ManufacturingType, int> _availableProductionLaneCounts =
            new Dictionary<ManufacturingType, int>();
        private readonly Dictionary<ManufacturingType, double> _productionThroughputs =
            new Dictionary<ManufacturingType, double>();
        private readonly Dictionary<ManufacturingType, double> _idleProductionThroughputs =
            new Dictionary<ManufacturingType, double>();
        private readonly Dictionary<ManufacturingType, int> _queuedProductionWork =
            new Dictionary<ManufacturingType, int>();
        private readonly Dictionary<ManufacturingType, double> _queuedProductionClearTicks =
            new Dictionary<ManufacturingType, double>();
        private readonly Dictionary<string, Planet> _knownPlanets = new Dictionary<string, Planet>(
            StringComparer.Ordinal
        );
        private readonly Dictionary<string, IReadOnlyList<Planet>> _knownPlanetsBySystemId =
            new Dictionary<string, IReadOnlyList<Planet>>(StringComparer.Ordinal);
        private readonly Dictionary<string, int> _offensiveSupportLeverage = new Dictionary<
            string,
            int
        >(StringComparer.Ordinal);
        private readonly Dictionary<string, int> _defensiveSupportRisks = new Dictionary<
            string,
            int
        >(StringComparer.Ordinal);
        private readonly Dictionary<
            string,
            IReadOnlyList<Planet>
        > _attackCampaignPlanetsBySystemId = new Dictionary<string, IReadOnlyList<Planet>>(
            StringComparer.Ordinal
        );
        private readonly Dictionary<string, int> _enemySystemSupportLeverage = new Dictionary<
            string,
            int
        >(StringComparer.Ordinal);
        private readonly HashSet<string> _knownGarrisonedPlanetIds = new HashSet<string>(
            StringComparer.Ordinal
        );
        private readonly IReadOnlyList<string> _opposingFactionIds;

        // Planet Intelligence.
        public IReadOnlyList<Planet> KnownColonizedPlanets { get; }

        public IReadOnlyList<Planet> KnownUncolonizedPlanets { get; }

        public IReadOnlyList<Planet> FactionViewPlanets { get; }

        public IReadOnlyList<Planet> UnexploredPlanets { get; }

        public IReadOnlyList<Planet> OwnedPlanets { get; }

        public IReadOnlyList<Planet> EnemyPlanets { get; }

        public IReadOnlyList<Planet> NeutralPlanets { get; }

        // Economy.
        public int MaintenanceCapacity { get; }

        public int ProjectedMaintenanceHeadroom { get; }

        public int RefinedMaterialSupply { get; }

        public int RefinedMaterialStockpile { get; }

        // Missions.
        public IReadOnlyList<IMissionParticipant> AvailableMissionParticipants { get; }

        internal IReadOnlyList<Mission> ActiveMissions { get; }

        public IReadOnlyList<(
            Planet Planet,
            Officer TargetOfficer
        )> TargetableEnemyOfficerMissionTargets { get; }

        // Fleets.
        public IReadOnlyList<Fleet> OwnedFleets { get; }

        public IReadOnlyList<Fleet> AttackOrderedFleets { get; }

        public IReadOnlyList<Fleet> EngagementOrderedFleets { get; }

        public IReadOnlyList<Fleet> ColonizationOrderedFleets { get; }

        /// <summary>
        /// Creates an AI assessment for a turn context.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        public AIAssessment(AITurnContext context)
        {
            _context = context;
            _opposingFactionIds =
                context
                    ?.Game?.GetFactions()
                    .Where(candidate => candidate.InstanceID != context.Faction?.InstanceID)
                    .Select(candidate => candidate.InstanceID)
                    .ToList()
                ?? new List<string>();
            _sabotageTargets = new AISabotageTargetPolicy(
                this,
                context?.Game?.Config?.AI?.MissionPlanning
            );
            Faction faction = context?.Faction;
            int availableMaterials = faction?.GetTotalAvailableMaterialsRaw() ?? 0;
            MaintenanceCapacity =
                availableMaterials * (faction?.Settings?.ResourceProcessingPointsPerFacility ?? 0);
            ProjectedMaintenanceHeadroom =
                MaintenanceCapacity - (faction?.GetTotalProjectedMaintenanceCost() ?? 0);
            RefinedMaterialSupply =
                availableMaterials * (faction?.Settings?.RefinementMultiplier ?? 0);
            RefinedMaterialStockpile = faction?.RefinedMaterialStockpile ?? 0;
            ActiveMissions = BuildActiveMissions();
            FactionViewPlanets = BuildFactionViewPlanets();
            UnexploredPlanets = FactionViewPlanets
                .Where(planet => planet.IsUnexploredView)
                .ToList();
            KnownColonizedPlanets = FactionViewPlanets
                .Where(planet =>
                    !planet.IsUnexploredView && planet.IsColonized && !planet.IsDestroyed
                )
                .ToList();
            KnownUncolonizedPlanets = FactionViewPlanets
                .Where(planet =>
                    !planet.IsUnexploredView
                    && !planet.IsColonized
                    && !planet.IsDestroyed
                    && string.IsNullOrEmpty(planet.GetOwnerInstanceID())
                )
                .ToList();
            foreach (
                Planet planet in FactionViewPlanets.Where(planet =>
                    !planet.IsUnexploredView && !planet.IsDestroyed
                )
            )
                _knownPlanets[planet.InstanceID] = planet;
            foreach (
                IGrouping<string, Planet> system in _knownPlanets
                    .Values.GroupBy(GetPlanetSystemId)
                    .Where(system => !string.IsNullOrEmpty(system.Key))
            )
            {
                _knownPlanetsBySystemId[system.Key] = system
                    .OrderBy(planet => planet.InstanceID, StringComparer.Ordinal)
                    .ToList();
            }
            foreach (Planet planet in _knownPlanets.Values)
            {
                if (
                    planet
                        .GetAllRegiments()
                        .Any(regiment =>
                            regiment.ManufacturingStatus == ManufacturingStatus.Complete
                            && regiment.Movement == null
                        )
                )
                    _knownGarrisonedPlanetIds.Add(planet.InstanceID);
            }
            OwnedPlanets = BuildOwnedPlanets();
            EnemyPlanets = BuildEnemyPlanets();
            NeutralPlanets = BuildNeutralPlanets();
            AvailableMissionParticipants = BuildAvailableMissionParticipants();
            TargetableEnemyOfficerMissionTargets = BuildTargetableEnemyOfficerMissionTargets();
            OwnedFleets = BuildOwnedFleets();
            AttackOrderedFleets = BuildAttackOrderedFleets();
            EngagementOrderedFleets = BuildEngagementOrderedFleets();
            ColonizationOrderedFleets = BuildColonizationOrderedFleets();
        }

        /// <summary>
        /// Returns whether a planet is the faction's active headquarters.
        /// </summary>
        /// <param name="planet">Planet to inspect.</param>
        /// <returns>True when the planet is the active headquarters.</returns>
        public bool IsFactionHeadquarters(Planet planet)
        {
            return planet != null
                && _context?.Faction != null
                && planet.InstanceID == _context.Faction.HQInstanceID
                && planet.GetOwnerInstanceID() == _context.Faction.InstanceID;
        }

        /// <summary>
        /// Returns whether the faction controls another faction's fixed headquarters planet.
        /// </summary>
        /// <param name="planet">Planet to inspect.</param>
        /// <returns>True when the planet is a captured enemy headquarters.</returns>
        public bool IsCapturedEnemyHeadquarters(Planet planet)
        {
            if (!IsOwnedPlanet(planet) || _context?.Game == null)
                return false;

            return _context
                .Game.GetFactions()
                .Any(faction =>
                    faction != null
                    && faction.InstanceID != _context.Faction.InstanceID
                    && faction.Settings?.Headquarters?.IsMobile != true
                    && faction.HQInstanceID == planet.InstanceID
                );
        }

        /// <summary>
        /// Returns whether a planet requires headquarters-level protection.
        /// </summary>
        /// <param name="planet">Planet to inspect.</param>
        /// <returns>True for the faction headquarters or a captured enemy headquarters.</returns>
        public bool IsPriorityDefensePlanet(Planet planet)
        {
            return IsFactionHeadquarters(planet) || IsCapturedEnemyHeadquarters(planet);
        }

        /// <summary>
        /// Returns a known planet by instance identifier.
        /// </summary>
        /// <param name="instanceId">Planet instance identifier.</param>
        /// <returns>The known planet, or null.</returns>
        public Planet GetKnownPlanet(string instanceId)
        {
            return
                !string.IsNullOrEmpty(instanceId)
                && _knownPlanets.TryGetValue(instanceId, out Planet planet)
                ? planet
                : null;
        }

        /// <summary>
        /// Returns the age of the faction's intelligence for a planet.
        /// </summary>
        /// <param name="planet">Planet to inspect.</param>
        /// <returns>Intel age in ticks, or the maximum integer when unknown.</returns>
        public int GetPlanetIntelAge(Planet planet)
        {
            if (planet == null || _context?.Faction?.Fog == null || _context.Game == null)
                return int.MaxValue;

            if (
                IsOwnedPlanet(planet)
                || GetFriendlyFleets(planet)
                    .Any(fleet => fleet.Movement == null && fleet.HasOperationalCapitalShips())
            )
                return 0;

            if (
                !_context.Faction.Fog.PlanetToSector.TryGetValue(
                    planet.InstanceID,
                    out string sectorId
                )
                || !_context.Faction.Fog.Snapshots.TryGetValue(
                    sectorId,
                    out PlanetSectorSnapshot sectorSnapshot
                )
                || !sectorSnapshot.Planets.TryGetValue(
                    planet.InstanceID,
                    out PlanetSnapshot snapshot
                )
            )
                return int.MaxValue;

            return Math.Max(0, _context.Game.CurrentTick - snapshot.TickCaptured);
        }

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
        /// Returns the production and economic value gained by diplomatically securing a planet.
        /// </summary>
        /// <param name="planet">The prospective diplomacy target.</param>
        /// <returns>The diplomacy target's strategic value.</returns>
        public int GetDiplomacyTargetStrategicValue(Planet planet)
        {
            if (planet == null || _context?.Game?.Config?.AI?.MissionPlanning == null)
                return 0;

            GameConfig.AIMissionPlanningConfig config = _context.Game.Config.AI.MissionPlanning;
            int value =
                planet.GetProductionFacilityCount(ManufacturingType.Building)
                    * config.DiplomacyConstructionFacilityWeight
                + planet.GetProductionFacilityCount(ManufacturingType.Ship)
                    * config.DiplomacyShipyardWeight
                + planet.GetProductionFacilityCount(ManufacturingType.Troop)
                    * config.DiplomacyTrainingFacilityWeight;

            int maintenanceReserve = _context
                .Game
                .Config
                .AI
                .Selection
                .MinimumMaintenanceHeadroomAfterProduction;
            if (ProjectedMaintenanceHeadroom < maintenanceReserve)
                value += planet.GetRawResourceNodes() * config.DiplomacyResourceNodeWeight;

            return value;
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
        /// Returns the containing system identifier for a planet.
        /// </summary>
        /// <param name="planet">Planet to inspect.</param>
        /// <returns>The system identifier, or an empty string.</returns>
        public string GetPlanetSystemId(Planet planet)
        {
            return planet?.GetParentOfType<PlanetSector>()?.InstanceID ?? string.Empty;
        }

        /// <summary>
        /// Returns known enemy planets in a system.
        /// </summary>
        /// <param name="systemId">System instance identifier.</param>
        /// <returns>The ordered enemy planets.</returns>
        public IReadOnlyList<Planet> GetAttackCampaignPlanets(string systemId)
        {
            if (string.IsNullOrEmpty(systemId))
                return Array.Empty<Planet>();

            return GetOrAdd(
                _attackCampaignPlanetsBySystemId,
                systemId,
                () =>
                    EnemyPlanets
                        .Where(planet => GetPlanetSystemId(planet) == systemId)
                        .OrderBy(planet => planet.InstanceID, StringComparer.Ordinal)
                        .ToList()
            );
        }

        /// <summary>
        /// Returns the campaign planets associated with an attack target.
        /// </summary>
        /// <param name="targetPlanet">Primary attack target.</param>
        /// <returns>The ordered campaign planets.</returns>
        public IReadOnlyList<Planet> GetAttackCampaignPlanets(Planet targetPlanet)
        {
            if (targetPlanet == null)
                return Array.Empty<Planet>();

            List<Planet> targets = GetAttackCampaignPlanets(GetPlanetSystemId(targetPlanet))
                .ToList();
            if (
                IsEnemyPlanet(targetPlanet)
                && targets.All(planet => planet.InstanceID != targetPlanet.InstanceID)
            )
                targets.Add(targetPlanet);

            return targets;
        }

        /// <summary>
        /// Returns the faction's share of known planets in a system.
        /// </summary>
        /// <param name="systemId">System instance identifier.</param>
        /// <returns>A ratio from zero through one.</returns>
        public double GetOwnedSystemPresenceRatio(string systemId)
        {
            if (string.IsNullOrEmpty(systemId))
                return 0;

            List<Planet> systemPlanets = FactionViewPlanets
                .Where(planet => GetPlanetSystemId(planet) == systemId)
                .ToList();
            if (systemPlanets.Count == 0)
                return 0;

            return (double)systemPlanets.Count(IsOwnedPlanet) / systemPlanets.Count;
        }

        /// <summary>
        /// Returns the known enemy planet count in a system.
        /// </summary>
        /// <param name="systemId">System instance identifier.</param>
        /// <returns>The enemy planet count.</returns>
        public int GetEnemyPlanetCountInSystem(string systemId)
        {
            return GetAttackCampaignPlanets(systemId).Count;
        }

        /// <summary>
        /// Returns the combined strategic value of known enemy planets in a system.
        /// </summary>
        /// <param name="systemId">System instance identifier.</param>
        /// <returns>The combined strategic value.</returns>
        public double GetEnemySystemValue(string systemId)
        {
            return GetAttackCampaignPlanets(systemId).Sum(GetPlanetValue);
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
        /// Returns the highest known uncolonized planet value.
        /// </summary>
        /// <returns>The highest value.</returns>
        public double GetHighestKnownUncolonizedPlanetValue()
        {
            return KnownUncolonizedPlanets.Select(GetPlanetValue).DefaultIfEmpty().Max();
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
        /// Returns the known support leverage gained by capturing a planet.
        /// </summary>
        /// <param name="planet">The prospective attack target.</param>
        /// <returns>The target and follow-on planets exposed by the resulting support shift.</returns>
        public int GetOffensiveSupportLeverage(Planet planet)
        {
            if (planet == null || _context?.Faction == null)
                return 0;

            return GetOrAdd(
                _offensiveSupportLeverage,
                planet.InstanceID,
                () => CalculateSupportLeverage(planet, _context.Faction.InstanceID)
            );
        }

        /// <summary>
        /// Returns the known sector-wide support risk created by losing an owned planet.
        /// </summary>
        /// <param name="planet">The owned planet being evaluated.</param>
        /// <returns>The target and follow-on planets exposed to the opposing faction.</returns>
        public int GetDefensiveSupportRisk(Planet planet)
        {
            if (planet == null || _context?.Faction == null || _context.Game == null)
                return 0;

            return GetOrAdd(
                _defensiveSupportRisks,
                planet.InstanceID,
                () => CalculateSupportLeverage(planet, GetLeadingOpposingFactionId(planet))
            );
        }

        /// <summary>
        /// Returns the combined offensive support leverage for known enemy planets in a system.
        /// </summary>
        /// <param name="systemId">The system identifier.</param>
        /// <returns>The combined support leverage.</returns>
        public int GetEnemySystemSupportLeverage(string systemId)
        {
            if (string.IsNullOrEmpty(systemId))
                return 0;

            return GetOrAdd(
                _enemySystemSupportLeverage,
                systemId,
                () => GetAttackCampaignPlanets(systemId).Sum(GetOffensiveSupportLeverage)
            );
        }

        /// <summary>
        /// Returns the opposing faction with the greatest known support on a planet.
        /// </summary>
        /// <param name="planet">The planet being evaluated.</param>
        /// <returns>The leading opposing faction identifier, or null when none exists.</returns>
        private string GetLeadingOpposingFactionId(Planet planet)
        {
            string leadingFactionId = null;
            int leadingSupport = int.MinValue;
            foreach (string factionId in _opposingFactionIds)
            {
                int support = planet.GetPopularSupport(factionId);
                if (support <= leadingSupport)
                    continue;

                leadingFactionId = factionId;
                leadingSupport = support;
            }

            return leadingFactionId;
        }

        /// <summary>
        /// Calculates how many known planets a change of control places within one support shift
        /// of the beneficiary's ownership threshold.
        /// </summary>
        /// <param name="planet">The planet whose control may change.</param>
        /// <param name="beneficiaryFactionId">The faction receiving the support shift.</param>
        /// <returns>The number of directly and indirectly exposed planets.</returns>
        private int CalculateSupportLeverage(Planet planet, string beneficiaryFactionId)
        {
            if (
                planet == null
                || string.IsNullOrEmpty(beneficiaryFactionId)
                || _context?.Game?.Config?.SupportShift == null
            )
                return 0;

            int leverage = 0;
            string ownerId = planet.GetOwnerInstanceID();
            int ownerSupport = string.IsNullOrEmpty(ownerId)
                ? 0
                : planet.GetPopularSupport(ownerId);
            if (
                ownerId != beneficiaryFactionId
                && planet.GetPopularSupport(beneficiaryFactionId) > ownerSupport
            )
                leverage++;

            string systemId = GetPlanetSystemId(planet);
            if (
                !_knownPlanetsBySystemId.TryGetValue(
                    systemId,
                    out IReadOnlyList<Planet> systemPlanets
                )
            )
                return leverage;

            int threshold = _context.Game.Config.SupportShift.OwnershipTransferThreshold;
            int shift = _context.Game.Config.SupportShift.GarrisonRemovalSupportShift;
            foreach (Planet candidate in systemPlanets)
            {
                if (
                    candidate.InstanceID == planet.InstanceID
                    || candidate.GetOwnerInstanceID() == beneficiaryFactionId
                    || _knownGarrisonedPlanetIds.Contains(candidate.InstanceID)
                )
                    continue;

                int support = candidate.GetPopularSupport(beneficiaryFactionId);
                if (support < threshold && support + shift >= threshold)
                    leverage++;
            }

            return leverage;
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
        /// Returns the buildings attached to a planet during this AI turn.
        /// </summary>
        /// <param name="planet">The planet to inspect.</param>
        /// <returns>The planet's buildings.</returns>
        public IReadOnlyList<Building> GetPlanetBuildings(Planet planet)
        {
            if (planet == null)
                return Array.Empty<Building>();

            return GetOrAdd(
                _planetBuildings,
                planet.InstanceID,
                () => planet.GetAllBuildings().ToList()
            );
        }

        /// <summary>
        /// Returns the regiments attached to a planet during this AI turn.
        /// </summary>
        /// <param name="planet">The planet to inspect.</param>
        /// <returns>The planet's regiments.</returns>
        public IReadOnlyList<Regiment> GetPlanetRegiments(Planet planet)
        {
            if (planet == null)
                return Array.Empty<Regiment>();

            return GetOrAdd(
                _planetRegiments,
                planet.InstanceID,
                () => planet.GetAllRegiments().ToList()
            );
        }

        /// <summary>
        /// Returns the starfighters attached to a planet during this AI turn.
        /// </summary>
        /// <param name="planet">The planet to inspect.</param>
        /// <returns>The planet's starfighters.</returns>
        public IReadOnlyList<Starfighter> GetPlanetStarfighters(Planet planet)
        {
            if (planet == null)
                return Array.Empty<Starfighter>();

            return GetOrAdd(
                _planetStarfighters,
                planet.InstanceID,
                () => planet.GetAllStarfighters().ToList()
            );
        }

        /// <summary>
        /// Returns a planet's production facility count during this AI turn.
        /// </summary>
        /// <param name="planet">The planet to inspect.</param>
        /// <param name="manufacturingType">The manufacturing category to count.</param>
        /// <returns>The number of matching production facilities.</returns>
        public int GetPlanetProductionFacilityCount(
            Planet planet,
            ManufacturingType manufacturingType
        )
        {
            if (planet == null)
                return 0;

            return GetOrAdd(
                _planetProductionFacilityCounts,
                (planet.InstanceID, manufacturingType),
                () => planet.GetProductionFacilityCount(manufacturingType)
            );
        }

        /// <summary>
        /// Returns a planet's production rate during this AI turn.
        /// </summary>
        /// <param name="planet">The planet to inspect.</param>
        /// <param name="manufacturingType">The manufacturing category to evaluate.</param>
        /// <returns>The matching production rate.</returns>
        public double GetPlanetProductionRate(Planet planet, ManufacturingType manufacturingType)
        {
            if (planet == null)
                return 0;

            return GetOrAdd(
                _planetProductionRates,
                (planet.InstanceID, manufacturingType),
                () => planet.GetProductionRate(manufacturingType)
            );
        }

        /// <summary>
        /// Returns officers known to be present at a planet.
        /// </summary>
        /// <param name="planet">Planet to inspect.</param>
        /// <returns>The known officers.</returns>
        public IEnumerable<Officer> GetKnownOfficers(Planet planet)
        {
            return planet?.GetChildren<Officer>(recursive: true) ?? Enumerable.Empty<Officer>();
        }

        /// <summary>
        /// Returns the active defending regiment count on a planet.
        /// </summary>
        /// <param name="planet">The planet to inspect.</param>
        /// <returns>The regiment count.</returns>
        public int GetDefendingRegimentCount(Planet planet)
        {
            string ownerId = planet?.GetOwnerInstanceID();
            if (string.IsNullOrEmpty(ownerId))
                return 0;

            return planet
                .GetAllRegiments()
                .Count(regiment =>
                    regiment.GetOwnerInstanceID() == ownerId
                    && regiment.ManufacturingStatus == ManufacturingStatus.Complete
                    && regiment.Movement == null
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
        public int GetDefendingRegimentDefenseStrength(Planet planet)
        {
            if (planet == null || _context?.Game?.Config == null)
                return 0;

            int leadershipBonus = PlanetaryAssaultSystem.GetLeadershipBonus(
                planet.GetAllOfficers(),
                OfficerRank.General,
                planet.GetOwnerInstanceID(),
                _context.Game.Config.Combat.PlanetaryAssault
            );
            return planet
                .GetAllRegiments()
                .Where(regiment =>
                    regiment.GetOwnerInstanceID() == planet.GetOwnerInstanceID()
                    && regiment.ManufacturingStatus == ManufacturingStatus.Complete
                    && regiment.Movement == null
                )
                .Sum(regiment => regiment.DefenseRating + leadershipBonus);
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
                        .GetChildren<Fleet>()
                        .Where(fleet => fleet.GetOwnerInstanceID() == _context?.Faction?.InstanceID)
                        .OrderBy(fleet => fleet.InstanceID)
                        .ToList()
            );
        }

        /// <summary>
        /// Returns whether a fleet is committed to headquarters defense.
        /// </summary>
        /// <param name="planet">Headquarters planet to inspect.</param>
        /// <returns>True when a fleet is committed.</returns>
        public bool HasCommittedHeadquartersFleet(Planet planet)
        {
            if (!IsFactionHeadquarters(planet))
                return false;

            return GetFriendlyFleets(planet)
                .Any(fleet =>
                    fleet
                        .GetChildren<CapitalShip>()
                        .Any(capitalShip =>
                            capitalShip.ManufacturingStatus
                                is ManufacturingStatus.Complete
                                    or ManufacturingStatus.Building
                        )
                );
        }

        /// <summary>
        /// Returns the strongest committed headquarters defense.
        /// </summary>
        /// <param name="planet">Headquarters planet to inspect.</param>
        /// <returns>The committed defense strength.</returns>
        public int GetCommittedHeadquartersDefenseStrength(Planet planet)
        {
            if (!IsPriorityDefensePlanet(planet))
                return 0;

            return GetFriendlyFleets(planet).Select(GetFleetCombatValue).DefaultIfEmpty().Max();
        }

        /// <summary>
        /// Returns the fleet strength required to defend a headquarters planet.
        /// </summary>
        /// <param name="planet">Headquarters planet to inspect.</param>
        /// <returns>The required defense strength.</returns>
        public int GetRequiredHeadquartersDefenseStrength(Planet planet)
        {
            if (!IsPriorityDefensePlanet(planet) || _context?.Game?.Config == null)
                return 0;

            GameConfig.AIFleetDeploymentConfig config = _context.Game.Config.AI.FleetDeployment;
            int hostileFleetRequirement = IntegerMath.ScaleByPercent(
                GetHeadquartersThreatStrength(planet),
                config.AttackStrengthPercentOfStrongestHostileFleet
            );
            return Math.Max(config.MinimumDefenseStrength, hostileFleetRequirement);
        }

        /// <summary>
        /// Returns the strongest known threat to a headquarters planet.
        /// </summary>
        /// <param name="headquarters">Headquarters planet to inspect.</param>
        /// <returns>The hostile fleet strength.</returns>
        private int GetHeadquartersThreatStrength(Planet headquarters)
        {
            return FactionViewPlanets
                .SelectMany(planet => GetHostileFleets(planet))
                .Where(fleet =>
                    (
                        fleet.Movement == null
                        && GetFleetPlanet(fleet)?.InstanceID == headquarters.InstanceID
                    )
                    || (
                        fleet.Order?.OrderType == FleetOrderType.Attack
                        && fleet.Order.TargetPlanetId == headquarters.InstanceID
                    )
                )
                .Select(GetFleetCombatValue)
                .DefaultIfEmpty()
                .Max();
        }

        /// <summary>
        /// Returns whether a fleet can leave without compromising headquarters defense.
        /// </summary>
        /// <param name="fleet">Fleet to inspect.</param>
        /// <returns>True when the fleet can depart.</returns>
        public bool CanFleetDepartHeadquarters(Fleet fleet)
        {
            Planet planet = GetFleetPlanet(fleet);
            if (!IsPriorityDefensePlanet(planet))
                return true;

            int remainingDefense = GetFriendlyFleets(planet)
                .Where(localFleet => localFleet != fleet && localFleet.Movement == null)
                .Select(GetFleetCombatValue)
                .DefaultIfEmpty()
                .Max();
            int requiredDefense = GetRequiredHeadquartersDefenseStrength(planet);
            return remainingDefense >= requiredDefense;
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
                        .GetChildren<Fleet>()
                        .Where(fleet =>
                            !string.IsNullOrEmpty(fleet.GetOwnerInstanceID())
                            && fleet.GetOwnerInstanceID() != _context?.Faction?.InstanceID
                        )
                        .OrderBy(fleet => fleet.InstanceID)
                        .ToList()
            );
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
                () => GetRequiredOrbitalStrength(planet)
            );
        }

        /// <summary>
        /// Returns the orbital strength required for a system campaign.
        /// </summary>
        /// <param name="systemId">Target system identifier.</param>
        /// <returns>The required combat strength.</returns>
        public int GetRequiredAttackCampaignCombatStrength(string systemId)
        {
            return GetRequiredAttackCampaignCombatStrength(GetAttackCampaignPlanets(systemId));
        }

        /// <summary>
        /// Returns the orbital strength required for a target planet's campaign.
        /// </summary>
        /// <param name="targetPlanet">Primary campaign target.</param>
        /// <returns>The required combat strength.</returns>
        public int GetRequiredAttackCampaignCombatStrength(Planet targetPlanet)
        {
            return GetRequiredAttackCampaignCombatStrength(GetAttackCampaignPlanets(targetPlanet));
        }

        /// <summary>
        /// Returns the orbital strength required for a set of campaign planets.
        /// </summary>
        /// <param name="targetPlanets">Campaign planets to evaluate.</param>
        /// <returns>The required combat strength.</returns>
        private int GetRequiredAttackCampaignCombatStrength(
            IReadOnlyCollection<Planet> targetPlanets
        )
        {
            if (targetPlanets == null || targetPlanets.Count == 0 || _context?.Game?.Config == null)
                return 0;

            return SumRequirements(targetPlanets, GetRequiredOrbitalStrength);
        }

        /// <summary>
        /// Returns the regiment count required for a target planet's campaign.
        /// </summary>
        /// <param name="targetPlanet">Primary campaign target.</param>
        /// <returns>The required regiment count.</returns>
        public int GetRequiredAttackCampaignRegimentCount(Planet targetPlanet)
        {
            return SumRequirements(
                GetAttackCampaignPlanets(targetPlanet),
                GetRequiredAttackRegimentCount
            );
        }

        /// <summary>
        /// Returns the regiment count required for a system campaign.
        /// </summary>
        /// <param name="systemId">Target system identifier.</param>
        /// <returns>The required regiment count.</returns>
        public int GetRequiredAttackCampaignRegimentCount(string systemId)
        {
            return SumRequirements(
                GetAttackCampaignPlanets(systemId),
                GetRequiredAttackRegimentCount
            );
        }

        /// <summary>
        /// Returns the regiment strength required for a target planet's campaign.
        /// </summary>
        /// <param name="targetPlanet">Primary campaign target.</param>
        /// <returns>The required regiment strength.</returns>
        public int GetRequiredAttackCampaignRegimentStrength(Planet targetPlanet)
        {
            return SumRequirements(
                GetAttackCampaignPlanets(targetPlanet),
                GetRequiredAttackRegimentStrength
            );
        }

        /// <summary>
        /// Returns the bombardment strength required for a target planet's campaign.
        /// </summary>
        /// <param name="targetPlanet">Primary campaign target.</param>
        /// <returns>The required bombardment strength.</returns>
        public int GetRequiredAttackCampaignBombardmentStrength(Planet targetPlanet)
        {
            return GetAttackCampaignPlanets(targetPlanet)
                .Select(GetRequiredBombardmentStrength)
                .DefaultIfEmpty()
                .Max();
        }

        /// <summary>
        /// Returns the bombardment strength required for a system campaign.
        /// </summary>
        /// <param name="systemId">Target system identifier.</param>
        /// <returns>The required bombardment strength.</returns>
        public int GetRequiredAttackCampaignBombardmentStrength(string systemId)
        {
            return GetAttackCampaignPlanets(systemId)
                .Select(GetRequiredBombardmentStrength)
                .DefaultIfEmpty()
                .Max();
        }

        /// <summary>
        /// Returns the orbital strength required to defeat known forces at a planet.
        /// </summary>
        /// <param name="planet">Planet to inspect.</param>
        /// <returns>The required orbital strength.</returns>
        public int GetRequiredOrbitalStrength(Planet planet)
        {
            if (planet == null || _context?.Game?.Config == null)
                return 0;

            int hostileStrength =
                GetStrongestHostileFleetStrength(planet)
                + GetHostilePlanetaryStarfighterStrength(planet);
            return hostileStrength > 0
                ? IntegerMath.ScaleByPercent(
                    hostileStrength,
                    _context
                        .Game
                        .Config
                        .AI
                        .FleetDeployment
                        .AttackStrengthPercentOfStrongestHostileFleet
                )
                : 0;
        }

        /// <summary>
        /// Returns the known hostile planetary starfighter strength.
        /// </summary>
        /// <param name="planet">Planet to inspect.</param>
        /// <returns>The hostile starfighter strength.</returns>
        public int GetHostilePlanetaryStarfighterStrength(Planet planet)
        {
            if (planet == null)
                return 0;

            return GetOrAdd(
                _hostilePlanetaryStarfighterStrengths,
                planet.InstanceID,
                () =>
                    planet
                        .GetAllStarfighters()
                        .Where(starfighter =>
                            !string.IsNullOrEmpty(starfighter.GetOwnerInstanceID())
                            && starfighter.GetOwnerInstanceID() != _context?.Faction?.InstanceID
                        )
                        .Sum(starfighter => starfighter.GetCombatValue())
            );
        }

        /// <summary>
        /// Returns the strongest known fleet threat to a friendly planet.
        /// </summary>
        /// <param name="planet">Planet to inspect.</param>
        /// <returns>The hostile fleet strength.</returns>
        public int GetPlanetDefenseThreatStrength(Planet planet)
        {
            if (planet == null)
                return 0;

            return GetOrAdd(
                _planetDefenseThreatStrengths,
                planet.InstanceID,
                () => GetHostileFleets(planet).Select(GetFleetCombatValue).DefaultIfEmpty().Max()
            );
        }

        /// <summary>
        /// Returns whether a planet faces a known enemy presence: a hostile fleet in contact or
        /// a known enemy colony in the planet's sector.
        /// </summary>
        /// <param name="planet">The planet to inspect.</param>
        /// <returns>True when the planet is threatened.</returns>
        public bool IsPlanetThreatened(Planet planet)
        {
            if (planet == null)
                return false;

            if (GetPlanetDefenseThreatStrength(planet) > 0)
                return true;

            string sectorId = planet.GetParentOfType<PlanetSector>()?.InstanceID;
            if (string.IsNullOrEmpty(sectorId))
                return false;

            _hostileSectorIds ??= KnownColonizedPlanets
                .Where(known =>
                {
                    string ownerId = known.GetOwnerInstanceID();
                    return !string.IsNullOrEmpty(ownerId)
                        && ownerId != _context?.Faction?.InstanceID;
                })
                .Select(known => known.GetParentOfType<PlanetSector>()?.InstanceID)
                .Where(id => !string.IsNullOrEmpty(id))
                .ToHashSet(StringComparer.Ordinal);

            return _hostileSectorIds.Contains(sectorId);
        }

        /// <summary>
        /// Returns the fleet strength required to defend an ordinary planet.
        /// </summary>
        /// <param name="planet">Planet to inspect.</param>
        /// <returns>The required defense strength.</returns>
        public int GetRequiredPlanetDefenseStrength(Planet planet)
        {
            if (!IsOwnedPlanet(planet) || _context?.Game?.Config == null)
                return 0;

            int hostileStrength = GetPlanetDefenseThreatStrength(planet);
            return hostileStrength > 0
                ? IntegerMath.ScaleByPercent(
                    hostileStrength,
                    _context
                        .Game
                        .Config
                        .AI
                        .FleetDeployment
                        .AttackStrengthPercentOfStrongestHostileFleet
                )
                : 0;
        }

        /// <summary>
        /// Returns the fleet strength required to defend a planet.
        /// </summary>
        /// <param name="planet">Planet to inspect.</param>
        /// <returns>The required defense strength.</returns>
        public int GetRequiredDefenseStrength(Planet planet)
        {
            return IsPriorityDefensePlanet(planet)
                ? GetRequiredHeadquartersDefenseStrength(planet)
                : GetRequiredPlanetDefenseStrength(planet);
        }

        /// <summary>
        /// Returns whether a fleet can satisfy a planet's defense requirement.
        /// </summary>
        /// <param name="fleet">Candidate defense fleet.</param>
        /// <param name="planet">Planet to defend.</param>
        /// <returns>True when the fleet is sufficient.</returns>
        public bool CanDefendPlanet(Fleet fleet, Planet planet)
        {
            int requiredStrength = GetRequiredDefenseStrength(planet);
            return requiredStrength > 0
                && fleet?.HasOperationalCapitalShips() == true
                && GetReadyFleetCombatValue(fleet) >= requiredStrength;
        }

        /// <summary>
        /// Returns energy needed to complete a planet's static defense target.
        /// </summary>
        /// <param name="planet">Planet to inspect.</param>
        /// <returns>The energy deficit.</returns>
        public int GetPlanetaryDefenseEnergyDeficit(Planet planet)
        {
            if (!IsOwnedPlanet(planet) || _context?.Game?.Config == null)
                return 0;

            int shieldCount = planet
                .GetAllBuildings()
                .Count(building =>
                    building.GetOwnerInstanceID() == _context.Faction.InstanceID
                    && building.IsPlanetaryShieldGenerator()
                );
            int weaponCount = planet
                .GetAllBuildings()
                .Count(building =>
                    building.GetOwnerInstanceID() == _context.Faction.InstanceID
                    && building.GetBuildingType() == BuildingType.Weapon
                );
            int shieldDeficit = Math.Max(
                0,
                _context.Game.Config.Combat.PlanetaryAssault.ShieldGeneratorLimit - shieldCount
            );
            int weaponDeficit = Math.Max(
                0,
                _context.Game.Config.AI.Infrastructure.PlanetaryWeaponTargetCount - weaponCount
            );
            return shieldDeficit + weaponDeficit;
        }

        /// <summary>
        /// Returns whether a fleet can defeat known orbital defenders.
        /// </summary>
        /// <param name="fleet">Attacking fleet.</param>
        /// <param name="planet">Target planet.</param>
        /// <returns>True when the fleet has sufficient strength.</returns>
        public bool CanWinOrbitalCombat(Fleet fleet, Planet planet)
        {
            int requiredStrength = GetRequiredOrbitalStrength(planet);
            return requiredStrength > 0
                && fleet?.HasOperationalCapitalShips() == true
                && GetReadyFleetCombatValue(fleet) >= requiredStrength;
        }

        /// <summary>
        /// Returns regiment strength required to capture a planet.
        /// </summary>
        /// <param name="planet">Target planet.</param>
        /// <returns>The required regiment strength.</returns>
        public int GetRequiredAttackRegimentStrength(Planet planet)
        {
            if (planet == null || _context?.Game?.Config == null)
                return 0;

            return Math.Max(
                1,
                IntegerMath.ScaleByPercent(
                    GetDefendingRegimentDefenseStrength(planet),
                    _context.Game.Config.AI.FleetDeployment.AttackStrengthPercentOfDefense
                )
            );
        }

        /// <summary>
        /// Returns the ground strength required after accounting for the fleet's bombardment.
        /// </summary>
        /// <param name="fleet">Fleet assigned to the attack.</param>
        /// <param name="planet">Planet being attacked.</param>
        /// <returns>The required regiment attack strength.</returns>
        public int GetRequiredAttackRegimentStrength(Fleet fleet, Planet planet)
        {
            return CanBombardDefendingRegiments(fleet, planet, projected: false)
                ? 0
                : GetRequiredAttackRegimentStrength(planet);
        }

        /// <summary>
        /// Returns the projected ground strength requirement after bombardment.
        /// </summary>
        /// <param name="fleet">Fleet assigned to the attack.</param>
        /// <param name="planet">Planet being attacked.</param>
        /// <returns>The projected required regiment attack strength.</returns>
        public int GetProjectedRequiredAttackRegimentStrength(Fleet fleet, Planet planet)
        {
            return CanBombardDefendingRegiments(fleet, planet, projected: true)
                ? 0
                : GetRequiredAttackRegimentStrength(planet);
        }

        /// <summary>
        /// Returns bombardment strength required to penetrate planetary shields.
        /// </summary>
        /// <param name="planet">Target planet.</param>
        /// <returns>The required bombardment strength.</returns>
        public int GetRequiredBombardmentStrength(Planet planet)
        {
            if (!IsAssaultBlockedByShields(planet))
                return 0;

            return BombardmentSystem.GetBombardmentShieldStrength(planet) + 1;
        }

        /// <summary>
        /// Returns whether planetary shields currently block an assault.
        /// </summary>
        /// <param name="planet">Target planet.</param>
        /// <returns>True when shields block the assault.</returns>
        public bool IsAssaultBlockedByShields(Planet planet)
        {
            if (planet == null || _context?.Game?.Config == null)
                return false;

            return PlanetaryAssaultSystem.IsBlockedByShields(
                planet,
                _context.Game.Config.Combat.PlanetaryAssault.ShieldGeneratorLimit
            );
        }

        /// <summary>
        /// Returns whether a fleet cannot penetrate shields that prevent its ground assault.
        /// </summary>
        /// <param name="fleet">Fleet assigned to the attack.</param>
        /// <param name="planet">Target planet.</param>
        /// <returns>True when the fleet must wait for sabotage or choose another target.</returns>
        public bool IsFleetBlockedByTargetShields(Fleet fleet, Planet planet)
        {
            return IsAssaultBlockedByShields(planet)
                && GetFleetBombardmentStrength(fleet) < GetRequiredBombardmentStrength(planet);
        }

        /// <summary>
        /// Returns whether shields block a planet selected for attack preparation.
        /// </summary>
        /// <param name="planet">Target planet.</param>
        /// <returns>True when the target is blocked.</returns>
        public bool IsAttackTargetBlockedByShields(Planet planet)
        {
            return IsAssaultBlockedByShields(planet) && IsAttackPreparationTarget(planet);
        }

        /// <summary>
        /// Returns whether a planet belongs to an active attack campaign.
        /// </summary>
        /// <param name="planet">Planet to inspect.</param>
        /// <returns>True when the planet is an attack-preparation target.</returns>
        public bool IsAttackPreparationTarget(Planet planet)
        {
            return planet != null
                && AttackOrderedFleets.Any(fleet =>
                    fleet.Order.TargetPlanetId == planet.InstanceID
                );
        }

        /// <summary>
        /// Returns the contextual priority bonus for a sabotage target.
        /// </summary>
        /// <param name="planet">Planet containing the target.</param>
        /// <param name="target">Target to evaluate.</param>
        /// <returns>The priority bonus.</returns>
        public int GetSabotageTargetPriorityBonus(Planet planet, IManufacturable target)
        {
            return _sabotageTargets.GetPriorityBonus(planet, target);
        }

        /// <summary>
        /// Returns the tactical priority tier for a sabotage target.
        /// </summary>
        /// <param name="target">The sabotage target to classify.</param>
        /// <returns>A larger value for targets that must be destroyed first.</returns>
        public static int GetSabotageTargetPriority(IManufacturable target)
        {
            return AISabotageTargetPolicy.GetPriority(target);
        }

        /// <summary>
        /// Returns regiment count required to attack a planet.
        /// </summary>
        /// <param name="planet">The planet to inspect.</param>
        /// <returns>The required attack regiment count.</returns>
        public int GetRequiredAttackRegimentCount(Planet planet)
        {
            if (planet == null || _context?.Game?.Config == null)
                return 0;

            return GetOrAdd(
                _planetRequiredAttackRegimentCounts,
                planet.InstanceID,
                () =>
                {
                    return GetDefendingRegimentCount(planet) + 1;
                }
            );
        }

        /// <summary>
        /// Returns the regiment count required after accounting for the fleet's bombardment.
        /// </summary>
        /// <param name="fleet">Fleet assigned to the attack.</param>
        /// <param name="planet">Planet being attacked.</param>
        /// <returns>The required regiment count.</returns>
        public int GetRequiredAttackRegimentCount(Fleet fleet, Planet planet)
        {
            return CanBombardDefendingRegiments(fleet, planet, projected: false)
                ? GetRequiredOccupationRegimentCount(planet)
                : GetRequiredAttackRegimentCount(planet);
        }

        /// <summary>
        /// Returns the projected regiment count requirement after bombardment.
        /// </summary>
        /// <param name="fleet">Fleet assigned to the attack.</param>
        /// <param name="planet">Planet being attacked.</param>
        /// <returns>The projected required regiment count.</returns>
        public int GetProjectedRequiredAttackRegimentCount(Fleet fleet, Planet planet)
        {
            return CanBombardDefendingRegiments(fleet, planet, projected: true)
                ? GetRequiredOccupationRegimentCount(planet)
                : GetRequiredAttackRegimentCount(planet);
        }

        /// <summary>
        /// Returns the force needed to hold a planet after its defenders are removed.
        /// </summary>
        /// <param name="planet">Planet being occupied.</param>
        /// <returns>The required occupation regiment count.</returns>
        private int GetRequiredOccupationRegimentCount(Planet planet)
        {
            if (planet == null || _context?.Game?.Config == null)
                return 0;

            int minimum = Math.Max(
                1,
                _context.Game.Config.AI.FleetDeployment.MinimumPlanetaryAssaultRegimentCount
            );
            int stabilityRequirement = UprisingSystem.CalculateGarrisonRequirement(
                planet,
                _context.Faction,
                _context.Game.Config.AI.Garrison
            );
            int landingCapacity = _context.Game.Config.Combat.PlanetaryAssault.CaptureGarrisonCount;
            return Math.Max(minimum, Math.Min(stabilityRequirement, landingCapacity));
        }

        /// <summary>
        /// Returns whether a fleet can penetrate the target's shields and bombard its garrison.
        /// </summary>
        /// <param name="fleet">Fleet assigned to the attack.</param>
        /// <param name="planet">Planet being attacked.</param>
        /// <param name="projected">Whether queued fleet strength is included.</param>
        /// <returns>True when military bombardment can reach defending regiments.</returns>
        private bool CanBombardDefendingRegiments(Fleet fleet, Planet planet, bool projected)
        {
            if (fleet == null || planet == null || GetDefendingRegimentCount(planet) == 0)
                return false;

            int bombardmentStrength = projected
                ? GetProjectedFleetBombardmentStrength(fleet)
                : GetFleetBombardmentStrength(fleet);
            return bombardmentStrength > BombardmentSystem.GetBombardmentShieldStrength(planet);
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
        /// Returns the active enemy attack target assigned to a fleet.
        /// </summary>
        /// <param name="fleet">The fleet to inspect.</param>
        /// <returns>The attack target planet, or null when the order is not actionable.</returns>
        public Planet GetAttackTargetPlanet(Fleet fleet)
        {
            string targetPlanetId = fleet?.Order?.TargetPlanetId;
            if (
                fleet?.Order?.OrderType != FleetOrderType.Attack
                || string.IsNullOrEmpty(targetPlanetId)
            )
                return null;

            Planet targetPlanet = GetKnownPlanet(targetPlanetId);
            return IsEnemyPlanet(targetPlanet) ? targetPlanet : null;
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
        private static bool HasAttackOrder(Fleet fleet)
        {
            return fleet?.Order?.OrderType == FleetOrderType.Attack;
        }

        /// <summary>
        /// Returns whether a fleet has a colonization order.
        /// </summary>
        /// <param name="fleet">Fleet to inspect.</param>
        /// <returns>True when the fleet is colonizing.</returns>
        private static bool HasColonizationOrder(Fleet fleet)
        {
            return fleet?.Order?.OrderType == FleetOrderType.Colonize;
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
            int availableCombat = GetReadyFleetCombatValue(fleet);
            int requiredRegiments = GetRequiredAttackRegimentCount(fleet, targetPlanet);
            int requiredRegimentStrength = GetRequiredAttackRegimentStrength(fleet, targetPlanet);
            int requiredBombardment = GetRequiredBombardmentStrength(targetPlanet);
            return fleet?.HasOperationalCapitalShips() == true
                && availableCombat > 0
                && availableCombat >= requiredCombat
                && GetReadyFleetRegimentCount(fleet) >= requiredRegiments
                && GetReadyFleetRegimentCapacity(fleet) >= requiredRegiments
                && GetReadyFleetRegimentAttackStrength(fleet) >= requiredRegimentStrength
                && GetFleetBombardmentStrength(fleet) >= requiredBombardment
                && (
                    CanBombardMilitaryTargets(fleet, targetPlanet)
                    || GetPlanetaryAssaultSuccessPercent(fleet, targetPlanet)
                        >= _context
                            .Game
                            .Config
                            .AI
                            .FleetDeployment
                            .MinimumPlanetaryAssaultSuccessPercent
                );
        }

        /// <summary>
        /// Estimates the chance that a fleet captures a planet in an immediate ground assault.
        /// </summary>
        /// <param name="fleet">Fleet assigned to the attack.</param>
        /// <param name="targetPlanet">Planet being attacked.</param>
        /// <returns>The estimated success chance from zero through one hundred.</returns>
        public int GetPlanetaryAssaultSuccessPercent(Fleet fleet, Planet targetPlanet)
        {
            if (fleet == null || targetPlanet == null || _context?.Game?.Config == null)
                return 0;

            return PlanetaryAssaultSystem.EstimateSuccessPercent(
                new List<Fleet> { fleet },
                targetPlanet,
                _context.Game.Config.Combat.PlanetaryAssault
            );
        }

        /// <summary>
        /// Returns whether a fleet can weaken a planet before evaluating a ground assault.
        /// </summary>
        /// <param name="fleet">Fleet assigned to the attack.</param>
        /// <param name="targetPlanet">Planet being attacked.</param>
        /// <returns>True when military targets remain within the fleet's bombardment capability.</returns>
        private bool CanBombardMilitaryTargets(Fleet fleet, Planet targetPlanet)
        {
            return fleet != null
                && targetPlanet != null
                && GetFleetBombardmentStrength(fleet)
                    > BombardmentSystem.GetBombardmentShieldStrength(targetPlanet)
                && BombardmentSystem.HasActiveMilitaryTargets(
                    targetPlanet,
                    targetPlanet.GetOwnerInstanceID()
                );
        }

        /// <summary>
        /// Returns whether projected fleet strength satisfies one attack target.
        /// </summary>
        /// <param name="fleet">Fleet to inspect.</param>
        /// <param name="targetPlanet">Target planet.</param>
        /// <returns>True when projected strength is sufficient.</returns>
        public bool IsFleetProjectedReadyToAttack(Fleet fleet, Planet targetPlanet)
        {
            int requiredCombat = GetRequiredAttackCombatStrength(targetPlanet);
            int availableCombat = GetProjectedFleetCombatValue(fleet);
            int requiredRegiments = GetProjectedRequiredAttackRegimentCount(fleet, targetPlanet);
            int requiredRegimentStrength = GetProjectedRequiredAttackRegimentStrength(
                fleet,
                targetPlanet
            );
            int requiredBombardment = GetRequiredBombardmentStrength(targetPlanet);
            return fleet?.GetChildren<CapitalShip>().Any(capitalShip => capitalShip != null) == true
                && availableCombat > 0
                && availableCombat >= requiredCombat
                && GetFleetLoadedRegimentCount(fleet) >= requiredRegiments
                && GetFleetRegimentCapacity(fleet) >= requiredRegiments
                && GetProjectedFleetRegimentAttackStrength(fleet) >= requiredRegimentStrength
                && GetProjectedFleetBombardmentStrength(fleet) >= requiredBombardment;
        }

        /// <summary>
        /// Returns whether projected fleet strength satisfies the target campaign.
        /// </summary>
        /// <param name="fleet">Fleet to inspect.</param>
        /// <param name="targetPlanet">Primary campaign target.</param>
        /// <returns>True when projected strength is sufficient.</returns>
        public bool IsFleetProjectedReadyToAttackCampaign(Fleet fleet, Planet targetPlanet)
        {
            int requiredCombat = GetRequiredAttackCampaignCombatStrength(targetPlanet);
            int availableCombat = GetProjectedFleetCombatValue(fleet);
            int requiredRegiments = GetRequiredAttackCampaignRegimentCount(targetPlanet);
            int requiredRegimentStrength = GetRequiredAttackCampaignRegimentStrength(targetPlanet);
            int requiredBombardment = GetRequiredAttackCampaignBombardmentStrength(targetPlanet);
            return fleet?.GetChildren<CapitalShip>().Any(capitalShip => capitalShip != null) == true
                && availableCombat > 0
                && availableCombat >= requiredCombat
                && GetFleetLoadedRegimentCount(fleet) >= requiredRegiments
                && GetFleetRegimentCapacity(fleet) >= requiredRegiments
                && GetProjectedFleetRegimentAttackStrength(fleet) >= requiredRegimentStrength
                && GetProjectedFleetBombardmentStrength(fleet) >= requiredBombardment;
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
            int requiredRegiments = GetRequiredAttackRegimentCount(fleet, targetPlanet);
            int requiredRegimentStrength = GetRequiredAttackRegimentStrength(fleet, targetPlanet);
            int requiredBombardment = GetRequiredBombardmentStrength(targetPlanet);
            int gateCount = 0;

            if (fleet?.HasOperationalCapitalShips() == true)
                gateCount++;

            int availableCombat = GetReadyFleetCombatValue(fleet);
            if (availableCombat > 0 && availableCombat >= requiredCombat)
                gateCount++;

            if (GetReadyFleetRegimentCount(fleet) >= requiredRegiments)
                gateCount++;

            if (GetReadyFleetRegimentCapacity(fleet) >= requiredRegiments)
                gateCount++;

            if (GetReadyFleetRegimentAttackStrength(fleet) >= requiredRegimentStrength)
                gateCount++;

            if (GetFleetBombardmentStrength(fleet) >= requiredBombardment)
                gateCount++;

            return gateCount;
        }

        /// <summary>
        /// Returns the number of campaign readiness requirements a fleet satisfies.
        /// </summary>
        /// <param name="fleet">Fleet to inspect.</param>
        /// <param name="targetPlanet">Primary campaign target.</param>
        /// <returns>The satisfied requirement count.</returns>
        public int GetFleetAttackCampaignReadinessGateCount(Fleet fleet, Planet targetPlanet)
        {
            int requiredCombat = GetRequiredAttackCampaignCombatStrength(targetPlanet);
            int requiredRegiments = GetRequiredAttackCampaignRegimentCount(targetPlanet);
            int requiredRegimentStrength = GetRequiredAttackCampaignRegimentStrength(targetPlanet);
            int requiredBombardment = GetRequiredAttackCampaignBombardmentStrength(targetPlanet);
            int gateCount = 0;

            if (fleet?.HasOperationalCapitalShips() == true)
                gateCount++;

            int availableCombat = GetReadyFleetCombatValue(fleet);
            if (availableCombat > 0 && availableCombat >= requiredCombat)
                gateCount++;

            if (GetReadyFleetRegimentCount(fleet) >= requiredRegiments)
                gateCount++;

            if (GetReadyFleetRegimentCapacity(fleet) >= requiredRegiments)
                gateCount++;

            if (GetReadyFleetRegimentAttackStrength(fleet) >= requiredRegimentStrength)
                gateCount++;

            if (GetFleetBombardmentStrength(fleet) >= requiredBombardment)
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
        /// Returns fleet combat value including committed reinforcements.
        /// </summary>
        /// <param name="fleet">Fleet to inspect.</param>
        /// <returns>The projected combat value.</returns>
        public int GetProjectedFleetCombatValue(Fleet fleet)
        {
            if (fleet == null)
                return 0;

            return fleet.GetChildren<CapitalShip>().Sum(GetProjectedCapitalShipCombatValue);
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
                .GetChildren<CapitalShip>()
                .Where(IsReadyCapitalShip)
                .SelectMany(ship => ship.GetChildren<Regiment>())
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
                .GetChildren<CapitalShip>()
                .Where(IsReadyCapitalShip)
                .Sum(ship => ship.GetRegimentCapacity());
        }

        /// <summary>
        /// Returns attack strength from ready loaded regiments in a fleet.
        /// </summary>
        /// <param name="fleet">The fleet to inspect.</param>
        /// <returns>The ready loaded regiment attack strength.</returns>
        public int GetReadyFleetRegimentAttackStrength(Fleet fleet)
        {
            if (fleet == null || _context?.Game?.Config == null)
                return 0;

            int leadershipBonus = PlanetaryAssaultSystem.GetLeadershipBonus(
                fleet.GetOfficers(),
                OfficerRank.General,
                fleet.GetOwnerInstanceID(),
                _context.Game.Config.Combat.PlanetaryAssault
            );
            return fleet
                .GetChildren<CapitalShip>()
                .Where(IsReadyCapitalShip)
                .SelectMany(ship => ship.GetChildren<Regiment>())
                .Where(IsReadyRegiment)
                .Sum(regiment => regiment.AttackRating + leadershipBonus);
        }

        /// <summary>
        /// Returns loaded regiment strength including committed reinforcements.
        /// </summary>
        /// <param name="fleet">Fleet to inspect.</param>
        /// <returns>The projected regiment strength.</returns>
        public int GetProjectedFleetRegimentAttackStrength(Fleet fleet)
        {
            if (fleet == null || _context?.Game?.Config == null)
                return 0;

            int leadershipBonus = PlanetaryAssaultSystem.GetLeadershipBonus(
                fleet.GetOfficers(),
                OfficerRank.General,
                fleet.GetOwnerInstanceID(),
                _context.Game.Config.Combat.PlanetaryAssault
            );
            return fleet
                .GetRegiments()
                .Where(regiment => regiment != null)
                .Sum(regiment => regiment.AttackRating + leadershipBonus);
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

            return capitalShip.GetChildren<Regiment>().Count(IsReadyRegiment);
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
        /// Returns projected loaded regiment strength for a capital ship.
        /// </summary>
        /// <param name="targetFleet">Fleet containing the ship.</param>
        /// <param name="capitalShip">Capital ship to inspect.</param>
        /// <returns>The projected regiment strength.</returns>
        public int GetProjectedCapitalShipRegimentAttackStrength(
            Fleet targetFleet,
            CapitalShip capitalShip
        )
        {
            if (
                targetFleet == null
                || !IsReadyCapitalShip(capitalShip)
                || _context?.Game?.Config == null
            )
                return 0;

            int leadershipBonus = PlanetaryAssaultSystem.GetLeadershipBonus(
                targetFleet.GetOfficers(),
                OfficerRank.General,
                targetFleet.GetOwnerInstanceID(),
                _context.Game.Config.Combat.PlanetaryAssault
            );
            return capitalShip
                .GetChildren<Regiment>()
                .Where(IsReadyRegiment)
                .Sum(regiment => regiment.AttackRating + leadershipBonus);
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
                    BombardmentSystem.GetBombardmentStrength(
                        new[] { fleet },
                        _context.Game.Config.Combat.Bombardment
                    )
            );
        }

        /// <summary>
        /// Returns fleet bombardment strength including committed reinforcements.
        /// </summary>
        /// <param name="fleet">Fleet to inspect.</param>
        /// <returns>The projected bombardment strength.</returns>
        public int GetProjectedFleetBombardmentStrength(Fleet fleet)
        {
            if (fleet == null || _context?.Game?.Config == null)
                return 0;

            return GetOrAdd(
                _projectedFleetBombardmentStrengths,
                fleet.InstanceID,
                () =>
                    BombardmentSystem.GetProjectedBombardmentStrength(
                        fleet,
                        _context.Game.Config.Combat.Bombardment
                    )
            );
        }

        /// <summary>
        /// Returns projected bombardment strength for a capital ship.
        /// </summary>
        /// <param name="fleet">Fleet containing the ship.</param>
        /// <param name="capitalShip">Capital ship to inspect.</param>
        /// <returns>The projected bombardment strength.</returns>
        public int GetProjectedCapitalShipBombardmentStrength(Fleet fleet, CapitalShip capitalShip)
        {
            if (fleet == null || capitalShip == null || _context?.Game?.Config == null)
                return 0;

            return BombardmentSystem.GetProjectedCapitalShipBombardmentStrength(
                fleet,
                capitalShip,
                _context.Game.Config.Combat.Bombardment
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
        /// Returns loaded starfighter count for a fleet.
        /// </summary>
        /// <param name="fleet">The fleet to inspect.</param>
        /// <returns>The loaded starfighter count.</returns>
        public int GetFleetLoadedStarfighterCount(Fleet fleet)
        {
            return fleet?.GetCurrentStarfighterCount() ?? 0;
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
                () => OwnedPlanets.Sum(planet => planet.GetAvailableManufacturingCapacity(type))
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
        /// Builds the known colonized planet list.
        /// </summary>
        /// <returns>Known colonized planets.</returns>
        private List<Planet> BuildFactionViewPlanets()
        {
            if (_context?.FactionView == null)
                return new List<Planet>();

            return _context
                .FactionView.GetChildren<PlanetSector>()
                .SelectMany(sector => sector.GetChildren<Planet>())
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
            return KnownColonizedPlanets.Where(IsEnemyPlanet).ToList();
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
        /// Builds the active mission list visible to the faction.
        /// </summary>
        /// <returns>The active missions.</returns>
        private List<Mission> BuildActiveMissions()
        {
            if (_context?.Game == null || _context.Faction == null)
                return new List<Mission>();

            return _context.Game.GetSceneNodesByType<Mission>(mission =>
                mission.GetOwnerInstanceID() == _context.Faction.InstanceID
            );
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
                    GetKnownOfficers(planet)
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
                && officer.GetOwnerInstanceID() != _context.Faction.InstanceID
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
        /// Builds the attack-ordered fleet list.
        /// </summary>
        /// <returns>Attack-ordered fleets.</returns>
        private List<Fleet> BuildAttackOrderedFleets()
        {
            return OwnedFleets.Where(HasAttackOrder).ToList();
        }

        /// <summary>
        /// Builds the fleet list assigned to orbital engagements.
        /// </summary>
        /// <returns>Engagement-ordered fleets.</returns>
        private List<Fleet> BuildEngagementOrderedFleets()
        {
            return OwnedFleets
                .Where(fleet => fleet?.Order?.OrderType == FleetOrderType.Engage)
                .ToList();
        }

        /// <summary>
        /// Builds the owned fleet list with active colonization orders.
        /// </summary>
        /// <returns>The colonization fleets.</returns>
        private List<Fleet> BuildColonizationOrderedFleets()
        {
            return OwnedFleets.Where(HasColonizationOrder).ToList();
        }

        /// <summary>
        /// Returns a planet's system x position.
        /// </summary>
        /// <param name="planet">The planet to inspect.</param>
        /// <returns>The system x position.</returns>
        private int GetPlanetSystemPositionX(Planet planet)
        {
            return planet.GetParentOfType<PlanetSector>()?.PositionX ?? 0;
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
        /// Sums nonnegative requirements across target planets.
        /// </summary>
        /// <param name="planets">Planets to evaluate.</param>
        /// <param name="getRequirement">Requirement selector.</param>
        /// <returns>The bounded requirement total.</returns>
        private static int SumRequirements(
            IEnumerable<Planet> planets,
            Func<Planet, int> getRequirement
        )
        {
            long total = planets?.Sum(planet => (long)getRequirement(planet)) ?? 0;
            return total > int.MaxValue ? int.MaxValue : (int)total;
        }

        /// <summary>
        /// Returns whether a capital ship is ready for planning.
        /// </summary>
        /// <param name="capitalShip">The capital ship to inspect.</param>
        /// <returns>True if the capital ship is ready.</returns>
        private static bool IsReadyCapitalShip(CapitalShip capitalShip)
        {
            return capitalShip != null
                && capitalShip.ManufacturingStatus == ManufacturingStatus.Complete
                && capitalShip.Movement == null;
        }

        /// <summary>
        /// Returns projected combat value for a capital ship.
        /// </summary>
        /// <param name="capitalShip">Capital ship to inspect.</param>
        /// <returns>The projected combat value.</returns>
        public int GetProjectedCapitalShipCombatValue(CapitalShip capitalShip)
        {
            if (capitalShip == null)
                return 0;

            int starfighterCombat = capitalShip
                .GetChildren<Starfighter>()
                .Where(starfighter => starfighter != null)
                .Sum(GetProjectedStarfighterCombatValue);

            return capitalShip.GetProjectedCombatValue() + starfighterCombat;
        }

        /// <summary>
        /// Returns current ready combat value for a capital ship.
        /// </summary>
        /// <param name="capitalShip">Capital ship to inspect.</param>
        /// <returns>The ready combat value.</returns>
        public int GetReadyCapitalShipCombatValue(CapitalShip capitalShip)
        {
            if (!IsReadyCapitalShip(capitalShip))
                return 0;

            return capitalShip.GetCombatValue()
                + capitalShip
                    .GetChildren<Starfighter>()
                    .Where(starfighter => starfighter != null)
                    .Sum(starfighter => starfighter.GetCombatValue());
        }

        /// <summary>
        /// Returns projected combat value for a starfighter unit.
        /// </summary>
        /// <param name="starfighter">Starfighter to inspect.</param>
        /// <returns>The projected combat value.</returns>
        private static int GetProjectedStarfighterCombatValue(Starfighter starfighter)
        {
            int weaponStrength =
                starfighter.LaserCannon + starfighter.IonCannon + starfighter.Torpedoes;
            int squadronSize =
                starfighter.ManufacturingStatus == ManufacturingStatus.Complete
                    ? starfighter.CurrentSquadronSize
                    : starfighter.MaxSquadronSize;
            return starfighter.MaxSquadronSize > 0
                ? weaponStrength * Math.Max(0, squadronSize) / starfighter.MaxSquadronSize
                : weaponStrength;
        }

        /// <summary>
        /// Returns whether a regiment is ready for planning.
        /// </summary>
        /// <param name="regiment">The regiment to inspect.</param>
        /// <returns>True if the regiment is ready.</returns>
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

        /// <summary>
        /// Returns a cached value by composite key.
        /// </summary>
        /// <param name="cache">Cache to use.</param>
        /// <param name="key">Cache key.</param>
        /// <param name="createValue">Value factory used on cache miss.</param>
        /// <returns>The cached or created value.</returns>
        private static TValue GetOrAdd<TKey, TValue>(
            Dictionary<TKey, TValue> cache,
            TKey key,
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
