using System;
using System.Collections.Generic;
using System.Linq;
using Rebellion.Game;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Results;
using Rebellion.Game.Units;

namespace Rebellion.Gameplay
{
    public partial class SpaceCombatCommands
    {
        /// <summary>
        /// Records a participant's response and resolves only after every human participant has responded.
        /// AI-only encounters continue through their existing automatic path.
        /// </summary>
        /// <param name="factionID">The authenticated responding faction.</param>
        /// <param name="retreat">Whether that faction chooses withdrawal.</param>
        /// <param name="results">The resolved results, or null while another human response is outstanding.</param>
        /// <param name="tacticalRoster">The admitted roster for a response taking manual control.</param>
        /// <returns>Whether the response was valid for the pending encounter.</returns>
        internal bool TrySubmitResponse(
            string factionID,
            bool retreat,
            out List<GameResult> results,
            TacticalCombatFactory.EncounterRoster tacticalRoster = null
        )
        {
            results = null;
            SpaceCombatDecision decision = _pendingDecision;
            if (
                decision == null
                || _tacticalEncounter.HasValue
                || (
                    factionID != decision.AttackerOwnerInstanceID
                    && factionID != decision.DefenderOwnerInstanceID
                )
            )
                return false;
            bool takeCommand = tacticalRoster != null;
            bool attacker = factionID == decision.AttackerOwnerInstanceID;
            bool? previous = attacker
                ? decision.AttackerRetreatResponse
                : decision.DefenderRetreatResponse;
            bool previousCommand = attacker
                ? decision.AttackerTakeCommand
                : decision.DefenderTakeCommand;
            if (retreat)
            {
                PendingCombatResult pending = _queries.BuildPendingCombatResult(decision);
                if (!(attacker ? pending.AttackerCanRetreat : pending.DefenderCanRetreat))
                    return false;
            }
            if (attacker)
            {
                decision.AttackerRetreatResponse = retreat;
                decision.AttackerTakeCommand = takeCommand;
            }
            else
            {
                decision.DefenderRetreatResponse = retreat;
                decision.DefenderTakeCommand = takeCommand;
            }
            if (
                (
                    RequiresHumanResponse(decision.AttackerOwnerInstanceID)
                    && !decision.AttackerRetreatResponse.HasValue
                )
                || (
                    RequiresHumanResponse(decision.DefenderOwnerInstanceID)
                    && !decision.DefenderRetreatResponse.HasValue
                )
            )
                return true;
            if (
                decision.AttackerRetreatResponse == true
                && decision.DefenderRetreatResponse == true
            )
            {
                results = ResolveBothWithdrawals(decision);
                if (results != null)
                    _pendingDecision = null;
            }
            else if (
                decision.AttackerRetreatResponse == true
                || decision.DefenderRetreatResponse == true
            )
                results = ResolvePendingRetreat(
                    decision.AttackerRetreatResponse == true
                        ? decision.AttackerOwnerInstanceID
                        : decision.DefenderOwnerInstanceID
                );
            else if (decision.AttackerTakeCommand || decision.DefenderTakeCommand)
            {
                BeginNetworkTacticalCombat(decision, tacticalRoster);
                return true;
            }
            else
                results = ResolvePending(true);
            if (results != null)
                return true;
            if (attacker)
            {
                decision.AttackerRetreatResponse = previous;
                decision.AttackerTakeCommand = previousCommand;
            }
            else
            {
                decision.DefenderRetreatResponse = previous;
                decision.DefenderTakeCommand = previousCommand;
            }
            return false;
        }

        /// <summary>
        /// Records a manual preference through the same participant response barrier as automatic combat and retreat.
        /// </summary>
        /// <param name="factionID">The authenticated participant taking command.</param>
        /// <param name="battleID">The host-issued identity of the first manual response.</param>
        /// <param name="seed">The host-issued battle seed.</param>
        /// <param name="scenerySeed">The host-issued scenery seed.</param>
        /// <param name="results">The resolved withdrawal result, or null while waiting or entering manual combat.</param>
        /// <returns>Whether this pending encounter admitted the manual response.</returns>
        internal bool TrySubmitTacticalResponse(
            string factionID,
            string battleID,
            uint seed,
            int scenerySeed,
            out List<GameResult> results
        )
        {
            results = null;
            SpaceCombatDecision decision = _pendingDecision;
            if (
                _game.Metadata?.IsMultiplayer != true
                || !Guid.TryParseExact(battleID, "N", out _)
                || _tacticalEncounter.HasValue
                || decision == null
                || factionID != decision.AttackerOwnerInstanceID
                    && factionID != decision.DefenderOwnerInstanceID
                || _tacticalFactory == null
                || !_tacticalFactory.TryPrepare(decision, out var roster)
            )
                return false;
            try
            {
                _aftermath?.ValidateBattle(roster.Planet);
            }
            catch (InvalidOperationException)
            {
                return false;
            }
            if (
                string.IsNullOrEmpty(decision.TacticalBattleID)
                || !decision.AttackerTakeCommand && !decision.DefenderTakeCommand
            )
            {
                decision.TacticalBattleID = battleID;
                decision.TacticalSeed = seed;
                decision.TacticalScenerySeed = scenerySeed;
            }
            return TrySubmitResponse(factionID, false, out results, roster);
        }

        /// <summary>
        /// Determines whether a combat side is owned by a human participant who must respond.
        /// </summary>
        /// <param name="factionID">The participating faction.</param>
        /// <returns>True when the existing controller assignment is human.</returns>
        private bool RequiresHumanResponse(string factionID) =>
            _game.GetFactionPlayer(factionID)?.ControllerType == PlayerControllerType.Human;

        /// <summary>
        /// Prepares both existing manual withdrawal plans before applying either side's movement.
        /// Both withdrawals produce a draw and retain each side's ordinary withdrawal eligibility and losses.
        /// </summary>
        /// <param name="decision">The encounter for which both participants selected withdrawal.</param>
        /// <returns>The combined result, or null if either withdrawal is unavailable.</returns>
        private List<GameResult> ResolveBothWithdrawals(SpaceCombatDecision decision)
        {
            Planet planet = _queries.ResolveCombatPlanet(decision);
            List<Fleet> attackers = _queries.GetFleets(decision.AttackerFleetInstanceIDs);
            List<Fleet> defenders = _queries.GetFleets(decision.DefenderFleetInstanceIDs);
            SpaceCombatResult result = BuildRetreatResult(
                decision,
                true,
                attackers,
                defenders,
                planet
            );
            SpaceCombatWithdrawalPlan attackerPlan = _withdrawalResolver.ResolveManualWithdrawal(
                result,
                attackers,
                defenders,
                decision.AttackerOwnerInstanceID,
                true,
                planet
            );
            SpaceCombatWithdrawalPlan defenderPlan = _withdrawalResolver.ResolveManualWithdrawal(
                result,
                defenders,
                attackers,
                decision.DefenderOwnerInstanceID,
                false,
                planet
            );
            if (attackerPlan == null || defenderPlan == null)
                return null;
            result.Winner = SpaceCombatResult.BattleOutcome.Draw;
            result.AttackerRetreatPlanetInstanceID = attackerPlan.GetRetreatPlanetInstanceID();
            result.DefenderRetreatPlanetInstanceID = defenderPlan.GetRetreatPlanetInstanceID();
            result.Events = ApplyCombatResult(
                result,
                attackers,
                defenders,
                attackerPlan,
                defenderPlan
            );
            List<GameResult> results = new() { result };
            results.AddRange(
                result.Events.Where(resultEvent =>
                    resultEvent is GameObjectDestroyedResult or BlockadeChangedResult
                )
            );
            ClearCombatFlags(decision);
            return results;
        }
    }
}
