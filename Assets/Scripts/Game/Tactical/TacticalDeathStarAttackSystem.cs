using System;
using System.Collections.Generic;
using System.Linq;

namespace Rebellion.Game.Tactical
{
    /// <summary>
    /// Advances the timed fighter attack sequence used against a Death Star.
    /// </summary>
    internal sealed class TacticalDeathStarAttackSystem
    {
        internal const float RunDuration = 3f;
        internal const float ReportInterval = 0.3f;
        internal const int ReportCount = 9;
        private static readonly int[] _cleanSuccessReports = { 0, 1, 2, 10, 3, 4, 11, -1, -1 };
        private static readonly int[] _damagedSuccessReports = { 0, 5, 6, 7, 12, 13, 8, -1, -1 };
        private static readonly int[] _failureReports = { 0, 5, 6, 7, 12, 13, 8, 14, 9 };

        private readonly TacticalDeathStarAttackResolver resolver;
        private readonly IReadOnlyDictionary<TacticalBattleSide, float> fighterCommandBudgets;
        private readonly Dictionary<TacticalShipGroup, AttackRun> runs =
            new Dictionary<TacticalShipGroup, AttackRun>();
        private readonly HashSet<TacticalShipGroup> completedGroups =
            new HashSet<TacticalShipGroup>();
        private readonly List<TacticalCombatEvent> events = new List<TacticalCombatEvent>();

        /// <summary>
        /// Tracks one resolved attack outcome while its timed reports play out.
        /// </summary>
        private sealed class AttackRun
        {
            /// <summary>Gets the fighter used as the source of attack-run events.</summary>
            public TacticalUnitState Leader { get; }

            /// <summary>Gets the Death Star targeted by the run.</summary>
            public TacticalUnitState DeathStar { get; }

            /// <summary>Gets the outcome resolved when the run began.</summary>
            public TacticalDeathStarAttackResult Result { get; }

            /// <summary>Gets the ordered chatter indexes for the resolved outcome.</summary>
            public IReadOnlyList<int> Reports { get; }

            /// <summary>Gets or sets the tactical time spent in the run.</summary>
            public float ElapsedTime { get; set; }

            /// <summary>Gets or sets the number of report checkpoints already processed.</summary>
            public int ReportsEmitted { get; set; }

            /// <summary>
            /// Initializes the timed presentation for one resolved run.
            /// </summary>
            /// <param name="leader">The fighter used as the event source.</param>
            /// <param name="deathStar">The targeted Death Star.</param>
            /// <param name="result">The pre-resolved combat outcome.</param>
            public AttackRun(
                TacticalUnitState leader,
                TacticalUnitState deathStar,
                TacticalDeathStarAttackResult result
            )
            {
                Leader = leader;
                DeathStar = deathStar;
                Result = result;
                Reports = result.Succeeded
                    ? result.TookApproachDamage
                        ? _damagedSuccessReports
                        : _cleanSuccessReports
                    : _failureReports;
            }
        }

        /// <summary>
        /// Initializes the attack-run system with deterministic combat resolution inputs.
        /// </summary>
        /// <param name="resolver">The fighter-run combat resolver.</param>
        /// <param name="fighterCommandBudgets">The fighter-command contribution for each side.</param>
        public TacticalDeathStarAttackSystem(
            TacticalDeathStarAttackResolver resolver,
            IReadOnlyDictionary<TacticalBattleSide, float> fighterCommandBudgets
        )
        {
            this.resolver = resolver ?? throw new ArgumentNullException(nameof(resolver));
            this.fighterCommandBudgets =
                fighterCommandBudgets
                ?? throw new ArgumentNullException(nameof(fighterCommandBudgets));
        }

        /// <summary>
        /// Gets whether a group has begun or completed its Death Star attack run.
        /// </summary>
        /// <param name="group">The fighter group to inspect.</param>
        /// <returns>True when the group cannot begin another run.</returns>
        public bool IsCommitted(TacticalShipGroup group)
        {
            return group != null && (runs.ContainsKey(group) || completedGroups.Contains(group));
        }

        /// <summary>
        /// Begins one attack run and resolves the participating fighters' approach losses.
        /// </summary>
        /// <param name="group">The fighter group beginning the run.</param>
        /// <param name="deathStar">The opposing Death Star.</param>
        /// <returns>True when a new run begins.</returns>
        public bool TryBegin(TacticalShipGroup group, TacticalUnitState deathStar)
        {
            if (group == null)
                throw new ArgumentNullException(nameof(group));
            if (deathStar == null)
                throw new ArgumentNullException(nameof(deathStar));
            if (
                runs.Count > 0
                || IsCommitted(group)
                || !deathStar.IsActive
                || deathStar.Side == group.Side
            )
                return false;

            TacticalUnitState[] participants = group
                .Units.Where(unit => unit is { IsActive: true, Kind: TacticalUnitKind.Fighters })
                .ToArray();
            TacticalUnitState leader = participants.FirstOrDefault();
            if (leader == null)
                return false;

            HashSet<TacticalUnitState> activeBefore = participants.ToHashSet();
            TacticalDeathStarAttackResult result = resolver.Resolve(
                participants,
                fighterCommandBudgets.TryGetValue(group.Side, out float commandBudget)
                    ? commandBudget
                    : 1f
            );
            foreach (TacticalUnitState participant in activeBefore.Where(unit => !unit.IsActive))
                events.Add(TacticalCombatEvent.UnitDestroyed(deathStar, participant));

            runs.Add(group, new AttackRun(leader, deathStar, result));
            events.Add(
                TacticalCombatEvent.DeathStarAttackPhase(
                    TacticalCombatEventKind.DeathStarAttackStarted,
                    leader,
                    deathStar
                )
            );
            return true;
        }

        /// <summary>
        /// Advances every active run through reports and final resolution.
        /// </summary>
        /// <param name="elapsedTime">The elapsed tactical time.</param>
        public void Advance(float elapsedTime)
        {
            if (elapsedTime < 0f)
                throw new ArgumentOutOfRangeException(nameof(elapsedTime));

            foreach (KeyValuePair<TacticalShipGroup, AttackRun> entry in runs.ToArray())
            {
                TacticalShipGroup group = entry.Key;
                AttackRun run = entry.Value;
                if (group.Behavior != TacticalBehavior.AttackDeathStar || !run.DeathStar.IsActive)
                {
                    Complete(group, run, TacticalCombatEventKind.DeathStarAttackBrokenOff);
                    continue;
                }

                run.ElapsedTime += elapsedTime;
                while (
                    run.ReportsEmitted < ReportCount
                    && run.ElapsedTime >= run.ReportsEmitted * ReportInterval
                )
                {
                    int reportIndex = run.Reports[run.ReportsEmitted];
                    if (reportIndex >= 0)
                    {
                        events.Add(
                            TacticalCombatEvent.DeathStarAttackReport(
                                run.Leader,
                                run.DeathStar,
                                reportIndex
                            )
                        );
                    }
                    run.ReportsEmitted++;
                }

                if (run.ElapsedTime < RunDuration)
                    continue;

                TacticalCombatEventKind outcome = run.Result.Succeeded
                    ? TacticalCombatEventKind.DeathStarAttackSucceeded
                    : TacticalCombatEventKind.DeathStarAttackFailed;
                if (run.Result.Succeeded)
                    run.DeathStar.Hull = 0;
                Complete(group, run, outcome);
                if (run.Result.Succeeded)
                    events.Add(TacticalCombatEvent.UnitDestroyed(run.Leader, run.DeathStar));
            }
        }

        /// <summary>
        /// Returns accumulated attack-run events and clears the pending collection.
        /// </summary>
        /// <returns>The events produced since the previous drain.</returns>
        public IReadOnlyList<TacticalCombatEvent> DrainEvents()
        {
            TacticalCombatEvent[] result = events.ToArray();
            events.Clear();
            return result;
        }

        private void Complete(
            TacticalShipGroup group,
            AttackRun run,
            TacticalCombatEventKind outcome
        )
        {
            group.SetBehavior(TacticalBehavior.None);
            events.Add(
                TacticalCombatEvent.DeathStarAttackPhase(outcome, run.Leader, run.DeathStar)
            );
            runs.Remove(group);
            completedGroups.Add(group);
        }
    }
}
