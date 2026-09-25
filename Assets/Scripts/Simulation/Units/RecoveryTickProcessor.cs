using System.Collections.Generic;
using System.Linq;
using Rebellion.Game;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Results;
using Rebellion.Game.Units;
using Rebellion.SceneGraph;

namespace Rebellion.Simulation
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

                int amount = officer.HealsFast(game.Config.Jedi.FastHealThreshold)
                    ? config.FastHealAmount
                    : config.NormalHealAmount;
                officer.Heal(amount);

                if (officer.InjuryPoints == 0)
                {
                    results.Add(
                        new OfficerInjuredResult
                        {
                            Officer = officer,
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
            foreach (CapitalShip ship in game.GetSceneNodesByType<CapitalShip>())
            {
                if (
                    !ship.IsDamaged()
                    || ship.ManufacturingStatus != ManufacturingStatus.Complete
                    || ((IMovable)ship).GetTransitMovement() != null
                )
                    continue;

                int before = ship.CurrentHullStrength;
                int amount = IsAtFriendlyShipyard(ship)
                    ? config.FastRepairAmount
                    : config.NormalRepairAmount;
                ship.RepairHull(amount);

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

        /// <summary>
        /// Returns true if the ship is at a friendly planet with an operational orbital shipyard.
        /// </summary>
        /// <param name="ship">The capital ship to check.</param>
        /// <returns>True when a friendly operational shipyard can accelerate repairs.</returns>
        private static bool IsAtFriendlyShipyard(CapitalShip ship)
        {
            Planet planet = ship.GetParentOfType<Planet>();
            return planet != null
                && planet.OwnerInstanceID == ship.OwnerInstanceID
                && planet
                    .GetChildren<Building>()
                    .Any(building =>
                        building.OwnerInstanceID == ship.OwnerInstanceID
                        && building.BuildingType == BuildingType.Shipyard
                        && building.ManufacturingStatus == ManufacturingStatus.Complete
                        && building.Movement == null
                    );
        }
    }
}
