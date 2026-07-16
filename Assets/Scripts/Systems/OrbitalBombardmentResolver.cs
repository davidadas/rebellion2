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
    internal class OrbitalBombardmentResolver
    {
        private readonly GameRoot _game;
        private readonly IRandomNumberProvider _provider;
        private readonly MovementSystem _movement;
        private readonly PlanetaryControlSystem _ownership;

        public OrbitalBombardmentResolver(
            GameRoot game,
            IRandomNumberProvider provider,
            MovementSystem movement,
            PlanetaryControlSystem ownership
        )
        {
            _game = game;
            _provider = provider;
            _movement = movement;
            _ownership = ownership;
        }

        public BombardmentResult Execute(
            List<Fleet> attackingFleets,
            Planet targetPlanet,
            BombardmentType type
        )
        {
            BombardmentResult result = new BombardmentResult
            {
                Planet = targetPlanet,
                Type = type,
                Tick = _game.CurrentTick,
            };

            if (!CanBombard(attackingFleets, targetPlanet))
                return result;

            string attackerId = attackingFleets[0].GetOwnerInstanceID();
            string defenderId = targetPlanet.GetOwnerInstanceID();
            int initialDefenderRegimentCount = GetActiveDefenderRegiments(
                targetPlanet,
                defenderId
            ).Count;
            result.AttackingFaction = _game.GetFactionByOwnerInstanceID(attackerId);

            SetCombatState(attackingFleets, targetPlanet, true);
            try
            {
                bool destroysPlanet =
                    type == BombardmentType.DestroySystem
                    && HasPlanetDestroyingShip(attackingFleets);
                if (destroysPlanet)
                    DestroyPlanet(targetPlanet, result);

                ResolveDefenseFire(attackingFleets, targetPlanet, result);
                if (destroysPlanet)
                {
                    AddOwnershipChanges(
                        result,
                        ApplyDirectBombardmentPenalty(targetPlanet, result.AttackingFaction)
                    );
                    AddOwnershipChanges(
                        result,
                        ApplyDestroyedSystemPenalty(result.AttackingFaction)
                    );
                    return result;
                }

                if (!GetActiveCapitalShips(attackingFleets).Any())
                    return result;

                result.BombardmentStrength = CalculateBombardmentStrength(attackingFleets);
                result.ShieldStrength = CalculatePlanetShieldStrength(targetPlanet);
                result.StrikeAttempts = Math.Max(
                    0,
                    result.BombardmentStrength - result.ShieldStrength
                );

                bool civilianTargetsDestroyed = ResolveStrikes(
                    targetPlanet,
                    defenderId,
                    type,
                    result
                );

                AddOwnershipChanges(
                    result,
                    ReconcileControl(targetPlanet, defenderId, initialDefenderRegimentCount)
                );

                if (civilianTargetsDestroyed)
                {
                    AddOwnershipChanges(
                        result,
                        ApplyCivilianBombardmentPenalty(targetPlanet, result.AttackingFaction)
                    );
                }

                return result;
            }
            finally
            {
                SetCombatState(attackingFleets, targetPlanet, false);
            }
        }

        private static bool CanBombard(List<Fleet> fleets, Planet targetPlanet)
        {
            if (targetPlanet == null || fleets?.Any() != true || fleets.Any(fleet => fleet == null))
                return false;

            string ownerId = fleets[0].GetOwnerInstanceID();
            return !string.IsNullOrEmpty(ownerId)
                && fleets.All(fleet =>
                    fleet.GetOwnerInstanceID() == ownerId
                    && fleet.Movement == null
                    && fleet.GetParent() == targetPlanet
                );
        }

        private static void SetCombatState(List<Fleet> attackers, Planet planet, bool isInCombat)
        {
            foreach (Fleet fleet in attackers)
                fleet.IsInCombat = isInCombat;

            foreach (Fleet fleet in planet.Fleets)
                fleet.IsInCombat = isInCombat;
        }

        private int CalculateBombardmentStrength(List<Fleet> fleets)
        {
            int divisor = _game.Config.Combat.BombardmentAdmiralLeadershipDivisor;
            int total = 0;

            foreach (Fleet fleet in fleets)
            {
                int leadership = GetLeadership(
                    fleet.GetOfficers(),
                    OfficerRank.Admiral,
                    fleet.GetOwnerInstanceID()
                );
                int multiplier = leadership / divisor + 1;
                int fleetStrength = 0;

                foreach (CapitalShip ship in fleet.CapitalShips.Where(IsActive))
                {
                    fleetStrength += ScaleByCondition(
                        ship.Bombardment,
                        ship.CurrentHullStrength,
                        ship.MaxHullStrength
                    );
                    fleetStrength += ship
                        .Starfighters.Where(IsActive)
                        .Sum(fighter =>
                            ScaleByCondition(
                                fighter.Bombardment,
                                fighter.CurrentSquadronSize,
                                fighter.MaxSquadronSize
                            )
                        );
                }

                total += fleetStrength * multiplier;
            }

            return total;
        }

        private static int CalculatePlanetShieldStrength(Planet planet)
        {
            return planet
                .GetAllBuildings()
                .Where(building =>
                    IsActive(building)
                    && building.DefenseFacilityClass == DefenseFacilityClass.Shield
                )
                .Sum(building => building.ShieldStrength);
        }

        private void ResolveDefenseFire(
            List<Fleet> attackingFleets,
            Planet planet,
            BombardmentResult result
        )
        {
            List<CapitalShip> targets = GetActiveCapitalShips(attackingFleets);
            if (targets.Count == 0)
                return;

            int leadership = GetLeadership(
                planet.GetAllOfficers(),
                OfficerRank.General,
                planet.GetOwnerInstanceID()
            );
            int multiplier =
                leadership / _game.Config.Combat.BombardmentDefenseGeneralLeadershipDivisor + 1;
            Dictionary<CapitalShip, int> remainingShields = targets.ToDictionary(
                ship => ship,
                GetEffectiveShieldStrength
            );
            Dictionary<CapitalShip, int> hullDamage = targets.ToDictionary(ship => ship, _ => 0);

            IEnumerable<Building> facilities = GetActiveDefenseFacilities(
                    planet,
                    DefenseFacilityClass.KDY
                )
                .Concat(GetActiveDefenseFacilities(planet, DefenseFacilityClass.LNR));

            foreach (Building facility in facilities)
            {
                CapitalShip target = targets[_provider.NextInt(0, targets.Count)];
                int damage = facility.WeaponPower * multiplier;
                int absorbed = Math.Min(remainingShields[target], damage);
                remainingShields[target] -= absorbed;

                if (facility.DefenseFacilityClass == DefenseFacilityClass.LNR)
                    hullDamage[target] += damage - absorbed;
            }

            foreach (CapitalShip ship in targets)
            {
                int damage = hullDamage[ship];
                if (damage <= 0)
                    continue;

                int hullBefore = ship.CurrentHullStrength;
                ship.CurrentHullStrength = Math.Max(0, hullBefore - damage);
                result.AttackerShipDamage.Add(
                    new ShipDamageResult
                    {
                        Ship = ship,
                        HullBefore = hullBefore,
                        HullAfter = ship.CurrentHullStrength,
                    }
                );

                if (ship.CurrentHullStrength > 0)
                    continue;

                result.DestroyedCapitalShips.Add(ship);
                CombatSystem.DestroyCapitalShip(_game, _movement, ship);
            }
        }

        private bool ResolveStrikes(
            Planet planet,
            string defenderId,
            BombardmentType type,
            BombardmentResult result
        )
        {
            bool civilianTargetsDestroyed = false;

            if (type == BombardmentType.Military && result.StrikeAttempts > 0)
            {
                int militaryTargetCount = BuildTargets(planet, defenderId, type).Count;
                if (
                    _provider.NextInt(0, militaryTargetCount + 1) == 0
                    && TryStrikeCivilianTarget(planet, result)
                )
                {
                    civilianTargetsDestroyed = true;
                }
            }

            for (int attempt = 0; attempt < result.StrikeAttempts; attempt++)
            {
                List<BombardmentTarget> targets = BuildTargets(planet, defenderId, type);
                if (targets.Count == 0)
                    break;

                BombardmentTarget target = targets[_provider.NextInt(0, targets.Count)];
                if (!RollStrike(target.Resistance))
                    continue;

                ApplyStrike(planet, defenderId, target, result);
                civilianTargetsDestroyed |= target.IsCivilian;
            }

            return civilianTargetsDestroyed;
        }

        private bool TryStrikeCivilianTarget(Planet planet, BombardmentResult result)
        {
            List<BombardmentTarget> targets = BuildCivilianTargets(planet);
            if (targets.Count == 0)
                return false;

            BombardmentTarget target = targets[_provider.NextInt(0, targets.Count)];
            if (!RollStrike(target.Resistance))
                return false;

            ApplyStrike(planet, planet.GetOwnerInstanceID(), target, result);
            return true;
        }

        private List<BombardmentTarget> BuildTargets(
            Planet planet,
            string defenderId,
            BombardmentType type
        )
        {
            List<BombardmentTarget> targets = new List<BombardmentTarget>();
            if (
                type
                is BombardmentType.Military
                    or BombardmentType.General
                    or BombardmentType.DestroySystem
            )
            {
                targets.AddRange(BuildMilitaryTargets(planet, defenderId));
            }

            if (
                type
                is BombardmentType.Civilian
                    or BombardmentType.General
                    or BombardmentType.DestroySystem
            )
            {
                targets.AddRange(BuildCivilianTargets(planet));
            }

            if (type is BombardmentType.General or BombardmentType.DestroySystem)
                AddEnergyTargets(planet, targets);

            return targets;
        }

        private List<BombardmentTarget> BuildMilitaryTargets(Planet planet, string defenderId)
        {
            List<BombardmentTarget> targets = planet
                .GetAllRegiments()
                .Where(regiment =>
                    IsActive(regiment) && regiment.GetOwnerInstanceID() == defenderId
                )
                .Select(regiment => new BombardmentTarget
                {
                    Type = BombardmentTargetType.Regiment,
                    Entity = regiment,
                    Resistance = regiment.BombardmentDefense,
                })
                .ToList();

            targets.AddRange(
                planet
                    .GetAllBuildings()
                    .Where(building => IsActive(building) && IsDefenseFacility(building))
                    .Select(building => new BombardmentTarget
                    {
                        Type = BombardmentTargetType.Building,
                        Entity = building,
                        Resistance = building.Bombardment,
                    })
            );

            if (CanBombardHeadquarters(planet, defenderId))
            {
                targets.Add(
                    new BombardmentTarget
                    {
                        Type = BombardmentTargetType.Headquarters,
                        Resistance = _game.Config.Combat.BombardmentHeadquartersResistance,
                    }
                );
            }

            return targets;
        }

        private static List<BombardmentTarget> BuildCivilianTargets(Planet planet)
        {
            return planet
                .GetAllBuildings()
                .Where(building => IsActive(building) && IsCivilianBuilding(building))
                .Select(building => new BombardmentTarget
                {
                    Type = BombardmentTargetType.Building,
                    Entity = building,
                    Resistance = building.Bombardment,
                    IsCivilian = true,
                })
                .ToList();
        }

        private void AddEnergyTargets(Planet planet, List<BombardmentTarget> targets)
        {
            if (planet.EnergyCapacity > 0)
            {
                targets.Add(
                    new BombardmentTarget
                    {
                        Type = BombardmentTargetType.EnergyCapacity,
                        Resistance = _game.Config.Combat.BombardmentEnergyResistance,
                    }
                );
            }

            if (planet.AllocatedEnergy > 0)
            {
                targets.Add(
                    new BombardmentTarget
                    {
                        Type = BombardmentTargetType.AllocatedEnergy,
                        Resistance = _game.Config.Combat.BombardmentAllocatedEnergyResistance,
                    }
                );
            }
        }

        private bool RollStrike(int resistance)
        {
            GameConfig.CombatConfig config = _game.Config.Combat;
            int roll = _provider.NextInt(
                config.BombardmentStrikeRollMinimum,
                config.BombardmentStrikeRollMaximum + 1
            );
            return resistance < roll;
        }

        private void ApplyStrike(
            Planet planet,
            string defenderId,
            BombardmentTarget target,
            BombardmentResult result
        )
        {
            switch (target.Type)
            {
                case BombardmentTargetType.Regiment:
                    Regiment regiment = (Regiment)target.Entity;
                    result.DestroyedRegiments.Add(regiment);
                    _game.DetachNode(regiment);
                    break;
                case BombardmentTargetType.Building:
                    Building building = (Building)target.Entity;
                    result.DestroyedBuildings.Add(building);
                    _game.DetachNode(building);
                    break;
                case BombardmentTargetType.Headquarters:
                    DestroyHeadquarters(planet, defenderId, result);
                    break;
                case BombardmentTargetType.EnergyCapacity:
                    planet.EnergyCapacity--;
                    result.EnergyCapacityDamage++;
                    break;
                case BombardmentTargetType.AllocatedEnergy:
                    planet.AllocatedEnergy--;
                    result.AllocatedEnergyDamage++;
                    break;
            }

            result.SuccessfulStrikes++;
            result.Strikes.Add(
                new BombardmentStrikeEvent
                {
                    TargetType = target.Type,
                    Target = target.Entity,
                    TargetName = GetTargetName(target),
                }
            );
        }

        private void DestroyHeadquarters(Planet planet, string defenderId, BombardmentResult result)
        {
            planet.IsHeadquarters = false;
            if (!string.IsNullOrEmpty(defenderId))
                _game.GetFactionByOwnerInstanceID(defenderId).HQInstanceID = null;
            result.HeadquartersDestroyed = true;
        }

        private static string GetTargetName(BombardmentTarget target)
        {
            return target.Type switch
            {
                BombardmentTargetType.Headquarters => "Headquarters",
                BombardmentTargetType.EnergyCapacity => "Energy Capacity",
                BombardmentTargetType.AllocatedEnergy => "Allocated Energy",
                _ => target.Entity?.GetDisplayName(),
            };
        }

        private bool CanBombardHeadquarters(Planet planet, string defenderId)
        {
            return planet.IsHeadquarters
                && !string.IsNullOrEmpty(defenderId)
                && _game
                    .GetFactionByOwnerInstanceID(defenderId)
                    .Settings.HeadquartersCanBeBombarded;
        }

        private bool HasPlanetDestroyingShip(List<Fleet> fleets)
        {
            HashSet<string> typeIds =
                _game.Config.Combat.PlanetDestroyingCapitalShipTypeIDs.ToHashSet();
            return GetActiveCapitalShips(fleets).Any(ship => typeIds.Contains(ship.GetTypeID()));
        }

        private void DestroyPlanet(Planet planet, BombardmentResult result)
        {
            planet.IsDestroyed = true;
            result.PlanetDestroyed = true;

            foreach (
                Officer officer in planet
                    .GetAllOfficers()
                    .Where(officer => !officer.IsMain && !officer.IsKilled)
                    .ToList()
            )
            {
                if (!RollPercent(_game.Config.Combat.DestroySystemPersonnelInjuryPercent))
                    continue;

                officer.ApplyInjury(1, _game.Config.Recovery.MaxInjuryPoints);
                result.Events.Add(
                    new OfficerInjuredResult
                    {
                        Officer = officer,
                        Severity = 1,
                        Tick = _game.CurrentTick,
                    }
                );

                if (!RollPercent(_game.Config.Combat.DestroySystemMinorPersonnelDeathPercent))
                    continue;

                officer.IsKilled = true;
                result.Events.Add(
                    new OfficerKilledResult
                    {
                        TargetOfficer = officer,
                        Context = planet,
                        Tick = _game.CurrentTick,
                    }
                );
                _game.DetachNode(officer);
            }
        }

        private List<PlanetOwnershipChangedResult> ApplyCivilianBombardmentPenalty(
            Planet planet,
            Faction attacker
        )
        {
            List<PlanetOwnershipChangedResult> results = ApplyDirectBombardmentPenalty(
                planet,
                attacker
            );

            PlanetSystem system = planet.GetParentOfType<PlanetSystem>();
            if (system == null)
                return results;

            int shift = GetCivilianSystemPenalty(system, attacker);
            results.AddRange(
                _ownership.ShiftBombardmentSupport(GetAffectedPlanets(system), attacker, shift)
            );
            return results;
        }

        private List<PlanetOwnershipChangedResult> ApplyDirectBombardmentPenalty(
            Planet planet,
            Faction attacker
        )
        {
            int shift = _game.Config.Combat.CivilianBombardmentSupportPenalty;
            if (planet.GetParentOfType<PlanetSystem>()?.SystemType == PlanetSystemType.CoreSystem)
            {
                bool weaken = attacker.Settings.WeakSupportPenaltyTrigger switch
                {
                    SupportShiftCondition.Positive => shift > 0,
                    SupportShiftCondition.Negative => shift < 0,
                    _ => false,
                };
                if (weaken)
                    shift /= _game.Config.SupportShift.WeakSupportPenaltyDivisor;
            }

            return _ownership.ShiftBombardmentSupport(new[] { planet }, attacker, shift);
        }

        private int GetCivilianSystemPenalty(PlanetSystem system, Faction attacker)
        {
            bool empire = attacker.Settings.InvertSupportShift;
            if (system.SystemType == PlanetSystemType.CoreSystem)
            {
                return empire
                    ? _game.Config.Combat.CivilianBombardmentCoreEmpireSupportPenalty
                    : _game.Config.Combat.CivilianBombardmentCoreAllianceSupportPenalty;
            }

            return empire
                ? _game.Config.Combat.CivilianBombardmentOuterRimEmpireSupportPenalty
                : _game.Config.Combat.CivilianBombardmentOuterRimAllianceSupportPenalty;
        }

        private List<PlanetOwnershipChangedResult> ApplyDestroyedSystemPenalty(Faction attacker)
        {
            List<PlanetOwnershipChangedResult> results = new List<PlanetOwnershipChangedResult>();
            List<Planet> corePlanets = _game
                .GetSceneNodesByType<PlanetSystem>()
                .Where(system => system.SystemType == PlanetSystemType.CoreSystem)
                .SelectMany(GetAffectedPlanets)
                .ToList();
            results.AddRange(
                _ownership.ShiftBombardmentSupport(
                    corePlanets,
                    attacker,
                    _game.Config.Combat.DestroySystemCoreSupportPenalty
                )
            );

            List<Planet> outerRimPlanets = _game
                .GetSceneNodesByType<PlanetSystem>()
                .Where(system => system.SystemType == PlanetSystemType.OuterRim)
                .SelectMany(GetAffectedPlanets)
                .Where(planet =>
                    planet.GetPopularSupport(attacker.InstanceID)
                    < _game.Config.Combat.DestroySystemOuterRimSupportThreshold
                )
                .ToList();
            results.AddRange(
                _ownership.ShiftBombardmentSupport(
                    outerRimPlanets,
                    attacker,
                    _game.Config.Combat.DestroySystemOuterRimSupportPenalty
                )
            );
            return results;
        }

        private List<PlanetOwnershipChangedResult> ReconcileControl(
            Planet planet,
            string previousOwnerId,
            int initialDefenderRegimentCount
        )
        {
            if (
                string.IsNullOrEmpty(previousOwnerId)
                || initialDefenderRegimentCount == 0
                || GetActiveDefenderRegiments(planet, previousOwnerId).Count > 0
            )
                return new List<PlanetOwnershipChangedResult>();

            return _ownership.ResolveBombardmentControl(planet, previousOwnerId);
        }

        private static void AddOwnershipChanges(
            BombardmentResult result,
            IEnumerable<PlanetOwnershipChangedResult> changes
        )
        {
            foreach (PlanetOwnershipChangedResult change in changes)
            {
                if (change?.Planet == null)
                    continue;

                if (change.Planet == result.Planet)
                {
                    result.OwnershipChange = MergeOwnershipChanges(result.OwnershipChange, change);
                    continue;
                }

                PlanetOwnershipChangedResult existing = result
                    .Events.OfType<PlanetOwnershipChangedResult>()
                    .FirstOrDefault(candidate => candidate.Planet == change.Planet);
                if (existing == null)
                    result.Events.Add(change);
                else
                {
                    PlanetOwnershipChangedResult merged = MergeOwnershipChanges(existing, change);
                    if (merged == null)
                        result.Events.Remove(existing);
                }
            }
        }

        private static PlanetOwnershipChangedResult MergeOwnershipChanges(
            PlanetOwnershipChangedResult first,
            PlanetOwnershipChangedResult second
        )
        {
            if (first == null)
                return second;
            if (second != null)
                first.NewOwner = second.NewOwner;
            if (first.PreviousOwner?.InstanceID == first.NewOwner?.InstanceID)
                return null;
            return first;
        }

        private bool RollPercent(int chance)
        {
            return _provider.NextInt(0, 100) < chance;
        }

        private static List<Planet> GetAffectedPlanets(PlanetSystem system)
        {
            return system
                .Planets.Where(planet => planet.IsPopulated() && !planet.IsDestroyed)
                .ToList();
        }

        private static List<CapitalShip> GetActiveCapitalShips(IEnumerable<Fleet> fleets)
        {
            return fleets.SelectMany(fleet => fleet.CapitalShips).Where(IsActive).ToList();
        }

        private static IEnumerable<Building> GetActiveDefenseFacilities(
            Planet planet,
            DefenseFacilityClass defenseClass
        )
        {
            return planet
                .GetAllBuildings()
                .Where(building =>
                    IsActive(building) && building.DefenseFacilityClass == defenseClass
                );
        }

        private static List<Regiment> GetActiveDefenderRegiments(Planet planet, string defenderId)
        {
            return planet
                .GetAllRegiments()
                .Where(regiment =>
                    IsActive(regiment) && regiment.GetOwnerInstanceID() == defenderId
                )
                .ToList();
        }

        private static int GetLeadership(
            IEnumerable<Officer> officers,
            OfficerRank rank,
            string ownerId
        )
        {
            Officer commander = officers.FirstOrDefault(officer =>
                officer.CurrentRank == rank
                && officer.GetOwnerInstanceID() == ownerId
                && !officer.IsKilled
            );
            return commander?.GetEffectiveRating(OfficerRating.Leadership) ?? 0;
        }

        private static int GetEffectiveShieldStrength(CapitalShip ship)
        {
            return ScaleByCondition(
                ship.MaxShieldStrength,
                ship.CurrentHullStrength,
                ship.MaxHullStrength
            );
        }

        private static int ScaleByCondition(int value, int current, int maximum)
        {
            return maximum > 0 ? value * Math.Max(0, current) / maximum : value;
        }

        private static bool IsActive(IManufacturable unit)
        {
            return unit.ManufacturingStatus == ManufacturingStatus.Complete
                && unit.Movement == null;
        }

        private static bool IsActive(CapitalShip ship)
        {
            return IsActive((IManufacturable)ship) && ship.CurrentHullStrength > 0;
        }

        private static bool IsActive(Starfighter fighter)
        {
            return IsActive((IManufacturable)fighter) && fighter.CurrentSquadronSize > 0;
        }

        private static bool IsDefenseFacility(Building building)
        {
            return building.DefenseFacilityClass
                is DefenseFacilityClass.KDY
                    or DefenseFacilityClass.LNR
                    or DefenseFacilityClass.Shield
                    or DefenseFacilityClass.DeathStarShield;
        }

        private static bool IsCivilianBuilding(Building building)
        {
            return building.BuildingType
                is BuildingType.Mine
                    or BuildingType.Refinery
                    or BuildingType.Shipyard
                    or BuildingType.TrainingFacility
                    or BuildingType.ConstructionFacility;
        }

        private class BombardmentTarget
        {
            public BombardmentTargetType Type;
            public IGameEntity Entity;
            public int Resistance;
            public bool IsCivilian;
        }
    }
}
