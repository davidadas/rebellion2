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
    /// Stores a fighter squadron's tactical durability, weapon-charge, and target state.
    /// </summary>
    public sealed class TacticalStarfighterUnit : TacticalBattleUnit
    {
        private const double _squadronRadius = 1.4;
        private const double _engagementRangeFraction = 0.8;
        private const double _targetDistanceScale = 20;
        private const int _laserLane = 0;
        private const int _torpedoLane = 1;
        private const int _ionLane = 2;
        private const int _laneCount = 3;

        private readonly double _durabilityPerFighter;
        private readonly double _laserCannonCapitalDamageMultiplier;
        private readonly double _commandBonus;
        private readonly double _maximumWeaponCharge;
        private double _currentDurability;
        private double _currentWeaponCharge;
        private double _scanCooldown;

        public Starfighter Fighter { get; }

        public CapitalShip Carrier { get; }

        public int InitialSquadronSize { get; }

        public TacticalBattleUnit Target { get; private set; }

        /// <summary>
        /// Gets or sets the direction the squadron circles its target: 1 counterclockwise,
        /// -1 clockwise. Alternating directions across a force keeps squadrons from stacking.
        /// </summary>
        internal int OrbitDirection { get; set; } = 1;

        /// <summary>
        /// Gets or sets how far out this squadron circles, as a multiple of the usual orbit
        /// clearance. A different value per squadron keeps them from stacking on one ring.
        /// </summary>
        internal double OrbitRadiusScale { get; set; } = 1;

        public int CurrentSquadronSize =>
            IsAlive
                ? Math.Min(
                    InitialSquadronSize,
                    (int)Math.Ceiling(_currentDurability / _durabilityPerFighter)
                )
                : 0;

        public double WeaponChargeFraction =>
            _maximumWeaponCharge > 0 ? _currentWeaponCharge / _maximumWeaponCharge : 0;

        public bool IsArmed => _maximumWeaponCharge > 0;

        public override ISceneNode Node => Fighter;

        public override bool IsAlive => _currentDurability > 0;

        public override bool IsStarfighter => true;

        public override double Radius => _squadronRadius;

        internal override bool CanWithdrawIndependently => Fighter.Hyperdrive > 0;

        internal override double RemainingDurability => _currentDurability;

        internal override double Speed => Math.Max(Fighter.SublightSpeed, 0);

        internal override double ManeuverRate =>
            Math.Max(Fighter.SublightSpeed + Fighter.Agility + _commandBonus, MinimumManeuverRatio);

        internal bool IsScanDue => _scanCooldown <= 0;

        /// <summary>
        /// Creates tactical state from a fighter squadron's current strategic state.
        /// </summary>
        /// <param name="fighter">The fighter squadron entering combat.</param>
        /// <param name="side">The side the squadron fights for.</param>
        /// <param name="config">The space-combat parameters.</param>
        /// <param name="commandBonus">The Commander's Combat-derived tactical bonus.</param>
        internal TacticalStarfighterUnit(
            Starfighter fighter,
            CombatSide side,
            GameConfig.SpaceCombatConfig config,
            double commandBonus
        )
            : base(side, config.AutoResolveMinimumManeuverRatio)
        {
            Fighter = fighter ?? throw new ArgumentNullException(nameof(fighter));
            Carrier = fighter.GetParent() as CapitalShip;
            InitialSquadronSize = Math.Max(fighter.CurrentSquadronSize, 0);
            _commandBonus = Math.Max(commandBonus, 0);
            // Every fighter has an airframe to shoot through and shields add to it, so TIEs fall
            // sooner than X-wings instead of popping at the first hit.
            _durabilityPerFighter = Math.Max(
                Math.Max(config.TacticalFighterHullStrength, 0)
                    + Math.Max(fighter.ShieldStrength, 0),
                1
            );
            _laserCannonCapitalDamageMultiplier = Math.Max(
                config.LaserCannonCapitalDamageMultiplier,
                0
            );
            _currentDurability = InitialSquadronSize * _durabilityPerFighter;
            _maximumWeaponCharge =
                Math.Max(fighter.LaserCannon, 0)
                + Math.Max(fighter.IonCannon, 0)
                + Math.Max(fighter.Torpedoes, 0);
            _currentWeaponCharge = _maximumWeaponCharge;
            // Squadrons dogfight first and turn on capital ships once the sky is clear.
            TargetPriority = TacticalTargetPriority.Starfighters;
        }

        /// <summary>
        /// Records an automatically selected target and restarts the scan cooldown.
        /// </summary>
        /// <param name="target">The selected target, or null when nothing can be engaged.</param>
        /// <param name="scanInterval">The rounds until the next automatic scan.</param>
        internal void SetTarget(TacticalBattleUnit target, double scanInterval)
        {
            Target = target;
            _scanCooldown = scanInterval;
        }

        /// <summary>
        /// Makes the squadron pick a new target on the next step.
        /// </summary>
        internal void RequestScan()
        {
            _scanCooldown = 0;
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
        /// Scores how attractive an opposing unit is as a target.
        /// </summary>
        /// <param name="candidate">The opposing unit.</param>
        /// <returns>The target score, where higher is better.</returns>
        internal double ScoreTarget(TacticalBattleUnit candidate)
        {
            double potential =
                GetCombinedWeaponStrength(
                    candidate.IsStarfighter,
                    engagementDistance: 0,
                    requireRange: false
                )
                * GetManeuverMultiplier(candidate)
                * GetRemainingSquadronStrength();
            return potential / (1 + GetDistanceTo(candidate) / _targetDistanceScale);
        }

        /// <summary>
        /// Returns the distance the squadron closes to before holding against a target type.
        /// </summary>
        /// <param name="targetsFighters">Whether the target is a fighter squadron.</param>
        /// <returns>The engagement distance, or zero when no weapon can engage the target.</returns>
        internal double GetEngagementRange(bool targetsFighters)
        {
            int shortestRange = 0;
            for (int lane = 0; lane < _laneCount; lane++)
            {
                if ((lane == _ionLane || lane == _torpedoLane) && targetsFighters)
                    continue;

                int strength = GetWeaponStrength(lane, engagementDistance: 0, requireRange: true);
                if (strength <= 0)
                    continue;

                int range = GetWeaponRange(lane);
                shortestRange = shortestRange <= 0 ? range : Math.Min(shortestRange, range);
            }

            return shortestRange * _engagementRangeFraction;
        }

        /// <summary>
        /// Calculates the fighter squadron's remaining tactical strength.
        /// </summary>
        /// <param name="targetsFighters">Whether the opposing target is a fighter squadron.</param>
        /// <returns>The remaining tactical strength.</returns>
        internal override double GetEffectiveness(bool targetsFighters)
        {
            return GetCombinedWeaponStrength(
                    targetsFighters,
                    engagementDistance: 0,
                    requireRange: true
                ) * GetRemainingSquadronStrength();
        }

        /// <summary>
        /// Queues one volley from every weapon lane once the squadron is fully charged.
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
            if (_maximumWeaponCharge <= 0 || _currentWeaponCharge < _maximumWeaponCharge)
                return;

            double squadronStrength = GetRemainingSquadronStrength();
            double consumedCharge = 0;
            for (int lane = 0; lane < _laneCount; lane++)
            {
                TacticalBattleUnit target = SelectLaneTarget(
                    lane,
                    targets,
                    squadronStrength,
                    out int weaponStrength,
                    out double damage
                );
                if (target == null)
                    continue;

                QueueHit(
                    hitSink,
                    shots,
                    target,
                    damage,
                    lane == _ionLane,
                    shotDisplaySeconds,
                    isTorpedo: lane == _torpedoLane
                );
                consumedCharge += weaponStrength;
            }

            if (consumedCharge > 0)
                _currentWeaponCharge = Math.Max(_currentWeaponCharge - consumedCharge, 0);
        }

        /// <summary>
        /// Applies simultaneous conventional and ion damage to the fighter squadron.
        /// </summary>
        /// <param name="conventionalDamage">The non-negative conventional attack strength.</param>
        /// <param name="ionDamage">The non-negative ion attack strength.</param>
        /// <param name="config">The space-combat parameters.</param>
        /// <param name="random">The combat random-number stream.</param>
        internal override void ApplyDamage(
            double conventionalDamage,
            double ionDamage,
            GameConfig.SpaceCombatConfig config,
            IRandomNumberProvider random
        )
        {
            _currentDurability = Math.Max(_currentDurability - Math.Max(conventionalDamage, 0), 0);
        }

        /// <summary>
        /// Advances fighter weapon recharge state.
        /// </summary>
        /// <param name="rounds">The elapsed combat rounds.</param>
        /// <param name="config">The space-combat parameters.</param>
        internal override void AdvanceState(double rounds, GameConfig.SpaceCombatConfig config)
        {
            double recharge =
                GetRemainingSquadronStrength()
                * Math.Max(config.AutoResolveFighterWeaponRechargeMultiplier, 0)
                * Math.Max(config.TacticalFighterRechargeMultiplier, 0)
                * rounds;
            _currentWeaponCharge = Math.Min(_maximumWeaponCharge, _currentWeaponCharge + recharge);
        }

        /// <summary>
        /// Destroys the fighter squadron by removing its remaining durability.
        /// </summary>
        internal override void Destroy()
        {
            _currentDurability = 0;
        }

        /// <summary>
        /// Selects the target one weapon lane fires at.
        /// </summary>
        /// <param name="lane">The zero-based weapon lane.</param>
        /// <param name="targets">The targetable opposing units.</param>
        /// <param name="squadronStrength">The surviving fraction of the full squadron.</param>
        /// <param name="weaponStrength">Receives the raw weapon strength consumed by the shot.</param>
        /// <param name="damage">Receives the damage dealt by the shot.</param>
        /// <returns>The selected target, or null when nothing is in range.</returns>
        private TacticalBattleUnit SelectLaneTarget(
            int lane,
            IReadOnlyList<TacticalBattleUnit> targets,
            double squadronStrength,
            out int weaponStrength,
            out double damage
        )
        {
            if (
                Target?.IsTargetable == true
                && TryEvaluateLane(lane, Target, squadronStrength, out weaponStrength, out damage)
            )
                return Target;

            TacticalBattleUnit selected = null;
            weaponStrength = 0;
            damage = 0;
            foreach (TacticalBattleUnit candidate in targets)
            {
                if (
                    !TryEvaluateLane(
                        lane,
                        candidate,
                        squadronStrength,
                        out int candidateStrength,
                        out double candidateDamage
                    )
                    || candidateDamage <= damage
                )
                    continue;

                selected = candidate;
                weaponStrength = candidateStrength;
                damage = candidateDamage;
            }

            return selected;
        }

        /// <summary>
        /// Evaluates whether one weapon lane can hit a candidate.
        /// </summary>
        /// <param name="lane">The zero-based weapon lane.</param>
        /// <param name="candidate">The opposing unit.</param>
        /// <param name="squadronStrength">The surviving fraction of the full squadron.</param>
        /// <param name="weaponStrength">Receives the raw weapon strength.</param>
        /// <param name="damage">Receives the damage dealt.</param>
        /// <returns>True when the lane can damage the candidate at its current distance.</returns>
        private bool TryEvaluateLane(
            int lane,
            TacticalBattleUnit candidate,
            double squadronStrength,
            out int weaponStrength,
            out double damage
        )
        {
            weaponStrength = 0;
            damage = 0;
            if (lane == _ionLane && candidate.IsStarfighter)
                return false;
            if (lane == _torpedoLane && !CanTorpedo(candidate))
                return false;

            weaponStrength = GetWeaponStrength(
                lane,
                GetEngagementDistanceTo(candidate),
                requireRange: true
            );
            if (weaponStrength <= 0)
                return false;

            damage =
                GetEffectiveWeaponStrength(weaponStrength, lane, candidate.IsStarfighter)
                * GetManeuverMultiplier(candidate)
                * squadronStrength;
            return damage > 0;
        }

        /// <summary>
        /// Returns whether torpedoes can be fired at a target: only a capital ship whose shields
        /// are down, since torpedoes cannot punch through shields and are wasted on fighters.
        /// </summary>
        /// <param name="candidate">The opposing unit.</param>
        /// <returns>True when the torpedo lane can engage the candidate.</returns>
        private static bool CanTorpedo(TacticalBattleUnit candidate)
        {
            return candidate is TacticalCapitalShipUnit ship && ship.ShieldsDown;
        }

        /// <summary>
        /// Returns the durability-adjusted fraction of the full squadron that survives.
        /// </summary>
        /// <returns>The surviving fraction of the full squadron.</returns>
        private double GetRemainingSquadronStrength()
        {
            double maximumSquadronSize = Math.Max(Fighter.MaxSquadronSize, 1);
            return Math.Min(_currentDurability / (_durabilityPerFighter * maximumSquadronSize), 1);
        }

        /// <summary>
        /// Returns the squadron's usable weapon strength against a target type.
        /// </summary>
        /// <param name="targetsFighters">Whether the target is a fighter squadron.</param>
        /// <param name="engagementDistance">The distance to the target.</param>
        /// <param name="requireRange">Whether a positive range covering the distance is required.</param>
        /// <returns>The fighter's usable weapon strength.</returns>
        private double GetCombinedWeaponStrength(
            bool targetsFighters,
            double engagementDistance,
            bool requireRange
        )
        {
            double strength = GetEffectiveWeaponStrength(
                GetWeaponStrength(_laserLane, engagementDistance, requireRange),
                _laserLane,
                targetsFighters
            );
            if (!targetsFighters)
            {
                // Torpedoes and ion cannons are capital-ship weapons.
                strength +=
                    GetEffectiveWeaponStrength(
                        GetWeaponStrength(_torpedoLane, engagementDistance, requireRange),
                        _torpedoLane,
                        targetsFighters
                    )
                    + GetEffectiveWeaponStrength(
                        GetWeaponStrength(_ionLane, engagementDistance, requireRange),
                        _ionLane,
                        targetsFighters
                    );
            }

            return strength;
        }

        /// <summary>
        /// Applies command and target-type damage modifiers to a usable fighter weapon lane.
        /// </summary>
        /// <param name="weaponStrength">The usable raw weapon strength.</param>
        /// <param name="lane">The zero-based weapon lane.</param>
        /// <param name="targetsFighters">Whether the target is a fighter squadron.</param>
        /// <returns>The command- and target-adjusted weapon strength.</returns>
        private double GetEffectiveWeaponStrength(
            int weaponStrength,
            int lane,
            bool targetsFighters
        )
        {
            double multiplier =
                lane == _laserLane && !targetsFighters ? _laserCannonCapitalDamageMultiplier : 1;
            return weaponStrength > 0 ? (weaponStrength + _commandBonus) * multiplier : 0;
        }

        /// <summary>
        /// Returns one squadron weapon lane's usable strength.
        /// </summary>
        /// <param name="lane">The zero-based weapon lane.</param>
        /// <param name="engagementDistance">The distance to the target.</param>
        /// <param name="requireRange">Whether a positive range covering the distance is required.</param>
        /// <returns>The usable weapon strength.</returns>
        private int GetWeaponStrength(int lane, double engagementDistance, bool requireRange)
        {
            int strength = lane switch
            {
                _laserLane => Fighter.LaserCannon,
                _torpedoLane => Fighter.Torpedoes,
                _ionLane => Fighter.IonCannon,
                _ => 0,
            };
            int range = GetWeaponRange(lane);
            return requireRange && (range <= 0 || range < engagementDistance)
                ? 0
                : Math.Max(strength, 0);
        }

        /// <summary>
        /// Returns one squadron weapon lane's configured range.
        /// </summary>
        /// <param name="lane">The zero-based weapon lane.</param>
        /// <returns>The configured range.</returns>
        private int GetWeaponRange(int lane)
        {
            return lane switch
            {
                _laserLane => Fighter.LaserRange,
                _torpedoLane => Fighter.TorpedoRange,
                _ionLane => Fighter.IonRange,
                _ => 0,
            };
        }
    }
}
