using System;
using System.Collections.Generic;
using System.Linq;
using Rebellion.Game.Units;

namespace Rebellion.Game.Tactical
{
    /// <summary>
    /// Owns Death Star superlaser charge and resolves its dedicated tactical attack.
    /// </summary>
    internal sealed class TacticalSuperlaserSystem
    {
        internal const float MaximumCharge = 100f;
        internal const float ResolutionDelay = 1f;
        private const float _chargePerTacticalSecond = 1f / 3f;
        private readonly HashSet<TacticalUnitState> participants;
        private readonly Dictionary<TacticalUnitState, float> chargeByDeathStar;
        private readonly List<PendingShot> pendingShots = new List<PendingShot>();
        private readonly List<TacticalUnitState> readyDeathStars = new List<TacticalUnitState>();
        private readonly List<ResolvedShot> resolvedShots = new List<ResolvedShot>();

        /// <summary>
        /// Identifies the source and target of one delayed superlaser result.
        /// </summary>
        internal readonly struct ResolvedShot
        {
            /// <summary>
            /// Gets the Death Star that fired the shot.
            /// </summary>
            public TacticalUnitState Source { get; }

            /// <summary>
            /// Gets the opposing tactical object struck by the shot.
            /// </summary>
            public TacticalUnitState Target { get; }

            /// <summary>
            /// Initializes one resolved superlaser shot.
            /// </summary>
            /// <param name="source">The Death Star that fired the shot.</param>
            /// <param name="target">The opposing tactical object struck by the shot.</param>
            public ResolvedShot(TacticalUnitState source, TacticalUnitState target)
            {
                Source = source;
                Target = target;
            }
        }

        private sealed class PendingShot
        {
            /// <summary>
            /// Gets the Death Star that fired the shot.
            /// </summary>
            public TacticalUnitState Source { get; }

            /// <summary>
            /// Gets the opposing tactical object selected for destruction.
            /// </summary>
            public TacticalUnitState Target { get; }

            /// <summary>
            /// Gets or sets the tactical time remaining before the shot resolves.
            /// </summary>
            public float RemainingTime { get; set; } = ResolutionDelay;

            /// <summary>
            /// Initializes one delayed superlaser result.
            /// </summary>
            /// <param name="source">The Death Star that fired the shot.</param>
            /// <param name="target">The opposing tactical object selected for destruction.</param>
            public PendingShot(TacticalUnitState source, TacticalUnitState target)
            {
                Source = source;
                Target = target;
            }
        }

        /// <summary>
        /// Initializes every participating Death Star with a fully charged superlaser.
        /// </summary>
        /// <param name="units">The tactical units participating in the battle.</param>
        public TacticalSuperlaserSystem(IReadOnlyList<TacticalUnitState> units)
        {
            if (units == null)
                throw new ArgumentNullException(nameof(units));

            participants = new HashSet<TacticalUnitState>(units);
            chargeByDeathStar = units
                .Where(IsDeathStar)
                .ToDictionary(unit => unit, _ => MaximumCharge);
        }

        /// <summary>
        /// Gets one Death Star's current charge percentage.
        /// </summary>
        /// <param name="deathStar">The Death Star whose charge is requested.</param>
        /// <returns>The current charge from zero through one hundred.</returns>
        public float GetCharge(TacticalUnitState deathStar)
        {
            ValidateDeathStar(deathStar);
            return chargeByDeathStar[deathStar];
        }

        /// <summary>
        /// Captures charge meters, delayed shots, and undrained superlaser notifications.
        /// </summary>
        /// <returns>The resumable superlaser state.</returns>
        internal TacticalSuperlaserSnapshot CaptureState()
        {
            return new TacticalSuperlaserSnapshot
            {
                Charges = chargeByDeathStar
                    .Select(entry => new TacticalSuperlaserChargeSnapshot
                    {
                        DeathStarInstanceID = entry.Key.Unit.GetInstanceID(),
                        Charge = entry.Value,
                    })
                    .ToList(),
                PendingShots = pendingShots
                    .Select(shot => CaptureShot(shot.Source, shot.Target, shot.RemainingTime))
                    .ToList(),
                ReadyDeathStarInstanceIDs = readyDeathStars
                    .Select(unit => unit.Unit.GetInstanceID())
                    .ToList(),
                ResolvedShots = resolvedShots
                    .Select(shot => CaptureShot(shot.Source, shot.Target, 0f))
                    .ToList(),
            };
        }

        /// <summary>
        /// Restores charge meters, delayed shots, and undrained superlaser notifications.
        /// </summary>
        /// <param name="snapshot">The saved superlaser state.</param>
        /// <param name="unitsById">All participating tactical units indexed by identifier.</param>
        internal void RestoreState(
            TacticalSuperlaserSnapshot snapshot,
            IReadOnlyDictionary<string, TacticalUnitState> unitsById
        )
        {
            if (snapshot == null)
                throw new ArgumentNullException(nameof(snapshot));
            if (unitsById == null)
                throw new ArgumentNullException(nameof(unitsById));
            if (
                snapshot.Charges == null
                || snapshot.PendingShots == null
                || snapshot.ReadyDeathStarInstanceIDs == null
                || snapshot.ResolvedShots == null
            )
            {
                throw new ArgumentException(
                    "The superlaser snapshot is incomplete.",
                    nameof(snapshot)
                );
            }

            chargeByDeathStar.Clear();
            foreach (TacticalSuperlaserChargeSnapshot chargeSnapshot in snapshot.Charges)
            {
                TacticalUnitState deathStar = ResolveUnit(
                    chargeSnapshot.DeathStarInstanceID,
                    unitsById
                );
                if (!IsDeathStar(deathStar) || chargeByDeathStar.ContainsKey(deathStar))
                {
                    throw new ArgumentException(
                        "The superlaser snapshot contains an invalid Death Star.",
                        nameof(snapshot)
                    );
                }

                chargeByDeathStar.Add(
                    deathStar,
                    Math.Clamp(chargeSnapshot.Charge, 0f, MaximumCharge)
                );
            }

            pendingShots.Clear();
            pendingShots.AddRange(
                snapshot.PendingShots.Select(shotSnapshot => new PendingShot(
                    ResolveUnit(shotSnapshot.SourceInstanceID, unitsById),
                    ResolveUnit(shotSnapshot.TargetInstanceID, unitsById)
                )
                {
                    RemainingTime = Math.Max(0f, shotSnapshot.RemainingTime),
                })
            );
            readyDeathStars.Clear();
            readyDeathStars.AddRange(
                snapshot.ReadyDeathStarInstanceIDs.Select(unitInstanceID =>
                    ResolveUnit(unitInstanceID, unitsById)
                )
            );
            resolvedShots.Clear();
            resolvedShots.AddRange(
                snapshot.ResolvedShots.Select(shotSnapshot => new ResolvedShot(
                    ResolveUnit(shotSnapshot.SourceInstanceID, unitsById),
                    ResolveUnit(shotSnapshot.TargetInstanceID, unitsById)
                ))
            );
        }

        /// <summary>
        /// Captures one superlaser shot using stable strategic unit identifiers.
        /// </summary>
        /// <param name="source">The firing Death Star.</param>
        /// <param name="target">The targeted tactical unit.</param>
        /// <param name="remainingTime">The remaining delayed-resolution time.</param>
        /// <returns>The persisted shot state.</returns>
        private static TacticalSuperlaserShotSnapshot CaptureShot(
            TacticalUnitState source,
            TacticalUnitState target,
            float remainingTime
        )
        {
            return new TacticalSuperlaserShotSnapshot
            {
                SourceInstanceID = source.Unit.GetInstanceID(),
                TargetInstanceID = target.Unit.GetInstanceID(),
                RemainingTime = remainingTime,
            };
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
        /// Advances the dedicated charge meter for every operational Death Star.
        /// </summary>
        /// <param name="elapsedTime">The elapsed tactical time.</param>
        public void Advance(float elapsedTime)
        {
            if (elapsedTime < 0f)
                throw new ArgumentOutOfRangeException(nameof(elapsedTime));

            foreach (TacticalUnitState deathStar in chargeByDeathStar.Keys.ToArray())
            {
                if (!CanOperate(deathStar))
                    continue;

                float previousCharge = chargeByDeathStar[deathStar];
                chargeByDeathStar[deathStar] = Math.Min(
                    MaximumCharge,
                    previousCharge + elapsedTime * _chargePerTacticalSecond
                );
                if (previousCharge < MaximumCharge && chargeByDeathStar[deathStar] >= MaximumCharge)
                {
                    readyDeathStars.Add(deathStar);
                }
            }

            foreach (PendingShot shot in pendingShots.ToArray())
            {
                shot.RemainingTime -= elapsedTime;
                if (shot.RemainingTime > 0f)
                    continue;

                pendingShots.Remove(shot);
                if (shot.Target.IsActive)
                    resolvedShots.Add(new ResolvedShot(shot.Source, shot.Target));
            }
        }

        /// <summary>
        /// Fires a charged Death Star at one active opposing tactical object.
        /// </summary>
        /// <param name="deathStar">The firing Death Star.</param>
        /// <param name="target">The opposing tactical object selected as its target.</param>
        /// <returns>True when the shot fires and schedules the target's destruction.</returns>
        public bool TryFire(TacticalUnitState deathStar, TacticalUnitState target)
        {
            ValidateDeathStar(deathStar);
            if (
                !CanOperate(deathStar)
                || target is not { IsActive: true }
                || !participants.Contains(target)
                || target.Side == deathStar.Side
                || chargeByDeathStar[deathStar] < MaximumCharge
            )
            {
                return false;
            }

            chargeByDeathStar[deathStar] = 0f;
            pendingShots.Add(new PendingShot(deathStar, target));
            return true;
        }

        /// <summary>
        /// Removes and returns delayed superlaser shots that reached active targets.
        /// </summary>
        /// <returns>The resolved shots in simulation order.</returns>
        public IReadOnlyList<ResolvedShot> DrainResolvedShots()
        {
            ResolvedShot[] result = resolvedShots.ToArray();
            resolvedShots.Clear();
            return result;
        }

        /// <summary>
        /// Removes and returns Death Stars that finished recharging during advancement.
        /// </summary>
        /// <returns>The newly ready Death Stars in simulation order.</returns>
        public IReadOnlyList<TacticalUnitState> DrainReadyDeathStars()
        {
            TacticalUnitState[] result = readyDeathStars.ToArray();
            readyDeathStars.Clear();
            return result;
        }

        /// <summary>
        /// Tests whether a tactical unit represents a Death Star.
        /// </summary>
        /// <param name="unit">The tactical unit to inspect.</param>
        /// <returns>True when the unit is a Death Star.</returns>
        private static bool IsDeathStar(TacticalUnitState unit)
        {
            return unit?.Unit is CapitalShip { IsDeathStar: true };
        }

        /// <summary>
        /// Tests whether a Death Star remains active and has not begun withdrawing.
        /// </summary>
        /// <param name="deathStar">The Death Star to inspect.</param>
        /// <returns>True when its superlaser can charge or fire.</returns>
        private static bool CanOperate(TacticalUnitState deathStar)
        {
            return deathStar.IsActive && !deathStar.IsWithdrawing;
        }

        /// <summary>
        /// Rejects objects that do not belong to this system's Death Star set.
        /// </summary>
        /// <param name="deathStar">The proposed firing object.</param>
        private void ValidateDeathStar(TacticalUnitState deathStar)
        {
            if (deathStar == null)
                throw new ArgumentNullException(nameof(deathStar));
            if (!chargeByDeathStar.ContainsKey(deathStar))
                throw new ArgumentException(
                    "The tactical unit is not a participating Death Star.",
                    nameof(deathStar)
                );
        }
    }
}
