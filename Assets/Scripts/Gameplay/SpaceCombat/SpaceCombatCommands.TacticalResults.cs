using System;
using System.Collections.Generic;
using System.Linq;
using Rebellion.Game.Results;
using Rebellion.Game.SpaceCombat;
using Rebellion.Game.Units;
using Rebellion.SceneGraph;
using Rebellion.Tactical;

namespace Rebellion.Gameplay
{
    public partial class SpaceCombatCommands
    {
        /// <summary>
        /// Retains campaign references solely at the campaign's encounter import boundary.
        /// </summary>
        private void CaptureTacticalSources()
        {
            _tacticalSources = new Dictionary<string, ISceneNode>(StringComparer.Ordinal);
            foreach (TacticalCombatUnit unit in _tacticalCombat.Units)
                _tacticalSources.Add(unit.InstanceID, _game.NodesByInstanceID[unit.InstanceID]);
        }

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
                ISceneNode source = _tacticalSources[unit.InstanceID];
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
                                    ? (CapitalShip)_tacticalSources[state.CarrierID]
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
    }
}
