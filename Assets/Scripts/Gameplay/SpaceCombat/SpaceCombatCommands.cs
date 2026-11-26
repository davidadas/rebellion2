using System;
using System.Collections.Generic;
using System.Linq;
using Rebellion.Game;
using Rebellion.Game.Combat;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Results;
using Rebellion.Game.Units;
using Rebellion.SceneGraph;
using Rebellion.Tactical;
using Rebellion.Util.Logging;
using InvalidDataException = global::System.IO.InvalidDataException;

namespace Rebellion.Gameplay
{
    /// <summary>
    /// Identifies a fleet encounter waiting for space-combat resolution.
    /// </summary>
    internal sealed class SpaceCombatDecision
    {
        public List<string> AttackerFleetInstanceIDs { get; set; } = new List<string>();
        public List<string> DefenderFleetInstanceIDs { get; set; } = new List<string>();
        public string AttackerOwnerInstanceID { get; set; }
        public string DefenderOwnerInstanceID { get; set; }
        public string PlanetInstanceID { get; set; }
        public bool? AttackerRetreatResponse { get; set; }
        public bool? DefenderRetreatResponse { get; set; }
        public bool AttackerTakeCommand { get; set; }
        public bool DefenderTakeCommand { get; set; }
        public string TacticalBattleID { get; set; }
        public uint TacticalSeed { get; set; }
        public int TacticalScenerySeed { get; set; }
    }

    /// <summary>
    /// Detects and resolves hostile fleet encounters.
    /// </summary>
    public partial class SpaceCombatCommands
    {
        private readonly GameRoot _game;
        private readonly SpaceCombatQueries _queries;
        private readonly MovementCommands _movement;
        private readonly BlockadeTransitionResolver _blockadeTransitionResolver;
        private readonly SpaceCombatAutoResolver _autoResolver;
        private readonly SpaceCombatWithdrawalResolver _withdrawalResolver;
        private SpaceCombatDecision _pendingDecision;
        private readonly TacticalCombatFactory _tacticalFactory;
        private readonly SpaceCombatObserver _combatObserver;
        private TacticalCombatSession _tacticalCombat;
        private string _tacticalBattleID;
        private List<CombatUnitSnapshot> _tacticalAttackingUnits;
        private List<CombatUnitSnapshot> _tacticalDefendingUnits;

        /// <summary>
        /// Whether a player-involved combat encounter is waiting for resolution.
        /// </summary>
        public bool HasPendingDecision => _pendingDecision != null;

        /// <summary>
        /// Gets the captured manual encounter while the campaign decision remains suspended.
        /// </summary>
        public TacticalCombatSession ActiveTacticalCombat => _tacticalCombat;

        /// <summary>
        /// Gets the identity of the active network battle, or null outside network combat.
        /// </summary>
        public string ActiveTacticalBattleID => _tacticalBattleID;

        /// <summary>
        /// Gets whether the waiting encounter has complete tactical deployment definitions.
        /// </summary>
        public bool CanTakeCommand =>
            _pendingDecision != null && _tacticalFactory?.Supports(_pendingDecision) == true;

        /// <summary>
        /// Captures an interactive battle while keeping campaign time and its pending decision suspended.
        /// </summary>
        /// <returns>The deployed tactical encounter.</returns>
        public TacticalCombatSession BeginTacticalCombat()
        {
            if (_tacticalCombat != null)
                throw new InvalidOperationException("A tactical encounter is already active.");
            if (
                _tacticalFactory == null
                || !_tacticalFactory.TryPrepare(_pendingDecision, out var roster)
            )
                throw new InvalidOperationException(
                    "Tactical deployment is unavailable for this encounter."
                );
            Planet planet = roster.Planet;
            _combatObserver?.ValidateBattle(planet);
            _tacticalCombat = _tacticalFactory.Create(roster);
            CaptureTacticalReport(roster);
            return _tacticalCombat;
        }

        /// <summary>
        /// Captures an ordered multiplayer encounter with host-issued entry seeds and identity.
        /// </summary>
        /// <param name="battleID">The host-issued identity for this encounter.</param>
        /// <param name="seed">The authoritative battle random seed.</param>
        /// <param name="scenerySeed">The authoritative scenery calendar seed.</param>
        /// <returns>The authority-owned encounter or its local participant's passive projection.</returns>
        public TacticalCombatSession BeginNetworkTacticalCombat(
            string battleID,
            uint seed,
            int scenerySeed
        )
        {
            if (!Guid.TryParseExact(battleID, "N", out _))
                throw new ArgumentException(
                    "A valid battle identity is required.",
                    nameof(battleID)
                );
            if (_game.Metadata?.IsMultiplayer != true)
                throw new InvalidOperationException("A multiplayer campaign is required.");
            if (
                _tacticalCombat != null
                || _tacticalFactory == null
                || !_tacticalFactory.TryPrepare(_pendingDecision, out var roster)
                || _pendingDecision.AttackerRetreatResponse != false
                || _pendingDecision.DefenderRetreatResponse != false
                || !_pendingDecision.AttackerTakeCommand && !_pendingDecision.DefenderTakeCommand
            )
                throw new InvalidOperationException("The encounter cannot enter tactical combat.");

            Planet planet = roster.Planet;
            _combatObserver?.ValidateBattle(planet);
            _tacticalCombat = _tacticalFactory.Create(
                roster,
                seed,
                unchecked((uint)scenerySeed),
                _game.LocalPlayerFactionID != _game.Metadata.MultiplayerCampaign.HostFactionID
            );
            CaptureTacticalReport(roster);
            if (_game.LocalPlayerFactionID == _game.Metadata.MultiplayerCampaign.HostFactionID)
            {
                _tacticalCombat.SetAutomated(
                    CombatSide.Attacker,
                    !_pendingDecision.AttackerTakeCommand
                );
                _tacticalCombat.SetAutomated(
                    CombatSide.Defender,
                    !_pendingDecision.DefenderTakeCommand
                );
            }
            _tacticalBattleID = battleID;
            return _tacticalCombat;
        }

        /// <summary>
        /// Releases a prepared encounter whose required presentation assets could not be loaded.
        /// </summary>
        /// <param name="session">The unstarted encounter to release.</param>
        public void CancelTacticalPreparation(TacticalCombatSession session)
        {
            if (!ReferenceEquals(session, _tacticalCombat) || session.SystemTime != 3)
                throw new InvalidOperationException(
                    "Only an unstarted tactical encounter can be released."
                );
            _tacticalCombat = null;
            _game.ActiveBattle = null;
            _tacticalBattleID = null;
            _tacticalAttackingUnits = null;
            _tacticalDefendingUnits = null;
        }

        /// <summary>
        /// Commits a completed interactive encounter through the campaign's combat result boundary.
        /// </summary>
        /// <param name="session">The completed encounter created by these commands.</param>
        /// <returns>The applied combat result and its destruction and blockade events.</returns>
        public List<GameResult> CompleteTacticalCombat(TacticalCombatSession session)
        {
            if (
                !ReferenceEquals(session, _tacticalCombat)
                || !session.IsComplete
                || _pendingDecision == null
            )
                throw new InvalidOperationException(
                    "The tactical encounter is not ready to commit."
                );
            return ImportTacticalResult(
                CreateTacticalResult(session.CaptureOutcome()),
                session.ImmediateResultRequested
            );
        }

        /// <summary>
        /// Imports one host-ordered outcome against this peer's captured encounter participants.
        /// </summary>
        /// <param name="battleID">The active battle identity from the ordered completion request.</param>
        /// <param name="outcome">The detached authoritative tactical outcome.</param>
        /// <returns>The applied campaign result and its resulting domain events.</returns>
        public List<GameResult> CompleteNetworkTacticalCombat(
            string battleID,
            TacticalCombatOutcome outcome
        )
        {
            if (!TryCompleteNetworkTacticalCombat(battleID, outcome, out List<GameResult> results))
                throw new InvalidOperationException(
                    "The network battle outcome cannot be committed."
                );
            return results;
        }

        /// <summary>
        /// Rejects stale or malformed outcomes before entering the campaign mutation boundary.
        /// </summary>
        /// <param name="battleID">The host-issued identity of the completed encounter.</param>
        /// <param name="outcome">The authoritative final tactical pools.</param>
        /// <param name="results">The applied result and domain events, or null after rejection.</param>
        /// <returns>Whether the final outcome was validated and committed.</returns>
        public bool TryCompleteNetworkTacticalCombat(
            string battleID,
            TacticalCombatOutcome outcome,
            out List<GameResult> results
        )
        {
            results = null;
            if (
                _tacticalCombat?.IsComplete != true
                || _pendingDecision == null
                || string.IsNullOrEmpty(_tacticalBattleID)
                || !string.Equals(_tacticalBattleID, battleID, StringComparison.Ordinal)
            )
                return false;
            SpaceCombatResult result;
            try
            {
                result = CreateTacticalResult(outcome, validateNetwork: true);
            }
            catch (InvalidDataException)
            {
                return false;
            }
            results = ImportTacticalResult(result, immediateResult: false);
            return true;
        }

        private SpaceCombatResult CreateTacticalResult(
            TacticalCombatOutcome outcome,
            bool validateNetwork = false
        )
        {
            if (
                outcome?.Ships == null
                || outcome.Fighters == null
                || validateNetwork
                    && (
                        !Enum.IsDefined(typeof(SpaceCombatResult.BattleOutcome), outcome.Winner)
                        || !Enum.IsDefined(
                            typeof(SpaceCombatResult.SideOutcome),
                            outcome.AttackerOutcome
                        )
                        || !Enum.IsDefined(
                            typeof(SpaceCombatResult.SideOutcome),
                            outcome.DefenderOutcome
                        )
                    )
            )
                throw new InvalidDataException("The tactical outcome is incomplete.");

            Dictionary<string, TacticalCombatOutcome.ShipOutcome> ships = new(
                StringComparer.Ordinal
            );
            foreach (TacticalCombatOutcome.ShipOutcome ship in outcome.Ships)
                if (string.IsNullOrEmpty(ship?.UnitID) || !ships.TryAdd(ship.UnitID, ship))
                    throw new InvalidDataException(
                        "A tactical ship outcome has an invalid identity."
                    );

            Dictionary<string, TacticalCombatOutcome.FighterOutcome> fighters = new(
                StringComparer.Ordinal
            );
            foreach (TacticalCombatOutcome.FighterOutcome fighter in outcome.Fighters)
                if (
                    string.IsNullOrEmpty(fighter?.UnitID)
                    || !fighters.TryAdd(fighter.UnitID, fighter)
                )
                    throw new InvalidDataException(
                        "A tactical fighter outcome has an invalid identity."
                    );

            if (ships.Count + fighters.Count != _tacticalCombat.Units.Count)
                throw new InvalidDataException(
                    "The tactical outcome does not cover the captured roster."
                );

            SpaceCombatResult result = new()
            {
                Winner = outcome.Winner,
                AttackerOutcome = outcome.AttackerOutcome,
                DefenderOutcome = outcome.DefenderOutcome,
                IterationsCompleted = outcome.IterationsCompleted,
            };
            foreach (CombatUnit unit in _tacticalCombat.Units)
            {
                ISceneNode source = ResolveTacticalSource(unit);
                if (
                    source is CapitalShip ship
                    && ships.TryGetValue(unit.InstanceID, out var shipState)
                )
                {
                    if (
                        validateNetwork
                        && (
                            shipState.Hull < 0
                            || shipState.Hull > Math.Max(1, (int)unit.MaximumHull)
                            || !IsValidTacticalComponentDamage(shipState.SystemDamage, unit, ship)
                        )
                    )
                        throw new InvalidDataException("A tactical ship outcome is invalid.");

                    result.ShipDamage.Add(
                        new ShipDamageResult
                        {
                            Ship = ship,
                            HullBefore = (int)unit.InitialHull,
                            HullAfter = shipState.Hull,
                            SystemDamage = shipState.SystemDamage,
                        }
                    );
                    if (shipState.Withdrawn)
                        result.WithdrawnUnits.Add(ship);
                    continue;
                }
                if (
                    source is not Starfighter fighter
                    || !fighters.TryGetValue(unit.InstanceID, out var fighterState)
                )
                    throw new InvalidDataException(
                        "A tactical outcome has no matching participant."
                    );
                if (
                    validateNetwork
                    && (
                        fighterState.SquadronSize < 0
                        || fighterState.SquadronSize > Math.Max(1, fighter.MaxSquadronSize)
                    )
                )
                    throw new InvalidDataException("A tactical fighter outcome is invalid.");

                CapitalShip carrier = null;
                if (fighterState.CarrierID != null)
                {
                    CombatUnit host = _tacticalCombat.GetUnit(fighterState.CarrierID);
                    if (
                        ResolveTacticalSource(host) is not CapitalShip shipHost
                        || validateNetwork && host.Side != unit.Side
                    )
                        throw new InvalidDataException("A tactical recovery host is invalid.");
                    carrier = shipHost;
                }
                result.FighterLosses.Add(
                    new FighterLossResult
                    {
                        Fighter = fighter,
                        SquadsBefore = unit.InitialSquadronSize,
                        SquadsAfter = fighterState.SquadronSize,
                    }
                );
                result.FighterRecovery.Add(
                    new FighterRecoveryResult { Fighter = fighter, Carrier = carrier }
                );
                if (fighterState.Withdrawn)
                    result.WithdrawnUnits.Add(fighter);
            }
            return result;
        }

        private ISceneNode ResolveTacticalSource(CombatUnit unit)
        {
            string sourceID = unit?.SourceUnitInstanceID;
            ISceneNode source = _game.GetSceneNodeByInstanceID<ISceneNode>(
                sourceID,
                includeDisabled: true
            );
            return source
                ?? throw new InvalidDataException(
                    $"Tactical source '{sourceID}' is no longer present in the campaign."
                );
        }

        /// <summary>
        /// Admits only component levels and installed hyperdrives represented by the captured ship.
        /// </summary>
        /// <param name="damage">The authoritative component damage.</param>
        /// <param name="unit">The captured tactical participant.</param>
        /// <param name="ship">Its immutable campaign source during the encounter.</param>
        /// <returns>Whether all component fields are within their supported ranges.</returns>
        private static bool IsValidTacticalComponentDamage(
            CapitalShipSystemDamage damage,
            CombatUnit unit,
            CapitalShip ship
        ) =>
            damage?.Shields is >= 0 and <= 4
            && damage.Weapons is >= 0 and <= 4
            && damage.TractorBeam is >= 0 and <= 4
            && damage.Engines is >= 0 and <= 4
            && damage.PrimaryHyperdrive >= 0
            && damage.PrimaryHyperdrive <= (ship.Hyperdrive != 0 ? 1 : 0)
            && damage.BackupHyperdrive >= 0
            && damage.BackupHyperdrive <= (unit.HasBackupHyperdrive ? 1 : 0);

        /// <summary>
        /// Applies captured tactical pools through the shared campaign loss and withdrawal boundary.
        /// </summary>
        /// <param name="result">The completed encounter's result using local campaign references.</param>
        /// <param name="immediateResult">Whether surviving forces require automatic continuation.</param>
        /// <returns>The final campaign result and its resulting domain events.</returns>
        private List<GameResult> ImportTacticalResult(
            SpaceCombatResult result,
            bool immediateResult
        )
        {
            SpaceCombatDecision decision = _pendingDecision;
            List<Fleet> attackers = _queries.GetFleets(decision.AttackerFleetInstanceIDs);
            List<Fleet> defenders = _queries.GetFleets(decision.DefenderFleetInstanceIDs);
            Planet planet = _queries.ResolveCombatPlanet(decision);
            result.AttackerFleet = SpaceCombatQueries.GetRepresentativeFleet(attackers);
            result.DefenderFleet = SpaceCombatQueries.GetRepresentativeFleet(defenders);
            result.AttackerOwnerInstanceID = decision.AttackerOwnerInstanceID;
            result.DefenderOwnerInstanceID = decision.DefenderOwnerInstanceID;
            result.Planet = planet;
            result.PlanetOwnerInstanceID = planet.OwnerInstanceID;
            result.Tick = _game.CurrentTick;
            result.AttackingUnits = _tacticalAttackingUnits;
            result.DefendingUnits = _tacticalDefendingUnits;
            bool wasBlockaded = planet.IsBlockaded(_game);
            List<GameResult> events = ApplyFighterResults(result);
            events.AddRange(ApplyShipResults(result, attackers, defenders));
            if (immediateResult)
                result = ResolveImmediateTacticalContinuation(
                    decision,
                    result,
                    wasBlockaded,
                    events
                );
            else
            {
                HashSet<ISceneNode> departures = result.WithdrawnUnits.ToHashSet();
                SpaceCombatWithdrawalPlan attackerPlan = _queries.GetTacticalWithdrawalPlan(
                    attackers,
                    planet,
                    decision.AttackerOwnerInstanceID,
                    departures,
                    result.FighterRecovery
                );
                SpaceCombatWithdrawalPlan defenderPlan = _queries.GetTacticalWithdrawalPlan(
                    defenders,
                    planet,
                    decision.DefenderOwnerInstanceID,
                    departures,
                    result.FighterRecovery
                );
                result.AttackerRetreatPlanetInstanceID = attackerPlan.GetRetreatPlanetInstanceID();
                result.DefenderRetreatPlanetInstanceID = defenderPlan.GetRetreatPlanetInstanceID();
                CompleteFighterWithdrawal(attackerPlan);
                CompleteFighterWithdrawal(defenderPlan);
                result.Events = CompleteCombatResult(
                    result,
                    attackerPlan,
                    defenderPlan,
                    wasBlockaded,
                    events
                );
            }
            result.AttackingUnits = _tacticalAttackingUnits;
            result.DefendingUnits = _tacticalDefendingUnits;
            IEnumerable<ISceneNode> destroyed = result
                .Events.OfType<GameObjectDestroyedResult>()
                .Select(destruction => destruction.DestroyedObject)
                .OfType<ISceneNode>();
            IEnumerable<ISceneNode> damaged = result
                .ShipDamage.Where(damage =>
                    damage.HullAfter < damage.Ship.MaxHullStrength || damage.SystemDamage?.Total > 0
                )
                .Select(damage => (ISceneNode)damage.Ship)
                .Concat(
                    result
                        .FighterLosses.Where(loss =>
                            loss.SquadsAfter < loss.Fighter.MaxSquadronSize
                        )
                        .Select(loss => (ISceneNode)loss.Fighter)
                );
            CombatUnitSnapshot.RecordOutcomes(result.AttackingUnits, damaged, destroyed);
            CombatUnitSnapshot.RecordOutcomes(result.DefendingUnits, damaged, destroyed);
            result.IsTacticalCombat = true;
            ClearCombatFlags(decision);
            _pendingDecision = null;
            _tacticalCombat = null;
            _game.ActiveBattle = null;
            _tacticalBattleID = null;
            _tacticalAttackingUnits = null;
            _tacticalDefendingUnits = null;
            List<GameResult> results = new() { result };
            results.AddRange(
                result.Events.Where(item =>
                    item is GameObjectDestroyedResult or BlockadeChangedResult
                )
            );
            return results;
        }

        /// <summary>
        /// Imports the current losses before automatically resolving survivors through the shared encounter path.
        /// </summary>
        /// <param name="decision">The encounter being continued.</param>
        /// <param name="tactical">The already-applied tactical damage snapshot.</param>
        /// <param name="wasBlockaded">The blockade state before either resolution phase.</param>
        /// <param name="events">The events produced while importing tactical losses.</param>
        /// <returns>The single result spanning both resolution phases.</returns>
        private SpaceCombatResult ResolveImmediateTacticalContinuation(
            SpaceCombatDecision decision,
            SpaceCombatResult tactical,
            bool wasBlockaded,
            List<GameResult> events
        )
        {
            SpaceCombatResult result = ResolveAutomaticFleetEncounter(decision)
                .OfType<SpaceCombatResult>()
                .SingleOrDefault();
            if (result == null)
            {
                result = tactical;
                UpdateCombatEncounterResultOutcomes(result, decision);
            }
            else
                MergeTacticalLosses(result, tactical);

            result.Events = events
                .Concat(result.Events)
                .Where(resultEvent => resultEvent is not BlockadeChangedResult)
                .ToList();
            BlockadeChangedResult blockade = _blockadeTransitionResolver.Resolve(
                result.Planet,
                wasBlockaded
            );
            if (blockade != null)
                result.Events.Add(blockade);
            return result;
        }

        /// <summary>
        /// Retains original hull and squadron counts and participants lost before automatic continuation.
        /// </summary>
        /// <param name="result">The automatic continuation's result.</param>
        /// <param name="tactical">The previously imported tactical losses.</param>
        private void MergeTacticalLosses(SpaceCombatResult result, SpaceCombatResult tactical)
        {
            Dictionary<CapitalShip, ShipDamageResult> ships = result.ShipDamage.ToDictionary(
                damage => damage.Ship
            );
            foreach (ShipDamageResult previous in tactical.ShipDamage)
                if (ships.TryGetValue(previous.Ship, out ShipDamageResult damage))
                {
                    damage.HullBefore = previous.HullBefore;
                    damage.SystemDamage ??= previous.SystemDamage;
                }
                else
                    result.ShipDamage.Add(previous);

            Dictionary<Starfighter, FighterLossResult> fighters = result.FighterLosses.ToDictionary(
                loss => loss.Fighter
            );
            foreach (FighterLossResult previous in tactical.FighterLosses)
                if (fighters.TryGetValue(previous.Fighter, out FighterLossResult loss))
                    loss.SquadsBefore = previous.SquadsBefore;
                else
                    result.FighterLosses.Add(previous);

            result.FighterRecovery.Clear();
            foreach (FighterLossResult loss in result.FighterLosses)
            {
                Starfighter fighter = loss.Fighter;
                CapitalShip carrier = null;
                if (
                    loss.SquadsAfter > 0
                    && ReferenceEquals(
                        _game.GetSceneNodeByInstanceID<Starfighter>(fighter.InstanceID),
                        fighter
                    )
                )
                {
                    CapitalShip parent = fighter.GetParent() as CapitalShip;
                    if (
                        parent != null
                        && ReferenceEquals(
                            _game.GetSceneNodeByInstanceID<CapitalShip>(parent.InstanceID),
                            parent
                        )
                    )
                        carrier = parent;
                }
                result.FighterRecovery.Add(
                    new FighterRecoveryResult { Fighter = fighter, Carrier = carrier }
                );
            }
        }

        /// <summary>
        /// Gets the presentation snapshot for the combat encounter awaiting player input.
        /// </summary>
        /// <param name="result">Receives the pending encounter snapshot.</param>
        /// <returns>True when an encounter is waiting for player input.</returns>
        public bool TryGetPendingCombat(out PendingCombatResult result)
        {
            result =
                _pendingDecision == null
                    ? null
                    : _queries.BuildPendingCombatResult(_pendingDecision);
            return result != null;
        }

        /// <summary>
        /// Creates space-combat commands.
        /// </summary>
        /// <param name="game">Active game state.</param>
        /// <param name="movement">Movement operations used for retreats and evacuation.</param>
        /// <param name="queries">Encounter and retreat eligibility queries.</param>
        /// <param name="blockadeTransitionResolver">Resolves blockade changes after combat mutations.</param>
        /// <param name="tacticalFactory">Captures interactive deployment from the selected content.</param>
        /// <param name="starfighterTypes">The available fighter designs used to build combat formations.</param>
        /// <param name="combatObserver">The session's manual battle aftermath owner.</param>
        public SpaceCombatCommands(
            GameRoot game,
            MovementCommands movement,
            SpaceCombatQueries queries,
            BlockadeTransitionResolver blockadeTransitionResolver = null,
            TacticalCombatFactory tacticalFactory = null,
            IReadOnlyList<Starfighter> starfighterTypes = null,
            SpaceCombatObserver combatObserver = null
        )
        {
            _game = game ?? throw new ArgumentNullException(nameof(game));
            _tacticalFactory = tacticalFactory;
            _combatObserver = combatObserver;
            _movement = movement ?? throw new ArgumentNullException(nameof(movement));
            _autoResolver = new SpaceCombatAutoResolver(
                game.Config.Combat.SpaceCombat,
                game.Random,
                starfighterTypes
            );
            _queries = queries ?? throw new ArgumentNullException(nameof(queries));
            _withdrawalResolver = new SpaceCombatWithdrawalResolver(game, _queries);
            _blockadeTransitionResolver =
                blockadeTransitionResolver ?? new BlockadeTransitionResolver(game);
        }

        /// <summary>
        /// Attempts to auto-resolve a detected combat encounter when both sides are AI-controlled.
        /// </summary>
        /// <param name="decision">The detected encounter to resolve.</param>
        /// <param name="resolvedFleetIds">Set updated with both fleet IDs on successful resolution.</param>
        /// <param name="results">Output list that receives combat results.</param>
        /// <returns>True if auto-resolved; false if either side is player-controlled.</returns>
        internal bool TryAutoResolveAICombat(
            SpaceCombatDecision decision,
            HashSet<string> resolvedFleetIds,
            List<GameResult> results
        )
        {
            if (!_queries.BothSidesAIControlled(decision))
                return false;

            results.AddRange(ResolveAutomaticFleetEncounter(decision));

            if (IsEncounterStillContested(decision))
            {
                resolvedFleetIds.UnionWith(decision.AttackerFleetInstanceIDs);
                resolvedFleetIds.UnionWith(decision.DefenderFleetInstanceIDs);
            }

            return true;
        }

        /// <summary>
        /// Stores a player-involved encounter and creates its presentation result.
        /// </summary>
        /// <param name="decision">The encounter awaiting player input.</param>
        /// <returns>The pending-combat result exposed to presentation.</returns>
        internal PendingCombatResult DeferCombatDecision(SpaceCombatDecision decision)
        {
            _pendingDecision = decision ?? throw new ArgumentNullException(nameof(decision));
            return _queries.BuildPendingCombatResult(decision);
        }

        /// <summary>
        /// Checks whether both sides in the encounter still occupy a contested planet.
        /// </summary>
        /// <param name="decision">The combat decision to evaluate.</param>
        /// <returns>True when both sides still contest the same planet.</returns>
        private bool IsEncounterStillContested(SpaceCombatDecision decision)
        {
            return _queries.AreForcesContestingPlanet(decision);
        }

        /// <summary>
        /// Resolves and clears the player-involved encounter waiting for a decision.
        /// </summary>
        /// <param name="autoResolve">Whether to auto-resolve instead of tactical combat.</param>
        /// <returns>Combat results generated by the encounter.</returns>
        public List<GameResult> ResolvePending(bool autoResolve)
        {
            if (_tacticalCombat != null)
                throw new InvalidOperationException(
                    "The active manual encounter must finish first."
                );
            if (_pendingDecision == null)
                throw new InvalidOperationException("No pending combat to resolve.");

            SpaceCombatDecision decision = _pendingDecision;
            List<GameResult> results = Resolve(decision, autoResolve);
            _pendingDecision = null;
            return results;
        }

        /// <summary>
        /// Resolves a withdrawal by the requested side of the pending encounter.
        /// </summary>
        /// <param name="retreatingFactionInstanceId">The withdrawing faction identifier.</param>
        /// <returns>The combat results, or null when that side cannot withdraw.</returns>
        public List<GameResult> ResolvePendingRetreat(string retreatingFactionInstanceId)
        {
            if (_tacticalCombat != null)
                throw new InvalidOperationException(
                    "The active manual encounter must finish first."
                );
            if (_pendingDecision == null)
                throw new InvalidOperationException("No pending combat to resolve.");

            if (
                retreatingFactionInstanceId != _pendingDecision.AttackerOwnerInstanceID
                    && retreatingFactionInstanceId != _pendingDecision.DefenderOwnerInstanceID
                || !TryResolveRetreat(
                    _pendingDecision,
                    retreatingFactionInstanceId,
                    out List<GameResult> results
                )
            )
                return null;

            _pendingDecision = null;
            return results;
        }

        /// <summary>
        /// Attempts to resolve a pending combat decision by withdrawing one side.
        /// </summary>
        /// <param name="decision">The pending combat decision.</param>
        /// <param name="retreatingFactionInstanceId">The faction requested to withdraw.</param>
        /// <param name="results">Receives the generated combat result.</param>
        /// <returns>True when the side withdrew successfully.</returns>
        private bool TryResolveRetreat(
            SpaceCombatDecision decision,
            string retreatingFactionInstanceId,
            out List<GameResult> results
        )
        {
            Planet planet = _queries.ResolveCombatPlanet(decision);
            results = new List<GameResult>();

            List<Fleet> attackerFleets = _queries.GetFleets(decision.AttackerFleetInstanceIDs);
            List<Fleet> defenderFleets = _queries.GetFleets(decision.DefenderFleetInstanceIDs);
            bool attackerRetreated =
                retreatingFactionInstanceId == decision.AttackerOwnerInstanceID;
            List<Fleet> retreatingFleets = attackerRetreated ? attackerFleets : defenderFleets;
            List<Fleet> opposingFleets = attackerRetreated ? defenderFleets : attackerFleets;

            SpaceCombatResult result = BuildRetreatResult(
                decision,
                attackerRetreated,
                attackerFleets,
                defenderFleets,
                planet
            );
            SpaceCombatWithdrawalPlan withdrawalPlan = _withdrawalResolver.ResolveManualWithdrawal(
                result,
                retreatingFleets,
                opposingFleets,
                retreatingFactionInstanceId,
                attackerRetreated,
                planet
            );
            if (withdrawalPlan == null)
                return false;

            result.ManualRetreatingFactionID = retreatingFactionInstanceId;
            result.Events = ApplyCombatResult(
                result,
                attackerFleets,
                defenderFleets,
                attackerRetreated ? withdrawalPlan : new SpaceCombatWithdrawalPlan(),
                attackerRetreated ? new SpaceCombatWithdrawalPlan() : withdrawalPlan
            );
            string retreatPlanetInstanceId = withdrawalPlan.GetRetreatPlanetInstanceID();
            if (attackerRetreated)
                result.AttackerRetreatPlanetInstanceID = retreatPlanetInstanceId;
            else
                result.DefenderRetreatPlanetInstanceID = retreatPlanetInstanceId;

            results.Add(result);
            results.AddRange(
                result.Events.Where(resultEvent =>
                    resultEvent is GameObjectDestroyedResult or BlockadeChangedResult
                )
            );
            ClearCombatFlags(decision);
            return true;
        }

        /// <summary>
        /// Builds the combat result emitted after a successful force withdrawal.
        /// </summary>
        /// <param name="decision">The resolved combat decision.</param>
        /// <param name="attackerRetreated">Whether the attacking side withdrew.</param>
        /// <param name="attackerFleets">The attacking fleets.</param>
        /// <param name="defenderFleets">The defending fleets.</param>
        /// <param name="planet">The combat location.</param>
        /// <returns>The withdrawal combat result.</returns>
        private SpaceCombatResult BuildRetreatResult(
            SpaceCombatDecision decision,
            bool attackerRetreated,
            IReadOnlyList<Fleet> attackerFleets,
            IReadOnlyList<Fleet> defenderFleets,
            Planet planet
        )
        {
            Fleet attacker = SpaceCombatQueries.GetRepresentativeFleet(attackerFleets);
            Fleet defender = SpaceCombatQueries.GetRepresentativeFleet(defenderFleets);
            SpaceCombatResult result = new SpaceCombatResult
            {
                AttackerFleet = attacker,
                DefenderFleet = defender,
                AttackerOwnerInstanceID = decision.AttackerOwnerInstanceID,
                DefenderOwnerInstanceID = decision.DefenderOwnerInstanceID,
                Planet = planet,
                PlanetOwnerInstanceID = planet.OwnerInstanceID,
                Winner = attackerRetreated
                    ? SpaceCombatResult.BattleOutcome.Defender
                    : SpaceCombatResult.BattleOutcome.Attacker,
                AttackerOutcome = attackerRetreated
                    ? SpaceCombatResult.SideOutcome.Withdrawn
                    : SpaceCombatResult.SideOutcome.Active,
                DefenderOutcome = attackerRetreated
                    ? SpaceCombatResult.SideOutcome.Active
                    : SpaceCombatResult.SideOutcome.Withdrawn,
                Tick = _game.CurrentTick,
            };

            result.AttackingUnits.AddRange(
                CaptureCombatUnits(attackerFleets, planet, decision.AttackerOwnerInstanceID)
            );
            result.DefendingUnits.AddRange(
                CaptureCombatUnits(defenderFleets, planet, decision.DefenderOwnerInstanceID)
            );

            return result;
        }

        /// <summary>
        /// Detects a hostile fleet encounter while skipping fleets already handled this tick.
        /// </summary>
        /// <param name="excludedFleetIds">Fleet instance IDs to skip.</param>
        /// <param name="decision">The detected encounter.</param>
        /// <returns>True if a hostile encounter was detected.</returns>
        internal bool TryBeginFleetCombat(
            HashSet<string> excludedFleetIds,
            out SpaceCombatDecision decision
        )
        {
            decision = null;

            if (
                !_queries.TryFindContestedForces(
                    excludedFleetIds,
                    out Planet planet,
                    out string attackerOwnerInstanceId,
                    out string defenderOwnerInstanceId,
                    out List<Fleet> attackerFleets,
                    out List<Fleet> defenderFleets
                )
            )
                return false;

            foreach (Fleet fleet in attackerFleets.Concat(defenderFleets))
                fleet.SetCombatState(true);

            decision = new SpaceCombatDecision
            {
                AttackerFleetInstanceIDs = attackerFleets.ConvertAll(fleet =>
                    fleet.GetInstanceID()
                ),
                DefenderFleetInstanceIDs = defenderFleets.ConvertAll(fleet =>
                    fleet.GetInstanceID()
                ),
                AttackerOwnerInstanceID = attackerOwnerInstanceId,
                DefenderOwnerInstanceID = defenderOwnerInstanceId,
                PlanetInstanceID = planet.GetInstanceID(),
            };

            return true;
        }

        /// <summary>
        /// Resolves a pending combat encounter. Applies damage to the game world and clears
        /// IsInCombat on every participating fleet regardless of outcome.
        /// </summary>
        /// <param name="decision">The combat decision to resolve.</param>
        /// <param name="autoResolve">True to use auto-resolve; false to use manual combat.</param>
        /// <returns>Combat results generated by the encounter.</returns>
        internal List<GameResult> Resolve(SpaceCombatDecision decision, bool autoResolve)
        {
            if (autoResolve)
                return ResolveFleetEncounter(decision);

            throw new InvalidOperationException(
                "Interactive combat must be started and completed through its tactical session."
            );
        }

        /// <summary>
        /// Resolves an AI-controlled fleet encounter.
        /// </summary>
        /// <param name="decision">Encounter context to resolve.</param>
        /// <returns>Combat results generated by the encounter.</returns>
        private List<GameResult> ResolveAutomaticFleetEncounter(SpaceCombatDecision decision)
        {
            return ResolveFleetEncounter(decision);
        }

        /// <summary>
        /// Resolves an entire fleet encounter through the shared automatic tactical resolver.
        /// </summary>
        /// <param name="decision">Encounter context to resolve.</param>
        /// <returns>The result for the encounter.</returns>
        private List<GameResult> ResolveFleetEncounter(SpaceCombatDecision decision)
        {
            List<GameResult> results = new List<GameResult>();
            List<Fleet> attackerFleets = _queries.GetFleets(decision.AttackerFleetInstanceIDs);
            List<Fleet> defenderFleets = _queries.GetFleets(decision.DefenderFleetInstanceIDs);
            SpaceCombatResult combatResult = null;

            try
            {
                if (_queries.AreForcesContestingPlanet(decision))
                    combatResult = ResolveCombat(decision, attackerFleets, defenderFleets);
            }
            finally
            {
                ClearCombatFlags(decision);
            }

            UpdateCombatEncounterResultOutcomes(combatResult, decision);

            if (combatResult != null)
            {
                results.Add(combatResult);
                results.AddRange(
                    combatResult.Events.Where(resultEvent =>
                        resultEvent is GameObjectDestroyedResult or BlockadeChangedResult
                    )
                );
            }

            return results;
        }

        /// <summary>
        /// Updates encounter outcomes from each fleet's final runtime state.
        /// </summary>
        /// <param name="result">The encounter result to update.</param>
        /// <param name="decision">The combat decision identifying both sides.</param>
        private void UpdateCombatEncounterResultOutcomes(
            SpaceCombatResult result,
            SpaceCombatDecision decision
        )
        {
            if (result == null)
                return;

            List<Fleet> attackerFleets = _queries.GetFleets(decision.AttackerFleetInstanceIDs);
            List<Fleet> defenderFleets = _queries.GetFleets(decision.DefenderFleetInstanceIDs);
            List<Starfighter> attackerFighters = GetLiveFighters(result.AttackingUnits);
            List<Starfighter> defenderFighters = GetLiveFighters(result.DefendingUnits);
            result.AttackerOutcome = GetCombatSideOutcome(
                attackerFleets,
                attackerFighters,
                result.AttackerOwnerInstanceID,
                result.Planet,
                result.AttackerOutcome
            );
            result.DefenderOutcome = GetCombatSideOutcome(
                defenderFleets,
                defenderFighters,
                result.DefenderOwnerInstanceID,
                result.Planet,
                result.DefenderOutcome
            );
            result.AttackerRetreatPlanetInstanceID = GetRetreatPlanetInstanceID(
                attackerFleets,
                attackerFighters,
                result.Planet,
                result.AttackerOutcome
            );
            result.DefenderRetreatPlanetInstanceID = GetRetreatPlanetInstanceID(
                defenderFleets,
                defenderFighters,
                result.Planet,
                result.DefenderOutcome
            );
            UpdateCombatEncounterWinner(result);
        }

        /// <summary>
        /// Resolves surviving fighter participants from detached combat snapshots.
        /// </summary>
        /// <param name="snapshots">The combat-side unit snapshots.</param>
        /// <returns>The participating fighters that remain in the live game.</returns>
        private List<Starfighter> GetLiveFighters(IEnumerable<CombatUnitSnapshot> snapshots)
        {
            return (snapshots ?? Enumerable.Empty<CombatUnitSnapshot>())
                .Select(snapshot => snapshot?.Unit?.GetInstanceID())
                .Where(instanceId => !string.IsNullOrEmpty(instanceId))
                .Select(instanceId => _game.GetSceneNodeByInstanceID<Starfighter>(instanceId))
                .Where(fighter => fighter != null)
                .Distinct()
                .ToList();
        }

        /// <summary>
        /// Aligns the encounter winner with the sides that remain active after resolution.
        /// </summary>
        /// <param name="result">The encounter result to update.</param>
        private static void UpdateCombatEncounterWinner(SpaceCombatResult result)
        {
            bool attackerActive = result.AttackerOutcome == SpaceCombatResult.SideOutcome.Active;
            bool defenderActive = result.DefenderOutcome == SpaceCombatResult.SideOutcome.Active;
            if (attackerActive == defenderActive)
                return;

            result.Winner = attackerActive
                ? SpaceCombatResult.BattleOutcome.Attacker
                : SpaceCombatResult.BattleOutcome.Defender;
        }

        /// <summary>
        /// Returns the destination recorded for a withdrawn combat side.
        /// </summary>
        /// <param name="fleets">The side's surviving participating fleets.</param>
        /// <param name="fighters">The side's surviving participating fighter squadrons.</param>
        /// <param name="battlePlanet">The planet where combat occurred.</param>
        /// <param name="outcome">The side's final combat outcome.</param>
        /// <returns>The retreat planet identifier, or null when the side did not withdraw.</returns>
        private static string GetRetreatPlanetInstanceID(
            IReadOnlyList<Fleet> fleets,
            IReadOnlyList<Starfighter> fighters,
            Planet battlePlanet,
            SpaceCombatResult.SideOutcome outcome
        )
        {
            if (outcome != SpaceCombatResult.SideOutcome.Withdrawn)
                return null;
            Planet destination = (fleets ?? Array.Empty<Fleet>())
                .Select(fleet => fleet?.GetParentOfType<Planet>())
                .Concat(
                    (fighters ?? Array.Empty<Starfighter>()).Select(fighter =>
                        fighter?.GetParentOfType<Planet>()
                    )
                )
                .FirstOrDefault(planet => planet != null && planet != battlePlanet);
            return destination?.InstanceID;
        }

        /// <summary>
        /// Resolves a combat side's final encounter outcome.
        /// </summary>
        /// <param name="fleets">The participating fleets.</param>
        /// <param name="fighters">The surviving participating fighter squadrons.</param>
        /// <param name="ownerInstanceId">The participating owner's identifier.</param>
        /// <param name="battlePlanet">The encounter location.</param>
        /// <param name="resolvedOutcome">The outcome recorded by the tactical resolver.</param>
        /// <returns>The final encounter outcome.</returns>
        private static SpaceCombatResult.SideOutcome GetCombatSideOutcome(
            IReadOnlyList<Fleet> fleets,
            IReadOnlyList<Starfighter> fighters,
            string ownerInstanceId,
            Planet battlePlanet,
            SpaceCombatResult.SideOutcome resolvedOutcome
        )
        {
            if (resolvedOutcome == SpaceCombatResult.SideOutcome.Destroyed)
                return SpaceCombatResult.SideOutcome.Destroyed;

            if (SpaceCombatQueries.HasActiveSpaceUnits(fleets, battlePlanet, ownerInstanceId))
                return SpaceCombatResult.SideOutcome.Active;

            if (
                fleets?.Any(fleet => fleet?.Movement != null) == true
                || fighters?.Any(fighter => fighter?.Movement != null) == true
            )
                return SpaceCombatResult.SideOutcome.Withdrawn;

            List<Planet> currentPlanets = (fleets ?? Array.Empty<Fleet>())
                .Select(fleet => fleet?.GetParentOfType<Planet>())
                .Concat(
                    (fighters ?? Array.Empty<Starfighter>()).Select(fighter =>
                        fighter?.GetParentOfType<Planet>()
                    )
                )
                .Where(planet => planet != null)
                .ToList();
            if (currentPlanets.Count == 0)
                return SpaceCombatResult.SideOutcome.Destroyed;

            if (battlePlanet != null && currentPlanets.Any(planet => planet != battlePlanet))
                return SpaceCombatResult.SideOutcome.Withdrawn;

            return SpaceCombatResult.SideOutcome.Active;
        }

        /// <summary>
        /// Clears combat state from fleets that remain after an encounter.
        /// </summary>
        /// <param name="decision">Encounter identifying the affected fleets.</param>
        private void ClearCombatFlags(SpaceCombatDecision decision)
        {
            foreach (
                Fleet fleet in _queries
                    .GetFleets(decision.AttackerFleetInstanceIDs)
                    .Concat(_queries.GetFleets(decision.DefenderFleetInstanceIDs))
            )
            {
                fleet.SetCombatState(false);
            }
        }

        /// <summary>
        /// Resolves one complete space-combat encounter and applies it to the game state.
        /// </summary>
        /// <param name="decision">The combat decision identifying both sides.</param>
        /// <param name="attackerFleets">Attacking fleets.</param>
        /// <param name="defenderFleets">Defending fleets.</param>
        /// <returns>The applied combat result, or null when the encounter is no longer valid.</returns>
        private SpaceCombatResult ResolveCombat(
            SpaceCombatDecision decision,
            IReadOnlyList<Fleet> attackerFleets,
            IReadOnlyList<Fleet> defenderFleets
        )
        {
            Planet planet = _queries.ResolveCombatPlanet(decision);
            if (planet == null)
            {
                GameLogger.Warning("ResolveCombat: the combat planet no longer exists.");
                return null;
            }

            SpaceCombatResult result = ResolveSpace(
                attackerFleets,
                defenderFleets,
                decision.AttackerOwnerInstanceID,
                decision.DefenderOwnerInstanceID,
                planet,
                _game.CurrentTick,
                out SpaceCombatWithdrawalPlan attackerWithdrawalPlan,
                out SpaceCombatWithdrawalPlan defenderWithdrawalPlan
            );
            result.Events = ApplyCombatResult(
                result,
                attackerFleets,
                defenderFleets,
                attackerWithdrawalPlan,
                defenderWithdrawalPlan
            );

            GameLogger.Log(
                $"Combat at {planet.GetDisplayName()}: "
                    + $"{decision.AttackerOwnerInstanceID} vs "
                    + $"{decision.DefenderOwnerInstanceID} - "
                    + $"Winner: {result.Winner}"
            );

            return result;
        }

        /// <summary>
        /// Moves withdrawn fighters to their reserved destinations after losses are applied.
        /// </summary>
        /// <param name="plan">The reserved withdrawal plan.</param>
        private void CompleteFighterWithdrawal(SpaceCombatWithdrawalPlan plan)
        {
            foreach (
                KeyValuePair<Starfighter, ContainerNode> assignment in plan.FighterDestinations
            )
            {
                _movement.TryRequestMove(assignment.Key, assignment.Value);
            }

            _withdrawalResolver.ApplyFighterRecovery(plan.FighterRecoveryAssignments);
        }

        /// <summary>
        /// Moves withdrawing fleets to their reserved planets.
        /// </summary>
        /// <param name="plan">The reserved withdrawal plan.</param>
        /// <param name="results">The combat event collection receiving blockade transitions.</param>
        private void CompleteFleetWithdrawal(
            SpaceCombatWithdrawalPlan plan,
            ICollection<GameResult> results
        )
        {
            foreach (KeyValuePair<Fleet, Planet> assignment in plan.FleetDestinations)
            {
                Fleet fleet = assignment.Key;
                List<CapitalShip> escapingShips = fleet
                    .GetChildren<CapitalShip>()
                    .Where(ship => plan.Contains(ship))
                    .ToList();
                if (escapingShips.Count != fleet.GetChildren<CapitalShip>().Count)
                    fleet =
                        new FleetCommands(_game).CreateFleet(
                            escapingShips,
                            fleet.GetOwnerInstanceID()
                        )
                        ?? throw new InvalidOperationException(
                            "The withdrawing ships could not form a fleet."
                        );
                Planet originalPlanet = fleet.GetParentOfType<Planet>();
                if (
                    !_movement.TryRequestMoveWithImmediateBlockadeResults(
                        fleet,
                        assignment.Value,
                        results
                    )
                )
                    continue;

                if (
                    (
                        fleet.Order?.OrderType == FleetOrderType.Attack
                        || fleet.Order?.OrderType == FleetOrderType.Engage
                    )
                    && fleet.Order.TargetPlanetId == originalPlanet?.InstanceID
                )
                    fleet.Order = null;
            }
        }

        /// <summary>
        /// Resolves one battle with the headless tactical auto-resolver.
        /// </summary>
        /// <param name="attackerFleets">The attacking fleets.</param>
        /// <param name="defenderFleets">The defending fleets.</param>
        /// <param name="attackerOwnerInstanceId">The attacking owner identifier.</param>
        /// <param name="defenderOwnerInstanceId">The defending owner identifier.</param>
        /// <param name="planet">Planet where combat occurs.</param>
        /// <param name="tick">Current game tick (recorded on the result).</param>
        /// <param name="attackerWithdrawalPlan">Receives the attacking side's reserved withdrawal.</param>
        /// <param name="defenderWithdrawalPlan">Receives the defending side's reserved withdrawal.</param>
        /// <returns>The combat result with winner, per-ship damage, and fighter losses.</returns>
        private SpaceCombatResult ResolveSpace(
            IReadOnlyList<Fleet> attackerFleets,
            IReadOnlyList<Fleet> defenderFleets,
            string attackerOwnerInstanceId,
            string defenderOwnerInstanceId,
            Planet planet,
            int tick,
            out SpaceCombatWithdrawalPlan attackerWithdrawalPlan,
            out SpaceCombatWithdrawalPlan defenderWithdrawalPlan
        )
        {
            List<CapitalShip> attackerShips = attackerFleets
                .SelectMany(SpaceCombatQueries.GetActiveCapitalShips)
                .Distinct()
                .ToList();
            List<CapitalShip> defenderShips = defenderFleets
                .SelectMany(SpaceCombatQueries.GetActiveCapitalShips)
                .Distinct()
                .ToList();
            List<Starfighter> attackerPlanetaryFighters = SpaceCombatQueries
                .GetActivePlanetStarfighters(planet, attackerOwnerInstanceId)
                .ToList();
            List<Starfighter> defenderPlanetaryFighters = SpaceCombatQueries
                .GetActivePlanetStarfighters(planet, defenderOwnerInstanceId)
                .ToList();
            List<Starfighter> attackerFighters = attackerShips
                .SelectMany(ship => ship.GetChildren<Starfighter>())
                .Concat(attackerPlanetaryFighters)
                .Where(SpaceCombatQueries.IsActiveStarfighter)
                .Distinct()
                .ToList();
            List<Starfighter> defenderFighters = defenderShips
                .SelectMany(ship => ship.GetChildren<Starfighter>())
                .Concat(defenderPlanetaryFighters)
                .Where(SpaceCombatQueries.IsActiveStarfighter)
                .Distinct()
                .ToList();
            bool attackerCanWithdraw = _withdrawalResolver.CanAutomaticallyWithdraw(
                attackerFleets,
                planet,
                attackerOwnerInstanceId
            );
            bool defenderCanWithdraw = _withdrawalResolver.CanAutomaticallyWithdraw(
                defenderFleets,
                planet,
                defenderOwnerInstanceId
            );
            (Officer Admiral, Officer Commander) attackerCommandOfficers = GetCommandOfficers(
                attackerFleets,
                planet,
                attackerOwnerInstanceId
            );
            (Officer Admiral, Officer Commander) defenderCommandOfficers = GetCommandOfficers(
                defenderFleets,
                planet,
                defenderOwnerInstanceId
            );
            SpaceCombatCommandModifiers attackerCommand = CreateCommandModifiers(
                attackerCommandOfficers
            );
            SpaceCombatCommandModifiers defenderCommand = CreateCommandModifiers(
                defenderCommandOfficers
            );
            SpaceCombatResult result = _autoResolver.Resolve(
                attackerShips,
                attackerFighters,
                defenderShips,
                defenderFighters,
                attackerCanWithdraw,
                defenderCanWithdraw,
                attackerCommand,
                defenderCommand,
                attackerShips
                    .Concat(defenderShips)
                    .Where(ship => SpaceCombatQueries.IsPlanetDestroyerShielded(ship, planet))
                    .ToList(),
                attackerHasInitiative: _game
                    .GetFactions()
                    .FindIndex(faction => faction.InstanceID == attackerOwnerInstanceId)
                    > _game
                        .GetFactions()
                        .FindIndex(faction => faction.InstanceID == defenderOwnerInstanceId),
                attackerUsesRetreatThreshold: ShouldUseRetreatThreshold(
                    attackerOwnerInstanceId,
                    defenderOwnerInstanceId
                ),
                defenderUsesRetreatThreshold: ShouldUseRetreatThreshold(
                    defenderOwnerInstanceId,
                    attackerOwnerInstanceId
                )
            );
            IncludeCommandOfficers(result.AttackingUnits, attackerCommandOfficers);
            IncludeCommandOfficers(result.DefendingUnits, defenderCommandOfficers);
            result.AttackerFleet = SpaceCombatQueries.GetRepresentativeFleet(attackerFleets);
            result.DefenderFleet = SpaceCombatQueries.GetRepresentativeFleet(defenderFleets);
            result.AttackerOwnerInstanceID = attackerOwnerInstanceId;
            result.DefenderOwnerInstanceID = defenderOwnerInstanceId;
            result.Planet = planet;
            result.PlanetOwnerInstanceID = planet.OwnerInstanceID;
            result.Tick = tick;
            HashSet<ISceneNode> tacticallyWithdrawnUnits = result.WithdrawnUnits.ToHashSet();
            attackerWithdrawalPlan =
                result.AttackerOutcome == SpaceCombatResult.SideOutcome.Withdrawn
                    ? _withdrawalResolver.ResolveAutomaticWithdrawal(
                        result,
                        attackerFleets,
                        attackerOwnerInstanceId,
                        attackerRetreated: true,
                        planet,
                        tacticallyWithdrawnUnits
                    )
                    : new SpaceCombatWithdrawalPlan();
            defenderWithdrawalPlan =
                result.DefenderOutcome == SpaceCombatResult.SideOutcome.Withdrawn
                    ? _withdrawalResolver.ResolveAutomaticWithdrawal(
                        result,
                        defenderFleets,
                        defenderOwnerInstanceId,
                        attackerRetreated: false,
                        planet,
                        tacticallyWithdrawnUnits
                    )
                    : new SpaceCombatWithdrawalPlan();
            return result;
        }

        /// <summary>
        /// Determines whether relative combat strength may trigger a participant's retreat.
        /// </summary>
        /// <param name="ownerInstanceID">The participant's faction.</param>
        /// <param name="opponentInstanceID">The opposing faction.</param>
        /// <returns>False for a human facing AI; true otherwise.</returns>
        private bool ShouldUseRetreatThreshold(string ownerInstanceID, string opponentInstanceID)
        {
            return !(
                _game.GetFactionPlayer(ownerInstanceID)?.ControllerType
                    == PlayerControllerType.Human
                && _game.GetFactionPlayer(opponentInstanceID)?.ControllerType
                    != PlayerControllerType.Human
            );
        }

        /// <summary>
        /// Selects the tactical command officers for one side of a space battle.
        /// The best Leadership-rated fleet Admiral commands capital ships; the best Combat-rated
        /// Commander across the system and participating fleets commands starfighters.
        /// </summary>
        /// <param name="fleets">The side's participating fleets in encounter order.</param>
        /// <param name="planet">The system where combat occurs.</param>
        /// <param name="ownerInstanceId">The side's faction instance identifier.</param>
        /// <returns>The selected officers' effective ratings.</returns>
        internal static SpaceCombatCommandModifiers GetCommandModifiers(
            IReadOnlyList<Fleet> fleets,
            Planet planet,
            string ownerInstanceId
        )
        {
            return CreateCommandModifiers(GetCommandOfficers(fleets, planet, ownerInstanceId));
        }

        /// <summary>
        /// Selects the ranking officers who command one side of a space battle.
        /// </summary>
        /// <param name="fleets">The side's participating fleets in encounter order.</param>
        /// <param name="planet">The system where combat occurs.</param>
        /// <param name="ownerInstanceId">The side's faction instance identifier.</param>
        /// <returns>The selected Admiral and Commander.</returns>
        internal static (Officer Admiral, Officer Commander) GetCommandOfficers(
            IReadOnlyList<Fleet> fleets,
            Planet planet,
            string ownerInstanceId
        )
        {
            List<Officer> fleetOfficers = (fleets ?? Array.Empty<Fleet>())
                .Where(fleet => fleet != null)
                .SelectMany(fleet => fleet.GetOfficers())
                .ToList();
            Officer admiral = SelectBestCommandOfficer(
                fleetOfficers,
                OfficerRank.Admiral,
                SkillRating.Leadership,
                ownerInstanceId
            );

            IEnumerable<Officer> commanderCandidates = (
                planet?.GetAllOfficers() ?? new List<Officer>()
            ).Concat(fleetOfficers);
            Officer commander = SelectBestCommandOfficer(
                commanderCandidates,
                OfficerRank.Commander,
                SkillRating.Combat,
                ownerInstanceId
            );
            return (admiral, commander);
        }

        /// <summary>
        /// Converts selected command officers to the ratings consumed by tactical resolution.
        /// </summary>
        /// <param name="officers">The selected Admiral and Commander.</param>
        /// <returns>The tactical command modifiers.</returns>
        private static SpaceCombatCommandModifiers CreateCommandModifiers(
            (Officer Admiral, Officer Commander) officers
        )
        {
            return new SpaceCombatCommandModifiers(
                officers.Admiral?.GetEffectiveRating(SkillRating.Leadership) ?? 0,
                officers.Commander?.GetParent() is CapitalShip
                    ? officers.Commander.GetEffectiveRating(SkillRating.Combat)
                    : 0,
                officers.Admiral?.GetParent() as CapitalShip,
                officers.Commander?.GetParent() as CapitalShip
            );
        }

        /// <summary>
        /// Adds selected command officers that are not already carried by a reported fleet.
        /// </summary>
        /// <param name="units">The side's detached combat snapshots.</param>
        /// <param name="officers">The selected Admiral and Commander.</param>
        private static void IncludeCommandOfficers(
            ICollection<CombatUnitSnapshot> units,
            (Officer Admiral, Officer Commander) officers
        )
        {
            HashSet<string> includedIds = units
                .Where(unit => unit?.Unit != null)
                .Select(unit => unit.Unit.GetInstanceID())
                .Where(instanceId => !string.IsNullOrEmpty(instanceId))
                .ToHashSet(StringComparer.Ordinal);
            foreach (Officer officer in new[] { officers.Admiral, officers.Commander })
            {
                if (officer == null || !includedIds.Add(officer.GetInstanceID()))
                    continue;

                units.Add(new CombatUnitSnapshot(officer));
            }
        }

        /// <summary>
        /// Returns the first eligible officer with the highest requested effective rating.
        /// </summary>
        /// <param name="officers">The stable-order candidates to inspect.</param>
        /// <param name="rank">The required command rank.</param>
        /// <param name="rating">The rating used to compare eligible officers.</param>
        /// <param name="ownerInstanceId">The required faction instance identifier.</param>
        /// <returns>The selected officer, or null when no candidate is eligible.</returns>
        private static Officer SelectBestCommandOfficer(
            IEnumerable<Officer> officers,
            OfficerRank rank,
            SkillRating rating,
            string ownerInstanceId
        )
        {
            Officer selected = null;
            int selectedRating = int.MinValue;
            foreach (Officer candidate in officers ?? Enumerable.Empty<Officer>())
            {
                if (
                    candidate == null
                    || candidate.CurrentRank != rank
                    || candidate.GetOwnerInstanceID() != ownerInstanceId
                    || candidate.IsKilled
                    || candidate.IsCaptured
                    || candidate.IsRetired
                    || candidate.Movement != null
                    || candidate.IsOnMission()
                )
                    continue;

                int candidateRating = candidate.GetEffectiveRating(rating);
                if (candidateRating <= selectedRating)
                    continue;

                selected = candidate;
                selectedRating = candidateRating;
            }
            return selected;
        }

        private void CaptureTacticalReport(TacticalCombatFactory.EncounterRoster roster)
        {
            Dictionary<string, ISceneNode> battleCopies = _tacticalCombat.Units.ToDictionary(
                unit => unit.SourceUnitInstanceID,
                unit => (ISceneNode)unit.Unit,
                StringComparer.Ordinal
            );
            _tacticalAttackingUnits = CaptureCombatUnits(
                roster.Attacker.Fleets,
                roster.Planet,
                roster.Attacker.OwnerID,
                battleCopies
            );
            _tacticalDefendingUnits = CaptureCombatUnits(
                roster.Defender.Fleets,
                roster.Planet,
                roster.Defender.OwnerID,
                battleCopies
            );
        }

        /// <summary>
        /// Captures the current units on one side of a combat encounter without resolving damage.
        /// </summary>
        /// <param name="fleets">The participating fleets.</param>
        /// <param name="planet">The combat planet.</param>
        /// <param name="ownerInstanceId">The owner of planetary starfighters to include.</param>
        /// <returns>The detached unit snapshots for the force.</returns>
        private static List<CombatUnitSnapshot> CaptureCombatUnits(
            IReadOnlyList<Fleet> fleets,
            Planet planet,
            string ownerInstanceId,
            IReadOnlyDictionary<string, ISceneNode> battleCopies = null
        )
        {
            List<CapitalShip> ships = (fleets ?? Array.Empty<Fleet>())
                .SelectMany(SpaceCombatQueries.GetActiveCapitalShips)
                .Distinct()
                .ToList();
            List<Starfighter> fighters = ships
                .SelectMany(ship => ship.GetChildren<Starfighter>())
                .Concat(SpaceCombatQueries.GetActivePlanetStarfighters(planet, ownerInstanceId))
                .Where(SpaceCombatQueries.IsActiveStarfighter)
                .Distinct()
                .ToList();
            List<CombatUnitSnapshot> units = ships
                .SelectMany(ship =>
                    new[] { ship }
                        .Cast<ISceneNode>()
                        .Concat(ship.GetChildren<ISceneNode>(recursive: true))
                )
                .Concat(fighters)
                .Where(unit => unit != null)
                .Distinct()
                .Select(unit => new CombatUnitSnapshot(
                    unit,
                    unit.InstanceID == null
                        ? null
                        : battleCopies?.GetValueOrDefault(unit.InstanceID)
                ))
                .ToList();
            IEnumerable<ISceneNode> damagedUnits = ships
                .Where(ship => ship.CurrentHullStrength < ship.MaxHullStrength)
                .Cast<ISceneNode>()
                .Concat(
                    fighters
                        .Where(fighter => fighter.CurrentSquadronSize < fighter.MaxSquadronSize)
                        .Cast<ISceneNode>()
                );
            CombatUnitSnapshot.RecordOutcomes(units, damagedUnits, Enumerable.Empty<ISceneNode>());
            return units;
        }

        /// <summary>
        /// Applies a space combat result to the game world: updates hull strength, removes
        /// destroyed ships and depleted fighter squadrons, cleans up empty fleets.
        /// </summary>
        /// <param name="result">The combat result to apply.</param>
        /// <param name="attackerFleets">The attacking fleets to clean up.</param>
        /// <param name="defenderFleets">The defending fleets to clean up.</param>
        /// <param name="attackerWithdrawalPlan">The attacking side's reserved withdrawal.</param>
        /// <param name="defenderWithdrawalPlan">The defending side's reserved withdrawal.</param>
        /// <returns>Events generated from ship damage and destruction.</returns>
        private List<GameResult> ApplyCombatResult(
            SpaceCombatResult result,
            IReadOnlyList<Fleet> attackerFleets,
            IReadOnlyList<Fleet> defenderFleets,
            SpaceCombatWithdrawalPlan attackerWithdrawalPlan,
            SpaceCombatWithdrawalPlan defenderWithdrawalPlan
        )
        {
            bool wasBlockaded = result.Planet.IsBlockaded(_game);
            List<GameResult> events = ApplyFighterResults(result);
            CompleteFighterWithdrawal(attackerWithdrawalPlan);
            CompleteFighterWithdrawal(defenderWithdrawalPlan);
            events.AddRange(
                RecoverFightersFromDestroyedCarriers(result, attackerFleets, defenderFleets)
            );
            events.AddRange(ApplyShipResults(result, attackerFleets, defenderFleets));
            return CompleteCombatResult(
                result,
                attackerWithdrawalPlan,
                defenderWithdrawalPlan,
                wasBlockaded,
                events
            );
        }

        /// <summary>
        /// Commits fighter counts and final carrier assignments before departure destinations are applied.
        /// </summary>
        /// <param name="result">The combat result containing fighter losses and recovery assignments.</param>
        /// <returns>The fighter destruction events.</returns>
        private List<GameResult> ApplyFighterResults(SpaceCombatResult result)
        {
            List<GameResult> events = ApplyFighterSquadronLosses(
                result.FighterLosses,
                result.Planet
            );
            _movement.RecoverStarfighters(result.Planet, result.FighterRecovery);
            return events;
        }

        /// <summary>
        /// Commits capital-ship hull and component state and removes fleets left without survivors.
        /// </summary>
        /// <param name="result">The combat result containing final ship damage.</param>
        /// <param name="attackerFleets">The attacking fleets to clean up.</param>
        /// <param name="defenderFleets">The defending fleets to clean up.</param>
        /// <returns>The ship damage and destruction events.</returns>
        private List<GameResult> ApplyShipResults(
            SpaceCombatResult result,
            IReadOnlyList<Fleet> attackerFleets,
            IReadOnlyList<Fleet> defenderFleets
        )
        {
            List<GameResult> events = ApplyShipDamage(result.ShipDamage, result.Planet);
            RemoveEmptyFleets(attackerFleets, defenderFleets);
            return events;
        }

        /// <summary>
        /// Applies fleet departures and blockade transitions, then records destroyed-unit outcomes.
        /// </summary>
        /// <param name="result">The combat result receiving final unit outcomes.</param>
        /// <param name="attackerWithdrawalPlan">The attacking side's reserved withdrawal.</param>
        /// <param name="defenderWithdrawalPlan">The defending side's reserved withdrawal.</param>
        /// <param name="wasBlockaded">Whether the combat planet was blockaded before unit results were committed.</param>
        /// <param name="events">The committed unit events receiving departure and blockade events.</param>
        /// <returns>The complete combat event collection.</returns>
        private List<GameResult> CompleteCombatResult(
            SpaceCombatResult result,
            SpaceCombatWithdrawalPlan attackerWithdrawalPlan,
            SpaceCombatWithdrawalPlan defenderWithdrawalPlan,
            bool wasBlockaded,
            List<GameResult> events
        )
        {
            BlockadeChangedResult blockade = _blockadeTransitionResolver.Resolve(
                result.Planet,
                wasBlockaded
            );
            if (blockade != null)
                events.Add(blockade);
            CompleteFleetWithdrawal(attackerWithdrawalPlan, events);
            CompleteFleetWithdrawal(defenderWithdrawalPlan, events);

            List<ISceneNode> destroyedUnits = events
                .OfType<GameObjectDestroyedResult>()
                .Select(destruction => destruction.DestroyedObject)
                .OfType<ISceneNode>()
                .ToList();
            CombatUnitSnapshot.RecordOutcomes(
                result.AttackingUnits,
                Enumerable.Empty<ISceneNode>(),
                destroyedUnits
            );
            CombatUnitSnapshot.RecordOutcomes(
                result.DefendingUnits,
                Enumerable.Empty<ISceneNode>(),
                destroyedUnits
            );
            return events;
        }

        /// <summary>
        /// Moves surviving fighter squadrons away from carriers that combat destroyed before those
        /// carrier subtrees are removed. Fighters use surviving carrier capacity first, then their
        /// own hyperdrives; squadrons with neither option are recorded as combat losses.
        /// </summary>
        /// <param name="result">The combat result whose losses identify destroyed carriers.</param>
        /// <param name="attackerFleets">The attacking fleets that participated.</param>
        /// <param name="defenderFleets">The defending fleets that participated.</param>
        /// <returns>Movement and destruction events produced while settling displaced squadrons.</returns>
        private List<GameResult> RecoverFightersFromDestroyedCarriers(
            SpaceCombatResult result,
            IReadOnlyList<Fleet> attackerFleets,
            IReadOnlyList<Fleet> defenderFleets
        )
        {
            HashSet<CapitalShip> destroyedCarriers = result
                .ShipDamage.Where(damage => damage?.Ship != null && damage.HullAfter <= 0)
                .Select(damage => damage.Ship)
                .ToHashSet();
            if (destroyedCarriers.Count == 0)
                return new List<GameResult>();

            List<GameResult> events = new List<GameResult>();
            List<FighterLossResult> unrecoverableLosses = new List<FighterLossResult>();
            RecoverFightersFromDestroyedCarriers(
                attackerFleets,
                destroyedCarriers,
                result.DefenderOutcome == SpaceCombatResult.SideOutcome.Active,
                unrecoverableLosses,
                events
            );
            RecoverFightersFromDestroyedCarriers(
                defenderFleets,
                destroyedCarriers,
                result.AttackerOutcome == SpaceCombatResult.SideOutcome.Active,
                unrecoverableLosses,
                events
            );
            foreach (FighterLossResult unrecoverableLoss in unrecoverableLosses)
            {
                FighterLossResult recordedLoss = result.FighterLosses.FirstOrDefault(loss =>
                    ReferenceEquals(loss.Fighter, unrecoverableLoss.Fighter)
                );
                if (recordedLoss == null)
                    result.FighterLosses.Add(unrecoverableLoss);
                else
                    recordedLoss.SquadsAfter = 0;
            }
            events.AddRange(ApplyFighterSquadronLosses(unrecoverableLosses, result.Planet));
            return events;
        }

        /// <summary>
        /// Recovers one side's surviving squadrons from destroyed carriers in stable fleet order.
        /// </summary>
        /// <param name="fleets">The side's participating fleets.</param>
        /// <param name="destroyedCarriers">The carriers that will be removed.</param>
        /// <param name="opposingForceRemains">Whether the opposing force still blocks departure.</param>
        /// <param name="unrecoverableLosses">Receives squadrons that cannot leave their carrier.</param>
        /// <param name="events">Receives movement results from independent evacuation.</param>
        private void RecoverFightersFromDestroyedCarriers(
            IReadOnlyList<Fleet> fleets,
            ISet<CapitalShip> destroyedCarriers,
            bool opposingForceRemains,
            ICollection<FighterLossResult> unrecoverableLosses,
            ICollection<GameResult> events
        )
        {
            List<CapitalShip> participatingShips = (fleets ?? Array.Empty<Fleet>())
                .Where(fleet => fleet != null)
                .SelectMany(SpaceCombatQueries.GetActiveCapitalShips)
                .Distinct()
                .ToList();
            List<Starfighter> displacedFighters = participatingShips
                .Where(destroyedCarriers.Contains)
                .SelectMany(ship => ship.GetChildren<Starfighter>(includeDisabled: true))
                .Where(SpaceCombatQueries.IsActiveStarfighter)
                .Distinct()
                .OrderBy(fighter => fighter.Hyperdrive > 0 ? 1 : 0)
                .ToList();
            if (displacedFighters.Count == 0)
                return;

            List<CapitalShip> survivingCarriers = participatingShips
                .Where(ship => !destroyedCarriers.Contains(ship) && ship.StarfighterCapacity > 0)
                .ToList();
            Dictionary<Starfighter, CapitalShip> recoveryAssignments =
                SpaceCombatWithdrawalPlanner.GetRecoveryAssignments(
                    survivingCarriers,
                    displacedFighters,
                    displacedFighters.ToHashSet()
                );

            _withdrawalResolver.ApplyFighterRecovery(recoveryAssignments);
            foreach (Starfighter fighter in displacedFighters)
            {
                if (recoveryAssignments.ContainsKey(fighter))
                    continue;

                if (
                    fighter.Hyperdrive > 0
                    && _movement.TryEvacuateToNearestFriendlyPlanet(
                        fighter,
                        events,
                        opposingBlockadeAtDeparture: opposingForceRemains
                    )
                )
                    continue;

                unrecoverableLosses.Add(
                    new FighterLossResult
                    {
                        Fighter = fighter,
                        SquadsBefore = fighter.CurrentSquadronSize,
                        SquadsAfter = 0,
                    }
                );
            }
        }

        /// <summary>
        /// Removes fleet containers left without capital ships.
        /// </summary>
        /// <param name="attackerFleets">The attacking fleets to clean up.</param>
        /// <param name="defenderFleets">The defending fleets to clean up.</param>
        private void RemoveEmptyFleets(
            IReadOnlyList<Fleet> attackerFleets,
            IReadOnlyList<Fleet> defenderFleets
        )
        {
            foreach (
                Fleet fleet in attackerFleets
                    .Concat(defenderFleets)
                    .Where(fleet => fleet?.GetChildren<CapitalShip>().Count == 0)
                    .ToList()
            )
            {
                RemoveFleetFromScene(fleet);
            }
        }

        /// <summary>
        /// Writes hull damage back to each ship and records destroyed ships for post-combat
        /// relocation reactions.
        /// </summary>
        /// <param name="damageResults">Ship damage entries produced by the battle.</param>
        /// <param name="planet">The planet where combat occurred.</param>
        /// <returns>A GameObjectDamagedResult per damaged ship.</returns>
        private List<GameResult> ApplyShipDamage(
            List<ShipDamageResult> damageResults,
            Planet planet
        )
        {
            List<GameResult> events = new List<GameResult>();
            HashSet<(string PlanetInstanceId, string OwnerInstanceId)> shipyardLocations = null;

            foreach (ShipDamageResult damage in damageResults)
            {
                CapitalShip ship = damage.Ship;
                if (ship == null)
                    continue;

                bool wasDamaged = ship.IsDamaged();
                ship.CurrentHullStrength = damage.HullAfter;
                if (damage.SystemDamage != null)
                    ship.SystemDamage = damage.SystemDamage.CreateCopy();

                if (damage.HullAfter < damage.HullBefore)
                    events.Add(
                        new GameObjectDamagedResult
                        {
                            GameObject = ship,
                            DamageValue = damage.HullBefore - damage.HullAfter,
                            Tick = _game.CurrentTick,
                        }
                    );

                if (damage.HullAfter <= 0)
                {
                    foreach (
                        ISceneNode unit in ship.GetChildren<Regiment>(includeDisabled: true)
                            .Cast<ISceneNode>()
                            .Concat(ship.GetChildren<SpecialForces>(includeDisabled: true))
                    )
                    {
                        events.Add(CreateCombatDestructionResult(unit, planet, _game.CurrentTick));
                    }

                    _game.DeleteNode(ship);
                    events.Add(CreateCombatDestructionResult(ship, planet, _game.CurrentTick));
                    GameLogger.Log($"Ship destroyed: {ship.GetDisplayName()}");
                }
                else if (!wasDamaged && ship.IsDamaged())
                {
                    shipyardLocations ??= RecoveryLocationRules.BuildShipyardIndex(_game);
                    ship.ScheduleRepair(
                        _game.CurrentTick,
                        ShipRecoveryRules.GetRepairInterval(_game, ship, shipyardLocations)
                    );
                }
            }

            return events;
        }

        /// <summary>
        /// Writes squadron-size losses back to each squadron and detaches any that are wiped out.
        /// </summary>
        /// <param name="lossResults">Fighter loss entries produced by the battle.</param>
        /// <param name="planet">The planet where combat occurred.</param>
        /// <returns>Destruction results for eliminated squadrons.</returns>
        private List<GameResult> ApplyFighterSquadronLosses(
            List<FighterLossResult> lossResults,
            Planet planet
        )
        {
            List<GameResult> events = new List<GameResult>();
            bool needsReplacementSchedule = lossResults.Any(loss =>
                loss.Fighter is Starfighter fighter
                && !fighter.HasLosses()
                && loss.SquadsAfter > 0
                && loss.SquadsAfter < fighter.MaxSquadronSize
            );
            HashSet<(string PlanetInstanceId, string OwnerInstanceId)> shipyardLocations =
                needsReplacementSchedule
                    ? RecoveryLocationRules.BuildShipyardIndex(_game)
                    : new HashSet<(string PlanetInstanceId, string OwnerInstanceId)>();
            foreach (FighterLossResult loss in lossResults)
            {
                Starfighter fighter = loss.Fighter;
                if (fighter == null)
                    continue;

                bool hadLosses = fighter.HasLosses();
                fighter.CurrentSquadronSize = loss.SquadsAfter;

                if (loss.SquadsAfter <= 0)
                {
                    _game.DeleteNode(fighter);
                    events.Add(CreateCombatDestructionResult(fighter, planet, _game.CurrentTick));
                    GameLogger.Log($"Fighter squadron destroyed: {fighter.GetDisplayName()}");
                }
                else if (!hadLosses && fighter.HasLosses())
                {
                    fighter.ScheduleReplacement(
                        _game.CurrentTick,
                        FighterRecoveryRules.GetReplacementInterval(
                            _game,
                            fighter,
                            shipyardLocations
                        )
                    );
                }
            }

            return events;
        }

        internal static GameObjectDestroyedResult CreateCombatDestructionResult(
            ISceneNode unit,
            Planet planet,
            int tick
        ) =>
            new()
            {
                DestroyedObject = unit,
                Context = planet,
                Reason = UnitDestructionReason.Combat,
                Tick = tick,
                CountsAsMilitaryLoss =
                    unit is CapitalShip or Starfighter
                    || unit
                        is Regiment
                        {
                            ManufacturingStatus: ManufacturingStatus.Complete,
                            Movement: null
                        },
            };

        /// <summary>
        /// Removes a fleet with no remaining capital ships from the scene graph.
        /// </summary>
        /// <param name="fleet">Empty fleet to remove.</param>
        private void RemoveFleetFromScene(Fleet fleet)
        {
            _game.DeleteNode(fleet);
            GameLogger.Log($"Fleet destroyed: {fleet.GetDisplayName()}");
        }
    }
}
