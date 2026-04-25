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
        CapitalShip,
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
    /// Facade over the three combat resolvers (space combat, planetary assault, orbital
    /// bombardment). Also owns fleet-collision detection.
    /// </summary>
    public class CombatSystem
    {
        private readonly GameRoot _game;
        private readonly SpaceCombatResolver _spaceCombat;
        private readonly PlanetaryAssaultResolver _assault;
        private readonly PlanetaryDefenseCombatResolver _defenseCombat;

        public CombatSystem(
            GameRoot game,
            IRandomNumberProvider provider,
            MovementSystem movement,
            PlanetaryControlSystem ownership
        )
        {
            _game = game;
            if (movement == null)
                throw new ArgumentNullException(nameof(movement));
            if (ownership == null)
                throw new ArgumentNullException(nameof(ownership));
            _spaceCombat = new SpaceCombatResolver(game, provider, movement);
            _assault = new PlanetaryAssaultResolver(game, provider, ownership);
            _defenseCombat = new PlanetaryDefenseCombatResolver(
                game,
                provider,
                movement,
                ownership
            );
        }

        /// <summary>
        /// Resolves all AI-vs-AI combat encounters this tick in a single pass.
        /// When a player-involved encounter is found, emits a PendingCombatResult and stops —
        /// the caller is responsible for freezing the tick until the player resolves it.
        /// </summary>
        public List<GameResult> ProcessTick()
        {
            List<GameResult> results = new List<GameResult>();
            HashSet<string> resolvedFleetIds = new HashSet<string>();

            while (TryStartCombatExcluding(resolvedFleetIds, out CombatDecisionContext decision))
            {
                if (TryAutoResolveAICombat(decision, resolvedFleetIds, results))
                    continue;

                results.Add(BuildPendingCombatResult(decision));
                return results;
            }

            return results;
        }

        /// <summary>
        /// Attempts to auto-resolve a detected combat encounter when both sides are AI-controlled.
        /// </summary>
        /// <param name="decision">The detected encounter to resolve.</param>
        /// <param name="resolvedFleetIds">Set updated with both fleet IDs on successful resolution.</param>
        /// <param name="results">Output list that receives per-ship damage/destruction events.</param>
        /// <returns>True if auto-resolved; false if either side is player-controlled.</returns>
        private bool TryAutoResolveAICombat(
            CombatDecisionContext decision,
            HashSet<string> resolvedFleetIds,
            List<GameResult> results
        )
        {
            if (!BothSidesAIControlled(decision))
                return false;

            SpaceCombatResult combatResult = Resolve(decision, autoResolve: true);
            if (combatResult != null)
                results.AddRange(combatResult.Events);

            resolvedFleetIds.Add(decision.AttackerFleetInstanceID);
            resolvedFleetIds.Add(decision.DefenderFleetInstanceID);
            return true;
        }

        /// <summary>
        /// Returns true when both fleets in the decision belong to AI-controlled factions.
        /// </summary>
        /// <param name="decision">The combat decision context to evaluate.</param>
        /// <returns>True if both sides are AI-controlled; false otherwise.</returns>
        private bool BothSidesAIControlled(CombatDecisionContext decision)
        {
            Faction attacker = GetFleetFaction(decision.AttackerFleetInstanceID);
            Faction defender = GetFleetFaction(decision.DefenderFleetInstanceID);
            return attacker != null
                && defender != null
                && attacker.IsAIControlled()
                && defender.IsAIControlled();
        }

        /// <summary>
        /// Looks up the controlling faction for a fleet by its instance ID.
        /// </summary>
        /// <param name="fleetInstanceId">The fleet's scene-graph instance ID.</param>
        /// <returns>The faction that owns the fleet, or null if the fleet or faction is missing.</returns>
        private Faction GetFleetFaction(string fleetInstanceId)
        {
            Fleet fleet = _game.GetSceneNodeByInstanceID<Fleet>(fleetInstanceId);
            return _game.GetFactionByOwnerInstanceID(fleet?.GetOwnerInstanceID());
        }

        /// <summary>
        /// Builds a PendingCombatResult for a player-involved encounter that must pause the tick.
        /// </summary>
        /// <param name="decision">The combat decision context to forward to the player.</param>
        /// <returns>Pending-combat event with both fleets and the current tick.</returns>
        private PendingCombatResult BuildPendingCombatResult(CombatDecisionContext decision)
        {
            return new PendingCombatResult
            {
                AttackerFleet = _game.GetSceneNodeByInstanceID<Fleet>(
                    decision.AttackerFleetInstanceID
                ),
                DefenderFleet = _game.GetSceneNodeByInstanceID<Fleet>(
                    decision.DefenderFleetInstanceID
                ),
                Tick = _game.CurrentTick,
            };
        }

        /// <summary>
        /// Detects the first hostile fleet encounter this tick. The encounter is NOT resolved
        /// here; call Resolve() after player decision.
        /// </summary>
        /// <param name="decision">Output: populated with the encounter context on success.</param>
        /// <returns>True if a hostile encounter was detected.</returns>
        public bool TryStartCombat(out CombatDecisionContext decision) =>
            TryStartCombatExcluding(new HashSet<string>(), out decision);

        /// <summary>
        /// Resolves a pending combat encounter via the space-combat pipeline.
        /// </summary>
        /// <param name="decision">The encounter context to resolve.</param>
        /// <param name="autoResolve">True for AI auto-resolve; false for manual/interactive.</param>
        /// <returns>The combat result, or null if manual combat was used.</returns>
        public SpaceCombatResult Resolve(CombatDecisionContext decision, bool autoResolve) =>
            _spaceCombat.Resolve(decision, autoResolve);

        /// <summary>
        /// Runs the planetary-assault pipeline against a defending planet.
        /// </summary>
        /// <param name="attackingFleets">Fleets performing the assault (all must share a faction).</param>
        /// <param name="defendingPlanet">Planet being assaulted.</param>
        /// <returns>Assault outcome, including destroyed units and any ownership change.</returns>
        public PlanetaryAssaultResult ExecutePlanetaryAssault(
            List<Fleet> attackingFleets,
            Planet defendingPlanet
        ) => _assault.Execute(attackingFleets, defendingPlanet);

        /// <summary>
        /// Runs the 6-stage orbital bombardment pipeline against a target planet.
        /// </summary>
        /// <param name="attackingFleets">Fleets performing the bombardment (all must share a faction).</param>
        /// <param name="targetPlanet">Planet being bombarded.</param>
        /// <returns>Bombardment outcome, including strikes and any ship/regiment/building destruction.</returns>
        public BombardmentResult ExecuteOrbitalBombardment(
            List<Fleet> attackingFleets,
            Planet targetPlanet
        ) => _defenseCombat.Execute(attackingFleets, targetPlanet);

        /// <summary>
        /// Detects a hostile fleet encounter while skipping any fleet IDs already resolved
        /// this tick. Used by ProcessTick to prevent Draw outcomes from being re-detected.
        /// </summary>
        /// <param name="excludedIDs">Fleet instance IDs to skip (already fought this tick).</param>
        /// <param name="decision">Output: populated with the encounter context on success.</param>
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
        /// Finds the first pair of hostile fleets occupying the same planet. Skips fleets
        /// already engaged in combat or excluded by ID. Faction groups are sorted by owner ID
        /// for deterministic selection.
        /// </summary>
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
    }

    internal class SpaceCombatResolver
    {
        private readonly GameRoot _game;
        private readonly IRandomNumberProvider _provider;
        private readonly MovementSystem _movement;

        public SpaceCombatResolver(
            GameRoot game,
            IRandomNumberProvider provider,
            MovementSystem movement
        )
        {
            _game = game;
            _provider = provider;
            _movement = movement;
        }

        /// <summary>
        /// Resolves a pending combat encounter. Applies damage to the game world and clears
        /// IsInCombat on both fleets regardless of outcome.
        /// </summary>
        public SpaceCombatResult Resolve(CombatDecisionContext decision, bool autoResolve)
        {
            SpaceCombatResult result = null;

            if (autoResolve)
                result = AutoResolveCombat(decision, _provider);
            else
                RunManualCombat();

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
        /// Runs the 7-phase combat pipeline for an AI encounter and applies the outcome to
        /// the game world.
        /// </summary>
        /// <param name="decision">The encounter to resolve.</param>
        /// <param name="rng">Random-number provider for damage variance.</param>
        /// <returns>The combat result with winner and per-ship damage, or null if either fleet is missing.</returns>
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
                _game.CurrentTick,
                _game.Config.Combat
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
        /// Placeholder for interactive/manual combat resolution.
        /// </summary>
        private void RunManualCombat()
        {
            // TODO: Implement manual combat resolution
        }

        /// <summary>
        /// 7-phase space combat pipeline: snapshot -> composition -> weapon fire -> fighter
        /// engagement -> result. Shield absorption and hull damage happen inside weapon fire.
        /// </summary>
        /// <param name="attackerFleet">The attacking fleet.</param>
        /// <param name="defenderFleet">The defending fleet.</param>
        /// <param name="planet">Planet where combat occurs.</param>
        /// <param name="rng">Random-number provider for damage variance.</param>
        /// <param name="tick">Current game tick (recorded on the result).</param>
        /// <param name="config">Combat configuration supplying damage/variance tuning values.</param>
        /// <returns>The combat result with winner, per-ship damage, and fighter losses.</returns>
        private static SpaceCombatResult ResolveSpace(
            Fleet attackerFleet,
            Fleet defenderFleet,
            Planet planet,
            IRandomNumberProvider rng,
            int tick,
            GameConfig.CombatConfig config
        )
        {
            (List<ShipSnap> atkShips, List<int> atkFighters) = SnapshotFleet(attackerFleet);
            (List<ShipSnap> defShips, List<int> defFighters) = SnapshotFleet(defenderFleet);
            List<int> atkInitialFighters = atkFighters.ToList();
            List<int> defInitialFighters = defFighters.ToList();

            bool anyArmed =
                atkShips.Any(s => s.Alive && s.WeaponNibble > 0)
                || defShips.Any(s => s.Alive && s.WeaponNibble > 0)
                || HasArmedFighters(attackerFleet, atkFighters)
                || HasArmedFighters(defenderFleet, defFighters);

            if (anyArmed)
            {
                PhaseWeaponFire(attackerFleet, atkShips, defShips, rng, config);
                PhaseWeaponFire(defenderFleet, defShips, atkShips, rng, config);
                PhaseFighterEngage(
                    attackerFleet,
                    defenderFleet,
                    atkFighters,
                    defFighters,
                    atkShips,
                    defShips,
                    rng,
                    config
                );
            }

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
        /// Determines the combat winner by counting surviving capital ships and fighter squadrons
        /// on each side. Returns Draw if both sides have survivors or both are wiped out.
        /// </summary>
        /// <param name="atkShips">Attacker ship snapshots.</param>
        /// <param name="defShips">Defender ship snapshots.</param>
        /// <param name="atkFighters">Attacker fighter squadron sizes.</param>
        /// <param name="defFighters">Defender fighter squadron sizes.</param>
        /// <returns>The winning side, or Draw.</returns>
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
        /// Builds mutable per-battle snapshots for all capital ships in a fleet and captures
        /// current squadron sizes for all its starfighter groups.
        /// </summary>
        /// <param name="fleet">Fleet to snapshot.</param>
        /// <returns>Ship snapshots and parallel list of squadron sizes.</returns>
        private static (List<ShipSnap> ships, List<int> fighters) SnapshotFleet(Fleet fleet)
        {
            List<ShipSnap> ships = new List<ShipSnap>();

            foreach (CapitalShip ship in fleet.CapitalShips)
            {
                ships.Add(
                    new ShipSnap
                    {
                        HullCurrent = ship.CurrentHullStrength,
                        HullMax = ship.MaxHullStrength,
                        ShieldNibble = Math.Min(ship.ShieldRechargeRate, 15),
                        WeaponNibble = 15,
                        Alive = true,
                    }
                );
            }

            List<int> fighters = fleet
                .GetStarfighters()
                .Select(f => f.CurrentSquadronSize)
                .ToList();

            return (ships, fighters);
        }

        /// <summary>
        /// Returns true if any squadron in the fleet has both fighters remaining and non-zero
        /// weapon strength (laser + ion + torpedo).
        /// </summary>
        /// <param name="fleet">Fleet owning the fighter groups.</param>
        /// <param name="squadrons">Current squadron sizes.</param>
        /// <returns>True if at least one squadron can attack.</returns>
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
        /// One side fires all primary weapon arcs at the other. Total firepower is scaled by
        /// each ship's weapon nibble, divided evenly across alive targets, and applied with
        /// shield absorption and configured damage variance.
        /// </summary>
        /// <param name="firingFleet">Fleet doing the firing (used for weapon data).</param>
        /// <param name="firing">Firing side's ship snapshots.</param>
        /// <param name="targets">Target side's ship snapshots (mutated with damage).</param>
        /// <param name="rng">Random-number provider for variance.</param>
        /// <param name="config">Combat configuration supplying damage variance.</param>
        private static void PhaseWeaponFire(
            Fleet firingFleet,
            List<ShipSnap> firing,
            List<ShipSnap> targets,
            IRandomNumberProvider rng,
            GameConfig.CombatConfig config
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
                ApplyWeaponDamage(targets[idx], firePerTarget, rng, config);
            }
        }

        /// <summary>
        /// Sums primary weapon arc values across all alive ships, scaled by each ship's
        /// weapon nibble (0-15).
        /// </summary>
        /// <param name="fleet">Fleet owning the ships (used for weapon data).</param>
        /// <param name="ships">Ship snapshots with alive/weapon-nibble state.</param>
        /// <returns>Total firepower for the side this tick.</returns>
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
        /// Applies weapon damage to a single target with configured variance and shield absorption.
        /// Shield nibble / 15 of damage is absorbed; remainder reduces hull.
        /// </summary>
        /// <param name="target">Target ship snapshot (mutated).</param>
        /// <param name="baseDamage">Pre-variance damage to apply.</param>
        /// <param name="rng">Random-number provider for variance.</param>
        /// <param name="config">Combat configuration supplying variance percentage.</param>
        private static void ApplyWeaponDamage(
            ShipSnap target,
            int baseDamage,
            IRandomNumberProvider rng,
            GameConfig.CombatConfig config
        )
        {
            double roll = rng.NextDouble();
            int variance = (int)(
                (double)baseDamage * config.WeaponDamageVariancePercent * (roll * 2.0 - 1.0) / 100.0
            );
            int damage = Math.Max(baseDamage + variance, 0);

            int absorbed = (int)(damage * target.ShieldNibble / 15.0);
            int hullDamage = Math.Max(damage - absorbed, 0);

            target.HullCurrent = Math.Max(target.HullCurrent - hullDamage, 0);
            if (target.HullCurrent == 0)
                target.Alive = false;
        }

        /// <summary>
        /// Fighter phase: each side's fighters attack enemy capital ships, then opposing
        /// squadrons dogfight each other and apply proportional losses.
        /// </summary>
        /// <param name="attackerFleet">Attacker fleet (used for fighter weapon data).</param>
        /// <param name="defenderFleet">Defender fleet (used for fighter weapon data).</param>
        /// <param name="atkFighters">Attacker squadron sizes (mutated).</param>
        /// <param name="defFighters">Defender squadron sizes (mutated).</param>
        /// <param name="atkShips">Attacker ship snapshots (targets for defender fighters).</param>
        /// <param name="defShips">Defender ship snapshots (targets for attacker fighters).</param>
        /// <param name="rng">Random-number provider.</param>
        /// <param name="config">Combat configuration supplying damage/loss tuning.</param>
        private static void PhaseFighterEngage(
            Fleet attackerFleet,
            Fleet defenderFleet,
            List<int> atkFighters,
            List<int> defFighters,
            List<ShipSnap> atkShips,
            List<ShipSnap> defShips,
            IRandomNumberProvider rng,
            GameConfig.CombatConfig config
        )
        {
            FightersAttackShips(atkFighters, attackerFleet, defShips, rng, config);
            FightersAttackShips(defFighters, defenderFleet, atkShips, rng, config);

            int atkTotal = atkFighters.Sum();
            int defTotal = defFighters.Sum();

            if (atkTotal == 0 || defTotal == 0)
                return;

            double rollAtk = rng.NextDouble();
            double rollDef = rng.NextDouble();

            double atkHitRate = (double)defTotal / (atkTotal + defTotal);
            double defHitRate = (double)atkTotal / (atkTotal + defTotal);

            double lossRate = config.FighterDogfightLossRatePercent / 100.0;
            int atkLosses = Math.Min((int)(atkTotal * atkHitRate * lossRate * rollAtk), atkTotal);
            int defLosses = Math.Min((int)(defTotal * defHitRate * lossRate * rollDef), defTotal);

            ApplyFighterLosses(atkFighters, atkLosses);
            ApplyFighterLosses(defFighters, defLosses);
        }

        /// <summary>
        /// Each fighter squadron picks a random alive enemy capital ship and attacks it
        /// with total squadron firepower times squadron size, with configured damage spread.
        /// </summary>
        /// <param name="squadrons">Squadron sizes for the attacking side.</param>
        /// <param name="fleet">Fleet owning the fighters (used for weapon data).</param>
        /// <param name="enemyShips">Enemy ship snapshots to attack (mutated).</param>
        /// <param name="rng">Random-number provider.</param>
        /// <param name="config">Combat configuration supplying damage range.</param>
        private static void FightersAttackShips(
            List<int> squadrons,
            Fleet fleet,
            List<ShipSnap> enemyShips,
            IRandomNumberProvider rng,
            GameConfig.CombatConfig config
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
                double basePct = config.FighterDamageBasePercent / 100.0;
                double spreadPct = config.FighterDamageSpreadPercent / 100.0;
                int damage = (int)(totalAttack * (basePct + spreadPct * roll));

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
        /// Distributes a total loss count across squadrons proportionally to their size. Any
        /// remainder from integer division is distributed one-per-squadron to avoid rounding bias.
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
        /// Builds a SpaceCombatResult from the final snapshots and initial fighter counts,
        /// recording per-ship damage and per-squadron losses.
        /// </summary>
        /// <param name="attackerFleet">Attacker fleet.</param>
        /// <param name="defenderFleet">Defender fleet.</param>
        /// <param name="planet">Planet where combat occurred.</param>
        /// <param name="atkShips">Post-combat attacker ship snapshots.</param>
        /// <param name="defShips">Post-combat defender ship snapshots.</param>
        /// <param name="atkFighters">Post-combat attacker squadron sizes.</param>
        /// <param name="defFighters">Post-combat defender squadron sizes.</param>
        /// <param name="atkInitialFighters">Pre-combat attacker squadron sizes.</param>
        /// <param name="defInitialFighters">Pre-combat defender squadron sizes.</param>
        /// <param name="tick">Game tick when combat occurred.</param>
        /// <returns>The populated combat result.</returns>
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
        /// Appends a ShipDamageResult for each ship that took hull damage during the battle.
        /// </summary>
        /// <param name="results">List to append damage entries to.</param>
        /// <param name="fleet">Fleet owning the ships.</param>
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
        /// Appends a FighterLossResult for each squadron that took casualties.
        /// </summary>
        /// <param name="results">List to append loss entries to.</param>
        /// <param name="fleet">Fleet owning the squadrons.</param>
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
        /// Applies a space combat result to the game world: updates hull strength, removes
        /// destroyed ships and depleted fighter squadrons, cleans up empty fleets.
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
        /// Writes hull damage back to each ship, detaches destroyed ships, and evacuates their
        /// officers (to a surviving ship or to the nearest friendly planet).
        /// </summary>
        /// <param name="damageResults">Ship damage entries produced by the battle.</param>
        /// <returns>A GameObjectDamagedResult per damaged ship.</returns>
        private List<GameResult> ApplyShipDamage(List<ShipDamageResult> damageResults)
        {
            List<GameResult> events = new List<GameResult>();

            foreach (ShipDamageResult damage in damageResults)
            {
                CapitalShip ship = damage.Ship;
                if (ship == null)
                    continue;

                ship.CurrentHullStrength = damage.HullAfter;

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
                    CombatHelpers.EvacuateOfficers(_game, _movement, ship, fleet);
                    _game.DetachNode(ship);
                    GameLogger.Log($"Ship destroyed: {ship.GetDisplayName()}");
                }
            }

            return events;
        }

        /// <summary>
        /// Writes squadron-size losses back to each squadron and detaches any that are wiped out.
        /// </summary>
        /// <param name="lossResults">Fighter loss entries produced by the battle.</param>
        private void ApplyFighterSquadronLosses(List<FighterLossResult> lossResults)
        {
            foreach (FighterLossResult loss in lossResults)
            {
                Starfighter fighter = loss.Fighter;
                if (fighter == null)
                    continue;

                fighter.CurrentSquadronSize = loss.SquadsAfter;

                if (loss.SquadsAfter <= 0)
                {
                    _game.DetachNode(fighter);
                    GameLogger.Log($"Fighter squadron destroyed: {fighter.GetDisplayName()}");
                }
            }
        }

        /// <summary>
        /// Removes a fleet with no remaining capital ships from the scene graph.
        /// </summary>
        /// <param name="fleet">Empty fleet to remove.</param>
        private void RemoveFleetFromScene(Fleet fleet)
        {
            _game.DetachNode(fleet);
            GameLogger.Log($"Fleet destroyed: {fleet.GetDisplayName()}");
        }

        /// <summary>Mutable per-battle snapshot of one capital ship.</summary>
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
    }

    internal class PlanetaryAssaultResolver
    {
        private readonly GameRoot _game;
        private readonly IRandomNumberProvider _provider;
        private readonly PlanetaryControlSystem _ownership;

        public PlanetaryAssaultResolver(
            GameRoot game,
            IRandomNumberProvider provider,
            PlanetaryControlSystem ownership
        )
        {
            _game = game;
            _provider = provider;
            _ownership = ownership;
        }

        /// <summary>
        /// Runs the planetary-assault pipeline: strength vs defense gate, dice-roll success
        /// gate, capital-strike loop, and ownership transfer if the planet is wiped out.
        /// </summary>
        /// <param name="attackingFleets">Fleets performing the assault (all must share a faction).</param>
        /// <param name="defendingPlanet">Planet being assaulted.</param>
        /// <returns>Assault outcome, including destroyed units and any ownership change.</returns>
        public PlanetaryAssaultResult Execute(List<Fleet> attackingFleets, Planet defendingPlanet)
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

            // Phase 1: total assault strength
            int assaultDivisor = _game.Config.Combat.AssaultPersonnelDivisor;
            int totalAssaultStrength = 0;
            foreach (Fleet fleet in attackingFleets)
                totalAssaultStrength += fleet.GetAssaultStrength(assaultDivisor);
            result.AssaultStrength = totalAssaultStrength;

            // Phase 2: total defense
            int totalDefenseStrength = defendingPlanet.GetDefenseStrength();
            result.DefenseStrength = totalDefenseStrength;

            // Phase 3: strength gate
            if (totalAssaultStrength <= totalDefenseStrength)
            {
                result.Success = false;
                return result;
            }

            int excessAssaultStrength = totalAssaultStrength - totalDefenseStrength;

            int energyResistance = _game.Config.AI.CapitalShipProduction.EnergyStrikeResistance;
            int allocatedEnergyResistance = _game
                .Config
                .AI
                .CapitalShipProduction
                .AllocatedEnergyStrikeResistance;

            // Phase 5: capital strikes
            result.Success = true;
            ExecuteCapitalStrikes(
                defendingPlanet,
                excessAssaultStrength,
                energyResistance,
                allocatedEnergyResistance,
                result
            );

            // Phase 6: successful assault captures the planet.
            if (result.Success)
            {
                TransferPlanetOwnership(defendingPlanet, attackerFactionID, result);
                LandFleetUnits(attackingFleets, defendingPlanet);
            }

            return result;
        }

        /// <summary>
        /// Pre-loop production-facility strike, then a main loop running excessAssaultStrength
        /// iterations. Main loop uses the STORED initial lane count as the roll denominator so
        /// that destroyed targets leave rolls that become no-ops.
        /// </summary>
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

            // A) Initial pre-loop strike — gated by a probabilistic roll, then targets a
            //    random production facility. Mirrors the gate in FUN_0058c580 (~91% with
            //    default 1/10 thresholds).
            List<Building> productionBuildings = planet
                .GetAllBuildings()
                .Where(IsProductionBuilding)
                .ToList();
            if (productionBuildings.Count > 0)
            {
                int gateRoll = _provider.NextInt(0, thresholdLow + thresholdHigh);
                if (gateRoll < thresholdHigh)
                {
                    Building target = productionBuildings[
                        _provider.NextInt(0, productionBuildings.Count)
                    ];
                    int strikeRoll = _provider.NextInt(thresholdLow, thresholdHigh + 1);
                    if (target.Bombardment < strikeRoll)
                    {
                        result.DestroyedBuildings.Add(target);
                        _game.DetachNode(target);
                        GameLogger.Log(
                            $"Initial capital strike destroyed {target.GetDisplayName()} at {planet.GetDisplayName()}"
                        );
                    }
                }
            }

            // B) Main loop — strikes always target the highest-priority remaining lane
            //    (troops -> buildings -> energy -> allocated energy). The initial-lane-count
            //    roll is preserved to match the original's RNG step consumption.
            int initialLaneCount = BuildAssaultLanes(
                planet,
                energyResistance,
                allocatedEnergyResistance
            ).Count;

            if (initialLaneCount == 0)
                return;

            for (int i = 0; i < excessAssaultStrength; i++)
            {
                _ = _provider.NextInt(0, initialLaneCount);

                List<AssaultLane> lanes = BuildAssaultLanes(
                    planet,
                    energyResistance,
                    allocatedEnergyResistance
                );
                if (lanes.Count == 0)
                    break;

                AssaultLane lane = lanes[0];
                int strikeRoll = _provider.NextInt(thresholdLow, thresholdHigh + 1);
                if (lane.Resistance >= strikeRoll)
                    continue;

                ApplyAssaultStrike(planet, lane, result);
            }
        }

        /// <summary>
        /// Returns true for mines, refineries, shipyards, training facilities, and construction
        /// facilities — the production category targetable by the initial pre-loop strike.
        /// </summary>
        /// <param name="building">Building to classify.</param>
        /// <returns>True if the building is a production facility.</returns>
        private static bool IsProductionBuilding(Building building)
        {
            BuildingType type = building.GetBuildingType();
            return type == BuildingType.Mine
                || type == BuildingType.Refinery
                || type == BuildingType.Shipyard
                || type == BuildingType.TrainingFacility
                || type == BuildingType.ConstructionFacility;
        }

        private static bool IsDefenseFacilityBuilding(Building building)
        {
            return building.GetBuildingType() == BuildingType.Defense;
        }

        private static bool IsProductionTargetBuilding(Building building)
        {
            BuildingType type = building.GetBuildingType();
            return type == BuildingType.Mine
                || type == BuildingType.Refinery
                || type == BuildingType.Shipyard
                || type == BuildingType.TrainingFacility
                || type == BuildingType.ConstructionFacility;
        }

        /// <summary>
        /// Enumerates assault lanes in the original's priority order: troops, then defense
        /// facilities, then production buildings, then the energy and allocated-energy lanes.
        /// </summary>
        /// <param name="planet">Planet being assaulted.</param>
        /// <param name="energyResistance">Resistance value for the energy lane.</param>
        /// <param name="allocatedEnergyResistance">Resistance value for the allocated-energy lane.</param>
        /// <returns>Lane list in priority order.</returns>
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
                if (!IsDefenseFacilityBuilding(buildings[i]))
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

            for (int i = 0; i < buildings.Count; i++)
            {
                if (!IsProductionTargetBuilding(buildings[i]))
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

            if (planet.AllocatedEnergy > 0)
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
        /// Applies one successful assault strike: destroys the targeted regiment or building,
        /// or reduces energy capacity. Records the event on the result.
        /// </summary>
        /// <param name="planet">Planet being assaulted.</param>
        /// <param name="lane">Chosen lane identifying the target.</param>
        /// <param name="result">Result object to record the strike on.</param>
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
                    if (planet.AllocatedEnergy > 0)
                    {
                        planet.AllocatedEnergy--;
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
        /// Transfers planet ownership to the attacking faction via PlanetaryControlSystem and
        /// records the change on the result.
        /// </summary>
        /// <param name="planet">Planet changing hands.</param>
        /// <param name="newOwnerID">Instance ID of the faction taking ownership.</param>
        /// <param name="result">Result object to record the ownership change on.</param>
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
        /// Moves every regiment, officer, and starfighter from the assaulting fleets onto
        /// the captured planet's surface.
        /// </summary>
        /// <param name="fleets">The assaulting fleets whose units will land.</param>
        /// <param name="planet">The captured planet receiving the units.</param>
        private void LandFleetUnits(List<Fleet> fleets, Planet planet)
        {
            foreach (Fleet fleet in fleets)
            {
                foreach (Regiment regiment in fleet.GetRegiments().ToList())
                    _game.MoveNode(regiment, planet);
                foreach (Officer officer in fleet.GetOfficers().ToList())
                    _game.MoveNode(officer, planet);
                foreach (Starfighter starfighter in fleet.GetStarfighters().ToList())
                    _game.MoveNode(starfighter, planet);
            }
        }

        private class AssaultLane
        {
            public AssaultLaneType Type;
            public int Resistance;
            public int TargetIndex;
        }
    }

    internal class PlanetaryDefenseCombatResolver
    {
        private readonly GameRoot _game;
        private readonly IRandomNumberProvider _provider;
        private readonly MovementSystem _movement;
        private readonly PlanetaryControlSystem _ownership;

        public PlanetaryDefenseCombatResolver(
            GameRoot game,
            IRandomNumberProvider provider,
            MovementSystem movement,
            PlanetaryControlSystem ownership
        )
        {
            _game = game;
            _provider = provider;
            _movement = movement;
            _ownership = ownership;
        }

        /// <summary>
        /// Runs the 6-stage planetary defense combat pipeline (setup, defense-facility fire,
        /// post-fire troop pass, repeat damage, garrison resolution, cleanup). Short-circuits
        /// if any stage returns false; also short-circuits on a shield block.
        /// </summary>
        /// <param name="attackingFleets">Fleets performing the bombardment (all must share a faction).</param>
        /// <param name="targetPlanet">Planet being bombarded.</param>
        /// <returns>Bombardment outcome, including strikes and any ship/regiment/building destruction.</returns>
        public BombardmentResult Execute(List<Fleet> attackingFleets, Planet targetPlanet)
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

            if (IsShieldBlocking(targetPlanet))
            {
                result.ShieldBlocked = true;
                GameLogger.Log(
                    $"Bombardment blocked by shields at {targetPlanet.GetDisplayName()}"
                );
                return result;
            }

            DefenseRun run = new DefenseRun
            {
                Planet = targetPlanet,
                AttackingFleets = attackingFleets,
                Result = result,
            };

            if (!Stage1_Setup(run))
                return result;
            if (!Stage2_DefenseFacilityFire(run))
                return result;
            if (!Stage3_PostFireTroopIteration(run))
                return result;
            if (!Stage4_RepeatDamage(run))
                return result;
            if (!Stage5_GarrisonTroopResolution(run))
                return result;
            Stage6_Cleanup(run);

            return result;
        }

        /// <summary>
        /// Returns true when the planet has enough Shield / DeathStarShield facilities to
        /// meet the bombardment-block threshold from config.
        /// </summary>
        /// <param name="planet">Planet under bombardment.</param>
        /// <returns>True if shields block bombardment entirely.</returns>
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
        /// Stage 1 — establishes initial attacker troop count, remaining-ship cap, and
        /// stage-4 trial count (derived from fleet bombardment strength).
        /// </summary>
        /// <param name="run">Run state (populated).</param>
        /// <returns>True to continue the pipeline.</returns>
        private bool Stage1_Setup(DefenseRun run)
        {
            int attackerTroops = CountAttackingTroopsFromOrbitingShips(
                run.AttackingFleets,
                run.Planet.GetOwnerInstanceID()
            );
            run.InitialAttackingTroopCount = attackerTroops;
            run.RemainingAttackingTroopCount = attackerTroops;

            int garrison = run.Planet.GetAllRegiments().Count;
            run.InitialGarrisonTroopCount = garrison;
            run.GarrisonTroopCount = garrison;

            int strength = CalculateFleetBombardmentStrength(run.AttackingFleets);
            run.StageFourTrials = strength;
            run.Result.FleetBombardmentStrength = strength;
            return true;
        }

        /// <summary>
        /// Stage 2 — each KDY/LNR facility rolls (scaled by its production modifier) and,
        /// on success, destroys one randomly-picked attacker regiment aboard the orbiting
        /// capital ships.
        /// </summary>
        /// <param name="run">Run state.</param>
        /// <returns>True to continue the pipeline.</returns>
        private bool Stage2_DefenseFacilityFire(DefenseRun run)
        {
            int divisor = _game.Config.Combat.DefenseFacilityResponseDivisor;
            List<Building> defenseFacilities = run
                .Planet.GetAllBuildings()
                .Where(b =>
                    b.DefenseFacilityClass == DefenseFacilityClass.KDY
                    || b.DefenseFacilityClass == DefenseFacilityClass.LNR
                )
                .ToList();

            foreach (Building facility in defenseFacilities)
            {
                if (run.RemainingAttackingTroopCount <= 0)
                    break;

                int scaledChance =
                    divisor > 0
                        ? facility.ProductionModifier / divisor
                        : facility.ProductionModifier;
                if (!RollProbabilitySuccess(scaledChance))
                    continue;

                Regiment target = PickRandomAttackerTroop(run);
                if (target == null)
                    continue;

                run.Result.DestroyedRegiments.Add(target);
                _game.DetachNode(target);
                run.RemainingAttackingTroopCount--;
                GameLogger.Log(
                    $"Defense facility {facility.GetDisplayName()} destroyed attacker {target.GetDisplayName()}"
                );
            }

            return true;
        }

        /// <summary>
        /// Picks the (target_index)-th surviving attacker regiment across all orbiting hostile
        /// capital ships. Roll range is the initial attacker count; if the roll lands beyond
        /// the surviving range, returns null and the strike is wasted.
        /// </summary>
        /// <param name="run">Run state.</param>
        /// <returns>The selected regiment, or null if the roll missed.</returns>
        private Regiment PickRandomAttackerTroop(DefenseRun run)
        {
            if (run.InitialAttackingTroopCount <= 0)
                return null;
            int targetIndex = _provider.NextInt(0, run.InitialAttackingTroopCount);
            if (targetIndex >= run.RemainingAttackingTroopCount)
                return null;

            string defenderId = run.Planet.GetOwnerInstanceID();
            int counter = 0;
            foreach (Fleet fleet in run.AttackingFleets)
            {
                if (fleet.GetOwnerInstanceID() == defenderId)
                    continue;
                foreach (CapitalShip ship in fleet.CapitalShips)
                {
                    foreach (Regiment regiment in ship.Regiments)
                    {
                        if (counter == targetIndex)
                            return regiment;
                        counter++;
                    }
                }
            }
            return null;
        }

        /// <summary>
        /// Stage 3 — ground combat. Iterates each attacker troop on each orbiting capital
        /// ship and runs a duel against a randomly-picked garrison regiment. Decrements the
        /// loser's remaining-count on each duel and stops when either side hits zero.
        /// </summary>
        /// <param name="run">Run state.</param>
        /// <returns>True to continue the pipeline.</returns>
        private bool Stage3_PostFireTroopIteration(DefenseRun run)
        {
            if (run.RemainingAttackingTroopCount <= 0 || run.GarrisonTroopCount <= 0)
                return true;

            string defenderId = run.Planet.GetOwnerInstanceID();
            int attackerCommanderRating = GetTopAdmiralLeadership(run.AttackingFleets, defenderId);
            int defenderCommanderRating = GetPlanetTopAdmiralLeadership(run.Planet);

            List<(Regiment troop, CapitalShip ship)> attackerTroops = SnapshotAttackingTroops(
                run.AttackingFleets,
                defenderId
            );

            foreach ((Regiment attackerTroop, CapitalShip ship) in attackerTroops)
            {
                if (run.RemainingAttackingTroopCount <= 0 || run.GarrisonTroopCount <= 0)
                    break;
                if (!ship.Regiments.Contains(attackerTroop))
                    continue;

                Regiment defenderTroop = PickRandomGarrisonRegiment(run);
                if (defenderTroop == null)
                    continue;

                (bool defenderWins, bool attackerWins) = ResolveTroopKillContest(
                    attackerStrength: attackerTroop.AttackRating,
                    attackerCommanderRating: attackerCommanderRating,
                    defenderStrength: defenderTroop.DefenseRating,
                    defenderCommanderRating: defenderCommanderRating
                );

                if (attackerWins)
                {
                    run.Result.DestroyedRegiments.Add(defenderTroop);
                    _game.DetachNode(defenderTroop);
                    run.GarrisonTroopCount--;
                    GameLogger.Log(
                        $"Ground combat at {run.Planet.GetDisplayName()}: attacker destroyed garrison {defenderTroop.GetDisplayName()}"
                    );
                }
                else if (defenderWins)
                {
                    _game.DetachNode(attackerTroop);
                    run.RemainingAttackingTroopCount--;
                    GameLogger.Log(
                        $"Ground combat at {run.Planet.GetDisplayName()}: garrison destroyed attacker {attackerTroop.GetDisplayName()}"
                    );
                }
            }

            return true;
        }

        /// <summary>
        /// Picks a random garrison regiment using the original's "roll over the initial
        /// count, succeed only if index lands in the still-alive range" pattern. As
        /// garrison shrinks, miss probability rises and Stage 3 progresses faster.
        /// </summary>
        /// <param name="run">Run state.</param>
        /// <returns>Selected garrison regiment, or null if the roll missed or none exist.</returns>
        private Regiment PickRandomGarrisonRegiment(DefenseRun run)
        {
            if (run.InitialGarrisonTroopCount <= 0)
                return null;
            int rollRange = Math.Max(1, run.InitialGarrisonTroopCount);
            int targetIndex = _provider.NextInt(0, rollRange);
            if (targetIndex >= run.GarrisonTroopCount)
                return null;
            List<Regiment> garrison = run.Planet.GetAllRegiments();
            if (targetIndex >= garrison.Count)
                return null;
            return garrison[targetIndex];
        }

        /// <summary>
        /// Snapshots every attacker troop currently aboard hostile capital ships. The
        /// result is captured up-front so iteration is stable across mid-loop destructions.
        /// </summary>
        /// <param name="fleets">Attacking fleets.</param>
        /// <param name="defenderOwnerId">Planet owner ID; same-owner fleets are skipped.</param>
        /// <returns>List of (regiment, hosting ship) pairs.</returns>
        private static List<(Regiment, CapitalShip)> SnapshotAttackingTroops(
            List<Fleet> fleets,
            string defenderOwnerId
        )
        {
            List<(Regiment, CapitalShip)> result = new List<(Regiment, CapitalShip)>();
            foreach (Fleet fleet in fleets)
            {
                if (fleet.GetOwnerInstanceID() == defenderOwnerId)
                    continue;
                foreach (CapitalShip ship in fleet.CapitalShips)
                {
                    foreach (Regiment regiment in ship.Regiments.ToList())
                        result.Add((regiment, ship));
                }
            }
            return result;
        }

        /// <summary>
        /// Returns the highest General-rank Leadership score across all hostile fleets, or 0.
        /// </summary>
        /// <param name="fleets">Attacking fleets to scan.</param>
        /// <param name="defenderOwnerId">Defender owner ID; same-owner fleets are skipped.</param>
        /// <returns>Top Leadership rating among hostile-fleet Admirals, or 0 if none present.</returns>
        private static int GetTopAdmiralLeadership(List<Fleet> fleets, string defenderOwnerId)
        {
            int best = 0;
            foreach (Fleet fleet in fleets)
            {
                if (fleet.GetOwnerInstanceID() == defenderOwnerId)
                    continue;
                Officer admiral = fleet
                    .GetOfficers()
                    .FirstOrDefault(o => o.CurrentRank == OfficerRank.Admiral);
                if (admiral == null)
                    continue;
                int rating = admiral.GetSkillValue(MissionParticipantSkill.Leadership);
                if (rating > best)
                    best = rating;
            }
            return best;
        }

        /// <summary>
        /// Returns the highest Admiral-rank Leadership score among officers stationed at the
        /// planet, or 0 if none are present.
        /// </summary>
        /// <param name="planet">Planet to scan for ground-combat commanders.</param>
        /// <returns>Top Leadership rating among on-planet Admirals, or 0.</returns>
        private static int GetPlanetTopAdmiralLeadership(Planet planet)
        {
            int best = 0;
            foreach (Officer officer in planet.GetAllOfficers())
            {
                if (officer.CurrentRank != OfficerRank.Admiral)
                    continue;
                int rating = officer.GetSkillValue(MissionParticipantSkill.Leadership);
                if (rating > best)
                    best = rating;
            }
            return best;
        }

        /// <summary>
        /// Stage 4 — repeat-damage loop. For each successful Bernoulli trial, rebuilds the
        /// defender ship-group + energy + allocated-energy lanes and applies damage via the
        /// weighted lane picker.
        /// </summary>
        /// <param name="run">Run state.</param>
        /// <returns>True to continue the pipeline.</returns>
        private bool Stage4_RepeatDamage(DefenseRun run)
        {
            int successful = CountSuccessfulRepeatTrials(run.StageFourTrials);
            if (successful <= 0)
                return true;

            for (int i = 0; i < successful; i++)
            {
                string defenderId = run.Planet.GetOwnerInstanceID();
                List<CapitalShip> ships = run
                    .Planet.Fleets.Where(f => f.GetOwnerInstanceID() == defenderId)
                    .SelectMany(f => f.CapitalShips)
                    .Where(s => s.CurrentHullStrength > 0)
                    .ToList();
                List<Starfighter> fighters = run
                    .Planet.GetAllStarfighters()
                    .Where(f => f.GetOwnerInstanceID() == defenderId)
                    .ToList();
                List<Building> defensiveBuildings = run
                    .Planet.GetAllBuildings()
                    .Where(b =>
                        b.DefenseFacilityClass == DefenseFacilityClass.KDY
                        || b.DefenseFacilityClass == DefenseFacilityClass.LNR
                        || b.DefenseFacilityClass == DefenseFacilityClass.Shield
                        || b.DefenseFacilityClass == DefenseFacilityClass.DeathStarShield
                    )
                    .ToList();

                int shipGroupCount = ships.Count + fighters.Count + defensiveBuildings.Count;
                bool energyPresent = run.Planet.EnergyCapacity > 0;
                bool allocatedPresent = run.Planet.AllocatedEnergy > 0;

                LaneChoice choice = SelectRepeatDamageLane(
                    shipGroupCount,
                    energyPresent,
                    allocatedPresent
                );

                if (choice.Lane == Lane.None)
                    break;

                ApplyRepeatDamageLane(run, choice, ships, fighters, defensiveBuildings);
            }

            return true;
        }

        /// <summary>
        /// Stage 5 — garrison check for final ground-combat resolution between remaining
        /// attacker troops and the defender's garrison requirement.
        /// </summary>
        /// <param name="run">Run state.</param>
        /// <returns>True to continue the pipeline.</returns>
        private bool Stage5_GarrisonTroopResolution(DefenseRun run)
        {
            if (run.RemainingAttackingTroopCount <= 0 || run.GarrisonTroopCount > 0)
                return true;

            int systemSide = 0;
            Faction owner = _game.GetFactionByOwnerInstanceID(run.Planet.GetOwnerInstanceID());
            if (owner != null)
                systemSide = owner.IsAIControlled() ? 1 : 2;

            run.GarrisonRequirement = CalculateGarrisonRequirement(
                systemSide,
                support: 0,
                coreSupportFlag: 0,
                uprisingActive: 0,
                applyUprisingMultiplier: 0
            );
            run.Result.GarrisonRequirement = run.GarrisonRequirement;

            LandAttackerTroopsUpToRequirement(run, run.GarrisonRequirement);
            return true;
        }

        /// <summary>
        /// Lands the first N attacker regiments aboard orbiting capital ships onto the
        /// planet, where N is the garrison requirement. Transfers planet ownership to the
        /// attacker before landing so the regiments can attach. Stops once the requirement
        /// is met or no more attacker troops remain.
        /// </summary>
        /// <param name="run">Run state.</param>
        /// <param name="requirement">Maximum number of regiments to land.</param>
        private void LandAttackerTroopsUpToRequirement(DefenseRun run, int requirement)
        {
            if (requirement <= 0)
                return;

            string defenderId = run.Planet.GetOwnerInstanceID();
            string attackerId = ResolveAttackerFactionId(run.AttackingFleets, defenderId);
            if (attackerId == null)
                return;

            Faction attackerFaction = _game.GetFactionByOwnerInstanceID(attackerId);
            if (attackerFaction == null)
                return;

            _ownership.TransferPlanet(run.Planet, attackerFaction);

            int landed = 0;
            foreach (Fleet fleet in run.AttackingFleets)
            {
                if (fleet.GetOwnerInstanceID() == defenderId)
                    continue;
                foreach (CapitalShip ship in fleet.CapitalShips)
                {
                    foreach (Regiment regiment in ship.Regiments.ToList())
                    {
                        if (landed >= requirement)
                            return;
                        _game.MoveNode(regiment, run.Planet);
                        landed++;
                    }
                }
            }
        }

        private static string ResolveAttackerFactionId(List<Fleet> fleets, string defenderOwnerId)
        {
            foreach (Fleet fleet in fleets)
            {
                string owner = fleet.GetOwnerInstanceID();
                if (owner != defenderOwnerId)
                    return owner;
            }
            return null;
        }

        /// <summary>
        /// Stage 6 — release combat lock on every fleet involved in the engagement.
        /// </summary>
        /// <param name="run">Run state.</param>
        private static void Stage6_Cleanup(DefenseRun run)
        {
            foreach (Fleet attacker in run.AttackingFleets)
                attacker.IsInCombat = false;

            foreach (Fleet defender in run.Planet.Fleets)
                defender.IsInCombat = false;
        }

        /// <summary>Counts regiments carried by hostile capital ships orbiting the planet.</summary>
        private static int CountAttackingTroopsFromOrbitingShips(
            List<Fleet> fleets,
            string defendingOwnerId
        )
        {
            int count = 0;
            foreach (Fleet fleet in fleets)
            {
                if (fleet.GetOwnerInstanceID() == defendingOwnerId)
                    continue;
                foreach (CapitalShip ship in fleet.CapitalShips)
                    count += ship.Regiments.Count;
            }
            return count;
        }

        /// <summary>
        /// Sums per-ship bombardment strength across all fleets, using the commander's
        /// leadership as a multiplier: (personnel / divisor + 1) * ship.Bombardment.
        /// </summary>
        /// <param name="fleets">Attacking fleets.</param>
        /// <returns>Total bombardment strength (stage-4 trial count).</returns>
        private int CalculateFleetBombardmentStrength(List<Fleet> fleets)
        {
            int divisor = _game.Config.Combat.AssaultPersonnelDivisor;
            int strength = 0;
            foreach (Fleet fleet in fleets)
            {
                Officer commander = fleet
                    .GetOfficers()
                    .FirstOrDefault(o => o.CurrentRank == OfficerRank.General);
                int personnel = commander?.GetSkillValue(MissionParticipantSkill.Leadership) ?? 0;
                foreach (CapitalShip ship in fleet.CapitalShips)
                    strength += (personnel / divisor + 1) * ship.Bombardment;
            }
            return strength;
        }

        /// <summary>Probability gate: true with chance (successes / 100).</summary>
        private bool RollProbabilitySuccess(int successesPerHundred)
        {
            if (successesPerHundred <= 0)
                return false;
            if (successesPerHundred >= 100)
                return true;
            return _provider.NextInt(0, 100) < successesPerHundred;
        }

        /// <summary>N independent Bernoulli trials at the repeat-trial probability.</summary>
        private int CountSuccessfulRepeatTrials(int trialCount)
        {
            int prob = _game.Config.Combat.RepeatTrialProbability;
            int successes = 0;
            for (int i = 0; i < trialCount; i++)
            {
                if (RollProbabilitySuccess(prob))
                    successes++;
            }
            return successes;
        }

        /// <summary>
        /// Ground-combat duel: rolls one die, adds (attacker strength + commander bonus) -
        /// (defender strength + commander bonus), and compares against the win thresholds.
        /// Scores between the two thresholds are draws (both troops survive).
        /// </summary>
        /// <param name="attackerStrength">The attacker regiment's offensive row strength.</param>
        /// <param name="attackerCommanderRating">Leadership rating of the attacker's General (0 if none).</param>
        /// <param name="defenderStrength">The defender regiment's defensive row strength.</param>
        /// <param name="defenderCommanderRating">Leadership rating of the defender's General (0 if none).</param>
        /// <returns>Pair of flags: (defenderWins, attackerWins). Both false on a draw.</returns>
        private (bool DefenderWins, bool AttackerWins) ResolveTroopKillContest(
            int attackerStrength,
            int attackerCommanderRating,
            int defenderStrength,
            int defenderCommanderRating
        )
        {
            var config = _game.Config.Combat;
            int divisor = config.GroundCombatCommanderDivisor;
            int attackerBonus = divisor > 0 ? attackerCommanderRating / divisor : 0;
            int defenderBonus = divisor > 0 ? defenderCommanderRating / divisor : 0;
            int roll = _provider.NextInt(0, config.GroundCombatContestDiceRange + 1);
            int score =
                roll + (attackerBonus + attackerStrength) - (defenderBonus + defenderStrength);
            bool defenderWins = score <= config.GroundCombatDefenderWinsThreshold;
            bool attackerWins = score >= config.GroundCombatAttackerWinsThreshold;
            return (defenderWins, attackerWins);
        }

        /// <summary>
        /// Picks which target a repeat-damage strike hits: one of the planet's defender ships,
        /// fighters, or defense buildings; or the system energy; or the system's allocated
        /// energy. Ships/fighters/buildings are grouped and picked proportional to how many
        /// of them are on the planet; energy and allocated energy each count as a single lane.
        /// Returns None when there are no valid targets.
        /// </summary>
        /// <param name="shipGroupCount">Total count of defender ships + fighters + defense buildings.</param>
        /// <param name="energyPresent">True if the planet has remaining energy capacity.</param>
        /// <param name="allocatedPresent">True if the planet has any energy currently allocated.</param>
        /// <returns>The chosen lane; if ShipGroup, ShipGroupIndex is the index into the group list.</returns>
        private LaneChoice SelectRepeatDamageLane(
            int shipGroupCount,
            bool energyPresent,
            bool allocatedPresent
        )
        {
            int total = shipGroupCount + (energyPresent ? 1 : 0) + (allocatedPresent ? 1 : 0);
            if (total == 0)
                return new LaneChoice { Lane = Lane.None };

            int roll = _provider.NextInt(0, total);
            if (roll < shipGroupCount)
                return new LaneChoice { Lane = Lane.ShipGroup, ShipGroupIndex = roll };
            if (roll == shipGroupCount)
            {
                if (energyPresent)
                    return new LaneChoice { Lane = Lane.Energy };
                if (allocatedPresent)
                    return new LaneChoice { Lane = Lane.AllocatedEnergy };
            }
            else if (allocatedPresent)
            {
                return new LaneChoice { Lane = Lane.AllocatedEnergy };
            }
            return new LaneChoice { Lane = Lane.None };
        }

        /// <summary>
        /// Applies one stage-4 strike: destroys the picked ship/fighter/defense-building, or
        /// reduces system energy (or allocated energy). Records a BombardmentStrikeEvent.
        /// </summary>
        /// <param name="run">Run state (result is mutated).</param>
        /// <param name="choice">Lane picker output identifying the target.</param>
        /// <param name="ships">Current defender ship list.</param>
        /// <param name="fighters">Current defender fighter list.</param>
        /// <param name="defensiveBuildings">Current defender defense-building list.</param>
        private void ApplyRepeatDamageLane(
            DefenseRun run,
            LaneChoice choice,
            List<CapitalShip> ships,
            List<Starfighter> fighters,
            List<Building> defensiveBuildings
        )
        {
            BombardmentStrikeEvent strike = new BombardmentStrikeEvent();

            switch (choice.Lane)
            {
                case Lane.Energy:
                    if (run.Planet.EnergyCapacity > 0)
                    {
                        run.Planet.EnergyCapacity--;
                        run.Result.EnergyDamage++;
                        strike.Lane = BombardmentLaneType.Energy;
                        strike.TargetName = "System Energy";
                    }
                    break;
                case Lane.AllocatedEnergy:
                    if (run.Planet.AllocatedEnergy > 0)
                    {
                        run.Planet.AllocatedEnergy--;
                        run.Result.EnergyDamage++;
                        strike.Lane = BombardmentLaneType.Energy;
                        strike.TargetName = "Allocated Energy";
                    }
                    break;
                case Lane.ShipGroup:
                {
                    int idx = choice.ShipGroupIndex;
                    if (idx < ships.Count)
                    {
                        CapitalShip target = ships[idx];
                        strike.Lane = BombardmentLaneType.CapitalShip;
                        strike.Target = target;
                        strike.TargetName = target.GetDisplayName();
                        Fleet parentFleet = target.GetParentOfType<Fleet>();
                        if (parentFleet != null)
                            CombatHelpers.EvacuateOfficers(_game, _movement, target, parentFleet);
                        _game.DetachNode(target);
                    }
                    else if (idx < ships.Count + fighters.Count)
                    {
                        Starfighter target = fighters[idx - ships.Count];
                        strike.Lane = BombardmentLaneType.Starfighter;
                        strike.Target = target;
                        strike.TargetName = target.GetDisplayName();
                        run.Result.DestroyedStarfighters.Add(target);
                        _game.DetachNode(target);
                    }
                    else
                    {
                        int buildingIdx = idx - ships.Count - fighters.Count;
                        if (buildingIdx < defensiveBuildings.Count)
                        {
                            Building target = defensiveBuildings[buildingIdx];
                            strike.Lane = BombardmentLaneType.Building;
                            strike.Target = target;
                            strike.TargetName = target.GetDisplayName();
                            run.Result.DestroyedBuildings.Add(target);
                            _game.DetachNode(target);
                        }
                    }
                    break;
                }
            }

            run.Result.Strikes.Add(strike);
            run.Result.NetStrikes++;
        }

        /// <summary>
        /// How many garrison troops the defender needs on this planet: increases as popular
        /// support drops, decreases for a strong core system, and increases during an active
        /// uprising when the uprising multiplier applies.
        /// </summary>
        /// <param name="systemSide">1 = alliance-controlled, 2 = empire-controlled.</param>
        /// <param name="support">Popular support for the controller (0-100).</param>
        /// <param name="coreSupportFlag">Non-zero if this is a core system with strong support.</param>
        /// <param name="uprisingActive">Non-zero if an uprising is currently active.</param>
        /// <param name="applyUprisingMultiplier">Non-zero to apply the uprising multiplier.</param>
        /// <returns>Required garrison troop count.</returns>
        private static int CalculateGarrisonRequirement(
            int systemSide,
            int support,
            int coreSupportFlag,
            int uprisingActive,
            int applyUprisingMultiplier
        )
        {
            int required = Math.Max(0, 100 - support);
            if (coreSupportFlag != 0 && systemSide == 2)
                required /= 2;
            if (uprisingActive != 0 && applyUprisingMultiplier != 0)
                required *= 2;
            return required;
        }

        /// <summary>Mutable state threaded through the 6 defense-combat stages.</summary>
        private class DefenseRun
        {
            public Planet Planet;
            public List<Fleet> AttackingFleets;
            public BombardmentResult Result;
            public int InitialAttackingTroopCount;
            public int RemainingAttackingTroopCount;
            public int InitialGarrisonTroopCount;
            public int GarrisonTroopCount;
            public int StageFourTrials;
            public int GarrisonRequirement;
        }

        private enum Lane
        {
            None,
            ShipGroup,
            Energy,
            AllocatedEnergy,
        }

        private struct LaneChoice
        {
            public Lane Lane;
            public int ShipGroupIndex;
        }
    }

    internal static class CombatHelpers
    {
        /// <summary>
        /// Moves officers off a destroyed ship. Prefers another surviving ship in the same
        /// fleet; falls back to the nearest friendly planet via MovementSystem.
        /// </summary>
        public static void EvacuateOfficers(
            GameRoot game,
            MovementSystem movement,
            CapitalShip ship,
            Fleet fleet
        )
        {
            List<Officer> officers = ship.Officers.ToList();
            if (officers.Count == 0)
                return;

            CapitalShip survivingShip = fleet?.CapitalShips.FirstOrDefault(s =>
                !ReferenceEquals(s, ship) && s.CurrentHullStrength > 0
            );

            foreach (Officer officer in officers)
            {
                if (survivingShip != null)
                {
                    game.DetachNode(officer);
                    game.AttachNode(officer, survivingShip);
                    GameLogger.Log(
                        $"{officer.GetDisplayName()} evacuated to {survivingShip.GetDisplayName()} after {ship.GetDisplayName()} destroyed."
                    );
                }
                else
                {
                    movement.EvacuateToNearestFriendlyPlanet(officer);
                }
            }
        }
    }
}
