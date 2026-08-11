using System;
using System.Collections.Generic;
using System.Linq;
using Rebellion.Game.Units;
using Rebellion.Util.Common;

namespace Rebellion.Game.Tactical
{
    /// <summary>
    /// Captures the resolved outcome and presentation branch for one Death Star attack run.
    /// </summary>
    internal readonly struct TacticalDeathStarAttackResult
    {
        /// <summary>Gets whether a participating fighter completed the attack run.</summary>
        public bool Succeeded { get; }

        /// <summary>Gets whether defensive fire damaged any fighter during the approach.</summary>
        public bool TookApproachDamage { get; }

        /// <summary>Gets the surviving squadrons destroyed when the run finishes.</summary>
        public IReadOnlyList<TacticalUnitState> CompletionCasualties { get; }

        /// <summary>
        /// Initializes one resolved attack-run result.
        /// </summary>
        /// <param name="succeeded">Whether the attack destroyed the Death Star.</param>
        /// <param name="tookApproachDamage">Whether defensive fire damaged the attackers.</param>
        /// <param name="completionCasualties">The surviving squadrons lost when the run finishes.</param>
        public TacticalDeathStarAttackResult(
            bool succeeded,
            bool tookApproachDamage,
            IReadOnlyList<TacticalUnitState> completionCasualties
        )
        {
            Succeeded = succeeded;
            TookApproachDamage = tookApproachDamage;
            CompletionCasualties =
                completionCasualties
                ?? throw new ArgumentNullException(nameof(completionCasualties));
        }
    }

    /// <summary>
    /// Resolves the dedicated fighter attack run used against a Death Star.
    /// </summary>
    internal sealed class TacticalDeathStarAttackResolver
    {
        private const float _attackStrengthSuccessScale = 0.53333336f;
        private const int _maximumAttackPasses = 1001;
        private readonly IRandomNumberProvider random;

        /// <summary>
        /// Initializes the attack-run resolver with the battle's deterministic random stream.
        /// </summary>
        /// <param name="random">The random source shared by the tactical battle.</param>
        public TacticalDeathStarAttackResolver(IRandomNumberProvider random)
        {
            this.random = random ?? throw new ArgumentNullException(nameof(random));
        }

        /// <summary>
        /// Resolves one complete attack run and applies its fighter losses.
        /// </summary>
        /// <param name="fighterUnits">The fighter squadrons committed to the run.</param>
        /// <param name="fighterCommandBudget">The normalized Combat contribution from the fighter commander.</param>
        /// <returns>The resolved outcome and presentation branch.</returns>
        public TacticalDeathStarAttackResult Resolve(
            IReadOnlyList<TacticalUnitState> fighterUnits,
            float fighterCommandBudget
        )
        {
            if (fighterUnits == null)
                throw new ArgumentNullException(nameof(fighterUnits));

            List<TacticalUnitState> participants = fighterUnits
                .Where(unit => unit is { IsActive: true, Kind: TacticalUnitKind.Fighters })
                .ToList();
            Queue<TacticalUnitState> attackOrder = new Queue<TacticalUnitState>(participants);
            bool tookApproachDamage = false;
            for (int pass = 0; pass < _maximumAttackPasses && attackOrder.Count > 0; pass++)
            {
                TacticalUnitState unit = attackOrder.Dequeue();
                tookApproachDamage |= ApplyApproachFire(unit);
                if (!unit.IsActive)
                    continue;

                if (CanCompleteRun(unit, fighterCommandBudget))
                {
                    IReadOnlyList<TacticalUnitState> completionCasualties = PrepareSuccessfulRun(
                        participants
                    );
                    return new TacticalDeathStarAttackResult(
                        true,
                        tookApproachDamage,
                        completionCasualties
                    );
                }

                attackOrder.Enqueue(unit);
            }

            return new TacticalDeathStarAttackResult(
                false,
                tookApproachDamage,
                participants.Where(unit => unit.IsActive).ToArray()
            );
        }

        /// <summary>
        /// Applies the defensive-fire check made before one squadron attempts its attack.
        /// </summary>
        /// <param name="unit">The fighter squadron making the approach.</param>
        /// <returns>True when defensive fire damages or destroys the squadron.</returns>
        private bool ApplyApproachFire(TacticalUnitState unit)
        {
            Starfighter fighters = (Starfighter)unit.Unit;
            int evasion =
                unit.EffectiveSublightSpeed <= 0f ? 1 : Math.Clamp(fighters.Agility + 1, 1, 9);
            if (random.NextInt(1, 11) <= evasion)
                return false;

            if (unit.Hull < 2)
            {
                unit.Hull = 0;
                return true;
            }

            unit.Hull -= random.NextInt(1, unit.Hull + 1);
            return true;
        }

        /// <summary>
        /// Tests whether one surviving squadron completes the attack run.
        /// </summary>
        /// <param name="unit">The participating fighter squadron.</param>
        /// <param name="fighterCommandBudget">The normalized Combat contribution from the fighter commander.</param>
        /// <returns>True when a fighter succeeds.</returns>
        private bool CanCompleteRun(TacticalUnitState unit, float fighterCommandBudget)
        {
            Starfighter fighters = (Starfighter)unit.Unit;
            int maximumFighterCount = Math.Max(1, fighters.MaxSquadronSize);
            float survivingStrength =
                (fighters.LaserCannon + fighters.IonCannon)
                * _attackStrengthSuccessScale
                * unit.Hull
                / maximumFighterCount;
            int successChance = Math.Min(
                100,
                (int)((survivingStrength + Math.Clamp(fighterCommandBudget, 1f, 9f)) * 0.5f)
            );
            return random.NextInt(1, 101) <= successChance;
        }

        /// <summary>
        /// Restores surviving squadrons and selects the half lost when the run finishes.
        /// </summary>
        /// <param name="participants">The squadrons committed to the run.</param>
        /// <returns>The surviving squadrons lost at completion in formation order.</returns>
        private static IReadOnlyList<TacticalUnitState> PrepareSuccessfulRun(
            IReadOnlyList<TacticalUnitState> participants
        )
        {
            TacticalUnitState[] survivors = participants.Where(unit => unit.IsActive).ToArray();
            foreach (TacticalUnitState survivor in survivors)
            {
                Starfighter fighters = (Starfighter)survivor.Unit;
                survivor.Hull = Math.Max(0, fighters.MaxSquadronSize);
            }

            return survivors.Take(survivors.Length / 2).ToArray();
        }
    }
}
