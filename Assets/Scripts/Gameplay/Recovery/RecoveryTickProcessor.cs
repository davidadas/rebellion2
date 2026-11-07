using System.Collections.Generic;
using Rebellion.Game;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Results;
using Rebellion.Game.Units;
using Rebellion.SceneGraph;

namespace Rebellion.Gameplay
{
    /// <summary>
    /// Heals injured officers, repairs capital ships, and replenishes fighter squadrons each tick.
    /// </summary>
    internal sealed class RecoveryTickProcessor : ITickProcessor
    {
        /// <summary>
        /// Creates recovery tick processing.
        /// </summary>
        public RecoveryTickProcessor() { }

        /// <summary>
        /// Processes one tick of recovery for all officers, ships, and squadrons.
        /// </summary>
        /// <param name="game">The game state being advanced.</param>
        /// <returns>Results emitted when a unit fully recovers.</returns>
        public IReadOnlyList<GameResult> ProcessTick(GameRoot game)
        {
            List<GameResult> results = new List<GameResult>();
            GameConfig.RecoveryConfig config = game.Config.Recovery;

            HealOfficers(game, config, results);
            RepairShips(game, config, results);
            ReplaceSquadronLosses(game, config, results);

            return results;
        }

        /// <summary>
        /// Heals each injured, non-captured officer by the configured amount.
        /// Emits a result only when the officer is fully healed.
        /// </summary>
        /// <param name="game">The game state being advanced.</param>
        /// <param name="config">The recovery configuration.</param>
        /// <param name="results">Collection to append healing results to.</param>
        private static void HealOfficers(
            GameRoot game,
            GameConfig.RecoveryConfig config,
            List<GameResult> results
        )
        {
            foreach (Officer officer in game.GetSceneNodesByType<Officer>())
            {
                if (!officer.CanHeal())
                    continue;

                int amount = officer.HealsFast() ? config.FastHealAmount : config.NormalHealAmount;
                officer.Heal(amount);

                if (officer.InjuryPoints == 0)
                {
                    results.Add(
                        new OfficerInjuredResult
                        {
                            Officer = officer,
                            ParentAtInjury = officer.GetParent(),
                            Severity = 0,
                            Tick = game.CurrentTick,
                        }
                    );
                }
            }
        }

        /// <summary>
        /// Repairs hull damage on each damaged capital ship.
        /// Ships at friendly orbital shipyards repair faster.
        /// Emits a result only when the ship is fully repaired.
        /// </summary>
        /// <param name="game">The game state being advanced.</param>
        /// <param name="config">The recovery configuration.</param>
        /// <param name="results">Collection to append repair results to.</param>
        private static void RepairShips(
            GameRoot game,
            GameConfig.RecoveryConfig config,
            List<GameResult> results
        )
        {
            HashSet<(string Planet, string Owner)> shipyards = null;
            foreach (CapitalShip ship in game.GetSceneNodesByType<CapitalShip>())
            {
                bool damagedSystems = ship.SystemDamage.Total > 0;
                if (!ship.IsDamaged())
                    continue;
                if (
                    ship.ManufacturingStatus != ManufacturingStatus.Complete
                    || ((IMovable)ship).GetTransitMovement() != null
                )
                {
                    ship.SystemDamage.RepairDueTick = 0;
                    ship.SystemDamage.RepairInterval = 0;
                    continue;
                }
                shipyards ??= GetShipyards(game);
                Planet planet = ship.GetParentOfType<Planet>();
                bool atShipyard =
                    planet != null
                    && planet.OwnerInstanceID == ship.OwnerInstanceID
                    && shipyards.Contains((planet.InstanceID, ship.OwnerInstanceID));
                int before = ship.CurrentHullStrength;
                if (damagedSystems)
                    RepairSystems(game, ship, atShipyard);
                if (ship.CurrentHullStrength < ship.MaxHullStrength)
                    ship.RepairHull(
                        atShipyard ? config.FastRepairAmount : config.NormalRepairAmount
                    );
                if (!ship.IsDamaged())
                {
                    results.Add(
                        new ShipHullDamageResult
                        {
                            Ship = ship,
                            OldHull = before,
                            NewHull = ship.CurrentHullStrength,
                            Tick = game.CurrentTick,
                        }
                    );
                }
            }
        }

        /// <summary>
        /// Indexes operational shipyards once for the tick's damaged ships.
        /// </summary>
        /// <param name="game">The active campaign graph.</param>
        /// <returns>The planet and owner pairs that provide accelerated repair.</returns>
        private static HashSet<(string Planet, string Owner)> GetShipyards(GameRoot game)
        {
            HashSet<(string Planet, string Owner)> result = new();
            foreach (Building building in game.GetSceneNodesByType<Building>())
            {
                if (
                    building.BuildingType == BuildingType.Shipyard
                    && building.ManufacturingStatus == ManufacturingStatus.Complete
                    && building.Movement == null
                    && building.GetParent() is Planet planet
                )
                    result.Add((planet.InstanceID, building.OwnerInstanceID));
            }
            return result;
        }

        /// <summary>
        /// Advances the ship's persistent repair timer and attempts one weighted component repair.
        /// </summary>
        /// <param name="game">The active campaign and random stream.</param>
        /// <param name="ship">The damaged ship.</param>
        /// <param name="atShipyard">Whether an operational friendly shipyard is available.</param>
        private static void RepairSystems(GameRoot game, CapitalShip ship, bool atShipyard)
        {
            CapitalShipSystemDamage damage = ship.SystemDamage;
            int interval = ship.GetSystemRepairInterval(atShipyard);
            if (damage.RepairDueTick == 0 || damage.RepairInterval != interval)
            {
                damage.RepairInterval = interval;
                damage.RepairDueTick = game.CurrentTick + interval;
            }
            if (game.CurrentTick < damage.RepairDueTick)
                return;
            damage.RepairDueTick = game.CurrentTick + interval;
            if (game.Random.NextInt(1, 101) <= ship.DamageControl)
                damage.RepairComponent(game.Random.NextInt(0, damage.Total));
            if (damage.Total == 0)
            {
                damage.RepairDueTick = 0;
                damage.RepairInterval = 0;
            }
        }

        /// <summary>
        /// Replaces lost fighters in each depleted squadron.
        /// Squadrons at friendly planets replace faster.
        /// Emits a result only when the squadron is back to full strength.
        /// </summary>
        /// <param name="game">The game state being advanced.</param>
        /// <param name="config">The recovery configuration.</param>
        /// <param name="results">Collection to append replacement results to.</param>
        private static void ReplaceSquadronLosses(
            GameRoot game,
            GameConfig.RecoveryConfig config,
            List<GameResult> results
        )
        {
            foreach (Starfighter squadron in game.GetSceneNodesByType<Starfighter>())
            {
                if (
                    !squadron.HasLosses()
                    || squadron.ManufacturingStatus != ManufacturingStatus.Complete
                )
                    continue;

                int before = squadron.CurrentSquadronSize;
                int amount = IsAtFriendlyPlanet(squadron)
                    ? config.FastReplacementAmount
                    : config.NormalReplacementAmount;
                squadron.ReplaceFighters(amount);

                if (!squadron.HasLosses())
                {
                    results.Add(
                        new FighterDamageResult
                        {
                            Fighter = squadron,
                            OldSize = before,
                            NewSize = squadron.CurrentSquadronSize,
                            Tick = game.CurrentTick,
                        }
                    );
                }
            }
        }

        /// <summary>
        /// Returns true if the unit is at a planet owned by the unit's faction.
        /// </summary>
        /// <param name="unit">The scene node to check.</param>
        /// <returns>True if the unit is at a friendly planet.</returns>
        private static bool IsAtFriendlyPlanet(ISceneNode unit)
        {
            Planet planet = unit.GetParentOfType<Planet>();
            return planet != null && planet.OwnerInstanceID == unit.OwnerInstanceID;
        }
    }
}
