using System;
using System.Collections.Generic;
using System.Linq;
using Rebellion.Game;
using Rebellion.Game.Results;
using Rebellion.Game.Units;
using Rebellion.SceneGraph;
using Rebellion.Util.Random;

namespace Rebellion.Gameplay
{
    /// <summary>
    /// Simulates an interactive space battle on a two-dimensional plane using the same weapon,
    /// shield, and withdrawal rules as automatic resolution.
    /// </summary>
    public sealed class TacticalSpaceBattle
    {
        public const double RoundSeconds = 0.5;

        private const double _stepRounds = 0.25;
        private const double _arenaHeightRatio = 0.52;
        private const double _withdrawalMargin = 2;
        private const double _shotDisplaySeconds = 0.3;
        private const double _arenaScale = 3;
        private const double _spawnLineFraction = 0.8;
        private const double _spawnCornerFraction = 0.6;
        private const double _shipSpacing = 8;
        private const double _shipClearance = 1.1;
        private const double _fighterLaunchOffset = 3;
        private const double _fighterLaunchSpacing = 2;
        private const double _planetaryFighterOffset = 8;
        private const double _arrivalTolerance = 0.5;
        private const double _orbitRadiusFraction = 0.6;
        private const double _orbitRangeCeiling = 0.9;
        private const double _patrolClearance = 6;
        private const double _pinnedTravelFraction = 0.25;
        private const double _attackApproachFraction = 0.85;
        private const double _roundEpsilon = 1e-9;

        private readonly GameConfig.SpaceCombatConfig _config;
        private readonly IRandomNumberProvider _random;
        private readonly TacticalBattleForce _attacker;
        private readonly TacticalBattleForce _defender;
        private readonly List<TacticalBattleUnit> _units;
        private readonly List<TacticalShot> _shots = new List<TacticalShot>();
        private readonly Dictionary<TacticalBattleUnit, List<PendingHit>> _pendingHits =
            new Dictionary<TacticalBattleUnit, List<PendingHit>>();
        private readonly double _scanInterval;
        private readonly List<TacticalBattleEvent> _events = new List<TacticalBattleEvent>();
        private readonly HashSet<TacticalBattleUnit> _reportedDestroyed =
            new HashSet<TacticalBattleUnit>();
        private readonly HashSet<TacticalBattleUnit> _reportedWithdrawn =
            new HashSet<TacticalBattleUnit>();
        private double _pendingRounds;
        private double _roundProgress;
        private int _roundsCompleted;
        private int _stagnantRounds;
        private double _previousAttackerDurability;
        private double _previousDefenderDurability;
        private double _previousAttackerProgress;
        private double _previousDefenderProgress;
        private bool _isComplete;

        public CombatSide? PlayerSide { get; }

        public double HalfWidth { get; }

        public double HalfHeight { get; }

        public bool IsComplete => _isComplete;

        public double ElapsedSeconds { get; private set; }

        public int RoundsCompleted => _roundsCompleted;

        public IReadOnlyList<TacticalBattleUnit> Units => _units;

        public IReadOnlyList<TacticalShot> Shots => _shots;

        /// <summary>
        /// Creates a tactical battle between two forces.
        /// </summary>
        /// <param name="config">The space-combat parameters.</param>
        /// <param name="random">The persisted game random-number stream.</param>
        /// <param name="attackerShips">The attacking capital ships.</param>
        /// <param name="attackerFighters">The attacking fighter squadrons.</param>
        /// <param name="defenderShips">The defending capital ships.</param>
        /// <param name="defenderFighters">The defending fighter squadrons.</param>
        /// <param name="attackerCanWithdraw">Whether the attacking force can withdraw.</param>
        /// <param name="defenderCanWithdraw">Whether the defending force can withdraw.</param>
        /// <param name="attackerCommand">The attacking side's tactical command bonuses.</param>
        /// <param name="defenderCommand">The defending side's tactical command bonuses.</param>
        /// <param name="playerSide">The side the player commands, or null when both sides are automatic.</param>
        public TacticalSpaceBattle(
            GameConfig.SpaceCombatConfig config,
            IRandomNumberProvider random,
            IReadOnlyList<CapitalShip> attackerShips,
            IReadOnlyList<Starfighter> attackerFighters,
            IReadOnlyList<CapitalShip> defenderShips,
            IReadOnlyList<Starfighter> defenderFighters,
            bool attackerCanWithdraw,
            bool defenderCanWithdraw,
            SpaceCombatCommandModifiers attackerCommand,
            SpaceCombatCommandModifiers defenderCommand,
            CombatSide? playerSide
        )
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _random = random ?? throw new ArgumentNullException(nameof(random));
            if (playerSide == CombatSide.Draw)
                throw new ArgumentOutOfRangeException(nameof(playerSide));

            PlayerSide = playerSide;
            _attacker = new TacticalBattleForce(
                CombatSide.Attacker,
                attackerShips,
                attackerFighters,
                attackerCanWithdraw,
                playerSide != CombatSide.Attacker,
                config,
                attackerCommand
            );
            _defender = new TacticalBattleForce(
                CombatSide.Defender,
                defenderShips,
                defenderFighters,
                defenderCanWithdraw,
                playerSide != CombatSide.Defender,
                config,
                defenderCommand
            );
            _units = new List<TacticalBattleUnit>(_attacker.Units.Count + _defender.Units.Count);
            _units.AddRange(_attacker.Units);
            _units.AddRange(_defender.Units);
            HalfWidth = Math.Max(config.AutoResolveStartingDistance, 1) * _arenaScale;
            HalfHeight = HalfWidth * _arenaHeightRatio;
            _scanInterval = Math.Max(config.AutoResolveTargetScanDivisor, 1);
            PlaceForce(_attacker);
            PlaceForce(_defender);
            SeparateShips();
            _previousAttackerDurability = _attacker.GetDurability();
            _previousDefenderDurability = _defender.GetDurability();
            _previousAttackerProgress = _attacker.GetWithdrawalProgress();
            _previousDefenderProgress = _defender.GetWithdrawalProgress();
        }

        /// <summary>
        /// Returns whether a side is permitted to withdraw from the battle.
        /// </summary>
        /// <param name="side">The side to inspect.</param>
        /// <returns>True when the side can withdraw.</returns>
        public bool CanWithdraw(CombatSide side)
        {
            return GetForce(side).CanWithdraw;
        }

        /// <summary>
        /// Returns whether a side has already been ordered to withdraw.
        /// </summary>
        /// <param name="side">The side to inspect.</param>
        /// <returns>True when withdrawal has been ordered.</returns>
        public bool HasOrderedWithdrawal(CombatSide side)
        {
            return GetForce(side).WithdrawalOrdered;
        }

        /// <summary>
        /// Returns whether the simulation controls a side.
        /// </summary>
        /// <param name="side">The side to inspect.</param>
        /// <returns>True when the side is automatically controlled.</returns>
        public bool IsAutomatic(CombatSide side)
        {
            return GetForce(side).IsAutomatic;
        }

        /// <summary>
        /// Returns a side's current combat outcome.
        /// </summary>
        /// <param name="side">The side to inspect.</param>
        /// <returns>The side's outcome.</returns>
        public SpaceCombatSideOutcome GetOutcome(CombatSide side)
        {
            return GetForce(side).Outcome;
        }

        /// <summary>
        /// Returns the share of a side's starting durability that remains in the fight.
        /// </summary>
        /// <param name="side">The side to inspect.</param>
        /// <returns>The remaining durability fraction.</returns>
        public double GetStrengthFraction(CombatSide side)
        {
            TacticalBattleForce force = GetForce(side);
            return force.InitialDurability > 0
                ? force.GetDurability() / force.InitialDurability
                : 0;
        }

        /// <summary>
        /// Counts a side's capital ships that remain in the fight.
        /// </summary>
        /// <param name="side">The side to inspect.</param>
        /// <returns>The targetable capital-ship count.</returns>
        public int CountShips(CombatSide side)
        {
            return GetForce(side).Ships.Count(ship => ship.IsTargetable);
        }

        /// <summary>
        /// Counts a side's fighter squadrons that remain in the fight.
        /// </summary>
        /// <param name="side">The side to inspect.</param>
        /// <returns>The targetable squadron count.</returns>
        public int CountFighters(CombatSide side)
        {
            return GetForce(side).Fighters.Count(fighter => fighter.IsTargetable);
        }

        /// <summary>
        /// Hands the player's side to the simulation or returns it to manual command.
        /// </summary>
        /// <param name="side">The player's side.</param>
        /// <param name="automatic">Whether the simulation should control the side.</param>
        public void SetAutomatic(CombatSide side, bool automatic)
        {
            if (side != PlayerSide)
                throw new InvalidOperationException("Only the player's side can change control.");

            TacticalBattleForce force = GetForce(side);
            if (force.IsAutomatic == automatic)
                return;

            force.IsAutomatic = automatic;
            force.ClearOrders();
        }

        /// <summary>
        /// Orders every surviving unit on a side to leave the battle.
        /// </summary>
        /// <param name="side">The withdrawing side.</param>
        /// <returns>True when withdrawal began.</returns>
        public bool OrderWithdrawal(CombatSide side)
        {
            TacticalBattleForce force = GetForce(side);
            if (_isComplete || !force.CanWithdraw || force.WithdrawalOrdered)
                return false;

            force.BeginWithdrawal();
            return true;
        }

        /// <summary>
        /// Returns whether the player can currently issue orders to a unit.
        /// </summary>
        /// <param name="unit">The unit to inspect.</param>
        /// <returns>True when the unit accepts player orders.</returns>
        public bool CanCommand(TacticalBattleUnit unit)
        {
            if (
                _isComplete
                || PlayerSide == null
                || unit is not TacticalCapitalShipUnit ship
                || ship.Side != PlayerSide.Value
                || !ship.IsTargetable
            )
                return false;

            TacticalBattleForce force = GetForce(ship.Side);
            return !force.IsAutomatic && !force.WithdrawalOrdered;
        }

        /// <summary>
        /// Orders the commandable ships in a selection to travel toward a position in formation.
        /// </summary>
        /// <param name="units">The selected units.</param>
        /// <param name="x">The destination horizontal coordinate.</param>
        /// <param name="y">The destination vertical coordinate.</param>
        public void IssueMoveOrder(IEnumerable<TacticalBattleUnit> units, double x, double y)
        {
            List<TacticalCapitalShipUnit> ships = GetCommandableShips(units);
            if (ships.Count == 0)
                return;

            double centroidX = ships.Average(ship => ship.X);
            double centroidY = ships.Average(ship => ship.Y);
            foreach (TacticalCapitalShipUnit ship in ships)
            {
                ship.SetMoveOrder(
                    ClampX(x + ship.X - centroidX, ship.Radius),
                    ClampY(y + ship.Y - centroidY, ship.Radius)
                );
            }
        }

        /// <summary>
        /// Returns whether the player may change a unit's target priority: the unit must be on
        /// the player's side and still in the fight.
        /// </summary>
        /// <param name="unit">The unit to check.</param>
        /// <returns>True when the priority can be changed.</returns>
        public bool CanSetTargetPriority(TacticalBattleUnit unit)
        {
            return !_isComplete
                && PlayerSide != null
                && unit != null
                && unit.Side == PlayerSide.Value
                && unit.IsTargetable;
        }

        /// <summary>
        /// Changes which enemy class the player's units engage first; each affected unit picks
        /// a new automatic target on the next step. Units the player may not direct are skipped.
        /// </summary>
        /// <param name="units">The units to change.</param>
        /// <param name="priority">The class to engage first.</param>
        public void SetTargetPriority(
            IEnumerable<TacticalBattleUnit> units,
            TacticalTargetPriority priority
        )
        {
            if (units == null)
                throw new ArgumentNullException(nameof(units));

            foreach (TacticalBattleUnit unit in units)
            {
                if (!CanSetTargetPriority(unit) || unit.TargetPriority == priority)
                    continue;

                unit.TargetPriority = priority;
                if (unit is TacticalCapitalShipUnit ship)
                    ship.RequestScan();
                else if (unit is TacticalStarfighterUnit fighter)
                    fighter.RequestScan();
            }
        }

        /// <summary>
        /// Orders the commandable ships in a selection to pursue and engage an opposing unit.
        /// </summary>
        /// <param name="units">The selected units.</param>
        /// <param name="target">The opposing unit to engage.</param>
        public void IssueAttackOrder(
            IEnumerable<TacticalBattleUnit> units,
            TacticalBattleUnit target
        )
        {
            if (target?.IsTargetable != true || target.Side == PlayerSide)
                return;

            foreach (TacticalCapitalShipUnit ship in GetCommandableShips(units))
                ship.SetAttackOrder(target);
        }

        /// <summary>
        /// Finds the surviving unit closest to a position within its selection radius.
        /// </summary>
        /// <param name="x">The horizontal coordinate.</param>
        /// <param name="y">The vertical coordinate.</param>
        /// <param name="tolerance">Extra distance accepted beyond each unit's radius.</param>
        /// <returns>The closest matching unit, or null.</returns>
        public TacticalBattleUnit FindUnitAt(double x, double y, double tolerance)
        {
            TacticalBattleUnit nearest = null;
            double nearestDistance = double.MaxValue;
            foreach (TacticalBattleUnit unit in _units)
            {
                if (!unit.IsAlive || unit.HasWithdrawn)
                    continue;

                double distance = TacticalBattleGeometry.GetDistance(unit.X, unit.Y, x, y);
                if (distance > unit.Radius + tolerance || distance >= nearestDistance)
                    continue;

                nearest = unit;
                nearestDistance = distance;
            }

            return nearest;
        }

        /// <summary>
        /// Returns and clears the presentation events raised since the previous call.
        /// </summary>
        /// <returns>The pending events in the order they occurred.</returns>
        public IReadOnlyList<TacticalBattleEvent> TakeEvents()
        {
            if (_events.Count == 0)
                return Array.Empty<TacticalBattleEvent>();

            List<TacticalBattleEvent> taken = new List<TacticalBattleEvent>(_events);
            _events.Clear();
            return taken;
        }

        /// <summary>
        /// Advances the battle by elapsed real time.
        /// </summary>
        /// <param name="seconds">The elapsed seconds, already scaled by the playback speed.</param>
        public void Advance(double seconds)
        {
            if (seconds <= 0)
                return;

            AgeShots(seconds);
            if (_isComplete)
                return;

            ElapsedSeconds += seconds;
            _pendingRounds += seconds / RoundSeconds;
            while (_pendingRounds >= _stepRounds && !_isComplete)
            {
                _pendingRounds -= _stepRounds;
                Step(_stepRounds);
                PublishUnitStateEvents();
            }
        }

        /// <summary>
        /// Creates the authoritative result of the finished battle.
        /// </summary>
        /// <returns>The resolved space-combat result.</returns>
        public SpaceCombatResult CreateResult()
        {
            if (!_isComplete)
                throw new InvalidOperationException("The tactical battle has not finished.");

            SpaceCombatResult result = new SpaceCombatResult
            {
                AttackerOutcome = _attacker.Outcome,
                DefenderOutcome = _defender.Outcome,
                IterationsCompleted = _roundsCompleted,
            };

            AddShipResults(result, _attacker.Ships);
            AddShipResults(result, _defender.Ships);
            AddFighterResults(result, _attacker.Fighters);
            AddFighterResults(result, _defender.Fighters);
            result.AttackingUnits.AddRange(CaptureCombatUnits(_attacker));
            result.DefendingUnits.AddRange(CaptureCombatUnits(_defender));
            result.Winner = DetermineWinner();
            result.WithdrawnUnits.AddRange(
                _units.Where(unit => unit.HasWithdrawn).Select(unit => unit.Node)
            );
            return result;
        }

        /// <summary>
        /// Runs one fixed simulation step.
        /// </summary>
        /// <param name="rounds">The combat rounds covered by the step.</param>
        private void Step(double rounds)
        {
            if (CompleteEliminatedForces())
            {
                Finish();
                return;
            }

            WithdrawAtThreshold(_attacker, _defender);
            WithdrawAtThreshold(_defender, _attacker);
            UpdateAutomaticTargets(_attacker, _defender, rounds);
            UpdateAutomaticTargets(_defender, _attacker, rounds);
            MoveUnits(_attacker, rounds);
            MoveUnits(_defender, rounds);
            SeparateShips();
            UpdateTractorBeams();
            QueueAttacks(_attacker, _defender);
            QueueAttacks(_defender, _attacker);
            ApplyPendingHits();
            AdvanceUnitStates(_attacker, rounds);
            AdvanceUnitStates(_defender, rounds);
            CompleteReadyWithdrawals(_attacker);
            CompleteReadyWithdrawals(_defender);

            _roundProgress += rounds;
            if (_roundProgress < 1 - _roundEpsilon)
                return;

            _roundProgress -= 1;
            if (CompleteRound())
                Finish();
        }

        /// <summary>
        /// Records one completed round and resolves stalemates.
        /// </summary>
        /// <returns>True when the battle was resolved by stalemate.</returns>
        private bool CompleteRound()
        {
            _roundsCompleted++;
            double attackerDurability = _attacker.GetDurability();
            double defenderDurability = _defender.GetDurability();
            double attackerProgress = _attacker.GetWithdrawalProgress();
            double defenderProgress = _defender.GetWithdrawalProgress();
            bool stateChanged =
                Math.Abs(attackerDurability - _previousAttackerDurability) > double.Epsilon
                || Math.Abs(defenderDurability - _previousDefenderDurability) > double.Epsilon
                || Math.Abs(attackerProgress - _previousAttackerProgress) > double.Epsilon
                || Math.Abs(defenderProgress - _previousDefenderProgress) > double.Epsilon;
            _stagnantRounds = stateChanged ? 0 : _stagnantRounds + 1;
            _previousAttackerDurability = attackerDurability;
            _previousDefenderDurability = defenderDurability;
            _previousAttackerProgress = attackerProgress;
            _previousDefenderProgress = defenderProgress;

            bool neitherCanHarm =
                _attacker.HasCombatants
                && _defender.HasCombatants
                && !_attacker.HasPendingWithdrawal
                && !_defender.HasPendingWithdrawal
                && _attacker.GetTacticalStrength(_defender) <= 0
                && _defender.GetTacticalStrength(_attacker) <= 0;
            if (
                !neitherCanHarm
                && _stagnantRounds < _config.AutoResolveStagnationIterations
                && _roundsCompleted < _config.AutoResolveMaximumIterations
            )
                return false;

            ResolveStalemate();
            return true;
        }

        /// <summary>
        /// Completes any force without survivors and marks the battle finished.
        /// </summary>
        private void Finish()
        {
            if (!CompleteEliminatedForces())
            {
                bool attackerActive = _attacker.Outcome == SpaceCombatSideOutcome.Active;
                bool defenderActive = _defender.Outcome == SpaceCombatSideOutcome.Active;
                if (attackerActive && defenderActive)
                    ResolveStalemate();
                else if (attackerActive)
                    CompleteStalematedForce(_attacker);
                else if (defenderActive)
                    CompleteStalematedForce(_defender);
            }

            _isComplete = true;
        }

        /// <summary>
        /// Completes sides without units remaining in combat.
        /// </summary>
        /// <returns>True when combat has ended.</returns>
        private bool CompleteEliminatedForces()
        {
            bool attackerUnresolved = _attacker.HasUnresolvedUnits;
            bool defenderUnresolved = _defender.HasUnresolvedUnits;
            if (attackerUnresolved && defenderUnresolved)
                return false;

            if (!attackerUnresolved)
                CompleteForce(_attacker);
            if (!defenderUnresolved)
                CompleteForce(_defender);
            return !_attacker.HasPendingWithdrawal && !_defender.HasPendingWithdrawal;
        }

        /// <summary>
        /// Assigns the completed state of a force with no remaining combatants.
        /// </summary>
        /// <param name="force">The completed force.</param>
        private static void CompleteForce(TacticalBattleForce force)
        {
            force.Outcome = force.HasWithdrawnUnits
                ? SpaceCombatSideOutcome.Withdrawn
                : SpaceCombatSideOutcome.Destroyed;
        }

        /// <summary>
        /// Begins withdrawal for an automatically controlled force that has fallen below the
        /// configured share of opposing strength.
        /// </summary>
        /// <param name="force">The force whose withdrawal state is evaluated.</param>
        /// <param name="opposingForce">The force providing the opposing tactical strength.</param>
        private void WithdrawAtThreshold(
            TacticalBattleForce force,
            TacticalBattleForce opposingForce
        )
        {
            if (
                !force.IsAutomatic
                || force.WithdrawalOrdered
                || !force.CanWithdraw
                || !force.HasCombatants
            )
                return;

            double opposingStrength = opposingForce.GetRetreatStrength();
            if (opposingStrength <= 0)
                return;

            if (
                force.GetRetreatStrength() / opposingStrength
                < _config.AutoResolveRetreatStrengthRatio
            )
                force.BeginWithdrawal();
        }

        /// <summary>
        /// Refreshes automatic targets for units without standing player orders.
        /// </summary>
        /// <param name="force">The force whose targets are refreshed.</param>
        /// <param name="opposingForce">The force providing targets.</param>
        /// <param name="rounds">The elapsed combat rounds.</param>
        private void UpdateAutomaticTargets(
            TacticalBattleForce force,
            TacticalBattleForce opposingForce,
            double rounds
        )
        {
            IReadOnlyList<TacticalBattleUnit> targets = opposingForce.GetTargetableUnits();
            foreach (TacticalCapitalShipUnit ship in force.Ships)
            {
                if (!ship.IsTargetable)
                    continue;

                ship.AdvanceScanCooldown(rounds);
                if (ship.Order == TacticalShipOrder.Attack && ship.Target?.IsTargetable != true)
                    ship.ClearOrder();
                if (!ship.IsAutomatic)
                    continue;

                // Stay on the current target until it is gone; the periodic scan only looks for
                // a preferred-class target while the ship is making do with the other class.
                if (ship.Target?.IsTargetable != true || ShouldRetarget(ship, ship.IsScanDue))
                    ship.SetAutomaticTarget(SelectShipTarget(ship, targets), _scanInterval);
            }

            foreach (TacticalStarfighterUnit fighter in force.Fighters)
            {
                if (!fighter.IsTargetable)
                    continue;

                fighter.AdvanceScanCooldown(rounds);
                if (
                    fighter.Target?.IsTargetable != true
                    || ShouldRetarget(fighter, fighter.IsScanDue)
                )
                    fighter.SetTarget(SelectFighterTarget(fighter, targets), _scanInterval);
            }
        }

        /// <summary>
        /// Returns whether a unit with a live target should look again: only when its scan is
        /// due and the target is not of the class it prefers.
        /// </summary>
        /// <param name="unit">The unit holding a target.</param>
        /// <param name="scanDue">Whether the unit's scan cooldown has elapsed.</param>
        /// <returns>True when the unit should pick a target again.</returns>
        private static bool ShouldRetarget(TacticalBattleUnit unit, bool scanDue)
        {
            TacticalBattleUnit target =
                unit is TacticalCapitalShipUnit ship ? ship.Target
                : unit is TacticalStarfighterUnit fighter ? fighter.Target
                : null;
            return scanDue && target != null && !unit.PrefersTarget(target);
        }

        /// <summary>
        /// Selects the most attractive target for an automatically engaging capital ship.
        /// </summary>
        /// <param name="ship">The firing ship.</param>
        /// <param name="targets">The targetable opposing units.</param>
        /// <returns>The selected target, or null when nothing can be engaged.</returns>
        private static TacticalBattleUnit SelectShipTarget(
            TacticalCapitalShipUnit ship,
            IReadOnlyList<TacticalBattleUnit> targets
        )
        {
            return SelectPreferredTarget(ship, targets, ship.ScoreTarget);
        }

        /// <summary>
        /// Picks the nearest engageable target from the unit's preferred class, falling back to
        /// the nearest engageable target of the other class only when none is preferred.
        /// </summary>
        /// <param name="unit">The unit choosing a target.</param>
        /// <param name="targets">The targetable enemy units.</param>
        /// <param name="score">Scores a candidate; zero or less means it cannot be engaged.</param>
        /// <returns>The chosen target, or null when nothing can be engaged.</returns>
        private static TacticalBattleUnit SelectPreferredTarget(
            TacticalBattleUnit unit,
            IReadOnlyList<TacticalBattleUnit> targets,
            Func<TacticalBattleUnit, double> score
        )
        {
            TacticalBattleUnit preferred = null;
            double preferredDistance = double.MaxValue;
            TacticalBattleUnit fallback = null;
            double fallbackDistance = double.MaxValue;
            foreach (TacticalBattleUnit candidate in targets)
            {
                if (score(candidate) <= 0)
                    continue;

                double distance = unit.GetDistanceTo(candidate);
                if (unit.PrefersTarget(candidate))
                {
                    if (distance < preferredDistance)
                    {
                        preferred = candidate;
                        preferredDistance = distance;
                    }
                }
                else if (distance < fallbackDistance)
                {
                    fallback = candidate;
                    fallbackDistance = distance;
                }
            }

            return preferred ?? fallback;
        }

        /// <summary>
        /// Selects the most attractive target for a fighter squadron.
        /// </summary>
        /// <param name="fighter">The firing squadron.</param>
        /// <param name="targets">The targetable opposing units.</param>
        /// <returns>The selected target, or null when nothing can be engaged.</returns>
        private static TacticalBattleUnit SelectFighterTarget(
            TacticalStarfighterUnit fighter,
            IReadOnlyList<TacticalBattleUnit> targets
        )
        {
            return SelectPreferredTarget(fighter, targets, fighter.ScoreTarget);
        }

        /// <summary>
        /// Moves every surviving unit in a force according to its orders.
        /// </summary>
        /// <param name="force">The force to move.</param>
        /// <param name="rounds">The elapsed combat rounds.</param>
        private void MoveUnits(TacticalBattleForce force, double rounds)
        {
            foreach (TacticalBattleUnit unit in force.Units)
            {
                if (!unit.IsAlive || unit.HasWithdrawn)
                    continue;

                if (unit is TacticalCapitalShipUnit ship)
                    MoveShip(ship, force, rounds);
                else if (unit is TacticalStarfighterUnit fighter)
                    MoveFighter(fighter, force, rounds);
            }
        }

        /// <summary>
        /// Locks tractor beams: a ship with beam power holds its capital-ship target while the
        /// target's hull lies within beam range, and the strongest beam on a target wins.
        /// </summary>
        private void UpdateTractorBeams()
        {
            foreach (TacticalCapitalShipUnit ship in _attacker.Ships)
                ship.TractoredBy = null;
            foreach (TacticalCapitalShipUnit ship in _defender.Ships)
                ship.TractoredBy = null;

            LockTractorBeams(_attacker);
            LockTractorBeams(_defender);
        }

        /// <summary>
        /// Applies one force's tractor beams to their current targets.
        /// </summary>
        /// <param name="force">The force whose beams are evaluated.</param>
        private static void LockTractorBeams(TacticalBattleForce force)
        {
            foreach (TacticalCapitalShipUnit ship in force.Ships)
            {
                if (!ship.IsTargetable || !ship.HasTractorBeam)
                {
                    ship.HeldShip = null;
                    continue;
                }

                // A beam keeps hold of a ship that is running for hyperspace until its hull slips
                // out of range; otherwise it follows the ship's current target.
                TacticalCapitalShipUnit held =
                    ship.HeldShip?.IsAlive == true
                    && ship.HeldShip.IsWithdrawing
                    && !ship.HeldShip.HasWithdrawn
                        ? ship.HeldShip
                        : ship.Target as TacticalCapitalShipUnit;
                if (
                    held?.IsAlive != true
                    || held.HasWithdrawn
                    || ship.GetEngagementDistanceTo(held) > ship.TractorBeamRange
                )
                {
                    ship.HeldShip = null;
                    continue;
                }

                ship.HeldShip = held;
                if (
                    held.TractoredBy == null
                    || held.TractoredBy.TractorBeamPower < ship.TractorBeamPower
                )
                    held.TractoredBy = ship;
            }
        }

        /// <summary>
        /// Pushes capital ships apart so their hull footprints never overlap, whichever side they
        /// belong to; each ship in an overlapping pair yields half of the shortfall.
        /// </summary>
        private void SeparateShips()
        {
            List<TacticalCapitalShipUnit> ships = new List<TacticalCapitalShipUnit>();
            foreach (TacticalCapitalShipUnit ship in _attacker.Ships)
            {
                if (ship.IsAlive && !ship.HasWithdrawn)
                    ships.Add(ship);
            }

            foreach (TacticalCapitalShipUnit ship in _defender.Ships)
            {
                if (ship.IsAlive && !ship.HasWithdrawn)
                    ships.Add(ship);
            }

            for (int first = 0; first < ships.Count; first++)
            {
                for (int second = first + 1; second < ships.Count; second++)
                {
                    TacticalCapitalShipUnit a = ships[first];
                    TacticalCapitalShipUnit b = ships[second];
                    double minimum = (a.HullFootprint + b.HullFootprint) * _shipClearance;
                    double distance = a.GetDistanceTo(b);
                    if (distance >= minimum)
                        continue;

                    double angle =
                        distance > _arrivalTolerance
                            ? a.GetAngleTo(b)
                            : (a.Side == CombatSide.Attacker ? Math.PI / 2 : -Math.PI / 2);
                    double shift = (minimum - distance) / 2;
                    a.X -= Math.Cos(angle) * shift;
                    a.Y -= Math.Sin(angle) * shift;
                    b.X += Math.Cos(angle) * shift;
                    b.Y += Math.Sin(angle) * shift;
                    ClampToArena(a);
                    ClampToArena(b);
                }
            }
        }

        /// <summary>
        /// Moves one capital ship according to its withdrawal state or standing order.
        /// </summary>
        /// <param name="ship">The capital ship.</param>
        /// <param name="force">The ship's force.</param>
        /// <param name="rounds">The elapsed combat rounds.</param>
        private void MoveShip(
            TacticalCapitalShipUnit ship,
            TacticalBattleForce force,
            double rounds
        )
        {
            if (ship.IsWithdrawing)
            {
                SteerShip(ship, GetRetreatX(force.Side), ship.Y, rounds, clamp: false);
                return;
            }

            if (ship.Order == TacticalShipOrder.Move)
            {
                if (
                    !ship.HasDestination
                    || SteerShip(ship, ship.DestinationX, ship.DestinationY, rounds, clamp: true)
                )
                    ship.ClearOrder();
                return;
            }

            TacticalBattleUnit target = ship.Target;
            if (target?.IsTargetable != true || !ship.IsArmed)
                return;

            double preferredRange = ship.GetPreferredRange(target);
            if (ship.GetDistanceTo(target) > preferredRange)
            {
                double angle = ship.GetAngleTo(target);
                double approach = preferredRange * _attackApproachFraction;
                SteerShip(
                    ship,
                    target.X - Math.Cos(angle) * approach,
                    target.Y - Math.Sin(angle) * approach,
                    rounds,
                    clamp: true
                );
                return;
            }

            ship.Heading = TacticalBattleGeometry.TurnToward(
                ship.Heading,
                ship.GetDesiredFacing(target),
                ship.TurnRate * rounds
            );
        }

        /// <summary>
        /// Steers a capital ship toward a position at its sublight speed.
        /// </summary>
        /// <param name="ship">The capital ship.</param>
        /// <param name="x">The destination horizontal coordinate.</param>
        /// <param name="y">The destination vertical coordinate.</param>
        /// <param name="rounds">The elapsed combat rounds.</param>
        /// <param name="clamp">Whether the ship stays inside the arena.</param>
        /// <returns>True when the ship has reached the position.</returns>
        private bool SteerShip(
            TacticalCapitalShipUnit ship,
            double x,
            double y,
            double rounds,
            bool clamp
        )
        {
            double distance = TacticalBattleGeometry.GetDistance(ship.X, ship.Y, x, y);
            if (distance <= _arrivalTolerance)
                return true;

            double angle = TacticalBattleGeometry.GetAngle(ship.X, ship.Y, x, y);
            ship.Heading = TacticalBattleGeometry.TurnToward(
                ship.Heading,
                angle,
                ship.TurnRate * rounds
            );
            double travel = Math.Min(ship.Speed * rounds, distance);
            ship.X += Math.Cos(angle) * travel;
            ship.Y += Math.Sin(angle) * travel;
            if (clamp)
                ClampToArena(ship);
            return distance - travel <= _arrivalTolerance;
        }

        /// <summary>
        /// Moves one fighter squadron toward its target, carrier, or escape edge.
        /// </summary>
        /// <param name="fighter">The fighter squadron.</param>
        /// <param name="force">The squadron's force.</param>
        /// <param name="rounds">The elapsed combat rounds.</param>
        private void MoveFighter(
            TacticalStarfighterUnit fighter,
            TacticalBattleForce force,
            double rounds
        )
        {
            if (fighter.IsWithdrawing)
            {
                TacticalCapitalShipUnit carrier = fighter.CanWithdrawIndependently
                    ? null
                    : FindRecoveryCarrier(fighter, force);
                if (carrier == null)
                {
                    MoveDirect(fighter, GetRetreatX(force.Side), fighter.Y, rounds, clamp: false);
                    return;
                }

                // Recovering squadrons close on their carrier and circle it until it jumps.
                double carrierDistance = fighter.GetDistanceTo(carrier);
                double recoveryRadius =
                    carrier.HullFootprint + _patrolClearance * fighter.OrbitRadiusScale;
                if (carrierDistance > recoveryRadius * 2)
                    MoveDirect(fighter, carrier.X, carrier.Y, rounds, clamp: false);
                else
                    Orbit(fighter, carrier.X, carrier.Y, carrierDistance, recoveryRadius, rounds);
                return;
            }

            TacticalBattleUnit target = fighter.Target;
            double engagementRange =
                target?.IsTargetable == true ? fighter.GetEngagementRange(target.IsStarfighter) : 0;
            if (engagementRange <= 0)
            {
                // Nothing to engage: fly a patrol circle around the nearest friendly ship.
                PatrolNearFleet(fighter, force, rounds);
                return;
            }

            double distance = fighter.GetDistanceTo(target);
            if (fighter.GetEngagementDistanceTo(target) <= engagementRange)
            {
                OrbitTarget(fighter, target, distance, engagementRange, rounds);
                return;
            }

            MoveDirect(
                fighter,
                target.X,
                target.Y,
                rounds,
                clamp: true,
                engagementRange + target.HullFootprint
            );
        }

        /// <summary>
        /// Circles a squadron around its target inside weapon range: it flies along the orbit
        /// tangent, steering inward when it drifts too far and outward when it gets too close.
        /// </summary>
        /// <param name="fighter">The orbiting squadron.</param>
        /// <param name="target">The unit being attacked.</param>
        /// <param name="distance">The current distance to the target.</param>
        /// <param name="engagementRange">The squadron's engagement range against the target.</param>
        /// <param name="rounds">The rounds of movement to apply.</param>
        private void OrbitTarget(
            TacticalStarfighterUnit fighter,
            TacticalBattleUnit target,
            double distance,
            double engagementRange,
            double rounds
        )
        {
            // Circle just outside the target's hull footprint, inside weapon range of its surface;
            // each squadron's own radius scale puts it on a different ring.
            double orbitRadius =
                target.HullFootprint
                + Math.Min(
                    Math.Max(engagementRange * _orbitRadiusFraction, fighter.Radius)
                        * fighter.OrbitRadiusScale,
                    engagementRange * _orbitRangeCeiling
                );
            Orbit(fighter, target.X, target.Y, distance, orbitRadius, rounds);
        }

        /// <summary>
        /// Keeps an idle squadron flying: it circles the nearest friendly capital ship just
        /// outside its hull, or holds position when the fleet has no ships left.
        /// </summary>
        /// <param name="fighter">The squadron without a target it can engage.</param>
        /// <param name="force">The squadron's force.</param>
        /// <param name="rounds">The rounds of movement to apply.</param>
        private void PatrolNearFleet(
            TacticalStarfighterUnit fighter,
            TacticalBattleForce force,
            double rounds
        )
        {
            TacticalCapitalShipUnit anchor = null;
            double anchorDistance = double.MaxValue;
            foreach (TacticalCapitalShipUnit ship in force.Ships)
            {
                if (!ship.IsAlive || ship.HasWithdrawn)
                    continue;

                double distance = fighter.GetDistanceTo(ship);
                if (distance >= anchorDistance)
                    continue;

                anchor = ship;
                anchorDistance = distance;
            }

            if (anchor == null)
                return;

            Orbit(
                fighter,
                anchor.X,
                anchor.Y,
                anchorDistance,
                anchor.HullFootprint + _patrolClearance * fighter.OrbitRadiusScale,
                rounds
            );
        }

        /// <summary>
        /// Flies a squadron along a circle around a point: along the tangent, steering inward
        /// when too far and outward when too close, and swinging back when the arena edge
        /// interrupts the circle.
        /// </summary>
        /// <param name="fighter">The squadron to move.</param>
        /// <param name="centerX">The circle center's horizontal coordinate.</param>
        /// <param name="centerY">The circle center's vertical coordinate.</param>
        /// <param name="distance">The squadron's current distance from the center.</param>
        /// <param name="orbitRadius">The circle radius.</param>
        /// <param name="rounds">The rounds of movement to apply.</param>
        private void Orbit(
            TacticalStarfighterUnit fighter,
            double centerX,
            double centerY,
            double distance,
            double orbitRadius,
            double rounds
        )
        {
            double toCenter = TacticalBattleGeometry.GetAngle(
                fighter.X,
                fighter.Y,
                centerX,
                centerY
            );
            double correction = Math.Atan((distance - orbitRadius) / Math.Max(orbitRadius, 1));
            fighter.Heading = TacticalBattleGeometry.NormalizeAngle(
                toCenter + fighter.OrbitDirection * (Math.PI / 2 - correction)
            );
            double travel = fighter.Speed * rounds;
            double fromX = fighter.X;
            double fromY = fighter.Y;
            fighter.X += Math.Cos(fighter.Heading) * travel;
            fighter.Y += Math.Sin(fighter.Heading) * travel;
            ClampToArena(fighter);
            // Where the circle crosses the arena edge the squadron slides along it; only when the
            // edge stops it dead does it swing back the other way.
            double moved = TacticalBattleGeometry.GetDistance(fromX, fromY, fighter.X, fighter.Y);
            if (moved < travel * _pinnedTravelFraction)
                fighter.OrbitDirection = -fighter.OrbitDirection;
        }

        /// <summary>
        /// Moves a unit straight toward a position, facing its direction of travel.
        /// </summary>
        /// <param name="unit">The unit to move.</param>
        /// <param name="x">The destination horizontal coordinate.</param>
        /// <param name="y">The destination vertical coordinate.</param>
        /// <param name="rounds">The elapsed combat rounds.</param>
        /// <param name="clamp">Whether the unit stays inside the arena.</param>
        /// <param name="stopDistance">The distance from the destination at which the unit stops.</param>
        private void MoveDirect(
            TacticalBattleUnit unit,
            double x,
            double y,
            double rounds,
            bool clamp,
            double stopDistance = 0
        )
        {
            double remaining =
                TacticalBattleGeometry.GetDistance(unit.X, unit.Y, x, y) - stopDistance;
            if (remaining <= 0)
                return;

            double angle = TacticalBattleGeometry.GetAngle(unit.X, unit.Y, x, y);
            // Squadrons never throttle back: they overshoot into their orbit band and circle.
            double travel = unit.IsStarfighter
                ? unit.Speed * rounds
                : Math.Min(unit.Speed * rounds, remaining);
            unit.Heading = angle;
            unit.X += Math.Cos(angle) * travel;
            unit.Y += Math.Sin(angle) * travel;
            if (clamp)
                ClampToArena(unit);
        }

        /// <summary>
        /// Finds the nearest friendly carrier that can still carry a stranded squadron away.
        /// </summary>
        /// <param name="fighter">The carrier-dependent squadron.</param>
        /// <param name="force">The squadron's force.</param>
        /// <returns>The nearest eligible carrier, or null.</returns>
        private static TacticalCapitalShipUnit FindRecoveryCarrier(
            TacticalStarfighterUnit fighter,
            TacticalBattleForce force
        )
        {
            TacticalCapitalShipUnit nearest = null;
            double nearestDistance = double.MaxValue;
            foreach (TacticalCapitalShipUnit ship in force.Ships)
            {
                if (
                    !ship.IsAlive
                    || ship.HasWithdrawn
                    || !ship.CanWithdrawIndependently
                    || ship.Ship.StarfighterCapacity <= 0
                )
                    continue;

                double distance = fighter.GetDistanceTo(ship);
                if (distance >= nearestDistance)
                    continue;

                nearest = ship;
                nearestDistance = distance;
            }

            return nearest;
        }

        /// <summary>
        /// Queues simultaneous attacks from one force against another.
        /// </summary>
        /// <param name="firingForce">The force performing attacks.</param>
        /// <param name="targetForce">The force receiving attacks.</param>
        private void QueueAttacks(TacticalBattleForce firingForce, TacticalBattleForce targetForce)
        {
            IReadOnlyList<TacticalBattleUnit> targets = targetForce.GetTargetableUnits();
            if (targets.Count == 0)
                return;

            foreach (TacticalBattleUnit unit in firingForce.Units)
            {
                if (!unit.CanFire)
                    continue;

                int shotCount = _shots.Count;
                unit.QueueAttacks(targets, AddPendingHit, _shots, _shotDisplaySeconds, _random);
                if (_shots.Count > shotCount)
                    _events.Add(new TacticalBattleEvent(TacticalBattleEventKind.WeaponFired, unit));
            }
        }

        /// <summary>
        /// Records one weapon lane's hit for simultaneous application.
        /// </summary>
        /// <param name="target">The unit receiving the hit.</param>
        /// <param name="damage">The positive damage dealt.</param>
        /// <param name="isIonDamage">Whether the hit came from an ion weapon.</param>
        private void AddPendingHit(TacticalBattleUnit target, double damage, bool isIonDamage)
        {
            if (!_pendingHits.TryGetValue(target, out List<PendingHit> hits))
            {
                hits = new List<PendingHit>();
                _pendingHits[target] = hits;
            }

            hits.Add(new PendingHit(damage, isIonDamage));
        }

        /// <summary>
        /// Applies all queued hits after both forces have fired.
        /// </summary>
        private void ApplyPendingHits()
        {
            foreach (KeyValuePair<TacticalBattleUnit, List<PendingHit>> entry in _pendingHits)
            {
                if (entry.Key is TacticalCapitalShipUnit)
                    _events.Add(
                        new TacticalBattleEvent(TacticalBattleEventKind.UnitHit, entry.Key)
                    );

                foreach (PendingHit hit in entry.Value)
                {
                    entry.Key.ApplyDamage(
                        hit.IsIonDamage ? 0 : hit.Damage,
                        hit.IsIonDamage ? hit.Damage : 0,
                        _config,
                        _random
                    );
                }
            }

            _pendingHits.Clear();
        }

        /// <summary>
        /// Advances recharge and temporary damage state for every surviving unit.
        /// </summary>
        /// <param name="force">The force whose units advance.</param>
        /// <param name="rounds">The elapsed combat rounds.</param>
        private void AdvanceUnitStates(TacticalBattleForce force, double rounds)
        {
            foreach (TacticalBattleUnit unit in force.Units)
            {
                if (unit.IsAlive && !unit.HasWithdrawn)
                    unit.AdvanceState(rounds, _config);
            }
        }

        /// <summary>
        /// Completes withdrawal for units that have left the arena and resolves stranded units.
        /// </summary>
        /// <param name="force">The withdrawing force.</param>
        private void CompleteReadyWithdrawals(TacticalBattleForce force)
        {
            if (!force.WithdrawalOrdered)
                return;

            foreach (TacticalBattleUnit unit in force.Units)
            {
                // A ship held in a tractor beam cannot make its jump.
                if (unit is TacticalCapitalShipUnit ship && ship.IsTractored)
                    continue;

                if (
                    unit.IsAlive
                    && unit.IsWithdrawing
                    && unit.CanWithdrawIndependently
                    && Math.Abs(unit.X) >= HalfWidth + _withdrawalMargin
                )
                    unit.FinishWithdrawal();
            }

            if (force.HasWithdrawingIndependentUnit)
                return;

            force.CompleteDependentWithdrawal();
        }

        /// <summary>
        /// Resolves forces that can no longer change the tactical state.
        /// </summary>
        private void ResolveStalemate()
        {
            if (_attacker.HasPendingWithdrawal)
            {
                CompleteStalematedForce(_attacker);
                return;
            }

            if (_defender.HasPendingWithdrawal)
            {
                CompleteStalematedForce(_defender);
                return;
            }

            int comparison = _attacker
                .GetTacticalStrength(_defender)
                .CompareTo(_defender.GetTacticalStrength(_attacker));
            if (comparison < 0)
            {
                CompleteStalematedForce(_attacker);
                return;
            }

            if (comparison > 0)
            {
                CompleteStalematedForce(_defender);
                return;
            }

            CompleteStalematedForce(_attacker);
            CompleteStalematedForce(_defender);
        }

        /// <summary>
        /// Withdraws or destroys a force selected by stalemate resolution.
        /// </summary>
        /// <param name="force">The force to complete.</param>
        private static void CompleteStalematedForce(TacticalBattleForce force)
        {
            if (force.CanWithdraw && !force.WithdrawalOrdered)
                force.CompleteWithdrawal();

            foreach (
                TacticalBattleUnit unit in force.Units.Where(unit =>
                    unit.IsAlive && !unit.HasWithdrawn
                )
            )
                unit.Destroy();

            force.Outcome = force.HasWithdrawnUnits
                ? SpaceCombatSideOutcome.Withdrawn
                : SpaceCombatSideOutcome.Destroyed;
        }

        /// <summary>
        /// Forms a force up in its own corner of the arena, facing the enemy's corner, and
        /// launches its fighters ahead of their carriers.
        /// </summary>
        /// <param name="force">The force to place.</param>
        private void PlaceForce(TacticalBattleForce force)
        {
            double direction = force.Side == CombatSide.Attacker ? 1 : -1;
            double cornerX = -direction * HalfWidth * _spawnLineFraction;
            double cornerY = -direction * HalfHeight * _spawnCornerFraction;
            double heading = TacticalBattleGeometry.GetAngle(cornerX, cornerY, -cornerX, -cornerY);
            double forwardX = Math.Cos(heading);
            double forwardY = Math.Sin(heading);
            // The battle line runs across the heading.
            double acrossX = -forwardY;
            double acrossY = forwardX;
            int shipCount = force.Ships.Count;
            double largestFootprint =
                shipCount > 0 ? force.Ships.Max(ship => ship.HullFootprint) : 0;
            double spacing =
                shipCount > 1
                    ? Math.Min(
                        Math.Max(_shipSpacing, 2 * largestFootprint * _shipClearance),
                        2 * HalfHeight / (shipCount + 1)
                    )
                    : 0;
            for (int index = 0; index < shipCount; index++)
            {
                TacticalCapitalShipUnit ship = force.Ships[index];
                double offset = (index - (shipCount - 1) / 2.0) * spacing;
                ship.X = cornerX + acrossX * offset;
                ship.Y = cornerY + acrossY * offset;
                ship.Heading = heading;
                ClampToArena(ship);
            }

            Dictionary<CapitalShip, int> launchedCounts = new Dictionary<CapitalShip, int>();
            int planetaryIndex = 0;
            foreach (TacticalStarfighterUnit fighter in force.Fighters)
            {
                TacticalCapitalShipUnit carrier =
                    fighter.Carrier == null
                        ? null
                        : force.Ships.FirstOrDefault(ship => ship.Ship == fighter.Carrier);
                fighter.Heading = heading;
                if (carrier != null)
                {
                    launchedCounts.TryGetValue(carrier.Ship, out int launched);
                    launchedCounts[carrier.Ship] = launched + 1;
                    double ahead = carrier.HullFootprint + _fighterLaunchOffset;
                    double across = GetAlternatingOffset(launched) * _fighterLaunchSpacing;
                    fighter.X = carrier.X + forwardX * ahead + acrossX * across;
                    fighter.Y = carrier.Y + forwardY * ahead + acrossY * across;
                }
                else
                {
                    double across = GetAlternatingOffset(planetaryIndex++) * _fighterLaunchSpacing;
                    fighter.X = cornerX + forwardX * _planetaryFighterOffset + acrossX * across;
                    fighter.Y = cornerY + forwardY * _planetaryFighterOffset + acrossY * across;
                }

                ClampToArena(fighter);
            }
        }

        /// <summary>
        /// Returns a lateral slot offset that alternates outward from the center.
        /// </summary>
        /// <param name="index">The zero-based slot index.</param>
        /// <returns>The signed slot offset.</returns>
        private static int GetAlternatingOffset(int index)
        {
            int magnitude = (index + 1) / 2;
            return index % 2 == 0 ? magnitude : -magnitude;
        }

        /// <summary>
        /// Returns the horizontal coordinate a side travels to when withdrawing.
        /// </summary>
        /// <param name="side">The withdrawing side.</param>
        /// <returns>The escape coordinate beyond the arena edge.</returns>
        private double GetRetreatX(CombatSide side)
        {
            double escape = HalfWidth + _withdrawalMargin + 1;
            return side == CombatSide.Attacker ? -escape : escape;
        }

        /// <summary>
        /// Keeps a unit inside the arena.
        /// </summary>
        /// <param name="unit">The unit to clamp.</param>
        private void ClampToArena(TacticalBattleUnit unit)
        {
            unit.X = ClampX(unit.X, unit.Radius);
            unit.Y = ClampY(unit.Y, unit.Radius);
        }

        /// <summary>
        /// Clamps a horizontal coordinate inside the arena.
        /// </summary>
        /// <param name="x">The coordinate to clamp.</param>
        /// <param name="radius">The unit radius kept inside the edge.</param>
        /// <returns>The clamped coordinate.</returns>
        private double ClampX(double x, double radius)
        {
            double limit = Math.Max(HalfWidth - radius, 0);
            return Math.Max(-limit, Math.Min(limit, x));
        }

        /// <summary>
        /// Clamps a vertical coordinate inside the arena.
        /// </summary>
        /// <param name="y">The coordinate to clamp.</param>
        /// <param name="radius">The unit radius kept inside the edge.</param>
        /// <returns>The clamped coordinate.</returns>
        private double ClampY(double y, double radius)
        {
            double limit = Math.Max(HalfHeight - radius, 0);
            return Math.Max(-limit, Math.Min(limit, y));
        }

        /// <summary>
        /// Ages visible shots and removes those that have faded.
        /// </summary>
        /// <param name="seconds">The elapsed seconds.</param>
        private void AgeShots(double seconds)
        {
            for (int index = _shots.Count - 1; index >= 0; index--)
            {
                _shots[index].RemainingSeconds -= seconds;
                if (_shots[index].RemainingSeconds <= 0)
                    _shots.RemoveAt(index);
            }
        }

        /// <summary>
        /// Returns the commandable capital ships within a selection.
        /// </summary>
        /// <param name="units">The selected units.</param>
        /// <returns>The ships that accept player orders.</returns>
        private List<TacticalCapitalShipUnit> GetCommandableShips(
            IEnumerable<TacticalBattleUnit> units
        )
        {
            return (units ?? Enumerable.Empty<TacticalBattleUnit>())
                .OfType<TacticalCapitalShipUnit>()
                .Where(CanCommand)
                .Distinct()
                .ToList();
        }

        /// <summary>
        /// Raises destruction and withdrawal events for units whose state changed this step.
        /// </summary>
        private void PublishUnitStateEvents()
        {
            foreach (TacticalBattleUnit unit in _units)
            {
                if (!unit.IsAlive && _reportedDestroyed.Add(unit))
                    _events.Add(
                        new TacticalBattleEvent(TacticalBattleEventKind.UnitDestroyed, unit)
                    );
                else if (unit.HasWithdrawn && _reportedWithdrawn.Add(unit))
                    _events.Add(
                        new TacticalBattleEvent(TacticalBattleEventKind.UnitWithdrew, unit)
                    );
            }
        }

        /// <summary>
        /// Returns the force fighting for a side.
        /// </summary>
        /// <param name="side">The side to resolve.</param>
        /// <returns>The matching force.</returns>
        private TacticalBattleForce GetForce(CombatSide side)
        {
            return side switch
            {
                CombatSide.Attacker => _attacker,
                CombatSide.Defender => _defender,
                _ => throw new ArgumentOutOfRangeException(nameof(side)),
            };
        }

        /// <summary>
        /// Appends committed capital-ship damage to a combat result.
        /// </summary>
        /// <param name="result">The result receiving damage records.</param>
        /// <param name="ships">The resolved capital ships.</param>
        private static void AddShipResults(
            SpaceCombatResult result,
            IReadOnlyList<TacticalCapitalShipUnit> ships
        )
        {
            foreach (TacticalCapitalShipUnit ship in ships)
            {
                int hullAfter = ship.IsAlive
                    ? (int)Math.Round(Math.Max(ship.CurrentHull, 1), MidpointRounding.ToEven)
                    : 0;
                // Generator damage leaves the battle with the ship, whether it was taken here or
                // worked off here.
                bool generatorInvolved =
                    ship.ShieldGeneratorRepairDays > 0 || ship.Ship.ShieldGeneratorRepairDays > 0;
                if (hullAfter >= ship.InitialHull && !generatorInvolved)
                    continue;

                result.ShipDamage.Add(
                    new ShipDamageResult
                    {
                        Ship = ship.Ship,
                        HullBefore = ship.InitialHull,
                        HullAfter = hullAfter,
                        ShieldGeneratorRepairDays = ship.ShieldGeneratorRepairDays,
                    }
                );
            }
        }

        /// <summary>
        /// Appends committed fighter losses to a combat result.
        /// </summary>
        /// <param name="result">The result receiving fighter-loss records.</param>
        /// <param name="fighters">The resolved fighter squadrons.</param>
        private static void AddFighterResults(
            SpaceCombatResult result,
            IReadOnlyList<TacticalStarfighterUnit> fighters
        )
        {
            foreach (TacticalStarfighterUnit fighter in fighters)
            {
                if (fighter.CurrentSquadronSize >= fighter.InitialSquadronSize)
                    continue;

                result.FighterLosses.Add(
                    new FighterLossResult
                    {
                        Fighter = fighter.Fighter,
                        SquadsBefore = fighter.InitialSquadronSize,
                        SquadsAfter = fighter.CurrentSquadronSize,
                    }
                );
            }
        }

        /// <summary>
        /// Captures detached presentation state for one resolved force.
        /// </summary>
        /// <param name="force">The resolved combat force.</param>
        /// <returns>The detached unit snapshots.</returns>
        private static List<CombatUnitSnapshot> CaptureCombatUnits(TacticalBattleForce force)
        {
            List<CombatUnitSnapshot> units = force
                .Ships.SelectMany(ship =>
                    new[] { ship.Ship }
                        .Cast<ISceneNode>()
                        .Concat(ship.Ship.GetChildren<ISceneNode>(recursive: true))
                )
                .Concat(force.Fighters.Select(fighter => fighter.Fighter))
                .Where(unit => unit != null)
                .Distinct()
                .Select(unit => new CombatUnitSnapshot(unit))
                .ToList();
            CombatUnitSnapshot.RecordOutcomes(
                units,
                force.Units.Where(IsDamaged).Select(unit => unit.Node),
                force.Units.Where(unit => !unit.IsAlive).Select(unit => unit.Node)
            );
            return units;
        }

        /// <summary>
        /// Returns whether a resolved tactical unit sustained damage.
        /// </summary>
        /// <param name="unit">The resolved tactical unit.</param>
        /// <returns>True when the unit lost hull or squadron strength.</returns>
        private static bool IsDamaged(TacticalBattleUnit unit)
        {
            return unit switch
            {
                TacticalCapitalShipUnit ship => ship.CurrentHull < ship.Ship.MaxHullStrength,
                TacticalStarfighterUnit fighter => fighter.CurrentSquadronSize
                    < fighter.InitialSquadronSize,
                _ => false,
            };
        }

        /// <summary>
        /// Determines the winning side from the resolved force states.
        /// </summary>
        /// <returns>The winning side, or a draw.</returns>
        private CombatSide DetermineWinner()
        {
            bool attackerActive = _attacker.Outcome == SpaceCombatSideOutcome.Active;
            bool defenderActive = _defender.Outcome == SpaceCombatSideOutcome.Active;
            if (attackerActive != defenderActive)
                return attackerActive ? CombatSide.Attacker : CombatSide.Defender;

            bool attackerSurvived = _attacker.Units.Any(unit => unit.IsAlive);
            bool defenderSurvived = _defender.Units.Any(unit => unit.IsAlive);
            if (attackerSurvived == defenderSurvived)
                return CombatSide.Draw;

            return attackerSurvived ? CombatSide.Attacker : CombatSide.Defender;
        }

        /// <summary>
        /// Records one conventional or ion hit in its original firing order.
        /// </summary>
        private readonly struct PendingHit
        {
            internal double Damage { get; }

            internal bool IsIonDamage { get; }

            /// <summary>
            /// Creates a pending hit.
            /// </summary>
            /// <param name="damage">The positive damage dealt by the hit.</param>
            /// <param name="isIonDamage">Whether the hit uses ion damage.</param>
            internal PendingHit(double damage, bool isIonDamage)
            {
                Damage = damage;
                IsIonDamage = isIonDamage;
            }
        }
    }
}
