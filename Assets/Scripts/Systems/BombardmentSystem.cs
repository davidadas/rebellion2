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
    public enum BombardmentType
    {
        Military,
        Civilian,
        General,
        DestroySystem,
    }

    public enum BombardmentTargetType
    {
        Regiment,
        Building,
        Headquarters,
        EnergyCapacity,
        AllocatedEnergy,
    }

    /// <summary>
    /// Resolves orbital bombardment against planets.
    /// </summary>
    public class BombardmentSystem
    {
        private readonly GameRoot _game;
        private readonly IRandomNumberProvider _provider;
        private readonly MovementSystem _movement;
        private readonly PlanetaryControlSystem _ownership;

        /// <summary>
        /// Creates the bombardment system.
        /// </summary>
        /// <param name="game">Active game state.</param>
        /// <param name="provider">Random-number provider used by bombardment resolution.</param>
        /// <param name="movement">Movement system used for surviving passenger evacuation.</param>
        /// <param name="ownership">Planetary control system used for support and ownership changes.</param>
        public BombardmentSystem(
            GameRoot game,
            IRandomNumberProvider provider,
            MovementSystem movement,
            PlanetaryControlSystem ownership
        )
        {
            _game = game;
            _provider = provider;
            _movement = movement ?? throw new ArgumentNullException(nameof(movement));
            _ownership = ownership ?? throw new ArgumentNullException(nameof(ownership));
        }

        /// <summary>
        /// Runs the 6-stage orbital bombardment pipeline against a target planet.
        /// </summary>
        /// <param name="attackingFleets">Fleets performing the bombardment (all must share a faction).</param>
        /// <param name="targetPlanet">Planet being bombarded.</param>
        /// <param name="type">Targets and consequences selected for the bombardment.</param>
        /// <returns>Bombardment outcome, including strikes and any ship/regiment/building destruction.</returns>
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

            if (!CanExecute(attackingFleets, targetPlanet, type))
                return result;

            string attackerId = attackingFleets[0].GetOwnerInstanceID();
            string defenderId = targetPlanet.GetOwnerInstanceID();
            int initialDefenderRegimentCount = GetActiveDefenderRegiments(
                targetPlanet,
                defenderId
            ).Count;
            result.AttackingFaction = _game.GetFactionByOwnerInstanceID(attackerId);
            result.AttackerOwnerInstanceID = attackerId;
            result.DefenderOwnerInstanceID = defenderId;
            result.AttackingUnits.AddRange(SnapshotFleetUnits(attackingFleets));
            result.DefendingUnits.AddRange(SnapshotPlanetUnits(targetPlanet, defenderId));

            SetBombardmentCombatState(attackingFleets, targetPlanet, true);
            try
            {
                bool destroysPlanet =
                    type == BombardmentType.DestroySystem
                    && HasPlanetDestroyingShip(attackingFleets);
                if (destroysPlanet)
                    DestroyPlanet(targetPlanet, result);

                ResolveBombardmentDefenseFire(attackingFleets, targetPlanet, result);
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
                SetBombardmentCombatState(attackingFleets, targetPlanet, false);
            }
        }

        /// <summary>
        /// Determines whether the supplied fleets can bombard the target planet.
        /// </summary>
        /// <param name="fleets">Fleets attempting the bombardment.</param>
        /// <param name="targetPlanet">Planet being targeted.</param>
        /// <param name="type">Bombardment mode being requested.</param>
        /// <returns>True when every fleet is stationary, colocated, and owned by one faction.</returns>
        public bool CanExecute(
            IReadOnlyList<Fleet> fleets,
            Planet targetPlanet,
            BombardmentType type
        )
        {
            if (!CanBombard(fleets, targetPlanet))
                return false;

            return type == BombardmentType.DestroySystem
                ? HasPlanetDestroyingShip(fleets)
                : CalculateBombardmentStrength(fleets) > 0;
        }

        /// <summary>
        /// Determines whether fleets satisfy the shared bombardment restrictions.
        /// </summary>
        /// <param name="fleets">Fleets attempting the bombardment.</param>
        /// <param name="targetPlanet">Planet being targeted.</param>
        /// <returns>True when the fleets can perform an ordinary bombardment.</returns>
        private static bool CanBombard(IReadOnlyList<Fleet> fleets, Planet targetPlanet)
        {
            if (
                targetPlanet?.IsDestroyed != false
                || fleets?.Any() != true
                || fleets.Any(fleet => fleet == null)
            )
                return false;

            string ownerId = fleets[0].GetOwnerInstanceID();
            return !string.IsNullOrEmpty(ownerId)
                && targetPlanet?.GetOwnerInstanceID() != ownerId
                && fleets.All(fleet =>
                    fleet.GetOwnerInstanceID() == ownerId
                    && fleet.Movement == null
                    && !fleet.IsInCombat
                    && fleet.GetParent() == targetPlanet
                )
                && GetActiveCapitalShips(fleets).Any();
        }

        /// <summary>
        /// Captures the units carried by the attacking fleets before combat mutates the scene graph.
        /// </summary>
        /// <param name="fleets">The attacking fleets.</param>
        /// <returns>The attacking unit snapshot.</returns>
        private static List<ISceneNode> SnapshotFleetUnits(IEnumerable<Fleet> fleets)
        {
            return fleets
                .Where(fleet => fleet != null)
                .SelectMany(fleet => fleet.GetChildren<ISceneNode>(_ => true))
                .Distinct()
                .ToList();
        }

        /// <summary>
        /// Captures the target owner's units before combat mutates the scene graph.
        /// </summary>
        /// <param name="planet">The target planet.</param>
        /// <param name="ownerInstanceId">The target owner identifier.</param>
        /// <returns>The defending unit snapshot.</returns>
        private static List<ISceneNode> SnapshotPlanetUnits(Planet planet, string ownerInstanceId)
        {
            return planet
                .GetChildren<ISceneNode>(unit => unit.GetOwnerInstanceID() == ownerInstanceId)
                .Distinct()
                .ToList();
        }

        /// <summary>
        /// Sets the combat state for the attacking fleets and fleets stationed at the planet.
        /// </summary>
        /// <param name="attackers">Fleets performing the bombardment.</param>
        /// <param name="planet">Planet where the bombardment is occurring.</param>
        /// <param name="isInCombat">Whether the affected fleets are in combat.</param>
        private static void SetBombardmentCombatState(
            List<Fleet> attackers,
            Planet planet,
            bool isInCombat
        )
        {
            foreach (Fleet fleet in attackers)
                fleet.IsInCombat = isInCombat;

            foreach (Fleet fleet in planet.Fleets)
                fleet.IsInCombat = isInCombat;
        }

        /// <summary>
        /// Calculates the total effective bombardment strength of the attacking fleets.
        /// </summary>
        /// <param name="fleets">Fleets contributing ships and starfighters.</param>
        /// <returns>The combined bombardment strength after condition and leadership adjustments.</returns>
        private int CalculateBombardmentStrength(IReadOnlyList<Fleet> fleets)
        {
            int divisor = _game.Config.Combat.Bombardment.AttackerLeadershipDivisor;
            int total = 0;

            foreach (Fleet fleet in fleets)
            {
                int leadership = GetBombardmentLeadership(
                    fleet.GetOfficers(),
                    OfficerRank.Admiral,
                    fleet.GetOwnerInstanceID()
                );
                int multiplier = leadership / divisor + 1;
                int fleetStrength = 0;

                foreach (CapitalShip ship in fleet.CapitalShips.Where(IsActiveBombardmentUnit))
                {
                    fleetStrength += ScaleByCondition(
                        ship.Bombardment,
                        ship.CurrentHullStrength,
                        ship.MaxHullStrength
                    );
                    fleetStrength += ship
                        .Starfighters.Where(IsActiveBombardmentUnit)
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

        /// <summary>
        /// Calculates the total protection supplied by active planetary shield facilities.
        /// </summary>
        /// <param name="planet">Planet whose shields are evaluated.</param>
        /// <returns>The combined active shield strength.</returns>
        private static int CalculatePlanetShieldStrength(Planet planet)
        {
            return planet
                .GetAllBuildings()
                .Where(building =>
                    IsActiveBombardmentUnit(building)
                    && building.DefenseFacilityClass == DefenseFacilityClass.Shield
                )
                .Sum(building => building.ShieldStrength);
        }

        /// <summary>
        /// Resolves planetary defense-facility fire against the attacking capital ships.
        /// </summary>
        /// <param name="attackingFleets">Fleets exposed to defense fire.</param>
        /// <param name="planet">Planet containing the defending facilities.</param>
        /// <param name="result">Bombardment result receiving ship damage and destruction.</param>
        private void ResolveBombardmentDefenseFire(
            List<Fleet> attackingFleets,
            Planet planet,
            BombardmentResult result
        )
        {
            List<CapitalShip> targets = GetActiveCapitalShips(attackingFleets);
            if (targets.Count == 0)
                return;

            int leadership = GetBombardmentLeadership(
                planet.GetAllOfficers(),
                OfficerRank.General,
                planet.GetOwnerInstanceID()
            );
            int multiplier =
                leadership / _game.Config.Combat.Bombardment.DefenderLeadershipDivisor + 1;
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
                CapitalShipDestruction.Resolve(_game, _movement, ship);
            }
        }

        /// <summary>
        /// Resolves the available bombardment strike attempts against eligible targets.
        /// </summary>
        /// <param name="planet">Planet containing the targets.</param>
        /// <param name="defenderId">Defending faction instance ID.</param>
        /// <param name="type">Bombardment mode controlling eligible target lanes.</param>
        /// <param name="result">Bombardment result receiving successful strikes.</param>
        /// <returns>True when at least one civilian target was destroyed.</returns>
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

        /// <summary>
        /// Attempts one collateral strike against a civilian target.
        /// </summary>
        /// <param name="planet">Planet containing potential targets.</param>
        /// <param name="result">Bombardment result receiving a successful strike.</param>
        /// <returns>True when a civilian target was successfully struck.</returns>
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

        /// <summary>
        /// Builds the currently eligible target list for a bombardment mode.
        /// </summary>
        /// <param name="planet">Planet containing potential targets.</param>
        /// <param name="defenderId">Defending faction instance ID.</param>
        /// <param name="type">Bombardment mode controlling eligible target lanes.</param>
        /// <returns>The ordered list of eligible targets.</returns>
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

        /// <summary>
        /// Builds the active military targets on a planet.
        /// </summary>
        /// <param name="planet">Planet containing potential targets.</param>
        /// <param name="defenderId">Defending faction instance ID.</param>
        /// <returns>Defending regiments, defense facilities, and an eligible headquarters.</returns>
        private List<BombardmentTarget> BuildMilitaryTargets(Planet planet, string defenderId)
        {
            List<BombardmentTarget> targets = planet
                .GetAllRegiments()
                .Where(regiment =>
                    IsActiveBombardmentUnit(regiment) && regiment.GetOwnerInstanceID() == defenderId
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
                    .Where(building =>
                        IsActiveBombardmentUnit(building) && IsBombardmentDefenseFacility(building)
                    )
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
                        Resistance = _game.Config.Combat.Bombardment.HeadquartersResistance,
                    }
                );
            }

            return targets;
        }

        /// <summary>
        /// Builds the active civilian facility targets on a planet.
        /// </summary>
        /// <param name="planet">Planet containing potential targets.</param>
        /// <returns>The eligible civilian facilities.</returns>
        private static List<BombardmentTarget> BuildCivilianTargets(Planet planet)
        {
            return planet
                .GetAllBuildings()
                .Where(building =>
                    IsActiveBombardmentUnit(building) && IsCivilianBuilding(building)
                )
                .Select(building => new BombardmentTarget
                {
                    Type = BombardmentTargetType.Building,
                    Entity = building,
                    Resistance = building.Bombardment,
                    IsCivilian = true,
                })
                .ToList();
        }

        /// <summary>
        /// Adds damageable energy-capacity targets to a target list.
        /// </summary>
        /// <param name="planet">Planet supplying the energy pools.</param>
        /// <param name="targets">Target list to update.</param>
        private void AddEnergyTargets(Planet planet, List<BombardmentTarget> targets)
        {
            if (planet.EnergyCapacity > 0)
            {
                targets.Add(
                    new BombardmentTarget
                    {
                        Type = BombardmentTargetType.EnergyCapacity,
                        Resistance = _game.Config.Combat.Bombardment.EnergyResistance,
                    }
                );
            }

            if (planet.AllocatedEnergy > 0)
            {
                targets.Add(
                    new BombardmentTarget
                    {
                        Type = BombardmentTargetType.AllocatedEnergy,
                        Resistance = _game.Config.Combat.Bombardment.AllocatedEnergyResistance,
                    }
                );
            }
        }

        /// <summary>
        /// Determines whether a strike overcomes a target's resistance.
        /// </summary>
        /// <param name="resistance">Resistance of the selected target.</param>
        /// <returns>True when the strike succeeds.</returns>
        private bool RollStrike(int resistance)
        {
            GameConfig.BombardmentConfig config = _game.Config.Combat.Bombardment;
            int roll = _provider.NextInt(config.StrikeRollMinimum, config.StrikeRollMaximum + 1);
            return resistance < roll;
        }

        /// <summary>
        /// Applies a successful strike and records its outcome.
        /// </summary>
        /// <param name="planet">Planet containing the target.</param>
        /// <param name="defenderId">Defending faction instance ID.</param>
        /// <param name="target">Target selected for the strike.</param>
        /// <param name="result">Bombardment result receiving the strike details.</param>
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

        /// <summary>
        /// Removes a faction headquarters from its planet and owning faction.
        /// </summary>
        /// <param name="planet">Planet containing the headquarters.</param>
        /// <param name="defenderId">Owning faction instance ID.</param>
        /// <param name="result">Bombardment result receiving the destruction flag.</param>
        private void DestroyHeadquarters(Planet planet, string defenderId, BombardmentResult result)
        {
            planet.IsHeadquarters = false;
            if (!string.IsNullOrEmpty(defenderId))
                _game.GetFactionByOwnerInstanceID(defenderId).HQInstanceID = null;
            result.HeadquartersDestroyed = true;
        }

        /// <summary>
        /// Returns the display name used to report a bombardment target.
        /// </summary>
        /// <param name="target">Target to describe.</param>
        /// <returns>The target's report display name.</returns>
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

        /// <summary>
        /// Determines whether the defending headquarters is an eligible target.
        /// </summary>
        /// <param name="planet">Planet containing the headquarters.</param>
        /// <param name="defenderId">Defending faction instance ID.</param>
        /// <returns>True when the headquarters exists and its faction permits bombardment.</returns>
        private bool CanBombardHeadquarters(Planet planet, string defenderId)
        {
            return planet.IsHeadquarters
                && !string.IsNullOrEmpty(defenderId)
                && _game
                    .GetFactionByOwnerInstanceID(defenderId)
                    .Settings.HeadquartersCanBeBombarded;
        }

        /// <summary>
        /// Determines whether the attacking fleets contain an active planet-destroying ship.
        /// </summary>
        /// <param name="fleets">Attacking fleets to inspect.</param>
        /// <returns>True when an active configured ship type is present.</returns>
        private bool HasPlanetDestroyingShip(IEnumerable<Fleet> fleets)
        {
            HashSet<string> typeIds =
                _game.Config.Combat.Bombardment.PlanetDestroyingCapitalShipTypeIDs.ToHashSet();
            return GetActiveCapitalShips(fleets).Any(ship => typeIds.Contains(ship.GetTypeID()));
        }

        /// <summary>
        /// Marks a planet destroyed and resolves effects on eligible personnel.
        /// </summary>
        /// <param name="planet">Planet being destroyed.</param>
        /// <param name="result">Bombardment result receiving personnel consequences.</param>
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
                if (
                    !RollBombardmentPercent(
                        _game.Config.Combat.Bombardment.DestroySystemPersonnelInjuryPercent
                    )
                )
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

                if (
                    !RollBombardmentPercent(
                        _game.Config.Combat.Bombardment.DestroySystemMinorPersonnelDeathPercent
                    )
                )
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

        /// <summary>
        /// Applies direct and local-system support penalties for civilian destruction.
        /// </summary>
        /// <param name="planet">Planet where civilian targets were destroyed.</param>
        /// <param name="attacker">Faction responsible for the bombardment.</param>
        /// <returns>Ownership changes caused by the support shifts.</returns>
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

        /// <summary>
        /// Applies the direct popular-support penalty at the bombarded planet.
        /// </summary>
        /// <param name="planet">Planet receiving the support shift.</param>
        /// <param name="attacker">Faction responsible for the bombardment.</param>
        /// <returns>Ownership changes caused by the support shift.</returns>
        private List<PlanetOwnershipChangedResult> ApplyDirectBombardmentPenalty(
            Planet planet,
            Faction attacker
        )
        {
            int shift = _game.Config.Combat.Bombardment.CivilianSupportPenalty;
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

        /// <summary>
        /// Returns the local-system support penalty for civilian destruction.
        /// </summary>
        /// <param name="system">Planet system where the destruction occurred.</param>
        /// <param name="attacker">Faction responsible for the bombardment.</param>
        /// <returns>The applicable popular-support shift.</returns>
        private int GetCivilianSystemPenalty(PlanetSystem system, Faction attacker)
        {
            bool empire = attacker.Settings.InvertSupportShift;
            if (system.SystemType == PlanetSystemType.CoreSystem)
            {
                return empire
                    ? _game.Config.Combat.Bombardment.CivilianCoreEmpireSupportPenalty
                    : _game.Config.Combat.Bombardment.CivilianCoreAllianceSupportPenalty;
            }

            return empire
                ? _game.Config.Combat.Bombardment.CivilianOuterRimEmpireSupportPenalty
                : _game.Config.Combat.Bombardment.CivilianOuterRimAllianceSupportPenalty;
        }

        /// <summary>
        /// Applies galaxy-wide support penalties after a planet is destroyed.
        /// </summary>
        /// <param name="attacker">Faction responsible for destroying the planet.</param>
        /// <returns>Ownership changes caused by the support shifts.</returns>
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
                    _game.Config.Combat.Bombardment.DestroySystemCoreSupportPenalty
                )
            );

            List<Planet> outerRimPlanets = _game
                .GetSceneNodesByType<PlanetSystem>()
                .Where(system => system.SystemType == PlanetSystemType.OuterRim)
                .SelectMany(GetAffectedPlanets)
                .Where(planet =>
                    planet.GetPopularSupport(attacker.InstanceID)
                    < _game.Config.Combat.Bombardment.DestroySystemOuterRimSupportThreshold
                )
                .ToList();
            results.AddRange(
                _ownership.ShiftBombardmentSupport(
                    outerRimPlanets,
                    attacker,
                    _game.Config.Combat.Bombardment.DestroySystemOuterRimSupportPenalty
                )
            );
            return results;
        }

        /// <summary>
        /// Reconciles planet control after bombardment removes the defending garrison.
        /// </summary>
        /// <param name="planet">Bombarded planet.</param>
        /// <param name="previousOwnerId">Faction instance ID that controlled the planet.</param>
        /// <param name="initialDefenderRegimentCount">Number of active defenders before bombardment.</param>
        /// <returns>Ownership changes caused by the garrison removal.</returns>
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

        /// <summary>
        /// Merges ownership changes into the bombardment result by affected planet.
        /// </summary>
        /// <param name="result">Bombardment result to update.</param>
        /// <param name="changes">Ownership changes to merge.</param>
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

        /// <summary>
        /// Combines sequential ownership changes for one planet.
        /// </summary>
        /// <param name="first">Existing ownership change.</param>
        /// <param name="second">Subsequent ownership change.</param>
        /// <returns>The combined change, or null when the planet returns to its original owner.</returns>
        private static PlanetOwnershipChangedResult MergeOwnershipChanges(
            PlanetOwnershipChangedResult first,
            PlanetOwnershipChangedResult second
        )
        {
            if (first == null)
                return second;
            if (second != null)
            {
                first.NewOwner = second.NewOwner;
                first.ObserverFactionInstanceIDs = (
                    first.ObserverFactionInstanceIDs ?? Enumerable.Empty<string>()
                )
                    .Concat(second.ObserverFactionInstanceIDs ?? Enumerable.Empty<string>())
                    .Distinct()
                    .ToList();
                if (second.Reason != PlanetOwnershipChangeReason.None)
                    first.Reason = second.Reason;
            }
            if (first.PreviousOwner?.InstanceID == first.NewOwner?.InstanceID)
                return null;
            return first;
        }

        /// <summary>
        /// Rolls a percentage chance for a bombardment event.
        /// </summary>
        /// <param name="chance">Percentage chance threshold.</param>
        /// <returns>True when the roll succeeds.</returns>
        private bool RollBombardmentPercent(int chance)
        {
            return _provider.NextInt(0, 100) < chance;
        }

        /// <summary>
        /// Returns populated, undestroyed planets affected by a system support shift.
        /// </summary>
        /// <param name="system">Planet system to inspect.</param>
        /// <returns>The planets eligible for the shift.</returns>
        private static List<Planet> GetAffectedPlanets(PlanetSystem system)
        {
            return system
                .Planets.Where(planet => planet.IsPopulated() && !planet.IsDestroyed)
                .ToList();
        }

        /// <summary>
        /// Returns active capital ships from the supplied fleets.
        /// </summary>
        /// <param name="fleets">Fleets to inspect.</param>
        /// <returns>The active capital ships.</returns>
        private static List<CapitalShip> GetActiveCapitalShips(IEnumerable<Fleet> fleets)
        {
            return fleets
                .SelectMany(fleet => fleet.CapitalShips)
                .Where(IsActiveBombardmentUnit)
                .ToList();
        }

        /// <summary>
        /// Returns active defense facilities of a specified class.
        /// </summary>
        /// <param name="planet">Planet containing the facilities.</param>
        /// <param name="defenseClass">Defense-facility class to select.</param>
        /// <returns>The matching active facilities.</returns>
        private static IEnumerable<Building> GetActiveDefenseFacilities(
            Planet planet,
            DefenseFacilityClass defenseClass
        )
        {
            return planet
                .GetAllBuildings()
                .Where(building =>
                    IsActiveBombardmentUnit(building)
                    && building.DefenseFacilityClass == defenseClass
                );
        }

        /// <summary>
        /// Returns active defending regiments owned by the specified faction.
        /// </summary>
        /// <param name="planet">Planet containing the defenders.</param>
        /// <param name="defenderId">Defending faction instance ID.</param>
        /// <returns>The active defending regiments.</returns>
        private static List<Regiment> GetActiveDefenderRegiments(Planet planet, string defenderId)
        {
            return planet
                .GetAllRegiments()
                .Where(regiment =>
                    IsActiveBombardmentUnit(regiment) && regiment.GetOwnerInstanceID() == defenderId
                )
                .ToList();
        }

        /// <summary>
        /// Returns the leadership rating of the first eligible bombardment commander.
        /// </summary>
        /// <param name="officers">Officers to search.</param>
        /// <param name="rank">Required command rank.</param>
        /// <param name="ownerId">Required faction instance ID.</param>
        /// <returns>The commander's leadership rating, or zero when none is eligible.</returns>
        private static int GetBombardmentLeadership(
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

        /// <summary>
        /// Returns a capital ship's shield strength at its current hull condition.
        /// </summary>
        /// <param name="ship">Capital ship to evaluate.</param>
        /// <returns>The effective shield strength.</returns>
        private static int GetEffectiveShieldStrength(CapitalShip ship)
        {
            return ScaleByCondition(
                ship.MaxShieldStrength,
                ship.CurrentHullStrength,
                ship.MaxHullStrength
            );
        }

        /// <summary>
        /// Scales a unit value by its current condition.
        /// </summary>
        /// <param name="value">Undamaged value.</param>
        /// <param name="current">Current condition.</param>
        /// <param name="maximum">Maximum condition.</param>
        /// <returns>The condition-adjusted value.</returns>
        private static int ScaleByCondition(int value, int current, int maximum)
        {
            return maximum > 0 ? value * Math.Max(0, current) / maximum : value;
        }

        /// <summary>
        /// Determines whether a manufacturable unit is complete and stationary.
        /// </summary>
        /// <param name="unit">Unit to inspect.</param>
        /// <returns>True when the unit can participate in bombardment.</returns>
        private static bool IsActiveBombardmentUnit(IManufacturable unit)
        {
            return unit.ManufacturingStatus == ManufacturingStatus.Complete
                && unit.Movement == null;
        }

        /// <summary>
        /// Determines whether a capital ship can participate in bombardment.
        /// </summary>
        /// <param name="ship">Capital ship to inspect.</param>
        /// <returns>True when the ship is active and has remaining hull strength.</returns>
        private static bool IsActiveBombardmentUnit(CapitalShip ship)
        {
            return IsActiveBombardmentUnit((IManufacturable)ship) && ship.CurrentHullStrength > 0;
        }

        /// <summary>
        /// Determines whether a starfighter group can contribute to bombardment.
        /// </summary>
        /// <param name="fighter">Starfighter group to inspect.</param>
        /// <returns>True when the group is active and has remaining fighters.</returns>
        private static bool IsActiveBombardmentUnit(Starfighter fighter)
        {
            return IsActiveBombardmentUnit((IManufacturable)fighter)
                && fighter.CurrentSquadronSize > 0;
        }

        /// <summary>
        /// Determines whether a building belongs to a bombardment defense target lane.
        /// </summary>
        /// <param name="building">Building to inspect.</param>
        /// <returns>True when the building is a planetary defense facility.</returns>
        private static bool IsBombardmentDefenseFacility(Building building)
        {
            return building.DefenseFacilityClass
                is DefenseFacilityClass.KDY
                    or DefenseFacilityClass.LNR
                    or DefenseFacilityClass.Shield
                    or DefenseFacilityClass.DeathStarShield;
        }

        /// <summary>
        /// Determines whether a building belongs to a civilian bombardment target lane.
        /// </summary>
        /// <param name="building">Building to inspect.</param>
        /// <returns>True when the building is a civilian or manufacturing facility.</returns>
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
