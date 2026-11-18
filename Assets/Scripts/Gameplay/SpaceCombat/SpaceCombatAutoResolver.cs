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
    /// Stores the tactical command bonuses contributed by one side's ranking officers.
    /// </summary>
    public readonly struct SpaceCombatCommandModifiers
    {
        /// <summary>
        /// Gets the selected Admiral's effective Leadership rating.
        /// </summary>
        public int AdmiralLeadership { get; }

        /// <summary>
        /// Gets the selected Commander's effective Combat rating.
        /// </summary>
        public int CommanderCombat { get; }

        /// <summary>
        /// Gets the Admiral's carrier, whose presence sustains the capital-ship command bonus.
        /// </summary>
        public CapitalShip AdmiralShip { get; }

        /// <summary>
        /// Gets the Commander's carrier at the start of the battle.
        /// </summary>
        public CapitalShip CommanderShip { get; }

        /// <summary>
        /// Creates command modifiers from the officers selected for tactical command.
        /// </summary>
        /// <param name="admiralLeadership">The selected Admiral's effective Leadership.</param>
        /// <param name="commanderCombat">The selected Commander's effective Combat.</param>
        /// <param name="admiralShip">The Admiral's carrier, or null for an unbound rating estimate.</param>
        /// <param name="commanderShip">The Commander's carrier, or null for an unbound rating estimate.</param>
        public SpaceCombatCommandModifiers(
            int admiralLeadership,
            int commanderCombat,
            CapitalShip admiralShip = null,
            CapitalShip commanderShip = null
        )
        {
            AdmiralLeadership = Math.Max(admiralLeadership, 0);
            CommanderCombat = Math.Max(commanderCombat, 0);
            AdmiralShip = admiralShip;
            CommanderShip = commanderShip;
        }
    }

    /// <summary>
    /// Resolves a space battle without constructing or rendering the tactical scene.
    /// </summary>
    public sealed class SpaceCombatAutoResolver
    {
        private readonly GameConfig.SpaceCombatConfig _config;
        private readonly IRandomNumberProvider _random;
        private readonly IReadOnlyList<Starfighter> _starfighterTypes;

        /// <summary>
        /// Creates an automatic resolver using the supplied combat parameters.
        /// </summary>
        /// <param name="config">The automatic space-combat resolution parameters.</param>
        /// <param name="random">The persisted game random-number stream.</param>
        /// <param name="starfighterTypes">The available fighter designs, including absent formation members.</param>
        public SpaceCombatAutoResolver(
            GameConfig.SpaceCombatConfig config,
            IRandomNumberProvider random,
            IReadOnlyList<Starfighter> starfighterTypes = null
        )
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _random = random ?? throw new ArgumentNullException(nameof(random));
            _starfighterTypes = starfighterTypes ?? Array.Empty<Starfighter>();
        }

        /// <summary>
        /// Resolves the supplied forces until one side is destroyed, withdraws, or combat stalls.
        /// </summary>
        /// <param name="attackerShips">The attacking capital ships.</param>
        /// <param name="attackerFighters">The attacking fighter squadrons.</param>
        /// <param name="defenderShips">The defending capital ships.</param>
        /// <param name="defenderFighters">The defending fighter squadrons.</param>
        /// <param name="attackerCanWithdraw">Whether the attacking force can withdraw.</param>
        /// <param name="defenderCanWithdraw">Whether the defending force can withdraw.</param>
        /// <param name="attackerCommand">The attacking side's tactical command bonuses.</param>
        /// <param name="defenderCommand">The defending side's tactical command bonuses.</param>
        /// <param name="shieldedPlanetDestroyers">
        /// The planet destroyers among the ships that a shield generator on the planet protects
        /// from fighter attack, or null when none is.
        /// </param>
        /// <param name="attackerHasInitiative">Whether the attacker performs the first side update.</param>
        /// <param name="attackerRetreatStrengthRatio">The attacker's retreat threshold, or null for the content setting.</param>
        /// <param name="defenderRetreatStrengthRatio">The defender's retreat threshold, or null for the content setting.</param>
        /// <returns>The resolved tactical state for both forces.</returns>
        public SpaceCombatResult Resolve(
            IReadOnlyList<CapitalShip> attackerShips,
            IReadOnlyList<Starfighter> attackerFighters,
            IReadOnlyList<CapitalShip> defenderShips,
            IReadOnlyList<Starfighter> defenderFighters,
            bool attackerCanWithdraw,
            bool defenderCanWithdraw,
            SpaceCombatCommandModifiers attackerCommand = default,
            SpaceCombatCommandModifiers defenderCommand = default,
            IReadOnlyCollection<CapitalShip> shieldedPlanetDestroyers = null,
            bool attackerHasInitiative = false,
            double? attackerRetreatStrengthRatio = null,
            double? defenderRetreatStrengthRatio = null
        )
        {
            CombatForce attacker = new CombatForce(
                attackerShips,
                attackerFighters,
                attackerCanWithdraw,
                _config,
                attackerCommand,
                shieldedPlanetDestroyers,
                _random,
                attackerRetreatStrengthRatio
            );
            CombatForce defender = new CombatForce(
                defenderShips,
                defenderFighters,
                defenderCanWithdraw,
                _config,
                defenderCommand,
                shieldedPlanetDestroyers,
                _random,
                defenderRetreatStrengthRatio
            );
            attacker.InitializeGroups(_starfighterTypes, attackerHasInitiative);
            defender.InitializeGroups(_starfighterTypes, !attackerHasInitiative);
            CombatForce first = attackerHasInitiative ? attacker : defender;
            CombatForce second = attackerHasInitiative ? defender : attacker;
            bool hasPlanetDestroyers =
                first.PlanetDestroyers.Count > 0 || second.PlanetDestroyers.Count > 0;
            float firstMinimum = (float)GetTacticalStrength(first, second);
            float secondMinimum = (float)GetTacticalStrength(second, first);
            int firstStall = 0;
            int secondStall = 0;
            int iterationsCompleted = 0;
            while (true)
            {
                if (CompleteEliminatedForces(attacker, defender))
                    break;

                iterationsCompleted++;
                if (hasPlanetDestroyers)
                {
                    if (iterationsCompleted == 1)
                    {
                        first.LaunchFighters();
                        second.LaunchFighters();
                    }
                    OrderAttackRuns(first, second);
                    OrderAttackRuns(second, first);
                    PreparePlanetDestroyerWithdrawal(first, second);
                    PreparePlanetDestroyerWithdrawal(second, first);
                    FireSuperlasers(first, second);
                    FireSuperlasers(second, first);
                }
                ResolveSide(first, second, hasPlanetDestroyers);
                ResolveSide(second, first, hasPlanetDestroyers);
                if (hasPlanetDestroyers)
                {
                    AdvancePlanetDestroyers(first, second);
                    AdvancePlanetDestroyers(second, first);
                    ResolveAttackRuns(first, second);
                    ResolveAttackRuns(second, first);
                }
                if (CompleteEliminatedForces(attacker, defender))
                    break;

                float firstStrength = (float)GetTacticalStrength(first, second);
                float secondStrength = (float)GetTacticalStrength(second, first);
                bool specialProgress =
                    hasPlanetDestroyers
                    && (
                        HasPlanetDestroyerProgress(first, second)
                        || HasPlanetDestroyerProgress(second, first)
                    );
                firstStall = specialProgress || firstStrength < firstMinimum ? 0 : firstStall + 1;
                secondStall =
                    specialProgress || secondStrength < secondMinimum ? 0 : secondStall + 1;
                firstMinimum = Math.Min(firstMinimum, firstStrength);
                secondMinimum = Math.Min(secondMinimum, secondStrength);
                if (
                    firstStall >= _config.AutoResolveStagnationIterations
                    && secondStall >= _config.AutoResolveStagnationIterations
                )
                {
                    if (first.HasPlanetDestroyerInBattle || second.HasPlanetDestroyerInBattle)
                        break;
                    if (firstStrength <= secondStrength)
                        ResolveStalledSide(first, second);
                    if (secondStrength <= firstStrength)
                        ResolveStalledSide(second, first);
                }
            }

            return CreateResult(attacker, defender, iterationsCompleted);
        }

        /// <summary>
        /// Advances one side's groups, retreat decisions, and shield recovery in combat order.
        /// </summary>
        /// <param name="force">The side taking its update.</param>
        /// <param name="opponent">The opposing side.</param>
        /// <param name="hasPlanetDestroyers">Whether special actions advance in shared phases.</param>
        private void ResolveSide(CombatForce force, CombatForce opponent, bool hasPlanetDestroyers)
        {
            if (!force.HasUnresolvedUnits)
                return;
            if (force.HasInitiative && !hasPlanetDestroyers)
            {
                force.LaunchFighters();
                opponent.LaunchFighters();
            }
            force.AssignTargets(opponent);
            foreach (CombatGroup group in force.Groups)
            {
                TacticalUnit target = group.Target;
                if (
                    target?.IsAlive != true
                    || group.IsRetired
                    || (hasPlanetDestroyers && !target.IsTargetable)
                )
                    continue;
                target.ApplyDamage(group.GetDamageRate(target), _random);
                if (!target.IsAlive)
                    opponent.RemoveDestroyedUnit(target);
            }
            if (
                !hasPlanetDestroyers
                && CanWithdraw(force, opponent)
                && !force.HasAttackRunUnderWay
                && HasReachedWithdrawalThreshold(force, opponent)
            )
                force.CompleteWithdrawal();
            AdvanceTacticalState(force);
        }

        /// <summary>
        /// Orders escorts and special ships to escape before superlaser targets are reserved.
        /// </summary>
        /// <param name="force">The side whose escape decisions are prepared.</param>
        /// <param name="opponent">The side providing the opposing escort strength.</param>
        private static void PreparePlanetDestroyerWithdrawal(
            CombatForce force,
            CombatForce opponent
        )
        {
            if (
                !force.WithdrawalOrdered
                && force.HasCombatants
                && !force.HasAttackRunUnderWay
                && CanWithdraw(force, opponent)
            )
            {
                double opposingStrength = GetPlanetDestroyerEscortStrength(opponent);
                if (
                    opposingStrength > 0
                    && GetPlanetDestroyerEscortStrength(force) / opposingStrength
                        < force.RetreatStrengthRatio
                )
                    force.BeginWithdrawal();
            }
            WithdrawFromPlanetDestroyer(force, opponent);
            WithdrawPlanetDestroyerFromFighterStalemate(force, opponent);
            if (force.WithdrawalOrdered)
                force.CompleteWithdrawal();
        }

        /// <summary>
        /// Measures ordinary escort strength for special-ship withdrawal decisions.
        /// </summary>
        /// <param name="force">The side whose escorts are measured.</param>
        /// <returns>The sustained output of its targetable ordinary units.</returns>
        private static double GetPlanetDestroyerEscortStrength(CombatForce force)
        {
            double strength = 0;
            foreach (TacticalUnit unit in force.Units)
                if (unit.IsTargetable)
                    strength += unit.GetEffectiveness();
            return strength;
        }

        /// <summary>
        /// Keeps finite special actions from being classified as a stalled battle.
        /// </summary>
        /// <param name="force">The side whose special actions are inspected.</param>
        /// <param name="opponent">The side that may still supply a superlaser target.</param>
        /// <returns>True when an attack run, withdrawal, or usable superlaser is progressing.</returns>
        private static bool HasPlanetDestroyerProgress(CombatForce force, CombatForce opponent)
        {
            if (force.HasAttackRunUnderWay || force.HasPendingWithdrawal)
                return true;
            if (force.WithdrawalOrdered || !opponent.HasTargetableShips)
                return false;
            foreach (PlanetDestroyerState ship in force.PlanetDestroyers)
                if (ship.IsInBattle && !ship.IsWithdrawing)
                    return true;
            return false;
        }

        /// <summary>
        /// Determines whether a side still has a usable escape route and mobile hyperdrives.
        /// </summary>
        /// <param name="force">The side requesting escape.</param>
        /// <param name="opponent">The side that may prevent escape.</param>
        /// <returns>Whether an automatic withdrawal can be attempted.</returns>
        private static bool CanWithdraw(CombatForce force, CombatForce opponent)
        {
            if (!force.CanWithdraw || force.Outcome != SpaceCombatSideOutcome.Active)
                return false;
            foreach (CapitalShipState ship in opponent.Ships)
                if (ship.IsTargetable && ship.Ship.HasGravityWell)
                    return false;
            foreach (TacticalUnit unit in force.Units)
                if (unit.IsTargetable && unit.CanWithdrawIndependently)
                    return true;
            return force.HasPlanetDestroyerInBattle
                && force.PlanetDestroyers.Any(ship =>
                    ship.IsInBattle && ship.CanWithdrawIndependently
                );
        }

        /// <summary>
        /// Forces a stalled side to leave on its next update or destroys its trapped units.
        /// </summary>
        /// <param name="force">The stalled side.</param>
        /// <param name="opponent">Its opponent.</param>
        private static void ResolveStalledSide(CombatForce force, CombatForce opponent)
        {
            if (CanWithdraw(force, opponent))
                force.ForcedWithdrawal = true;
            else if (!force.HasPlanetDestroyerInBattle)
                force.DestroyRemainingUnits();
        }

        /// <summary>
        /// Completes sides without units remaining in combat.
        /// </summary>
        /// <param name="attacker">The attacking force.</param>
        /// <param name="defender">The defending force.</param>
        /// <returns>True when combat has ended.</returns>
        private static bool CompleteEliminatedForces(CombatForce attacker, CombatForce defender)
        {
            bool attackerUnresolved = attacker.HasUnresolvedUnits;
            bool defenderUnresolved = defender.HasUnresolvedUnits;
            if (attackerUnresolved && defenderUnresolved)
                return false;

            if (!attackerUnresolved)
                CompleteForce(attacker);
            if (!defenderUnresolved)
                CompleteForce(defender);
            return !attacker.HasPendingWithdrawal && !defender.HasPendingWithdrawal;
        }

        /// <summary>
        /// Withdraws a force that has nothing left to fight but a planet destroyer it cannot
        /// attack: staying would only feed its ships to the superlaser.
        /// </summary>
        /// <param name="force">The force whose withdrawal is evaluated.</param>
        /// <param name="opposingForce">The force that may own a planet destroyer.</param>
        private static void WithdrawFromPlanetDestroyer(
            CombatForce force,
            CombatForce opposingForce
        )
        {
            if (
                force.WithdrawalOrdered
                || !force.CanWithdraw
                || !force.HasCombatants
                || !opposingForce.HasPlanetDestroyerInBattle
                || opposingForce.HasCombatants
                || opposingForce.WithdrawalOrdered
            )
                return;

            bool canAttack =
                opposingForce.PlanetDestroyers.Any(planetDestroyer => planetDestroyer.IsAttackable)
                && force.Fighters.Any(fighter =>
                    fighter.IsAlive && !fighter.HasWithdrawn && fighter.HasTorpedoes
                );
            if (!canAttack)
                force.BeginWithdrawal();
        }

        /// <summary>
        /// Withdraws an independently mobile planet destroyer when the opposing force contains
        /// only fighters that cannot attack it and the planet destroyer has no valid target.
        /// </summary>
        /// <param name="force">The force containing the planet destroyer.</param>
        /// <param name="opposingForce">The opposing fighter force.</param>
        private static void WithdrawPlanetDestroyerFromFighterStalemate(
            CombatForce force,
            CombatForce opposingForce
        )
        {
            if (
                force.WithdrawalOrdered
                || !force.CanWithdraw
                || force.HasCombatants
                || !force.HasPlanetDestroyerInBattle
                || !opposingForce.HasCombatants
                || opposingForce.HasTargetableShips
                || opposingForce.WithdrawalOrdered
            )
                return;

            bool opposingForceCanAttack =
                force.PlanetDestroyers.Any(planetDestroyer => planetDestroyer.IsAttackable)
                && opposingForce.Fighters.Any(fighter =>
                    fighter.IsAlive && !fighter.HasWithdrawn && fighter.HasTorpedoes
                );
            if (!opposingForceCanAttack)
                force.BeginWithdrawal();
        }

        /// <summary>
        /// Sends a force's available torpedo squadrons on an attack run against the first enemy
        /// planet destroyer that can be attacked and does not already have a run in progress.
        /// </summary>
        /// <param name="force">The force whose squadrons are sent.</param>
        /// <param name="opposingForce">The force that may own a planet destroyer.</param>
        private void OrderAttackRuns(CombatForce force, CombatForce opposingForce)
        {
            if (force.WithdrawalOrdered)
                return;

            PlanetDestroyerState planetDestroyer = opposingForce.PlanetDestroyers.FirstOrDefault(
                candidate => candidate.IsAttackable && !candidate.HasAttackRun
            );
            if (planetDestroyer == null)
                return;

            List<StarfighterState> participants = force
                .Fighters.Where(fighter =>
                    fighter.IsTargetable && fighter.HasTorpedoes && fighter.AttackRunTarget == null
                )
                .ToList();
            if (participants.Count == 0)
                return;

            planetDestroyer.BeginAttackRun(participants, SelectAttackRun(participants));
        }

        /// <summary>
        /// Selects whether one of the participating squadrons can complete an attack run.
        /// </summary>
        /// <param name="participants">The squadrons available for the run in stable order.</param>
        /// <returns>True when a squadron passes the run selection checks.</returns>
        private bool SelectAttackRun(IReadOnlyList<StarfighterState> participants)
        {
            Queue<AttackRunCandidate> candidates = new Queue<AttackRunCandidate>(
                participants.Select(fighter => new AttackRunCandidate(
                    fighter,
                    fighter.CurrentSquadronSize
                ))
            );
            int maximumEvaluations = Math.Max(_config.AttackRunSelectionMaximumEvaluations, 0);
            for (int pass = 0; pass < maximumEvaluations && candidates.Count > 0; pass++)
            {
                AttackRunCandidate candidate = candidates.Dequeue();
                int craft = candidate.RemainingCraft;
                if (candidate.Fighter.AttackRunTurnBudget < _random.NextInt(1, 11))
                {
                    if (craft <= 1)
                        continue;

                    craft -= _random.NextInt(1, craft + 1);
                    if (craft <= 0)
                        continue;
                }

                if (_random.NextInt(1, 101) <= candidate.Fighter.GetAttackRunSelectionThreshold())
                    return true;

                candidates.Enqueue(new AttackRunCandidate(candidate.Fighter, craft));
            }

            return false;
        }

        /// <summary>
        /// Fires each charged superlaser at the first untargeted enemy capital ship in stable
        /// combat order.
        /// </summary>
        /// <param name="force">The force whose planet destroyers fire.</param>
        /// <param name="opposingForce">The force providing targets.</param>
        private static void FireSuperlasers(CombatForce force, CombatForce opposingForce)
        {
            if (force.WithdrawalOrdered)
                return;

            foreach (PlanetDestroyerState planetDestroyer in force.PlanetDestroyers)
            {
                if (!planetDestroyer.IsSuperlaserReady)
                    continue;

                CapitalShipState target = opposingForce.Ships.FirstOrDefault(ship =>
                    ship.IsTargetable
                    && !force.PlanetDestroyers.Any(other =>
                        ReferenceEquals(other.PendingSuperlaserTarget, ship)
                    )
                );

                if (target != null)
                    planetDestroyer.QueueSuperlaserStrike(target);
            }
        }

        /// <summary>
        /// Resolves pending superlaser strikes and advances each planet destroyer's recharge and
        /// withdrawal state by one iteration.
        /// </summary>
        /// <param name="force">The force whose planet destroyers advance.</param>
        /// <param name="opponent">The side owning any struck ordinary unit.</param>
        private static void AdvancePlanetDestroyers(CombatForce force, CombatForce opponent)
        {
            foreach (PlanetDestroyerState planetDestroyer in force.PlanetDestroyers)
            {
                if (planetDestroyer.IsWithdrawing && planetDestroyer.IsJumpReady)
                {
                    planetDestroyer.FinishWithdrawal();
                    continue;
                }
                TacticalUnit struck = planetDestroyer.Advance();
                if (struck?.IsAlive == true && !struck.HasWithdrawn)
                    opponent.RemoveDestroyedUnit(struck);
            }
        }

        /// <summary>
        /// Advances and resolves the fixed-duration fighter attack runs against one force's
        /// planet destroyers. A successful run destroys the planet destroyer and half of multiple
        /// participating squadrons; a failed run destroys every participant.
        /// </summary>
        /// <param name="planetDestroyerForce">The force whose planet destroyers are attacked.</param>
        /// <param name="opponent">The side owning the participating squadrons.</param>
        private static void ResolveAttackRuns(
            CombatForce planetDestroyerForce,
            CombatForce opponent
        )
        {
            foreach (PlanetDestroyerState planetDestroyer in planetDestroyerForce.PlanetDestroyers)
            {
                IReadOnlyList<StarfighterState> run = planetDestroyer.AttackRunParticipants;
                if (run.Count == 0)
                    continue;

                if (!planetDestroyer.IsAttackable)
                {
                    planetDestroyer.CancelAttackRun();
                    continue;
                }

                if (!planetDestroyer.AdvanceAttackRun())
                    continue;

                bool succeeded = planetDestroyer.AttackRunSucceeded;
                if (succeeded)
                {
                    int losses = run.Count > 1 ? run.Count / 2 : 0;
                    for (int index = 0; index < run.Count; index++)
                    {
                        if (index < losses)
                            opponent.RemoveDestroyedUnit(run[index]);
                        else
                            run[index].EndAttackRun();
                    }
                    planetDestroyer.Destroy();
                }
                else
                {
                    foreach (StarfighterState fighter in run)
                        opponent.RemoveDestroyedUnit(fighter);
                    planetDestroyer.CompleteAttackRun();
                }
            }
        }

        /// <summary>
        /// Determines whether a force has fallen below the configured share of opposing tactical
        /// strength.
        /// </summary>
        /// <param name="force">The force to inspect.</param>
        /// <param name="opposingForce">The force providing the opposing tactical strength.</param>
        /// <returns>True when the withdrawal threshold has been reached.</returns>
        private bool HasReachedWithdrawalThreshold(CombatForce force, CombatForce opposingForce)
        {
            if (force.ForcedWithdrawal)
                return true;
            double own = GetTacticalStrength(force, opposingForce);
            if (own == 0)
                return true;
            double opposing = GetTacticalStrength(opposingForce, force);
            return opposing > 0 && own / opposing < (float)force.RetreatStrengthRatio;
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
        /// Advances temporary damage timers and capital-ship shield and weapon recharge.
        /// </summary>
        /// <param name="force">The force whose tactical state advances.</param>
        private void AdvanceTacticalState(CombatForce force)
        {
            foreach (TacticalUnit unit in force.Units)
            {
                if (!unit.IsAlive || unit.HasWithdrawn)
                    continue;

                unit.AdvanceTacticalState(_config);
            }
        }

        /// <summary>
        /// Calculates strength not suppressed by opposing combat groups.
        /// </summary>
        /// <param name="force">The force being measured.</param>
        /// <param name="opposingForce">The side whose current targets can suppress capital ships.</param>
        /// <returns>The force's remaining tactical strength.</returns>
        private static double GetTacticalStrength(CombatForce force, CombatForce opposingForce)
        {
            float strength = 0;
            foreach (TacticalUnit unit in force.Units)
            {
                if (!unit.IsTargetable && !(force.HasInitiative && unit.IsAlive && unit.IsDocked))
                    continue;
                bool suppressed = false;
                if (!unit.IsStarfighter)
                    foreach (CombatGroup group in opposingForce.Groups)
                        if (
                            !group.IsRetired
                            && ReferenceEquals(group.Target, unit)
                            && unit.ShieldRechargeRate <= group.GetDamageRate(unit)
                        )
                        {
                            suppressed = true;
                            break;
                        }
                if (!suppressed)
                    strength = (float)(strength + unit.GetEffectiveness());
            }
            if (force.HasPlanetDestroyerInBattle)
                strength += 20000000;
            return strength;
        }

        /// <summary>
        /// Creates the authoritative result of the resolved tactical state.
        /// </summary>
        /// <param name="attacker">The resolved attacking force.</param>
        /// <param name="defender">The resolved defending force.</param>
        /// <param name="iterationsCompleted">The number of completed resolver iterations.</param>
        /// <returns>The resolved space-combat result.</returns>
        private static SpaceCombatResult CreateResult(
            CombatForce attacker,
            CombatForce defender,
            int iterationsCompleted
        )
        {
            SpaceCombatResult result = new SpaceCombatResult
            {
                AttackerOutcome = attacker.Outcome,
                DefenderOutcome = defender.Outcome,
                IterationsCompleted = iterationsCompleted,
            };

            AddShipResults(result, attacker.Ships);
            AddShipResults(result, defender.Ships);
            AddPlanetDestroyerResults(result, attacker.PlanetDestroyers);
            AddPlanetDestroyerResults(result, defender.PlanetDestroyers);
            AddFighterResults(result, attacker.Fighters);
            AddFighterResults(result, defender.Fighters);
            result.AttackingUnits.AddRange(CaptureCombatUnits(attacker));
            result.DefendingUnits.AddRange(CaptureCombatUnits(defender));
            result.Winner = DetermineWinner(attacker, defender);
            result.WithdrawnUnits.AddRange(
                attacker
                    .Units.Concat(defender.Units)
                    .Where(unit => unit.HasWithdrawn)
                    .Select(unit => unit.Node)
            );
            result.WithdrawnUnits.AddRange(
                attacker
                    .PlanetDestroyers.Concat(defender.PlanetDestroyers)
                    .Where(planetDestroyer => planetDestroyer.HasWithdrawn)
                    .Select(planetDestroyer => planetDestroyer.Ship)
            );
            return result;
        }

        /// <summary>
        /// Appends committed capital-ship damage to a combat result.
        /// </summary>
        /// <param name="result">The result receiving damage records.</param>
        /// <param name="ships">The resolved capital ships.</param>
        private static void AddShipResults(
            SpaceCombatResult result,
            IReadOnlyList<CapitalShipState> ships
        )
        {
            foreach (CapitalShipState ship in ships)
            {
                int hullAfter = GetCommittedHullStrength(ship);
                if (hullAfter >= ship.InitialHull)
                    continue;

                result.ShipDamage.Add(
                    new ShipDamageResult
                    {
                        Ship = ship.Ship,
                        HullBefore = ship.InitialHull,
                        HullAfter = hullAfter,
                    }
                );
            }
        }

        /// <summary>
        /// Records destroyed planet destroyers in a combat result. A surviving planet destroyer
        /// was never hit because ordinary weapons cannot reach it.
        /// </summary>
        /// <param name="result">The result receiving damage records.</param>
        /// <param name="planetDestroyers">The resolved planet destroyers.</param>
        private static void AddPlanetDestroyerResults(
            SpaceCombatResult result,
            IReadOnlyList<PlanetDestroyerState> planetDestroyers
        )
        {
            foreach (PlanetDestroyerState planetDestroyer in planetDestroyers)
            {
                if (planetDestroyer.IsAlive)
                    continue;

                result.ShipDamage.Add(
                    new ShipDamageResult
                    {
                        Ship = planetDestroyer.Ship,
                        HullBefore = planetDestroyer.Ship.CurrentHullStrength,
                        HullAfter = 0,
                    }
                );
            }
        }

        /// <summary>
        /// Converts simulated capital-ship durability to committed integer hull strength.
        /// </summary>
        /// <param name="ship">The resolved capital ship.</param>
        /// <returns>Zero when destroyed; otherwise at least one hull point.</returns>
        private static int GetCommittedHullStrength(CapitalShipState ship)
        {
            if (!ship.IsAlive)
                return 0;

            return (int)Math.Round(Math.Max(ship.CurrentHull, 1), MidpointRounding.ToEven);
        }

        /// <summary>
        /// Appends committed fighter losses to a combat result.
        /// </summary>
        /// <param name="result">The result receiving fighter-loss records.</param>
        /// <param name="fighters">The resolved fighter squadrons.</param>
        private static void AddFighterResults(
            SpaceCombatResult result,
            IReadOnlyList<StarfighterState> fighters
        )
        {
            foreach (StarfighterState fighter in fighters)
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
        private static List<CombatUnitSnapshot> CaptureCombatUnits(CombatForce force)
        {
            List<CombatUnitSnapshot> units = force
                .Ships.Select(ship => ship.Ship)
                .Concat(force.PlanetDestroyers.Select(planetDestroyer => planetDestroyer.Ship))
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
                force.Units.Where(unit => IsDamaged(unit)).Select(unit => unit.Node),
                force
                    .Units.Where(unit => !unit.IsAlive)
                    .Select(unit => unit.Node)
                    .Concat(
                        force
                            .PlanetDestroyers.Where(planetDestroyer => !planetDestroyer.IsAlive)
                            .Select(planetDestroyer => (ISceneNode)planetDestroyer.Ship)
                    )
            );
            return units;
        }

        /// <summary>
        /// Returns whether a resolved tactical unit sustained damage.
        /// </summary>
        /// <param name="unit">The resolved tactical unit.</param>
        /// <returns>True when the unit lost hull or squadron strength.</returns>
        private static bool IsDamaged(TacticalUnit unit)
        {
            return unit switch
            {
                CapitalShipState ship => ship.CurrentHull < ship.Ship.MaxHullStrength,
                StarfighterState fighter => fighter.CurrentSquadronSize
                    < fighter.InitialSquadronSize,
                _ => false,
            };
        }

        /// <summary>
        /// Determines the winning side from the resolved force states.
        /// </summary>
        /// <param name="attacker">The resolved attacking force.</param>
        /// <param name="defender">The resolved defending force.</param>
        /// <returns>The winning side, or a draw.</returns>
        private static CombatSide DetermineWinner(CombatForce attacker, CombatForce defender)
        {
            bool attackerActive = attacker.Outcome == SpaceCombatSideOutcome.Active;
            bool defenderActive = defender.Outcome == SpaceCombatSideOutcome.Active;
            if (attackerActive != defenderActive)
                return attackerActive ? CombatSide.Attacker : CombatSide.Defender;

            bool attackerSurvived =
                attacker.Units.Any(unit => unit.IsAlive)
                || attacker.PlanetDestroyers.Any(planetDestroyer => planetDestroyer.IsAlive);
            bool defenderSurvived =
                defender.Units.Any(unit => unit.IsAlive)
                || defender.PlanetDestroyers.Any(planetDestroyer => planetDestroyer.IsAlive);
            if (attackerSurvived == defenderSurvived)
                return CombatSide.Draw;

            return attackerSurvived ? CombatSide.Attacker : CombatSide.Defender;
        }

        /// <summary>
        /// Stores one squadron's temporary craft count during attack-run selection.
        /// </summary>
        private readonly struct AttackRunCandidate
        {
            internal StarfighterState Fighter { get; }
            internal int RemainingCraft { get; }

            /// <summary>
            /// Creates a candidate for one attack-run selection pass.
            /// </summary>
            /// <param name="fighter">The candidate squadron.</param>
            /// <param name="remainingCraft">The craft remaining in the selection model.</param>
            internal AttackRunCandidate(StarfighterState fighter, int remainingCraft)
            {
                Fighter = fighter ?? throw new ArgumentNullException(nameof(fighter));
                RemainingCraft = Math.Max(remainingCraft, 0);
            }
        }

        /// <summary>
        /// Represents one side's mutable state during automatic combat.
        /// </summary>
        private sealed class CombatForce
        {
            internal readonly List<CapitalShipState> Ships;
            internal readonly List<StarfighterState> Fighters;
            internal readonly List<TacticalUnit> Units;

            // Planet destroyers stand off from the fight and are not among the units.
            internal readonly List<PlanetDestroyerState> PlanetDestroyers;
            internal readonly List<CombatGroup> Groups = new List<CombatGroup>();
            private readonly IRandomNumberProvider _random;
            private readonly double _capitalCommandBonus;
            private readonly bool _requiresAdmiralCarrier;
            private readonly CapitalShipState _admiralCarrier;
            private readonly PlanetDestroyerState _admiralPlanetDestroyer;
            private readonly List<TacticalUnit> _candidates = new List<TacticalUnit>();
            private readonly List<TacticalUnit> _capitalCandidates = new List<TacticalUnit>();
            private readonly List<TacticalUnit> _fighterCandidates = new List<TacticalUnit>();
            private readonly List<CombatGroup> _capitalGroups = new List<CombatGroup>();
            private readonly List<CombatGroup> _laserGroups = new List<CombatGroup>();
            private readonly List<CombatGroup> _remainingGroups = new List<CombatGroup>();
            private readonly List<(float Score, CombatGroup Group)> _metrics =
                new List<(float, CombatGroup)>();

            internal bool HasInitiative { get; private set; }
            internal bool ForcedWithdrawal { get; set; }
            internal double RetreatStrengthRatio { get; }
            internal double CapitalCommandBonus =>
                !_requiresAdmiralCarrier
                || _admiralCarrier?.IsTargetable == true
                || (
                    _admiralPlanetDestroyer?.IsInBattle == true
                    && !_admiralPlanetDestroyer.IsWithdrawing
                )
                    ? _capitalCommandBonus
                    : 1;

            internal bool HasCombatants => HasTargetableUnits(Units);
            internal bool HasUnresolvedUnits =>
                HasUnresolvedUnit(Units) || HasPlanetDestroyerInBattle;
            internal bool HasPendingWithdrawal =>
                HasWithdrawingUnit(Units)
                || PlanetDestroyers.Any(planetDestroyer =>
                    planetDestroyer.IsAlive && planetDestroyer.IsWithdrawing
                );
            internal bool HasTargetableShips => HasTargetableUnits(Ships);
            internal bool HasTargetableFighters => HasTargetableUnits(Fighters);
            internal bool HasWithdrawnUnits =>
                HasWithdrawnUnit(Units)
                || PlanetDestroyers.Any(planetDestroyer => planetDestroyer.HasWithdrawn);
            internal bool HasPlanetDestroyerInBattle =>
                PlanetDestroyers.Any(planetDestroyer => planetDestroyer.IsInBattle);
            internal bool HasAttackRunUnderWay =>
                Fighters.Any(fighter => fighter.IsAlive && fighter.AttackRunTarget != null);
            internal bool CanWithdraw { get; }
            internal SpaceCombatSideOutcome Outcome { get; set; }
            internal bool WithdrawalOrdered { get; private set; }

            /// <summary>
            /// Creates tactical state for one combat force.
            /// </summary>
            /// <param name="ships">The force's capital ships.</param>
            /// <param name="fighters">The force's fighter squadrons.</param>
            /// <param name="canWithdraw">Whether the force can withdraw.</param>
            /// <param name="config">The automatic combat parameters.</param>
            /// <param name="command">The force's tactical command bonuses.</param>
            /// <param name="shieldedPlanetDestroyers">
            /// The planet destroyers a planetary shield generator protects, or null.
            /// </param>
            /// <param name="random">The battle's random stream.</param>
            /// <param name="retreatStrengthRatio">The side's retreat threshold, or null for the content setting.</param>
            internal CombatForce(
                IReadOnlyList<CapitalShip> ships,
                IReadOnlyList<Starfighter> fighters,
                bool canWithdraw,
                GameConfig.SpaceCombatConfig config,
                SpaceCombatCommandModifiers command,
                IReadOnlyCollection<CapitalShip> shieldedPlanetDestroyers,
                IRandomNumberProvider random,
                double? retreatStrengthRatio
            )
            {
                _random = random;
                RetreatStrengthRatio =
                    retreatStrengthRatio ?? config.AutoResolveRetreatStrengthRatio;
                float scaledLeadership = (float)GetCommandBonus(
                    command.AdmiralLeadership,
                    config.AdmiralLeadershipDivisor
                );
                _capitalCommandBonus =
                    config.AdmiralLeadershipDivisor > 0
                    && (command.AdmiralShip != null || command.AdmiralLeadership > 0)
                        ? Math.Clamp(9.0 - scaledLeadership, 1, 9)
                        : 1;
                _requiresAdmiralCarrier = command.AdmiralShip != null;
                PlanetDestroyers = (ships ?? Array.Empty<CapitalShip>())
                    .Where(ship => ship?.CanDestroyPlanets == true)
                    .Select(ship => new PlanetDestroyerState(
                        ship,
                        shieldedPlanetDestroyers?.Contains(ship) == true,
                        config.PlanetDestroyerSuperlaserRechargeIterations,
                        config.PlanetDestroyerAttackRunIterations,
                        config.PlanetDestroyerWithdrawalIterations
                    ))
                    .ToList();
                Ships = (ships ?? Array.Empty<CapitalShip>())
                    .Where(ship => ship?.CanDestroyPlanets == false)
                    .Select(ship => new CapitalShipState(ship, config, this))
                    .ToList();
                _admiralCarrier = Ships.Find(ship =>
                    ReferenceEquals(ship.Ship, command.AdmiralShip)
                );
                _admiralPlanetDestroyer = PlanetDestroyers.Find(ship =>
                    ReferenceEquals(ship.Ship, command.AdmiralShip)
                );
                bool commanderPresent =
                    command.CommanderShip == null
                    || Ships.Any(ship =>
                        ship.IsAlive && ReferenceEquals(ship.Ship, command.CommanderShip)
                    )
                    || PlanetDestroyers.Any(ship =>
                        ship.IsAlive && ReferenceEquals(ship.Ship, command.CommanderShip)
                    );
                double starfighterCommandBonus = GetCommandBonus(
                    commanderPresent ? command.CommanderCombat : 0,
                    config.CommanderCombatDivisor
                );
                Fighters = (fighters ?? Array.Empty<Starfighter>())
                    .Where(fighter => fighter != null)
                    .Select(fighter => new StarfighterState(
                        fighter,
                        config,
                        starfighterCommandBonus
                    ))
                    .ToList();
                Units = new List<TacticalUnit>(Ships.Count + Fighters.Count);
                Units.AddRange(Ships);
                Units.AddRange(Fighters);
                CanWithdraw = canWithdraw;
                Outcome = SpaceCombatSideOutcome.Active;
            }

            /// <summary>
            /// Scales an officer rating with its configured tactical divisor.
            /// </summary>
            /// <param name="rating">The selected officer's effective rating.</param>
            /// <param name="divisor">The configured tactical divisor.</param>
            /// <returns>The tactical command bonus, or zero for an invalid divisor.</returns>
            private static double GetCommandBonus(int rating, int divisor)
            {
                return divisor > 0 ? (double)Math.Max(rating, 0) / divisor : 0;
            }

            /// <summary>
            /// Creates stable weapon formations and captures the original carrier relationships.
            /// </summary>
            /// <param name="starfighterTypes">The available fighter designs and their formation metadata.</param>
            /// <param name="hasInitiative">Whether this side performs the first update.</param>
            internal void InitializeGroups(
                IReadOnlyList<Starfighter> starfighterTypes,
                bool hasInitiative
            )
            {
                HasInitiative = hasInitiative;
                for (int i = 0; i < Units.Count; i++)
                    Units[i].StableOrder = i;
                Dictionary<CapitalShip, CapitalShipState> carriers = Ships.ToDictionary(ship =>
                    ship.Ship
                );
                foreach (StarfighterState fighter in Fighters)
                {
                    CapitalShip parent = fighter.Fighter.GetParent() as CapitalShip;
                    if (
                        parent != null
                        && carriers.TryGetValue(parent, out CapitalShipState carrier)
                    )
                    {
                        fighter.OriginalCarrier = carrier;
                        if (!fighter.CanWithdrawIndependently)
                        {
                            fighter.DockedCarrier = carrier;
                            carrier.DockedFighters.Add(fighter);
                        }
                    }
                }
                string owner =
                    Units.Count == 0
                        ? string.Empty
                        : Units[0].Node.GetOwnerInstanceID() ?? string.Empty;
                Dictionary<string, Starfighter> designs = new(StringComparer.Ordinal);
                foreach (Starfighter design in starfighterTypes)
                    if (
                        design.FormationPriority.HasValue
                        && design.ManufacturingFactionInstanceIDs?.Contains(owner) == true
                    )
                        designs.TryAdd(design.TypeID ?? string.Empty, design);
                foreach (StarfighterState fighter in Fighters)
                    designs.TryAdd(fighter.Fighter.TypeID ?? string.Empty, fighter.Fighter);
                List<string> typeOrder = designs
                    .OrderBy(entry => entry.Value.GetFormationPriority())
                    .ThenBy(entry => entry.Key, StringComparer.Ordinal)
                    .Select(entry => entry.Key)
                    .ToList();
                while (typeOrder.Count < 4)
                    typeOrder.Add(null);
                CombatGroup[] buckets = new CombatGroup[typeOrder.Count];
                int[] counts = new int[typeOrder.Count];
                for (int i = 0; i < buckets.Length; i++)
                {
                    if (typeOrder[i] == null)
                        continue;
                    List<TacticalUnit> members = Fighters
                        .Where(f => (f.Fighter.TypeID ?? string.Empty) == typeOrder[i])
                        .Cast<TacticalUnit>()
                        .ToList();
                    counts[i] = members.Count;
                    if (members.Count > 0)
                        buckets[i] = AddGroup(members);
                }
                for (int i = 0; i < buckets.Length; i++)
                {
                    if (counts[i] != 0)
                        continue;
                    int largest = 0;
                    for (int j = 1; j < counts.Length; j++)
                        if (counts[j] > counts[largest])
                            largest = j;
                    if (counts[largest] < 8)
                        continue;
                    counts[i] = counts[largest] / 2;
                    counts[largest] -= counts[i];
                    int moved = Math.Min(
                        HasInitiative ? 1 : counts[i],
                        buckets[largest].Members.Count
                    );
                    List<TacticalUnit> members = buckets[largest].Members.GetRange(0, moved);
                    buckets[largest].Members.RemoveRange(0, moved);
                    buckets[i] = AddGroup(members);
                }
                List<TacticalUnit> heavy = Ships
                    .Where(ship => ship.HasWeapons && !ship.HasLasers)
                    .Cast<TacticalUnit>()
                    .ToList();
                List<TacticalUnit> lasers = Ships
                    .Where(ship => ship.HasLasers)
                    .Cast<TacticalUnit>()
                    .ToList();
                List<TacticalUnit> unarmed = Ships
                    .Where(ship => !ship.HasWeapons)
                    .Cast<TacticalUnit>()
                    .ToList();
                int first = AddCapitalGroups(heavy, 0, 3);
                int second = AddCapitalGroups(lasers, first, first + 3);
                AddCapitalGroups(unarmed, first + second, 7);
            }

            /// <summary>
            /// Adds one formation in stable firing order.
            /// </summary>
            /// <param name="members">The formation's combatants.</param>
            /// <returns>The new formation.</returns>
            private CombatGroup AddGroup(List<TacticalUnit> members)
            {
                CombatGroup group = new CombatGroup(Groups.Count, members);
                Groups.Add(group);
                return group;
            }

            /// <summary>
            /// Distributes a range-sorted capital-ship category over bounded formations.
            /// </summary>
            /// <param name="members">The ships in this category.</param>
            /// <param name="lower">The first formation slot.</param>
            /// <param name="upper">The last formation slot.</param>
            /// <returns>The number of created formations.</returns>
            private int AddCapitalGroups(List<TacticalUnit> members, int lower, int upper)
            {
                if (members.Count == 0)
                    return 0;
                members.Sort(
                    (a, b) =>
                    {
                        int score = (
                            (float)(b.MaximumActiveRange * 10000 + b.ClosingSpeed)
                        ).CompareTo((float)(a.MaximumActiveRange * 10000 + a.ClosingSpeed));
                        return score != 0 ? score : a.StableOrder.CompareTo(b.StableOrder);
                    }
                );
                int count = members.Count <= 1 ? 1 : Math.Min((int)Math.Sqrt(members.Count), upper);
                count = Math.Min(count, upper - lower + 1);
                int spacing = members.Count / count;
                int created = 0;
                CombatGroup current = null;
                for (int i = 0; i < members.Count; i++)
                {
                    if (i % spacing == 0 && created < count)
                    {
                        current = AddGroup(new List<TacticalUnit>());
                        created++;
                    }
                    current.Members.Add(members[i]);
                }
                foreach (CombatGroup group in Groups)
                    group.Members.Sort((a, b) => a.StableOrder.CompareTo(b.StableOrder));
                return count;
            }

            /// <summary>
            /// Releases pending squadrons from their launch carriers.
            /// </summary>
            internal void LaunchFighters()
            {
                foreach (StarfighterState fighter in Fighters)
                {
                    fighter.DockedCarrier?.DockedFighters.Remove(fighter);
                    fighter.DockedCarrier = null;
                }
            }

            /// <summary>
            /// Rebuilds candidate preferences while preserving viable existing engagements.
            /// </summary>
            /// <param name="opponent">The opposing side.</param>
            internal void AssignTargets(CombatForce opponent)
            {
                _candidates.Clear();
                foreach (TacticalUnit unit in opponent.Units)
                    if (unit.IsTargetable)
                        _candidates.Add(unit);
                _candidates.Sort(
                    (a, b) =>
                    {
                        int availability = b.IsAvailableTarget.CompareTo(a.IsAvailableTarget);
                        if (availability != 0)
                            return availability;
                        int score = b.TargetPriority.CompareTo(a.TargetPriority);
                        return score != 0 ? score : a.StableOrder.CompareTo(b.StableOrder);
                    }
                );
                if (_candidates.Count == 0)
                    return;
                _capitalCandidates.Clear();
                _fighterCandidates.Clear();
                foreach (TacticalUnit unit in _candidates)
                    if (!unit.IsStarfighter)
                        _capitalCandidates.Add(unit);
                foreach (TacticalUnit unit in _candidates)
                    if (unit.IsStarfighter)
                        _fighterCandidates.Add(unit);
                foreach (TacticalUnit unit in _candidates)
                    if (!unit.IsStarfighter)
                        _fighterCandidates.Add(unit);
                _capitalCandidates.AddRange(_fighterCandidates);
                _capitalGroups.Clear();
                _laserGroups.Clear();
                foreach (CombatGroup group in Groups)
                {
                    TacticalUnit primary = group.PrimaryMember;
                    if (group.IsRetired || group.HasOffFieldMembers || RetainsTarget(group))
                        continue;
                    (primary?.HasLasers == true ? _laserGroups : _capitalGroups).Add(group);
                }
                Regroup(_capitalGroups, _capitalCandidates);
                Regroup(_laserGroups, _fighterCandidates);
            }

            /// <summary>
            /// Checks the combined effort of groups already assigned to the same target.
            /// </summary>
            /// <param name="group">The formation whose target is checked.</param>
            /// <returns>Whether the target can be retained.</returns>
            private bool RetainsTarget(CombatGroup group)
            {
                TacticalUnit target = group.Target;
                if (target?.IsAvailableTarget != true)
                    return false;
                float budget = 0;
                float metric = group.GetTargetMetric(target, ref budget);
                if (metric >= 0 && metric < 300)
                    return true;
                foreach (CombatGroup other in Groups)
                    if (!ReferenceEquals(group, other) && ReferenceEquals(other.Target, target))
                        metric = other.GetTargetMetric(target, ref budget);
                return metric >= 0 && metric < 300;
            }

            /// <summary>
            /// Distributes formations over candidates using their combined damage budgets.
            /// </summary>
            /// <param name="groups">The formations requiring new assignments.</param>
            /// <param name="candidates">Targets in category preference order.</param>
            private void Regroup(List<CombatGroup> groups, List<TacticalUnit> candidates)
            {
                if (groups.Count == 0 || candidates.Count == 0)
                    return;
                _remainingGroups.Clear();
                _remainingGroups.AddRange(groups);
                foreach (TacticalUnit candidate in candidates)
                {
                    if (!candidate.IsAvailableTarget)
                        continue;
                    _metrics.Clear();
                    foreach (CombatGroup group in _remainingGroups)
                    {
                        float budget = 0;
                        float metric = group.GetTargetMetric(candidate, ref budget);
                        float score = metric >= 0 && metric <= 180 ? metric : 100000 - budget;
                        _metrics.Add((score, group));
                    }
                    _metrics.Sort(
                        (a, b) =>
                        {
                            int score = a.Score.CompareTo(b.Score);
                            return score != 0 ? score : a.Group.Order.CompareTo(b.Group.Order);
                        }
                    );
                    if (_metrics.Count == 0)
                        return;
                    if (_metrics.Count == 1)
                    {
                        _metrics[0].Group.Target = candidate;
                        return;
                    }
                    float combined = _metrics[0].Group.GetDamageRate(candidate);
                    for (int index = _metrics.Count - 1; index > 0; index--)
                    {
                        float metric = _metrics[index]
                            .Group.GetTargetMetric(candidate, ref combined);
                        if (metric < 0 || metric > 180)
                            continue;
                        for (int assigned = _metrics.Count - 1; assigned >= index; assigned--)
                        {
                            CombatGroup group = _metrics[assigned].Group;
                            group.Target = candidate;
                            _remainingGroups.Remove(group);
                        }
                        break;
                    }
                }
                TacticalUnit fallback =
                    candidates.Find(unit => unit.IsAvailableTarget) ?? candidates[0];
                foreach (CombatGroup group in _remainingGroups)
                    group.Target = fallback;
            }

            /// <summary>
            /// Removes a lost combatant and any squadrons actually docked aboard it.
            /// </summary>
            /// <param name="unit">The destroyed combatant.</param>
            internal void RemoveDestroyedUnit(TacticalUnit unit)
            {
                if (unit.IsAlive)
                    unit.ApplyDamage(100000, _random);
                unit.Destroy();
                RetireMember(unit);
                if (unit is CapitalShipState ship)
                {
                    foreach (StarfighterState fighter in ship.DockedFighters)
                    {
                        if (fighter.IsAlive)
                            fighter.ApplyDamage(100000, _random);
                        fighter.Destroy();
                        RetireMember(fighter);
                    }
                    ship.DockedFighters.Clear();
                }
            }

            /// <summary>
            /// Removes a departing combatant while preserving the identity of other formations.
            /// </summary>
            /// <param name="unit">The destroyed or withdrawn combatant.</param>
            private void RetireMember(TacticalUnit unit)
            {
                foreach (CombatGroup group in Groups)
                    if (group.Members.Remove(unit) && group.Members.Count == 0)
                        group.IsRetired = true;
            }

            /// <summary>
            /// Destroys every unit still present in a trapped force.
            /// </summary>
            internal void DestroyRemainingUnits()
            {
                foreach (TacticalUnit unit in Units)
                    if (unit.IsAlive && !unit.HasWithdrawn)
                        RemoveDestroyedUnit(unit);
            }

            /// <summary>
            /// Starts withdrawal for every surviving unit in the force.
            /// </summary>
            internal void BeginWithdrawal()
            {
                if (!CanWithdraw || WithdrawalOrdered)
                    return;

                WithdrawalOrdered = true;
                // Squadrons out on an attack run break off and leave with the rest.
                foreach (StarfighterState fighter in Fighters)
                {
                    if (fighter.IsAlive && fighter.AttackRunTarget != null)
                        fighter.AttackRunTarget.CancelAttackRun();
                }

                foreach (TacticalUnit unit in Units.Where(unit => unit.IsTargetable))
                    unit.StartWithdrawal();
                foreach (PlanetDestroyerState planetDestroyer in PlanetDestroyers)
                    planetDestroyer.StartWithdrawal();
            }

            /// <summary>
            /// Resolves ordinary withdrawals and starts any special ship's escape countdown.
            /// </summary>
            internal void CompleteWithdrawal()
            {
                WithdrawalOrdered = true;
                foreach (StarfighterState fighter in Fighters)
                {
                    if (!fighter.IsAlive || fighter.HasWithdrawn)
                        continue;
                    if (fighter.CanWithdrawIndependently)
                    {
                        fighter.FinishWithdrawal();
                        continue;
                    }
                    CapitalShipState carrier = fighter.OriginalCarrier;
                    if (!CanRecover(carrier))
                        carrier = Ships.Find(CanRecover);
                    if (carrier != null)
                    {
                        fighter.DockedCarrier = carrier;
                        carrier.DockedFighters.Add(fighter);
                        fighter.FinishWithdrawal();
                    }
                    else
                        RemoveDestroyedUnit(fighter);
                }
                foreach (CapitalShipState ship in Ships)
                {
                    if (!ship.IsAlive || ship.HasWithdrawn)
                        continue;
                    if (ship.CanWithdrawIndependently)
                        ship.FinishWithdrawal();
                    else
                        RemoveDestroyedUnit(ship);
                }
                foreach (PlanetDestroyerState ship in PlanetDestroyers)
                {
                    if (!ship.IsInBattle)
                        continue;
                    if (ship.CanWithdrawIndependently)
                        ship.StartWithdrawal();
                    else
                        ship.Destroy();
                }
                foreach (TacticalUnit unit in Units)
                    if (unit.HasWithdrawn || !unit.IsAlive)
                        RetireMember(unit);
            }

            /// <summary>
            /// Determines whether a present carrier has room to recover another squadron.
            /// </summary>
            /// <param name="carrier">The potential recovery ship.</param>
            /// <returns>Whether another squadron fits.</returns>
            private static bool CanRecover(CapitalShipState carrier)
            {
                return carrier?.IsAlive == true
                    && !carrier.HasWithdrawn
                    && carrier.DockedFighters.Count < carrier.Ship.StarfighterCapacity;
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
            /// Determines whether a tactical collection contains an unresolved survivor.
            /// </summary>
            /// <param name="units">The units to inspect.</param>
            /// <returns>True when at least one unit is alive and has not withdrawn.</returns>
            private static bool HasUnresolvedUnit(IReadOnlyList<TacticalUnit> units)
            {
                foreach (TacticalUnit unit in units)
                {
                    if (unit.IsAlive && !unit.HasWithdrawn)
                        return true;
                }
                return false;
            }

            /// <summary>
            /// Determines whether a tactical collection is still moving out of combat.
            /// </summary>
            /// <param name="units">The units to inspect.</param>
            /// <returns>True when at least one surviving unit is withdrawing.</returns>
            private static bool HasWithdrawingUnit(IReadOnlyList<TacticalUnit> units)
            {
                foreach (TacticalUnit unit in units)
                {
                    if (unit.IsAlive && unit.IsWithdrawing)
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
        }

        /// <summary>
        /// Owns one weapon formation and its retained target for a battle.
        /// </summary>
        private sealed class CombatGroup
        {
            internal readonly int Order;
            internal readonly List<TacticalUnit> Members;
            internal TacticalUnit Target { get; set; }
            internal bool IsRetired { get; set; }
            internal TacticalUnit PrimaryMember =>
                Members.Find(unit => unit.IsAlive && !unit.HasWithdrawn);
            internal bool HasOffFieldMembers =>
                Members.Any(unit => unit.IsAlive && unit.IsOffField);

            /// <summary>
            /// Captures a formation's stable order and member list.
            /// </summary>
            /// <param name="order">The stable firing order.</param>
            /// <param name="members">The participating combatants.</param>
            internal CombatGroup(int order, List<TacticalUnit> members)
            {
                Order = order;
                Members = members;
            }

            /// <summary>
            /// Adds every active member's output against one target in member order.
            /// </summary>
            /// <param name="target">The potential target.</param>
            /// <returns>The combined damage rate.</returns>
            internal float GetDamageRate(TacticalUnit target)
            {
                float damage = 0;
                foreach (TacticalUnit member in Members)
                    if (member.IsAlive && !member.HasWithdrawn && !member.IsOffField)
                        damage = (float)(damage + member.GetDamageRate(target));
                return damage;
            }

            /// <summary>
            /// Evaluates durability against the accumulated fire committed to a target.
            /// </summary>
            /// <param name="target">The potential target.</param>
            /// <param name="budget">The combined output including previously evaluated formations.</param>
            /// <returns>The estimated effort, or negative one when recharge prevents progress.</returns>
            internal float GetTargetMetric(TacticalUnit target, ref float budget)
            {
                budget = (float)(budget + GetDamageRate(target));
                double net = budget - target.ShieldRechargeRate;
                float durability = (float)(
                    target.ShieldStrength + target.HullStrength * (double)0.65f
                );
                return net > 0 ? (float)(Math.Max(1, durability) / net) : -1;
            }
        }

        /// <summary>
        /// Provides common tactical state and target-specific attack behavior.
        /// </summary>
        private abstract class TacticalUnit
        {
            private readonly double _minimumManeuverRatio;
            private readonly int[] _systemDamage = new int[5];
            protected readonly int InstalledHyperdriveSystems;

            protected int ShieldDamage => _systemDamage[0];
            protected int WeaponDamage => _systemDamage[1];
            protected int EngineDamage => _systemDamage[3];
            protected int HyperdriveDamage => _systemDamage[4];
            internal int StableOrder { get; set; }
            internal abstract double HullStrength { get; }
            internal abstract double HullFraction { get; }
            internal abstract double ShieldRechargeRate { get; }
            internal abstract double ShieldRechargeRatio { get; }
            internal abstract double WeaponRechargeRate { get; }
            internal abstract bool HasLasers { get; }
            internal abstract bool HasWeapons { get; }
            internal abstract double MaximumActiveRange { get; }
            internal virtual bool IsDocked => false;
            internal virtual int HostedUnitCount => 0;
            internal bool IsAvailableTarget => IsTargetable && WeaponRechargeRate != 0;
            internal float TargetPriority
            {
                get
                {
                    float score = (float)(HostedUnitCount + 15 * GetEffectiveness());
                    score = (float)(score - 0.01f * ShieldRechargeRatio);
                    return (float)(score - 0.01f * HullFraction);
                }
            }
            internal abstract ISceneNode Node { get; }
            internal abstract bool IsAlive { get; }
            internal abstract bool CanWithdrawIndependently { get; }
            internal abstract double ManeuverRate { get; }
            internal abstract double ClosingSpeed { get; }
            internal abstract bool IsStarfighter { get; }
            internal abstract double ShieldStrength { get; }
            internal bool HasWithdrawn { get; private set; }
            internal bool IsWithdrawing { get; private set; }

            /// <summary>
            /// Gets whether enemies can still engage this unit.
            /// </summary>
            internal bool IsTargetable => IsAlive && !HasWithdrawn && !IsWithdrawing && !IsOffField;

            /// <summary>
            /// Gets whether the unit has left the fight without leaving the battle, as a squadron
            /// does during a planet-destroyer attack run.
            /// </summary>
            internal virtual bool IsOffField => false;

            /// <summary>
            /// Calculates sustained output against one opposing combatant.
            /// </summary>
            /// <param name="target">The opposing combatant.</param>
            /// <returns>The damage applied per combat step.</returns>
            internal abstract double GetDamageRate(TacticalUnit target);

            /// <summary>
            /// Calculates this unit's remaining tactical strength.
            /// </summary>
            /// <returns>The remaining tactical strength.</returns>
            internal abstract double GetEffectiveness();

            /// <summary>
            /// Applies the combined damage rate for this firing phase.
            /// </summary>
            /// <param name="damage">The incoming damage rate.</param>
            /// <param name="random">The combat random-number stream.</param>
            internal abstract void ApplyDamage(double damage, IRandomNumberProvider random);

            /// <summary>
            /// Advances temporary damage and recharge state.
            /// </summary>
            /// <param name="config">The automatic combat parameters.</param>
            internal void AdvanceTacticalState(GameConfig.SpaceCombatConfig config)
            {
                AdvanceUnitState(config);
            }

            /// <summary>
            /// Advances unit-specific temporary damage and recharge state.
            /// </summary>
            /// <param name="config">The automatic combat parameters.</param>
            protected abstract void AdvanceUnitState(GameConfig.SpaceCombatConfig config);

            /// <summary>
            /// Destroys the tactical unit.
            /// </summary>
            internal abstract void Destroy();

            /// <summary>
            /// Creates tactical state using the configured maneuver floor.
            /// </summary>
            /// <param name="minimumManeuverRatio">The minimum maneuver value and multiplier.</param>
            /// <param name="hyperdriveSystems">The number of installed hyperdrive systems.</param>
            protected TacticalUnit(double minimumManeuverRatio, int hyperdriveSystems)
            {
                _minimumManeuverRatio = minimumManeuverRatio;
                InstalledHyperdriveSystems = hyperdriveSystems;
            }

            /// <summary>
            /// Clears escape flags when a previously recovered unit is destroyed aboard its carrier.
            /// </summary>
            protected void ClearWithdrawal()
            {
                HasWithdrawn = false;
                IsWithdrawing = false;
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
            /// Depletes shields and hull and records the resulting system damage.
            /// </summary>
            /// <param name="hull">The remaining hull strength.</param>
            /// <param name="shields">The remaining shields.</param>
            /// <param name="damage">The incoming damage rate.</param>
            /// <param name="random">The combat random-number stream.</param>
            protected void ApplyDamageRate(
                ref double hull,
                ref double shields,
                double damage,
                IRandomNumberProvider random
            )
            {
                if (hull <= 0)
                    return;
                double originalHull = hull;
                double originalShields = shields;
                shields = (float)(shields - damage);
                if (originalShields > 0)
                {
                    float threshold = (float)Math.Max(100 / originalShields * damage, 1);
                    if (random.NextInt(0, 100) <= threshold)
                        DamageSystem(0);
                }
                if (shields >= 0)
                    return;

                hull = (float)Math.Max(hull - Math.Max(-shields, 1), 0);
                shields = 0;
                double liveScore =
                    (hull - originalHull) * 100 / originalHull + random.NextInt(0, 100);
                float score = (float)liveScore;
                if (liveScore > 50 && score <= 65)
                    DamageSystem(0);
                else if (score > 65 && score <= 80)
                    DamageSystem(1);
                else if (score > 80 && score <= 90)
                    DamageSystem(2);
                else if (score > 90 && score <= 95)
                    DamageSystem(3);
                else if (score > 95 && score <= 100)
                    DamageSystem(4);
            }

            /// <summary>
            /// Records damage to one of the combatant's systems.
            /// </summary>
            /// <param name="system">The damaged system category.</param>
            private void DamageSystem(int system)
            {
                int capacity = system == 4 ? InstalledHyperdriveSystems : 4;
                _systemDamage[system] = Math.Min(_systemDamage[system] + 1, capacity);
            }

            /// <summary>
            /// Calculates the maneuver-rate adjustment used against fighter targets.
            /// </summary>
            /// <param name="target">The target unit.</param>
            /// <returns>The attack multiplier.</returns>
            protected double GetManeuverMultiplier(TacticalUnit target)
            {
                if (!target.IsStarfighter)
                    return 1;

                double targetRate = Math.Max(target.ManeuverRate, _minimumManeuverRatio);
                return Math.Max((double)(float)ManeuverRate / targetRate, _minimumManeuverRatio);
            }
        }

        /// <summary>
        /// Stores a planet destroyer's superlaser, attack-run, and withdrawal state.
        /// </summary>
        private sealed class PlanetDestroyerState
        {
            private readonly List<StarfighterState> _attackRunParticipants =
                new List<StarfighterState>();
            private readonly int _attackRunIterations;
            private bool _attackRunActive;
            private int _attackRunIterationsRemaining;
            private int _jumpIterationsRemaining;
            private readonly int _superlaserRechargeIterations;
            private int _superlaserRechargeIterationsRemaining;
            private readonly int _withdrawalIterations;

            internal CapitalShip Ship { get; }
            internal bool IsShielded { get; }
            internal bool IsAlive { get; private set; } = true;
            internal bool HasWithdrawn { get; private set; }
            internal bool IsWithdrawing { get; private set; }
            internal TacticalUnit PendingSuperlaserTarget { get; private set; }
            internal bool IsInBattle => IsAlive && !HasWithdrawn;
            internal bool IsAttackable => IsInBattle && !IsShielded;
            internal bool IsSuperlaserReady =>
                IsInBattle
                && !IsWithdrawing
                && PendingSuperlaserTarget == null
                && _superlaserRechargeIterationsRemaining <= 0;
            internal bool CanWithdrawIndependently => Ship.Hyperdrive > 0;
            internal bool IsJumpReady => _jumpIterationsRemaining <= 0;
            internal int SuperlaserRechargeIterationsRemaining =>
                _superlaserRechargeIterationsRemaining;
            internal bool HasAttackRun => _attackRunActive;
            internal IReadOnlyList<StarfighterState> AttackRunParticipants =>
                _attackRunParticipants;
            internal bool AttackRunSucceeded { get; private set; }
            internal int WithdrawalProgress =>
                IsWithdrawing ? _withdrawalIterations - _jumpIterationsRemaining : 0;

            /// <summary>
            /// Creates tactical state for one planet destroyer.
            /// </summary>
            /// <param name="ship">The planet destroyer entering combat.</param>
            /// <param name="isShielded">Whether a planetary shield protects the ship.</param>
            /// <param name="superlaserRechargeIterations">The delay between completed strikes.</param>
            /// <param name="attackRunIterations">The duration of a fighter attack run.</param>
            /// <param name="withdrawalIterations">The ship's withdrawal countdown.</param>
            internal PlanetDestroyerState(
                CapitalShip ship,
                bool isShielded,
                int superlaserRechargeIterations,
                int attackRunIterations,
                int withdrawalIterations
            )
            {
                Ship = ship ?? throw new ArgumentNullException(nameof(ship));
                IsShielded = isShielded;
                _superlaserRechargeIterations = superlaserRechargeIterations;
                _attackRunIterations = attackRunIterations;
                _withdrawalIterations = withdrawalIterations;
            }

            /// <summary>
            /// Queues a superlaser target for resolution at the end of the current iteration.
            /// </summary>
            /// <param name="target">The target destroyed when the strike completes.</param>
            internal void QueueSuperlaserStrike(TacticalUnit target)
            {
                PendingSuperlaserTarget = target ?? throw new ArgumentNullException(nameof(target));
            }

            /// <summary>
            /// Resolves a pending superlaser strike and advances the recharge and withdrawal
            /// countdowns by one resolver iteration.
            /// </summary>
            /// <returns>The target whose superlaser strike completed, or null.</returns>
            internal TacticalUnit Advance()
            {
                if (!IsInBattle)
                    return null;

                if (IsWithdrawing && _jumpIterationsRemaining > 0)
                    _jumpIterationsRemaining--;

                if (PendingSuperlaserTarget != null)
                {
                    TacticalUnit struck = PendingSuperlaserTarget;
                    PendingSuperlaserTarget = null;
                    _superlaserRechargeIterationsRemaining = _superlaserRechargeIterations;
                    return struck;
                }

                if (_superlaserRechargeIterationsRemaining > 0)
                    _superlaserRechargeIterationsRemaining--;

                return null;
            }

            /// <summary>
            /// Starts a fixed-duration fighter attack run against the planet destroyer.
            /// </summary>
            /// <param name="participants">The squadrons committed to the run.</param>
            /// <param name="succeeds">Whether the completed run destroys the ship.</param>
            internal void BeginAttackRun(
                IReadOnlyList<StarfighterState> participants,
                bool succeeds
            )
            {
                if (participants == null)
                    throw new ArgumentNullException(nameof(participants));

                _attackRunParticipants.Clear();
                for (int index = 0; index < participants.Count; index++)
                {
                    StarfighterState fighter = participants[index];
                    _attackRunParticipants.Add(fighter);
                    fighter.BeginAttackRun(this);
                }
                _attackRunActive = true;
                AttackRunSucceeded = succeeds;
                _attackRunIterationsRemaining = _attackRunIterations;
            }

            /// <summary>
            /// Advances an active fighter attack run by one resolver iteration.
            /// </summary>
            /// <returns>True when the run completed during this iteration.</returns>
            internal bool AdvanceAttackRun()
            {
                if (!_attackRunActive)
                    return false;

                _attackRunIterationsRemaining--;
                if (_attackRunIterationsRemaining > 0)
                    return false;

                _attackRunActive = false;
                return true;
            }

            /// <summary>
            /// Cancels an active fighter attack run without resolving it.
            /// </summary>
            internal void CancelAttackRun()
            {
                CompleteAttackRun();
                AttackRunSucceeded = false;
            }

            /// <summary>
            /// Clears a completed attack run and returns its surviving participants to combat.
            /// </summary>
            internal void CompleteAttackRun()
            {
                for (int index = 0; index < _attackRunParticipants.Count; index++)
                    _attackRunParticipants[index].EndAttackRun();

                _attackRunParticipants.Clear();
                _attackRunActive = false;
                _attackRunIterationsRemaining = 0;
            }

            /// <summary>
            /// Starts the planet destroyer's hyperspace withdrawal countdown.
            /// </summary>
            internal void StartWithdrawal()
            {
                if (!IsInBattle || IsWithdrawing)
                    return;

                IsWithdrawing = true;
                _jumpIterationsRemaining = _withdrawalIterations;
            }

            /// <summary>
            /// Marks the surviving planet destroyer as withdrawn from combat.
            /// </summary>
            internal void FinishWithdrawal()
            {
                if (!IsAlive)
                    return;

                HasWithdrawn = true;
                IsWithdrawing = false;
                PendingSuperlaserTarget = null;
                CancelAttackRun();
            }

            /// <summary>
            /// Marks the planet destroyer as destroyed.
            /// </summary>
            internal void Destroy()
            {
                IsAlive = false;
                IsWithdrawing = false;
                PendingSuperlaserTarget = null;
                CancelAttackRun();
            }
        }

        /// <summary>
        /// Resolves the installed hyperdrive count independently of strategic travel speed.
        /// </summary>
        /// <param name="typeId">The unit type identifier.</param>
        /// <param name="rating">The primary hyperdrive's travel rating.</param>
        /// <param name="config">The content's hardware definitions.</param>
        /// <returns>The installed system count.</returns>
        private static int GetHyperdriveSystems(
            string typeId,
            int rating,
            GameConfig.SpaceCombatConfig config
        )
        {
            return typeId != null
                && config.HyperdriveSystemCounts != null
                && config.HyperdriveSystemCounts.TryGetValue(typeId, out int count)
                    ? Math.Max(count, 0)
                : rating > 0 ? 1
                : 0;
        }

        /// <summary>
        /// Stores a capital ship's tactical hull, shield, and attack state.
        /// </summary>
        private sealed class CapitalShipState : TacticalUnit
        {
            internal readonly CapitalShip Ship;
            internal readonly int InitialHull;
            internal readonly List<StarfighterState> DockedFighters = new List<StarfighterState>();
            private readonly SpaceCombatDamageCalculator _damage;
            private readonly double _maximumHull;
            private readonly double _maximumShields;
            private readonly CombatForce _force;
            private readonly int _hostedUnitCount;
            private double _hull;
            private double _shields;

            internal double CurrentHull => _hull;
            internal override double HullStrength => _hull;
            internal override double HullFraction => _hull / _maximumHull;
            internal override double ShieldRechargeRate =>
                Math.Max(
                    (float)(Math.Max(Ship.ShieldRechargeRate, 0) * _hull / _maximumHull)
                        - Math.Max(Ship.ShieldRechargeRate, 0) * 0.25 * ShieldDamage,
                    0
                );
            internal override double ShieldRechargeRatio =>
                Ship.ShieldRechargeRate > 0 ? ShieldRechargeRate / Ship.ShieldRechargeRate : 0;
            internal override double WeaponRechargeRate =>
                _damage.GetRechargeRate(HullFraction, WeaponDamage);
            internal override bool HasLasers => _damage.HasLasers;
            internal override bool HasWeapons => _damage.HasWeapons;
            internal override double MaximumActiveRange => _damage.MaximumActiveRange;
            internal override int HostedUnitCount => _hostedUnitCount;
            internal double CurrentShields => _shields;
            internal override double ShieldStrength => _shields;
            internal override ISceneNode Node => Ship;
            internal override bool IsAlive => _hull > 0;
            internal override bool CanWithdrawIndependently =>
                InstalledHyperdriveSystems > HyperdriveDamage && ClosingSpeed > 0;
            internal override bool IsStarfighter => false;
            internal override double ClosingSpeed =>
                Math.Max(
                    (_force.CapitalCommandBonus + Ship.SublightSpeed * (double)0.24f)
                        * (1 - EngineDamage * 0.25),
                    0
                );
            internal override double ManeuverRate =>
                EngineDamage >= 4
                    ? 1
                    : Math.Min(Ship.Maneuverability + _force.CapitalCommandBonus, 9);

            /// <summary>
            /// Captures a capital ship's combat state for one automatic battle.
            /// </summary>
            /// <param name="ship">The ship entering combat.</param>
            /// <param name="config">The combat rules.</param>
            /// <param name="force">The force providing live tactical command presence.</param>
            internal CapitalShipState(
                CapitalShip ship,
                GameConfig.SpaceCombatConfig config,
                CombatForce force
            )
                : base(
                    config.AutoResolveMinimumManeuverRatio,
                    GetHyperdriveSystems(ship.TypeID, ship.Hyperdrive, config)
                )
            {
                Ship = ship;
                InitialHull = Math.Max(ship.CurrentHullStrength, 0);
                _hull = InitialHull;
                _maximumHull = Math.Max(ship.MaxHullStrength, 1);
                _maximumShields = Math.Max(ship.MaxShieldStrength, 0);
                _shields = _maximumShields;
                _force = force;
                _hostedUnitCount = ship.GetChildren()
                    .Count(child =>
                        child switch
                        {
                            Officer officer => officer.Movement == null,
                            Regiment regiment => regiment.Movement == null
                                && regiment.ManufacturingStatus == ManufacturingStatus.Complete,
                            SpecialForces forces => forces.Movement == null
                                && forces.ManufacturingStatus == ManufacturingStatus.Complete,
                            _ => false,
                        }
                    );
                _damage = new SpaceCombatDamageCalculator(ship, config);
            }

            /// <summary>
            /// Calculates sustained output against one target.
            /// </summary>
            /// <param name="target">The opposing combatant.</param>
            /// <returns>The damage applied per step.</returns>
            internal override double GetDamageRate(TacticalUnit target)
            {
                return _damage.GetDamageRate(
                    _hull / _maximumHull,
                    target.IsStarfighter,
                    GetManeuverMultiplier(target),
                    target.ShieldStrength > 0,
                    WeaponDamage
                );
            }

            /// <summary>
            /// Calculates unopposed sustained output against shielded capital ships.
            /// </summary>
            /// <returns>The remaining combat strength.</returns>
            internal override double GetEffectiveness()
            {
                return _damage.GetDamageRate(_hull / _maximumHull, false, 1, true, WeaponDamage);
            }

            /// <summary>
            /// Applies incoming damage to the ship's shields and hull.
            /// </summary>
            /// <param name="damage">The combined damage rate.</param>
            /// <param name="random">The combat random-number stream.</param>
            internal override void ApplyDamage(double damage, IRandomNumberProvider random)
            {
                ApplyDamageRate(ref _hull, ref _shields, damage, random);
            }

            /// <summary>
            /// Recharges the ship's shields at its remaining system capacity.
            /// </summary>
            /// <param name="config">The combat rules.</param>
            protected override void AdvanceUnitState(GameConfig.SpaceCombatConfig config)
            {
                _shields = (float)Math.Min(_maximumShields, _shields + ShieldRechargeRate);
            }

            /// <summary>
            /// Removes the ship's remaining durability.
            /// </summary>
            internal override void Destroy()
            {
                ClearWithdrawal();
                _hull = 0;
                _shields = 0;
            }
        }

        /// <summary>
        /// Stores a fighter squadron's tactical durability and attack state.
        /// </summary>
        private sealed class StarfighterState : TacticalUnit
        {
            internal readonly Starfighter Fighter;
            internal readonly int InitialSquadronSize;
            private readonly SpaceCombatDamageCalculator _damage;
            private readonly double _hullPerCraft;
            private readonly float _shieldRechargePerCraft;
            private readonly double _commandBonus;
            private readonly double _attackRunSelectionThresholdMultiplier;
            private readonly double _attackRunTorpedoBudgetMultiplier;
            private double _hull;
            private double _shields;
            private double _shieldCapacity;

            internal CapitalShipState OriginalCarrier { get; set; }
            internal CapitalShipState DockedCarrier { get; set; }
            internal override bool IsDocked => DockedCarrier != null;
            internal override double HullStrength => _hull;
            internal override double HullFraction =>
                _hull / (_hullPerCraft * Math.Max(Fighter.MaxSquadronSize, 1));
            internal override double ShieldRechargeRate =>
                _hull / _hullPerCraft * _shieldRechargePerCraft;
            internal override double ShieldRechargeRatio => ShieldRechargeRate > 0 ? 1 : 0;
            internal override double WeaponRechargeRate => _damage.GetRechargeRate(HullFraction);
            internal override bool HasLasers => _damage.HasLasers;
            internal override bool HasWeapons => _damage.HasWeapons;
            internal override double MaximumActiveRange => _damage.MaximumActiveRange;
            internal int CurrentSquadronSize =>
                IsAlive
                    ? Math.Min(InitialSquadronSize, Math.Max((int)(_hull / _hullPerCraft), 1))
                    : 0;
            internal override ISceneNode Node => Fighter;
            internal override bool IsAlive => _hull > 0;
            internal override double ShieldStrength => _shields;
            internal override bool CanWithdrawIndependently =>
                InstalledHyperdriveSystems > HyperdriveDamage && ClosingSpeed > 0;
            internal override bool IsStarfighter => true;
            internal override double ClosingSpeed =>
                Math.Max(
                    (Math.Clamp(_commandBonus + 1, 1, 9) + Fighter.SublightSpeed * (double)0.4f)
                        * (1 - EngineDamage * 0.25),
                    0
                );
            internal override double ManeuverRate =>
                EngineDamage >= 4 ? 1 : Math.Min(Fighter.Agility + 1, 9);
            internal override bool IsOffField => AttackRunTarget != null || IsDocked;
            internal bool HasTorpedoes => Fighter.Torpedoes > 0;
            internal int AttackRunTurnBudget => (int)ManeuverRate;
            internal PlanetDestroyerState AttackRunTarget { get; private set; }

            /// <summary>
            /// Captures one squadron's combat state for an automatic battle.
            /// </summary>
            /// <param name="fighter">The squadron entering combat.</param>
            /// <param name="config">The combat rules.</param>
            /// <param name="commandBonus">The Commander's attack-run bonus.</param>
            internal StarfighterState(
                Starfighter fighter,
                GameConfig.SpaceCombatConfig config,
                double commandBonus
            )
                : base(
                    config.AutoResolveMinimumManeuverRatio,
                    GetHyperdriveSystems(fighter.TypeID, fighter.Hyperdrive, config)
                )
            {
                Fighter = fighter;
                InitialSquadronSize = Math.Max(fighter.CurrentSquadronSize, 0);
                _hullPerCraft = (float)Math.Max(config.AutoResolveFighterHullPerCraft, 1);
                _shieldRechargePerCraft = (float)
                    Math.Max(config.AutoResolveFighterShieldRechargePerCraft, 0);
                _hull = InitialSquadronSize * _hullPerCraft;
                _shieldCapacity =
                    (double)Math.Max(fighter.ShieldStrength, 0)
                    * Math.Max(fighter.MaxSquadronSize, 0);
                _shields = _shieldCapacity;
                _commandBonus = Math.Max(commandBonus, 0);
                _attackRunSelectionThresholdMultiplier = Math.Max(
                    config.AttackRunSelectionThresholdMultiplier,
                    0
                );
                _attackRunTorpedoBudgetMultiplier = Math.Max(
                    config.AttackRunTorpedoBudgetMultiplier,
                    0
                );
                _damage = new SpaceCombatDamageCalculator(fighter, config);
            }

            /// <summary>
            /// Calculates this squadron's attack-run selection threshold.
            /// </summary>
            /// <returns>The inclusive one-to-one-hundred threshold.</returns>
            internal int GetAttackRunSelectionThreshold()
            {
                double sideBudget = Math.Min(Math.Max(_commandBonus + 1, 1), 9);
                double torpedoBudget =
                    Math.Max(Fighter.Torpedoes, 0)
                    * Math.Max(Fighter.MaxSquadronSize, 0)
                    * _attackRunTorpedoBudgetMultiplier;
                return (int)((sideBudget + torpedoBudget) * _attackRunSelectionThresholdMultiplier);
            }

            /// <summary>
            /// Places this squadron on a planet-destroyer attack run.
            /// </summary>
            /// <param name="planetDestroyer">The attack-run target.</param>
            internal void BeginAttackRun(PlanetDestroyerState planetDestroyer)
            {
                AttackRunTarget = planetDestroyer;
            }

            /// <summary>
            /// Returns this squadron to ordinary combat.
            /// </summary>
            internal void EndAttackRun()
            {
                AttackRunTarget = null;
            }

            /// <summary>
            /// Calculates sustained output against an opposing combatant.
            /// </summary>
            /// <param name="target">The opposing combatant.</param>
            /// <returns>The damage applied per step.</returns>
            internal override double GetDamageRate(TacticalUnit target)
            {
                return _damage.GetDamageRate(
                    HullFraction,
                    target.IsStarfighter,
                    GetManeuverMultiplier(target),
                    target.ShieldStrength > 0
                );
            }

            /// <summary>
            /// Calculates unopposed sustained output against shielded capital ships.
            /// </summary>
            /// <returns>The remaining combat strength.</returns>
            internal override double GetEffectiveness()
            {
                return _damage.GetDamageRate(HullFraction, false, 1, true);
            }

            /// <summary>
            /// Applies incoming damage to squadron shields and craft.
            /// </summary>
            /// <param name="damage">The combined damage rate.</param>
            /// <param name="random">The combat random-number stream.</param>
            internal override void ApplyDamage(double damage, IRandomNumberProvider random)
            {
                ApplyDamageRate(ref _hull, ref _shields, damage, random);
            }

            /// <summary>
            /// Adjusts surviving shield capacity and recharges the remaining craft.
            /// </summary>
            /// <param name="config">The combat rules.</param>
            protected override void AdvanceUnitState(GameConfig.SpaceCombatConfig config)
            {
                double liveCapacity =
                    _hull
                    * _shieldCapacity
                    / (_hullPerCraft * Math.Max(Fighter.MaxSquadronSize, 1));
                _shieldCapacity = (float)liveCapacity;
                _shields = (float)Math.Min(_shields, liveCapacity);
                _shields = (float)Math.Min(_shieldCapacity, _shields + ShieldRechargeRate);
            }

            /// <summary>
            /// Removes the squadron's remaining durability.
            /// </summary>
            internal override void Destroy()
            {
                ClearWithdrawal();
                _hull = 0;
                _shields = 0;
            }
        }
    }
}
