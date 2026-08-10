using System;
using System.Collections.Generic;
using System.Linq;
using Rebellion.Game.Units;
using Rebellion.Util.Common;

namespace Rebellion.Game.Tactical
{
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
        /// <returns>True when one participating fighter completes the run.</returns>
        public bool Resolve(
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
            for (int pass = 0; pass < _maximumAttackPasses && attackOrder.Count > 0; pass++)
            {
                TacticalUnitState unit = attackOrder.Dequeue();
                ApplyApproachFire(unit);
                if (!unit.IsActive)
                    continue;

                if (CanCompleteRun(unit, fighterCommandBudget))
                {
                    ResolveSuccessfulRun(participants);
                    return true;
                }

                attackOrder.Enqueue(unit);
            }

            DestroySquadrons(participants);
            return false;
        }

        /// <summary>
        /// Applies the defensive-fire check made before one squadron attempts its attack.
        /// </summary>
        /// <param name="unit">The fighter squadron making the approach.</param>
        private void ApplyApproachFire(TacticalUnitState unit)
        {
            Starfighter fighters = (Starfighter)unit.Unit;
            int evasion =
                unit.EffectiveSublightSpeed <= 0f ? 1 : Math.Clamp(fighters.Agility + 1, 1, 9);
            if (random.NextInt(1, 11) <= evasion)
                return;

            if (unit.Hull < 2)
            {
                unit.Hull = 0;
                return;
            }

            unit.Hull -= random.NextInt(1, unit.Hull + 1);
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
        /// Restores surviving squadrons and destroys half of them in formation order.
        /// </summary>
        /// <param name="participants">The squadrons committed to the run.</param>
        private static void ResolveSuccessfulRun(IReadOnlyList<TacticalUnitState> participants)
        {
            TacticalUnitState[] survivors = participants.Where(unit => unit.IsActive).ToArray();
            foreach (TacticalUnitState survivor in survivors)
            {
                Starfighter fighters = (Starfighter)survivor.Unit;
                survivor.Hull = Math.Max(0, fighters.MaxSquadronSize);
            }

            DestroySquadrons(survivors.Take(survivors.Length / 2));
        }

        /// <summary>
        /// Destroys each supplied fighter squadron.
        /// </summary>
        /// <param name="squadrons">The fighter squadrons to destroy.</param>
        private static void DestroySquadrons(IEnumerable<TacticalUnitState> squadrons)
        {
            foreach (TacticalUnitState squadron in squadrons)
                squadron.Hull = 0;
        }
    }
}
