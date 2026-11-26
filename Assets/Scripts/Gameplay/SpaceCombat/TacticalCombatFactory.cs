using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using Rebellion.Content;
using Rebellion.Game;
using Rebellion.Game.Combat;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Results;
using Rebellion.Game.Units;
using Rebellion.SceneGraph;
using Rebellion.Tactical;
using Rebellion.Util.Random;

namespace Rebellion.Gameplay
{
    /// <summary>
    /// Captures campaign rosters, command officers, and departure routes for isolated tactical encounters.
    /// </summary>
    public sealed class TacticalCombatFactory
    {
        private readonly GameRoot _game;
        private readonly SpaceCombatQueries _queries;
        private readonly Action<TacticalCombatSession> _attachAutomation;
        private readonly Func<LinearCongruentialRandomProvider> _createRandom;
        private readonly Func<DateTimeOffset> _localClock;
        private readonly TacticalCombatDefinition _definition;
        private readonly Dictionary<string, TacticalCombatUnitDefinition> _definitions;
        private readonly Dictionary<string, TacticalCombatFactionDefinition> _factions;

        /// <summary>
        /// Creates the battle-entry boundary for the active campaign and tactical definitions.
        /// </summary>
        /// <param name="game">The active campaign graph.</param>
        /// <param name="definition">The selected tactical combat definitions.</param>
        /// <param name="queries">The campaign encounter and departure queries.</param>
        /// <param name="attachAutomation">Attaches the configured tactical AI after the encounter is constructed.</param>
        /// <param name="createRandom">An optional replay stream factory; live battles capture the entry clock.</param>
        /// <param name="localClock">The platform's local clock, acquired only when scenery first needs a random position.</param>
        public TacticalCombatFactory(
            GameRoot game,
            TacticalCombatDefinition definition,
            SpaceCombatQueries queries,
            Action<TacticalCombatSession> attachAutomation,
            Func<LinearCongruentialRandomProvider> createRandom = null,
            Func<DateTimeOffset> localClock = null
        )
        {
            this._game = game ?? throw new ArgumentNullException(nameof(game));
            _attachAutomation =
                attachAutomation ?? throw new ArgumentNullException(nameof(attachAutomation));
            _createRandom =
                createRandom
                ?? (
                    () =>
                        new LinearCongruentialRandomProvider(unchecked((uint)Environment.TickCount))
                );
            _localClock = localClock ?? (() => DateTimeOffset.Now);
            this._queries = queries ?? throw new ArgumentNullException(nameof(queries));
            _definition = definition ?? throw new ArgumentNullException(nameof(definition));
            _definitions = _definition.Units.ToDictionary(
                unit => unit.UnitTypeID,
                StringComparer.Ordinal
            );
            _factions = _definition.Factions.ToDictionary(
                faction => faction.FactionInstanceID,
                StringComparer.Ordinal
            );
        }

        internal sealed class EncounterRoster
        {
            internal Planet Planet { get; }
            internal EncounterSide Attacker { get; }
            internal EncounterSide Defender { get; }
            internal IReadOnlyList<(
                BaseSceneNode Source,
                CombatSide Side,
                int Index,
                TacticalCombatUnitDefinition Definition
            )> Units { get; }
            internal IReadOnlyList<string> PersonnelHostIDs { get; }

            /// <summary>
            /// Captures the admitted encounter before any units are deployed.
            /// </summary>
            /// <param name="planet">The campaign planet where the battle takes place.</param>
            /// <param name="attacker">The force that entered as the attacker.</param>
            /// <param name="defender">The force that entered as the defender.</param>
            /// <param name="units">The participants and their assigned registration indexes.</param>
            /// <param name="personnelHostIDs">The ships carrying available officers.</param>
            internal EncounterRoster(
                Planet planet,
                EncounterSide attacker,
                EncounterSide defender,
                IReadOnlyList<(
                    BaseSceneNode Source,
                    CombatSide Side,
                    int Index,
                    TacticalCombatUnitDefinition Definition
                )> units,
                IReadOnlyList<string> personnelHostIDs
            )
            {
                Planet = planet;
                Attacker = attacker;
                Defender = defender;
                Units = units;
                PersonnelHostIDs = personnelHostIDs;
            }
        }

        internal sealed class EncounterSide
        {
            internal CombatSide Side { get; }
            internal TacticalCombatFactionDefinition Faction { get; }
            internal IReadOnlyList<Fleet> Fleets { get; }
            internal string OwnerID => Faction.FactionInstanceID;
            internal int LastRegistrationIndex { get; private set; }

            /// <summary>
            /// Captures one faction and starts its tactical registration range.
            /// </summary>
            /// <param name="side">The side identified by the encounter.</param>
            /// <param name="faction">The faction providing the side's registration range.</param>
            /// <param name="fleets">The campaign fleets participating on this side.</param>
            internal EncounterSide(
                CombatSide side,
                TacticalCombatFactionDefinition faction,
                IReadOnlyList<Fleet> fleets
            )
            {
                Side = side;
                Faction = faction;
                Fleets = fleets;
                LastRegistrationIndex = faction.FactionIndex * 1000000;
            }

            /// <summary>
            /// Reserves the next index in this faction's registration range.
            /// </summary>
            /// <returns>The reserved tactical index.</returns>
            internal int ReserveRegistrationIndex() => ++LastRegistrationIndex;
        }

        /// <summary>
        /// Checks admission against the same roster used by deployment.
        /// </summary>
        /// <param name="decision">The pending campaign encounter.</param>
        /// <returns>Whether all required participants and tactical data are available.</returns>
        internal bool Supports(SpaceCombatDecision decision) => TryPrepare(decision, out _);

        /// <summary>
        /// Checks whether a pending campaign decision has a deployable roster.
        /// </summary>
        /// <param name="decision">The pending campaign encounter to prepare.</param>
        /// <param name="roster">The validated roster when the pending decision admits a battle.</param>
        /// <returns>Whether the decision and all its tactical data passed admission.</returns>
        internal bool TryPrepare(SpaceCombatDecision decision, out EncounterRoster roster)
        {
            roster = null;
            if (decision == null)
                return false;
            try
            {
                roster = Prepare(decision);
                return true;
            }
            catch (InvalidDataException)
            {
                return false;
            }
            catch (InvalidOperationException)
            {
                return false;
            }
        }

        /// <summary>
        /// Captures the pending encounter without changing campaign units or clearing its pending decision.
        /// </summary>
        /// <param name="decision">The pending campaign encounter.</param>
        /// <returns>The isolated deployed tactical session.</returns>
        internal TacticalCombatSession Create(SpaceCombatDecision decision) =>
            Create(Prepare(decision));

        /// <summary>
        /// Deploys an admitted roster for the selected local or network battle.
        /// </summary>
        /// <param name="roster">The validated roster captured for this encounter.</param>
        /// <returns>The isolated tactical session.</returns>
        internal TacticalCombatSession Create(EncounterRoster roster) =>
            CreateCore(roster, null, null, null);

        /// <summary>
        /// Captures a shared battle with explicit random inputs and the peer's independent local faction view.
        /// </summary>
        /// <param name="decision">The mutually admitted campaign encounter.</param>
        /// <param name="seed">The authoritative battle random seed.</param>
        /// <param name="scenerySeed">The captured scenery calendar seed.</param>
        /// <param name="isReplica">Whether this peer receives passive authoritative frames.</param>
        /// <returns>The configured authoritative encounter or passive local view.</returns>
        internal TacticalCombatSession Create(
            SpaceCombatDecision decision,
            uint seed,
            uint scenerySeed,
            bool isReplica
        ) => Create(Prepare(decision), seed, scenerySeed, isReplica);

        /// <summary>
        /// Deploys an admitted roster for the selected local or network battle.
        /// </summary>
        /// <param name="roster">The validated roster captured for this encounter.</param>
        /// <param name="seed">The authoritative simulation seed.</param>
        /// <param name="scenerySeed">The separately captured presentation seed.</param>
        /// <param name="isReplica">Whether the session is a passive view of the host battle.</param>
        /// <returns>The isolated tactical session.</returns>
        internal TacticalCombatSession Create(
            EncounterRoster roster,
            uint seed,
            uint scenerySeed,
            bool isReplica
        )
        {
            if (string.IsNullOrWhiteSpace(_game.LocalPlayerFactionID))
                throw new InvalidOperationException(
                    "Network combat requires an admitted local faction."
                );
            TacticalCombatSession session = CreateCore(
                roster,
                seed,
                scenerySeed,
                _game.LocalPlayerFactionID
            );
            if (isReplica)
                session.ConfigureSynchronizedReplica();
            else
                session.ConfigureSynchronizedAuthority();
            return session;
        }

        /// <summary>
        /// Resolves campaign participants in the order used for tactical registration.
        /// </summary>
        /// <param name="decision">The pending campaign encounter to prepare.</param>
        /// <returns>The roster and its resolved authored data.</returns>
        private EncounterRoster Prepare(SpaceCombatDecision decision)
        {
            Planet planet =
                _queries.ResolveCombatPlanet(decision)
                ?? throw new InvalidOperationException("The combat system is unavailable.");
            EncounterSide attacker = CaptureSide(
                CombatSide.Attacker,
                decision.AttackerOwnerInstanceID,
                decision.AttackerFleetInstanceIDs
            );
            EncounterSide defender = CaptureSide(
                CombatSide.Defender,
                decision.DefenderOwnerInstanceID,
                decision.DefenderFleetInstanceIDs
            );
            Dictionary<string, EncounterSide> sidesByOwner = new(StringComparer.Ordinal)
            {
                { attacker.OwnerID, attacker },
                { defender.OwnerID, defender },
            };
            if (attacker.Faction.FactionIndex == defender.Faction.FactionIndex)
                throw new InvalidDataException(
                    "Opposing factions require distinct tactical registration indexes."
                );
            List<string> personnelHostInstanceIDs = new();
            List<(
                BaseSceneNode Source,
                CombatSide Side,
                int Index,
                TacticalCombatUnitDefinition Definition
            )> units = new();
            CapitalShip construction = GetConstructionStation(planet);
            EncounterSide constructionSide = null;
            if (construction?.OwnerInstanceID != null)
                sidesByOwner.TryGetValue(construction.OwnerInstanceID, out constructionSide);
            int constructionIndex = 0;
            foreach (Fleet fleet in planet.GetChildren<Fleet>())
            {
                if (
                    fleet.Movement != null
                    || !sidesByOwner.TryGetValue(fleet.OwnerInstanceID, out EncounterSide fleetSide)
                )
                    continue;
                CapitalShip[] ships = SpaceCombatQueries
                    .GetActiveCapitalShips(fleet)
                    .OrderBy(ship => ship.CanDestroyPlanets)
                    .ToArray();
                foreach (CapitalShip ship in ships)
                    RegisterUnit(ship, fleetSide, fleetSide.ReserveRegistrationIndex());
                if (constructionSide != null)
                    constructionIndex = constructionSide.ReserveRegistrationIndex();
                foreach (CapitalShip ship in ships)
                {
                    foreach (
                        Starfighter fighter in ship.GetChildren<Starfighter>()
                            .Where(SpaceCombatQueries.IsActiveStarfighter)
                    )
                    {
                        EncounterSide fighterSide = GetFactionSide(fighter.OwnerInstanceID);
                        RegisterUnit(fighter, fighterSide, fighterSide.ReserveRegistrationIndex());
                    }
                    foreach (Officer officer in ship.GetChildren<Officer>())
                    {
                        if (((IMovable)officer).GetTransitMovement() != null)
                            continue;
                        GetFactionSide(officer.OwnerInstanceID).ReserveRegistrationIndex();
                        personnelHostInstanceIDs.Add(ship.InstanceID);
                    }
                    foreach (
                        ISceneNode payload in ship.GetChildren<SpecialForces>()
                            .Cast<ISceneNode>()
                            .Concat(ship.GetChildren<Regiment>())
                    )
                        GetFactionSide(payload.OwnerInstanceID).ReserveRegistrationIndex();
                }
            }
            foreach (
                Starfighter fighter in SpaceCombatQueries.GetActivePlanetStarfighters(planet, null)
            )
            {
                EncounterSide fighterSide = GetFactionSide(fighter.OwnerInstanceID);
                RegisterUnit(fighter, fighterSide, fighterSide.ReserveRegistrationIndex());
            }
            if (constructionIndex != 0)
                RegisterUnit(construction, constructionSide, constructionIndex);

            return new EncounterRoster(planet, attacker, defender, units, personnelHostInstanceIDs);

            EncounterSide GetFactionSide(string ownerID)
            {
                if (ownerID != null && sidesByOwner.TryGetValue(ownerID, out EncounterSide side))
                    return side;
                throw new InvalidDataException(
                    $"A tactical participant belongs to an unregistered faction '{ownerID}'."
                );
            }

            void RegisterUnit(BaseSceneNode unit, EncounterSide side, int index) =>
                units.Add((unit, side.Side, index, RequireDefinition(unit.TypeID)));
        }

        /// <summary>
        /// Resolves the faction definition and participating campaign fleets for a side.
        /// </summary>
        /// <param name="side">The side identified by the encounter.</param>
        /// <param name="ownerID">The campaign faction's instance ID.</param>
        /// <param name="fleetIDs">The campaign fleet IDs recorded for the force.</param>
        /// <returns>The side with its independent registration range.</returns>
        private EncounterSide CaptureSide(
            CombatSide side,
            string ownerID,
            IReadOnlyList<string> fleetIDs
        )
        {
            if (string.IsNullOrEmpty(ownerID) || !_factions.TryGetValue(ownerID, out var faction))
                throw new InvalidDataException(
                    $"Tactical deployment is missing for faction '{ownerID}'."
                );
            if (faction.FactionIndex is < 0 or > 1 || Math.Abs(faction.ForwardZ) != 1)
                throw new InvalidDataException(
                    $"Tactical deployment is invalid for faction '{ownerID}'."
                );
            return new EncounterSide(side, faction, _queries.GetFleets(fleetIDs));
        }

        /// <summary>
        /// Deploys the validated roster and initializes the isolated battle clock.
        /// </summary>
        /// <param name="roster">The validated participants and admission state.</param>
        /// <param name="seed">The explicit simulation seed, when supplied.</param>
        /// <param name="scenerySeed">The explicit presentation seed, when supplied.</param>
        /// <param name="localFactionID">The peer's locally controlled faction, when supplied.</param>
        /// <returns>The deployed encounter ready for its local view.</returns>
        private TacticalCombatSession CreateCore(
            EncounterRoster roster,
            uint? seed,
            uint? scenerySeed,
            string localFactionID
        )
        {
            Planet planet = roster.Planet;
            ActiveBattle battle = new()
            {
                Kind = BattleKind.Space,
                PlanetInstanceID = planet.InstanceID,
                AttackerOwnerInstanceID = roster.Attacker.OwnerID,
                DefenderOwnerInstanceID = roster.Defender.OwnerID,
            };
            Dictionary<string, CombatUnit> byID = new(StringComparer.Ordinal);
            List<CombatUnit>[] rosters = { new(), new() };
            foreach (var participant in roster.Units)
            {
                CombatUnit unit = battle.AddCombatant(participant.Source);
                if (participant.Source is CapitalShip ship)
                    unit.HasPlanetaryProtection = HasPlanetaryProtection(ship, planet);
                participant.Definition.InitializeCombatant(
                    unit,
                    participant.Side,
                    participant.Index
                );
                rosters[(int)participant.Side].Add(unit);
                byID.Add(unit.InstanceID, unit);
            }

            int maxDeployedCount = 0;
            foreach (List<CombatUnit> sideUnits in rosters)
            {
                sideUnits.Sort(CompareUnitRegistration);
                int deployedCount = sideUnits.Count(unit =>
                    !unit.IsBattleStation && unit.Status == CombatUnitStatus.Active
                );
                maxDeployedCount = Math.Max(maxDeployedCount, deployedCount);
            }
            float depth = 100 + 3 * maxDeployedCount;
            List<TacticalCombatGroup> groups = new();
            TacticalCombatSide[] sides = new TacticalCombatSide[2];
            CombatSide playerSide =
                roster.Attacker.OwnerID == (localFactionID ?? _game.Summary.PlayerFactionID)
                    ? CombatSide.Attacker
                    : CombatSide.Defender;
            for (int side = 0; side < 2; side++)
            {
                EncounterSide entry = side == 0 ? roster.Attacker : roster.Defender;
                EncounterSide opposingEntry = side == 0 ? roster.Defender : roster.Attacker;
                List<CombatUnit> sideUnits = rosters[side];
                Vector3 forward = new(0, 0, entry.Faction.ForwardZ);
                TacticalCombatDeployment.PlaceUnits(sideUnits, forward, depth);
                (Officer admiral, Officer commander) = SpaceCombatCommands.GetCommandOfficers(
                    entry.Fleets,
                    planet,
                    entry.OwnerID
                );
                SpaceCombatWithdrawalPlan withdrawal = _queries.GetWithdrawalPlan(
                    entry.Fleets,
                    planet,
                    entry.OwnerID
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
                    entry.Side,
                    entry.Faction.FactionIndex,
                    forward,
                    withdrawal.CanWithdraw,
                    departureSeconds * 1000,
                    GetRetreatRatio(entry, opposingEntry, planet),
                    FindOfficerHost(admiral, byID),
                    FindOfficerHost(commander, byID),
                    admiral?.GetEffectiveRating(SkillRating.Leadership) ?? 0,
                    commander?.GetEffectiveRating(SkillRating.Combat) ?? 0,
                    entry.Side != playerSide
                );
                foreach (CombatUnit unit in sideUnits)
                    unit.CommandBudget = sides[side].GetCommandBudget(unit.IsFighter);
                int groupIndex = entry.LastRegistrationIndex;
                TacticalCombatDeployment.CreateGroups(
                    sideUnits,
                    groups,
                    forward,
                    groupIndex,
                    unit => _definitions[unit.TypeID].FighterGroup
                );
            }
            groups.Sort(CompareGroupRegistration);
            LinearCongruentialRandomProvider random = seed.HasValue
                ? new LinearCongruentialRandomProvider(seed.Value)
                : _createRandom();
            TacticalCombatSession session = new TacticalCombatSession(
                battle,
                groups,
                sides[0],
                sides[1],
                playerSide,
                depth,
                random,
                _attachAutomation,
                _definition,
                _definition.Environments.FirstOrDefault(environment =>
                    environment.PlanetIconPath == planet.PlanetIconPath
                ),
                new TacticalCombatSceneryRandom(
                    random,
                    () =>
                        scenerySeed.HasValue
                            ? unchecked((int)scenerySeed.Value)
                            : TacticalCombatSeed.FromClock(_localClock())
                ),
                personnelHostInstanceIDs: roster.PersonnelHostIDs
            );
            _game.ActiveBattle = battle;
            return session;
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
        /// Preserves the tactical entry rule that any completed local generator protecting the
        /// station type shields the planet destroyer, independently of campaign blockade ownership.
        /// </summary>
        /// <param name="ship">The planet-destroying ship entering combat.</param>
        /// <param name="planet">The encounter planet whose protection is inspected.</param>
        /// <returns>Whether a completed local generator protects the ship type.</returns>
        private static bool HasPlanetaryProtection(CapitalShip ship, Planet planet)
        {
            if (ship?.CanDestroyPlanets != true)
                return false;
            return planet
                .GetChildren<Building>()
                .Any(facility =>
                    facility.ManufacturingStatus == ManufacturingStatus.Complete
                    && facility.Movement == null
                    && facility.ProtectedUnitTypeIDs?.Contains(ship.TypeID) == true
                );
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
        private static CombatUnit FindOfficerHost(
            Officer officer,
            IReadOnlyDictionary<string, CombatUnit> units
        )
        {
            string carrierID = officer?.GetParentOfType<CapitalShip>()?.InstanceID;
            return carrierID != null && units.TryGetValue(carrierID, out CombatUnit carrier)
                ? carrier
                : null;
        }

        /// <summary>
        /// Calculates the strategic retreat threshold for a defended friendly system.
        /// </summary>
        /// <param name="side">The side being assessed.</param>
        /// <param name="opposingSide">The opposing side whose fleets supply the comparison.</param>
        /// <param name="planet">The battle system.</param>
        /// <returns>The strategic controller's threshold, bounded above by nine tenths.</returns>
        private float GetRetreatRatio(EncounterSide side, EncounterSide opposingSide, Planet planet)
        {
            if (
                planet.OwnerInstanceID != side.OwnerID
                || !SpaceCombatQueries.GetActivePlanetStarfighters(planet, side.OwnerID).Any()
            )
                return 0.9f;
            if (
                planet.IsHeadquarters
                    && _game
                        .GetFactionByOwnerInstanceID(side.OwnerID)
                        ?.Settings.Headquarters.IsMobile == true
                || _definition.CriticalPlanetInstanceIDs.Contains(planet.InstanceID)
            )
                return 0;
            int own = GetStrategicFleetAttack(side.Fleets);
            int opposing = GetStrategicFleetAttack(opposingSide.Fleets);
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
                        int turbo = ship.GetPrimaryWeaponStrength(
                            PrimaryWeaponType.Turbolaser,
                            (PrimaryWeaponArc)arc
                        );
                        int ion = ship.GetPrimaryWeaponStrength(
                            PrimaryWeaponType.IonCannon,
                            (PrimaryWeaponArc)arc
                        );
                        int laser = ship.GetPrimaryWeaponStrength(
                            PrimaryWeaponType.LaserCannon,
                            (PrimaryWeaponArc)arc
                        );
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
        private static int CompareUnitRegistration(CombatUnit first, CombatUnit second) =>
            first.Index.CompareTo(second.Index);

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
