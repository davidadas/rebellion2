using System;
using System.Collections.Generic;
using System.Linq;
using Rebellion.Game.Combat;
using Rebellion.Game.Results;
using Rebellion.Game.SpaceCombat;
using Rebellion.Game.Units;
using Rebellion.SceneGraph;
using Rebellion.Tactical;
using InvalidDataException = global::System.IO.InvalidDataException;

namespace Rebellion.Gameplay
{
    public partial class SpaceCombatCommands
    {
        /// <summary>
        /// Restores a locally completed battle's facts without running remote-input admission rules.
        /// </summary>
        /// <param name="outcome">The completed isolated encounter's detached outcome.</param>
        /// <returns>The uncommitted result referencing only the captured local participants.</returns>
        private SpaceCombatResult CreateTacticalResult(TacticalCombatOutcome outcome)
        {
            SpaceCombatResult result = new()
            {
                Winner = outcome.Winner,
                AttackerOutcome = outcome.AttackerOutcome,
                DefenderOutcome = outcome.DefenderOutcome,
                IterationsCompleted = outcome.IterationsCompleted,
            };
            Dictionary<string, TacticalCombatShipOutcome> ships = outcome.Ships.ToDictionary(
                ship => ship.UnitID,
                StringComparer.Ordinal
            );
            Dictionary<string, TacticalCombatFighterOutcome> fighters =
                outcome.Fighters.ToDictionary(fighter => fighter.UnitID, StringComparer.Ordinal);
            foreach (TacticalCombatUnit unit in _tacticalCombat.Units)
            {
                ISceneNode source = ResolveTacticalSource(unit);
                bool withdrawn;
                if (source is CapitalShip ship)
                {
                    TacticalCombatShipOutcome state = ships[unit.InstanceID];
                    result.ShipDamage.Add(
                        new ShipDamageResult
                        {
                            Ship = ship,
                            HullBefore = (int)unit.InitialHull,
                            HullAfter = state.Hull,
                            SystemDamage = state.SystemDamage,
                        }
                    );
                    withdrawn = state.Withdrawn;
                }
                else
                {
                    TacticalCombatFighterOutcome state = fighters[unit.InstanceID];
                    Starfighter fighter = (Starfighter)source;
                    result.FighterLosses.Add(
                        new FighterLossResult
                        {
                            Fighter = fighter,
                            SquadsBefore = unit.InitialSquadronSize,
                            SquadsAfter = state.SquadronSize,
                        }
                    );
                    result.FighterRecovery.Add(
                        new FighterRecoveryResult
                        {
                            Fighter = fighter,
                            Carrier =
                                state.CarrierID != null
                                    ? (CapitalShip)ResolveTacticalSource(
                                        _tacticalCombat.GetUnit(state.CarrierID)
                                    )
                                    : null,
                        }
                    );
                    withdrawn = state.Withdrawn;
                }
                if (withdrawn)
                    result.WithdrawnUnits.Add(source);
            }
            return result;
        }

        /// <summary>
        /// Resolves a battle copy's stable source identity only when campaign results are committed.
        /// </summary>
        /// <param name="unit">The tactical unit whose campaign source is required.</param>
        /// <returns>The original campaign scene node.</returns>
        private ISceneNode ResolveTacticalSource(TacticalCombatUnit unit)
        {
            string sourceID = unit?.Combatant?.SourceUnitInstanceID;
            ISceneNode source = _game.GetSceneNodeByInstanceID<ISceneNode>(
                sourceID,
                includeDisabled: true
            );
            if (source == null)
                throw new InvalidDataException(
                    $"Tactical source '{sourceID}' is no longer present in the campaign."
                );
            return source;
        }

        /// <summary>
        /// Commits only battle-owned damage and survivor counts after the campaign result pipeline
        /// has performed its destruction, recovery, and event work.
        /// </summary>
        /// <param name="result">The completed combat result containing surviving campaign units.</param>
        private void ReconcileBattleCopies(SpaceCombatResult result)
        {
            Dictionary<string, CapitalShip> ships = result.ShipDamage.ToDictionary(
                damage => damage.Ship.InstanceID,
                damage => damage.Ship,
                StringComparer.Ordinal
            );
            Dictionary<string, Starfighter> fighters = result.FighterLosses.ToDictionary(
                loss => loss.Fighter.InstanceID,
                loss => loss.Fighter,
                StringComparer.Ordinal
            );
            foreach (TacticalCombatUnit unit in _tacticalCombat.Units)
            {
                if (ships.TryGetValue(unit.InstanceID, out CapitalShip ship))
                    BattleResultReconciler.Apply(unit.Combatant, ship);
                else if (fighters.TryGetValue(unit.InstanceID, out Starfighter fighter))
                    BattleResultReconciler.Apply(unit.Combatant, fighter);
                else
                    throw new InvalidDataException(
                        $"Tactical result did not include combatant '{unit.InstanceID}'."
                    );
            }
        }
    }
}
