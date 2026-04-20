using System.Collections.Generic;
using Rebellion.Game;
using Rebellion.Game.Results;
using Rebellion.SceneGraph;

namespace Rebellion.Systems
{
    /// <summary>
    /// Heals injured officers and repairs damaged ships each tick.
    /// Officers heal based on CanHeal/FastHeal flags. Ships repair hull damage
    /// at a rate determined by whether they are at a friendly planet.
    /// </summary>
    public class RecoverySystem : IGameSystem
    {
        private readonly GameRoot _game;
        private readonly GameConfig.RecoveryConfig _config;

        /// <summary>
        /// Creates a new RecoverySystem.
        /// </summary>
        /// <param name="game">The active game state.</param>
        public RecoverySystem(GameRoot game)
        {
            _game = game;
            _config = game.Config.Recovery;
        }

        /// <summary>
        /// Processes one tick of recovery for all officers, ships, and squadrons.
        /// </summary>
        /// <returns>Results emitted when a unit fully recovers.</returns>
        public List<GameResult> ProcessTick()
        {
            List<GameResult> results = new List<GameResult>();

            HealOfficers(results);
            RepairShips(results);
            ReplaceSquadronLosses(results);

            return results;
        }

        /// <summary>
        /// Heals each injured, non-captured officer by the configured amount.
        /// Emits a result only when the officer is fully healed.
        /// </summary>
        /// <param name="results">Collection to append healing results to.</param>
        private void HealOfficers(List<GameResult> results)
        {
            foreach (Officer officer in _game.GetSceneNodesByType<Officer>())
            {
                if (officer.InjuryPoints <= 0 || !officer.CanHeal || officer.IsCaptured)
                    continue;

                int amount = officer.FastHeal ? _config.FastHealAmount : _config.NormalHealAmount;
                officer.Heal(amount);

                if (officer.InjuryPoints == 0)
                {
                    results.Add(
                        new OfficerInjuredResult
                        {
                            Officer = officer,
                            Severity = 0,
                            Tick = _game.CurrentTick,
                        }
                    );
                }
            }
        }

        /// <summary>
        /// Repairs hull damage on each damaged capital ship.
        /// Ships at friendly planets repair faster.
        /// Emits a result only when the ship is fully repaired.
        /// </summary>
        /// <param name="results">Collection to append repair results to.</param>
        private void RepairShips(List<GameResult> results)
        {
            foreach (CapitalShip ship in _game.GetSceneNodesByType<CapitalShip>())
            {
                if (!ship.IsDamaged() || ship.ManufacturingStatus != ManufacturingStatus.Complete)
                    continue;

                int before = ship.CurrentHullStrength;
                int amount = IsAtFriendlyPlanet(ship)
                    ? _config.FastRepairAmount
                    : _config.NormalRepairAmount;
                ship.RepairHull(amount);

                if (!ship.IsDamaged())
                {
                    results.Add(
                        new ShipHullDamageResult
                        {
                            Ship = ship,
                            OldHull = before,
                            NewHull = ship.CurrentHullStrength,
                            Tick = _game.CurrentTick,
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
        /// <param name="results">Collection to append replacement results to.</param>
        private void ReplaceSquadronLosses(List<GameResult> results)
        {
            foreach (Starfighter squadron in _game.GetSceneNodesByType<Starfighter>())
            {
                if (
                    !squadron.HasLosses()
                    || squadron.ManufacturingStatus != ManufacturingStatus.Complete
                )
                    continue;

                int before = squadron.CurrentSquadronSize;
                int amount = IsAtFriendlyPlanet(squadron)
                    ? _config.FastReplacementAmount
                    : _config.NormalReplacementAmount;
                squadron.ReplaceFighters(amount);

                if (!squadron.HasLosses())
                {
                    results.Add(
                        new FighterDamageResult
                        {
                            Fighter = squadron,
                            OldSize = before,
                            NewSize = squadron.CurrentSquadronSize,
                            Tick = _game.CurrentTick,
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
        private bool IsAtFriendlyPlanet(ISceneNode unit)
        {
            Planet planet = unit.GetParentOfType<Planet>();
            return planet != null && planet.OwnerInstanceID == unit.OwnerInstanceID;
        }
    }
}
