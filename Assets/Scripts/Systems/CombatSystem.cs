using System;
using System.Collections.Generic;
using System.Linq;
using Rebellion.Core.Simulation;
using Rebellion.Game;
using Rebellion.SceneGraph;
using Rebellion.Util.Common;

namespace Rebellion.Systems
{
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
        /// Processes combat for the current tick.
        /// Scans each system for hostile fleets and resolves battles.
        /// </summary>
        /// <param name="game">The game instance.</param>
        public void ProcessTick(GameRoot game)
        {
            // Scan all planets for hostile fleets
            IEnumerable<Planet> planets = game.GetSceneNodesByType<Planet>();

            foreach (Planet planet in planets)
            {
                // Get all fleets at this system
                List<Fleet> fleets = planet.GetChildren<Fleet>(f => true, recurse: false).ToList();
                if (fleets.Count < 2)
                    continue;

                // Group fleets by faction
                List<IGrouping<string, Fleet>> factionGroups = fleets
                    .GroupBy(f => f.GetOwnerInstanceID())
                    .Where(g => g.Key != null)
                    .ToList();

                if (factionGroups.Count < 2)
                    continue;

                // Combat occurs between different factions
                // Original behavior: only first fleet per faction fights
                List<Fleet> attackerFleets = factionGroups[0].ToList();
                List<Fleet> defenderFleets = factionGroups[1].ToList();

                if (attackerFleets.Count == 0 || defenderFleets.Count == 0)
                    continue;

                // Take first fleet from each faction (matches original 2-faction combat)
                Fleet attacker = attackerFleets[0];
                Fleet defender = defenderFleets[0];

                // Resolve space combat
                SpaceCombatResult result = ResolveSpace(
                    attacker,
                    defender,
                    planet,
                    provider,
                    game.CurrentTick
                );

                // Apply results
                ApplyCombatResult(result);

                // Log combat outcome
                GameLogger.Log(
                    $"Combat at {planet.GetDisplayName()}: "
                        + $"{attacker.GetDisplayName()} vs {defender.GetDisplayName()} - "
                        + $"Winner: {result.Winner}"
                );
            }
        }

        // -----------------------------------------------------------------------
        // Space combat auto-resolve — 7-phase pipeline
        // -----------------------------------------------------------------------

        /// <summary>
        /// Auto-resolve space combat between two fleets at a system.
        ///
        /// Implements the 7-phase C++ pipeline from FUN_005457f0 → FUN_00549910.
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
            // Phase 1: Initialize - build mutable hull snapshots (FUN_005442f0)
            (List<ShipSnap> atkShips, List<int> atkFighters) = SnapshotFleet(attackerFleet);
            (List<ShipSnap> defShips, List<int> defFighters) = SnapshotFleet(defenderFleet);
            List<int> atkInitialFighters = atkFighters.ToList();
            List<int> defInitialFighters = defFighters.ToList();

            // Phase 2: Fleet composition evaluation (FUN_00544da0, 96 lines)
            // Checks if either side has armed ships or fighters with weapons
            bool anyArmed =
                atkShips.Any(s => s.Alive && s.WeaponNibble > 0)
                || defShips.Any(s => s.Alive && s.WeaponNibble > 0)
                || HasArmedFighters(attackerFleet, atkFighters)
                || HasArmedFighters(defenderFleet, defFighters);

            bool phasesEnabled = false;

            // Phase 3: Weapon fire (FUN_00544030)
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
                // Phase 4: Shield absorption (FUN_00544130, 83 lines, vtable +0x1c8)
                // Note: Shield absorption is merged into phase 3 weapon fire

                // Phase 5: Hull damage application (FUN_005443f0, 54 lines, vtable +0x1d0)
                // Note: Hull damage is applied directly in phase 3

                // Phase 6: Fighter engagement (FUN_005444e0, 53 lines, vtable +0x1d4)
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

            // Phase 7: Result determination (FUN_005445d0, 175 lines)
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

        // -----------------------------------------------------------------------
        // Result types
        // -----------------------------------------------------------------------

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
}
