using System;
using System.Collections.Generic;
using System.Linq;
using Rebellion.Game;
using Rebellion.Game.Results;
using Rebellion.Util.Common;

namespace Rebellion.Systems
{
    public enum BombardmentLaneType
    {
        Troop,
        Starfighter,
        Energy,
        Building,
    }

    public enum AssaultLaneType
    {
        Troop,
        Building,
        Energy,
        EnergyAllocated,
    }

    /// <summary>
    /// Captures the context of a pending combat encounter requiring player resolution.
    /// Held by GameManager until the player resolves combat via ResolveCombat().
    /// </summary>
    public class CombatDecisionContext
    {
        public string AttackerFleetInstanceID { get; set; }
        public string DefenderFleetInstanceID { get; set; }
    }

    /// <summary>
    /// Manages space combat, planetary assault, and orbital bombardment.
    /// Space combat implements a 7-phase pipeline.
    /// </summary>
    public class CombatSystem
    {
        private readonly GameRoot _game;
        private readonly IRandomNumberProvider _provider;
        private readonly MovementSystem _movement;
        private readonly PlanetaryControlSystem _ownership;

        /// <summary>
        /// Creates a new CombatSystem.
        /// </summary>
        /// <param name="game">The game instance.</param>
        /// <param name="provider">Random number provider for combat resolution.</param>
        /// <param name="movement">Movement system used to evacuate officers from destroyed ships.</param>
        /// <param name="ownership">Planetary control system used to transfer planets on conquest.</param>
        public CombatSystem(
            GameRoot game,
            IRandomNumberProvider provider,
            MovementSystem movement,
            PlanetaryControlSystem ownership
        )
        {
            _game = game;
            _provider = provider;
            _movement = movement ?? throw new ArgumentNullException(nameof(movement));
            _ownership = ownership ?? throw new ArgumentNullException(nameof(ownership));
        }

        /// <summary>
        /// Resolves all AI-vs-AI combat encounters this tick in a single pass.
        /// When a player-involved encounter is found, emits a PendingCombatResult and stops —
        /// the caller is responsible for freezing the tick until the player resolves it.
        /// </summary>
        /// <returns>Combat results, or a PendingCombatResult if player input is needed.</returns>
        public List<GameResult> ProcessTick()
        {
            List<GameResult> results = new List<GameResult>();
            HashSet<string> foughtThisTick = new HashSet<string>();

            while (TryStartCombatExcluding(foughtThisTick, out CombatDecisionContext decision))
            {
                Fleet attackerFleet = _game.GetSceneNodeByInstanceID<Fleet>(
                    decision.AttackerFleetInstanceID
                );
                Fleet defenderFleet = _game.GetSceneNodeByInstanceID<Fleet>(
                    decision.DefenderFleetInstanceID
                );
                Faction attacker = _game.GetFactionByOwnerInstanceID(
                    attackerFleet?.GetOwnerInstanceID()
                );
                Faction defender = _game.GetFactionByOwnerInstanceID(
                    defenderFleet?.GetOwnerInstanceID()
                );

                if (
                    attacker != null
                    && defender != null
                    && attacker.IsAIControlled()
                    && defender.IsAIControlled()
                )
                {
                    SpaceCombatResult combatResult = Resolve(decision, autoResolve: true);
                    if (combatResult != null)
                        results.AddRange(combatResult.Events);
                    foughtThisTick.Add(decision.AttackerFleetInstanceID);
                    foughtThisTick.Add(decision.DefenderFleetInstanceID);
                }
                else
                {
                    results.Add(
                        new PendingCombatResult
                        {
                            AttackerFleet = attackerFleet,
                            DefenderFleet = defenderFleet,
                            Tick = _game.CurrentTick,
                        }
                    );
                    return results;
                }
            }

            return results;
        }

        /// <summary>
        /// Detects the first hostile fleet encounter this tick.
        /// The encounter is NOT resolved here; call Resolve() after player decision.
        /// </summary>
        /// <param name="decision">Output: the combat decision context if an encounter was found.</param>
        /// <returns>True if a hostile encounter was detected.</returns>
        public bool TryStartCombat(out CombatDecisionContext decision) =>
            TryStartCombatExcluding(new HashSet<string>(), out decision);

        /// <summary>
        /// Detects the first hostile fleet encounter excluding any fleet IDs already resolved
        /// this tick. Used by ProcessTick to prevent Draw outcomes from causing re-detection.
        /// </summary>
        /// <param name="excludedIDs">Fleet IDs to skip (already fought this tick).</param>
        /// <param name="decision">Output: the combat decision context if found.</param>
        /// <returns>True if a hostile encounter was detected.</returns>
        private bool TryStartCombatExcluding(
            HashSet<string> excludedIDs,
            out CombatDecisionContext decision
        )
        {
            decision = null;

            if (!DetectFleetCollision(excludedIDs, out Fleet attacker, out Fleet defender))
                return false;

            attacker.IsInCombat = true;
            defender.IsInCombat = true;

            decision = new CombatDecisionContext
            {
                AttackerFleetInstanceID = attacker.GetInstanceID(),
                DefenderFleetInstanceID = defender.GetInstanceID(),
            };

            return true;
        }

        /// <summary>
        /// Finds the first pair of hostile fleets occupying the same planet.
        /// Skips fleets already engaged in combat or excluded by ID.
        /// Faction groups are sorted by owner ID for deterministic selection.
        /// </summary>
        /// <param name="excludedIDs">Fleet IDs to skip.</param>
        /// <param name="attacker">Output: the attacking fleet.</param>
        /// <param name="defender">Output: the defending fleet.</param>
        /// <returns>True if a collision was found.</returns>
        private bool DetectFleetCollision(
            HashSet<string> excludedIDs,
            out Fleet attacker,
            out Fleet defender
        )
        {
            attacker = null;
            defender = null;

            foreach (Planet planet in _game.GetSceneNodesByType<Planet>())
            {
                List<Fleet> fleets = planet
                    .GetChildren<Fleet>(
                        f => !f.IsInCombat && !excludedIDs.Contains(f.GetInstanceID()),
                        recurse: false
                    )
                    .ToList();

                if (fleets.Count < 2)
                    continue;

                List<IGrouping<string, Fleet>> factionGroups = fleets
                    .GroupBy(f => f.GetOwnerInstanceID())
                    .Where(g => g.Key != null)
                    .OrderBy(g => g.Key)
                    .ToList();

                if (factionGroups.Count < 2)
                    continue;

                attacker = factionGroups[0].First();
                defender = factionGroups[1].First();
                return true;
            }

            return false;
        }

        /// <summary>
        /// Resolves a pending combat encounter. Applies damage to the game world and
        /// clears IsInCombat on both fleets regardless of outcome.
        /// </summary>
        /// <param name="decision">The pending combat decision to resolve.</param>
        /// <param name="autoResolve">True for AI auto-resolve, false for manual/interactive.</param>
        /// <returns>The combat result, or null if manual combat was used.</returns>
        public SpaceCombatResult Resolve(CombatDecisionContext decision, bool autoResolve)
        {
            SpaceCombatResult result = null;

            if (autoResolve)
            {
                result = AutoResolveCombat(decision, _provider);
            }
            else
            {
                RunManualCombat();
            }

            Fleet attacker = _game.GetSceneNodeByInstanceID<Fleet>(
                decision.AttackerFleetInstanceID
            );
            Fleet defender = _game.GetSceneNodeByInstanceID<Fleet>(
                decision.DefenderFleetInstanceID
            );
            if (attacker != null)
                attacker.IsInCombat = false;
            if (defender != null)
                defender.IsInCombat = false;

            return result;
        }

        /// <summary>
        /// Auto-resolves space combat using the 7-phase pipeline, then applies
        /// damage to the game world.
        /// </summary>
        /// <param name="decision">The combat decision context.</param>
        /// <param name="rng">Random number provider.</param>
        /// <returns>The combat result with winner and destroyed unit IDs.</returns>
        private SpaceCombatResult AutoResolveCombat(
            CombatDecisionContext decision,
            IRandomNumberProvider rng
        )
        {
            Fleet attacker = _game.GetSceneNodeByInstanceID<Fleet>(
                decision.AttackerFleetInstanceID
            );
            Fleet defender = _game.GetSceneNodeByInstanceID<Fleet>(
                decision.DefenderFleetInstanceID
            );

            if (attacker == null || defender == null)
            {
                GameLogger.Warning("AutoResolveCombat: one or both fleets no longer exist.");
                return null;
            }

            Planet planet = attacker.GetParentOfType<Planet>();
            if (planet == null)
            {
                GameLogger.Warning(
                    $"AutoResolveCombat: attacker {attacker.GetDisplayName()} is not at a planet."
                );
                return null;
            }

            SpaceCombatResult result = ResolveSpace(
                attacker,
                defender,
                planet,
                rng,
                _game.CurrentTick
            );
            result.Events = ApplyCombatResult(result);

            GameLogger.Log(
                $"Combat at {planet.GetDisplayName()}: "
                    + $"{attacker.GetDisplayName()} vs {defender.GetDisplayName()} — "
                    + $"Winner: {result.Winner}"
            );

            return result;
        }

        /// <summary>
        /// Placeholder for manual/interactive combat resolution.
        /// </summary>
        private void RunManualCombat()
        {
            // TODO: Implement manual combat resolution
        }

        /// <summary>
        /// Executes the 7-phase space combat pipeline between two fleets.
        ///
        /// Phase 1: Initialize — build mutable hull snapshots.
        /// Phase 2: Fleet composition evaluation — check if either side has weapons.
        /// Phase 3: Weapon fire — each side fires at the other with ±20% variance.
        /// Phase 4-5: Shield absorption and hull damage (merged into phase 3).
        /// Phase 6: Fighter engagement — fighters attack ships, then dogfight each other.
        /// Phase 7: Result determination — count survivors, declare winner.
        /// </summary>
        /// <param name="attackerFleet">The attacking fleet.</param>
        /// <param name="defenderFleet">The defending fleet.</param>
        /// <param name="planet">The planet where combat occurs.</param>
        /// <param name="rng">Random number provider for damage variance.</param>
        /// <param name="tick">Current game tick.</param>
        /// <returns>Combat result with damage results and winner.</returns>
        private static SpaceCombatResult ResolveSpace(
            Fleet attackerFleet,
            Fleet defenderFleet,
            Planet planet,
            IRandomNumberProvider rng,
            int tick
        )
        {
            // Phase 1: Initialize — build mutable hull snapshots
            (List<ShipSnap> atkShips, List<int> atkFighters) = SnapshotFleet(attackerFleet);
            (List<ShipSnap> defShips, List<int> defFighters) = SnapshotFleet(defenderFleet);
            List<int> atkInitialFighters = atkFighters.ToList();
            List<int> defInitialFighters = defFighters.ToList();

            // Phase 2: Fleet composition evaluation
            bool anyArmed =
                atkShips.Any(s => s.Alive && s.WeaponNibble > 0)
                || defShips.Any(s => s.Alive && s.WeaponNibble > 0)
                || HasArmedFighters(attackerFleet, atkFighters)
                || HasArmedFighters(defenderFleet, defFighters);

            if (anyArmed)
            {
                // Phase 3: Weapon fire (includes shield absorption and hull damage)
                PhaseWeaponFire(attackerFleet, atkShips, defShips, rng);
                PhaseWeaponFire(defenderFleet, defShips, atkShips, rng);

                // Phase 6: Fighter engagement
                PhaseFighterEngage(
                    attackerFleet,
                    defenderFleet,
                    atkFighters,
                    defFighters,
                    atkShips,
                    defShips,
                    rng
                );
            }

            // Phase 7: Build result
            return BuildSpaceResult(
                attackerFleet,
                defenderFleet,
                planet,
                atkShips,
                defShips,
                atkFighters,
                defFighters,
                atkInitialFighters,
                defInitialFighters,
                tick
            );
        }

        /// <summary>
        /// Determines the combat winner based on surviving units.
        /// </summary>
        /// <param name="atkShips">Attacker ship snapshots.</param>
        /// <param name="defShips">Defender ship snapshots.</param>
        /// <param name="atkFighters">Attacker fighter squadron counts.</param>
        /// <param name="defFighters">Defender fighter squadron counts.</param>
        /// <returns>Which side won, or Draw if both or neither have survivors.</returns>
        private static CombatSide DetermineWinner(
            List<ShipSnap> atkShips,
            List<ShipSnap> defShips,
            List<int> atkFighters,
            List<int> defFighters
        )
        {
            bool atkAlive = atkShips.Any(s => s.Alive) || atkFighters.Any(c => c > 0);
            bool defAlive = defShips.Any(s => s.Alive) || defFighters.Any(c => c > 0);

            if (atkAlive && !defAlive)
                return CombatSide.Attacker;
            if (!atkAlive && defAlive)
                return CombatSide.Defender;
            return CombatSide.Draw;
        }

        /// <summary>
        /// Builds mutable hull snapshots for all capital ships and fighter squadron
        /// counts for one fleet.
        /// </summary>
        /// <param name="fleet">The fleet to snapshot.</param>
        /// <returns>Ship snapshots and fighter squadron size list.</returns>
        private static (List<ShipSnap> ships, List<int> fighters) SnapshotFleet(Fleet fleet)
        {
            List<ShipSnap> ships = new List<ShipSnap>();

            foreach (CapitalShip ship in fleet.CapitalShips)
            {
                ships.Add(
                    new ShipSnap
                    {
                        HullCurrent = ship.GetCurrentHull(),
                        HullMax = ship.HullStrength,
                        ShieldNibble = Math.Min(ship.ShieldRechargeRate, 15),
                        WeaponNibble = 15,
                        Alive = true,
                    }
                );
            }

            List<int> fighters = fleet
                .GetStarfighters()
                .Select(f => f.GetCurrentSquadronSize())
                .ToList();

            return (ships, fighters);
        }

        /// <summary>
        /// Checks whether a fleet has any fighter squadrons with attack capability.
        /// </summary>
        /// <param name="fleet">The fleet to check.</param>
        /// <param name="squadrons">Current squadron sizes.</param>
        /// <returns>True if at least one squadron has weapons and fighters remaining.</returns>
        private static bool HasArmedFighters(Fleet fleet, List<int> squadrons)
        {
            List<Starfighter> fighters = fleet.GetStarfighters().ToList();
            for (int i = 0; i < squadrons.Count && i < fighters.Count; i++)
            {
                if (squadrons[i] > 0)
                {
                    Starfighter fighter = fighters[i];
                    int totalAttack = fighter.LaserCannon + fighter.IonCannon + fighter.Torpedoes;
                    if (totalAttack > 0)
                        return true;
                }
            }
            return false;
        }

        /// <summary>
        /// Phase 3: One side fires all weapon arcs at the other side's ships.
        /// Aggregates total firepower scaled by weapon nibble allocation, distributes
        /// evenly across alive targets with ±20% variance, and applies shield absorption.
        /// </summary>
        /// <param name="firingFleet">The fleet doing the firing (used for weapon data).</param>
        /// <param name="firing">Ship snapshots of the firing side.</param>
        /// <param name="targets">Ship snapshots of the target side.</param>
        /// <param name="rng">Random number provider for damage variance.</param>
        private static void PhaseWeaponFire(
            Fleet firingFleet,
            List<ShipSnap> firing,
            List<ShipSnap> targets,
            IRandomNumberProvider rng
        )
        {
            int totalFire = CalculateTotalFirepower(firingFleet, firing);
            if (totalFire == 0)
                return;

            List<int> aliveIndices = targets
                .Select((t, idx) => new { t, idx })
                .Where(x => x.t.Alive)
                .Select(x => x.idx)
                .ToList();

            if (aliveIndices.Count == 0)
                return;

            int firePerTarget = totalFire / aliveIndices.Count;
            foreach (int idx in aliveIndices)
            {
                ApplyWeaponDamage(targets[idx], firePerTarget, rng);
            }
        }

        /// <summary>
        /// Sums all weapon arc values across alive ships, scaled by each ship's weapon nibble.
        /// </summary>
        /// <param name="fleet">The fleet (used for weapon data lookup).</param>
        /// <param name="ships">Ship snapshots with alive/weapon nibble state.</param>
        /// <returns>Total firepower value.</returns>
        private static int CalculateTotalFirepower(Fleet fleet, List<ShipSnap> ships)
        {
            int totalFire = 0;
            for (int i = 0; i < ships.Count; i++)
            {
                if (!ships[i].Alive)
                    continue;

                CapitalShip ship = fleet.CapitalShips[i];
                int raw = 0;

                if (ship.PrimaryWeapons.ContainsKey(PrimaryWeaponType.Turbolaser))
                    raw += ship.PrimaryWeapons[PrimaryWeaponType.Turbolaser].Sum();
                if (ship.PrimaryWeapons.ContainsKey(PrimaryWeaponType.IonCannon))
                    raw += ship.PrimaryWeapons[PrimaryWeaponType.IonCannon].Sum();
                if (ship.PrimaryWeapons.ContainsKey(PrimaryWeaponType.LaserCannon))
                    raw += ship.PrimaryWeapons[PrimaryWeaponType.LaserCannon].Sum();

                totalFire += (raw * ships[i].WeaponNibble) / 15;
            }
            return totalFire;
        }

        /// <summary>
        /// Applies weapon damage to a single target ship with ±20% variance and shield absorption.
        /// Shield nibble / 15 fraction is absorbed; remainder hits hull.
        /// </summary>
        /// <param name="target">The target ship snapshot to damage.</param>
        /// <param name="baseDamage">Base damage before variance.</param>
        /// <param name="rng">Random number provider for variance roll.</param>
        private static void ApplyWeaponDamage(
            ShipSnap target,
            int baseDamage,
            IRandomNumberProvider rng
        )
        {
            double roll = rng.NextDouble();
            int variance = (int)(baseDamage * 0.2 * (roll * 2.0 - 1.0));
            int damage = Math.Max(baseDamage + variance, 0);

            int absorbed = (int)(damage * target.ShieldNibble / 15.0);
            int hullDamage = Math.Max(damage - absorbed, 0);

            target.HullCurrent = Math.Max(target.HullCurrent - hullDamage, 0);
            if (target.HullCurrent == 0)
                target.Alive = false;
        }

        /// <summary>
        /// Phase 6: Fighter engagement. Fighters attack enemy capital ships first,
        /// then opposing fighter squadrons dogfight each other.
        /// </summary>
        /// <param name="attackerFleet">Attacker fleet (used for fighter weapon data).</param>
        /// <param name="defenderFleet">Defender fleet (used for fighter weapon data).</param>
        /// <param name="atkFighters">Attacker squadron sizes (mutated).</param>
        /// <param name="defFighters">Defender squadron sizes (mutated).</param>
        /// <param name="atkShips">Attacker ship snapshots (targets for defender fighters).</param>
        /// <param name="defShips">Defender ship snapshots (targets for attacker fighters).</param>
        /// <param name="rng">Random number provider.</param>
        private static void PhaseFighterEngage(
            Fleet attackerFleet,
            Fleet defenderFleet,
            List<int> atkFighters,
            List<int> defFighters,
            List<ShipSnap> atkShips,
            List<ShipSnap> defShips,
            IRandomNumberProvider rng
        )
        {
            FightersAttackShips(atkFighters, attackerFleet, defShips, rng);
            FightersAttackShips(defFighters, defenderFleet, atkShips, rng);

            int atkTotal = atkFighters.Sum();
            int defTotal = defFighters.Sum();

            if (atkTotal == 0 || defTotal == 0)
                return;

            double rollAtk = rng.NextDouble();
            double rollDef = rng.NextDouble();

            double atkHitRate = (double)defTotal / (atkTotal + defTotal);
            double defHitRate = (double)atkTotal / (atkTotal + defTotal);

            int atkLosses = Math.Min((int)(atkTotal * atkHitRate * 0.3 * rollAtk), atkTotal);
            int defLosses = Math.Min((int)(defTotal * defHitRate * 0.3 * rollDef), defTotal);

            ApplyFighterLosses(atkFighters, atkLosses);
            ApplyFighterLosses(defFighters, defLosses);
        }

        /// <summary>
        /// Each fighter squadron picks a random alive enemy capital ship and attacks it.
        /// Damage is total squadron attack (laser + ion + torpedoes) * squadron size with ±20% variance.
        /// </summary>
        /// <param name="squadrons">Squadron sizes for the attacking side.</param>
        /// <param name="fleet">Fleet owning the fighters (used for weapon data).</param>
        /// <param name="enemyShips">Enemy ship snapshots to attack.</param>
        /// <param name="rng">Random number provider.</param>
        private static void FightersAttackShips(
            List<int> squadrons,
            Fleet fleet,
            List<ShipSnap> enemyShips,
            IRandomNumberProvider rng
        )
        {
            List<int> aliveTargets = enemyShips
                .Select((s, idx) => new { s, idx })
                .Where(x => x.s.Alive)
                .Select(x => x.idx)
                .ToList();

            if (aliveTargets.Count == 0)
                return;

            List<Starfighter> fighters = fleet.GetStarfighters().ToList();
            for (int sqIdx = 0; sqIdx < squadrons.Count; sqIdx++)
            {
                if (squadrons[sqIdx] == 0 || sqIdx >= fighters.Count)
                    continue;

                Starfighter fighter = fighters[sqIdx];
                int totalAttack =
                    (fighter.LaserCannon + fighter.IonCannon + fighter.Torpedoes)
                    * squadrons[sqIdx];

                if (totalAttack == 0)
                    continue;

                int targetIdx = aliveTargets[(int)(rng.NextDouble() * aliveTargets.Count)];

                double roll = rng.NextDouble();
                int damage = (int)(totalAttack * (0.8 + 0.4 * roll));

                enemyShips[targetIdx].HullCurrent = Math.Max(
                    enemyShips[targetIdx].HullCurrent - damage,
                    0
                );
                if (enemyShips[targetIdx].HullCurrent == 0)
                {
                    enemyShips[targetIdx].Alive = false;
                    aliveTargets.Remove(targetIdx);
                    if (aliveTargets.Count == 0)
                        break;
                }
            }
        }

        /// <summary>
        /// Distributes fighter losses proportionally across squadrons. Remainder
        /// is distributed one-per-squadron to avoid rounding bias.
        /// </summary>
        /// <param name="squadrons">Squadron sizes to reduce (mutated).</param>
        /// <param name="totalLosses">Total number of fighters to remove.</param>
        private static void ApplyFighterLosses(List<int> squadrons, int totalLosses)
        {
            if (totalLosses == 0)
                return;

            int remaining = totalLosses;
            int total = squadrons.Sum();

            if (total == 0)
                return;

            for (int i = 0; i < squadrons.Count && remaining > 0; i++)
            {
                if (squadrons[i] == 0)
                    continue;

                int loss = Math.Min((squadrons[i] * totalLosses) / total, remaining);
                squadrons[i] = Math.Max(squadrons[i] - loss, 0);
                remaining -= loss;
            }

            for (int i = 0; i < squadrons.Count && remaining > 0; i++)
            {
                if (squadrons[i] > 0)
                {
                    squadrons[i]--;
                    remaining--;
                }
            }
        }

        /// <summary>
        /// Builds a SpaceCombatResult by comparing post-combat snapshots to initial state.
        /// Records per-ship damage and per-squadron losses.
        /// </summary>
        /// <param name="attackerFleet">The attacking fleet.</param>
        /// <param name="defenderFleet">The defending fleet.</param>
        /// <param name="planet">The planet where combat occurred.</param>
        /// <param name="atkShips">Post-combat attacker ship snapshots.</param>
        /// <param name="defShips">Post-combat defender ship snapshots.</param>
        /// <param name="atkFighters">Post-combat attacker fighter counts.</param>
        /// <param name="defFighters">Post-combat defender fighter counts.</param>
        /// <param name="atkInitialFighters">Pre-combat attacker fighter counts.</param>
        /// <param name="defInitialFighters">Pre-combat defender fighter counts.</param>
        /// <param name="tick">Game tick when combat occurred.</param>
        /// <returns>The fully populated combat result.</returns>
        private static SpaceCombatResult BuildSpaceResult(
            Fleet attackerFleet,
            Fleet defenderFleet,
            Planet planet,
            List<ShipSnap> atkShips,
            List<ShipSnap> defShips,
            List<int> atkFighters,
            List<int> defFighters,
            List<int> atkInitialFighters,
            List<int> defInitialFighters,
            int tick
        )
        {
            SpaceCombatResult result = new SpaceCombatResult
            {
                AttackerFleet = attackerFleet,
                DefenderFleet = defenderFleet,
                Planet = planet,
                Winner = DetermineWinner(atkShips, defShips, atkFighters, defFighters),
                Tick = tick,
            };

            CollectShipDamage(result.ShipDamage, attackerFleet, atkShips);
            CollectShipDamage(result.ShipDamage, defenderFleet, defShips);
            CollectFighterLosses(
                result.FighterLosses,
                attackerFleet,
                atkFighters,
                atkInitialFighters
            );
            CollectFighterLosses(
                result.FighterLosses,
                defenderFleet,
                defFighters,
                defInitialFighters
            );

            return result;
        }

        /// <summary>
        /// Collects hull damage results for all ships in a fleet that took damage.
        /// </summary>
        /// <param name="results">List to add damage results to.</param>
        /// <param name="fleet">The fleet that owns the ships.</param>
        /// <param name="ships">Post-combat ship snapshots.</param>
        private static void CollectShipDamage(
            List<ShipDamageResult> results,
            Fleet fleet,
            List<ShipSnap> ships
        )
        {
            for (int i = 0; i < ships.Count; i++)
            {
                if (ships[i].HullCurrent < ships[i].HullMax)
                {
                    results.Add(
                        new ShipDamageResult
                        {
                            Ship = fleet.CapitalShips[i],
                            HullBefore = ships[i].HullMax,
                            HullAfter = ships[i].HullCurrent,
                        }
                    );
                }
            }
        }

        /// <summary>
        /// Collects fighter loss results for all squadrons that took casualties.
        /// </summary>
        /// <param name="results">List to add loss results to.</param>
        /// <param name="fleet">The fleet that owns the fighters.</param>
        /// <param name="fighters">Post-combat squadron sizes.</param>
        /// <param name="initialFighters">Pre-combat squadron sizes.</param>
        private static void CollectFighterLosses(
            List<FighterLossResult> results,
            Fleet fleet,
            List<int> fighters,
            List<int> initialFighters
        )
        {
            List<Starfighter> allFighters = fleet.GetStarfighters().ToList();
            for (int i = 0; i < fighters.Count; i++)
            {
                if (fighters[i] < initialFighters[i])
                {
                    results.Add(
                        new FighterLossResult
                        {
                            Fighter = i < allFighters.Count ? allFighters[i] : null,
                            SquadsBefore = initialFighters[i],
                            SquadsAfter = fighters[i],
                        }
                    );
                }
            }
        }

        /// <summary>
        /// Applies a space combat result to the game world: updates hull strength,
        /// removes destroyed ships and depleted fighter squadrons, and cleans up empty fleets.
        /// </summary>
        /// <param name="result">The combat result to apply.</param>
        /// <returns>Events generated from ship damage and destruction.</returns>
        private List<GameResult> ApplyCombatResult(SpaceCombatResult result)
        {
            List<GameResult> events = ApplyShipDamage(result.ShipDamage);
            ApplyFighterSquadronLosses(result.FighterLosses);

            if (result.AttackerFleet.CapitalShips.Count == 0)
                RemoveFleetFromScene(result.AttackerFleet);
            if (result.DefenderFleet.CapitalShips.Count == 0)
                RemoveFleetFromScene(result.DefenderFleet);

            return events;
        }

        /// <summary>
        /// Applies ship damage results to the game world.
        /// </summary>
        /// <param name="damageResults">Ship damage results to apply.</param>
        /// <returns>Damage and destruction events for each affected ship.</returns>
        private List<GameResult> ApplyShipDamage(List<ShipDamageResult> damageResults)
        {
            List<GameResult> events = new List<GameResult>();

            foreach (ShipDamageResult damage in damageResults)
            {
                CapitalShip ship = damage.Ship;
                if (ship == null)
                    continue;

                ship.HullDamage = ship.HullStrength - damage.HullAfter;

                events.Add(
                    new GameObjectDamagedResult
                    {
                        GameObject = ship,
                        DamageValue = damage.HullBefore - damage.HullAfter,
                        Tick = _game.CurrentTick,
                    }
                );

                if (damage.HullAfter <= 0)
                {
                    Fleet fleet = ship.GetParentOfType<Fleet>();
                    EvacuateOfficers(ship, fleet);
                    _game.DetachNode(ship);
                    GameLogger.Log($"Ship destroyed: {ship.GetDisplayName()}");
                }
            }

            return events;
        }

        /// <summary>
        /// Applies fighter loss results to the game world.
        /// </summary>
        /// <param name="lossResults">Fighter loss results to apply.</param>
        private void ApplyFighterSquadronLosses(List<FighterLossResult> lossResults)
        {
            foreach (FighterLossResult loss in lossResults)
            {
                Starfighter fighter = loss.Fighter;
                if (fighter == null)
                    continue;

                fighter.SquadronLosses = fighter.SquadronSize - loss.SquadsAfter;

                if (loss.SquadsAfter <= 0)
                {
                    _game.DetachNode(fighter);
                    GameLogger.Log($"Fighter squadron destroyed: {fighter.GetDisplayName()}");
                }
            }
        }

        /// <summary>
        /// Moves officers off a destroyed ship before it is removed from the scene graph.
        /// Prefers another surviving ship in the same fleet; falls back to the nearest friendly planet.
        /// </summary>
        /// <param name="ship">The destroyed ship whose officers need evacuation.</param>
        /// <param name="fleet">The fleet containing the destroyed ship.</param>
        private void EvacuateOfficers(CapitalShip ship, Fleet fleet)
        {
            List<Officer> officers = ship.Officers.ToList();
            if (officers.Count == 0)
                return;

            CapitalShip survivingShip = fleet.CapitalShips.FirstOrDefault(s =>
                !ReferenceEquals(s, ship) && s.GetCurrentHull() > 0
            );

            foreach (Officer officer in officers)
            {
                if (survivingShip != null)
                {
                    _game.DetachNode(officer);
                    _game.AttachNode(officer, survivingShip);
                    GameLogger.Log(
                        $"{officer.GetDisplayName()} evacuated to {survivingShip.GetDisplayName()} after {ship.GetDisplayName()} destroyed."
                    );
                }
                else
                {
                    _movement.EvacuateToNearestFriendlyPlanet(officer);
                }
            }
        }

        /// <summary>
        /// Removes a fleet with no remaining capital ships from the scene graph.
        /// </summary>
        /// <param name="fleet">The empty fleet to remove.</param>
        private void RemoveFleetFromScene(Fleet fleet)
        {
            _game.DetachNode(fleet);
            GameLogger.Log($"Fleet destroyed: {fleet.GetDisplayName()}");
        }

        /// <summary>
        /// Executes a planetary assault against a defending planet.
        /// Pipeline: calculate assault strength, compare to defense, roll for success,
        /// execute capital strikes on defensive buildings, transfer ownership if defenses fall.
        /// </summary>
        /// <param name="attackingFleets">Fleets performing the assault.</param>
        /// <param name="defendingPlanet">Planet being assaulted.</param>
        /// <returns>The result of the assault.</returns>
        public PlanetaryAssaultResult ExecutePlanetaryAssault(
            List<Fleet> attackingFleets,
            Planet defendingPlanet
        )
        {
            PlanetaryAssaultResult result = new PlanetaryAssaultResult
            {
                Planet = defendingPlanet,
                Tick = _game.CurrentTick,
            };

            if (attackingFleets?.Any() != true)
            {
                result.Success = false;
                return result;
            }

            string attackerFactionID = attackingFleets[0].GetOwnerInstanceID();
            if (!attackingFleets.All(f => f.GetOwnerInstanceID() == attackerFactionID))
            {
                GameLogger.Warning(
                    "ExecutePlanetaryAssault: attacking fleets belong to different factions"
                );
                result.Success = false;
                return result;
            }

            result.AttackingFaction = _game.GetFactionByOwnerInstanceID(attackerFactionID);

            // Phase 1: Calculate total assault strength from all fleets
            int totalAssaultStrength = 0;
            foreach (Fleet fleet in attackingFleets)
            {
                int fleetCombatValue = fleet.GetCombatValue();
                totalAssaultStrength += CalculateFleetAssaultStrength(fleet, fleetCombatValue);
            }
            result.AssaultStrength = totalAssaultStrength;

            // Phase 2: Calculate total defense strength
            int totalDefenseStrength = defendingPlanet.GetDefenseStrength();
            result.DefenseStrength = totalDefenseStrength;

            // Phase 3: Check if assault strength exceeds defense
            if (totalAssaultStrength <= totalDefenseStrength)
            {
                result.Success = false;
                return result;
            }

            int excessAssaultStrength = totalAssaultStrength - totalDefenseStrength;

            // Phase 4: Dice roll for success.
            // Matches original FUN_0058c580 (execute_capital_ship_assault_stage):
            // roll_dice(laneCount) returns [0, laneCount], succeed if roll < 1.
            // Probability: 1 / (laneCount + 1).
            int energyResistance = _game.Config.AI.CapitalShipProduction.EnergyStrikeResistance;
            int allocatedEnergyResistance = _game
                .Config
                .AI
                .CapitalShipProduction
                .AllocatedEnergyStrikeResistance;
            List<AssaultLane> initialLanes = BuildAssaultLanes(
                defendingPlanet,
                energyResistance,
                allocatedEnergyResistance
            );
            int laneCount = initialLanes.Count;

            if (laneCount == 0)
            {
                // Nothing to strike — assault fails
                result.Success = false;
                return result;
            }

            int assaultRoll = _provider.NextInt(0, laneCount + 1);
            if (assaultRoll >= 1)
            {
                result.Success = false;
                return result;
            }

            // Phase 5: Execute capital strikes
            result.Success = true;
            ExecuteCapitalStrikes(
                defendingPlanet,
                excessAssaultStrength,
                energyResistance,
                allocatedEnergyResistance,
                result
            );

            // Phase 6: Transfer ownership if planet wiped out
            // Original sets a "planet wiped out" flag when re-enumeration finds no targets.
            bool planetWipedOut =
                defendingPlanet.GetAllRegiments().Count == 0
                && defendingPlanet.GetAllBuildings().Count == 0
                && defendingPlanet.EnergyCapacity <= 0;

            if (planetWipedOut)
                TransferPlanetOwnership(defendingPlanet, attackerFactionID, result);

            return result;
        }

        /// <summary>
        /// Calculates fleet assault strength with personnel modifier.
        /// Formula: (personnel / divisor + 1) * fleet_combat_value.
        /// </summary>
        /// <param name="fleet">The fleet to evaluate.</param>
        /// <param name="fleetCombatValue">Pre-calculated combat value of the fleet.</param>
        /// <returns>Assault strength value.</returns>
        private int CalculateFleetAssaultStrength(Fleet fleet, int fleetCombatValue)
        {
            // Original (FUN_004fc950): searches for rank 2 (General) only, no fallback.
            Officer commander = fleet
                .GetOfficers()
                .FirstOrDefault(o => o.CurrentRank == OfficerRank.General);
            int personnel = commander?.GetSkillValue(MissionParticipantSkill.Leadership) ?? 0;
            int divisor = _game.Config.Combat.AssaultPersonnelDivisor;
            return (personnel / divisor + 1) * fleetCombatValue;
        }

        /// <summary>
        /// Executes capital strikes using lane-based targeting from the original disassembly
        /// (FUN_0058c580). First performs an initial pre-loop building strike, then runs
        /// the main loop for excess_assault iterations with lane-based targeting.
        /// </summary>
        /// <param name="planet">The planet under assault.</param>
        /// <param name="excessAssaultStrength">Assault strength exceeding defense (main loop iterations).</param>
        /// <param name="energyResistance">Resistance value for the energy lane (GENERAL_PARAM_1540).</param>
        /// <param name="allocatedEnergyResistance">Resistance value for allocated energy lane (GENERAL_PARAM_1541).</param>
        /// <param name="result">Result object to record destroyed targets.</param>
        private void ExecuteCapitalStrikes(
            Planet planet,
            int excessAssaultStrength,
            int energyResistance,
            int allocatedEnergyResistance,
            PlanetaryAssaultResult result
        )
        {
            int thresholdLow = _game.Config.Combat.BombardmentStrikeThresholdLow;
            int thresholdHigh = _game.Config.Combat.BombardmentStrikeThresholdHigh;

            // A) Initial pre-loop strike — targets defense/production buildings ONLY
            List<Building> buildings = planet
                .GetAllBuildings()
                .Where(IsAssaultTargetBuilding)
                .ToList();
            if (buildings.Count > 0)
            {
                int buildingIndex = _provider.NextInt(0, buildings.Count);
                Building target = buildings[buildingIndex];
                int resistance = target.Bombardment;
                int roll = _provider.NextInt(thresholdLow, thresholdHigh + 1);

                if (resistance < roll)
                {
                    result.DestroyedBuildings.Add(target);
                    _game.DetachNode(target);
                    GameLogger.Log(
                        $"Initial capital strike destroyed {target.GetDisplayName()} at {planet.GetDisplayName()}"
                    );
                }
            }

            // B) Main loop — runs excessAssaultStrength iterations
            for (int i = 0; i < excessAssaultStrength; i++)
            {
                List<AssaultLane> lanes = BuildAssaultLanes(
                    planet,
                    energyResistance,
                    allocatedEnergyResistance
                );
                if (lanes.Count == 0)
                    break;

                int laneIndex = _provider.NextInt(0, lanes.Count);
                AssaultLane lane = lanes[laneIndex];

                int strikeRoll = _provider.NextInt(thresholdLow, thresholdHigh + 1);
                if (lane.Resistance >= strikeRoll)
                    continue;

                ApplyAssaultStrike(planet, lane, result);
            }
        }

        /// <summary>
        /// Determines whether a building can be targeted by a capital-ship assault strike.
        /// Original enumerates defense (types 0x22–0x27) and production (0x28–0x2f) facilities;
        /// excludes the "Weapon" BuildingType, which is a planet-side fortress weapon category
        /// not covered by any assault-target range.
        /// </summary>
        private static bool IsAssaultTargetBuilding(Building building)
        {
            BuildingType type = building.GetBuildingType();
            return type == BuildingType.Defense
                || type == BuildingType.Mine
                || type == BuildingType.Refinery
                || type == BuildingType.Shipyard
                || type == BuildingType.TrainingFacility
                || type == BuildingType.ConstructionFacility;
        }

        /// <summary>
        /// Builds the list of assault target lanes for one strike iteration.
        /// Matches original FUN_0058b1e0 (enumerate_capital_strike_target_lanes):
        /// enumerates troops, defense facilities (types 0x22–0x27), production facilities
        /// (types 0x28–0x2f), energy, and allocated energy. HQ (types 0x20–0x21) is not
        /// represented as a Building in this port. Starfighters are handled during fleet
        /// combat before assault.
        /// </summary>
        /// <param name="planet">The planet being assaulted.</param>
        /// <param name="energyResistance">Resistance value for energy lanes (GENERAL_PARAM_1540).</param>
        /// <param name="allocatedEnergyResistance">Resistance value for allocated energy lanes (GENERAL_PARAM_1541).</param>
        /// <returns>List of available assault lanes.</returns>
        private static List<AssaultLane> BuildAssaultLanes(
            Planet planet,
            int energyResistance,
            int allocatedEnergyResistance
        )
        {
            List<AssaultLane> lanes = new List<AssaultLane>();

            List<Regiment> regiments = planet.GetAllRegiments();
            for (int i = 0; i < regiments.Count; i++)
            {
                lanes.Add(
                    new AssaultLane
                    {
                        Type = AssaultLaneType.Troop,
                        Resistance = regiments[i].BombardmentDefense,
                        TargetIndex = i,
                    }
                );
            }

            List<Building> buildings = planet.GetAllBuildings();
            for (int i = 0; i < buildings.Count; i++)
            {
                if (!IsAssaultTargetBuilding(buildings[i]))
                    continue;

                lanes.Add(
                    new AssaultLane
                    {
                        Type = AssaultLaneType.Building,
                        Resistance = buildings[i].Bombardment,
                        TargetIndex = i,
                    }
                );
            }

            if (planet.EnergyCapacity > 0)
            {
                lanes.Add(
                    new AssaultLane
                    {
                        Type = AssaultLaneType.Energy,
                        Resistance = energyResistance,
                        TargetIndex = 0,
                    }
                );
            }

            if (planet.GetEnergyUsed() > 0)
            {
                lanes.Add(
                    new AssaultLane
                    {
                        Type = AssaultLaneType.EnergyAllocated,
                        Resistance = allocatedEnergyResistance,
                        TargetIndex = 0,
                    }
                );
            }

            return lanes;
        }

        /// <summary>
        /// Applies a single assault strike to the target identified by the lane.
        /// Destroys the target unit or reduces energy capacity.
        /// </summary>
        /// <param name="planet">The planet being assaulted.</param>
        /// <param name="lane">The lane that was hit.</param>
        /// <param name="result">Result object to record the strike.</param>
        private void ApplyAssaultStrike(
            Planet planet,
            AssaultLane lane,
            PlanetaryAssaultResult result
        )
        {
            switch (lane.Type)
            {
                case AssaultLaneType.Troop:
                {
                    List<Regiment> regiments = planet.GetAllRegiments();
                    if (lane.TargetIndex < regiments.Count)
                    {
                        Regiment target = regiments[lane.TargetIndex];
                        result.DestroyedRegiments.Add(target);
                        _game.DetachNode(target);
                        GameLogger.Log(
                            $"Assault strike destroyed regiment {target.GetDisplayName()} at {planet.GetDisplayName()}"
                        );
                    }
                    break;
                }
                case AssaultLaneType.Building:
                {
                    List<Building> buildings = planet.GetAllBuildings();
                    if (lane.TargetIndex < buildings.Count)
                    {
                        Building target = buildings[lane.TargetIndex];
                        result.DestroyedBuildings.Add(target);
                        _game.DetachNode(target);
                        GameLogger.Log(
                            $"Assault strike destroyed building {target.GetDisplayName()} at {planet.GetDisplayName()}"
                        );
                    }
                    break;
                }
                case AssaultLaneType.Energy:
                {
                    if (planet.EnergyCapacity > 0)
                    {
                        planet.EnergyCapacity--;
                        result.EnergyDamage++;
                        GameLogger.Log(
                            $"Assault strike reduced energy capacity at {planet.GetDisplayName()}"
                        );
                    }
                    break;
                }
                case AssaultLaneType.EnergyAllocated:
                {
                    // Modeled as reducing EnergyCapacity (same practical effect as Energy lane;
                    // original decrements a separate "energy in use" counter, but our model
                    // derives energy used from Buildings.Count, so we reduce capacity instead).
                    if (planet.EnergyCapacity > 0)
                    {
                        planet.EnergyCapacity--;
                        result.EnergyDamage++;
                        GameLogger.Log(
                            $"Assault strike reduced allocated energy at {planet.GetDisplayName()}"
                        );
                    }
                    break;
                }
            }
        }

        /// <summary>
        /// Transfers planet ownership to the attacking faction via PlanetaryControlSystem.
        /// </summary>
        /// <param name="planet">The planet changing hands.</param>
        /// <param name="newOwnerID">The faction taking ownership.</param>
        /// <param name="result">Result object to record the ownership change.</param>
        private void TransferPlanetOwnership(
            Planet planet,
            string newOwnerID,
            PlanetaryAssaultResult result
        )
        {
            Faction newOwner = _game.GetFactionByOwnerInstanceID(newOwnerID);
            _ownership.TransferPlanet(planet, newOwner);

            result.OwnershipChanged = true;
            result.NewOwner = newOwner;

            GameLogger.Log($"Planet {planet.GetDisplayName()} captured by {newOwner.DisplayName}");
        }

        /// <summary>
        /// Executes orbital bombardment against a planet.
        /// Pipeline: shield gate, fleet strength calculation,
        /// defense value, net strikes, strike loop with lane targeting, popular support shift.
        /// </summary>
        /// <param name="attackingFleets">Fleets performing the bombardment.</param>
        /// <param name="targetPlanet">Planet being bombarded.</param>
        /// <returns>The bombardment result with strike events and destruction lists.</returns>
        public BombardmentResult ExecuteOrbitalBombardment(
            List<Fleet> attackingFleets,
            Planet targetPlanet
        )
        {
            BombardmentResult result = new BombardmentResult
            {
                Planet = targetPlanet,
                Tick = _game.CurrentTick,
            };

            if (attackingFleets?.Any() != true)
                return result;

            string attackerFactionID = attackingFleets[0].GetOwnerInstanceID();
            if (!attackingFleets.All(f => f.GetOwnerInstanceID() == attackerFactionID))
            {
                GameLogger.Warning(
                    "ExecuteOrbitalBombardment: attacking fleets belong to different factions"
                );
                return result;
            }
            result.AttackingFaction = _game.GetFactionByOwnerInstanceID(attackerFactionID);

            // Shield gate
            if (IsShieldBlocking(targetPlanet))
            {
                result.ShieldBlocked = true;
                GameLogger.Log(
                    $"Bombardment blocked by shields at {targetPlanet.GetDisplayName()}"
                );
                return result;
            }

            // Fleet bombardment strength
            int fleetStrength = CalculateBombardmentStrength(attackingFleets);
            result.FleetBombardmentStrength = fleetStrength;

            // Defense value from KDY/LNR class facilities
            int defenseValue = CalculatePlanetaryDefenseValue(targetPlanet);
            result.PlanetaryDefenseValue = defenseValue;

            int netStrikes = fleetStrength - defenseValue;
            result.NetStrikes = netStrikes;
            if (netStrikes <= 0)
                return result;

            // Execute strikes
            ExecuteBombardmentStrikes(targetPlanet, netStrikes, result);

            // TODO: Death Star orbital strike applies OrbitalStrikeSupportShift (GENERAL_PARAM_7705)
            // to the target system's popular support. Regular bombardment does NOT shift support
            // directly — the player-visible effect comes from the per-tick uprising/support recalc
            // reacting to destroyed garrison units.

            return result;
        }

        /// <summary>
        /// Checks if the planet has enough shield facilities to block orbital bombardment.
        /// </summary>
        /// <param name="planet">The planet to check.</param>
        /// <returns>True if shields block bombardment.</returns>
        private bool IsShieldBlocking(Planet planet)
        {
            int shieldCount = planet
                .GetAllBuildings()
                .Count(b =>
                    b.DefenseFacilityClass == DefenseFacilityClass.Shield
                    || b.DefenseFacilityClass == DefenseFacilityClass.DeathStarShield
                );
            return shieldCount >= _game.Config.Combat.BombardmentShieldBlockThreshold;
        }

        /// <summary>
        /// Calculates total bombardment strength across all attacking fleets.
        /// Formula per ship: (commander_leadership / divisor + 1) * ship.Bombardment.
        /// </summary>
        /// <param name="fleets">The attacking fleets.</param>
        /// <returns>Total bombardment strength.</returns>
        private int CalculateBombardmentStrength(List<Fleet> fleets)
        {
            int divisor = _game.Config.Combat.AssaultPersonnelDivisor;
            int strength = 0;

            foreach (Fleet fleet in fleets)
            {
                // Original (FUN_004fc950): searches for rank 2 (General) only, no fallback.
                Officer commander = fleet
                    .GetOfficers()
                    .FirstOrDefault(o => o.CurrentRank == OfficerRank.General);
                int personnel = commander?.GetSkillValue(MissionParticipantSkill.Leadership) ?? 0;

                foreach (CapitalShip ship in fleet.CapitalShips)
                {
                    strength += (personnel / divisor + 1) * ship.Bombardment;
                }
            }

            return strength;
        }

        /// <summary>
        /// Calculates planetary defense value from KDY/LNR class facilities.
        /// </summary>
        /// <param name="planet">The planet to evaluate.</param>
        /// <returns>Total defense value (sum of WeaponStrength).</returns>
        private static int CalculatePlanetaryDefenseValue(Planet planet)
        {
            return planet
                .GetAllBuildings()
                .Where(b =>
                    b.DefenseFacilityClass == DefenseFacilityClass.KDY
                    || b.DefenseFacilityClass == DefenseFacilityClass.LNR
                )
                .Sum(b => b.WeaponStrength);
        }

        /// <summary>
        /// Executes the bombardment strike loop. Each iteration rebuilds the lane list,
        /// picks a random lane, rolls against its resistance, and
        /// applies the strike if the roll exceeds resistance.
        /// </summary>
        /// <param name="planet">The planet being bombarded.</param>
        /// <param name="netStrikes">Number of strike iterations to execute.</param>
        /// <param name="result">Result object to record strikes.</param>
        private void ExecuteBombardmentStrikes(
            Planet planet,
            int netStrikes,
            BombardmentResult result
        )
        {
            int thresholdLow = _game.Config.Combat.BombardmentStrikeThresholdLow;
            int thresholdHigh = _game.Config.Combat.BombardmentStrikeThresholdHigh;
            int energyResistance = _game.Config.Combat.BombardmentEnergyResistance;

            for (int i = 0; i < netStrikes; i++)
            {
                List<BombardmentLane> lanes = BuildBombardmentLanes(planet, energyResistance);
                if (lanes.Count == 0)
                    break;

                int laneIndex = _provider.NextInt(0, lanes.Count);
                BombardmentLane lane = lanes[laneIndex];

                int roll = _provider.NextInt(thresholdLow, thresholdHigh + 1);
                if (lane.Resistance >= roll)
                    continue;

                ApplyBombardmentStrike(planet, lane, result);
            }
        }

        /// <summary>
        /// Builds the list of bombardment target lanes for one strike iteration.
        /// Each regiment, starfighter, energy point, and building is a separate lane
        /// with its own resistance value.
        /// </summary>
        /// <param name="planet">The planet being bombarded.</param>
        /// <param name="energyResistance">Config-driven resistance value for energy lanes.</param>
        /// <returns>List of available bombardment lanes.</returns>
        private static List<BombardmentLane> BuildBombardmentLanes(
            Planet planet,
            int energyResistance
        )
        {
            List<BombardmentLane> lanes = new List<BombardmentLane>();

            List<Regiment> regiments = planet.GetAllRegiments();
            for (int i = 0; i < regiments.Count; i++)
            {
                lanes.Add(
                    new BombardmentLane
                    {
                        Type = BombardmentLaneType.Troop,
                        Resistance = regiments[i].BombardmentDefense,
                        TargetIndex = i,
                    }
                );
            }

            List<Starfighter> fighters = planet.GetAllStarfighters();
            for (int i = 0; i < fighters.Count; i++)
            {
                lanes.Add(
                    new BombardmentLane
                    {
                        Type = BombardmentLaneType.Starfighter,
                        Resistance = fighters[i].ShieldStrength,
                        TargetIndex = i,
                    }
                );
            }

            if (planet.EnergyCapacity > 0)
            {
                lanes.Add(
                    new BombardmentLane
                    {
                        Type = BombardmentLaneType.Energy,
                        Resistance = energyResistance,
                        TargetIndex = 0,
                    }
                );
            }

            List<Building> buildings = planet.GetAllBuildings();
            for (int i = 0; i < buildings.Count; i++)
            {
                lanes.Add(
                    new BombardmentLane
                    {
                        Type = BombardmentLaneType.Building,
                        Resistance = buildings[i].Bombardment,
                        TargetIndex = i,
                    }
                );
            }

            return lanes;
        }

        /// <summary>
        /// Applies a single bombardment strike to the target identified by the lane.
        /// Destroys the target unit or reduces energy capacity.
        /// </summary>
        /// <param name="planet">The planet being bombarded.</param>
        /// <param name="lane">The lane that was hit.</param>
        /// <param name="result">Result object to record the strike.</param>
        private void ApplyBombardmentStrike(
            Planet planet,
            BombardmentLane lane,
            BombardmentResult result
        )
        {
            BombardmentStrikeEvent strike = new BombardmentStrikeEvent { Lane = lane.Type };

            switch (lane.Type)
            {
                case BombardmentLaneType.Troop:
                {
                    List<Regiment> regiments = planet.GetAllRegiments();
                    if (lane.TargetIndex < regiments.Count)
                    {
                        Regiment target = regiments[lane.TargetIndex];
                        strike.Target = target;
                        strike.TargetName = target.GetDisplayName();
                        result.DestroyedRegiments.Add(target);
                        _game.DetachNode(target);
                    }
                    break;
                }
                case BombardmentLaneType.Starfighter:
                {
                    List<Starfighter> fighters = planet.GetAllStarfighters();
                    if (lane.TargetIndex < fighters.Count)
                    {
                        Starfighter target = fighters[lane.TargetIndex];
                        strike.Target = target;
                        strike.TargetName = target.GetDisplayName();
                        result.DestroyedStarfighters.Add(target);
                        _game.DetachNode(target);
                    }
                    break;
                }
                case BombardmentLaneType.Energy:
                {
                    if (planet.EnergyCapacity > 0)
                    {
                        planet.EnergyCapacity--;
                        result.EnergyDamage++;
                        strike.TargetName = "System Energy";
                    }
                    break;
                }
                case BombardmentLaneType.Building:
                {
                    List<Building> buildings = planet.GetAllBuildings();
                    if (lane.TargetIndex < buildings.Count)
                    {
                        Building target = buildings[lane.TargetIndex];
                        strike.Target = target;
                        strike.TargetName = target.GetDisplayName();
                        result.DestroyedBuildings.Add(target);
                        _game.DetachNode(target);
                    }
                    break;
                }
            }

            result.Strikes.Add(strike);
        }

        /// <summary>
        /// Mutable snapshot of one capital ship hull for the duration of a single space battle.
        /// </summary>
        private class ShipSnap
        {
            public int HullCurrent;
            public int HullMax;

            /// <summary>Shield recharge allocation (0-15).</summary>
            public int ShieldNibble;

            /// <summary>Weapon recharge allocation (0-15).</summary>
            public int WeaponNibble;

            public bool Alive;
        }

        /// <summary>
        /// A single bombardment target lane with its resistance value and index.
        /// </summary>
        private class BombardmentLane
        {
            public BombardmentLaneType Type;
            public int Resistance;
            public int TargetIndex;
        }

        /// <summary>
        /// A single assault target lane with its resistance value and index.
        /// </summary>
        private class AssaultLane
        {
            public AssaultLaneType Type;
            public int Resistance;
            public int TargetIndex;
        }
    }
}
