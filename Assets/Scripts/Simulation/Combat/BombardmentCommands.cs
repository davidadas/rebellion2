using System;
using System.Collections.Generic;
using System.Linq;
using Rebellion.Game;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Results;
using Rebellion.Game.Units;
using Rebellion.SceneGraph;
using Rebellion.Util.Logging;
using Rebellion.Util.Random;

namespace Rebellion.Simulation
{
    /// <summary>
    /// Resolves orbital bombardment against planets.
    /// </summary>
    public class BombardmentCommands
    {
        private readonly GameRoot _game;
        private readonly BombardmentQueries _queries;
        private readonly IRandomNumberProvider _provider;
        private readonly PersonnelCommands _personnelCommands;

        /// <summary>
        /// Raised after an immediate bombardment command produces results.
        /// </summary>
        public event Action<IReadOnlyList<GameResult>> ResultsProduced;

        /// <summary>
        /// Creates bombardment commands.
        /// </summary>
        /// <param name="game">Active game state.</param>
        /// <param name="provider">Random-number provider used by bombardment resolution.</param>
        /// <param name="queries">Bombardment eligibility and strength rules.</param>
        /// <param name="personnelCommands">Personnel lifecycle commands.</param>
        public BombardmentCommands(
            GameRoot game,
            IRandomNumberProvider provider,
            BombardmentQueries queries,
            PersonnelCommands personnelCommands = null
        )
        {
            _game = game;
            _provider = provider;
            _personnelCommands =
                personnelCommands ?? new PersonnelCommands(new PersonnelQueries(game));
            _queries = queries ?? throw new ArgumentNullException(nameof(queries));
        }

        /// <summary>
        /// Executes a validated orbital bombardment command and publishes its results.
        /// </summary>
        /// <param name="attackingFleets">The attacking fleets.</param>
        /// <param name="targetPlanet">The bombardment target planet.</param>
        /// <param name="type">The bombardment target profile.</param>
        /// <returns>The bombardment result, or null when bombardment cannot execute.</returns>
        public BombardmentResult TryExecute(
            IReadOnlyList<Fleet> attackingFleets,
            Planet targetPlanet,
            BombardmentType type
        )
        {
            if (targetPlanet == null)
                return null;

            List<Fleet> fleets =
                attackingFleets?.Where(fleet => fleet != null).ToList() ?? new List<Fleet>();
            if (!_queries.CanExecute(fleets, targetPlanet, type))
                return null;

            BombardmentResult result = Execute(fleets, targetPlanet, type);
            List<GameResult> results = new List<GameResult> { result };
            results.AddRange(result.Events);
            if (result.OwnershipChange != null)
                results.Add(result.OwnershipChange);

            ResultsProduced?.Invoke(results);
            return result;
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

            if (!_queries.CanExecute(attackingFleets, targetPlanet, type))
                return result;

            string attackerId = attackingFleets[0].GetOwnerInstanceID();
            string defenderId = targetPlanet.GetOwnerInstanceID();
            result.AttackingFaction = _game.GetFactionByOwnerInstanceID(attackerId);
            result.AttackerOwnerInstanceID = attackerId;
            result.DefenderOwnerInstanceID = defenderId;
            result.AttackingUnits.AddRange(CombatUnitSnapshot.CaptureFleetUnits(attackingFleets));
            result.DefendingUnits.AddRange(
                CombatUnitSnapshot.CapturePlanetUnits(targetPlanet, defenderId)
            );

            SetBombardmentCombatState(attackingFleets, targetPlanet, true);
            try
            {
                bool destroysPlanet =
                    type == BombardmentType.DestroyPlanet
                    && BombardmentQueries.HasPlanetDestroyingShip(attackingFleets);
                if (destroysPlanet)
                    DestroyPlanet(targetPlanet, result);

                ResolveBombardmentDefenseFire(attackingFleets, targetPlanet, result);
                if (destroysPlanet)
                    return result;

                if (!BombardmentQueries.GetActiveCapitalShips(attackingFleets).Any())
                    return result;

                result.BombardmentStrength = BombardmentQueries.GetBombardmentStrength(
                    attackingFleets,
                    _game.Config.Combat.Bombardment
                );
                result.ShieldStrength = BombardmentQueries.GetBombardmentShieldStrength(
                    targetPlanet
                );
                result.StrikeAttempts = Math.Max(
                    0,
                    result.BombardmentStrength - result.ShieldStrength
                );

                ResolveStrikes(targetPlanet, defenderId, type, result);

                if (result.DestroyedRegiments.Count > 0)
                {
                    result.Events.Add(
                        new PlanetGarrisonChangedResult
                        {
                            Planet = targetPlanet,
                            Tick = _game.CurrentTick,
                        }
                    );
                }

                return result;
            }
            finally
            {
                RecordUnitOutcomes(result);
                SetBombardmentCombatState(attackingFleets, targetPlanet, false);
            }
        }

        /// <summary>
        /// Records which captured units were damaged or destroyed by bombardment.
        /// </summary>
        /// <param name="result">The completed bombardment result.</param>
        private static void RecordUnitOutcomes(BombardmentResult result)
        {
            CombatUnitSnapshot.RecordOutcomes(
                result.AttackingUnits,
                result.AttackerShipDamage.Select(damage => damage.Ship),
                result.DestroyedCapitalShips
            );
            CombatUnitSnapshot.RecordOutcomes(
                result.DefendingUnits,
                null,
                result
                    .DestroyedBuildings.Cast<ISceneNode>()
                    .Concat(result.DestroyedRegiments)
                    .Concat(
                        result
                            .Events.OfType<OfficerKilledResult>()
                            .Select(killed => killed.TargetOfficer)
                    )
            );
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
                fleet.SetCombatState(isInCombat);

            foreach (Fleet fleet in planet.GetChildren<Fleet>())
                fleet.SetCombatState(isInCombat);
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
            List<CapitalShip> targets = BombardmentQueries.GetActiveCapitalShips(attackingFleets);
            if (targets.Count == 0)
                return;

            int leadership = BombardmentQueries.GetBombardmentLeadership(
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

            IEnumerable<Building> facilities = GetActiveDefenseFacilities(planet)
                .OrderBy(facility =>
                    facility.DefenseWeaponEffect == DefenseWeaponEffect.ShieldDamage ? 0 : 1
                );

            foreach (Building facility in facilities)
            {
                CapitalShip target = targets[_provider.NextInt(0, targets.Count)];
                int damage = facility.WeaponPower * multiplier;
                int absorbed = Math.Min(remainingShields[target], damage);
                remainingShields[target] -= absorbed;

                if (facility.DefenseWeaponEffect == DefenseWeaponEffect.HullDamage)
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
                RecordDestroyedEmbarkedCombatUnits(ship, planet, result.Events);
                _game.DeleteNode(ship);
                result.Events.Add(
                    new GameObjectDestroyedResult
                    {
                        DestroyedObject = ship,
                        Context = planet,
                        Reason = UnitDestructionReason.Combat,
                        Tick = _game.CurrentTick,
                    }
                );
                GameLogger.Log($"Ship destroyed: {ship.GetDisplayName()}");
            }
        }

        /// <summary>
        /// Records embarked starfighters, regiments, and special-forces units that are destroyed
        /// with a carrier by planetary defense fire. Officers remain eligible for
        /// post-destruction relocation.
        /// </summary>
        /// <param name="ship">The carrier about to be destroyed.</param>
        /// <param name="planet">The planet where the destruction occurred.</param>
        /// <param name="events">The event collection receiving destruction facts.</param>
        private void RecordDestroyedEmbarkedCombatUnits(
            CapitalShip ship,
            Planet planet,
            ICollection<GameResult> events
        )
        {
            IEnumerable<IGameEntity> destroyedUnits = ship.GetChildren<Starfighter>(
                    includeDisabled: true
                )
                .Cast<IGameEntity>()
                .Concat(ship.GetChildren<Regiment>(includeDisabled: true))
                .Concat(ship.GetChildren<SpecialForces>(includeDisabled: true));
            foreach (IGameEntity unit in destroyedUnits)
            {
                events.Add(
                    new GameObjectDestroyedResult
                    {
                        DestroyedObject = unit,
                        Context = planet,
                        Reason = UnitDestructionReason.Combat,
                        Tick = _game.CurrentTick,
                    }
                );
            }
        }

        /// <summary>
        /// Resolves the available bombardment strike attempts against eligible targets.
        /// </summary>
        /// <param name="planet">Planet containing the targets.</param>
        /// <param name="defenderId">Defending faction instance ID.</param>
        /// <param name="type">Bombardment mode controlling eligible target lanes.</param>
        /// <param name="result">Bombardment result receiving successful strikes.</param>
        private void ResolveStrikes(
            Planet planet,
            string defenderId,
            BombardmentType type,
            BombardmentResult result
        )
        {
            if (type == BombardmentType.Military && result.StrikeAttempts > 0)
            {
                int militaryTargetCount = BuildTargets(planet, defenderId, type).Count;
                if (_provider.NextInt(0, militaryTargetCount + 1) == 0)
                    TryStrikeCivilianTarget(planet, result);
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
            }
        }

        /// <summary>
        /// Attempts one collateral strike against a civilian target.
        /// </summary>
        /// <param name="planet">Planet containing potential targets.</param>
        /// <param name="result">Bombardment result receiving a successful strike.</param>
        private void TryStrikeCivilianTarget(Planet planet, BombardmentResult result)
        {
            List<BombardmentTarget> targets = BuildCivilianTargets(planet);
            if (targets.Count == 0)
                return;

            BombardmentTarget target = targets[_provider.NextInt(0, targets.Count)];
            if (!RollStrike(target.Resistance))
                return;

            ApplyStrike(planet, planet.GetOwnerInstanceID(), target, result);
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
                    or BombardmentType.DestroyPlanet
            )
            {
                targets.AddRange(BuildMilitaryTargets(planet, defenderId));
            }

            if (
                type
                is BombardmentType.Civilian
                    or BombardmentType.General
                    or BombardmentType.DestroyPlanet
            )
            {
                targets.AddRange(BuildCivilianTargets(planet));
            }

            if (type is BombardmentType.General or BombardmentType.DestroyPlanet)
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
                    BombardmentQueries.IsActiveBombardmentUnit(regiment)
                    && regiment.GetOwnerInstanceID() == defenderId
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
                        BombardmentQueries.IsActiveBombardmentUnit(building)
                        && BombardmentQueries.IsBombardmentDefenseFacility(building)
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
                    BombardmentQueries.IsActiveBombardmentUnit(building)
                    && BombardmentQueries.IsCivilianTarget(building)
                )
                .Select(building => new BombardmentTarget
                {
                    Type = BombardmentTargetType.Building,
                    Entity = building,
                    Resistance = building.Bombardment,
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
                    _game.DeleteNode(regiment);
                    break;
                case BombardmentTargetType.Building:
                    Building building = (Building)target.Entity;
                    result.DestroyedBuildings.Add(building);
                    if (string.IsNullOrEmpty(building.OwnerInstanceID))
                    {
                        _game.DetachNode(building);
                    }
                    else
                    {
                        _game.DeleteNode(building);
                    }
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
                    .Settings.Headquarters?.IsBombardable == true;
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
                        _game.Config.Combat.Bombardment.DestroyPlanetPersonnelInjuryPercent
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
                        _game.Config.Combat.Bombardment.DestroyPlanetMinorPersonnelDeathPercent
                    )
                )
                    continue;

                _personnelCommands.KillOfficer(officer);
                result.Events.Add(
                    new OfficerKilledResult
                    {
                        TargetOfficer = officer,
                        Context = planet,
                        Tick = _game.CurrentTick,
                    }
                );
            }
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
        /// Returns active planetary defense weapons.
        /// </summary>
        /// <param name="planet">Planet containing the facilities.</param>
        /// <returns>The matching active facilities.</returns>
        private static IEnumerable<Building> GetActiveDefenseFacilities(Planet planet)
        {
            return planet
                .GetAllBuildings()
                .Where(building =>
                    BombardmentQueries.IsActiveBombardmentUnit(building)
                    && building.BuildingType == BuildingType.Weapon
                );
        }

        /// <summary>
        /// Returns a capital ship's shield strength at its current hull condition.
        /// </summary>
        /// <param name="ship">Capital ship to evaluate.</param>
        /// <returns>The effective shield strength.</returns>
        private static int GetEffectiveShieldStrength(CapitalShip ship)
        {
            return BombardmentQueries.ScaleByCondition(
                ship.MaxShieldStrength,
                ship.CurrentHullStrength,
                ship.MaxHullStrength
            );
        }

        private class BombardmentTarget
        {
            public BombardmentTargetType Type;
            public IGameEntity Entity;
            public int Resistance;
        }

        private enum BombardmentTargetType
        {
            Regiment,
            Building,
            Headquarters,
            EnergyCapacity,
            AllocatedEnergy,
        }
    }
}
