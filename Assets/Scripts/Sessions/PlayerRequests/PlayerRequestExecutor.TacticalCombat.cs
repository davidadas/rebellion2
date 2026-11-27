using System;
using Rebellion.Game;
using Rebellion.Game.Factions;
using Rebellion.Game.PlayerRequests;
using Rebellion.Game.Results;
using Rebellion.Gameplay;

namespace Rebellion.PlayerRequests
{
    public sealed partial class PlayerRequestExecutor
    {
        /// <summary>
        /// Records one admitted commander's manual response to the displayed shared encounter.
        /// </summary>
        /// <param name="actor">The authenticated responding faction.</param>
        /// <param name="request">The pending encounter and host-issued entry metadata.</param>
        /// <returns>The accepted response, optional withdrawal result, or explicit rejection.</returns>
        private PlayerRequestOutcome BeginTacticalCombat(
            Faction actor,
            BeginTacticalCombatRequest request
        )
        {
            MultiplayerCampaignIdentity identity = _session.Game.Metadata?.MultiplayerCampaign;
            if (
                _session.Game.Metadata?.IsMultiplayer != true
                || identity == null
                || !Guid.TryParseExact(request.BattleID, "N", out _)
                || !_session
                    .GetService<SpaceCombatCommands>()
                    .TryGetPendingCombat(out PendingCombatResult pending)
                || pending.PlanetInstanceID != request.PlanetID
                || pending.Tick != request.Tick
                || actor.InstanceID != pending.AttackerOwnerInstanceID
                    && actor.InstanceID != pending.DefenderOwnerInstanceID
                || !ContainsBothParticipants(pending, identity)
            )
                return Reject("The selected battle cannot accept this manual response.");

            return _session.Tick.TrySubmitTacticalCombatResponse(
                actor.InstanceID,
                request.BattleID,
                request.Seed,
                request.ScenerySeed,
                out SpaceCombatResult result
            )
                ? new PlayerRequestOutcome(true, spaceCombatResult: result)
                : Reject("The manual battle decision is unavailable.");
        }

        /// <summary>
        /// Imports final losses only from the admitted host and the exact active encounter.
        /// </summary>
        /// <param name="actor">The authenticated issuing faction.</param>
        /// <param name="request">The shared encounter identity and authoritative outcome.</param>
        /// <returns>The accepted campaign result or an explicit rejection before mutation.</returns>
        private PlayerRequestOutcome CompleteTacticalCombat(
            Faction actor,
            CompleteTacticalCombatRequest request
        )
        {
            MultiplayerCampaignIdentity identity = _session.Game.Metadata?.MultiplayerCampaign;
            SpaceCombatCommands combat = _session.GetService<SpaceCombatCommands>();
            if (
                _session.Game.Metadata?.IsMultiplayer != true
                || identity == null
                || actor.InstanceID != identity.HostFactionID
                || combat.ActiveTacticalCombat == null
                || !string.Equals(
                    combat.ActiveTacticalBattleID,
                    request.BattleID,
                    StringComparison.Ordinal
                )
                || !combat.TryGetPendingCombat(out PendingCombatResult pending)
                || pending.PlanetInstanceID != request.PlanetID
                || pending.Tick != request.Tick
            )
                return Reject("Only the host may commit the active shared battle.");

            return _session.Tick.TryCompleteNetworkTacticalCombat(
                request.BattleID,
                request.Outcome,
                out SpaceCombatResult result
            )
                ? new PlayerRequestOutcome(true, spaceCombatResult: result)
                : Reject("The shared battle outcome is invalid or incomplete.");
        }

        /// <summary>
        /// Matches the encounter's two command owners to the campaign's authenticated faction pair.
        /// </summary>
        /// <param name="pending">The displayed pending encounter.</param>
        /// <param name="identity">The admitted host and guest ownership.</param>
        /// <returns>Whether both participants own exactly one of the opposing combat sides.</returns>
        private static bool ContainsBothParticipants(
            PendingCombatResult pending,
            MultiplayerCampaignIdentity identity
        ) =>
            pending.AttackerOwnerInstanceID == identity.HostFactionID
                && pending.DefenderOwnerInstanceID == identity.GuestFactionID
            || pending.AttackerOwnerInstanceID == identity.GuestFactionID
                && pending.DefenderOwnerInstanceID == identity.HostFactionID;
    }
}
