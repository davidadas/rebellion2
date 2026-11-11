using System;
using System.Collections.Generic;
using Rebellion.Game;
using Rebellion.Game.Results;
using Rebellion.Game.Units;
using Rebellion.SceneGraph;
using Rebellion.Util.Random;

namespace Rebellion.Gameplay
{
    /// <summary>
    /// Identifies the standing order a capital ship follows in a tactical battle.
    /// </summary>
    public enum TacticalShipOrder
    {
        None,
        Move,
        Attack,
    }

    /// <summary>
    /// Stores a capital ship's tactical hull, shield, weapon-arc, and order state.
    /// </summary>
    public sealed class TacticalCapitalShipUnit : TacticalBattleUnit
    {
        private const double _turnRateBase = 0.08;
        private const double _turnRatePerManeuverability = 0.05;
        private const double _preferredRangeFraction = 0.55;
        private const double _tractorApproachFraction = 0.9;
        private const double _tractoredSpeedFactor = 0.2;
        private const double _shieldsDownFraction = 0.05;
        private const double _roundsPerRepairDay = 20;
        private const double _maximumShieldDelay = 200;
        private const double _baseRadius = 2;
        private const double _hullRadiusDivisor = 16;
        private static readonly PrimaryWeaponType[] _weaponTypes =
        {
            PrimaryWeaponType.Turbolaser,
            PrimaryWeaponType.LaserCannon,
            PrimaryWeaponType.IonCannon,
        };

        private readonly bool[] _arcQueuedForRecharge = new bool[
            CapitalShip.PrimaryWeaponArcs.Count
        ];
        private readonly double[] _currentArcCharge = new double[
            CapitalShip.PrimaryWeaponArcs.Count
        ];
        private readonly double[] _maximumArcCharge = new double[
            CapitalShip.PrimaryWeaponArcs.Count
        ];
        private readonly int[] _ionCannons;
        private readonly int[] _laserCannons;
        private readonly int[] _turbolasers;
        private readonly double _laserCannonCapitalDamageMultiplier;
        private readonly double _turbolaserFighterHitChance;
        private readonly double _commandBonus;
        private readonly double _maximumHull;
        private readonly double _maximumShields;
        private readonly double _maximumWeaponRange;
        private readonly double _capitalBatteryRange;
        private readonly double _fighterBatteryRange;
        private readonly double _rangeMultiplier;
        private readonly double _radius;
        private readonly Queue<int> _rechargeQueue = new Queue<int>();
        private double _attackDelay;
        private double _movementDelay;
        private double _shieldDelay;
        private double _scanCooldown;

        public CapitalShip Ship { get; }

        public int InitialHull { get; }

        public double CurrentHull { get; private set; }

        public double CurrentShields { get; private set; }

        public double MaximumHull => _maximumHull;

        public double MaximumShields => _maximumShields;

        public TacticalShipOrder Order { get; private set; }

        public TacticalBattleUnit Target { get; private set; }

        public bool HasDestination { get; private set; }

        public double DestinationX { get; private set; }

        public double DestinationY { get; private set; }

        public bool IsArmed => _maximumWeaponRange > 0;

        public double MaximumWeaponRange => _maximumWeaponRange;

        public bool WeaponsOffline => _attackDelay > 0;

        public bool EnginesOffline => _movementDelay > 0;

        /// <summary>
        /// Gets whether the shield generator is knocked out, so the shields cannot recharge.
        /// </summary>
        public bool ShieldGeneratorOffline => _shieldDelay > 0;

        /// <summary>
        /// Gets whether the shields are down, leaving the hull open to torpedoes: at or below a
        /// sliver of their maximum, so a trickle of recharge under fire does not count as cover.
        /// </summary>
        public bool ShieldsDown =>
            _maximumShields <= 0 || CurrentShields <= _maximumShields * _shieldsDownFraction;

        /// <summary>
        /// Gets the generator repair still owed, in strategic recovery ticks, to carry out of
        /// the battle.
        /// </summary>
        public int ShieldGeneratorRepairDays =>
            (int)Math.Ceiling(_shieldDelay / _roundsPerRepairDay);

        public bool HasHyperdrive => Ship.Hyperdrive > 0;

        /// <summary>
        /// Gets whether the ship is fitted with gravity well projectors.
        /// </summary>
        public bool HasGravityWell => Ship.HasGravityWell;

        /// <summary>
        /// Gets whether the ship is projecting a gravity well right now, holding enemy ships in
        /// the system: it has the projectors and is still in the battle.
        /// </summary>
        public bool ProjectsGravityWell => HasGravityWell && IsTargetable;

        /// <summary>
        /// Gets whether the ship is an escort: its laser cannons outweigh its heavy batteries, so
        /// it screens the ships of the line against fighters instead of joining the line.
        /// </summary>
        public bool IsEscort { get; }

        /// <summary>
        /// Gets or sets whether the ship is holding the place it fights from: its target, or the
        /// enemy line, is as near as it means to get. Friends closing up behind wait for it and
        /// give way to it rather than pushing it on.
        /// </summary>
        internal bool IsOnStation { get; set; }

        /// <summary>
        /// Gets the ship's tractor beam strength, or zero when it carries no beam.
        /// </summary>
        public int TractorBeamPower => Math.Max(Ship.TractorBeamPower, 0);

        /// <summary>
        /// Gets how far from a target's hull the ship's tractor beam reaches.
        /// </summary>
        public int TractorBeamRange => Math.Max(Ship.TractorBeamnRange, 0);

        /// <summary>
        /// Gets the enemy ship whose tractor beam currently holds this ship, if any.
        /// </summary>
        public TacticalCapitalShipUnit TractoredBy { get; internal set; }

        /// <summary>
        /// Gets whether an enemy tractor beam currently holds this ship.
        /// </summary>
        public bool IsTractored => TractoredBy != null;

        /// <summary>
        /// Gets the ship this ship's tractor beam held on the previous step, if any.
        /// </summary>
        internal TacticalCapitalShipUnit HeldShip { get; set; }

        public override ISceneNode Node => Ship;

        public override bool IsAlive => CurrentHull > 0;

        public override bool IsStarfighter => false;

        public override double Radius => _radius;

        internal override bool CanWithdrawIndependently => Ship.Hyperdrive > 0;

        internal override double RemainingDurability => CurrentHull + CurrentShields;

        internal override double LastingDurability => CurrentHull;

        internal override bool IsAttackDelayed => _attackDelay > 0;

        internal override double Speed =>
            _movementDelay > 0
                ? 0
                : Math.Max(Ship.SublightSpeed, 0) * (IsTractored ? _tractoredSpeedFactor : 1);

        internal override double ManeuverRate =>
            _movementDelay > 0
                ? MinimumManeuverRatio
                : Math.Max(
                    Ship.SublightSpeed + Ship.Maneuverability + _commandBonus,
                    MinimumManeuverRatio
                );

        internal double TurnRate =>
            _movementDelay > 0
                ? 0
                : _turnRateBase + _turnRatePerManeuverability * Math.Max(Ship.Maneuverability, 0);

        internal bool HasTractorBeam => TractorBeamPower > 0 && TractorBeamRange > 0;

        internal bool IsAutomatic => Order == TacticalShipOrder.None;

        internal bool IsScanDue => _scanCooldown <= 0;

        /// <summary>
        /// Creates tactical state from a capital ship's current strategic state.
        /// </summary>
        /// <param name="ship">The capital ship entering combat.</param>
        /// <param name="side">The side the ship fights for.</param>
        /// <param name="config">The space-combat parameters.</param>
        /// <param name="commandBonus">The Admiral's Leadership-derived tactical bonus.</param>
        internal TacticalCapitalShipUnit(
            CapitalShip ship,
            CombatSide side,
            GameConfig.SpaceCombatConfig config,
            double commandBonus
        )
            : base(side, config.AutoResolveMinimumManeuverRatio)
        {
            Ship = ship ?? throw new ArgumentNullException(nameof(ship));
            InitialHull = Math.Max(ship.CurrentHullStrength, 0);
            _commandBonus = Math.Max(commandBonus, 0);
            _maximumHull = Math.Max(ship.MaxHullStrength, 1);
            _maximumShields = Math.Max(ship.MaxShieldStrength, 0);
            _turbolasers = GetWeaponValues(ship, PrimaryWeaponType.Turbolaser);
            _laserCannons = GetWeaponValues(ship, PrimaryWeaponType.LaserCannon);
            _ionCannons = GetWeaponValues(ship, PrimaryWeaponType.IonCannon);
            _laserCannonCapitalDamageMultiplier = Math.Max(
                config.LaserCannonCapitalDamageMultiplier,
                0
            );
            _turbolaserFighterHitChance = Math.Clamp(
                config.TacticalTurbolaserFighterHitChance,
                0,
                1
            );
            _rangeMultiplier =
                config.TacticalCapitalWeaponRangeMultiplier > 0
                    ? config.TacticalCapitalWeaponRangeMultiplier
                    : 1;
            _radius = _baseRadius + Math.Sqrt(_maximumHull) / _hullRadiusDivisor;
            CurrentHull = InitialHull;
            CurrentShields = _maximumShields;
            // Generator damage carried in from an earlier battle is still being repaired.
            _shieldDelay = Math.Max(ship.ShieldGeneratorRepairDays, 0) * _roundsPerRepairDay;
            for (int arc = 0; arc < _maximumArcCharge.Length; arc++)
            {
                _maximumArcCharge[arc] = GetRawArcStrength(arc);
                _currentArcCharge[arc] = _maximumArcCharge[arc];
            }

            _maximumWeaponRange = GetMaximumWeaponRange();
            _capitalBatteryRange = GetMainBatteryRange(targetsFighters: false);
            _fighterBatteryRange = GetMainBatteryRange(targetsFighters: true);
            // Heavy batteries hunt capital ships; laser-heavy escorts screen against fighters.
            IsEscort =
                SumArcStrength(_laserCannons)
                > SumArcStrength(_turbolasers) + SumArcStrength(_ionCannons);
            TargetPriority = IsEscort
                ? TacticalTargetPriority.Starfighters
                : TacticalTargetPriority.CapitalShips;
        }

        /// <summary>
        /// Returns how much of a firing arc's weapon charge is currently available.
        /// </summary>
        /// <param name="arc">The firing arc.</param>
        /// <returns>The charged fraction, or zero for an unarmed arc.</returns>
        public double GetArcChargeFraction(PrimaryWeaponArc arc)
        {
            int index = (int)arc;
            if (index < 0 || index >= _maximumArcCharge.Length || _maximumArcCharge[index] <= 0)
                return 0;

            return _currentArcCharge[index] / _maximumArcCharge[index];
        }

        /// <summary>
        /// Orders the ship to travel to a position.
        /// </summary>
        /// <param name="x">The destination horizontal coordinate.</param>
        /// <param name="y">The destination vertical coordinate.</param>
        internal void SetMoveOrder(double x, double y)
        {
            Order = TacticalShipOrder.Move;
            Target = null;
            SetDestination(x, y);
        }

        /// <summary>
        /// Orders the ship to pursue and engage a target.
        /// </summary>
        /// <param name="target">The opposing unit to engage.</param>
        internal void SetAttackOrder(TacticalBattleUnit target)
        {
            Order = TacticalShipOrder.Attack;
            Target = target;
            ClearDestination();
        }

        /// <summary>
        /// Returns the ship to automatic engagement.
        /// </summary>
        internal void ClearOrder()
        {
            Order = TacticalShipOrder.None;
            Target = null;
            ClearDestination();
        }

        /// <summary>
        /// Records an automatically selected target and restarts the scan cooldown.
        /// </summary>
        /// <param name="target">The selected target, or null when nothing can be engaged.</param>
        /// <param name="scanInterval">The rounds until the next automatic scan.</param>
        internal void SetAutomaticTarget(TacticalBattleUnit target, double scanInterval)
        {
            Target = target;
            _scanCooldown = scanInterval;
        }

        /// <summary>
        /// Makes the ship pick a new automatic target on the next step.
        /// </summary>
        internal void RequestScan()
        {
            _scanCooldown = 0;
        }

        /// <summary>
        /// Sets the position the ship steers toward.
        /// </summary>
        /// <param name="x">The destination horizontal coordinate.</param>
        /// <param name="y">The destination vertical coordinate.</param>
        internal void SetDestination(double x, double y)
        {
            HasDestination = true;
            DestinationX = x;
            DestinationY = y;
        }

        /// <summary>
        /// Clears the position the ship steers toward.
        /// </summary>
        internal void ClearDestination()
        {
            HasDestination = false;
        }

        /// <summary>
        /// Advances the automatic target-scan cooldown.
        /// </summary>
        /// <param name="rounds">The elapsed combat rounds.</param>
        internal void AdvanceScanCooldown(double rounds)
        {
            _scanCooldown = Math.Max(_scanCooldown - rounds, 0);
        }

        /// <summary>
        /// Scores how attractive an opposing unit is as an automatic target.
        /// </summary>
        /// <param name="candidate">The opposing unit.</param>
        /// <returns>The target score, where higher is better.</returns>
        internal double ScoreTarget(TacticalBattleUnit candidate)
        {
            double condition = CurrentHull / _maximumHull;
            double potential =
                GetStrongestArcStrength(candidate.IsStarfighter)
                * GetManeuverMultiplier(candidate)
                * condition;
            return potential / (1 + GetDistanceTo(candidate) / Math.Max(_maximumWeaponRange, 1));
        }

        /// <summary>
        /// Returns the heading that points the ship's strongest battery against the target's
        /// class at the target. The choice ignores current charge, so the ship holds its facing
        /// between volleys instead of swinging each arc around in turn.
        /// </summary>
        /// <param name="target">The unit to face.</param>
        /// <returns>The desired heading in radians.</returns>
        internal double GetDesiredFacing(TacticalBattleUnit target)
        {
            double angleToTarget = GetAngleTo(target);
            int selectedArc = -1;
            double selectedStrength = 0;
            double selectedTurn = double.MaxValue;
            for (int arc = 0; arc < _maximumArcCharge.Length; arc++)
            {
                double strength = GetArcStrength(arc, target.IsStarfighter);
                if (strength <= 0)
                    continue;

                double desired =
                    angleToTarget - TacticalBattleGeometry.GetArcOffset((PrimaryWeaponArc)arc);
                double turn = Math.Abs(TacticalBattleGeometry.NormalizeAngle(desired - Heading));
                if (
                    strength < selectedStrength
                    || (
                        Math.Abs(strength - selectedStrength) < double.Epsilon
                        && turn >= selectedTurn
                    )
                )
                    continue;

                selectedArc = arc;
                selectedStrength = strength;
                selectedTurn = turn;
            }

            if (selectedArc < 0)
                return angleToTarget;

            return angleToTarget
                - TacticalBattleGeometry.GetArcOffset((PrimaryWeaponArc)selectedArc);
        }

        /// <summary>
        /// Calculates the capital ship's remaining tactical strength.
        /// </summary>
        /// <param name="targetsFighters">Whether the opposing target is a fighter squadron.</param>
        /// <returns>The remaining tactical strength.</returns>
        internal override double GetEffectiveness(bool targetsFighters)
        {
            double condition = CurrentHull / _maximumHull;
            return GetStrongestArcStrength(targetsFighters) * condition;
        }

        /// <summary>
        /// Queues attacks from every fully charged arc at targets inside that arc and in range.
        /// </summary>
        /// <param name="targets">The targetable opposing units.</param>
        /// <param name="hitSink">Receives each queued hit.</param>
        /// <param name="shots">Receives presentation records for each fired lane.</param>
        /// <param name="shotDisplaySeconds">How long each fired lane remains visible.</param>
        /// <param name="random">The combat random-number stream used for hit rolls.</param>
        internal override void QueueAttacks(
            IReadOnlyList<TacticalBattleUnit> targets,
            TacticalHitSink hitSink,
            ICollection<TacticalShot> shots,
            double shotDisplaySeconds,
            IRandomNumberProvider random
        )
        {
            double condition = CurrentHull / _maximumHull;
            for (int arc = 0; arc < _currentArcCharge.Length; arc++)
            {
                if (_maximumArcCharge[arc] <= 0 || _currentArcCharge[arc] < _maximumArcCharge[arc])
                    continue;

                double consumedCharge = 0;
                for (int weaponIndex = 0; weaponIndex < _weaponTypes.Length; weaponIndex++)
                {
                    PrimaryWeaponType type = _weaponTypes[weaponIndex];
                    TacticalBattleUnit target = SelectLaneTarget(
                        arc,
                        type,
                        targets,
                        condition,
                        out int weaponStrength,
                        out double damage,
                        out double hitChance
                    );
                    if (target == null)
                        continue;

                    bool isIon = type == PrimaryWeaponType.IonCannon;
                    if (hitChance >= 1 || random.NextDouble() < hitChance)
                        QueueHit(
                            hitSink,
                            shots,
                            target,
                            damage,
                            isIon,
                            shotDisplaySeconds,
                            weaponStrength
                        );
                    else
                        QueueMiss(shots, target, isIon, shotDisplaySeconds, weaponStrength, random);
                    consumedCharge += weaponStrength;
                }

                if (consumedCharge > 0)
                    DischargeArc(arc, consumedCharge);
            }
        }

        /// <summary>
        /// Applies simultaneous conventional and ion damage to the capital ship.
        /// </summary>
        /// <param name="conventionalDamage">The non-negative hull-damaging attack strength.</param>
        /// <param name="ionDamage">The non-negative shield and subsystem attack strength.</param>
        /// <param name="config">The space-combat parameters.</param>
        /// <param name="random">The combat random-number stream.</param>
        internal override void ApplyDamage(
            double conventionalDamage,
            double ionDamage,
            GameConfig.SpaceCombatConfig config,
            IRandomNumberProvider random
        )
        {
            double hullDamage = ApplyShieldDamage(conventionalDamage);
            CurrentHull = Math.Max(CurrentHull - hullDamage, 0);
            if (!IsAlive)
                return;

            double ionOverflowDamage = ApplyShieldDamage(ionDamage);
            ApplyComponentDamage(ionOverflowDamage, config, random);
            RollShieldGeneratorDamage(config, random);
        }

        /// <summary>
        /// Gives every hit a small chance of knocking the shield generator offline, so the many
        /// light hits of a strafing squadron threaten it more than a few heavy broadsides do.
        /// </summary>
        /// <param name="config">The space-combat parameters.</param>
        /// <param name="random">The combat random-number stream.</param>
        private void RollShieldGeneratorDamage(
            GameConfig.SpaceCombatConfig config,
            IRandomNumberProvider random
        )
        {
            double chance = Math.Clamp(config.TacticalShieldGeneratorHitChance, 0, 1);
            if (_maximumShields <= 0 || chance <= 0 || random.NextDouble() >= chance)
                return;

            _shieldDelay = Math.Min(
                _shieldDelay + GetComponentDelay(config, random),
                _maximumShieldDelay
            );
        }

        /// <summary>
        /// Advances component recovery, shield recharge, and weapon recharge.
        /// </summary>
        /// <param name="rounds">The elapsed combat rounds.</param>
        /// <param name="config">The space-combat parameters.</param>
        internal override void AdvanceState(double rounds, GameConfig.SpaceCombatConfig config)
        {
            double delayRecovery = Math.Max(config.AutoResolveComponentDelayRecovery, 1) * rounds;
            _attackDelay = Math.Max(_attackDelay - delayRecovery, 0);
            _movementDelay = Math.Max(_movementDelay - delayRecovery, 0);
            // A knocked-out generator takes far longer to bring back than other components, so
            // the loss shapes the rest of the battle rather than a few volleys.
            double generatorRecovery =
                delayRecovery / Math.Max(config.TacticalShieldGeneratorRepairMultiplier, 1);
            _shieldDelay = Math.Max(_shieldDelay - generatorRecovery, 0);
            RechargeShields(rounds, config);
            RechargeWeapons(rounds);
        }

        /// <summary>
        /// Returns the distance the ship closes to against a target and then holds: the gap
        /// between the two hulls is a comfortable fraction of the range of its main battery
        /// against that class of target, so ships stand off from each other instead of closing
        /// until their hulls meet. A ship with a tractor beam comes nearer to hold a capital
        /// ship that is running.
        /// </summary>
        /// <param name="target">The unit being engaged.</param>
        /// <returns>The preferred center-to-center distance.</returns>
        internal double GetPreferredRange(TacticalBattleUnit target)
        {
            double battery = target.IsStarfighter ? _fighterBatteryRange : _capitalBatteryRange;
            double gap = battery * _preferredRangeFraction;
            if (HasTractorBeam && target is TacticalCapitalShipUnit && target.IsWithdrawing)
                gap = Math.Min(gap, TractorBeamRange * _tractorApproachFraction);

            return gap + target.HullFootprint + HullFootprint;
        }

        /// <summary>
        /// Destroys the capital ship by removing its hull and shields.
        /// </summary>
        internal override void Destroy()
        {
            CurrentHull = 0;
            CurrentShields = 0;
        }

        /// <summary>
        /// Selects the target one weapon lane fires at from a charged arc.
        /// </summary>
        /// <param name="arc">The zero-based firing arc.</param>
        /// <param name="type">The weapon type.</param>
        /// <param name="targets">The targetable opposing units.</param>
        /// <param name="condition">The ship's current hull condition.</param>
        /// <param name="weaponStrength">Receives the raw weapon strength consumed by the shot.</param>
        /// <param name="damage">Receives the damage dealt when the shot hits.</param>
        /// <param name="hitChance">Receives the chance, from 0 to 1, that the shot hits.</param>
        /// <returns>The selected target, or null when nothing is in the arc and in range.</returns>
        private TacticalBattleUnit SelectLaneTarget(
            int arc,
            PrimaryWeaponType type,
            IReadOnlyList<TacticalBattleUnit> targets,
            double condition,
            out int weaponStrength,
            out double damage,
            out double hitChance
        )
        {
            if (
                Target?.IsTargetable == true
                && TryEvaluateLane(
                    arc,
                    type,
                    Target,
                    condition,
                    out weaponStrength,
                    out damage,
                    out hitChance
                )
            )
                return Target;

            TacticalBattleUnit selected = null;
            weaponStrength = 0;
            damage = 0;
            hitChance = 0;
            foreach (TacticalBattleUnit candidate in targets)
            {
                // Candidates compete on expected damage, so a battery that rarely hits fighters
                // still prefers a hull it can reach.
                if (
                    !TryEvaluateLane(
                        arc,
                        type,
                        candidate,
                        condition,
                        out int candidateStrength,
                        out double candidateDamage,
                        out double candidateHitChance
                    )
                    || candidateDamage * candidateHitChance <= damage * hitChance
                )
                    continue;

                selected = candidate;
                weaponStrength = candidateStrength;
                damage = candidateDamage;
                hitChance = candidateHitChance;
            }

            return selected;
        }

        /// <summary>
        /// Evaluates whether one weapon lane can hit a candidate from an arc.
        /// </summary>
        /// <param name="arc">The zero-based firing arc.</param>
        /// <param name="type">The weapon type.</param>
        /// <param name="candidate">The opposing unit.</param>
        /// <param name="condition">The ship's current hull condition.</param>
        /// <param name="weaponStrength">Receives the raw weapon strength.</param>
        /// <param name="damage">Receives the damage dealt when the shot hits.</param>
        /// <param name="hitChance">Receives the chance, from 0 to 1, that the shot hits.</param>
        /// <returns>True when the candidate is inside the arc and in range.</returns>
        private bool TryEvaluateLane(
            int arc,
            PrimaryWeaponType type,
            TacticalBattleUnit candidate,
            double condition,
            out int weaponStrength,
            out double damage,
            out double hitChance
        )
        {
            weaponStrength = 0;
            damage = 0;
            hitChance = GetHitChance(type, candidate.IsStarfighter);
            if (TacticalBattleGeometry.GetArc(GetBearingTo(candidate)) != (PrimaryWeaponArc)arc)
                return false;

            // Weapons reach from hull to hull, as tractor beams do.
            weaponStrength = GetWeaponStrength(
                type,
                arc,
                GetEngagementDistanceTo(candidate),
                requireRange: true
            );
            if (weaponStrength <= 0)
                return false;

            damage =
                weaponStrength
                * GetTargetTypeMultiplier(type, candidate.IsStarfighter)
                * GetManeuverMultiplier(candidate)
                * condition;
            return damage > 0;
        }

        /// <summary>
        /// Consumes shield strength and returns damage that penetrated the shields.
        /// </summary>
        /// <param name="damage">The non-negative incoming damage.</param>
        /// <returns>The damage remaining after the shields absorb what they can.</returns>
        private double ApplyShieldDamage(double damage)
        {
            damage = Math.Max(damage, 0);
            double shieldDamage = Math.Min(CurrentShields, damage);
            CurrentShields -= shieldDamage;
            return damage - shieldDamage;
        }

        /// <summary>
        /// Applies temporary subsystem damage from ion overflow.
        /// </summary>
        /// <param name="ionOverflowDamage">Ion damage that penetrated the shields.</param>
        /// <param name="config">The space-combat parameters.</param>
        /// <param name="random">The combat random-number stream.</param>
        private void ApplyComponentDamage(
            double ionOverflowDamage,
            GameConfig.SpaceCombatConfig config,
            IRandomNumberProvider random
        )
        {
            double interval = Math.Max(config.AutoResolveComponentDamageInterval, double.Epsilon);
            int rollCount = (int)Math.Ceiling(ionOverflowDamage / interval);
            int rollMaximum = Math.Max(config.AutoResolveComponentDamageRollMaximum, 1);
            for (int rollIndex = 0; rollIndex < rollCount; rollIndex++)
            {
                int roll = random.NextInt(1, rollMaximum + 1);
                if (roll == 1)
                    _attackDelay += GetComponentDelay(config, random);
                else if (roll == 2)
                    _movementDelay += GetComponentDelay(config, random);
                else
                    ClearArc((roll - 3) / 2);
            }
        }

        /// <summary>
        /// Returns a random temporary component-delay duration in rounds.
        /// </summary>
        /// <param name="config">The space-combat parameters.</param>
        /// <param name="random">The combat random-number stream.</param>
        /// <returns>The component delay.</returns>
        private static int GetComponentDelay(
            GameConfig.SpaceCombatConfig config,
            IRandomNumberProvider random
        )
        {
            int minimum = Math.Max(config.AutoResolveComponentDelayMinimum, 0);
            int maximum = Math.Max(config.AutoResolveComponentDelayMaximum, minimum);
            return random.NextInt(minimum, maximum + 1);
        }

        /// <summary>
        /// Clears one damaged firing arc and queues it for recharge.
        /// </summary>
        /// <param name="arc">The zero-based firing arc.</param>
        private void ClearArc(int arc)
        {
            if (arc < 0 || arc >= _currentArcCharge.Length)
                return;

            _currentArcCharge[arc] = 0;
            QueueArcForRecharge(arc);
        }

        /// <summary>
        /// Consumes the charge used by an attack and queues the arc for recharge.
        /// </summary>
        /// <param name="arc">The zero-based firing arc.</param>
        /// <param name="consumedCharge">The combined weapon strength fired from the arc.</param>
        private void DischargeArc(int arc, double consumedCharge)
        {
            _currentArcCharge[arc] = Math.Max(_currentArcCharge[arc] - consumedCharge, 0);
            QueueArcForRecharge(arc);
        }

        /// <summary>
        /// Adds a depleted arc to the recharge queue once.
        /// </summary>
        /// <param name="arc">The zero-based firing arc.</param>
        private void QueueArcForRecharge(int arc)
        {
            if (_arcQueuedForRecharge[arc] || _maximumArcCharge[arc] <= 0)
                return;

            _arcQueuedForRecharge[arc] = true;
            _rechargeQueue.Enqueue(arc);
        }

        /// <summary>
        /// Recharges shields according to current hull condition, at a fraction of the strategic
        /// rate: arcs recharge one at a time, so a ship's volleys are spread out and full-rate
        /// shields would heal completely between them.
        /// </summary>
        /// <param name="rounds">The elapsed combat rounds.</param>
        /// <param name="config">The space-combat parameters.</param>
        private void RechargeShields(double rounds, GameConfig.SpaceCombatConfig config)
        {
            if (_shieldDelay > 0)
                return;

            double condition = CurrentHull / _maximumHull;
            double rate =
                Math.Max(Ship.ShieldRechargeRate, 0)
                * Math.Max(config.TacticalShieldRechargeMultiplier, 0);
            CurrentShields = Math.Min(_maximumShields, CurrentShields + rate * condition * rounds);
        }

        /// <summary>
        /// Distributes the ship's recharge budget across depleted arcs in order.
        /// </summary>
        /// <param name="rounds">The elapsed combat rounds.</param>
        private void RechargeWeapons(double rounds)
        {
            double condition = CurrentHull / _maximumHull;
            double recharge =
                (Math.Max(Ship.WeaponRecharge, 0) + _commandBonus) * condition * rounds;
            int queuedArcCount = _rechargeQueue.Count;
            for (int arcIndex = 0; arcIndex < queuedArcCount && recharge > 0; arcIndex++)
            {
                int arc = _rechargeQueue.Peek();
                double missingCharge = _maximumArcCharge[arc] - _currentArcCharge[arc];
                double restoredCharge = Math.Min(recharge, missingCharge);
                _currentArcCharge[arc] += restoredCharge;
                recharge -= restoredCharge;
                if (_currentArcCharge[arc] < _maximumArcCharge[arc])
                    return;

                _rechargeQueue.Dequeue();
                _arcQueuedForRecharge[arc] = false;
            }
        }

        /// <summary>
        /// Returns the strongest usable primary-weapon arc at full charge.
        /// </summary>
        /// <param name="targetsFighters">Whether the opposing target is a fighter squadron.</param>
        /// <returns>The strongest arc strength.</returns>
        private double GetStrongestArcStrength(bool targetsFighters)
        {
            double strongestArc = 0;
            for (int arc = 0; arc < _maximumArcCharge.Length; arc++)
                strongestArc = Math.Max(strongestArc, GetArcStrength(arc, targetsFighters));

            return strongestArc;
        }

        /// <summary>
        /// Returns one arc's full-charge expected strength against a target type: its damage
        /// weighted by how often its weapons hit that class.
        /// </summary>
        /// <param name="arc">The zero-based firing arc.</param>
        /// <param name="targetsFighters">Whether the target is a fighter squadron.</param>
        /// <returns>The arc's effective strength.</returns>
        private double GetArcStrength(int arc, bool targetsFighters)
        {
            double strength = 0;
            foreach (PrimaryWeaponType weaponType in _weaponTypes)
            {
                strength +=
                    GetWeaponStrength(weaponType, arc, engagementDistance: 0, requireRange: true)
                    * GetTargetTypeMultiplier(weaponType, targetsFighters)
                    * GetHitChance(weaponType, targetsFighters);
            }

            return strength;
        }

        /// <summary>
        /// Returns one arc's raw weapon charge before target-type effectiveness is applied.
        /// </summary>
        /// <param name="arc">The zero-based firing arc.</param>
        /// <returns>The arc's raw weapon charge.</returns>
        private int GetRawArcStrength(int arc)
        {
            int strength = 0;
            foreach (PrimaryWeaponType weaponType in _weaponTypes)
            {
                strength += GetWeaponStrength(
                    weaponType,
                    arc,
                    engagementDistance: 0,
                    requireRange: false
                );
            }

            return strength;
        }

        /// <summary>
        /// Returns the longest range of any armed weapon type, scaled for the tactical arena.
        /// </summary>
        /// <returns>The longest weapon range, or zero when the ship is unarmed.</returns>
        private double GetMaximumWeaponRange()
        {
            double range = 0;
            foreach (PrimaryWeaponType weaponType in _weaponTypes)
            {
                int[] values = GetWeaponValuesByType(weaponType);
                if (values == null || values.Length <= CapitalShip.PrimaryWeaponRangeIndex)
                    continue;

                bool armed = false;
                for (int arc = 0; arc < CapitalShip.PrimaryWeaponArcs.Count; arc++)
                    armed |= arc < values.Length && values[arc] > 0;
                if (armed)
                    range = Math.Max(range, values[CapitalShip.PrimaryWeaponRangeIndex]);
            }

            return range * _rangeMultiplier;
        }

        /// <summary>
        /// Returns the range of the ship's main battery against one class of target: the weapon
        /// type that does the most expected damage to that class across all arcs. A ship with a
        /// few long guns and many short ones closes to where the many can fire.
        /// </summary>
        /// <param name="targetsFighters">Whether the target is a fighter squadron.</param>
        /// <returns>
        /// The main battery's range, scaled for the tactical arena, or the longest weapon range
        /// when nothing the ship carries can hurt that class.
        /// </returns>
        private double GetMainBatteryRange(bool targetsFighters)
        {
            double strongest = 0;
            double range = 0;
            foreach (PrimaryWeaponType weaponType in _weaponTypes)
            {
                int[] values = GetWeaponValuesByType(weaponType);
                if (values == null || values.Length <= CapitalShip.PrimaryWeaponRangeIndex)
                    continue;

                double strength =
                    SumArcStrength(values)
                    * GetTargetTypeMultiplier(weaponType, targetsFighters)
                    * GetHitChance(weaponType, targetsFighters);
                double weaponRange = values[CapitalShip.PrimaryWeaponRangeIndex] * _rangeMultiplier;
                if (weaponRange <= 0 || strength < strongest)
                    continue;
                if (strength > strongest || weaponRange > range)
                {
                    strongest = strength;
                    range = weaponRange;
                }
            }

            return strongest > 0 ? range : _maximumWeaponRange;
        }

        /// <summary>
        /// Returns a capital-ship weapon's effectiveness against the selected target type.
        /// </summary>
        /// <param name="type">The weapon type.</param>
        /// <param name="targetsFighters">Whether the target is a fighter squadron.</param>
        /// <returns>The target-specific damage multiplier.</returns>
        private double GetTargetTypeMultiplier(PrimaryWeaponType type, bool targetsFighters)
        {
            double multiplier = SpaceCombatStrengthCalculator.GetPrimaryWeaponTargetMultiplier(
                type,
                targetsFighters,
                _laserCannonCapitalDamageMultiplier
            );
            return multiplier;
        }

        /// <summary>
        /// Returns the chance that a capital-ship weapon hits the selected target type.
        /// </summary>
        /// <param name="type">The weapon type.</param>
        /// <param name="targetsFighters">Whether the target is a fighter squadron.</param>
        /// <returns>The hit chance, from 0 to 1.</returns>
        private double GetHitChance(PrimaryWeaponType type, bool targetsFighters)
        {
            // Heavy batteries track fighters poorly and mostly miss; laser cannons are the
            // anti-fighter weapon and always connect.
            return type == PrimaryWeaponType.Turbolaser && targetsFighters
                ? _turbolaserFighterHitChance
                : 1;
        }

        /// <summary>
        /// Returns one weapon type's non-negative strength on an arc.
        /// </summary>
        /// <param name="type">The weapon type.</param>
        /// <param name="arc">The zero-based firing arc.</param>
        /// <param name="engagementDistance">The distance to the target.</param>
        /// <param name="requireRange">Whether a positive weapon range covering the distance is required.</param>
        /// <returns>The weapon strength.</returns>
        private int GetWeaponStrength(
            PrimaryWeaponType type,
            int arc,
            double engagementDistance,
            bool requireRange
        )
        {
            int[] values = GetWeaponValuesByType(type);
            if (
                values == null
                || arc >= values.Length
                || (
                    requireRange
                    && (
                        values.Length <= CapitalShip.PrimaryWeaponRangeIndex
                        || values[CapitalShip.PrimaryWeaponRangeIndex] <= 0
                        || values[CapitalShip.PrimaryWeaponRangeIndex] * _rangeMultiplier
                            < engagementDistance
                    )
                )
            )
                return 0;

            return Math.Max(values[arc], 0);
        }

        /// <summary>
        /// Returns the cached weapon values for one weapon type.
        /// </summary>
        /// <param name="type">The weapon type.</param>
        /// <returns>The configured weapon values, or null when absent.</returns>
        private int[] GetWeaponValuesByType(PrimaryWeaponType type)
        {
            return type switch
            {
                PrimaryWeaponType.Turbolaser => _turbolasers,
                PrimaryWeaponType.LaserCannon => _laserCannons,
                PrimaryWeaponType.IonCannon => _ionCannons,
                _ => null,
            };
        }

        /// <summary>
        /// Returns the configured values for one capital-ship weapon type.
        /// </summary>
        /// <param name="ship">The capital ship providing weapon values.</param>
        /// <param name="type">The weapon type to retrieve.</param>
        /// <returns>The configured weapon values, or null when absent.</returns>
        private static int[] GetWeaponValues(CapitalShip ship, PrimaryWeaponType type)
        {
            return ship.PrimaryWeapons.TryGetValue(type, out int[] values) ? values : null;
        }

        /// <summary>
        /// Sums a weapon's strength across its four firing arcs, ignoring the range entry.
        /// </summary>
        /// <param name="values">The weapon's arc strengths followed by its range, or null.</param>
        /// <returns>The total arc strength.</returns>
        private static int SumArcStrength(int[] values)
        {
            if (values == null)
                return 0;

            int total = 0;
            for (int arc = 0; arc < Math.Min(values.Length, 4); arc++)
                total += Math.Max(values[arc], 0);
            return total;
        }
    }
}
