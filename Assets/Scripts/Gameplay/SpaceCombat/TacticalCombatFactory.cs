using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using Rebellion.Game;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Results;
using Rebellion.Game.SpaceCombat;
using Rebellion.Game.Units;
using Rebellion.SceneGraph;

namespace Rebellion.Gameplay
{
    /// <summary>
    /// Captures campaign rosters, command officers, and departure routes for isolated tactical encounters.
    /// </summary>
    public sealed class TacticalCombatFactory
    {
        private readonly GameRoot _game;
        private readonly SpaceCombatQueries _queries;
        private readonly Func<TacticalCombatRandom> _createRandom;
        private readonly Func<DateTimeOffset> _localClock;
        private readonly TacticalCombatDefinition _definition;
        private readonly Dictionary<string, TacticalCombatUnitDefinition> _definitions;
        private readonly Dictionary<string, TacticalCombatFactionDefinition> _factions;

        /// <summary>
        /// Creates the battle-entry boundary for the active campaign and content catalog.
        /// </summary>
        /// <param name="game">The active campaign graph.</param>
        /// <param name="catalog">The selected content catalog.</param>
        /// <param name="queries">The campaign encounter and departure queries.</param>
        /// <param name="createRandom">An optional replay stream factory; live battles capture the entry clock.</param>
        /// <param name="localClock">The platform's local clock, acquired only when scenery first needs a random position.</param>
        public TacticalCombatFactory(
            GameRoot game,
            GameDataCatalog catalog,
            SpaceCombatQueries queries,
            Func<TacticalCombatRandom> createRandom = null,
            Func<DateTimeOffset> localClock = null
        )
        {
            this._game = game ?? throw new ArgumentNullException(nameof(game));
            _createRandom =
                createRandom
                ?? (() => new TacticalCombatRandom(unchecked((uint)Environment.TickCount)));
            _localClock = localClock ?? (() => DateTimeOffset.Now);
            this._queries = queries ?? throw new ArgumentNullException(nameof(queries));
            _definition =
                catalog?.TacticalCombat ?? throw new ArgumentNullException(nameof(catalog));
            _definitions = _definition.Units.ToDictionary(
                unit => unit.UnitTypeID,
                StringComparer.Ordinal
            );
            _factions = _definition.Factions.ToDictionary(
                faction => faction.FactionInstanceID,
                StringComparer.Ordinal
            );
        }

        /// <summary>
        /// Checks that both factions and every active combat unit have authored tactical definitions.
        /// </summary>
        /// <param name="decision">The pending campaign encounter.</param>
        /// <returns>Whether the encounter can be deployed.</returns>
        internal bool Supports(SpaceCombatDecision decision)
        {
            if (
                decision == null
                || !_factions.ContainsKey(decision.AttackerOwnerInstanceID)
                || !_factions.ContainsKey(decision.DefenderOwnerInstanceID)
            )
                return false;
            Planet planet = _queries.ResolveCombatPlanet(decision);
            if (planet == null)
                return false;
            foreach (
                string owner in new[]
                {
                    decision.AttackerOwnerInstanceID,
                    decision.DefenderOwnerInstanceID,
                }
            )
            {
                IReadOnlyList<Fleet> fleets = _queries.GetFleets(
                    owner == decision.AttackerOwnerInstanceID
                        ? decision.AttackerFleetInstanceIDs
                        : decision.DefenderFleetInstanceIDs
                );
                foreach (
                    CapitalShip ship in fleets.SelectMany(SpaceCombatQueries.GetActiveCapitalShips)
                )
                {
                    if (!_definitions.ContainsKey(ship.TypeID))
                        return false;
                    foreach (
                        Starfighter fighter in ship.GetChildren<Starfighter>()
                            .Where(SpaceCombatQueries.IsActiveStarfighter)
                    )
                        if (!_definitions.ContainsKey(fighter.TypeID))
                            return false;
                }
                foreach (
                    Starfighter fighter in SpaceCombatQueries.GetActivePlanetStarfighters(
                        planet,
                        owner
                    )
                )
                    if (!_definitions.ContainsKey(fighter.TypeID))
                        return false;
            }
            CapitalShip construction = GetConstructionStation(planet);
            return construction == null
                || construction.OwnerInstanceID != decision.AttackerOwnerInstanceID
                    && construction.OwnerInstanceID != decision.DefenderOwnerInstanceID
                || _definitions.ContainsKey(construction.TypeID);
        }

        /// <summary>
        /// Captures the pending encounter without changing campaign units or clearing its pending decision.
        /// </summary>
        /// <param name="decision">The pending campaign encounter.</param>
        /// <returns>The isolated deployed tactical session.</returns>
        internal TacticalCombatSession Create(SpaceCombatDecision decision)
        {
            Planet planet =
                _queries.ResolveCombatPlanet(decision)
                ?? throw new InvalidOperationException("The combat system is unavailable.");
            IReadOnlyList<Fleet>[] fleets =
            {
                _queries.GetFleets(decision.AttackerFleetInstanceIDs),
                _queries.GetFleets(decision.DefenderFleetInstanceIDs),
            };
            string[] owners =
            {
                decision.AttackerOwnerInstanceID,
                decision.DefenderOwnerInstanceID,
            };
            List<TacticalCombatUnit> units = new();
            List<string> personnelHostInstanceIDs = new();
            Dictionary<string, TacticalCombatUnit> byID = new(StringComparer.Ordinal);
            TacticalCombatFactionDefinition[] factionDefinitions =
                new TacticalCombatFactionDefinition[2];
            int[] counters = new int[2];
            Dictionary<string, int> sidesByOwner = new(StringComparer.Ordinal);
            for (int side = 0; side < 2; side++)
            {
                if (
                    !_factions.TryGetValue(
                        owners[side],
                        out TacticalCombatFactionDefinition faction
                    )
                )
                    throw new InvalidDataException(
                        $"Tactical deployment is missing for faction '{owners[side]}'."
                    );
                if (faction.FactionIndex is < 0 or > 1 || Math.Abs(faction.ForwardZ) != 1)
                    throw new InvalidDataException(
                        $"Tactical deployment is invalid for faction '{owners[side]}'."
                    );
                factionDefinitions[side] = faction;
                counters[side] = faction.FactionIndex * 1000000;
                sidesByOwner.Add(owners[side], side);
            }
            if (factionDefinitions[0].FactionIndex == factionDefinitions[1].FactionIndex)
                throw new InvalidDataException(
                    "Opposing factions require distinct tactical registration indexes."
                );
            CapitalShip construction = GetConstructionStation(planet);
            int constructionSide =
                construction == null ? -1 : Array.IndexOf(owners, construction.OwnerInstanceID);
            int constructionIndex = 0;
            foreach (Fleet fleet in planet.GetChildren<Fleet>())
            {
                if (
                    fleet.Movement != null
                    || !sidesByOwner.TryGetValue(fleet.OwnerInstanceID, out int side)
                )
                    continue;
                CapitalShip[] ships = SpaceCombatQueries
                    .GetActiveCapitalShips(fleet)
                    .OrderBy(ship => ship.CanDestroyPlanets)
                    .ToArray();
                foreach (CapitalShip ship in ships)
                {
                    TacticalCombatUnit unit = new(
                        ship,
                        (CombatSide)side,
                        ++counters[side],
                        RequireDefinition(ship.TypeID)
                    );
                    units.Add(unit);
                    byID.Add(unit.InstanceID, unit);
                }
                if (constructionSide >= 0)
                    constructionIndex = ++counters[constructionSide];
                foreach (CapitalShip ship in ships)
                {
                    foreach (
                        Starfighter fighter in ship.GetChildren<Starfighter>()
                            .Where(SpaceCombatQueries.IsActiveStarfighter)
                    )
                    {
                        int fighterSide = GetRegistrationSide(
                            fighter.OwnerInstanceID,
                            sidesByOwner
                        );
                        TacticalCombatUnit unit = new(
                            fighter,
                            (CombatSide)fighterSide,
                            ++counters[fighterSide],
                            RequireDefinition(fighter.TypeID)
                        );
                        units.Add(unit);
                        byID.Add(unit.InstanceID, unit);
                    }
                    foreach (Officer officer in ship.GetChildren<Officer>())
                    {
                        if (((IMovable)officer).GetTransitMovement() != null)
                            continue;
                        counters[GetRegistrationSide(officer.OwnerInstanceID, sidesByOwner)]++;
                        personnelHostInstanceIDs.Add(ship.InstanceID);
                    }
                    foreach (
                        ISceneNode payload in ship.GetChildren<SpecialForces>()
                            .Cast<ISceneNode>()
                            .Concat(ship.GetChildren<Regiment>())
                    )
                        counters[GetRegistrationSide(payload.OwnerInstanceID, sidesByOwner)]++;
                }
            }
            foreach (
                Starfighter fighter in SpaceCombatQueries.GetActivePlanetStarfighters(planet, null)
            )
            {
                int side = GetRegistrationSide(fighter.OwnerInstanceID, sidesByOwner);
                TacticalCombatUnit unit = new(
                    fighter,
                    (CombatSide)side,
                    ++counters[side],
                    RequireDefinition(fighter.TypeID)
                );
                units.Add(unit);
                byID.Add(unit.InstanceID, unit);
            }
            if (constructionIndex != 0)
            {
                TacticalCombatUnit station = new(
                    construction,
                    (CombatSide)constructionSide,
                    constructionIndex,
                    RequireDefinition(construction.TypeID)
                );
                units.Add(station);
                byID.Add(station.InstanceID, station);
            }
            units.Sort(CompareUnitRegistration);
            int[] deployedCounts = new int[2];
            foreach (TacticalCombatUnit unit in units)
                if (!unit.IsBattleStation && unit.Status == TacticalCombatUnitStatus.Active)
                    deployedCounts[(int)unit.Side]++;
            float depth = 100 + 3 * Math.Max(deployedCounts[0], deployedCounts[1]);
            List<TacticalCombatGroup> groups = new();
            TacticalCombatSide[] sides = new TacticalCombatSide[2];
            CombatSide playerSide =
                owners[0] == _game.Summary.PlayerFactionID
                    ? CombatSide.Attacker
                    : CombatSide.Defender;
            for (int side = 0; side < 2; side++)
            {
                Vector3 forward = new(0, 0, factionDefinitions[side].ForwardZ);
                Deploy(units, (CombatSide)side, forward, depth);
                (Officer admiral, Officer commander) = SpaceCombatCommands.GetCommandOfficers(
                    fleets[side],
                    planet,
                    owners[side]
                );
                SpaceCombatWithdrawalPlan withdrawal = _queries.GetWithdrawalPlan(
                    fleets[side],
                    planet,
                    owners[side]
                );
                string destinationID = withdrawal.GetRetreatPlanetInstanceID();
                Planet destination =
                    destinationID == null
                        ? null
                        : _game.GetSceneNodeByInstanceID<Planet>(destinationID);
                int distance = destination == null ? 0 : (int)planet.GetRawDistanceTo(destination);
                int departureSeconds =
                    distance == 0
                        ? 0
                        : Math.Max(
                            (int)(
                                (int)(distance / _game.Config.Movement.DistanceDivisor)
                                * (long)_definition.WithdrawalDistancePercent
                                / 100
                            ),
                            1
                        );
                sides[side] = new TacticalCombatSide(
                    (CombatSide)side,
                    factionDefinitions[side].FactionIndex,
                    forward,
                    withdrawal.CanWithdraw,
                    departureSeconds * 1000,
                    GetRetreatRatio(fleets, side, planet, owners[side]),
                    FindOfficerHost(admiral, byID),
                    FindOfficerHost(commander, byID),
                    admiral?.GetEffectiveRating(SkillRating.Leadership) ?? 0,
                    commander?.GetEffectiveRating(SkillRating.Combat) ?? 0,
                    (CombatSide)side != playerSide
                );
                foreach (TacticalCombatUnit unit in units)
                    if ((int)unit.Side == side)
                        unit.CommandBudget = sides[side].GetCommandBudget(unit.IsFighter);
                CreateGroups(units, groups, (CombatSide)side, forward, ref counters[side]);
            }
            groups.Sort(CompareGroupRegistration);
            TacticalCombatRandom random = _createRandom();
            return new TacticalCombatSession(
                units,
                groups,
                sides[0],
                sides[1],
                playerSide,
                depth,
                random,
                _definition,
                _definition.Environments.FirstOrDefault(environment =>
                    environment.PlanetIconPath == planet.PlanetIconPath
                ),
                new TacticalCombatSceneryRandom(
                    random,
                    () => TacticalCombatRandom.GetCalendarSeed(_localClock())
                ),
                personnelHostInstanceIDs: personnelHostInstanceIDs,
                enteringSide: factionDefinitions[0].FactionIndex
                == (planet.SpaceCombatEntryFaction == 1 ? 0 : 1)
                    ? CombatSide.Attacker
                    : CombatSide.Defender
            );
        }

        /// <summary>
        /// Resolves a participant's faction independently of the carrier that currently hosts it.
        /// </summary>
        /// <param name="ownerID">The unit's owning faction.</param>
        /// <param name="sidesByOwner">The two factions captured for this encounter.</param>
        /// <returns>The owning side's encounter index.</returns>
        private static int GetRegistrationSide(
            string ownerID,
            IReadOnlyDictionary<string, int> sidesByOwner
        )
        {
            if (ownerID != null && sidesByOwner.TryGetValue(ownerID, out int side))
                return side;
            throw new InvalidDataException(
                $"A tactical participant belongs to an unregistered faction '{ownerID}'."
            );
        }

        /// <summary>
        /// Finds the station currently being produced by the system's capital-ship manufacturing lane.
        /// </summary>
        /// <param name="planet">The encounter's system.</param>
        /// <returns>The active incomplete station, or null when the lane is producing another unit.</returns>
        private static CapitalShip GetConstructionStation(Planet planet)
        {
            if (
                planet
                    .GetManufacturingQueue()
                    .TryGetValue(ManufacturingType.Ship, out List<IManufacturable> queue)
                && queue.Count > 0
                && queue[0] is CapitalShip ship
                && ship.CanDestroyPlanets
                && ship.ManufacturingStatus == ManufacturingStatus.Building
            )
                return ship;
            return null;
        }

        /// <summary>
        /// Requires an authored unit definition before any battle resources or campaign state are changed.
        /// </summary>
        /// <param name="typeID">The participating unit type.</param>
        /// <returns>The shared tactical definition.</returns>
        private TacticalCombatUnitDefinition RequireDefinition(string typeID) =>
            _definitions.TryGetValue(typeID, out TacticalCombatUnitDefinition unit)
                ? unit
                : throw new InvalidDataException(
                    $"Tactical combat definition is missing for unit '{typeID}'."
                );

        /// <summary>
        /// Finds the captured active carrier that hosts a selected command officer.
        /// </summary>
        /// <param name="officer">The selected command officer.</param>
        /// <param name="units">The captured unit index.</param>
        /// <returns>The hosting unit, or null for a ground-based or absent officer.</returns>
        private static TacticalCombatUnit FindOfficerHost(
            Officer officer,
            IReadOnlyDictionary<string, TacticalCombatUnit> units
        )
        {
            string carrierID = officer?.GetParentOfType<CapitalShip>()?.InstanceID;
            return carrierID != null && units.TryGetValue(carrierID, out TacticalCombatUnit carrier)
                ? carrier
                : null;
        }

        /// <summary>
        /// Assigns alternating entry positions to capital ships and active fighter squadrons.
        /// </summary>
        /// <param name="units">The ordered battle roster.</param>
        /// <param name="side">The side being deployed.</param>
        /// <param name="forward">The authored initial heading.</param>
        /// <param name="depth">The battle's deployment span.</param>
        private static void Deploy(
            IReadOnlyList<TacticalCombatUnit> units,
            CombatSide side,
            Vector3 forward,
            float depth
        )
        {
            int capitalIndex = 0;
            int fighterIndex = 0;
            foreach (TacticalCombatUnit unit in units)
            {
                if (unit.Side != side || unit.IsBattleStation)
                    continue;
                int index = unit.IsFighter ? fighterIndex++ : capitalIndex++;
                float x = index == 0 ? 0 : ((index + 1) / 2) * 5 * (index % 2 == 1 ? 1 : -1);
                unit.Position = new Vector3(
                    x,
                    0,
                    forward.Z * (-depth * 0.5f + (unit.IsFighter ? 20 : 0))
                );
                unit.Heading = forward;
                unit.DesiredHeading = forward;
            }
        }

        /// <summary>
        /// Creates deployed fighter categories before partitioning the side's command groups.
        /// </summary>
        /// <param name="units">The captured battle roster in registration order.</param>
        /// <param name="groups">The groups receiving the new deployment.</param>
        /// <param name="side">The deploying side.</param>
        /// <param name="forward">The side's heading.</param>
        /// <param name="counter">The side's shared registration counter.</param>
        private static void CreateGroups(
            IReadOnlyList<TacticalCombatUnit> units,
            List<TacticalCombatGroup> groups,
            CombatSide side,
            Vector3 forward,
            ref int counter
        )
        {
            List<TacticalCombatUnit>[] fighters = { new(), new(), new(), new() };
            List<TacticalCombatDeployment>[] capitals = { new(), new(), new() };
            foreach (TacticalCombatUnit unit in units)
            {
                if (unit.Side != side || unit.IsBattleStation)
                    continue;
                if (unit.IsFighter)
                {
                    int category = unit.Definition.FighterGroup;
                    if (category is < 0 or > 3)
                        throw new InvalidDataException(
                            $"Fighter group category is invalid for '{unit.TypeID}'."
                        );
                    fighters[category].Add(unit);
                }
                else
                {
                    TacticalCombatDeployment deployment = new(unit);
                    capitals[deployment.Category].Add(deployment);
                }
            }

            CreateFighterGroups(fighters, groups, forward, ref counter);
            foreach (List<TacticalCombatDeployment> partition in capitals)
                partition.Sort(TacticalCombatDeployment.Compare);
            int firstCount = CreateCapitalGroups(capitals[0], groups, 0, 3, forward, ref counter);
            int secondCount = CreateCapitalGroups(
                capitals[1],
                groups,
                firstCount,
                firstCount + 3,
                forward,
                ref counter
            );
            CreateCapitalGroups(
                capitals[2],
                groups,
                firstCount + secondCount,
                7,
                forward,
                ref counter
            );
        }

        /// <summary>
        /// Registers populated fighter categories before splitting their leading halves into empty categories.
        /// </summary>
        /// <param name="fighters">The four authored fighter buckets in registration order.</param>
        /// <param name="groups">The battle groups receiving newly registered categories.</param>
        /// <param name="forward">The side's heading.</param>
        /// <param name="counter">The side's shared registration counter.</param>
        private static void CreateFighterGroups(
            IReadOnlyList<List<TacticalCombatUnit>> fighters,
            List<TacticalCombatGroup> groups,
            Vector3 forward,
            ref int counter
        )
        {
            TacticalCombatGroup[] deployed = new TacticalCombatGroup[4];
            for (int category = 0; category < deployed.Length; category++)
                if (fighters[category].Count > 0)
                {
                    deployed[category] = new TacticalCombatGroup(
                        ++counter,
                        fighters[category],
                        forward
                    )
                    {
                        FighterCategory = category,
                    };
                    groups.Add(deployed[category]);
                }
            for (int category = 0; category < deployed.Length; category++)
            {
                if (deployed[category] != null)
                    continue;
                int largest = 0;
                for (int candidate = 1; candidate < deployed.Length; candidate++)
                    if (
                        (deployed[candidate]?.Members.Count ?? 0)
                        > (deployed[largest]?.Members.Count ?? 0)
                    )
                        largest = candidate;
                TacticalCombatGroup source = deployed[largest];
                int count = source?.Members.Count ?? 0;
                if (count < 8)
                    continue;
                List<TacticalCombatUnit> moved = new(count / 2);
                for (int index = 0; index < count / 2; index++)
                    moved.Add(source.Members[index]);
                foreach (TacticalCombatUnit unit in moved)
                {
                    source.RemoveRequestedMember(unit);
                    source.RemoveMember(unit);
                }
                deployed[category] = new TacticalCombatGroup(++counter, moved, forward)
                {
                    FighterCategory = category,
                };
                groups.Add(deployed[category]);
            }
        }

        /// <summary>
        /// Partitions one scored capital roster across its available command slots and aligns each addition.
        /// </summary>
        /// <param name="ships">The scored capital ships in deployment order.</param>
        /// <param name="groups">The battle groups receiving the partition.</param>
        /// <param name="lower">The first available command slot.</param>
        /// <param name="upper">The final available command slot.</param>
        /// <param name="forward">The side's heading.</param>
        /// <param name="counter">The side's shared registration counter.</param>
        /// <returns>The number of command groups created.</returns>
        private static int CreateCapitalGroups(
            IReadOnlyList<TacticalCombatDeployment> ships,
            List<TacticalCombatGroup> groups,
            int lower,
            int upper,
            Vector3 forward,
            ref int counter
        )
        {
            int count = ships.Count & 0xffff;
            int limit = 1;
            while (limit <= upper && limit * limit <= count)
                limit++;
            int selected = count <= 1 ? 1 : limit - 1;
            int available = upper - lower + 1;
            if (selected >= available)
                selected = available;
            int spacing = count / selected;
            int created = 0;
            TacticalCombatGroup group = null;
            for (int index = 0; index < ships.Count; index++)
            {
                TacticalCombatUnit unit = ships[index].Unit;
                if (index % spacing == 0 && created < selected)
                {
                    int slot = lower + created++;
                    float y = slot switch
                    {
                        0 => 0,
                        1 => 8,
                        2 => -8,
                        3 => 16,
                        4 => -16,
                        5 => 24,
                        6 => -24,
                        _ => 32,
                    };
                    unit.Position = new Vector3(0, y, unit.Position.Z);
                    group = new TacticalCombatGroup(++counter, new[] { unit }, forward);
                    groups.Add(group);
                }
                else
                {
                    group.AddRequestedMember(unit);
                    group.AddMember(unit);
                }
                AlignCapitalGroup(group);
            }
            return ships.Count == 0 ? 0 : selected;
        }

        /// <summary>
        /// Applies the marker's stored member offsets after each capital joins its deployment group.
        /// </summary>
        /// <param name="group">The active capital group being aligned.</param>
        private static void AlignCapitalGroup(TacticalCombatGroup group)
        {
            foreach (TacticalCombatUnit unit in group.Members)
            {
                Vector3 position = unit.Position;
                Vector3 offset = group.Formation.GetMemberMovement(unit);
                unit.Position = new Vector3(
                    (float)((double)position.X + offset.X),
                    (float)((double)position.Y + offset.Y),
                    (float)((double)position.Z + offset.Z)
                );
            }
        }

        /// <summary>
        /// Calculates the strategic retreat threshold for a defended friendly system.
        /// </summary>
        /// <param name="fleets">Both sides' participating fleets.</param>
        /// <param name="side">The side being assessed.</param>
        /// <param name="planet">The battle system.</param>
        /// <param name="ownerID">The assessed faction identifier.</param>
        /// <returns>The strategic controller's threshold, bounded above by nine tenths.</returns>
        private float GetRetreatRatio(
            IReadOnlyList<Fleet>[] fleets,
            int side,
            Planet planet,
            string ownerID
        )
        {
            if (
                planet.OwnerInstanceID != ownerID
                || !SpaceCombatQueries.GetActivePlanetStarfighters(planet, ownerID).Any()
            )
                return 0.9f;
            if (
                planet.IsHeadquarters
                    && _game.GetFactionByOwnerInstanceID(ownerID)?.Settings.Headquarters.IsMobile
                        == true
                || _definition.CriticalPlanetInstanceIDs.Contains(planet.InstanceID)
            )
                return 0;
            int own = GetStrategicFleetAttack(fleets[side]);
            int opposing = GetStrategicFleetAttack(fleets[side == 0 ? 1 : 0]);
            return Math.Min(opposing > 0 ? (float)own / opposing : own, 0.9f);
        }

        /// <summary>
        /// Calculates the entry fleet weapon assessment used by strategic retreat selection.
        /// </summary>
        /// <param name="fleets">The assessed fleets.</param>
        /// <returns>The summed integer attack assessment.</returns>
        private static int GetStrategicFleetAttack(IReadOnlyList<Fleet> fleets)
        {
            int total = 0;
            foreach (Fleet fleet in fleets)
            foreach (CapitalShip ship in SpaceCombatQueries.GetActiveCapitalShips(fleet))
            {
                if (!ship.CanDestroyPlanets)
                {
                    int bestScore = 0;
                    int bestStrength = 0;
                    for (int arc = 0; arc < 4; arc++)
                    {
                        int turbo = ship.PrimaryWeapons[PrimaryWeaponType.Turbolaser][arc];
                        int ion = ship.PrimaryWeapons[PrimaryWeaponType.IonCannon][arc];
                        int laser = ship.PrimaryWeapons[PrimaryWeaponType.LaserCannon][arc];
                        int score = (turbo + ion) * 100 + laser * 100 / 6;
                        if (score > bestScore)
                        {
                            bestScore = score;
                            bestStrength = turbo + ion + laser;
                        }
                    }
                    if (bestStrength > 0 && ship.WeaponRecharge > 0)
                    {
                        int baseScore = ship.WeaponRecharge * bestScore / bestStrength;
                        int damage = ship.WeaponRecharge * (ship.SystemDamage?.Weapons ?? 0) / 4;
                        total = unchecked(
                            total + baseScore * (ship.WeaponRecharge - damage) / ship.WeaponRecharge
                        );
                    }
                }
                foreach (
                    Starfighter fighter in ship.GetChildren<Starfighter>()
                        .Where(SpaceCombatQueries.IsActiveStarfighter)
                )
                {
                    int strength = fighter.IonCannon + fighter.LaserCannon;
                    if (strength == 0)
                        continue;
                    int score = fighter.IonCannon * 100 + fighter.LaserCannon * 100 / 6;
                    int full = score * fighter.MaxSquadronSize / strength;
                    total = unchecked(
                        total + full * fighter.CurrentSquadronSize / fighter.MaxSquadronSize
                    );
                }
            }
            return total;
        }

        /// <summary>
        /// Orders captured units by their unique shared battle registration identifier.
        /// </summary>
        /// <param name="first">The first unit.</param>
        /// <param name="second">The second unit.</param>
        /// <returns>The registration comparison.</returns>
        private static int CompareUnitRegistration(
            TacticalCombatUnit first,
            TacticalCombatUnit second
        ) => first.Index.CompareTo(second.Index);

        /// <summary>
        /// Orders groups by their shared battle registration identifier.
        /// </summary>
        /// <param name="first">The first group.</param>
        /// <param name="second">The second group.</param>
        /// <returns>The registration comparison.</returns>
        private static int CompareGroupRegistration(
            TacticalCombatGroup first,
            TacticalCombatGroup second
        ) => first.ID.CompareTo(second.ID);
    }
}
