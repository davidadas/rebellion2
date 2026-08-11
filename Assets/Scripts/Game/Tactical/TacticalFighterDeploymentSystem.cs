using System;
using System.Collections.Generic;
using System.Linq;
using Rebellion.Util.Common;

namespace Rebellion.Game.Tactical
{
    /// <summary>
    /// Staggers carrier-held fighter squadrons into tactical space.
    /// </summary>
    internal sealed class TacticalFighterDeploymentSystem
    {
        private const float _baseLaunchDelay = 5f;
        private const int _maximumLaunchDelay = 8;
        private readonly List<TacticalCombatEvent> events = new List<TacticalCombatEvent>();
        private readonly Dictionary<TacticalUnitState, Queue<TacticalUnitState>> launchQueues;
        private readonly Dictionary<TacticalUnitState, float> nextLaunchTimes =
            new Dictionary<TacticalUnitState, float>();
        private readonly IRandomNumberProvider random;
        private float elapsedTime;

        /// <summary>
        /// Initializes launch queues for every carrier with held fighter squadrons.
        /// </summary>
        /// <param name="units">All units participating in the tactical battle.</param>
        /// <param name="random">The deterministic tactical random source.</param>
        internal TacticalFighterDeploymentSystem(
            IReadOnlyList<TacticalUnitState> units,
            IRandomNumberProvider random,
            bool initializeLaunchQueues = true
        )
        {
            if (units == null)
                throw new ArgumentNullException(nameof(units));
            this.random = random ?? throw new ArgumentNullException(nameof(random));

            launchQueues = initializeLaunchQueues
                ? units
                    .Where(unit => !unit.IsDeployed && unit.RecoveryTarget != null)
                    .GroupBy(unit => unit.RecoveryTarget)
                    .ToDictionary(group => group.Key, group => new Queue<TacticalUnitState>(group))
                : new Dictionary<TacticalUnitState, Queue<TacticalUnitState>>();
            if (initializeLaunchQueues)
            {
                foreach (TacticalUnitState carrier in launchQueues.Keys)
                    ScheduleNextLaunch(carrier);
            }
        }

        /// <summary>
        /// Captures elapsed launch timing and each carrier's remaining fighter order.
        /// </summary>
        /// <returns>The resumable fighter-deployment state.</returns>
        internal TacticalFighterDeploymentSnapshot CaptureState()
        {
            return new TacticalFighterDeploymentSnapshot
            {
                ElapsedTime = elapsedTime,
                LaunchQueues = launchQueues
                    .Select(entry => new TacticalFighterLaunchQueueSnapshot
                    {
                        CarrierInstanceID = entry.Key.Unit.GetInstanceID(),
                        FighterInstanceIDs = entry
                            .Value.Select(fighter => fighter.Unit.GetInstanceID())
                            .ToList(),
                        NextLaunchTime = nextLaunchTimes[entry.Key],
                    })
                    .ToList(),
            };
        }

        /// <summary>
        /// Restores elapsed launch timing and carrier queues without consuming random values.
        /// </summary>
        /// <param name="snapshot">The saved fighter-deployment state.</param>
        /// <param name="unitsById">All participating tactical units indexed by identifier.</param>
        internal void RestoreState(
            TacticalFighterDeploymentSnapshot snapshot,
            IReadOnlyDictionary<string, TacticalUnitState> unitsById
        )
        {
            if (snapshot == null)
                throw new ArgumentNullException(nameof(snapshot));
            if (unitsById == null)
                throw new ArgumentNullException(nameof(unitsById));
            if (snapshot.LaunchQueues == null)
                throw new ArgumentException(
                    "The fighter launch queues are missing.",
                    nameof(snapshot)
                );

            elapsedTime = snapshot.ElapsedTime;
            launchQueues.Clear();
            nextLaunchTimes.Clear();
            foreach (TacticalFighterLaunchQueueSnapshot queueSnapshot in snapshot.LaunchQueues)
            {
                TacticalUnitState carrier = ResolveUnit(queueSnapshot.CarrierInstanceID, unitsById);
                if (queueSnapshot.FighterInstanceIDs == null)
                {
                    throw new ArgumentException(
                        "A fighter launch queue has no fighter order.",
                        nameof(snapshot)
                    );
                }

                Queue<TacticalUnitState> queue = new Queue<TacticalUnitState>(
                    queueSnapshot.FighterInstanceIDs.Select(fighterInstanceID =>
                        ResolveUnit(fighterInstanceID, unitsById)
                    )
                );
                if (queue.Count == 0 || launchQueues.ContainsKey(carrier))
                {
                    throw new ArgumentException(
                        "A fighter launch queue is empty or duplicated.",
                        nameof(snapshot)
                    );
                }

                launchQueues.Add(carrier, queue);
                nextLaunchTimes.Add(carrier, queueSnapshot.NextLaunchTime);
            }
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
        /// Advances carrier launch queues and deploys each due fighter squadron in order.
        /// </summary>
        /// <param name="deltaTime">The elapsed tactical time.</param>
        internal void Advance(float deltaTime)
        {
            if (deltaTime < 0f)
                throw new ArgumentOutOfRangeException(nameof(deltaTime));

            elapsedTime += deltaTime;
            ResolveCarrierStateChanges();
            foreach (TacticalUnitState carrier in launchQueues.Keys.ToArray())
            {
                Queue<TacticalUnitState> queue = launchQueues[carrier];
                if (!carrier.IsActive || elapsedTime < nextLaunchTimes[carrier])
                    continue;

                TacticalUnitState fighters = queue.Dequeue();
                fighters.Deploy(carrier.Position);
                events.Add(
                    TacticalCombatEvent.UnitLifecycle(
                        TacticalCombatEventKind.FightersDeployed,
                        fighters
                    )
                );
                if (queue.Count == 0)
                    RemoveQueue(carrier);
                else
                    ScheduleNextLaunch(carrier);
            }
        }

        /// <summary>
        /// Resolves held squadrons immediately when their carrier is destroyed or withdraws.
        /// </summary>
        internal void ResolveCarrierStateChanges()
        {
            foreach (TacticalUnitState carrier in launchQueues.Keys.ToArray())
            {
                Queue<TacticalUnitState> queue = launchQueues[carrier];
                if (carrier.Hull <= 0)
                    DestroyHeldFighters(queue);
                else if (carrier.HasWithdrawn)
                    WithdrawHeldFighters(queue);
                else
                    continue;

                RemoveQueue(carrier);
            }
        }

        /// <summary>
        /// Returns and clears fighter deployment events produced since the previous drain.
        /// </summary>
        /// <returns>The deployment events in simulation order.</returns>
        internal IReadOnlyList<TacticalCombatEvent> DrainEvents()
        {
            TacticalCombatEvent[] result = events.ToArray();
            events.Clear();
            return result;
        }

        /// <summary>
        /// Destroys fighter squadrons that remain aboard a destroyed carrier.
        /// </summary>
        /// <param name="fighters">The held fighter squadrons.</param>
        private static void DestroyHeldFighters(IEnumerable<TacticalUnitState> fighters)
        {
            foreach (TacticalUnitState fighter in fighters)
                fighter.Hull = 0;
        }

        /// <summary>
        /// Carries held fighter squadrons out of combat with a withdrawing carrier.
        /// </summary>
        /// <param name="fighters">The held fighter squadrons.</param>
        private static void WithdrawHeldFighters(IEnumerable<TacticalUnitState> fighters)
        {
            foreach (TacticalUnitState fighter in fighters)
                fighter.WithdrawWithCarrier();
        }

        /// <summary>
        /// Removes a completed or unavailable carrier launch queue.
        /// </summary>
        /// <param name="carrier">The carrier whose queue is removed.</param>
        private void RemoveQueue(TacticalUnitState carrier)
        {
            launchQueues.Remove(carrier);
            nextLaunchTimes.Remove(carrier);
        }

        /// <summary>
        /// Schedules the next held squadron using the tactical launch interval.
        /// </summary>
        /// <param name="carrier">The carrier launching the squadron.</param>
        private void ScheduleNextLaunch(TacticalUnitState carrier)
        {
            nextLaunchTimes[carrier] =
                elapsedTime + _baseLaunchDelay + random.NextInt(0, _maximumLaunchDelay + 1);
        }
    }
}
