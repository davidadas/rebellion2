using System;
using System.Collections.Generic;
using System.Linq;
using Rebellion.Game;
using Rebellion.Game.FogOfWar;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Missions;
using Rebellion.Game.Units;
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
        private readonly Dictionary<string, int> _planetRequiredAttackCombatStrengths =
            new Dictionary<string, int>(StringComparer.Ordinal);
        private readonly Dictionary<string, int> _planetRequiredAttackRegimentCounts =
            new Dictionary<string, int>(StringComparer.Ordinal);
        private readonly Dictionary<string, int> _strongestHostileFleetStrengths = new Dictionary<
            string,
            int
        >(StringComparer.Ordinal);
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

        /// <summary>
        /// Creates an AI assessment for a turn context.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        public AIAssessment(AITurnContext context)
        {
            _context = context;
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
            OwnedPlanets = BuildOwnedPlanets();
            EnemyPlanets = BuildEnemyPlanets();
            NeutralPlanets = BuildNeutralPlanets();
            AvailableMissionParticipants = BuildAvailableMissionParticipants();
            TargetableEnemyOfficerMissionTargets = BuildTargetableEnemyOfficerMissionTargets();
            OwnedFleets = BuildOwnedFleets();
            AttackOrderedFleets = BuildAttackOrderedFleets();
            ColonizationOrderedFleets = BuildColonizationOrderedFleets();
        }

        public IReadOnlyList<Planet> KnownColonizedPlanets { get; }

        public IReadOnlyList<Planet> KnownUncolonizedPlanets { get; }

        public IReadOnlyList<Planet> FactionViewPlanets { get; }

        public IReadOnlyList<Planet> UnexploredPlanets { get; }

        public IReadOnlyList<Planet> OwnedPlanets { get; }

        public IReadOnlyList<Planet> EnemyPlanets { get; }

        public IReadOnlyList<Planet> NeutralPlanets { get; }

        public IReadOnlyList<IMissionParticipant> AvailableMissionParticipants { get; }

        internal IReadOnlyList<Mission> ActiveMissions { get; }

        public IReadOnlyList<(
            Planet Planet,
            Officer TargetOfficer
        )> TargetableEnemyOfficerMissionTargets { get; }

        public IReadOnlyList<Fleet> OwnedFleets { get; }

        public IReadOnlyList<Fleet> AttackOrderedFleets { get; }

        public IReadOnlyList<Fleet> ColonizationOrderedFleets { get; }

        public bool IsFactionHeadquarters(Planet planet)
        {
            return planet != null
                && _context?.Faction != null
                && planet.InstanceID == _context.Faction.HQInstanceID
                && planet.GetOwnerInstanceID() == _context.Faction.InstanceID;
        }

        public Planet GetKnownPlanet(string instanceId)
        {
            return
                !string.IsNullOrEmpty(instanceId)
                && _knownPlanets.TryGetValue(instanceId, out Planet planet)
                ? planet
                : null;
        }

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
                !_context.Faction.Fog.PlanetToSystem.TryGetValue(
                    planet.InstanceID,
                    out string systemId
                )
                || !_context.Faction.Fog.Snapshots.TryGetValue(
                    systemId,
                    out SystemSnapshot systemSnapshot
                )
                || !systemSnapshot.Planets.TryGetValue(
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
        /// Returns the highest enemy planet value.
        /// </summary>
        /// <returns>The highest enemy planet value.</returns>
        public double GetHighestEnemyPlanetValue()
        {
            return EnemyPlanets.Select(GetPlanetValue).DefaultIfEmpty().Max();
        }

        public string GetPlanetSystemId(Planet planet)
        {
            return planet?.GetParentOfType<PlanetSystem>()?.InstanceID ?? string.Empty;
        }

        public IReadOnlyList<Planet> GetAttackCampaignPlanets(string systemId)
        {
            if (string.IsNullOrEmpty(systemId))
                return Array.Empty<Planet>();

            return EnemyPlanets
                .Where(planet => GetPlanetSystemId(planet) == systemId)
                .OrderBy(planet => planet.InstanceID, StringComparer.Ordinal)
                .ToList();
        }

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

        public int GetEnemyPlanetCountInSystem(string systemId)
        {
            return GetAttackCampaignPlanets(systemId).Count;
        }

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

        public IEnumerable<Officer> GetKnownOfficers(Planet planet)
        {
            return planet?.GetChildren<Officer>(_ => true) ?? Enumerable.Empty<Officer>();
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
                        .GetFleets()
                        .Where(fleet => fleet.GetOwnerInstanceID() == _context?.Faction?.InstanceID)
                        .OrderBy(fleet => fleet.InstanceID)
                        .ToList()
            );
        }

        public bool HasCommittedHeadquartersFleet(Planet planet)
        {
            if (!IsFactionHeadquarters(planet))
                return false;

            return GetFriendlyFleets(planet)
                .Any(fleet =>
                    fleet.CapitalShips.Any(capitalShip =>
                        capitalShip.ManufacturingStatus
                            is ManufacturingStatus.Complete
                                or ManufacturingStatus.Building
                    )
                );
        }

        public int GetCommittedHeadquartersDefenseStrength(Planet planet)
        {
            if (!IsFactionHeadquarters(planet))
                return 0;

            return GetFriendlyFleets(planet).Select(GetFleetCombatValue).DefaultIfEmpty().Max();
        }

        public int GetRequiredHeadquartersDefenseStrength(Planet planet)
        {
            if (!IsFactionHeadquarters(planet) || _context?.Game?.Config == null)
                return 0;

            GameConfig.AIFleetDeploymentConfig config = _context.Game.Config.AI.FleetDeployment;
            int hostileFleetRequirement = ScaleByPercent(
                GetHeadquartersThreatStrength(planet),
                config.AttackStrengthPercentOfStrongestHostileFleet
            );
            return Math.Max(config.MinimumDefenseStrength, hostileFleetRequirement);
        }

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

        public bool CanFleetDepartHeadquarters(Fleet fleet)
        {
            Planet planet = GetFleetPlanet(fleet);
            if (!IsFactionHeadquarters(planet))
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
                    int fleetDefenseRequirement = GetRequiredOrbitalStrength(planet);
                    return Math.Max(config.MinimumAttackStrength, fleetDefenseRequirement);
                }
            );
        }

        public int GetRequiredAttackCampaignCombatStrength(string systemId)
        {
            return GetRequiredAttackCampaignCombatStrength(GetAttackCampaignPlanets(systemId));
        }

        public int GetRequiredAttackCampaignCombatStrength(Planet targetPlanet)
        {
            return GetRequiredAttackCampaignCombatStrength(GetAttackCampaignPlanets(targetPlanet));
        }

        private int GetRequiredAttackCampaignCombatStrength(
            IReadOnlyCollection<Planet> targetPlanets
        )
        {
            if (targetPlanets == null || targetPlanets.Count == 0 || _context?.Game?.Config == null)
                return 0;

            return Math.Max(
                _context.Game.Config.AI.FleetDeployment.MinimumAttackStrength,
                SumRequirements(targetPlanets, GetRequiredOrbitalStrength)
            );
        }

        public int GetRequiredAttackCampaignRegimentCount(Planet targetPlanet)
        {
            return SumRequirements(
                GetAttackCampaignPlanets(targetPlanet),
                GetRequiredAttackRegimentCount
            );
        }

        public int GetRequiredAttackCampaignRegimentCount(string systemId)
        {
            return SumRequirements(
                GetAttackCampaignPlanets(systemId),
                GetRequiredAttackRegimentCount
            );
        }

        public int GetRequiredAttackCampaignRegimentStrength(Planet targetPlanet)
        {
            return SumRequirements(
                GetAttackCampaignPlanets(targetPlanet),
                GetRequiredAttackRegimentStrength
            );
        }

        public int GetRequiredAttackCampaignBombardmentStrength(Planet targetPlanet)
        {
            return GetAttackCampaignPlanets(targetPlanet)
                .Select(GetRequiredBombardmentStrength)
                .DefaultIfEmpty()
                .Max();
        }

        public int GetRequiredAttackCampaignBombardmentStrength(string systemId)
        {
            return GetAttackCampaignPlanets(systemId)
                .Select(GetRequiredBombardmentStrength)
                .DefaultIfEmpty()
                .Max();
        }

        public int GetRequiredOrbitalStrength(Planet planet)
        {
            if (planet == null || _context?.Game?.Config == null)
                return 0;

            int hostileStrength = GetStrongestHostileFleetStrength(planet);
            return hostileStrength > 0
                ? ScaleByPercent(
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

        public int GetRequiredPlanetDefenseStrength(Planet planet)
        {
            if (!IsOwnedPlanet(planet) || _context?.Game?.Config == null)
                return 0;

            int hostileStrength = GetPlanetDefenseThreatStrength(planet);
            return hostileStrength > 0
                ? ScaleByPercent(
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

        public int GetRequiredDefenseStrength(Planet planet)
        {
            return IsFactionHeadquarters(planet)
                ? GetRequiredHeadquartersDefenseStrength(planet)
                : GetRequiredPlanetDefenseStrength(planet);
        }

        public bool CanDefendPlanet(Fleet fleet, Planet planet)
        {
            int requiredStrength = GetRequiredPlanetDefenseStrength(planet);
            return requiredStrength > 0
                && fleet?.HasOperationalCapitalShips() == true
                && GetReadyFleetCombatValue(fleet) >= requiredStrength;
        }

        public int GetPlanetaryDefenseEnergyDeficit(Planet planet)
        {
            if (!IsOwnedPlanet(planet) || _context?.Game?.Config == null)
                return 0;

            int shieldCount = planet
                .GetAllBuildings()
                .Count(building =>
                    building.GetOwnerInstanceID() == _context.Faction.InstanceID
                    && building.DefenseFacilityClass == DefenseFacilityClass.Shield
                );
            int weaponCount = planet
                .GetAllBuildings()
                .Count(building =>
                    building.GetOwnerInstanceID() == _context.Faction.InstanceID
                    && building.GetBuildingType() == BuildingType.Weapon
                );
            int shieldDeficit = System.Math.Max(
                0,
                _context.Game.Config.Combat.PlanetaryAssault.ShieldGeneratorLimit - shieldCount
            );
            int weaponDeficit = System.Math.Max(
                0,
                _context.Game.Config.AI.Infrastructure.PlanetaryWeaponTargetCount - weaponCount
            );
            return shieldDeficit + weaponDeficit;
        }

        public bool CanWinOrbitalCombat(Fleet fleet, Planet planet)
        {
            int requiredStrength = GetRequiredOrbitalStrength(planet);
            return requiredStrength > 0
                && fleet?.HasOperationalCapitalShips() == true
                && GetReadyFleetCombatValue(fleet) >= requiredStrength;
        }

        public int GetRequiredAttackRegimentStrength(Planet planet)
        {
            if (planet == null || _context?.Game?.Config == null)
                return 0;

            return ScaleByPercent(
                GetDefendingRegimentDefenseStrength(planet),
                _context.Game.Config.AI.FleetDeployment.AttackStrengthPercentOfDefense
            );
        }

        public int GetRequiredBombardmentStrength(Planet planet)
        {
            if (!IsAssaultBlockedByShields(planet))
                return 0;

            return BombardmentSystem.GetBombardmentShieldStrength(planet) + 1;
        }

        public bool IsAssaultBlockedByShields(Planet planet)
        {
            if (planet == null || _context?.Game?.Config == null)
                return false;

            return PlanetaryAssaultSystem.IsBlockedByShields(
                planet,
                _context.Game.Config.Combat.PlanetaryAssault.ShieldGeneratorLimit
            );
        }

        public bool IsAttackTargetBlockedByShields(Planet planet)
        {
            return IsAssaultBlockedByShields(planet)
                && AttackOrderedFleets.Any(fleet =>
                    fleet.Order.TargetPlanetId == planet.InstanceID
                );
        }

        public bool IsAssaultBlockingShield(Planet planet, IManufacturable target)
        {
            return IsAttackTargetBlockedByShields(planet)
                && target
                    is Building
                    {
                        DefenseFacilityClass: DefenseFacilityClass.Shield,
                        ManufacturingStatus: ManufacturingStatus.Complete,
                        Movement: null,
                    } shield
                && shield.GetParentOfType<Planet>()?.InstanceID == planet.InstanceID;
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
                    int minimum = _context
                        .Game
                        .Config
                        .AI
                        .FleetDeployment
                        .MinimumPlanetaryAssaultRegimentCount;
                    int stableGarrison = UprisingSystem.CalculateGarrisonRequirement(
                        planet,
                        _context.Faction,
                        _context.Game.Config.AI.Garrison
                    );
                    return Math.Max(minimum, GetDefendingRegimentCount(planet) + stableGarrison);
                }
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

            Planet targetPlanet = _context?.Game?.GetSceneNodeByInstanceID<Planet>(targetPlanetId);
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
            int requiredRegiments = GetRequiredAttackRegimentCount(targetPlanet);
            int requiredRegimentStrength = GetRequiredAttackRegimentStrength(targetPlanet);
            int requiredBombardment = GetRequiredBombardmentStrength(targetPlanet);
            return fleet?.HasOperationalCapitalShips() == true
                && GetReadyFleetCombatValue(fleet) >= requiredCombat
                && GetReadyFleetRegimentCount(fleet) >= requiredRegiments
                && GetReadyFleetRegimentCapacity(fleet) >= requiredRegiments
                && GetReadyFleetRegimentAttackStrength(fleet) >= requiredRegimentStrength
                && GetFleetBombardmentStrength(fleet) >= requiredBombardment;
        }

        public bool IsFleetProjectedReadyToAttack(Fleet fleet, Planet targetPlanet)
        {
            int requiredCombat = GetRequiredAttackCombatStrength(targetPlanet);
            int requiredRegiments = GetRequiredAttackRegimentCount(targetPlanet);
            int requiredRegimentStrength = GetRequiredAttackRegimentStrength(targetPlanet);
            int requiredBombardment = GetRequiredBombardmentStrength(targetPlanet);
            return fleet?.CapitalShips.Any(capitalShip => capitalShip != null) == true
                && GetProjectedFleetCombatValue(fleet) >= requiredCombat
                && GetFleetLoadedRegimentCount(fleet) >= requiredRegiments
                && GetFleetRegimentCapacity(fleet) >= requiredRegiments
                && GetProjectedFleetRegimentAttackStrength(fleet) >= requiredRegimentStrength
                && GetProjectedFleetBombardmentStrength(fleet) >= requiredBombardment;
        }

        public bool IsFleetProjectedReadyToAttackCampaign(Fleet fleet, Planet targetPlanet)
        {
            int requiredCombat = GetRequiredAttackCampaignCombatStrength(targetPlanet);
            int requiredRegiments = GetRequiredAttackCampaignRegimentCount(targetPlanet);
            int requiredRegimentStrength = GetRequiredAttackCampaignRegimentStrength(targetPlanet);
            int requiredBombardment = GetRequiredAttackCampaignBombardmentStrength(targetPlanet);
            return fleet?.CapitalShips.Any(capitalShip => capitalShip != null) == true
                && GetProjectedFleetCombatValue(fleet) >= requiredCombat
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
            int requiredRegiments = GetRequiredAttackRegimentCount(targetPlanet);
            int requiredRegimentStrength = GetRequiredAttackRegimentStrength(targetPlanet);
            int requiredBombardment = GetRequiredBombardmentStrength(targetPlanet);
            int gateCount = 0;

            if (fleet?.HasOperationalCapitalShips() == true)
                gateCount++;

            if (GetReadyFleetCombatValue(fleet) >= requiredCombat)
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

        public int GetFleetAttackCampaignReadinessGateCount(Fleet fleet, Planet targetPlanet)
        {
            int requiredCombat = GetRequiredAttackCampaignCombatStrength(targetPlanet);
            int requiredRegiments = GetRequiredAttackCampaignRegimentCount(targetPlanet);
            int requiredRegimentStrength = GetRequiredAttackCampaignRegimentStrength(targetPlanet);
            int requiredBombardment = GetRequiredAttackCampaignBombardmentStrength(targetPlanet);
            int gateCount = 0;

            if (fleet?.HasOperationalCapitalShips() == true)
                gateCount++;

            if (GetReadyFleetCombatValue(fleet) >= requiredCombat)
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

        public int GetProjectedFleetCombatValue(Fleet fleet)
        {
            if (fleet == null)
                return 0;

            return fleet.CapitalShips.Sum(GetProjectedCapitalShipCombatValue);
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
                .CapitalShips.Where(IsReadyCapitalShip)
                .SelectMany(ship => ship.Regiments)
                .Where(IsReadyRegiment)
                .Sum(regiment => regiment.AttackRating + leadershipBonus);
        }

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
                .Regiments.Where(IsReadyRegiment)
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
            if (_context?.FogOfWar == null || _context.Faction == null)
                return new List<Planet>();

            return _context.FogOfWar.GetFactionKnowledgePlanets(_context.Faction);
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
        /// Builds the attack-ordered fleet list.
        /// </summary>
        /// <returns>Attack-ordered fleets.</returns>
        private List<Fleet> BuildAttackOrderedFleets()
        {
            return OwnedFleets.Where(HasAttackOrder).ToList();
        }

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
            return planet.GetParentOfType<PlanetSystem>()?.PositionX ?? 0;
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
        /// Scales an integer by a percent value.
        /// </summary>
        /// <param name="value">Value to scale.</param>
        /// <param name="percent">Percent to apply.</param>
        /// <returns>The scaled value.</returns>
        private int ScaleByPercent(int value, int percent)
        {
            return value * percent / 100;
        }

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

        public int GetProjectedCapitalShipCombatValue(CapitalShip capitalShip)
        {
            if (capitalShip == null)
                return 0;

            int starfighterCombat = capitalShip
                .Starfighters.Where(starfighter => starfighter != null)
                .Sum(GetProjectedStarfighterCombatValue);

            return capitalShip.GetProjectedCombatValue() + starfighterCombat;
        }

        public int GetReadyCapitalShipCombatValue(CapitalShip capitalShip)
        {
            if (!IsReadyCapitalShip(capitalShip))
                return 0;

            return capitalShip.GetCombatValue()
                + capitalShip
                    .Starfighters.Where(starfighter => starfighter != null)
                    .Sum(starfighter => starfighter.GetCombatValue());
        }

        private static int GetProjectedStarfighterCombatValue(Starfighter starfighter)
        {
            int weaponStrength =
                starfighter.LaserCannon + starfighter.IonCannon + starfighter.Torpedoes;
            int squadronSize =
                starfighter.ManufacturingStatus == ManufacturingStatus.Complete
                    ? starfighter.CurrentSquadronSize
                    : starfighter.MaxSquadronSize;
            return weaponStrength * Math.Max(0, squadronSize);
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
    }
}
