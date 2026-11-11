using System;
using System.Collections.Generic;
using System.Linq;
using Rebellion.Game;
using Rebellion.Game.Results;
using Rebellion.Game.Units;
using Rebellion.SceneGraph;
using Rebellion.Util.Logging;
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

        // A squadron over its hangar takes three seconds to come aboard.
        private const double _dockingRounds = 3 / RoundSeconds;
        private const double _shotDisplaySeconds = 0.3;
        private const double _arenaScale = 3;
        private const double _spawnLineFraction = 0.8;
        private const double _spawnCornerFraction = 0.6;
        private const double _shipSpacing = 8;
        private const double _shipClearance = 1.1;
        private const double _formationClearance = 1.25;
        private const double _escortStationFactor = 1.6;
        private const double _escortDeckStationFactor = 0.5;
        private const double _blockedMargin = 2;
        private const double _blockingArc = Math.PI / 3;
        private const int _deckCount = 3;
        private const double _deckSpacingFactor = 1.2;
        private const double _deckTolerance = 0.25;
        private const double _fighterClimbFraction = 0.5;
        private const double _fighterLaunchOffset = 3;
        private const double _fighterLaunchSpacing = 2;
        private const double _planetaryFighterOffset = 8;
        private const double _arrivalTolerance = 0.5;
        private const double _orbitRadiusFraction = 0.6;
        private const double _orbitRangeCeiling = 0.9;
        private const double _patrolClearance = 6;

        // A screening squadron takes on what comes this near its ship's hull, and lets go of a
        // target that gets the slack beyond it away again.
        private const double _screenClearance = 25;
        private const double _screenSlack = 10;
        private const double _pinnedTravelFraction = 0.25;
        private const double _attackApproachFraction = 0.85;
        private const double _roundEpsilon = 1e-9;
        private const double _durabilityProgressThreshold = 0.5;
        private const double _stationRadiusFraction = 0.5;
        private const double _stationStandoffRadii = 1.4;
        private const double _stationSpacingRadii = 2.2;
        private const double _arenaEdgeTolerance = 0.01;

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
        private readonly List<TacticalBattleUnit> _screenTargets = new List<TacticalBattleUnit>();
        private readonly HashSet<TacticalBattleUnit> _reportedDestroyed =
            new HashSet<TacticalBattleUnit>();
        private readonly HashSet<TacticalBattleUnit> _reportedWithdrawn =
            new HashSet<TacticalBattleUnit>();
        private readonly List<TacticalBattleStation> _stations = new List<TacticalBattleStation>();
        private readonly HashSet<TacticalBattleStation> _reportedStations =
            new HashSet<TacticalBattleStation>();
        private readonly double _superlaserRechargeRounds;
        private double _pendingRounds;
        private double _roundProgress;
        private int _roundsCompleted;
        private int _stagnantRounds;
        private double _previousAttackerDurability;
        private double _previousDefenderDurability;
        private double _lowestAttackerDurability;
        private double _lowestDefenderDurability;
        private double _previousAttackerProgress;
        private double _previousDefenderProgress;
        private bool _isComplete;

        public CombatSide? PlayerSide { get; }

        public double HalfWidth { get; }

        public double HalfHeight { get; }

        /// <summary>
        /// Gets the height between one deck of a formation and the next: a little more than the
        /// largest hull in the battle is long, so a ship on one deck clears the ships on the
        /// decks above and below it.
        /// </summary>
        public double DeckSpacing { get; }

        public bool IsComplete => _isComplete;

        public double ElapsedSeconds { get; private set; }

        public int RoundsCompleted => _roundsCompleted;

        public IReadOnlyList<TacticalBattleUnit> Units => _units;

        public IReadOnlyList<TacticalShot> Shots => _shots;

        /// <summary>
        /// Gets the battle stations standing off from the battlefield, on either side.
        /// </summary>
        public IReadOnlyList<TacticalBattleStation> Stations => _stations;

        /// <summary>
        /// Creates a tactical battle between two forces.
        /// </summary>
        /// <param name="config">The space-combat parameters.</param>
        /// <param name="random">The persisted game random-number stream.</param>
        /// <param name="attackerShips">The attacking capital ships.</param>
        /// <param name="attackerFighters">The attacking fighter squadrons.</param>
        /// <param name="defenderShips">The defending capital ships.</param>
        /// <param name="defenderFighters">The defending fighter squadrons.</param>
        /// <param name="attackerHasEscapeRoute">
        /// Whether the attacking force has a hyperspace route out of the system; enemy gravity
        /// wells are judged live from the ships in the battle.
        /// </param>
        /// <param name="defenderHasEscapeRoute">
        /// Whether the defending force has a hyperspace route out of the system.
        /// </param>
        /// <param name="attackerCommand">The attacking side's tactical command bonuses.</param>
        /// <param name="defenderCommand">The defending side's tactical command bonuses.</param>
        /// <param name="playerSide">The side the player commands, or null when both sides are automatic.</param>
        /// <param name="shieldedStations">
        /// The planet destroyers among the ships that a shield generator on the planet protects
        /// from fighter attack, or null when none is.
        /// </param>
        public TacticalSpaceBattle(
            GameConfig.SpaceCombatConfig config,
            IRandomNumberProvider random,
            IReadOnlyList<CapitalShip> attackerShips,
            IReadOnlyList<Starfighter> attackerFighters,
            IReadOnlyList<CapitalShip> defenderShips,
            IReadOnlyList<Starfighter> defenderFighters,
            bool attackerHasEscapeRoute,
            bool defenderHasEscapeRoute,
            SpaceCombatCommandModifiers attackerCommand,
            SpaceCombatCommandModifiers defenderCommand,
            CombatSide? playerSide,
            IReadOnlyCollection<CapitalShip> shieldedStations = null
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
                attackerHasEscapeRoute,
                playerSide != CombatSide.Attacker,
                config,
                attackerCommand,
                shieldedStations
            );
            _defender = new TacticalBattleForce(
                CombatSide.Defender,
                defenderShips,
                defenderFighters,
                defenderHasEscapeRoute,
                playerSide != CombatSide.Defender,
                config,
                defenderCommand,
                shieldedStations
            );
            _units = new List<TacticalBattleUnit>(_attacker.Units.Count + _defender.Units.Count);
            _units.AddRange(_attacker.Units);
            _units.AddRange(_defender.Units);
            HalfWidth = Math.Max(config.AutoResolveStartingDistance, 1) * _arenaScale;
            HalfHeight = HalfWidth * _arenaHeightRatio;
            double largestFootprint = 0;
            foreach (TacticalBattleUnit unit in _units)
                largestFootprint = Math.Max(largestFootprint, unit.HullFootprint);
            DeckSpacing = largestFootprint * _deckSpacingFactor;
            _scanInterval = Math.Max(config.AutoResolveTargetScanDivisor, 1);
            _superlaserRechargeRounds = BattleStationRules.GetRechargeRounds(config);
            _stations.AddRange(_attacker.Stations);
            _stations.AddRange(_defender.Stations);
            PlaceForce(_attacker);
            PlaceForce(_defender);
            PlaceStations(_attacker);
            PlaceStations(_defender);
            SeparateShips();
            _previousAttackerDurability = _attacker.GetLastingDurability();
            _previousDefenderDurability = _defender.GetLastingDurability();
            _lowestAttackerDurability = _attacker.GetDurability();
            _lowestDefenderDurability = _defender.GetDurability();
            _previousAttackerProgress = _attacker.GetWithdrawalProgress();
            _previousDefenderProgress = _defender.GetWithdrawalProgress();
        }

        /// <summary>
        /// Returns how far a force's starting line lies from its own edge of the battlefield.
        /// Automatic resolution has a withdrawing unit cover the same ground, so leaving a battle
        /// takes about as long there as it does here.
        /// </summary>
        /// <param name="config">The space-combat parameters.</param>
        /// <returns>The distance from a starting line to the edge behind it.</returns>
        internal static double GetStartingEdgeDistance(GameConfig.SpaceCombatConfig config)
        {
            return Math.Max(config.AutoResolveStartingDistance, 0)
                * _arenaScale
                * (1 - _spawnLineFraction);
        }

        /// <summary>
        /// Returns whether a side is permitted to withdraw from the battle right now.
        /// </summary>
        /// <param name="side">The side to inspect.</param>
        /// <returns>True when the side can withdraw.</returns>
        public bool CanWithdraw(CombatSide side)
        {
            return GetWithdrawalStatus(side) == TacticalWithdrawalStatus.Available;
        }

        /// <summary>
        /// Reports why a side can or cannot withdraw right now. Interdiction is judged from the
        /// enemy ships still in the battle, so destroying the ship that projects the gravity well
        /// reopens the lane; a side whose hyperdrive-capable units are all gone cannot leave either.
        /// </summary>
        /// <param name="side">The side to inspect.</param>
        /// <returns>The side's withdrawal status.</returns>
        public TacticalWithdrawalStatus GetWithdrawalStatus(CombatSide side)
        {
            TacticalBattleForce force = GetForce(side);
            if (!force.HasIndependentUnit)
                return TacticalWithdrawalStatus.NoHyperdrive;
            if (!force.HasEscapeRoute)
                return TacticalWithdrawalStatus.NoEscapeRoute;

            TacticalBattleForce opposing = side == CombatSide.Attacker ? _defender : _attacker;
            return opposing.ProjectsGravityWell
                ? TacticalWithdrawalStatus.Interdicted
                : TacticalWithdrawalStatus.Available;
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
            if (_isComplete || force.WithdrawalOrdered || !CanWithdraw(side))
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
                || unit == null
                || unit.Side != PlayerSide.Value
                || !unit.IsTargetable
            )
                return false;

            TacticalBattleForce force = GetForce(unit.Side);
            return !force.IsAutomatic && !force.WithdrawalOrdered;
        }

        /// <summary>
        /// Returns a side's battle station still taking part in the battle.
        /// </summary>
        /// <param name="side">The side to inspect.</param>
        /// <returns>The side's first station in the battle, or null when it has none.</returns>
        public TacticalBattleStation GetStation(CombatSide side)
        {
            return GetForce(side).Stations.FirstOrDefault(station => station.IsInBattle);
        }

        /// <summary>
        /// Returns whether the player can currently fire a battle station's superlaser by hand.
        /// </summary>
        /// <param name="station">The station to inspect.</param>
        /// <returns>True when the station takes the player's orders.</returns>
        public bool CanCommandStation(TacticalBattleStation station)
        {
            if (
                _isComplete
                || PlayerSide == null
                || station == null
                || station.Side != PlayerSide.Value
                || !station.IsInBattle
            )
                return false;

            TacticalBattleForce force = GetForce(station.Side);
            return !force.IsAutomatic && !force.WithdrawalOrdered;
        }

        /// <summary>
        /// Returns whether a station's superlaser could be fired at a unit right now: the laser
        /// is charged and the unit is an enemy capital ship still in the fight.
        /// </summary>
        /// <param name="station">The station that would fire.</param>
        /// <param name="target">The unit to fire at.</param>
        /// <returns>True when the shot can be taken.</returns>
        public bool CanTargetWithSuperlaser(
            TacticalBattleStation station,
            TacticalBattleUnit target
        )
        {
            return station?.IsSuperlaserReady == true
                && target is TacticalCapitalShipUnit
                && target.IsTargetable
                && target.Side != station.Side;
        }

        /// <summary>
        /// Fires the player's superlaser at an enemy capital ship. The beam destroys the ship a
        /// moment later, whatever its shields and hull, and the laser then recharges.
        /// </summary>
        /// <param name="station">The player's station.</param>
        /// <param name="target">The enemy capital ship to destroy.</param>
        /// <returns>True when the superlaser fired.</returns>
        public bool FireSuperlaser(TacticalBattleStation station, TacticalBattleUnit target)
        {
            return CanCommandStation(station) && BeginSuperlaserStrike(station, target);
        }

        /// <summary>
        /// Returns whether the player can send squadrons against an enemy battle station: it must
        /// still be in the battle and no planetary shield may protect it.
        /// </summary>
        /// <param name="station">The station to attack.</param>
        /// <returns>True when a trench run can be ordered.</returns>
        public bool CanOrderTrenchRun(TacticalBattleStation station)
        {
            return !_isComplete
                && PlayerSide != null
                && station != null
                && station.Side != PlayerSide.Value
                && IsAttackable(station);
        }

        /// <summary>
        /// Sends the commandable squadrons in a selection to fly an enemy station's trench. They
        /// leave the battlefield for the station, and the run is decided once every squadron sent
        /// has arrived or been lost on the way.
        /// </summary>
        /// <param name="units">The selected units.</param>
        /// <param name="station">The enemy station to attack.</param>
        /// <returns>True when at least one squadron took the order.</returns>
        public bool IssueTrenchRunOrder(
            IEnumerable<TacticalBattleUnit> units,
            TacticalBattleStation station
        )
        {
            if (!CanOrderTrenchRun(station))
                return false;

            bool ordered = false;
            foreach (TacticalBattleUnit unit in units ?? Enumerable.Empty<TacticalBattleUnit>())
            {
                if (unit is not TacticalStarfighterUnit fighter || !CanCommand(fighter))
                    continue;

                fighter.SetTrenchRun(station);
                ordered = true;
            }

            return ordered;
        }

        /// <summary>
        /// Returns whether the player can recall a squadron to a hangar right now: it takes
        /// orders, it is not already making for one, and a friendly ship has a bay free.
        /// </summary>
        /// <param name="unit">The unit to inspect.</param>
        /// <returns>True when the squadron would take a recover order.</returns>
        public bool CanOrderRecovery(TacticalBattleUnit unit)
        {
            return unit is TacticalStarfighterUnit fighter
                && CanCommand(fighter)
                && fighter.RecoveryShip == null
                && GetForce(fighter.Side).FindHangar(fighter, mustJump: false) != null;
        }

        /// <summary>
        /// Recalls the commandable squadrons in a selection to a hangar. Each flies to a ship
        /// with a bay free, takes a few seconds to come aboard, and is then off the battlefield
        /// for the rest of the battle: it is lost if that ship is destroyed and leaves with it
        /// if it withdraws. A ship holds as many squadrons as its hangar capacity, however
        /// depleted they are.
        /// </summary>
        /// <param name="units">The selected units.</param>
        /// <param name="hangar">
        /// The friendly capital ship to dock with, or null to send each squadron to the ship it
        /// is screening, else to its own carrier, or to the nearest ship with room when that
        /// has none.
        /// </param>
        /// <returns>True when at least one squadron has a hangar to make for.</returns>
        public bool IssueRecoverOrder(
            IEnumerable<TacticalBattleUnit> units,
            TacticalBattleUnit hangar = null
        )
        {
            TacticalCapitalShipUnit preferred = hangar as TacticalCapitalShipUnit;
            if (
                hangar != null
                && (preferred == null || preferred.Side != PlayerSide || !preferred.IsTargetable)
            )
                return false;

            bool ordered = false;
            foreach (TacticalBattleUnit unit in units ?? Enumerable.Empty<TacticalBattleUnit>())
            {
                if (unit is not TacticalStarfighterUnit fighter || !CanCommand(fighter))
                    continue;

                ordered |= GetForce(fighter.Side)
                    .TryRecover(fighter, preferred ?? fighter.ScreenedShip, mustJump: false);
            }

            return ordered;
        }

        /// <summary>
        /// Posts the commandable squadrons in a selection as a screen around a friendly capital
        /// ship. Each circles that ship and engages only the enemies that come near it, until
        /// the ship is gone, the side retreats, or the squadron is given another order.
        /// </summary>
        /// <param name="units">The selected units.</param>
        /// <param name="ship">The friendly capital ship to screen.</param>
        /// <returns>True when at least one squadron took the order.</returns>
        public bool IssueScreenOrder(IEnumerable<TacticalBattleUnit> units, TacticalBattleUnit ship)
        {
            if (
                ship is not TacticalCapitalShipUnit screened
                || screened.Side != PlayerSide
                || !screened.IsTargetable
            )
                return false;

            bool ordered = false;
            foreach (TacticalBattleUnit unit in units ?? Enumerable.Empty<TacticalBattleUnit>())
            {
                if (unit is not TacticalStarfighterUnit fighter || !CanCommand(fighter))
                    continue;

                fighter.SetScreen(screened);
                ordered = true;
            }

            return ordered;
        }

        /// <summary>
        /// Estimates the chance that a trench run on a station succeeds if the squadrons in a
        /// selection join the squadrons already flying it.
        /// </summary>
        /// <param name="units">The squadrons about to be sent.</param>
        /// <param name="station">The station to attack.</param>
        /// <returns>The chance from 0 to 1; zero when no squadron in the run carries torpedoes.</returns>
        public double EstimateTrenchRunChance(
            IEnumerable<TacticalBattleUnit> units,
            TacticalBattleStation station
        )
        {
            if (station == null || !IsAttackable(station))
                return 0;

            HashSet<TacticalStarfighterUnit> run = new HashSet<TacticalStarfighterUnit>();
            foreach (TacticalBattleUnit unit in units ?? Enumerable.Empty<TacticalBattleUnit>())
            {
                if (
                    unit is TacticalStarfighterUnit fighter
                    && fighter.IsTargetable
                    && fighter.Side != station.Side
                )
                    run.Add(fighter);
            }

            foreach (TacticalStarfighterUnit fighter in GetOpposingForce(station.Side).Fighters)
            {
                if (fighter.TrenchRunTarget == station && fighter.IsAlive && !fighter.HasWithdrawn)
                    run.Add(fighter);
            }

            return GetTrenchRunChance(run, GetForce(station.Side));
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
        /// Orders the commandable ships in a selection to pursue and engage an opposing unit,
        /// and sends its squadrons after it; a squadron holds that target until it is gone.
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
            foreach (TacticalBattleUnit unit in units ?? Enumerable.Empty<TacticalBattleUnit>())
            {
                if (unit is TacticalStarfighterUnit fighter && CanCommand(fighter))
                    fighter.SetDirectedTarget(target);
            }
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
                if (!unit.IsAlive || unit.HasWithdrawn || unit.IsOffField)
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
            AddStationResults(result, _attacker.Stations);
            AddStationResults(result, _defender.Stations);
            AddFighterResults(result, _attacker.Fighters);
            AddFighterResults(result, _defender.Fighters);
            AddDockingResults(result, _attacker.Fighters);
            AddDockingResults(result, _defender.Fighters);
            result.AttackingUnits.AddRange(CaptureCombatUnits(_attacker));
            result.DefendingUnits.AddRange(CaptureCombatUnits(_defender));
            result.Winner = DetermineWinner();
            result.WithdrawnUnits.AddRange(
                _units.Where(unit => unit.HasWithdrawn).Select(unit => unit.Node)
            );
            result.WithdrawnUnits.AddRange(
                _stations.Where(station => station.HasWithdrawn).Select(station => station.Ship)
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

            // Trench runs are ordered first: a side with squadrons on a run does not turn for
            // home until the run is decided.
            OrderAutomaticTrenchRuns(_attacker, _defender);
            OrderAutomaticTrenchRuns(_defender, _attacker);
            WithdrawAtThreshold(_attacker, _defender);
            WithdrawAtThreshold(_defender, _attacker);
            WithdrawFromStation(_attacker, _defender);
            WithdrawFromStation(_defender, _attacker);
            UpdateAutomaticTargets(_attacker, _defender, rounds);
            UpdateAutomaticTargets(_defender, _attacker, rounds);
            FireAutomaticSuperlasers(_attacker, _defender);
            FireAutomaticSuperlasers(_defender, _attacker);
            MoveUnits(_attacker, rounds);
            MoveUnits(_defender, rounds);
            SeparateShips();
            UpdateTractorBeams();
            QueueAttacks(_attacker, _defender);
            QueueAttacks(_defender, _attacker);
            ApplyPendingHits();
            AdvanceUnitStates(_attacker, rounds);
            AdvanceUnitStates(_defender, rounds);
            AdvanceStations(_attacker, rounds);
            AdvanceStations(_defender, rounds);
            ResolveTrenchRuns(_attacker, _defender);
            ResolveTrenchRuns(_defender, _attacker);
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
            // Progress is damage that sticks (hull, airframes) or a side's total durability
            // reaching a new low, which is a shield siege gaining ground. Shields that merely dip
            // and heal back are not progress: counting them let two forces that could not break
            // each other's shields fight on to the round cap.
            double attackerDurability = _attacker.GetLastingDurability();
            double defenderDurability = _defender.GetLastingDurability();
            double attackerTotal = _attacker.GetDurability();
            double defenderTotal = _defender.GetDurability();
            double attackerProgress = _attacker.GetWithdrawalProgress();
            double defenderProgress = _defender.GetWithdrawalProgress();
            bool stateChanged =
                Math.Abs(attackerDurability - _previousAttackerDurability) > double.Epsilon
                || Math.Abs(defenderDurability - _previousDefenderDurability) > double.Epsilon
                || attackerTotal < _lowestAttackerDurability - _durabilityProgressThreshold
                || defenderTotal < _lowestDefenderDurability - _durabilityProgressThreshold
                || Math.Abs(attackerProgress - _previousAttackerProgress) > double.Epsilon
                || Math.Abs(defenderProgress - _previousDefenderProgress) > double.Epsilon;
            _stagnantRounds = stateChanged ? 0 : _stagnantRounds + 1;
            _previousAttackerDurability = attackerDurability;
            _previousDefenderDurability = defenderDurability;
            _lowestAttackerDurability = Math.Min(_lowestAttackerDurability, attackerTotal);
            _lowestDefenderDurability = Math.Min(_lowestDefenderDurability, defenderTotal);
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
            // A force with a battle station still in the fight holds its ground, and one with
            // squadrons on a trench run waits for the outcome: squadrons inside the trench are
            // off the battlefield and would otherwise read as lost strength.
            if (
                !force.IsAutomatic
                || force.WithdrawalOrdered
                || !force.HasCombatants
                || force.HasStationInBattle
                || force.HasTrenchRunUnderWay
                || !CanWithdraw(force.Side)
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
        /// Withdraws an automatically controlled force that has nothing left to fight but a
        /// battle station it cannot attack: staying would only feed its ships to the superlaser.
        /// </summary>
        /// <param name="force">The force whose withdrawal is evaluated.</param>
        /// <param name="opposingForce">The force that may own a station.</param>
        private void WithdrawFromStation(
            TacticalBattleForce force,
            TacticalBattleForce opposingForce
        )
        {
            if (
                !force.IsAutomatic
                || force.WithdrawalOrdered
                || !force.HasCombatants
                || !opposingForce.HasStationInBattle
                || opposingForce.HasCombatants
                || opposingForce.WithdrawalOrdered
                || !CanWithdraw(force.Side)
            )
                return;

            bool canAttack =
                opposingForce.Stations.Any(IsAttackable)
                && force.Fighters.Any(fighter =>
                    fighter.IsAlive && !fighter.HasWithdrawn && fighter.HasTorpedoes
                );
            if (!canAttack)
                force.BeginWithdrawal();
        }

        /// <summary>
        /// Sends an automatically controlled force's torpedo squadrons against an enemy battle
        /// station that fighters can attack.
        /// </summary>
        /// <param name="force">The force whose squadrons are tasked.</param>
        /// <param name="opposingForce">The force that may own a station.</param>
        private static void OrderAutomaticTrenchRuns(
            TacticalBattleForce force,
            TacticalBattleForce opposingForce
        )
        {
            if (!force.IsAutomatic || force.WithdrawalOrdered)
                return;

            TacticalBattleStation station = opposingForce.Stations.FirstOrDefault(IsAttackable);
            if (station == null)
                return;

            foreach (TacticalStarfighterUnit fighter in force.Fighters)
            {
                // A squadron recalled to a hangar is left to come home.
                if (
                    fighter.IsTargetable
                    && fighter.HasTorpedoes
                    && fighter.TrenchRunTarget == null
                    && fighter.RecoveryShip == null
                )
                    fighter.SetTrenchRun(station);
            }
        }

        /// <summary>
        /// Fires an automatically controlled force's charged superlasers at the toughest enemy
        /// capital ship, once the battle's opening moments have passed.
        /// </summary>
        /// <param name="force">The force whose stations fire.</param>
        /// <param name="opposingForce">The force providing targets.</param>
        private void FireAutomaticSuperlasers(
            TacticalBattleForce force,
            TacticalBattleForce opposingForce
        )
        {
            if (
                !force.IsAutomatic
                || force.WithdrawalOrdered
                || _roundsCompleted + _roundProgress < BattleStationRules.AutomaticHoldRounds
            )
                return;

            foreach (TacticalBattleStation station in force.Stations)
            {
                if (!station.IsSuperlaserReady)
                    continue;

                TacticalCapitalShipUnit target = null;
                foreach (TacticalCapitalShipUnit ship in opposingForce.Ships)
                {
                    if (
                        !ship.IsTargetable
                        || force.Stations.Any(other => other.StrikeTarget == ship)
                        || (
                            target != null && ship.RemainingDurability <= target.RemainingDurability
                        )
                    )
                        continue;

                    target = ship;
                }

                if (target != null)
                    BeginSuperlaserStrike(station, target);
            }
        }

        /// <summary>
        /// Starts a superlaser shot and announces it.
        /// </summary>
        /// <param name="station">The firing station.</param>
        /// <param name="target">The capital ship to destroy.</param>
        /// <returns>True when the shot was taken.</returns>
        private bool BeginSuperlaserStrike(TacticalBattleStation station, TacticalBattleUnit target)
        {
            if (!CanTargetWithSuperlaser(station, target))
                return false;

            station.BeginStrike(target, BattleStationRules.StrikeRounds);
            _events.Add(
                new TacticalBattleEvent(TacticalBattleEventKind.SuperlaserFired, station, target)
            );
            return true;
        }

        /// <summary>
        /// Advances a force's stations: superlaser recharge, beams in flight, and jump countdowns.
        /// A beam that finishes burning destroys its target outright.
        /// </summary>
        /// <param name="force">The force whose stations advance.</param>
        /// <param name="rounds">The elapsed combat rounds.</param>
        private void AdvanceStations(TacticalBattleForce force, double rounds)
        {
            foreach (TacticalBattleStation station in force.Stations)
            {
                TacticalBattleUnit struck = station.Advance(rounds, _superlaserRechargeRounds);
                if (struck?.IsTargetable == true)
                    struck.Destroy();
            }
        }

        /// <summary>
        /// Decides the trench runs against one force's stations. A run is decided once every
        /// squadron sent against the station has reached it or been lost on the way; squadrons
        /// sent against a station that can no longer be attacked return to the fight.
        /// </summary>
        /// <param name="stationForce">The force whose stations are attacked.</param>
        /// <param name="attackingForce">The force flying the runs.</param>
        private void ResolveTrenchRuns(
            TacticalBattleForce stationForce,
            TacticalBattleForce attackingForce
        )
        {
            foreach (TacticalBattleStation station in stationForce.Stations)
            {
                List<TacticalStarfighterUnit> arrived = null;
                bool onTheWay = false;
                foreach (TacticalStarfighterUnit fighter in attackingForce.Fighters)
                {
                    if (
                        fighter.TrenchRunTarget != station
                        || !fighter.IsAlive
                        || fighter.HasWithdrawn
                    )
                        continue;

                    if (!IsAttackable(station))
                    {
                        ReturnFromTrench(fighter, station);
                        continue;
                    }

                    if (fighter.IsInTrench)
                        (arrived ??= new List<TacticalStarfighterUnit>()).Add(fighter);
                    else
                        onTheWay = true;
                }

                if (arrived != null && !onTheWay)
                    ResolveTrenchRun(station, stationForce, arrived);
            }
        }

        /// <summary>
        /// Rolls one trench run. Success destroys the station and brings the squadrons back to
        /// the battlefield; failure costs every squadron that flew it.
        /// </summary>
        /// <param name="station">The attacked station.</param>
        /// <param name="stationForce">The station's force, whose fighters defend it.</param>
        /// <param name="squadrons">The squadrons inside the trench.</param>
        private void ResolveTrenchRun(
            TacticalBattleStation station,
            TacticalBattleForce stationForce,
            List<TacticalStarfighterUnit> squadrons
        )
        {
            double chance = GetTrenchRunChance(squadrons, stationForce);
            bool succeeded = chance > 0 && _random.NextDouble() < chance;
            GameLogger.Log(
                $"Trench run on {station.DisplayName}: "
                    + $"{squadrons.Sum(fighter => fighter.CurrentSquadronSize)} fighters in "
                    + $"{squadrons.Count} squadrons, {chance:P0} chance, "
                    + (succeeded ? "succeeded." : "failed.")
            );
            if (succeeded)
            {
                station.Destroy();
                foreach (TacticalStarfighterUnit fighter in squadrons)
                    ReturnFromTrench(fighter, station);
            }
            else
            {
                foreach (TacticalStarfighterUnit fighter in squadrons)
                    fighter.Destroy();
            }

            _events.Add(
                new TacticalBattleEvent(
                    succeeded
                        ? TacticalBattleEventKind.TrenchRunSucceeded
                        : TacticalBattleEventKind.TrenchRunFailed,
                    station
                )
            );
        }

        /// <summary>
        /// Returns the chance that a trench run succeeds. A run needs torpedoes; with them it
        /// succeeds at the configured base chance while the fighters flying it are at least as
        /// many as the enemy fighters still on the battlefield, and less in proportion when they
        /// are outnumbered, down to a configured floor.
        /// </summary>
        /// <param name="squadrons">The squadrons flying the run.</param>
        /// <param name="stationForce">The station's force, whose fighters defend it.</param>
        /// <returns>The chance from 0 to 1.</returns>
        private double GetTrenchRunChance(
            IEnumerable<TacticalStarfighterUnit> squadrons,
            TacticalBattleForce stationForce
        )
        {
            int attackers = 0;
            bool armed = false;
            foreach (TacticalStarfighterUnit fighter in squadrons)
            {
                attackers += fighter.CurrentSquadronSize;
                armed |= fighter.HasTorpedoes && fighter.CurrentSquadronSize > 0;
            }

            int defenders = 0;
            foreach (TacticalStarfighterUnit fighter in stationForce.Fighters)
            {
                if (fighter.IsTargetable)
                    defenders += fighter.CurrentSquadronSize;
            }

            return BattleStationRules.GetTrenchRunChance(attackers, armed, defenders, _config);
        }

        /// <summary>
        /// Brings a squadron back from a trench run. One that was inside the trench reappears at
        /// the station's near side and flies back onto the battlefield.
        /// </summary>
        /// <param name="fighter">The returning squadron.</param>
        /// <param name="station">The station it was sent against.</param>
        private void ReturnFromTrench(
            TacticalStarfighterUnit fighter,
            TacticalBattleStation station
        )
        {
            if (fighter.IsInTrench)
            {
                double angle = TacticalBattleGeometry.GetAngle(station.X, station.Y, 0, 0);
                fighter.X = station.X + Math.Cos(angle) * station.Radius;
                fighter.Y = station.Y + Math.Sin(angle) * station.Radius;
                fighter.Heading = angle;
            }

            fighter.ClearTrenchRun();
            TacticalBattleForce force = GetForce(fighter.Side);
            if (!force.WithdrawalOrdered)
                return;

            // Back from the trench into a retreat: the squadron makes for a hangar like the rest.
            fighter.StartWithdrawal();
            force.TryRecover(fighter, null, mustJump: true);
        }

        /// <summary>
        /// Returns whether fighters can attack a station: it is still in the battle and no
        /// planetary shield protects it.
        /// </summary>
        /// <param name="station">The station to inspect.</param>
        /// <returns>True when a trench run can reach the station.</returns>
        private static bool IsAttackable(TacticalBattleStation station)
        {
            return station.IsInBattle && !station.IsShielded;
        }

        /// <summary>
        /// Returns the force opposing a side.
        /// </summary>
        /// <param name="side">The side whose opponent is wanted.</param>
        /// <returns>The opposing force.</returns>
        private TacticalBattleForce GetOpposingForce(CombatSide side)
        {
            return side == CombatSide.Attacker ? _defender : _attacker;
        }

        /// <summary>
        /// Stands a force's battle stations off beyond its own edge of the battlefield, level
        /// with its corner.
        /// </summary>
        /// <param name="force">The force whose stations are placed.</param>
        private void PlaceStations(TacticalBattleForce force)
        {
            double direction = force.Side == CombatSide.Attacker ? 1 : -1;
            double radius = HalfHeight * _stationRadiusFraction;
            for (int index = 0; index < force.Stations.Count; index++)
            {
                TacticalBattleStation station = force.Stations[index];
                station.Radius = radius;
                station.X = -direction * (HalfWidth + radius * _stationStandoffRadii);
                station.Y =
                    -direction
                    * (HalfHeight * _spawnCornerFraction + index * radius * _stationSpacingRadii);
            }
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
                // A squadron flying a trench run keeps to it and takes no other target.
                if (!fighter.IsTargetable || fighter.TrenchRunTarget != null)
                    continue;

                // A screen lasts as long as the ship it was posted around.
                if (fighter.ScreenedShip?.IsTargetable == false)
                    fighter.ClearScreen();

                fighter.AdvanceScanCooldown(rounds);
                // A screening squadron takes on only what comes near its ship, and lets go of a
                // target that gets away from it.
                TacticalCapitalShipUnit screened = fighter.ScreenedShip;
                bool lost =
                    fighter.Target?.IsTargetable != true
                    || (
                        screened != null && !IsInsideScreen(screened, fighter.Target, _screenSlack)
                    );
                // A player-directed target is kept until it is gone.
                if (lost || (!fighter.IsDirected && ShouldRetarget(fighter, fighter.IsScanDue)))
                    fighter.SetTarget(
                        SelectFighterTarget(
                            fighter,
                            screened == null ? targets : GetScreenTargets(screened, targets)
                        ),
                        _scanInterval
                    );
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
                    ClimbToward(fighter, MoveFighter(fighter, force, rounds), rounds);
            }
        }

        /// <summary>
        /// Eases a squadron toward the height of whatever it is flying at, at a fraction of its
        /// speed, so fighters cross between decks instead of jumping from one to another.
        /// </summary>
        /// <param name="fighter">The squadron.</param>
        /// <param name="altitude">The height it is making for.</param>
        /// <param name="rounds">The elapsed combat rounds.</param>
        private static void ClimbToward(
            TacticalStarfighterUnit fighter,
            double altitude,
            double rounds
        )
        {
            double step = Math.Max(fighter.Speed, 1) * _fighterClimbFraction * rounds;
            fighter.Altitude += Math.Clamp(altitude - fighter.Altitude, -step, step);
        }

        /// <summary>
        /// Returns whether two units stand on different decks, and so cannot be in each other's
        /// way however close they are on the plane.
        /// </summary>
        /// <param name="first">One unit.</param>
        /// <param name="second">The other unit.</param>
        /// <returns>True when the units' heights differ by a good part of a deck.</returns>
        private bool AreOnDifferentDecks(TacticalBattleUnit first, TacticalBattleUnit second)
        {
            return DeckSpacing > 0
                && Math.Abs(first.Altitude - second.Altitude) > DeckSpacing * _deckTolerance;
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
        /// Pushes capital ships on the same deck apart so their hull footprints never overlap,
        /// whichever side they belong to, and so ships of one fleet keep room between their
        /// hulls. The two ships of
        /// a pair that is too close share the shortfall, unless one of them is a friend holding
        /// its place in the line, which the other then gives way to.
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
                    // Ships on different decks pass over and under each other.
                    if (AreOnDifferentDecks(a, b))
                        continue;

                    // Ships of one fleet keep formation room between their hulls; enemies, and
                    // ships running for the edge, are only kept from overlapping.
                    double clearance =
                        a.Side == b.Side && !a.IsWithdrawing && !b.IsWithdrawing
                            ? _formationClearance
                            : _shipClearance;
                    double minimum = (a.HullFootprint + b.HullFootprint) * clearance;
                    double distance = a.GetDistanceTo(b);
                    if (distance >= minimum)
                        continue;

                    double angle =
                        distance > _arrivalTolerance
                            ? a.GetAngleTo(b)
                            : (a.Side == CombatSide.Attacker ? Math.PI / 2 : -Math.PI / 2);
                    double shift = minimum - distance;
                    // A ship fighting from its place keeps it, and a friend still finding its
                    // own gives all the way; otherwise the two share the shortfall.
                    double shareA =
                        a.Side == b.Side && a.IsOnStation != b.IsOnStation
                            ? (a.IsOnStation ? 0 : 1)
                            : 0.5;
                    a.X -= Math.Cos(angle) * shift * shareA;
                    a.Y -= Math.Sin(angle) * shift * shareA;
                    b.X += Math.Cos(angle) * shift * (1 - shareA);
                    b.Y += Math.Sin(angle) * shift * (1 - shareA);
                    // A withdrawing ship is on its way out past the edge and is not pulled back.
                    if (!a.IsWithdrawing)
                        ClampToArena(a);
                    if (!b.IsWithdrawing)
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
            ship.IsOnStation = false;
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

            // An escort left to itself stays with the ships of the line and shoots at what
            // comes within reach, rather than chasing fighters into the enemy fleet.
            TacticalBattleUnit target = ship.Target;
            TacticalCapitalShipUnit screened =
                ship.IsEscort && ship.IsAutomatic ? FindScreenedShip(ship, force) : null;
            if (screened != null)
            {
                KeepStation(ship, screened, target, rounds);
                return;
            }

            if (target?.IsTargetable != true || !ship.IsArmed)
                return;

            // A ship choosing its own way stops short of the enemy line: once any enemy ship is
            // as near as it would hold from that ship, it fights from where it is, even if its
            // own target is farther off. And it waits behind a friend already fighting from the
            // place ahead of it instead of shouldering that ship toward the enemy. A ship under
            // the player's attack order goes for its target regardless.
            double preferredRange = ship.GetPreferredRange(target);
            bool inPlace =
                ship.GetDistanceTo(target) <= preferredRange
                || (ship.IsAutomatic && HasReachedEnemyLine(ship, force));
            bool waiting = !inPlace && ship.IsAutomatic && IsBlockedByFriend(ship, force, target);
            if (!inPlace && !waiting)
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

            ship.IsOnStation = inPlace;
            ship.Heading = TacticalBattleGeometry.TurnToward(
                ship.Heading,
                ship.GetDesiredFacing(target),
                ship.TurnRate * rounds
            );
        }

        /// <summary>
        /// Returns whether a ship has come as near the enemy line as it means to: some enemy
        /// capital ship is already inside the distance the ship would hold from it.
        /// </summary>
        /// <param name="ship">The advancing ship.</param>
        /// <param name="force">The ship's force.</param>
        /// <returns>True when an enemy capital ship is within the ship's holding distance.</returns>
        private bool HasReachedEnemyLine(TacticalCapitalShipUnit ship, TacticalBattleForce force)
        {
            TacticalBattleForce opposing = ReferenceEquals(force, _attacker)
                ? _defender
                : _attacker;
            foreach (TacticalCapitalShipUnit enemy in opposing.Ships)
            {
                if (
                    enemy.IsTargetable
                    && ship.GetDistanceTo(enemy) <= ship.GetPreferredRange(enemy)
                )
                    return true;
            }

            return false;
        }

        /// <summary>
        /// Returns whether a friendly ship that is holding its place on the same deck lies
        /// close ahead on a ship's course to its target, leaving no room to pass.
        /// </summary>
        /// <param name="ship">The advancing ship.</param>
        /// <param name="force">The ship's force.</param>
        /// <param name="target">The unit the ship is closing on.</param>
        /// <returns>True when the ship should wait where it is.</returns>
        private bool IsBlockedByFriend(
            TacticalCapitalShipUnit ship,
            TacticalBattleForce force,
            TacticalBattleUnit target
        )
        {
            double course = ship.GetAngleTo(target);
            foreach (TacticalCapitalShipUnit friend in force.Ships)
            {
                if (
                    friend == ship
                    || !friend.IsOnStation
                    || !friend.IsTargetable
                    || AreOnDifferentDecks(ship, friend)
                )
                    continue;

                double room =
                    (ship.HullFootprint + friend.HullFootprint) * _formationClearance
                    + _blockedMargin;
                if (ship.GetDistanceTo(friend) > room)
                    continue;

                double offCourse = Math.Abs(
                    TacticalBattleGeometry.NormalizeAngle(ship.GetAngleTo(friend) - course)
                );
                if (offCourse < _blockingArc)
                    return true;
            }

            return false;
        }

        /// <summary>
        /// Finds the ship of the line an escort screens: the nearest armed friendly ship that is
        /// not itself an escort and is still fighting.
        /// </summary>
        /// <param name="escort">The escort.</param>
        /// <param name="force">The escort's force.</param>
        /// <returns>The ship to stay with, or null when no ship of the line is left.</returns>
        private static TacticalCapitalShipUnit FindScreenedShip(
            TacticalCapitalShipUnit escort,
            TacticalBattleForce force
        )
        {
            TacticalCapitalShipUnit nearest = null;
            double nearestDistance = double.MaxValue;
            foreach (TacticalCapitalShipUnit candidate in force.Ships)
            {
                if (
                    candidate == escort
                    || candidate.IsEscort
                    || !candidate.IsArmed
                    || !candidate.IsTargetable
                    || candidate.IsWithdrawing
                )
                    continue;

                double distance = escort.GetDistanceTo(candidate);
                if (distance < nearestDistance)
                {
                    nearest = candidate;
                    nearestDistance = distance;
                }
            }

            return nearest;
        }

        /// <summary>
        /// Keeps an escort beside the ship it screens: it closes up when it has fallen behind and
        /// otherwise holds, turning its guns on its target.
        /// </summary>
        /// <param name="escort">The escort.</param>
        /// <param name="screened">The ship of the line it stays with.</param>
        /// <param name="target">The escort's current target, or null.</param>
        /// <param name="rounds">The elapsed combat rounds.</param>
        private void KeepStation(
            TacticalCapitalShipUnit escort,
            TacticalCapitalShipUnit screened,
            TacticalBattleUnit target,
            double rounds
        )
        {
            // On the same deck the escort stands off beside its charge; on another deck it has
            // nothing to keep clear of and rides close over or under it.
            double station =
                (escort.HullFootprint + screened.HullFootprint)
                * (
                    AreOnDifferentDecks(escort, screened)
                        ? _escortDeckStationFactor
                        : _escortStationFactor
                );
            if (escort.GetDistanceTo(screened) > station)
            {
                double angle = escort.GetAngleTo(screened);
                double approach = station * _attackApproachFraction;
                SteerShip(
                    escort,
                    screened.X - Math.Cos(angle) * approach,
                    screened.Y - Math.Sin(angle) * approach,
                    rounds,
                    clamp: true
                );
                return;
            }

            escort.IsOnStation = true;
            if (target?.IsTargetable == true && escort.IsArmed)
            {
                escort.Heading = TacticalBattleGeometry.TurnToward(
                    escort.Heading,
                    escort.GetDesiredFacing(target),
                    escort.TurnRate * rounds
                );
            }
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
        /// <returns>
        /// The height the squadron should be making for: that of the unit it is flying at, the
        /// battle plane for a station beyond the arena, or its own when it has nothing to match.
        /// </returns>
        private double MoveFighter(
            TacticalStarfighterUnit fighter,
            TacticalBattleForce force,
            double rounds
        )
        {
            if (fighter.IsDocked)
            {
                // A squadron aboard rides with its ship.
                fighter.X = fighter.RecoveryShip.X;
                fighter.Y = fighter.RecoveryShip.Y;
                return fighter.RecoveryShip.Altitude;
            }

            if (fighter.IsInTrench)
                return fighter.Altitude;

            // A squadron with no hyperdrive keeps looking for a bay while its fleet withdraws:
            // one opens when a squadron bound for it is lost.
            if (
                fighter.RecoveryShip == null
                && fighter.IsWithdrawing
                && !fighter.CanWithdrawIndependently
            )
                force.TryRecover(fighter, null, mustJump: true);

            if (fighter.RecoveryShip != null && FlyToHangar(fighter, force, rounds))
                return fighter.RecoveryShip.Altitude;

            if (fighter.IsWithdrawing)
            {
                TacticalCapitalShipUnit carrier = fighter.CanWithdrawIndependently
                    ? null
                    : FindRecoveryCarrier(fighter, force);
                if (carrier == null)
                {
                    MoveDirect(fighter, GetRetreatX(force.Side), fighter.Y, rounds, clamp: false);
                    return fighter.Altitude;
                }

                // A squadron with no bay to go to stays with the nearest ship still leaving; it
                // is lost when the last of them has jumped.
                double carrierDistance = fighter.GetDistanceTo(carrier);
                double recoveryRadius =
                    carrier.HullFootprint + _patrolClearance * fighter.OrbitRadiusScale;
                if (carrierDistance > recoveryRadius * 2)
                    MoveDirect(fighter, carrier.X, carrier.Y, rounds, clamp: false);
                else
                    Orbit(fighter, carrier.X, carrier.Y, carrierDistance, recoveryRadius, rounds);
                return carrier.Altitude;
            }

            if (fighter.TrenchRunTarget != null)
            {
                FlyTrenchRun(fighter, rounds);
                return 0;
            }

            if (IsOutsideArena(fighter))
            {
                // Back from a station beyond the edge: fly home before taking up the fight.
                MoveDirect(
                    fighter,
                    ClampX(fighter.X, fighter.Radius),
                    ClampY(fighter.Y, fighter.Radius),
                    rounds,
                    clamp: false
                );
                return fighter.Altitude;
            }

            TacticalBattleUnit target = fighter.Target;
            double engagementRange =
                target?.IsTargetable == true ? fighter.GetEngagementRange(target.IsStarfighter) : 0;
            if (engagementRange <= 0)
            {
                // Nothing to engage: a screening squadron circles its ship, any other flies a
                // patrol circle around the nearest friendly ship.
                TacticalCapitalShipUnit anchor =
                    fighter.ScreenedShip != null
                        ? CircleShip(fighter, fighter.ScreenedShip, rounds)
                        : PatrolNearFleet(fighter, force, rounds);
                return anchor?.Altitude ?? fighter.Altitude;
            }

            double distance = fighter.GetDistanceTo(target);
            if (fighter.GetEngagementDistanceTo(target) <= engagementRange)
            {
                OrbitTarget(fighter, target, distance, engagementRange, rounds);
                return target.Altitude;
            }

            MoveDirect(
                fighter,
                target.X,
                target.Y,
                rounds,
                clamp: true,
                engagementRange + target.HullFootprint
            );
            return target.Altitude;
        }

        /// <summary>
        /// Flies a recalled squadron to its hangar and lands it: it makes straight for the ship,
        /// off the battlefield if need be, and once over the hull it rides with the ship for the
        /// few seconds it takes to come aboard. A squadron whose ship is gone before it lands
        /// looks for another bay.
        /// </summary>
        /// <param name="fighter">The recalled squadron.</param>
        /// <param name="force">The squadron's force.</param>
        /// <param name="rounds">The elapsed combat rounds.</param>
        /// <returns>
        /// True while the squadron has a hangar to fly for; false when none is left and it is
        /// back to its other orders.
        /// </returns>
        private bool FlyToHangar(
            TacticalStarfighterUnit fighter,
            TacticalBattleForce force,
            double rounds
        )
        {
            if (
                !fighter.RecoveryShip.IsTargetable
                && !force.TryRecover(fighter, null, force.WithdrawalOrdered)
            )
                return false;

            TacticalCapitalShipUnit ship = fighter.RecoveryShip;
            double reach = Math.Max(ship.Radius, fighter.Speed * rounds);
            if (fighter.DockingFraction <= 0 && fighter.GetDistanceTo(ship) > reach)
            {
                MoveDirect(fighter, ship.X, ship.Y, rounds, clamp: false);
                if (fighter.GetDistanceTo(ship) > reach)
                    return true;
            }

            fighter.X = ship.X;
            fighter.Y = ship.Y;
            fighter.Heading = ship.Heading;
            fighter.AdvanceDocking(rounds / _dockingRounds);
            return true;
        }

        /// <summary>
        /// Flies a squadron straight for the station it was sent against, off the battlefield if
        /// need be, and takes it into the trench once it reaches the station's surface.
        /// </summary>
        /// <param name="fighter">The squadron flying the run.</param>
        /// <param name="rounds">The elapsed combat rounds.</param>
        private void FlyTrenchRun(TacticalStarfighterUnit fighter, double rounds)
        {
            TacticalBattleStation station = fighter.TrenchRunTarget;
            MoveDirect(fighter, station.X, station.Y, rounds, clamp: false);
            if (
                TacticalBattleGeometry.GetDistance(fighter.X, fighter.Y, station.X, station.Y)
                <= station.Radius
            )
                fighter.EnterTrench();
        }

        /// <summary>
        /// Returns whether a unit lies beyond the battlefield's edge.
        /// </summary>
        /// <param name="unit">The unit to inspect.</param>
        /// <returns>True when the unit is outside the arena.</returns>
        private bool IsOutsideArena(TacticalBattleUnit unit)
        {
            return Math.Abs(unit.X) > Math.Max(HalfWidth - unit.Radius, 0) + _arenaEdgeTolerance
                || Math.Abs(unit.Y) > Math.Max(HalfHeight - unit.Radius, 0) + _arenaEdgeTolerance;
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
        /// Returns whether a unit is near enough to a screened ship for its screen to take it
        /// on: within the screen's clearance of the ship's hull, hull to hull.
        /// </summary>
        /// <param name="screened">The ship being screened.</param>
        /// <param name="unit">The opposing unit.</param>
        /// <param name="slack">Extra distance allowed beyond the clearance.</param>
        /// <returns>True when the unit is inside the screen.</returns>
        private static bool IsInsideScreen(
            TacticalCapitalShipUnit screened,
            TacticalBattleUnit unit,
            double slack
        )
        {
            return screened.GetEngagementDistanceTo(unit) <= _screenClearance + slack;
        }

        /// <summary>
        /// Lists the opposing units a ship's screen may take on: those near the ship.
        /// </summary>
        /// <param name="screened">The ship being screened.</param>
        /// <param name="targets">The targetable opposing units.</param>
        /// <returns>The reusable list of units inside the screen.</returns>
        private IReadOnlyList<TacticalBattleUnit> GetScreenTargets(
            TacticalCapitalShipUnit screened,
            IReadOnlyList<TacticalBattleUnit> targets
        )
        {
            _screenTargets.Clear();
            foreach (TacticalBattleUnit target in targets)
            {
                if (IsInsideScreen(screened, target, 0))
                    _screenTargets.Add(target);
            }

            return _screenTargets;
        }

        /// <summary>
        /// Flies a squadron around a friendly capital ship just outside its hull, closing on the
        /// ship first when it is some way off.
        /// </summary>
        /// <param name="fighter">The squadron.</param>
        /// <param name="ship">The ship to circle.</param>
        /// <param name="rounds">The rounds of movement to apply.</param>
        /// <returns>The ship the squadron circles.</returns>
        private TacticalCapitalShipUnit CircleShip(
            TacticalStarfighterUnit fighter,
            TacticalCapitalShipUnit ship,
            double rounds
        )
        {
            double distance = fighter.GetDistanceTo(ship);
            double radius = ship.HullFootprint + _patrolClearance * fighter.OrbitRadiusScale;
            if (distance > radius * 2)
                MoveDirect(fighter, ship.X, ship.Y, rounds, clamp: true);
            else
                Orbit(fighter, ship.X, ship.Y, distance, radius, rounds);
            return ship;
        }

        /// <summary>
        /// Keeps an idle squadron flying: it circles the nearest friendly capital ship just
        /// outside its hull, or holds position when the fleet has no ships left.
        /// </summary>
        /// <param name="fighter">The squadron without a target it can engage.</param>
        /// <param name="force">The squadron's force.</param>
        /// <param name="rounds">The rounds of movement to apply.</param>
        /// <returns>The ship the squadron circles, or null when the fleet has none left.</returns>
        private TacticalCapitalShipUnit PatrolNearFleet(
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
                return null;

            Orbit(
                fighter,
                anchor.X,
                anchor.Y,
                anchorDistance,
                anchor.HullFootprint + _patrolClearance * fighter.OrbitRadiusScale,
                rounds
            );
            return anchor;
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
        /// Finds the nearest friendly carrier still able to jump, for a squadron with no bay to
        /// go to and no hyperdrive to stay with while the fleet leaves.
        /// </summary>
        /// <param name="fighter">The carrier-dependent squadron.</param>
        /// <param name="force">The squadron's force.</param>
        /// <returns>The nearest such carrier, or null.</returns>
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
                // A ship held in a tractor beam cannot make its jump, and one with squadrons
                // still coming aboard holds it for them.
                if (
                    unit is TacticalCapitalShipUnit ship
                    && (ship.IsTractored || force.IsAwaitingSquadrons(ship))
                )
                    continue;

                if (
                    unit.IsAlive
                    && unit.IsWithdrawing
                    && unit.CanWithdrawIndependently
                    && Math.Abs(unit.X) >= HalfWidth + _withdrawalMargin
                )
                    unit.FinishWithdrawal();
            }

            // A station jumps last, once its countdown has run and its fleet is clear.
            bool fleetLeaving = force.HasWithdrawingIndependentFieldUnit;
            foreach (TacticalBattleStation station in force.Stations)
            {
                if (
                    station.IsAlive
                    && station.IsWithdrawing
                    && station.CanWithdrawIndependently
                    && station.IsJumpReady
                    && !fleetLeaving
                )
                    station.FinishWithdrawal();
            }

            // Squadrons inside a trench settle their run before the stragglers are counted.
            if (
                force.HasWithdrawingIndependentUnit
                || force.Fighters.Any(fighter => fighter.IsAlive && fighter.IsInTrench)
            )
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

            // A battle station nobody can destroy holds the field: the other side gives way.
            bool attackerHolds = _attacker.HasStationInBattle;
            bool defenderHolds = _defender.HasStationInBattle;
            if (attackerHolds != defenderHolds)
            {
                CompleteStalematedForce(attackerHolds ? _defender : _attacker);
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
        private void CompleteStalematedForce(TacticalBattleForce force)
        {
            if (!force.WithdrawalOrdered && CanWithdraw(force.Side))
                force.CompleteWithdrawal();

            foreach (
                TacticalBattleUnit unit in force.Units.Where(unit =>
                    unit.IsAlive && !unit.HasWithdrawn
                )
            )
                unit.Destroy();
            foreach (TacticalBattleStation station in force.Stations)
            {
                if (!station.IsInBattle)
                    continue;

                if (station.IsWithdrawing && station.CanWithdrawIndependently)
                    station.FinishWithdrawal();
                else
                    station.Destroy();
            }

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
            PlaceShips(force, cornerX, cornerY, heading);

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
                    fighter.Altitude = carrier.Altitude;
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
        /// Forms a force's capital ships up around its corner in lines across its heading. A line
        /// holds as many ships as fit across the arena with room between their hulls, and a fleet
        /// too large for one line stacks up to three of them on decks one above another, so every
        /// ship stands at the same distance from the enemy: the ships of the line first, largest
        /// in the middle, then the escorts and unarmed ships. Only a fleet too large for three
        /// decks forms further ranks behind. A formation that would overhang the arena is slid
        /// back inside it.
        /// </summary>
        /// <param name="force">The force to place.</param>
        /// <param name="cornerX">The horizontal coordinate the formation is centered on.</param>
        /// <param name="cornerY">The vertical coordinate the formation is centered on.</param>
        /// <param name="heading">The heading the force faces.</param>
        private void PlaceShips(
            TacticalBattleForce force,
            double cornerX,
            double cornerY,
            double heading
        )
        {
            List<TacticalCapitalShipUnit> ships = force
                .Ships.OrderBy(ship => ship.IsEscort || !ship.IsArmed ? 1 : 0)
                .ThenByDescending(ship => ship.MaximumHull)
                .ToList();
            if (ships.Count == 0)
                return;

            double forwardX = Math.Cos(heading);
            double forwardY = Math.Sin(heading);
            double acrossX = -forwardY;
            double acrossY = forwardX;
            double spacing = Math.Max(
                _shipSpacing,
                2 * ships.Max(ship => ship.HullFootprint) * _formationClearance
            );
            int lineCapacity = Math.Max((int)Math.Floor(2 * HalfHeight / spacing), 1);
            int rankCapacity = lineCapacity * _deckCount;
            int ranks = (ships.Count + rankCapacity - 1) / rankCapacity;
            for (int rank = 0; rank < ranks; rank++)
            {
                int first = rank * rankCapacity;
                int inRank = Math.Min(rankCapacity, ships.Count - first);
                // As few lines as hold the rank's ships, filled evenly rather than leaving a
                // last line of one.
                int lines = (inRank + lineCapacity - 1) / lineCapacity;
                int perLine = (inRank + lines - 1) / lines;
                double behind = (rank - (ranks - 1) / 2.0) * spacing;
                for (int index = 0; index < inRank; index++)
                {
                    TacticalCapitalShipUnit ship = ships[first + index];
                    int line = index / perLine;
                    int inLine = Math.Min(perLine, inRank - line * perLine);
                    // Slots alternate outward from the middle; an even line is centered between
                    // two. A line above or below the first stands half a slot to one side when
                    // its ships would otherwise hide the ones beneath them.
                    double slot =
                        GetAlternatingOffset(index % perLine) - (inLine % 2 == 0 ? 0.5 : 0);
                    if (line > 0 && inLine % 2 == perLine % 2)
                        slot += line == 1 ? 0.5 : -0.5;
                    ship.X = cornerX + acrossX * slot * spacing - forwardX * behind;
                    ship.Y = cornerY + acrossY * slot * spacing - forwardY * behind;
                    ship.Altitude = GetDeck(line, lines) * DeckSpacing;
                    ship.Heading = heading;
                }
            }

            SlideInsideArena(ships);
        }

        /// <summary>
        /// Returns the deck one line of a formation stands on, in deck spacings above the battle
        /// plane. One line stands on the plane; two stand half a deck above and below it; three
        /// stand on it, above it and below it. The first line, which holds the largest ships,
        /// takes the middle place, or the upper one of two.
        /// </summary>
        /// <param name="line">The zero-based line within its rank.</param>
        /// <param name="lines">The number of lines in the rank.</param>
        /// <returns>The deck, in deck spacings above the plane.</returns>
        private static double GetDeck(int line, int lines)
        {
            if (lines <= 1)
                return 0;
            if (lines == 2)
                return line == 0 ? 0.5 : -0.5;
            return line == 0 ? 0
                : line == 1 ? 1
                : -1;
        }

        /// <summary>
        /// Moves a formation as a whole so that none of its ships lies beyond the arena's edge,
        /// centering it on an axis it is too large for, then clamps any ship still outside.
        /// </summary>
        /// <param name="ships">The ships of the formation.</param>
        private void SlideInsideArena(IReadOnlyList<TacticalCapitalShipUnit> ships)
        {
            double pastLeft = ships.Max(ship => -Math.Max(HalfWidth - ship.Radius, 0) - ship.X);
            double pastRight = ships.Max(ship => ship.X - Math.Max(HalfWidth - ship.Radius, 0));
            double pastBottom = ships.Max(ship => -Math.Max(HalfHeight - ship.Radius, 0) - ship.Y);
            double pastTop = ships.Max(ship => ship.Y - Math.Max(HalfHeight - ship.Radius, 0));
            double shiftX = GetSlide(pastLeft, pastRight);
            double shiftY = GetSlide(pastBottom, pastTop);
            foreach (TacticalCapitalShipUnit ship in ships)
            {
                ship.X += shiftX;
                ship.Y += shiftY;
                ClampToArena(ship);
            }
        }

        /// <summary>
        /// Returns how far to slide a formation along one axis given how far it overhangs each
        /// end: back inside from the end it overhangs, or to the middle when it cannot fit.
        /// </summary>
        /// <param name="pastLow">How far the formation lies beyond the low edge, if positive.</param>
        /// <param name="pastHigh">How far the formation lies beyond the high edge, if positive.</param>
        /// <returns>The signed distance to move the formation.</returns>
        private static double GetSlide(double pastLow, double pastHigh)
        {
            // Too large for the axis: share the overhang between both ends.
            if (pastLow + pastHigh > 0)
                return (pastLow - pastHigh) / 2;
            if (pastLow > 0)
                return pastLow;
            return pastHigh > 0 ? -pastHigh : 0;
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
                // A squadron aboard a ship shares the ship's end: no blast or jump of its own.
                bool aboard = unit is TacticalStarfighterUnit squadron && squadron.IsDocked;
                if (!unit.IsAlive && _reportedDestroyed.Add(unit))
                {
                    if (!aboard)
                        _events.Add(
                            new TacticalBattleEvent(TacticalBattleEventKind.UnitDestroyed, unit)
                        );
                }
                else if (unit.HasWithdrawn && _reportedWithdrawn.Add(unit))
                {
                    if (!aboard)
                        _events.Add(
                            new TacticalBattleEvent(TacticalBattleEventKind.UnitWithdrew, unit)
                        );
                }
            }

            foreach (TacticalBattleStation station in _stations)
            {
                if (station.IsInBattle || !_reportedStations.Add(station))
                    continue;

                _events.Add(
                    new TacticalBattleEvent(
                        station.IsAlive
                            ? TacticalBattleEventKind.StationWithdrew
                            : TacticalBattleEventKind.StationDestroyed,
                        station
                    )
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
        /// Records destroyed battle stations in a combat result. A station that survives was
        /// never hit: nothing on the battlefield can reach it.
        /// </summary>
        /// <param name="result">The result receiving damage records.</param>
        /// <param name="stations">The resolved battle stations.</param>
        private static void AddStationResults(
            SpaceCombatResult result,
            IReadOnlyList<TacticalBattleStation> stations
        )
        {
            foreach (TacticalBattleStation station in stations)
            {
                if (station.IsAlive)
                    continue;

                result.ShipDamage.Add(
                    new ShipDamageResult
                    {
                        Ship = station.Ship,
                        HullBefore = station.Ship.CurrentHullStrength,
                        HullAfter = 0,
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
        /// Appends the squadrons that ended the battle aboard a ship, so each stays with the ship
        /// it docked with.
        /// </summary>
        /// <param name="result">The result receiving docking records.</param>
        /// <param name="fighters">The resolved fighter squadrons.</param>
        private static void AddDockingResults(
            SpaceCombatResult result,
            IReadOnlyList<TacticalStarfighterUnit> fighters
        )
        {
            foreach (TacticalStarfighterUnit fighter in fighters)
            {
                if (!fighter.IsDocked || !fighter.IsAlive)
                    continue;

                result.FighterDockings.Add(
                    new FighterDockingResult
                    {
                        Fighter = fighter.Fighter,
                        Ship = fighter.RecoveryShip.Ship,
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
                .Ships.Select(ship => ship.Ship)
                .Concat(force.Stations.Select(station => station.Ship))
                .SelectMany(ship =>
                    new[] { ship }
                        .Cast<ISceneNode>()
                        .Concat(ship.GetChildren<ISceneNode>(recursive: true))
                )
                .Concat(force.Fighters.Select(fighter => fighter.Fighter))
                .Where(unit => unit != null)
                .Distinct()
                .Select(unit => new CombatUnitSnapshot(unit))
                .ToList();
            CombatUnitSnapshot.RecordOutcomes(
                units,
                force.Units.Where(IsDamaged).Select(unit => unit.Node),
                force
                    .Units.Where(unit => !unit.IsAlive)
                    .Select(unit => unit.Node)
                    .Concat(
                        force
                            .Stations.Where(station => !station.IsAlive)
                            .Select(station => (ISceneNode)station.Ship)
                    )
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

            bool attackerSurvived =
                _attacker.Units.Any(unit => unit.IsAlive)
                || _attacker.Stations.Any(station => station.IsAlive);
            bool defenderSurvived =
                _defender.Units.Any(unit => unit.IsAlive)
                || _defender.Stations.Any(station => station.IsAlive);
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
