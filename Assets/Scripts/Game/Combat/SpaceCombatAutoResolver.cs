using System;
using System.Collections.Generic;
using System.Linq;
using Rebellion.Game.Units;
using Rebellion.Util.Common;

namespace Rebellion.Game.Combat
{
    /// <summary>
    /// Resolves a space battle without constructing or rendering the tactical scene.
    /// </summary>
    internal sealed class SpaceCombatAutoResolver
    {
        private readonly GameConfig.SpaceCombatConfig _config;
        private IRandomNumberProvider _random;

        /// <summary>
        /// Creates an automatic resolver using the supplied combat parameters.
        /// </summary>
        /// <param name="config">The automatic space-combat resolution parameters.</param>
        internal SpaceCombatAutoResolver(GameConfig.SpaceCombatConfig config)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));
        }

        /// <summary>
        /// Resolves the supplied forces until one side is destroyed, withdraws, or combat stalls.
        /// </summary>
        /// <param name="attackerShips">The attacking capital ships.</param>
        /// <param name="attackerFighters">The attacking fighter squadrons.</param>
        /// <param name="defenderShips">The defending capital ships.</param>
        /// <param name="defenderFighters">The defending fighter squadrons.</param>
        /// <param name="attackerCanWithdraw">Whether the attacking force can leave combat.</param>
        /// <param name="defenderCanWithdraw">Whether the defending force can leave combat.</param>
        /// <returns>The resolved tactical state for both forces.</returns>
        internal SpaceCombatAutoResolution Resolve(
            IReadOnlyList<CapitalShip> attackerShips,
            IReadOnlyList<Starfighter> attackerFighters,
            IReadOnlyList<CapitalShip> defenderShips,
            IReadOnlyList<Starfighter> defenderFighters,
            bool attackerCanWithdraw,
            bool defenderCanWithdraw
        )
        {
            _random = new SystemRandomProvider(_config.AutoResolveRandomSeed);
            CombatForce attacker = new CombatForce(
                attackerShips,
                attackerFighters,
                attackerCanWithdraw,
                _config.AutoResolveMinimumManeuverRatio
            );
            CombatForce defender = new CombatForce(
                defenderShips,
                defenderFighters,
                defenderCanWithdraw,
                _config.AutoResolveMinimumManeuverRatio
            );
            attacker.InitialStrength = GetTacticalStrength(attacker, defender);
            defender.InitialStrength = GetTacticalStrength(defender, attacker);

            double previousAttackerDurability = GetTacticalDurability(attacker);
            double previousDefenderDurability = GetTacticalDurability(defender);
            int stagnantIterations = 0;
            int iterationsCompleted = 0;

            for (int iteration = 0; iteration < _config.AutoResolveMaximumIterations; iteration++)
            {
                iterationsCompleted = iteration + 1;
                if (CompleteEliminatedForces(attacker, defender))
                    break;

                if (CompleteWithdrawingForce(attacker, defender))
                    break;

                Dictionary<TacticalUnit, double> pendingDamage = QueueAttacks(attacker, defender);
                AddPendingDamage(pendingDamage, QueueAttacks(defender, attacker));
                ApplyPendingDamage(pendingDamage);
                RechargeShields(attacker);
                RechargeShields(defender);

                double attackerStrength = GetTacticalStrength(attacker, defender);
                double defenderStrength = GetTacticalStrength(defender, attacker);
                double attackerDurability = GetTacticalDurability(attacker);
                double defenderDurability = GetTacticalDurability(defender);
                bool stateChanged =
                    Math.Abs(attackerDurability - previousAttackerDurability) > double.Epsilon
                    || Math.Abs(defenderDurability - previousDefenderDurability) > double.Epsilon;
                stagnantIterations = stateChanged ? 0 : stagnantIterations + 1;
                previousAttackerDurability = attackerDurability;
                previousDefenderDurability = defenderDurability;

                if (stagnantIterations >= _config.AutoResolveStagnationIterations)
                {
                    ResolveStalemate(attacker, defender, attackerStrength, defenderStrength);
                    break;
                }
            }

            if (
                attacker.Outcome == SpaceCombatAutoSideOutcome.Active
                && defender.Outcome == SpaceCombatAutoSideOutcome.Active
            )
            {
                ResolveStalemate(
                    attacker,
                    defender,
                    GetTacticalStrength(attacker, defender),
                    GetTacticalStrength(defender, attacker)
                );
            }

            return new SpaceCombatAutoResolution(
                attacker.Outcome,
                defender.Outcome,
                iterationsCompleted,
                attacker.Ships.Concat(defender.Ships).Select(CreateShipOutcome).ToList(),
                attacker.Fighters.Concat(defender.Fighters).Select(CreateFighterOutcome).ToList()
            );
        }

        /// <summary>
        /// Marks sides without surviving tactical units as destroyed.
        /// </summary>
        /// <param name="attacker">The attacking force.</param>
        /// <param name="defender">The defending force.</param>
        /// <returns>True when combat has ended.</returns>
        private static bool CompleteEliminatedForces(CombatForce attacker, CombatForce defender)
        {
            bool attackerAlive = attacker.HasSurvivors;
            bool defenderAlive = defender.HasSurvivors;
            if (attackerAlive && defenderAlive)
                return false;

            if (!attackerAlive)
                attacker.Outcome = SpaceCombatAutoSideOutcome.Destroyed;
            if (!defenderAlive)
                defender.Outcome = SpaceCombatAutoSideOutcome.Destroyed;
            return true;
        }

        /// <summary>
        /// Applies the original one-third-strength withdrawal threshold.
        /// </summary>
        /// <param name="attacker">The attacking force.</param>
        /// <param name="defender">The defending force.</param>
        /// <returns>True when either side leaves combat.</returns>
        private bool CompleteWithdrawingForce(CombatForce attacker, CombatForce defender)
        {
            bool attackerExhausted = HasReachedWithdrawalThreshold(attacker, defender);
            bool defenderExhausted = HasReachedWithdrawalThreshold(defender, attacker);
            if (!attackerExhausted && !defenderExhausted)
                return false;

            CompleteExhaustedForce(attacker, attackerExhausted);
            CompleteExhaustedForce(defender, defenderExhausted);
            return true;
        }

        /// <summary>
        /// Determines whether a force has fallen to one third of its initial tactical strength.
        /// </summary>
        /// <param name="force">The force to inspect.</param>
        /// <param name="opponent">The opposing force.</param>
        /// <returns>True when the withdrawal threshold has been reached.</returns>
        private bool HasReachedWithdrawalThreshold(CombatForce force, CombatForce opponent)
        {
            if (!force.HasSurvivors || force.InitialStrength <= 0)
                return false;

            return GetTacticalStrength(force, opponent) / force.InitialStrength
                <= _config.AutoResolveRetreatStrengthRatio;
        }

        /// <summary>
        /// Withdraws an exhausted force or destroys it when withdrawal is unavailable.
        /// </summary>
        /// <param name="force">The exhausted force.</param>
        /// <param name="isExhausted">Whether the force reached its threshold.</param>
        private static void CompleteExhaustedForce(CombatForce force, bool isExhausted)
        {
            if (!isExhausted)
                return;

            if (force.CanWithdraw)
            {
                force.Outcome = SpaceCombatAutoSideOutcome.Withdrawn;
                return;
            }

            DestroyForce(force);
        }

        /// <summary>
        /// Queues simultaneous attacks from one force against another.
        /// </summary>
        /// <param name="firingForce">The force performing attacks.</param>
        /// <param name="targetForce">The force receiving attacks.</param>
        /// <returns>Damage grouped by tactical target.</returns>
        private Dictionary<TacticalUnit, double> QueueAttacks(
            CombatForce firingForce,
            CombatForce targetForce
        )
        {
            Dictionary<TacticalUnit, double> pendingDamage = new Dictionary<TacticalUnit, double>();

            foreach (CapitalShipState ship in firingForce.Ships.Where(ship => ship.IsAlive))
            {
                TacticalUnit target = SelectCapitalShipTarget(targetForce);
                QueueAttack(pendingDamage, ship, target);
            }

            foreach (
                StarfighterState fighter in firingForce.Fighters.Where(fighter => fighter.IsAlive)
            )
            {
                TacticalUnit target = SelectStarfighterTarget(targetForce);
                QueueAttack(pendingDamage, fighter, target);
            }

            return pendingDamage;
        }

        /// <summary>
        /// Chooses a target for a capital ship.
        /// </summary>
        /// <param name="targetForce">The opposing force.</param>
        /// <returns>A surviving capital ship, or a fighter when no ship remains.</returns>
        private TacticalUnit SelectCapitalShipTarget(CombatForce targetForce)
        {
            List<CapitalShipState> ships = targetForce.Ships.Where(ship => ship.IsAlive).ToList();
            if (ships.Count > 0)
                return ships[_random.NextInt(0, ships.Count)];

            List<StarfighterState> fighters = targetForce
                .Fighters.Where(fighter => fighter.IsAlive)
                .ToList();
            return fighters.Count == 0 ? null : fighters[_random.NextInt(0, fighters.Count)];
        }

        /// <summary>
        /// Chooses a target for a fighter squadron.
        /// </summary>
        /// <param name="targetForce">The opposing force.</param>
        /// <returns>A surviving fighter, or a capital ship when no fighter remains.</returns>
        private TacticalUnit SelectStarfighterTarget(CombatForce targetForce)
        {
            List<StarfighterState> fighters = targetForce
                .Fighters.Where(fighter => fighter.IsAlive)
                .ToList();
            if (fighters.Count > 0)
                return fighters[_random.NextInt(0, fighters.Count)];

            List<CapitalShipState> ships = targetForce.Ships.Where(ship => ship.IsAlive).ToList();
            return ships.Count == 0 ? null : ships[_random.NextInt(0, ships.Count)];
        }

        /// <summary>
        /// Adds one tactical attack to the pending damage collection.
        /// </summary>
        /// <param name="pendingDamage">Damage grouped by target.</param>
        /// <param name="attacker">The attacking tactical unit.</param>
        /// <param name="target">The selected target.</param>
        private void QueueAttack(
            IDictionary<TacticalUnit, double> pendingDamage,
            TacticalUnit attacker,
            TacticalUnit target
        )
        {
            if (target == null)
                return;

            double damage = attacker.GetAttackStrength(target);
            if (damage <= 0)
                return;

            pendingDamage.TryGetValue(target, out double existingDamage);
            pendingDamage[target] = existingDamage + damage;
        }

        /// <summary>
        /// Merges pending target damage from another firing force.
        /// </summary>
        /// <param name="destination">The combined damage collection.</param>
        /// <param name="source">Damage to append.</param>
        private static void AddPendingDamage(
            IDictionary<TacticalUnit, double> destination,
            IReadOnlyDictionary<TacticalUnit, double> source
        )
        {
            foreach (KeyValuePair<TacticalUnit, double> entry in source)
            {
                destination.TryGetValue(entry.Key, out double existingDamage);
                destination[entry.Key] = existingDamage + entry.Value;
            }
        }

        /// <summary>
        /// Applies all queued damage after both forces have fired.
        /// </summary>
        /// <param name="pendingDamage">Damage grouped by tactical target.</param>
        private static void ApplyPendingDamage(
            IReadOnlyDictionary<TacticalUnit, double> pendingDamage
        )
        {
            foreach (KeyValuePair<TacticalUnit, double> entry in pendingDamage)
                entry.Key.ApplyDamage(entry.Value);
        }

        /// <summary>
        /// Recharges capital-ship shields according to current hull condition.
        /// </summary>
        /// <param name="force">The force whose shields recharge.</param>
        private static void RechargeShields(CombatForce force)
        {
            foreach (CapitalShipState ship in force.Ships.Where(ship => ship.IsAlive))
                ship.RechargeShields();
        }

        /// <summary>
        /// Calculates the target-specific strength used by the original completion checks.
        /// </summary>
        /// <param name="force">The force being measured.</param>
        /// <param name="opponent">The opposing force.</param>
        /// <returns>The force's remaining tactical strength.</returns>
        private static double GetTacticalStrength(CombatForce force, CombatForce opponent)
        {
            bool targetsFighters = opponent.Fighters.Any(fighter => fighter.IsAlive);
            return force
                    .Ships.Where(ship => ship.IsAlive)
                    .Sum(ship => ship.GetEffectiveness(targetsFighters))
                + force
                    .Fighters.Where(fighter => fighter.IsAlive)
                    .Sum(fighter => fighter.GetEffectiveness(targetsFighters));
        }

        /// <summary>
        /// Calculates the remaining hull, shields, and fighter durability used to detect progress.
        /// </summary>
        /// <param name="force">The force being measured.</param>
        /// <returns>The force's remaining tactical durability.</returns>
        private static double GetTacticalDurability(CombatForce force)
        {
            return force.Units.Sum(unit => unit.RemainingDurability);
        }

        /// <summary>
        /// Resolves forces that can no longer change the tactical state.
        /// </summary>
        /// <param name="attacker">The attacking force.</param>
        /// <param name="defender">The defending force.</param>
        /// <param name="attackerStrength">The attacker's current strength.</param>
        /// <param name="defenderStrength">The defender's current strength.</param>
        private static void ResolveStalemate(
            CombatForce attacker,
            CombatForce defender,
            double attackerStrength,
            double defenderStrength
        )
        {
            int comparison = attackerStrength.CompareTo(defenderStrength);
            if (comparison < 0)
            {
                CompleteStalematedForce(attacker);
                return;
            }

            if (comparison > 0)
            {
                CompleteStalematedForce(defender);
                return;
            }

            CompleteStalematedForce(attacker);
            CompleteStalematedForce(defender);
        }

        /// <summary>
        /// Withdraws or destroys a force selected by the stagnation resolver.
        /// </summary>
        /// <param name="force">The force to complete.</param>
        private static void CompleteStalematedForce(CombatForce force)
        {
            if (force.CanWithdraw)
                force.Outcome = SpaceCombatAutoSideOutcome.Withdrawn;
            else
                DestroyForce(force);
        }

        /// <summary>
        /// Marks every surviving unit in a force as destroyed.
        /// </summary>
        /// <param name="force">The force to destroy.</param>
        private static void DestroyForce(CombatForce force)
        {
            foreach (TacticalUnit unit in force.Units)
                unit.Destroy();
            force.Outcome = SpaceCombatAutoSideOutcome.Destroyed;
        }

        /// <summary>
        /// Creates a detached outcome for one resolved capital ship.
        /// </summary>
        /// <param name="state">The resolved tactical ship state.</param>
        /// <returns>The ship outcome.</returns>
        private static SpaceCombatAutoShipOutcome CreateShipOutcome(CapitalShipState state)
        {
            return new SpaceCombatAutoShipOutcome(state.Ship, state.InitialHull, state.CurrentHull);
        }

        /// <summary>
        /// Creates a detached outcome for one resolved fighter squadron.
        /// </summary>
        /// <param name="state">The resolved tactical fighter state.</param>
        /// <returns>The fighter outcome.</returns>
        private static SpaceCombatAutoFighterOutcome CreateFighterOutcome(StarfighterState state)
        {
            return new SpaceCombatAutoFighterOutcome(
                state.Fighter,
                state.InitialSquadronSize,
                state.CurrentSquadronSize
            );
        }

        /// <summary>
        /// Represents one side's mutable state during automatic combat.
        /// </summary>
        private sealed class CombatForce
        {
            internal readonly List<CapitalShipState> Ships;
            internal readonly List<StarfighterState> Fighters;
            internal readonly bool CanWithdraw;

            internal IEnumerable<TacticalUnit> Units => Ships.Cast<TacticalUnit>().Concat(Fighters);
            internal bool HasSurvivors => Units.Any(unit => unit.IsAlive);
            internal double InitialStrength { get; set; }
            internal SpaceCombatAutoSideOutcome Outcome { get; set; }

            /// <summary>
            /// Creates tactical state for one combat force.
            /// </summary>
            /// <param name="ships">The force's capital ships.</param>
            /// <param name="fighters">The force's fighter squadrons.</param>
            /// <param name="canWithdraw">Whether the force can leave combat.</param>
            /// <param name="minimumManeuverRatio">The minimum maneuver value and multiplier.</param>
            internal CombatForce(
                IReadOnlyList<CapitalShip> ships,
                IReadOnlyList<Starfighter> fighters,
                bool canWithdraw,
                double minimumManeuverRatio
            )
            {
                Ships = (ships ?? Array.Empty<CapitalShip>())
                    .Where(ship => ship != null)
                    .Select(ship => new CapitalShipState(ship, minimumManeuverRatio))
                    .ToList();
                Fighters = (fighters ?? Array.Empty<Starfighter>())
                    .Where(fighter => fighter != null)
                    .Select(fighter => new StarfighterState(fighter, minimumManeuverRatio))
                    .ToList();
                CanWithdraw = canWithdraw;
                Outcome = SpaceCombatAutoSideOutcome.Active;
            }
        }

        /// <summary>
        /// Provides common tactical state and target-specific attack behavior.
        /// </summary>
        private abstract class TacticalUnit
        {
            private readonly double _minimumManeuverRatio;

            protected double MinimumManeuverRatio => _minimumManeuverRatio;
            internal abstract bool IsAlive { get; }
            internal abstract double ManeuverRate { get; }
            internal abstract bool IsStarfighter { get; }
            internal abstract double RemainingDurability { get; }

            /// <summary>Calculates this unit's attack strength against a target.</summary>
            /// <param name="target">The target unit.</param>
            /// <returns>The attack strength.</returns>
            internal abstract double GetAttackStrength(TacticalUnit target);

            /// <summary>Calculates this unit's remaining tactical strength.</summary>
            /// <param name="targetsFighters">Whether fighter defenses constrain its weapons.</param>
            /// <returns>The remaining tactical strength.</returns>
            internal abstract double GetEffectiveness(bool targetsFighters);

            /// <summary>Applies simultaneous tactical damage.</summary>
            /// <param name="damage">The non-negative damage to apply.</param>
            internal abstract void ApplyDamage(double damage);

            /// <summary>Destroys the tactical unit.</summary>
            internal abstract void Destroy();

            /// <summary>
            /// Creates tactical state using the configured maneuver floor.
            /// </summary>
            /// <param name="minimumManeuverRatio">The minimum maneuver value and multiplier.</param>
            protected TacticalUnit(double minimumManeuverRatio)
            {
                _minimumManeuverRatio = minimumManeuverRatio;
            }

            /// <summary>
            /// Calculates the original maneuver-rate adjustment used against fighter targets.
            /// </summary>
            /// <param name="target">The target unit.</param>
            /// <returns>The attack multiplier.</returns>
            protected double GetManeuverMultiplier(TacticalUnit target)
            {
                if (!target.IsStarfighter)
                    return 1;

                double targetRate = Math.Max(target.ManeuverRate, _minimumManeuverRatio);
                return Math.Max(Math.Min(ManeuverRate / targetRate, 1), _minimumManeuverRatio);
            }
        }

        /// <summary>
        /// Stores a capital ship's tactical hull, shield, and attack state.
        /// </summary>
        private sealed class CapitalShipState : TacticalUnit
        {
            internal readonly CapitalShip Ship;
            internal readonly int InitialHull;
            private readonly double _maximumHull;
            private readonly double _maximumShields;

            internal double CurrentHull { get; private set; }
            internal double CurrentShields { get; private set; }
            internal override bool IsAlive => CurrentHull > 0;
            internal override bool IsStarfighter => false;
            internal override double RemainingDurability => CurrentHull + CurrentShields;
            internal override double ManeuverRate =>
                Math.Max(Ship.SublightSpeed + Ship.Maneuverability, MinimumManeuverRatio);

            /// <summary>
            /// Creates tactical state from a capital ship's current strategic state.
            /// </summary>
            /// <param name="ship">The capital ship entering combat.</param>
            /// <param name="minimumManeuverRatio">The minimum maneuver value and multiplier.</param>
            internal CapitalShipState(CapitalShip ship, double minimumManeuverRatio)
                : base(minimumManeuverRatio)
            {
                Ship = ship;
                InitialHull = Math.Max(ship.CurrentHullStrength, 0);
                _maximumHull = Math.Max(ship.MaxHullStrength, 1);
                _maximumShields = Math.Max(ship.MaxShieldStrength, 0);
                CurrentHull = InitialHull;
                CurrentShields = _maximumShields;
            }

            /// <inheritdoc />
            internal override double GetAttackStrength(TacticalUnit target)
            {
                double condition = CurrentHull / _maximumHull;
                double recharge = Math.Max(Ship.WeaponRecharge, 0);
                return GetStrongestArcStrength(target.IsStarfighter)
                    * GetManeuverMultiplier(target)
                    * condition
                    * recharge;
            }

            /// <inheritdoc />
            internal override double GetEffectiveness(bool targetsFighters)
            {
                double condition = CurrentHull / _maximumHull;
                return GetStrongestArcStrength(targetsFighters) * condition;
            }

            /// <inheritdoc />
            internal override void ApplyDamage(double damage)
            {
                double shieldDamage = Math.Min(CurrentShields, Math.Max(damage, 0));
                CurrentShields -= shieldDamage;
                CurrentHull = Math.Max(CurrentHull - (damage - shieldDamage), 0);
            }

            /// <inheritdoc />
            internal override void Destroy()
            {
                CurrentHull = 0;
                CurrentShields = 0;
            }

            /// <summary>Recharges shields according to current hull condition.</summary>
            internal void RechargeShields()
            {
                double condition = CurrentHull / _maximumHull;
                CurrentShields = Math.Min(
                    _maximumShields,
                    CurrentShields + Math.Max(Ship.ShieldRechargeRate, 0) * condition
                );
            }

            /// <summary>Returns the strongest usable primary-weapon arc.</summary>
            /// <param name="targetsFighters">Whether the target is a fighter squadron.</param>
            /// <returns>The strongest arc strength.</returns>
            private double GetStrongestArcStrength(bool targetsFighters)
            {
                double strongestArc = 0;
                for (int arc = 0; arc < 4; arc++)
                {
                    double arcStrength =
                        GetWeaponStrength(PrimaryWeaponType.Turbolaser, arc)
                        + GetWeaponStrength(PrimaryWeaponType.LaserCannon, arc);
                    if (!targetsFighters)
                        arcStrength += GetWeaponStrength(PrimaryWeaponType.IonCannon, arc);
                    strongestArc = Math.Max(strongestArc, arcStrength);
                }

                return strongestArc;
            }

            /// <summary>Returns one weapon type's non-negative strength on an arc.</summary>
            /// <param name="type">The weapon type.</param>
            /// <param name="arc">The zero-based firing arc.</param>
            /// <returns>The weapon strength.</returns>
            private int GetWeaponStrength(PrimaryWeaponType type, int arc)
            {
                if (
                    !Ship.PrimaryWeapons.TryGetValue(type, out int[] values)
                    || values == null
                    || arc >= values.Length
                )
                    return 0;

                return Math.Max(values[arc], 0);
            }
        }

        /// <summary>
        /// Stores a fighter squadron's tactical durability and attack state.
        /// </summary>
        private sealed class StarfighterState : TacticalUnit
        {
            internal readonly Starfighter Fighter;
            internal readonly int InitialSquadronSize;
            private readonly double _durabilityPerFighter;
            private double _currentDurability;

            internal int CurrentSquadronSize =>
                IsAlive
                    ? Math.Min(
                        InitialSquadronSize,
                        (int)Math.Ceiling(_currentDurability / _durabilityPerFighter)
                    )
                    : 0;
            internal override bool IsAlive => _currentDurability > 0;
            internal override bool IsStarfighter => true;
            internal override double RemainingDurability => _currentDurability;
            internal override double ManeuverRate =>
                Math.Max(Fighter.SublightSpeed + Fighter.Agility, MinimumManeuverRatio);

            /// <summary>
            /// Creates tactical state from a fighter squadron's current strategic state.
            /// </summary>
            /// <param name="fighter">The fighter squadron entering combat.</param>
            /// <param name="minimumManeuverRatio">The minimum maneuver value and multiplier.</param>
            internal StarfighterState(Starfighter fighter, double minimumManeuverRatio)
                : base(minimumManeuverRatio)
            {
                Fighter = fighter;
                InitialSquadronSize = Math.Max(fighter.CurrentSquadronSize, 0);
                _durabilityPerFighter = Math.Max(fighter.ShieldStrength, 1);
                _currentDurability = InitialSquadronSize * _durabilityPerFighter;
            }

            /// <inheritdoc />
            internal override double GetAttackStrength(TacticalUnit target)
            {
                return GetWeaponStrength(target.IsStarfighter)
                    * GetManeuverMultiplier(target)
                    * GetRemainingFighterCount();
            }

            /// <inheritdoc />
            internal override double GetEffectiveness(bool targetsFighters)
            {
                return GetWeaponStrength(targetsFighters) * GetRemainingFighterCount();
            }

            /// <inheritdoc />
            internal override void ApplyDamage(double damage)
            {
                _currentDurability = Math.Max(_currentDurability - Math.Max(damage, 0), 0);
            }

            /// <inheritdoc />
            internal override void Destroy()
            {
                _currentDurability = 0;
            }

            /// <summary>Returns the durability-adjusted number of surviving fighters.</summary>
            /// <returns>The surviving fighter count.</returns>
            private double GetRemainingFighterCount()
            {
                return _currentDurability / _durabilityPerFighter;
            }

            /// <summary>Returns one fighter's usable weapon strength against a target type.</summary>
            /// <param name="targetsFighters">Whether the target is a fighter squadron.</param>
            /// <returns>The fighter's usable weapon strength.</returns>
            private int GetWeaponStrength(bool targetsFighters)
            {
                int strength = Math.Max(Fighter.LaserCannon, 0) + Math.Max(Fighter.Torpedoes, 0);
                if (!targetsFighters)
                    strength += Math.Max(Fighter.IonCannon, 0);
                return strength;
            }
        }
    }
}
