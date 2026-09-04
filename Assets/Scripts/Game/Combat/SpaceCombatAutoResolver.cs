using System;
using System.Collections.Generic;
using System.Linq;
using Rebellion.Game.Results;
using Rebellion.Game.Units;
using Rebellion.SceneGraph;
using Rebellion.Util.Common;

namespace Rebellion.Game.Combat
{
    /// <summary>
    /// Resolves a space battle without constructing or rendering the tactical scene.
    /// </summary>
    public sealed class SpaceCombatAutoResolver
    {
        private readonly GameConfig.SpaceCombatConfig _config;
        private readonly IRandomNumberProvider _random;

        /// <summary>
        /// Creates an automatic resolver using the supplied combat parameters.
        /// </summary>
        /// <param name="config">The automatic space-combat resolution parameters.</param>
        /// <param name="random">The persisted game random-number stream.</param>
        public SpaceCombatAutoResolver(
            GameConfig.SpaceCombatConfig config,
            IRandomNumberProvider random
        )
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _random = random ?? throw new ArgumentNullException(nameof(random));
        }

        /// <summary>
        /// Resolves the supplied forces until one side is destroyed, withdraws, or combat stalls.
        /// </summary>
        /// <param name="attackerShips">The attacking capital ships.</param>
        /// <param name="attackerFighters">The attacking fighter squadrons.</param>
        /// <param name="defenderShips">The defending capital ships.</param>
        /// <param name="defenderFighters">The defending fighter squadrons.</param>
        /// <param name="attackerWithdrawalGroups">The attacking unit groups capable of retreat.</param>
        /// <param name="defenderWithdrawalGroups">The defending unit groups capable of retreat.</param>
        /// <returns>The resolved tactical state for both forces.</returns>
        public SpaceCombatAutoResult Resolve(
            IReadOnlyList<CapitalShip> attackerShips,
            IReadOnlyList<Starfighter> attackerFighters,
            IReadOnlyList<CapitalShip> defenderShips,
            IReadOnlyList<Starfighter> defenderFighters,
            IReadOnlyList<IReadOnlyCollection<ISceneNode>> attackerWithdrawalGroups,
            IReadOnlyList<IReadOnlyCollection<ISceneNode>> defenderWithdrawalGroups
        )
        {
            CombatForce attacker = new CombatForce(
                attackerShips,
                attackerFighters,
                attackerWithdrawalGroups,
                _config
            );
            CombatForce defender = new CombatForce(
                defenderShips,
                defenderFighters,
                defenderWithdrawalGroups,
                _config
            );
            attacker.InitialStrength = GetTacticalStrength(attacker, defender);
            defender.InitialStrength = GetTacticalStrength(defender, attacker);

            double previousAttackerDurability = GetTacticalDurability(attacker);
            double previousDefenderDurability = GetTacticalDurability(defender);
            int stagnantIterations = 0;
            int iterationsCompleted = 0;
            Dictionary<TacticalUnit, PendingDamage> pendingDamage =
                new Dictionary<TacticalUnit, PendingDamage>();
            for (int iteration = 0; iteration < _config.AutoResolveMaximumIterations; iteration++)
            {
                iterationsCompleted = iteration + 1;
                if (CompleteEliminatedForces(attacker, defender))
                    break;

                WithdrawUnitsAtThreshold(attacker, defender);
                WithdrawUnitsAtThreshold(defender, attacker);

                pendingDamage.Clear();
                QueueAttacks(attacker, defender, pendingDamage);
                QueueAttacks(defender, attacker, pendingDamage);
                ApplyPendingDamage(pendingDamage);
                AdvanceTacticalState(attacker);
                AdvanceTacticalState(defender);

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
                attacker.Outcome == SpaceCombatSideOutcome.Active
                && defender.Outcome == SpaceCombatSideOutcome.Active
            )
            {
                ResolveStalemate(
                    attacker,
                    defender,
                    GetTacticalStrength(attacker, defender),
                    GetTacticalStrength(defender, attacker)
                );
            }

            return new SpaceCombatAutoResult(
                attacker.Outcome,
                defender.Outcome,
                iterationsCompleted,
                attacker.Ships.Concat(defender.Ships).Select(CreateShipOutcome).ToList(),
                attacker.Fighters.Concat(defender.Fighters).Select(CreateFighterOutcome).ToList()
            );
        }

        /// <summary>
        /// Completes sides without units remaining in combat.
        /// </summary>
        /// <param name="attacker">The attacking force.</param>
        /// <param name="defender">The defending force.</param>
        /// <returns>True when combat has ended.</returns>
        private static bool CompleteEliminatedForces(CombatForce attacker, CombatForce defender)
        {
            bool attackerActive = attacker.HasCombatants;
            bool defenderActive = defender.HasCombatants;
            if (attackerActive && defenderActive)
                return false;

            if (!attackerActive)
                CompleteForce(attacker);
            if (!defenderActive)
                CompleteForce(defender);
            return true;
        }

        /// <summary>
        /// Begins per-unit withdrawal after a force falls below the original strength threshold.
        /// </summary>
        /// <param name="force">The force whose withdrawal state is evaluated.</param>
        /// <param name="opposingForce">The force providing the available target types.</param>
        private void WithdrawUnitsAtThreshold(CombatForce force, CombatForce opposingForce)
        {
            if (force.WithdrawalOrdered || !HasReachedWithdrawalThreshold(force, opposingForce))
                return;

            force.WithdrawalOrdered = true;
            foreach (TacticalUnit unit in force.Units.Where(unit => unit.CanWithdraw))
                unit.BeginWithdrawal();
        }

        /// <summary>
        /// Determines whether a force has fallen below one third of its initial tactical strength.
        /// </summary>
        /// <param name="force">The force to inspect.</param>
        /// <param name="opposingForce">The force providing the available target types.</param>
        /// <returns>True when the withdrawal threshold has been reached.</returns>
        private bool HasReachedWithdrawalThreshold(CombatForce force, CombatForce opposingForce)
        {
            if (!force.HasCombatants || force.InitialStrength <= 0)
                return false;

            return GetTacticalStrength(force, opposingForce) / force.InitialStrength
                < _config.AutoResolveRetreatStrengthRatio;
        }

        /// <summary>
        /// Assigns the completed state of a force with no remaining combatants.
        /// </summary>
        /// <param name="force">The completed force.</param>
        private static void CompleteForce(CombatForce force)
        {
            force.Outcome = force.HasWithdrawnUnits
                ? SpaceCombatSideOutcome.Withdrawn
                : SpaceCombatSideOutcome.Destroyed;
        }

        /// <summary>
        /// Queues simultaneous attacks from one force against another.
        /// </summary>
        /// <param name="firingForce">The force performing attacks.</param>
        /// <param name="targetForce">The force receiving attacks.</param>
        /// <param name="pendingDamage">Damage grouped by tactical target.</param>
        private void QueueAttacks(
            CombatForce firingForce,
            CombatForce targetForce,
            IDictionary<TacticalUnit, PendingDamage> pendingDamage
        )
        {
            IReadOnlyList<TacticalUnit> targets = targetForce.GetTargetableUnits();
            int scanDivisor = Math.Max(_config.AutoResolveTargetScanDivisor, 1);

            foreach (TacticalUnit attacker in firingForce.Units)
            {
                if (!attacker.CanFire)
                    continue;

                bool scansForTarget =
                    attacker.CanScanForTargets && _random.NextInt(0, scanDivisor) == 0;
                attacker.QueueAvailableAttacks(
                    targets,
                    scansForTarget,
                    _config.AutoResolveStartingDistance,
                    pendingDamage
                );
            }
        }

        /// <summary>
        /// Applies all queued damage after both forces have fired.
        /// </summary>
        /// <param name="pendingDamage">Damage grouped by tactical target.</param>
        private void ApplyPendingDamage(
            IReadOnlyDictionary<TacticalUnit, PendingDamage> pendingDamage
        )
        {
            foreach (KeyValuePair<TacticalUnit, PendingDamage> entry in pendingDamage)
            {
                foreach (PendingHit hit in entry.Value.Hits)
                {
                    entry.Key.ApplyDamage(
                        hit.IsIonDamage ? 0 : hit.Damage,
                        hit.IsIonDamage ? hit.Damage : 0,
                        _config,
                        _random
                    );
                }
            }
        }

        /// <summary>
        /// Keeps queued hits in firing order until both sides have finished firing.
        /// </summary>
        private sealed class PendingDamage
        {
            private readonly List<PendingHit> _hits = new List<PendingHit>();

            internal IReadOnlyList<PendingHit> Hits => _hits;

            /// <summary>
            /// Adds one weapon lane's damage to the appropriate channel.
            /// </summary>
            /// <param name="damage">The positive damage to add.</param>
            /// <param name="isIonDamage">Whether the damage comes from ion weapons.</param>
            internal void Add(double damage, bool isIonDamage)
            {
                _hits.Add(new PendingHit(damage, isIonDamage));
            }
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

        /// <summary>
        /// Advances temporary damage timers and capital-ship shield and weapon recharge.
        /// </summary>
        /// <param name="force">The force whose tactical state advances.</param>
        private void AdvanceTacticalState(CombatForce force)
        {
            foreach (TacticalUnit unit in force.Units)
            {
                if (!unit.IsTargetable)
                    continue;

                unit.AdvanceTacticalState(_config, _random);
            }
        }

        /// <summary>
        /// Calculates the remaining strength used by the original completion checks.
        /// </summary>
        /// <param name="force">The force being measured.</param>
        /// <param name="opposingForce">The force providing the available target types.</param>
        /// <returns>The force's remaining tactical strength.</returns>
        private static double GetTacticalStrength(CombatForce force, CombatForce opposingForce)
        {
            bool canTargetCapitalShips = opposingForce.HasTargetableShips;
            bool canTargetFighters = opposingForce.HasTargetableFighters;
            double strength = 0;
            foreach (TacticalUnit unit in force.Units)
            {
                if (!unit.IsTargetable)
                    continue;

                strength += Math.Max(
                    canTargetCapitalShips ? unit.GetEffectiveness(targetsFighters: false) : 0,
                    canTargetFighters ? unit.GetEffectiveness(targetsFighters: true) : 0
                );
            }
            return strength;
        }

        /// <summary>
        /// Calculates the remaining hull, shields, and fighter durability used to detect progress.
        /// </summary>
        /// <param name="force">The force being measured.</param>
        /// <returns>The force's remaining tactical durability.</returns>
        private static double GetTacticalDurability(CombatForce force)
        {
            double durability = 0;
            foreach (TacticalUnit unit in force.Units)
            {
                if (unit.IsTargetable)
                    durability += unit.RemainingDurability;
            }
            return durability;
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
            foreach (TacticalUnit unit in force.Units.Where(unit => unit.IsTargetable).ToList())
            {
                if (unit.CanWithdraw)
                    unit.CompleteWithdrawal();
            }

            foreach (TacticalUnit unit in force.Units.Where(unit => unit.IsTargetable))
                unit.Destroy();

            if (force.HasWithdrawnUnits)
                force.Outcome = SpaceCombatSideOutcome.Withdrawn;
            else
                force.Outcome = SpaceCombatSideOutcome.Destroyed;
        }

        /// <summary>
        /// Creates a detached outcome for one resolved capital ship.
        /// </summary>
        /// <param name="state">The resolved tactical ship state.</param>
        /// <returns>The ship outcome.</returns>
        private static SpaceCombatAutoShipOutcome CreateShipOutcome(CapitalShipState state)
        {
            return new SpaceCombatAutoShipOutcome(
                state.Ship,
                state.InitialHull,
                state.CurrentHull,
                state.HasWithdrawn
            );
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
                state.CurrentSquadronSize,
                state.HasWithdrawn
            );
        }

        /// <summary>
        /// Represents one side's mutable state during automatic combat.
        /// </summary>
        private sealed class CombatForce
        {
            internal readonly List<CapitalShipState> Ships;
            internal readonly List<StarfighterState> Fighters;
            internal readonly List<TacticalUnit> Units;
            private readonly List<TacticalUnit> _targetableUnits = new List<TacticalUnit>();

            internal bool HasCombatants => HasTargetableUnits(Units);
            internal bool HasTargetableShips => HasTargetableUnits(Ships);
            internal bool HasTargetableFighters => HasTargetableUnits(Fighters);
            internal bool HasWithdrawnUnits => HasWithdrawnUnit(Units);
            internal double InitialStrength { get; set; }
            internal SpaceCombatSideOutcome Outcome { get; set; }
            internal bool WithdrawalOrdered { get; set; }

            /// <summary>
            /// Creates tactical state for one combat force.
            /// </summary>
            /// <param name="ships">The force's capital ships.</param>
            /// <param name="fighters">The force's fighter squadrons.</param>
            /// <param name="withdrawalGroups">The force's unit groups capable of retreat.</param>
            /// <param name="config">The automatic combat parameters.</param>
            internal CombatForce(
                IReadOnlyList<CapitalShip> ships,
                IReadOnlyList<Starfighter> fighters,
                IReadOnlyList<IReadOnlyCollection<ISceneNode>> withdrawalGroups,
                GameConfig.SpaceCombatConfig config
            )
            {
                Ships = (ships ?? Array.Empty<CapitalShip>())
                    .Where(ship => ship != null)
                    .Select(ship => new CapitalShipState(ship, config))
                    .ToList();
                Fighters = (fighters ?? Array.Empty<Starfighter>())
                    .Where(fighter => fighter != null)
                    .Select(fighter => new StarfighterState(
                        fighter,
                        config.AutoResolveMinimumManeuverRatio
                    ))
                    .ToList();
                Units = new List<TacticalUnit>(Ships.Count + Fighters.Count);
                Units.AddRange(Ships);
                Units.AddRange(Fighters);
                ConfigureWithdrawalGroups(withdrawalGroups);
                Outcome = SpaceCombatSideOutcome.Active;
            }

            /// <summary>
            /// Returns the force's currently targetable units in stable combat order.
            /// </summary>
            /// <returns>The reusable target collection.</returns>
            internal IReadOnlyList<TacticalUnit> GetTargetableUnits()
            {
                _targetableUnits.Clear();
                foreach (TacticalUnit unit in Units)
                {
                    if (unit.IsTargetable)
                        _targetableUnits.Add(unit);
                }
                return _targetableUnits;
            }

            /// <summary>
            /// Assigns every tactical unit to an eligible or ineligible withdrawal group.
            /// </summary>
            /// <param name="withdrawalGroups">The scene-node groups capable of retreat.</param>
            private void ConfigureWithdrawalGroups(
                IReadOnlyList<IReadOnlyCollection<ISceneNode>> withdrawalGroups
            )
            {
                Dictionary<ISceneNode, TacticalUnit> stateByNode = Units.ToDictionary(unit =>
                    unit.Node
                );
                HashSet<TacticalUnit> assignedUnits = new HashSet<TacticalUnit>();
                foreach (
                    IReadOnlyCollection<ISceneNode> group in withdrawalGroups
                        ?? Array.Empty<IReadOnlyCollection<ISceneNode>>()
                )
                {
                    List<TacticalUnit> members = (group ?? Array.Empty<ISceneNode>())
                        .Where(stateByNode.ContainsKey)
                        .Select(node => stateByNode[node])
                        .Where(assignedUnits.Add)
                        .ToList();
                    if (members.Count > 0)
                        AssignWithdrawalGroup(members, canWithdraw: true);
                }

                foreach (TacticalUnit unit in Units.Where(unit => !assignedUnits.Contains(unit)))
                    AssignWithdrawalGroup(new[] { unit }, canWithdraw: false);
            }

            /// <summary>
            /// Determines whether a tactical collection contains a surviving unit.
            /// </summary>
            /// <typeparam name="TUnit">The tactical-unit type.</typeparam>
            /// <param name="units">The units to inspect.</param>
            /// <returns>True when at least one unit remains targetable.</returns>
            private static bool HasTargetableUnits<TUnit>(IReadOnlyList<TUnit> units)
                where TUnit : TacticalUnit
            {
                foreach (TUnit unit in units)
                {
                    if (unit.IsTargetable)
                        return true;
                }
                return false;
            }

            /// <summary>
            /// Determines whether a tactical collection contains a withdrawn unit.
            /// </summary>
            /// <param name="units">The units to inspect.</param>
            /// <returns>True when at least one unit has withdrawn.</returns>
            private static bool HasWithdrawnUnit(IReadOnlyList<TacticalUnit> units)
            {
                foreach (TacticalUnit unit in units)
                {
                    if (unit.HasWithdrawn)
                        return true;
                }
                return false;
            }

            /// <summary>
            /// Assigns tactical units to a shared withdrawal group.
            /// </summary>
            /// <param name="units">The tactical units to group.</param>
            /// <param name="canWithdraw">Whether the group can leave combat.</param>
            private static void AssignWithdrawalGroup(
                IReadOnlyList<TacticalUnit> units,
                bool canWithdraw
            )
            {
                WithdrawalGroup group = new WithdrawalGroup(units, canWithdraw);
                foreach (TacticalUnit unit in units)
                    unit.SetWithdrawalGroup(group);
            }
        }

        /// <summary>
        /// Coordinates tactical withdrawal for units that must leave combat together.
        /// </summary>
        private sealed class WithdrawalGroup
        {
            private readonly IReadOnlyList<TacticalUnit> _units;

            internal bool CanWithdraw { get; }
            internal bool IsWithdrawing { get; private set; }

            /// <summary>
            /// Creates a withdrawal group.
            /// </summary>
            /// <param name="units">The units that must withdraw together.</param>
            /// <param name="canWithdraw">Whether the group can leave the battle.</param>
            internal WithdrawalGroup(IReadOnlyList<TacticalUnit> units, bool canWithdraw)
            {
                _units = units ?? Array.Empty<TacticalUnit>();
                CanWithdraw = canWithdraw;
            }

            /// <summary>
            /// Starts withdrawal for every surviving member.
            /// </summary>
            internal void BeginWithdrawal()
            {
                if (!CanWithdraw || IsWithdrawing)
                    return;

                IsWithdrawing = true;
                foreach (TacticalUnit unit in _units.Where(unit => unit.IsTargetable))
                    unit.StartWithdrawal();
            }

            /// <summary>
            /// Completes withdrawal once every surviving member reaches safety.
            /// </summary>
            /// <param name="withdrawalDistance">The distance required to leave combat.</param>
            internal void CompleteWithdrawalWhenReady(double withdrawalDistance)
            {
                if (
                    !IsWithdrawing
                    || _units.Any(unit =>
                        unit.IsAlive && unit.WithdrawalDistance < withdrawalDistance
                    )
                )
                    return;

                CompleteWithdrawal();
            }

            /// <summary>
            /// Removes every surviving member from combat as withdrawn.
            /// </summary>
            internal void CompleteWithdrawal()
            {
                HashSet<TacticalUnit> recoverableUnits = GetRecoverableUnits();
                foreach (TacticalUnit unit in _units.Where(unit => unit.IsTargetable))
                {
                    if (unit.CanWithdrawIndependently || recoverableUnits.Contains(unit))
                        unit.FinishWithdrawal();
                    else
                        unit.CancelWithdrawal();
                }
                IsWithdrawing = false;
            }

            /// <summary>
            /// Assigns surviving non-hyperdrive fighters to withdrawing carrier capacity.
            /// Existing mother-ship assignments are honored first, followed by deterministic
            /// reassignment to another carrier that is still able to leave the battle.
            /// </summary>
            /// <returns>The carrier-dependent fighters that can leave with the group.</returns>
            private HashSet<TacticalUnit> GetRecoverableUnits()
            {
                List<CapitalShipState> carriers = _units
                    .OfType<CapitalShipState>()
                    .Where(carrier =>
                        carrier.IsTargetable
                        && carrier.CanWithdrawIndependently
                        && carrier.Ship.StarfighterCapacity > 0
                    )
                    .ToList();
                List<StarfighterState> fighters = _units
                    .OfType<StarfighterState>()
                    .Where(fighter => fighter.IsTargetable)
                    .ToList();
                Dictionary<CapitalShipState, int> remainingCapacity = carriers.ToDictionary(
                    carrier => carrier,
                    carrier => Math.Max(carrier.Ship.StarfighterCapacity, 0)
                );
                HashSet<TacticalUnit> recoverableUnits = new HashSet<TacticalUnit>();

                foreach (CapitalShipState carrier in carriers)
                {
                    IEnumerable<StarfighterState> assignedFighters = fighters
                        .Where(fighter =>
                            ReferenceEquals(
                                fighter.Fighter.GetParentOfType<CapitalShip>(),
                                carrier.Ship
                            )
                        )
                        .OrderBy(fighter => fighter.CanWithdrawIndependently ? 1 : 0);
                    foreach (StarfighterState fighter in assignedFighters)
                    {
                        if (remainingCapacity[carrier] <= 0)
                            break;

                        remainingCapacity[carrier]--;
                        if (!fighter.CanWithdrawIndependently)
                            recoverableUnits.Add(fighter);
                    }
                }

                foreach (
                    StarfighterState fighter in fighters.Where(fighter =>
                        !fighter.CanWithdrawIndependently && !recoverableUnits.Contains(fighter)
                    )
                )
                {
                    CapitalShipState recoveryCarrier = carriers.FirstOrDefault(carrier =>
                        remainingCapacity[carrier] > 0
                    );
                    if (recoveryCarrier == null)
                        continue;

                    remainingCapacity[recoveryCarrier]--;
                    recoverableUnits.Add(fighter);
                }

                return recoverableUnits;
            }
        }

        /// <summary>
        /// Provides common tactical state and target-specific attack behavior.
        /// </summary>
        private abstract class TacticalUnit
        {
            private readonly double _minimumManeuverRatio;
            private WithdrawalGroup _withdrawalGroup;
            private double _approachDistance;
            private double _withdrawalDistance;

            protected double MinimumManeuverRatio => _minimumManeuverRatio;
            internal abstract ISceneNode Node { get; }
            internal abstract bool IsAlive { get; }
            internal abstract bool CanWithdrawIndependently { get; }
            internal abstract double ManeuverRate { get; }
            internal abstract double ClosingSpeed { get; }
            internal virtual double WithdrawalSpeed => ClosingSpeed;
            internal abstract bool IsStarfighter { get; }
            internal abstract double RemainingDurability { get; }
            internal abstract bool CanScanForTargets { get; }
            internal virtual bool IsAttackDelayed => false;
            internal bool CanFire => IsTargetable && !IsWithdrawing && !IsAttackDelayed;
            internal bool CanWithdraw =>
                _withdrawalGroup?.CanWithdraw == true
                && IsTargetable
                && !_withdrawalGroup.IsWithdrawing;
            internal bool HasWithdrawn { get; private set; }
            internal bool IsWithdrawing { get; private set; }
            internal bool IsTargetable => IsAlive && !HasWithdrawn;
            internal double WithdrawalDistance => _withdrawalDistance;

            /// <summary>
            /// Updates this unit's target when scanning and queues available attacks.
            /// </summary>
            /// <param name="targets">The surviving opposing units.</param>
            /// <param name="scansForTarget">Whether the unit performs its periodic target scan.</param>
            /// <param name="engagementDistance">The abstract distance between combat forces.</param>
            /// <param name="pendingDamage">Damage grouped by tactical target.</param>
            internal abstract void QueueAvailableAttacks(
                IReadOnlyList<TacticalUnit> targets,
                bool scansForTarget,
                double engagementDistance,
                IDictionary<TacticalUnit, PendingDamage> pendingDamage
            );

            /// <summary>
            /// Adds attack damage to a tactical target.
            /// </summary>
            /// <param name="pendingDamage">Damage grouped by tactical target.</param>
            /// <param name="target">The tactical target.</param>
            /// <param name="damage">The positive damage to add.</param>
            /// <param name="isIonDamage">Whether the damage comes from ion weapons.</param>
            protected static void AddPendingDamage(
                IDictionary<TacticalUnit, PendingDamage> pendingDamage,
                TacticalUnit target,
                double damage,
                bool isIonDamage
            )
            {
                damage = Math.Max(damage, 1);
                if (!pendingDamage.TryGetValue(target, out PendingDamage targetDamage))
                {
                    targetDamage = new PendingDamage();
                    pendingDamage[target] = targetDamage;
                }

                targetDamage.Add(damage, isIonDamage);
            }

            /// <summary>
            /// Returns the pairwise distance between this unit and a target.
            /// </summary>
            /// <param name="target">The opposing tactical unit.</param>
            /// <param name="startingDistance">The configured initial separation.</param>
            /// <returns>The remaining abstract distance between the units.</returns>
            protected double GetDistanceTo(TacticalUnit target, double startingDistance)
            {
                return Math.Max(
                    Math.Max(startingDistance, 0)
                        - _approachDistance
                        - target._approachDistance
                        + _withdrawalDistance
                        + target._withdrawalDistance,
                    0
                );
            }

            /// <summary>
            /// Calculates this unit's remaining tactical strength.
            /// </summary>
            /// <param name="targetsFighters">Whether the opposing target is a fighter squadron.</param>
            /// <returns>The remaining tactical strength.</returns>
            internal abstract double GetEffectiveness(bool targetsFighters);

            /// <summary>
            /// Applies simultaneous tactical damage.
            /// </summary>
            /// <param name="conventionalDamage">The non-negative hull-damaging attack strength.</param>
            /// <param name="ionDamage">The non-negative shield and subsystem attack strength.</param>
            /// <param name="config">The automatic combat parameters.</param>
            /// <param name="random">The combat random-number stream.</param>
            internal abstract void ApplyDamage(
                double conventionalDamage,
                double ionDamage,
                GameConfig.SpaceCombatConfig config,
                IRandomNumberProvider random
            );

            /// <summary>
            /// Advances temporary damage and recharge state.
            /// </summary>
            /// <param name="config">The automatic combat parameters.</param>
            /// <param name="random">The combat random-number stream.</param>
            internal void AdvanceTacticalState(
                GameConfig.SpaceCombatConfig config,
                IRandomNumberProvider random
            )
            {
                AdvanceUnitState(config, random);
                if (IsWithdrawing)
                {
                    _withdrawalDistance += WithdrawalSpeed;
                    _withdrawalGroup.CompleteWithdrawalWhenReady(
                        Math.Max(config.AutoResolveWithdrawalDistance, 0)
                    );
                    return;
                }

                _approachDistance += ClosingSpeed;
            }

            /// <summary>
            /// Advances unit-specific temporary damage and recharge state.
            /// </summary>
            /// <param name="config">The automatic combat parameters.</param>
            /// <param name="random">The combat random-number stream.</param>
            protected abstract void AdvanceUnitState(
                GameConfig.SpaceCombatConfig config,
                IRandomNumberProvider random
            );

            /// <summary>
            /// Destroys the tactical unit.
            /// </summary>
            internal abstract void Destroy();

            /// <summary>
            /// Starts the vulnerable movement toward the tactical escape boundary.
            /// </summary>
            internal void BeginWithdrawal()
            {
                _withdrawalGroup?.BeginWithdrawal();
            }

            /// <summary>
            /// Removes this surviving unit from combat as withdrawn.
            /// </summary>
            internal void CompleteWithdrawal()
            {
                _withdrawalGroup?.CompleteWithdrawal();
            }

            /// <summary>
            /// Creates tactical state using the configured maneuver floor.
            /// </summary>
            /// <param name="minimumManeuverRatio">The minimum maneuver value and multiplier.</param>
            protected TacticalUnit(double minimumManeuverRatio)
            {
                _minimumManeuverRatio = minimumManeuverRatio;
            }

            /// <summary>
            /// Assigns the unit to its withdrawal group.
            /// </summary>
            /// <param name="withdrawalGroup">The group that coordinates this unit's retreat.</param>
            internal void SetWithdrawalGroup(WithdrawalGroup withdrawalGroup)
            {
                _withdrawalGroup = withdrawalGroup;
            }

            /// <summary>
            /// Starts this unit's movement toward the escape boundary.
            /// </summary>
            internal void StartWithdrawal()
            {
                IsWithdrawing = true;
            }

            /// <summary>
            /// Marks this surviving unit as withdrawn.
            /// </summary>
            internal void FinishWithdrawal()
            {
                if (!IsAlive)
                    return;

                HasWithdrawn = true;
                IsWithdrawing = false;
            }

            /// <summary>
            /// Returns a unit to combat when it reaches the boundary without a way to enter
            /// hyperspace or recover aboard a surviving carrier.
            /// </summary>
            internal void CancelWithdrawal()
            {
                IsWithdrawing = false;
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
            private static readonly PrimaryWeaponType[] _weaponTypes =
            {
                PrimaryWeaponType.Turbolaser,
                PrimaryWeaponType.LaserCannon,
                PrimaryWeaponType.IonCannon,
            };

            internal readonly CapitalShip Ship;
            internal readonly int InitialHull;
            private readonly bool[] _arcQueuedForRecharge = new bool[4];
            private readonly TacticalUnit[,] _arcTargets = new TacticalUnit[4, 3];
            private readonly double[,] _arcTargetDamage = new double[4, 3];
            private readonly double[] _currentArcCharge = new double[4];
            private readonly int[] _ionCannons;
            private readonly int[] _laserCannons;
            private readonly double _maximumHull;
            private readonly double _maximumShields;
            private readonly double[] _maximumArcCharge = new double[4];
            private readonly Queue<int> _rechargeQueue = new Queue<int>();
            private readonly int[] _turbolasers;
            private int _attackDelay;
            private int _movementDelay;

            internal double CurrentHull { get; private set; }
            internal double CurrentShields { get; private set; }
            internal override ISceneNode Node => Ship;
            internal override bool IsAlive => CurrentHull > 0;
            internal override bool CanWithdrawIndependently => Ship.Hyperdrive > 0;
            internal override bool IsStarfighter => false;
            internal override double RemainingDurability => CurrentHull + CurrentShields;
            internal override bool IsAttackDelayed => _attackDelay > 0;
            internal override bool CanScanForTargets
            {
                get
                {
                    for (int arc = 0; arc < _currentArcCharge.Length; arc++)
                    {
                        if (
                            _maximumArcCharge[arc] > 0
                            && _currentArcCharge[arc] >= _maximumArcCharge[arc]
                        )
                            return true;
                    }

                    return false;
                }
            }
            internal override double ClosingSpeed =>
                _movementDelay > 0 ? 0 : Math.Max(Ship.SublightSpeed, 0);
            internal override double ManeuverRate =>
                _movementDelay > 0
                    ? MinimumManeuverRatio
                    : Math.Max(Ship.SublightSpeed + Ship.Maneuverability, MinimumManeuverRatio);

            /// <summary>
            /// Creates tactical state from a capital ship's current strategic state.
            /// </summary>
            /// <param name="ship">The capital ship entering combat.</param>
            /// <param name="config">The automatic combat parameters.</param>
            internal CapitalShipState(CapitalShip ship, GameConfig.SpaceCombatConfig config)
                : base(config.AutoResolveMinimumManeuverRatio)
            {
                Ship = ship;
                InitialHull = Math.Max(ship.CurrentHullStrength, 0);
                _maximumHull = Math.Max(ship.MaxHullStrength, 1);
                _maximumShields = Math.Max(ship.MaxShieldStrength, 0);
                _turbolasers = GetWeaponValues(ship, PrimaryWeaponType.Turbolaser);
                _laserCannons = GetWeaponValues(ship, PrimaryWeaponType.LaserCannon);
                _ionCannons = GetWeaponValues(ship, PrimaryWeaponType.IonCannon);
                CurrentHull = InitialHull;
                CurrentShields = _maximumShields;
                for (int arc = 0; arc < _maximumArcCharge.Length; arc++)
                {
                    _maximumArcCharge[arc] = GetArcStrength(
                        arc,
                        targetsFighters: false,
                        engagementDistance: 0,
                        requireRange: false
                    );
                    _currentArcCharge[arc] = _maximumArcCharge[arc];
                }
            }

            /// <inheritdoc />
            internal override void QueueAvailableAttacks(
                IReadOnlyList<TacticalUnit> targets,
                bool scansForTarget,
                double engagementDistance,
                IDictionary<TacticalUnit, PendingDamage> pendingDamage
            )
            {
                if (scansForTarget)
                    ScanForArcTargets(targets, engagementDistance);

                double condition = CurrentHull / _maximumHull;
                int firedArcMask = 0;
                for (
                    int firedArcCount = 0;
                    firedArcCount < _currentArcCharge.Length;
                    firedArcCount++
                )
                {
                    double strongestDamage = 0;
                    int selectedArc = -1;
                    for (int arc = 0; arc < _currentArcCharge.Length; arc++)
                    {
                        if ((firedArcMask & (1 << arc)) != 0)
                            continue;

                        if (_currentArcCharge[arc] < _maximumArcCharge[arc])
                            continue;

                        double candidateDamage = GetQueuedArcDamage(
                            arc,
                            engagementDistance,
                            condition
                        );
                        if (candidateDamage <= strongestDamage)
                            continue;

                        strongestDamage = candidateDamage;
                        selectedArc = arc;
                    }

                    if (selectedArc < 0)
                        break;

                    firedArcMask |= 1 << selectedArc;
                    QueueArcAttacks(selectedArc, engagementDistance, condition, pendingDamage);
                }
            }

            /// <summary>
            /// Updates every charged weapon lane with its strongest available target.
            /// </summary>
            /// <param name="targets">The surviving opposing units.</param>
            /// <param name="engagementDistance">The abstract distance between combat forces.</param>
            private void ScanForArcTargets(
                IReadOnlyList<TacticalUnit> targets,
                double engagementDistance
            )
            {
                double condition = CurrentHull / _maximumHull;
                for (int arc = 0; arc < _currentArcCharge.Length; arc++)
                {
                    if (_currentArcCharge[arc] < _maximumArcCharge[arc])
                        continue;

                    for (int weaponIndex = 0; weaponIndex < _weaponTypes.Length; weaponIndex++)
                    {
                        _arcTargets[arc, weaponIndex] = null;
                        _arcTargetDamage[arc, weaponIndex] = 0;
                    }
                }

                foreach (TacticalUnit candidate in targets)
                {
                    double engagementRange = GetDistanceTo(candidate, engagementDistance);
                    double maneuverMultiplier = GetManeuverMultiplier(candidate);
                    for (int arc = 0; arc < _currentArcCharge.Length; arc++)
                    {
                        if (_currentArcCharge[arc] < _maximumArcCharge[arc])
                            continue;

                        for (int weaponIndex = 0; weaponIndex < _weaponTypes.Length; weaponIndex++)
                        {
                            PrimaryWeaponType weaponType = _weaponTypes[weaponIndex];
                            if (
                                weaponType == PrimaryWeaponType.IonCannon
                                && candidate.IsStarfighter
                            )
                                continue;

                            double candidateDamage =
                                GetWeaponStrength(
                                    weaponType,
                                    arc,
                                    engagementRange,
                                    requireRange: true
                                )
                                * maneuverMultiplier
                                * condition;
                            if (candidateDamage <= _arcTargetDamage[arc, weaponIndex])
                                continue;

                            _arcTargetDamage[arc, weaponIndex] = candidateDamage;
                            _arcTargets[arc, weaponIndex] = candidate;
                        }
                    }
                }
            }

            /// <summary>
            /// Returns the damage queued by all valid lanes on an arc.
            /// </summary>
            /// <param name="arc">The zero-based firing arc.</param>
            /// <param name="startingDistance">The configured initial separation.</param>
            /// <param name="condition">The ship's current hull condition.</param>
            /// <returns>The arc's combined pending damage.</returns>
            private double GetQueuedArcDamage(int arc, double startingDistance, double condition)
            {
                double damage = 0;
                for (int weaponIndex = 0; weaponIndex < _weaponTypes.Length; weaponIndex++)
                {
                    TacticalUnit target = _arcTargets[arc, weaponIndex];
                    if (target?.IsTargetable != true)
                        continue;

                    damage +=
                        GetWeaponStrength(
                            _weaponTypes[weaponIndex],
                            arc,
                            GetDistanceTo(target, startingDistance),
                            requireRange: true
                        )
                        * GetManeuverMultiplier(target)
                        * condition;
                }

                return damage;
            }

            /// <summary>
            /// Queues each valid lane on an arc against its independently selected target.
            /// </summary>
            /// <param name="arc">The zero-based firing arc.</param>
            /// <param name="startingDistance">The configured initial separation.</param>
            /// <param name="condition">The ship's current hull condition.</param>
            /// <param name="pendingDamage">Damage grouped by tactical target.</param>
            private void QueueArcAttacks(
                int arc,
                double startingDistance,
                double condition,
                IDictionary<TacticalUnit, PendingDamage> pendingDamage
            )
            {
                double consumedCharge = 0;
                for (int weaponIndex = 0; weaponIndex < _weaponTypes.Length; weaponIndex++)
                {
                    TacticalUnit target = _arcTargets[arc, weaponIndex];
                    if (target?.IsTargetable != true)
                        continue;

                    int weaponStrength = GetWeaponStrength(
                        _weaponTypes[weaponIndex],
                        arc,
                        GetDistanceTo(target, startingDistance),
                        requireRange: true
                    );
                    if (weaponStrength <= 0)
                        continue;

                    double damage = weaponStrength * GetManeuverMultiplier(target) * condition;
                    AddPendingDamage(
                        pendingDamage,
                        target,
                        damage,
                        _weaponTypes[weaponIndex] == PrimaryWeaponType.IonCannon
                    );
                    consumedCharge += weaponStrength;
                }

                if (consumedCharge > 0)
                    DischargeArc(arc, consumedCharge);
            }

            /// <inheritdoc />
            internal override double GetEffectiveness(bool targetsFighters)
            {
                double condition = CurrentHull / _maximumHull;
                return GetStrongestArcStrength(targetsFighters) * condition;
            }

            /// <inheritdoc />
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
            }

            /// <inheritdoc />
            protected override void AdvanceUnitState(
                GameConfig.SpaceCombatConfig config,
                IRandomNumberProvider random
            )
            {
                int delayRecovery = Math.Max(config.AutoResolveComponentDelayRecovery, 1);
                _attackDelay = Math.Max(_attackDelay - delayRecovery, 0);
                _movementDelay = Math.Max(_movementDelay - delayRecovery, 0);
                RechargeShields();
                RechargeWeapons();
            }

            /// <inheritdoc />
            internal override void Destroy()
            {
                CurrentHull = 0;
                CurrentShields = 0;
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
            /// <param name="config">The automatic combat parameters.</param>
            /// <param name="random">The combat random-number stream.</param>
            private void ApplyComponentDamage(
                double ionOverflowDamage,
                GameConfig.SpaceCombatConfig config,
                IRandomNumberProvider random
            )
            {
                double interval = Math.Max(
                    config.AutoResolveComponentDamageInterval,
                    double.Epsilon
                );
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
            /// Returns a random temporary component-delay duration.
            /// </summary>
            /// <param name="config">The automatic combat parameters.</param>
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
            /// Recharges shields according to current hull condition.
            /// </summary>
            private void RechargeShields()
            {
                double condition = CurrentHull / _maximumHull;
                CurrentShields = Math.Min(
                    _maximumShields,
                    CurrentShields + Math.Max(Ship.ShieldRechargeRate, 0) * condition
                );
            }

            /// <summary>
            /// Distributes the ship's recharge budget across depleted arcs in order.
            /// </summary>
            private void RechargeWeapons()
            {
                double condition = CurrentHull / _maximumHull;
                double recharge = Math.Max(Ship.WeaponRecharge, 0) * condition;
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
                for (int arc = 0; arc < 4; arc++)
                    strongestArc = Math.Max(
                        strongestArc,
                        GetArcStrength(
                            arc,
                            targetsFighters,
                            engagementDistance: 0,
                            requireRange: true
                        )
                    );

                return strongestArc;
            }

            /// <summary>
            /// Returns one arc's usable primary-weapon strength.
            /// </summary>
            /// <param name="arc">The zero-based firing arc.</param>
            /// <param name="targetsFighters">Whether the target is a fighter squadron.</param>
            /// <param name="engagementDistance">The abstract distance between combat forces.</param>
            /// <param name="requireRange">Whether weapons without a range are excluded.</param>
            /// <returns>The arc strength.</returns>
            private double GetArcStrength(
                int arc,
                bool targetsFighters,
                double engagementDistance,
                bool requireRange
            )
            {
                double strength =
                    GetWeaponStrength(
                        PrimaryWeaponType.Turbolaser,
                        arc,
                        engagementDistance,
                        requireRange
                    )
                    + GetWeaponStrength(
                        PrimaryWeaponType.LaserCannon,
                        arc,
                        engagementDistance,
                        requireRange
                    );
                if (!targetsFighters)
                {
                    strength += GetWeaponStrength(
                        PrimaryWeaponType.IonCannon,
                        arc,
                        engagementDistance,
                        requireRange
                    );
                }
                return strength;
            }

            /// <summary>
            /// Returns one weapon type's non-negative strength on an arc.
            /// </summary>
            /// <param name="type">The weapon type.</param>
            /// <param name="arc">The zero-based firing arc.</param>
            /// <param name="engagementDistance">The abstract distance between combat forces.</param>
            /// <param name="requireRange">Whether a positive weapon range is required.</param>
            /// <returns>The weapon strength.</returns>
            private int GetWeaponStrength(
                PrimaryWeaponType type,
                int arc,
                double engagementDistance,
                bool requireRange
            )
            {
                int[] values = type switch
                {
                    PrimaryWeaponType.Turbolaser => _turbolasers,
                    PrimaryWeaponType.LaserCannon => _laserCannons,
                    PrimaryWeaponType.IonCannon => _ionCannons,
                    _ => null,
                };
                if (
                    values == null
                    || arc >= values.Length
                    || (
                        requireRange
                        && (values.Length < 5 || values[4] <= 0 || values[4] < engagementDistance)
                    )
                )
                    return 0;

                return Math.Max(values[arc], 0);
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
        }

        /// <summary>
        /// Stores a fighter squadron's tactical durability and attack state.
        /// </summary>
        private sealed class StarfighterState : TacticalUnit
        {
            internal readonly Starfighter Fighter;
            internal readonly int InitialSquadronSize;
            private readonly double _durabilityPerFighter;
            private readonly double[] _weaponTargetDamage = new double[3];
            private readonly TacticalUnit[] _weaponTargets = new TacticalUnit[3];
            private double _currentDurability;

            internal int CurrentSquadronSize =>
                IsAlive
                    ? Math.Min(
                        InitialSquadronSize,
                        (int)Math.Ceiling(_currentDurability / _durabilityPerFighter)
                    )
                    : 0;
            internal override ISceneNode Node => Fighter;
            internal override bool IsAlive => _currentDurability > 0;
            internal override bool CanWithdrawIndependently => Fighter.Hyperdrive > 0;
            internal override bool IsStarfighter => true;
            internal override double RemainingDurability => _currentDurability;
            internal override bool CanScanForTargets =>
                GetCombinedWeaponStrength(
                    targetsFighters: false,
                    engagementDistance: 0,
                    requireRange: true
                ) > 0;
            internal override double ClosingSpeed => Math.Max(Fighter.SublightSpeed, 0);
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
            internal override void QueueAvailableAttacks(
                IReadOnlyList<TacticalUnit> targets,
                bool scansForTarget,
                double engagementDistance,
                IDictionary<TacticalUnit, PendingDamage> pendingDamage
            )
            {
                if (scansForTarget)
                    ScanForWeaponTargets(targets, engagementDistance);

                double squadronStrength = GetRemainingSquadronStrength();
                for (int weaponIndex = 0; weaponIndex < _weaponTargets.Length; weaponIndex++)
                {
                    TacticalUnit target = _weaponTargets[weaponIndex];
                    if (target?.IsTargetable != true)
                        continue;

                    int weaponStrength = GetWeaponStrength(
                        weaponIndex,
                        GetDistanceTo(target, engagementDistance),
                        requireRange: true
                    );
                    double damage =
                        weaponStrength * GetManeuverMultiplier(target) * squadronStrength;
                    if (damage > 0)
                    {
                        AddPendingDamage(pendingDamage, target, damage, weaponIndex == 2);
                    }
                }
            }

            /// <summary>
            /// Updates each fighter weapon lane with its strongest available target.
            /// </summary>
            /// <param name="targets">The surviving opposing units.</param>
            /// <param name="engagementDistance">The abstract distance between combat forces.</param>
            private void ScanForWeaponTargets(
                IReadOnlyList<TacticalUnit> targets,
                double engagementDistance
            )
            {
                double squadronStrength = GetRemainingSquadronStrength();
                for (int weaponIndex = 0; weaponIndex < _weaponTargets.Length; weaponIndex++)
                {
                    _weaponTargets[weaponIndex] = null;
                    _weaponTargetDamage[weaponIndex] = 0;
                }

                foreach (TacticalUnit candidate in targets)
                {
                    double engagementRange = GetDistanceTo(candidate, engagementDistance);
                    double maneuverMultiplier = GetManeuverMultiplier(candidate);
                    for (int weaponIndex = 0; weaponIndex < _weaponTargets.Length; weaponIndex++)
                    {
                        if (weaponIndex == 2 && candidate.IsStarfighter)
                            continue;

                        double candidateDamage =
                            GetWeaponStrength(weaponIndex, engagementRange, requireRange: true)
                            * maneuverMultiplier
                            * squadronStrength;
                        if (candidateDamage <= _weaponTargetDamage[weaponIndex])
                            continue;

                        _weaponTargetDamage[weaponIndex] = candidateDamage;
                        _weaponTargets[weaponIndex] = candidate;
                    }
                }
            }

            /// <inheritdoc />
            internal override double GetEffectiveness(bool targetsFighters)
            {
                return GetCombinedWeaponStrength(
                        targetsFighters,
                        engagementDistance: 0,
                        requireRange: true
                    ) * GetRemainingSquadronStrength();
            }

            /// <inheritdoc />
            internal override void ApplyDamage(
                double conventionalDamage,
                double ionDamage,
                GameConfig.SpaceCombatConfig config,
                IRandomNumberProvider random
            )
            {
                _currentDurability = Math.Max(
                    _currentDurability - Math.Max(conventionalDamage, 0),
                    0
                );
            }

            /// <inheritdoc />
            protected override void AdvanceUnitState(
                GameConfig.SpaceCombatConfig config,
                IRandomNumberProvider random
            ) { }

            /// <inheritdoc />
            internal override void Destroy()
            {
                _currentDurability = 0;
            }

            /// <summary>
            /// Returns the durability-adjusted fraction of the full squadron that survives.
            /// </summary>
            /// <returns>The surviving fraction of the full squadron.</returns>
            private double GetRemainingSquadronStrength()
            {
                double maximumSquadronSize = Math.Max(Fighter.MaxSquadronSize, 1);
                return Math.Min(
                    _currentDurability / (_durabilityPerFighter * maximumSquadronSize),
                    1
                );
            }

            /// <summary>
            /// Returns the squadron's usable weapon strength against a target type.
            /// </summary>
            /// <param name="targetsFighters">Whether the target is a fighter squadron.</param>
            /// <param name="engagementDistance">The abstract distance between combat forces.</param>
            /// <param name="requireRange">Whether a positive weapon range is required.</param>
            /// <returns>The fighter's usable weapon strength.</returns>
            private int GetCombinedWeaponStrength(
                bool targetsFighters,
                double engagementDistance,
                bool requireRange
            )
            {
                int strength =
                    GetWeaponStrength(0, engagementDistance, requireRange)
                    + GetWeaponStrength(1, engagementDistance, requireRange);
                if (!targetsFighters)
                    strength += GetWeaponStrength(2, engagementDistance, requireRange);
                return strength;
            }

            /// <summary>
            /// Returns one squadron weapon lane's usable strength.
            /// </summary>
            /// <param name="weaponIndex">The zero-based weapon lane.</param>
            /// <param name="engagementDistance">The abstract distance to the target.</param>
            /// <param name="requireRange">Whether a positive range is required.</param>
            /// <returns>The usable weapon strength.</returns>
            private int GetWeaponStrength(
                int weaponIndex,
                double engagementDistance,
                bool requireRange
            )
            {
                return weaponIndex switch
                {
                    0 => GetRangedWeaponStrength(
                        Fighter.LaserCannon,
                        Fighter.LaserRange,
                        engagementDistance,
                        requireRange
                    ),
                    1 => GetRangedWeaponStrength(
                        Fighter.Torpedoes,
                        Fighter.TorpedoRange,
                        engagementDistance,
                        requireRange
                    ),
                    2 => GetRangedWeaponStrength(
                        Fighter.IonCannon,
                        Fighter.IonRange,
                        engagementDistance,
                        requireRange
                    ),
                    _ => 0,
                };
            }

            /// <summary>
            /// Returns a fighter weapon's non-negative usable strength.
            /// </summary>
            /// <param name="strength">The configured weapon strength.</param>
            /// <param name="range">The configured weapon range.</param>
            /// <param name="engagementDistance">The abstract distance between combat forces.</param>
            /// <param name="requireRange">Whether a positive range is required.</param>
            /// <returns>The usable weapon strength.</returns>
            private static int GetRangedWeaponStrength(
                int strength,
                int range,
                double engagementDistance,
                bool requireRange
            )
            {
                return requireRange && (range <= 0 || range < engagementDistance)
                    ? 0
                    : Math.Max(strength, 0);
            }
        }
    }
}
