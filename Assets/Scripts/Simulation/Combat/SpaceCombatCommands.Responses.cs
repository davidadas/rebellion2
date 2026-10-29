using System.Collections.Generic;
using System.Linq;
using Rebellion.Game;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Results;
using Rebellion.Game.Units;

namespace Rebellion.Simulation
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
        /// <returns>Whether the response was valid for the pending encounter.</returns>
        internal bool TrySubmitResponse(
            string factionID,
            bool retreat,
            out List<GameResult> results
        )
        {
            results = null;
            SpaceCombatDecision decision = _pendingDecision;
            if (
                decision == null
                || (
                    factionID != decision.AttackerOwnerInstanceID
                    && factionID != decision.DefenderOwnerInstanceID
                )
            )
                return false;
            bool attacker = factionID == decision.AttackerOwnerInstanceID;
            bool? previous = attacker
                ? decision.AttackerRetreatResponse
                : decision.DefenderRetreatResponse;
            if (retreat)
            {
                PendingCombatResult pending = _queries.BuildPendingCombatResult(decision);
                if (!(attacker ? pending.AttackerCanRetreat : pending.DefenderCanRetreat))
                    return false;
            }
            if (attacker)
                decision.AttackerRetreatResponse = retreat;
            else
                decision.DefenderRetreatResponse = retreat;
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
            else
                results = ResolvePending(true);
            if (results != null)
                return true;
            if (attacker)
                decision.AttackerRetreatResponse = previous;
            else
                decision.DefenderRetreatResponse = previous;
            return false;
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
            result.Winner = CombatSide.Draw;
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
