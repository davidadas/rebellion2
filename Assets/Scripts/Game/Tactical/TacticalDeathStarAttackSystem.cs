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
        /// Captures active report sequences and groups that already committed to an attack.
        /// </summary>
        /// <param name="groups">All tactical command groups in stable battle order.</param>
        /// <returns>The resumable Death Star attack state.</returns>
        internal TacticalDeathStarAttackSnapshot CaptureState(
            IReadOnlyList<TacticalShipGroup> groups
        )
        {
            if (groups == null)
                throw new ArgumentNullException(nameof(groups));

            Dictionary<TacticalShipGroup, int> groupIndexes = groups
                .Select((group, index) => new { group, index })
                .ToDictionary(entry => entry.group, entry => entry.index);
            return new TacticalDeathStarAttackSnapshot
            {
                ActiveRuns = runs.Select(entry => new TacticalDeathStarAttackRunSnapshot
                    {
                        GroupIndex = ResolveGroupIndex(entry.Key, groupIndexes),
                        LeaderInstanceID = entry.Value.Leader.Unit.GetInstanceID(),
                        DeathStarInstanceID = entry.Value.DeathStar.Unit.GetInstanceID(),
                        Succeeded = entry.Value.Result.Succeeded,
                        TookApproachDamage = entry.Value.Result.TookApproachDamage,
                        CompletionCasualtyInstanceIDs = entry
                            .Value.Result.CompletionCasualties.Select(unit =>
                                unit.Unit.GetInstanceID()
                            )
                            .ToList(),
                        ElapsedTime = entry.Value.ElapsedTime,
                        ReportsEmitted = entry.Value.ReportsEmitted,
                    })
                    .ToList(),
                CompletedGroupIndexes = completedGroups
                    .Select(group => ResolveGroupIndex(group, groupIndexes))
                    .ToList(),
            };
        }

        /// <summary>
        /// Restores active report sequences and groups that already committed to an attack.
        /// </summary>
        /// <param name="snapshot">The saved Death Star attack state.</param>
        /// <param name="groups">All tactical command groups in stable battle order.</param>
        /// <param name="unitsById">All participating tactical units indexed by identifier.</param>
        internal void RestoreState(
            TacticalDeathStarAttackSnapshot snapshot,
            IReadOnlyList<TacticalShipGroup> groups,
            IReadOnlyDictionary<string, TacticalUnitState> unitsById
        )
        {
            if (snapshot == null)
                throw new ArgumentNullException(nameof(snapshot));
            if (groups == null)
                throw new ArgumentNullException(nameof(groups));
            if (unitsById == null)
                throw new ArgumentNullException(nameof(unitsById));
            if (snapshot.ActiveRuns == null || snapshot.CompletedGroupIndexes == null)
                throw new ArgumentException(
                    "The Death Star attack snapshot is incomplete.",
                    nameof(snapshot)
                );

            runs.Clear();
            completedGroups.Clear();
            foreach (TacticalDeathStarAttackRunSnapshot runSnapshot in snapshot.ActiveRuns)
            {
                TacticalShipGroup group = ResolveGroup(runSnapshot.GroupIndex, groups);
                if (runs.ContainsKey(group) || runSnapshot.CompletionCasualtyInstanceIDs == null)
                {
                    throw new ArgumentException(
                        "The Death Star attack snapshot contains a duplicate or incomplete run.",
                        nameof(snapshot)
                    );
                }

                TacticalDeathStarAttackResult result = new TacticalDeathStarAttackResult(
                    runSnapshot.Succeeded,
                    runSnapshot.TookApproachDamage,
                    runSnapshot
                        .CompletionCasualtyInstanceIDs.Select(unitInstanceID =>
                            ResolveUnit(unitInstanceID, unitsById)
                        )
                        .ToArray()
                );
                AttackRun run = new AttackRun(
                    ResolveUnit(runSnapshot.LeaderInstanceID, unitsById),
                    ResolveUnit(runSnapshot.DeathStarInstanceID, unitsById),
                    result
                )
                {
                    ElapsedTime = Math.Max(0f, runSnapshot.ElapsedTime),
                    ReportsEmitted = Math.Clamp(runSnapshot.ReportsEmitted, 0, ReportCount),
                };
                runs.Add(group, run);
            }

            foreach (int groupIndex in snapshot.CompletedGroupIndexes)
            {
                TacticalShipGroup group = ResolveGroup(groupIndex, groups);
                if (!completedGroups.Add(group) || runs.ContainsKey(group))
                {
                    throw new ArgumentException(
                        "The Death Star attack snapshot contains a duplicate committed group.",
                        nameof(snapshot)
                    );
                }
            }
        }

        /// <summary>
        /// Resolves a tactical group to its persisted stable index.
        /// </summary>
        /// <param name="group">The group to resolve.</param>
        /// <param name="groupIndexes">All tactical groups indexed by reference.</param>
        /// <returns>The group's stable battle index.</returns>
        private static int ResolveGroupIndex(
            TacticalShipGroup group,
            IReadOnlyDictionary<TacticalShipGroup, int> groupIndexes
        )
        {
            if (!groupIndexes.TryGetValue(group, out int groupIndex))
                throw new InvalidOperationException(
                    "A Death Star attack group is not part of the battle."
                );

            return groupIndex;
        }

        /// <summary>
        /// Resolves one saved group index to its tactical command group.
        /// </summary>
        /// <param name="groupIndex">The saved group index.</param>
        /// <param name="groups">All tactical groups in stable battle order.</param>
        /// <returns>The matching tactical command group.</returns>
        private static TacticalShipGroup ResolveGroup(
            int groupIndex,
            IReadOnlyList<TacticalShipGroup> groups
        )
        {
            if (groupIndex < 0 || groupIndex >= groups.Count)
                throw new ArgumentException(
                    "A Death Star attack group index is invalid.",
                    nameof(groupIndex)
                );

            return groups[groupIndex];
        }

        /// <summary>
        /// Resolves one strategic unit identifier to its tactical state.
        /// </summary>
        /// <param name="unitInstanceID">The strategic unit identifier.</param>
        /// <param name="unitsById">All participating tactical units indexed by identifier.</param>
        /// <returns>The matching tactical unit.</returns>
        private static TacticalUnitState ResolveUnit(
            string unitInstanceID,
            IReadOnlyDictionary<string, TacticalUnitState> unitsById
        )
        {
            if (
                string.IsNullOrEmpty(unitInstanceID)
                || !unitsById.TryGetValue(unitInstanceID, out TacticalUnitState unit)
            )
            {
                throw new ArgumentException(
                    $"Tactical unit '{unitInstanceID}' is not part of this battle.",
                    nameof(unitInstanceID)
                );
            }

            return unit;
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
                foreach (
                    TacticalUnitState casualty in run.Result.CompletionCasualties.Where(unit =>
                        unit.IsActive
                    )
                )
                {
                    casualty.Hull = 0;
                    events.Add(TacticalCombatEvent.UnitDestroyed(run.DeathStar, casualty));
                }
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
