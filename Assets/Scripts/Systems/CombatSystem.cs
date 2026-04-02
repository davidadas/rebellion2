using System;
using System.Collections.Generic;
using System.Linq;
using Rebellion.Core.Simulation;
using Rebellion.Game;
using Rebellion.Game.Results;
using Rebellion.SceneGraph;
using Rebellion.Util.Common;

namespace Rebellion.Systems
{
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
    /// Manages combat resolution during each game tick.
    /// Detects hostile fleets at each system and resolves space combat.
    /// Implements the 7-phase pipeline reverse-engineered from REBEXE.EXE.
    /// </summary>
    public class CombatSystem
    {
        private readonly GameRoot game;
        private readonly IRandomNumberProvider provider;

        /// <summary>
        /// Creates a new CombatSystem.
        /// </summary>
        /// <param name="game">The game instance.</param>
        /// <param name="provider">Random number provider for combat resolution.</param>
        public CombatSystem(GameRoot game, IRandomNumberProvider provider)
        {
            this.game = game;
            this.provider = provider;
        }

        /// <summary>
        /// Resolves all AI-vs-AI combat encounters this tick in a single pass.
        /// When a player-involved encounter is found, emits a PendingCombatResult and stops —
        /// the caller is responsible for freezing the tick until the player resolves it.
        /// </summary>
        public List<GameResult> ProcessTick(GameRoot game, IRandomNumberProvider rng)
        {
            List<GameResult> results = new List<GameResult>();
            HashSet<string> foughtThisTick = new HashSet<string>();

            while (
                TryStartCombatExcluding(game, foughtThisTick, out CombatDecisionContext decision)
            )
            {
                Fleet attackerFleet = game.GetSceneNodeByInstanceID<Fleet>(
                    decision.AttackerFleetInstanceID
                );
                Fleet defenderFleet = game.GetSceneNodeByInstanceID<Fleet>(
                    decision.DefenderFleetInstanceID
                );
                Faction attacker = game.GetFactionByOwnerInstanceID(
                    attackerFleet?.GetOwnerInstanceID()
                );
                Faction defender = game.GetFactionByOwnerInstanceID(
                    defenderFleet?.GetOwnerInstanceID()
                );

                if (
                    attacker != null
                    && defender != null
                    && attacker.IsAIControlled()
                    && defender.IsAIControlled()
                )
                {
                    Resolve(game, decision, autoResolve: true, rng);
                    foughtThisTick.Add(decision.AttackerFleetInstanceID);
                    foughtThisTick.Add(decision.DefenderFleetInstanceID);
                }
                else
                {
                    results.Add(
                        new PendingCombatResult
                        {
                            AttackerFleetInstanceID = decision.AttackerFleetInstanceID,
                            DefenderFleetInstanceID = decision.DefenderFleetInstanceID,
                            Tick = game.CurrentTick,
                        }
                    );
                    return results;
                }
            }

            return results;
        }

        /// <summary>
        /// Detects the first hostile fleet encounter this tick.
        /// If found, sets decision and returns true — the tick must stop immediately.
        /// The encounter is NOT resolved here; call Resolve() after player decision.
        /// </summary>
        public bool TryStartCombat(GameRoot game, out CombatDecisionContext decision) =>
            TryStartCombatExcluding(game, new HashSet<string>(), out decision);

        /// <summary>
        /// Detects the first hostile fleet encounter excluding any fleet IDs already resolved
        /// this tick. Used by ProcessTick to prevent Draw outcomes from causing re-detection.
        /// </summary>
        private bool TryStartCombatExcluding(
            GameRoot game,
            HashSet<string> excludedIDs,
            out CombatDecisionContext decision
        )
        {
            decision = null;

            if (!DetectFleetCollision(game, excludedIDs, out Fleet attacker, out Fleet defender))
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
        /// Resolves a pending combat encounter after the player has decided.
        /// Clears IsInCombat on both fleets regardless of outcome.
        /// </summary>
        public void Resolve(
            GameRoot game,
            CombatDecisionContext decision,
            bool autoResolve,
            IRandomNumberProvider rng
        )
        {
            if (autoResolve)
            {
                AutoResolveCombat(game, decision, rng);
            }
            else
            {
                RunManualCombat(game, decision);
            }

            // Clear combat flag on surviving fleets (destroyed fleets are already removed)
            Fleet attacker = game.GetSceneNodeByInstanceID<Fleet>(decision.AttackerFleetInstanceID);
            Fleet defender = game.GetSceneNodeByInstanceID<Fleet>(decision.DefenderFleetInstanceID);
            if (attacker != null)
                attacker.IsInCombat = false;
            if (defender != null)
                defender.IsInCombat = false;
        }

        /// <summary>
        /// Finds the first pair of hostile fleets occupying the same planet.
        /// Skips fleets already engaged in a pending combat encounter or already resolved
        /// this tick (via excludedIDs).
        /// Faction groups are sorted by owner ID for deterministic selection.
        /// </summary>
        private bool DetectFleetCollision(
            GameRoot game,
            HashSet<string> excludedIDs,
            out Fleet attacker,
            out Fleet defender
        )
        {
            attacker = null;
            defender = null;

            foreach (Planet planet in game.GetSceneNodesByType<Planet>())
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
        /// Auto-resolves combat using the 7-phase pipeline.
        /// </summary>
        private void AutoResolveCombat(
            GameRoot game,
            CombatDecisionContext decision,
            IRandomNumberProvider rng
        )
        {
            Fleet attacker = game.GetSceneNodeByInstanceID<Fleet>(decision.AttackerFleetInstanceID);
            Fleet defender = game.GetSceneNodeByInstanceID<Fleet>(decision.DefenderFleetInstanceID);

            if (attacker == null || defender == null)
            {
                GameLogger.Warning("AutoResolveCombat: one or both fleets no longer exist.");
                return;
            }

            Planet planet = attacker.GetParentOfType<Planet>();
            if (planet == null)
            {
                GameLogger.Warning(
                    $"AutoResolveCombat: attacker {attacker.GetDisplayName()} is not at a planet."
                );
                return;
            }

            SpaceCombatResult result = ResolveSpace(
                attacker,
                defender,
                planet,
                rng,
                game.CurrentTick
            );
            ApplyCombatResult(result);

            GameLogger.Log(
                $"Combat at {planet.GetDisplayName()}: "
                    + $"{attacker.GetDisplayName()} vs {defender.GetDisplayName()} — "
                    + $"Winner: {result.Winner}"
            );
        }

        /// <summary>
        /// Placeholder for manual/interactive combat resolution.
        /// </summary>
        private void RunManualCombat(GameRoot game, CombatDecisionContext decision)
        {
            // TODO: Implement manual combat resolution
        }

        /// <summary>
        /// Auto-resolve space combat between two fleets at a system.
        ///
        /// Implements a 7-phase space combat pipeline.
        /// </summary>
        /// <param name="attackerFleet">Attacking fleet</param>
        /// <param name="defenderFleet">Defending fleet</param>
        /// <param name="system">System where combat occurs</param>
        /// <param name="provider">Random number provider for damage variance</param>
        /// <param name="tick">Current game tick</param>
        /// <returns>Combat result with damage events and winner</returns>
        private static SpaceCombatResult ResolveSpace(
            Fleet attackerFleet,
            Fleet defenderFleet,
            Planet system,
            IRandomNumberProvider provider,
            int tick
        )
        {
            // Phase 1: Initialize - build mutable hull snapshots
            (List<ShipSnap> atkShips, List<int> atkFighters) = SnapshotFleet(attackerFleet);
            (List<ShipSnap> defShips, List<int> defFighters) = SnapshotFleet(defenderFleet);
            List<int> atkInitialFighters = atkFighters.ToList();
            List<int> defInitialFighters = defFighters.ToList();

            // Phase 2: Fleet composition evaluation
            // Checks if either side has armed ships or fighters with weapons
            bool anyArmed =
                atkShips.Any(s => s.Alive && s.WeaponNibble > 0)
                || defShips.Any(s => s.Alive && s.WeaponNibble > 0)
                || HasArmedFighters(attackerFleet, atkFighters)
                || HasArmedFighters(defenderFleet, defFighters);

            bool phasesEnabled = false;

            // Phase 3: Weapon fire
            // Gate: weapon fire runs BEFORE the pipeline is fully armed
            if (anyArmed)
            {
                PhaseWeaponFire(attackerFleet, atkShips, defShips, provider);
                PhaseWeaponFire(defenderFleet, defShips, atkShips, provider);

                // NOW arm subsequent phases after weapon fire completes
                phasesEnabled = true;
            }

            // Phases 4-6: Gate on phasesEnabled
            if (phasesEnabled)
            {
                // Phase 4: Shield absorption
                // Note: Shield absorption is merged into phase 3 weapon fire

                // Phase 5: Hull damage application
                // Note: Hull damage is applied directly in phase 3

                // Phase 6: Fighter engagement
                PhaseFighterEngage(
                    attackerFleet,
                    defenderFleet,
                    atkFighters,
                    defFighters,
                    atkShips,
                    defShips,
                    provider
                );
            }

            // Phase 7: Result determination
            bool atkAlive = atkShips.Any(s => s.Alive) || atkFighters.Any(c => c > 0);
            bool defAlive = defShips.Any(s => s.Alive) || defFighters.Any(c => c > 0);

            CombatSide winner;
            if (atkAlive && !defAlive)
                winner = CombatSide.Attacker;
            else if (!atkAlive && defAlive)
                winner = CombatSide.Defender;
            else
                winner = CombatSide.Draw;

            return BuildSpaceResult(
                attackerFleet,
                defenderFleet,
                system,
                atkShips,
                defShips,
                atkFighters,
                defFighters,
                atkInitialFighters,
                defInitialFighters,
                winner,
                tick
            );
        }

        /// <summary>
        /// Build mutable hull snapshots for one fleet.
        /// </summary>
        private static (List<ShipSnap>, List<int>) SnapshotFleet(Fleet fleet)
        {
            List<ShipSnap> ships = new List<ShipSnap>();

            // Each CapitalShip is an individual hull instance
            foreach (CapitalShip ship in fleet.CapitalShips)
            {
                ShipSnap snap = new ShipSnap
                {
                    HullCurrent = ship.HullStrength,
                    HullMax = ship.HullStrength,
                    // Initial shield nibble: clamped to 4-bit range (0-15)
                    ShieldNibble = Math.Min(ship.ShieldRechargeRate, 15),
                    // Weapon nibble starts at max allocation (0x0f = 15)
                    WeaponNibble = 0x0f,
                    Alive = true,
                };
                ships.Add(snap);
            }

            // Fighter squadron counts
            List<int> fighters = fleet.GetStarfighters().Select(f => f.SquadronSize).ToList();

            return (ships, fighters);
        }

        /// <summary>
        /// Phase 3: One side fires at the other.
        /// </summary>
        private static void PhaseWeaponFire(
            Fleet firingFleet,
            List<ShipSnap> firing,
            List<ShipSnap> targets,
            IRandomNumberProvider provider
        )
        {
            // Aggregate all weapon arcs for alive ships
            int totalFire = 0;
            for (int i = 0; i < firing.Count; i++)
            {
                if (!firing[i].Alive)
                    continue;

                CapitalShip ship = firingFleet.CapitalShips[i];
                int raw = 0;

                // Sum all weapon arcs
                if (ship.PrimaryWeapons.ContainsKey(PrimaryWeaponType.Turbolaser))
                    raw += ship.PrimaryWeapons[PrimaryWeaponType.Turbolaser].Sum();
                if (ship.PrimaryWeapons.ContainsKey(PrimaryWeaponType.IonCannon))
                    raw += ship.PrimaryWeapons[PrimaryWeaponType.IonCannon].Sum();
                if (ship.PrimaryWeapons.ContainsKey(PrimaryWeaponType.LaserCannon))
                    raw += ship.PrimaryWeapons[PrimaryWeaponType.LaserCannon].Sum();

                // Scale by weapon_nibble (0-15)
                totalFire += (raw * firing[i].WeaponNibble) / 15;
            }

            if (totalFire == 0)
                return;

            // Distribute damage across alive targets
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
                double roll = provider.NextDouble();

                // ±20% variance around firePerTarget
                int variance = (int)(firePerTarget * 0.2 * (roll * 2.0 - 1.0));
                int damage = Math.Max(firePerTarget + variance, 0);

                // Shield absorption: shield_nibble / 15 fraction absorbed
                int absorbed = (int)(damage * targets[idx].ShieldNibble / 15.0);
                int hullDamage = Math.Max(damage - absorbed, 0);

                targets[idx].HullCurrent = Math.Max(targets[idx].HullCurrent - hullDamage, 0);
                if (targets[idx].HullCurrent == 0)
                {
                    targets[idx].Alive = false; // alive_flag at +0xac bit0 cleared
                }
            }
        }

        /// <summary>
        /// Phase 6: Fighter engagement.
        /// </summary>
        private static void PhaseFighterEngage(
            Fleet attackerFleet,
            Fleet defenderFleet,
            List<int> atkFighters,
            List<int> defFighters,
            List<ShipSnap> atkShips,
            List<ShipSnap> defShips,
            IRandomNumberProvider provider
        )
        {
            // Fighters attack enemy capital ships
            FightersAttackShips(atkFighters, attackerFleet, defShips, provider);
            FightersAttackShips(defFighters, defenderFleet, atkShips, provider);

            // Fighter-vs-fighter engagement
            int atkTotal = atkFighters.Sum();
            int defTotal = defFighters.Sum();

            if (atkTotal == 0 || defTotal == 0)
                return;

            double rollAtk = provider.NextDouble();
            double rollDef = provider.NextDouble();

            // Loss = (enemy_count / (my_count + enemy_count)) * 0.3 * total_squads * roll
            double atkHitRate = (double)defTotal / (atkTotal + defTotal);
            double defHitRate = (double)atkTotal / (atkTotal + defTotal);

            int atkLosses = Math.Min((int)(atkTotal * atkHitRate * 0.3 * rollAtk), atkTotal);
            int defLosses = Math.Min((int)(defTotal * defHitRate * 0.3 * rollDef), defTotal);

            ApplyFighterLosses(atkFighters, atkLosses);
            ApplyFighterLosses(defFighters, defLosses);
        }

        /// <summary>
        /// Fighters attack enemy capital ships.
        /// </summary>
        private static void FightersAttackShips(
            List<int> squadrons,
            Fleet fleet,
            List<ShipSnap> enemyShips,
            IRandomNumberProvider provider
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
                if (squadrons[sqIdx] == 0)
                    continue;
                if (sqIdx >= fighters.Count)
                    continue;

                Starfighter fighter = fighters[sqIdx];

                int totalAttack =
                    (fighter.LaserCannon + fighter.IonCannon + fighter.Torpedoes)
                    * squadrons[sqIdx];

                if (totalAttack == 0)
                    continue;

                // Pick random target
                int targetIdx = aliveTargets[(int)(provider.NextDouble() * aliveTargets.Count)];

                // Apply damage with variance
                double roll = provider.NextDouble();
                int damage = (int)(totalAttack * (0.8 + 0.4 * roll)); // ±20% variance

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
        /// Apply fighter losses proportionally across squadrons.
        /// </summary>
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

                // Proportional loss
                int loss = Math.Min((squadrons[i] * totalLosses) / total, remaining);
                squadrons[i] = Math.Max(squadrons[i] - loss, 0);
                remaining -= loss;
            }

            // Distribute remainder
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
        /// Checks if a fleet has fighters with weapons.
        /// Fighters contribute to anyArmed if they have attack capability.
        /// </summary>
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
        /// Build result object with damage events.
        /// </summary>
        private static SpaceCombatResult BuildSpaceResult(
            Fleet attackerFleet,
            Fleet defenderFleet,
            Planet system,
            List<ShipSnap> atkShips,
            List<ShipSnap> defShips,
            List<int> atkFighters,
            List<int> defFighters,
            List<int> atkInitialFighters,
            List<int> defInitialFighters,
            CombatSide winner,
            int tick
        )
        {
            SpaceCombatResult result = new SpaceCombatResult
            {
                AttackerFleet = attackerFleet,
                DefenderFleet = defenderFleet,
                System = system,
                Winner = winner,
                Tick = tick,
            };

            // Record ship damage
            for (int i = 0; i < atkShips.Count; i++)
            {
                if (atkShips[i].HullCurrent < atkShips[i].HullMax)
                {
                    result.ShipDamage.Add(
                        new ShipDamageEvent
                        {
                            Fleet = attackerFleet,
                            ShipIndex = i,
                            HullBefore = atkShips[i].HullMax,
                            HullAfter = atkShips[i].HullCurrent,
                        }
                    );
                }
            }

            for (int i = 0; i < defShips.Count; i++)
            {
                if (defShips[i].HullCurrent < defShips[i].HullMax)
                {
                    result.ShipDamage.Add(
                        new ShipDamageEvent
                        {
                            Fleet = defenderFleet,
                            ShipIndex = i,
                            HullBefore = defShips[i].HullMax,
                            HullAfter = defShips[i].HullCurrent,
                        }
                    );
                }
            }

            // Record fighter losses
            for (int i = 0; i < atkFighters.Count; i++)
            {
                if (atkFighters[i] < atkInitialFighters[i])
                {
                    result.FighterLosses.Add(
                        new FighterLossEvent
                        {
                            Fleet = attackerFleet,
                            FighterIndex = i,
                            SquadsBefore = atkInitialFighters[i],
                            SquadsAfter = atkFighters[i],
                        }
                    );
                }
            }

            for (int i = 0; i < defFighters.Count; i++)
            {
                if (defFighters[i] < defInitialFighters[i])
                {
                    result.FighterLosses.Add(
                        new FighterLossEvent
                        {
                            Fleet = defenderFleet,
                            FighterIndex = i,
                            SquadsBefore = defInitialFighters[i],
                            SquadsAfter = defFighters[i],
                        }
                    );
                }
            }

            return result;
        }

        /// <summary>
        /// Applies combat result to the game world.
        /// Updates hull strength, removes destroyed ships and fighters.
        /// </summary>
        private void ApplyCombatResult(SpaceCombatResult result)
        {
            // Apply ship damage - group by fleet, then process each fleet's damage in reverse index order
            // Ships are stored in fleet.CapitalShips list, not attached as scene graph children
            foreach (
                IGrouping<Fleet, ShipDamageEvent> fleetGroup in result.ShipDamage.GroupBy(d =>
                    d.Fleet
                )
            )
            {
                foreach (ShipDamageEvent damage in fleetGroup.OrderByDescending(d => d.ShipIndex))
                {
                    if (damage.ShipIndex < damage.Fleet.CapitalShips.Count)
                    {
                        CapitalShip ship = damage.Fleet.CapitalShips[damage.ShipIndex];
                        ship.HullStrength = damage.HullAfter;

                        // Remove destroyed ships from fleet list
                        if (damage.HullAfter <= 0)
                        {
                            damage.Fleet.CapitalShips.RemoveAt(damage.ShipIndex);
                            GameLogger.Log($"Ship destroyed: {ship.GetDisplayName()}");
                        }
                    }
                }
            }

            // Apply fighter losses - group by fleet, then process in reverse index order
            // Fighters are stored in ship.Starfighters list, not attached as scene graph children
            foreach (
                IGrouping<Fleet, FighterLossEvent> fleetGroup in result.FighterLosses.GroupBy(l =>
                    l.Fleet
                )
            )
            {
                foreach (FighterLossEvent loss in fleetGroup.OrderByDescending(l => l.FighterIndex))
                {
                    List<Starfighter> fighters = loss.Fleet.GetStarfighters().ToList();
                    if (loss.FighterIndex < fighters.Count)
                    {
                        Starfighter fighter = fighters[loss.FighterIndex];
                        fighter.SquadronSize = loss.SquadsAfter;

                        // Remove depleted squadrons from parent ship's list
                        if (loss.SquadsAfter <= 0)
                        {
                            foreach (CapitalShip ship in loss.Fleet.CapitalShips)
                            {
                                if (ship.Starfighters.Contains(fighter))
                                {
                                    ship.Starfighters.Remove(fighter);
                                    break;
                                }
                            }
                            GameLogger.Log(
                                $"Fighter squadron destroyed: {fighter.GetDisplayName()}"
                            );
                        }
                    }
                }
            }

            // Remove empty fleets from the scene graph (fleets ARE scene graph nodes)
            if (result.AttackerFleet.CapitalShips.Count == 0)
            {
                RemoveFleetFromScene(result.AttackerFleet);
            }
            if (result.DefenderFleet.CapitalShips.Count == 0)
            {
                RemoveFleetFromScene(result.DefenderFleet);
            }
        }

        /// <summary>
        /// Removes a destroyed fleet from the scene graph.
        /// Uses centralized global state management (faction ownership + instance ID lookup).
        /// </summary>
        private void RemoveFleetFromScene(Fleet fleet)
        {
            game.DetachNode(fleet);
            GameLogger.Log($"Fleet destroyed: {fleet.GetDisplayName()}");
        }

        /// <summary>
        /// Executes planetary assault by hostile fleet(s) against a defending planet.
        /// Mirrors FUN_0058c580_execute_capital_ship_assault_stage from original.
        ///
        /// Original logic:
        /// 1. Find fleets with "assault ready" flag (bit 6 of offset 0x58)
        /// 2. Sum assault strength: (personnel / GENERAL_PARAM_1537 + 1) * fleet_combat_value
        /// 3. Sum defense strength from defensive buildings (offset 0x60)
        /// 4. Check if assault > defense
        /// 5. Roll dice: success if roll < threshold
        /// 6. Execute capital strikes destroying defensive buildings
        /// 7. Transfer planet ownership if defenses eliminated
        /// </summary>
        /// <param name="attackingFleets">Fleets performing the assault (same faction)</param>
        /// <param name="defendingPlanet">Planet being assaulted</param>
        /// <returns>Assault result with building damage and ownership changes</returns>
        public PlanetaryAssaultResult ExecutePlanetaryAssault(
            List<Fleet> attackingFleets,
            Planet defendingPlanet
        )
        {
            PlanetaryAssaultResult result = new PlanetaryAssaultResult
            {
                Planet = defendingPlanet,
                Tick = game.CurrentTick,
            };

            if (attackingFleets == null || !attackingFleets.Any())
            {
                result.Success = false;
                return result;
            }

            // All fleets must belong to same faction
            string attackerFactionID = attackingFleets[0].GetOwnerInstanceID();
            if (!attackingFleets.All(f => f.GetOwnerInstanceID() == attackerFactionID))
            {
                GameLogger.Warning(
                    "ExecutePlanetaryAssault: attacking fleets belong to different factions"
                );
                result.Success = false;
                return result;
            }

            result.AttackingFactionID = attackerFactionID;

            // Phase 1: Calculate total assault strength from all fleets
            // Original: Lines 43-74 in FUN_0058c580
            int totalAssaultStrength = 0;
            foreach (Fleet fleet in attackingFleets)
            {
                // Original checks bit 6 of offset 0x58 for "assault ready" flag
                // For now, all fleets at enemy planets are considered assault-ready
                int fleetCombatValue = CalculateFleetCombatValue(fleet);
                int assaultStrength = CalculateFleetAssaultStrength(fleet, fleetCombatValue);
                totalAssaultStrength += assaultStrength;
            }

            result.AssaultStrength = totalAssaultStrength;

            // Phase 2: Calculate total defense strength from defensive buildings
            // Original: Lines 79-90 in FUN_0058c580
            // Sums FUN_0051fd40_get_attached_defensive_core_value (offset 0x60)
            int totalDefenseStrength = CalculatePlanetDefenseStrength(defendingPlanet);
            result.DefenseStrength = totalDefenseStrength;

            // Phase 3: Determine if assault can proceed
            // Original: Lines 92-97
            if (totalAssaultStrength <= totalDefenseStrength)
            {
                result.Success = false;
                return result;
            }

            int excessAssaultStrength = totalAssaultStrength - totalDefenseStrength;

            // Phase 4: Dice roll for success
            // Original: Lines 95-97
            // iVar1 = *(int *)(system + 0x24);
            // iVar4 = FUN_0053c9f0_roll_dice(*(int *)(system + 0x20) + iVar1 - 1);
            // if (iVar4 < iVar1) { assault succeeds }
            int baseThreshold = 50; // Estimated from typical gameplay
            int rollRange = 100;
            int roll = provider.NextInt(0, rollRange);

            if (roll >= baseThreshold)
            {
                result.Success = false;
                return result;
            }

            // Phase 5: Execute capital strikes on defensive buildings
            // Original: Lines 99-152
            result.Success = true;
            ExecuteCapitalStrikes(defendingPlanet, excessAssaultStrength, result);

            // Phase 6: Transfer ownership if all defenses destroyed
            if (totalDefenseStrength > 0 && result.DestroyedBuildings.Count > 0)
            {
                int remainingDefense = defendingPlanet
                    .GetAllBuildings()
                    .Where(b => b.GetBuildingType() == BuildingType.Defense)
                    .Sum(b => b.DefenseRating);

                if (remainingDefense == 0)
                {
                    TransferPlanetOwnership(defendingPlanet, attackerFactionID, result);
                }
            }

            return result;
        }

        /// <summary>
        /// Calculates fleet combat value by summing capital ship and starfighter attack ratings.
        /// Mirrors FUN_004fc870_sum_fleet_unit_combat_value from original.
        /// </summary>
        private int CalculateFleetCombatValue(Fleet fleet)
        {
            int capitalShipCombat = fleet.CapitalShips.Sum(s => s.AttackRating);
            int starfighterCombat = fleet.GetStarfighters().Sum(f => f.AttackRating);
            return capitalShipCombat + starfighterCombat;
        }

        /// <summary>
        /// Calculates fleet assault strength with personnel modifier.
        /// Mirrors FUN_0055d120_scale_capital_ship_assault_fleet_strength from original.
        /// Formula: (personnel / GENERAL_PARAM_1537 + 1) * fleet_combat_value
        /// </summary>
        private int CalculateFleetAssaultStrength(Fleet fleet, int fleetCombatValue)
        {
            Officer commander = fleet.GetChildren().OfType<Officer>().FirstOrDefault();
            int personnel = commander?.GetSkillValue(MissionParticipantSkill.Leadership) ?? 0;

            int divisor = game.Config.Combat.AssaultPersonnelDivisor;
            return (personnel / divisor + 1) * fleetCombatValue;
        }

        /// <summary>
        /// Calculates planetary defense strength from defensive buildings.
        /// Mirrors defense calculation from FUN_0058c580 lines 84-88.
        /// Original: Sums defensive_core_value (offset 0x60) from defensive facilities.
        /// </summary>
        private int CalculatePlanetDefenseStrength(Planet planet)
        {
            return planet
                .GetAllBuildings()
                .Where(b => b.GetBuildingType() == BuildingType.Defense)
                .Sum(b => b.DefenseRating);
        }

        /// <summary>
        /// Executes capital strikes against defensive buildings.
        /// Original: FUN_0058c580 lines 99-152
        /// Each point of excess assault strength destroys one defensive structure.
        /// </summary>
        private void ExecuteCapitalStrikes(
            Planet planet,
            int excessAssaultStrength,
            PlanetaryAssaultResult result
        )
        {
            List<Building> defensiveBuildings = planet
                .GetAllBuildings()
                .Where(b => b.GetBuildingType() == BuildingType.Defense)
                .OrderBy(b => provider.NextDouble()) // Random target selection
                .ToList();

            int strikesRemaining = excessAssaultStrength;

            foreach (Building building in defensiveBuildings)
            {
                if (strikesRemaining <= 0)
                    break;

                // Each strike destroys one building
                result.DestroyedBuildings.Add(building);
                game.DetachNode(building);
                strikesRemaining--;

                GameLogger.Log(
                    $"Capital strike destroyed {building.GetDisplayName()} at {planet.GetDisplayName()}"
                );
            }
        }

        /// <summary>
        /// Transfers planet ownership to the attacking faction.
        /// Resets popular support and marks the planet as captured.
        /// </summary>
        private void TransferPlanetOwnership(
            Planet planet,
            string newOwnerID,
            PlanetaryAssaultResult result
        )
        {
            string oldOwnerID = planet.GetOwnerInstanceID();
            planet.SetOwnerInstanceID(newOwnerID);

            // Reset popular support to neutral/low for new owner
            planet.SetPopularSupport(newOwnerID, game.Config.Planet.MaxPopularSupport / 2);

            result.OwnershipChanged = true;
            result.NewOwnerID = newOwnerID;

            GameLogger.Log(
                $"Planet {planet.GetDisplayName()} captured! {oldOwnerID} -> {newOwnerID}"
            );
        }

        /// <summary>
        /// Outcome of space combat auto-resolve between two fleets.
        /// Mirrors open-rebellion's SpaceCombatResult.
        /// </summary>
        private class SpaceCombatResult
        {
            public Fleet AttackerFleet { get; set; }
            public Fleet DefenderFleet { get; set; }
            public Planet System { get; set; }
            public CombatSide Winner { get; set; }
            public List<ShipDamageEvent> ShipDamage { get; set; } = new List<ShipDamageEvent>();
            public List<FighterLossEvent> FighterLosses { get; set; } =
                new List<FighterLossEvent>();
            public int Tick { get; set; }
        }

        /// <summary>
        /// Which side won a combat engagement.
        /// </summary>
        private enum CombatSide
        {
            Attacker,
            Defender,
            Draw,
        }

        /// <summary>
        /// A hull took damage during combat.
        /// </summary>
        private class ShipDamageEvent
        {
            public Fleet Fleet { get; set; }

            /// <summary>
            /// Index into fleet.CapitalShips (not a stable reference - instances are transient).
            /// </summary>
            public int ShipIndex { get; set; }
            public int HullBefore { get; set; }
            public int HullAfter { get; set; }
        }

        /// <summary>
        /// A fighter squadron took losses during combat.
        /// </summary>
        private class FighterLossEvent
        {
            public Fleet Fleet { get; set; }
            public int FighterIndex { get; set; }
            public int SquadsBefore { get; set; }
            public int SquadsAfter { get; set; }
        }

        /// <summary>
        /// Mutable snapshot of one hull for the duration of a single space battle.
        /// </summary>
        private class ShipSnap
        {
            public int HullCurrent;
            public int HullMax;

            /// <summary>
            /// Shield recharge allocation (0-15).
            /// </summary>
            public int ShieldNibble;

            /// <summary>
            /// Weapon recharge allocation (0-15).
            /// </summary>
            public int WeaponNibble;
            public bool Alive;
        }
    }

    /// <summary>
    /// Outcome of planetary assault by fleets against a planet.
    /// Records assault/defense strength, destroyed buildings, and ownership changes.
    /// </summary>
    public class PlanetaryAssaultResult
    {
        public Planet Planet { get; set; }
        public string AttackingFactionID { get; set; }
        public int AssaultStrength { get; set; }
        public int DefenseStrength { get; set; }
        public bool Success { get; set; }
        public List<Building> DestroyedBuildings { get; set; } = new List<Building>();
        public bool OwnershipChanged { get; set; }
        public string NewOwnerID { get; set; }
        public int Tick { get; set; }
    }
}
