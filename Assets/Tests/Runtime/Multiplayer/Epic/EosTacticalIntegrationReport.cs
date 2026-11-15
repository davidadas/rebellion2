using System;

namespace Rebellion.Tests.MultiplayerIntegration
{
    /// <summary>
    /// Records observed real-service milestones without storing authentication material.
    /// </summary>
    [Serializable]
    internal sealed class EosTacticalIntegrationReport
    {
        public string runID;
        public string role;
        public string phase;
        public string simulationBuild;
        public string hostUserID;
        public string guestUserID;
        public string lobbyID;
        public string localUserID;
        public string battleID;
        public string campaignFingerprint;
        public string errorType;
        public string errorStack;
        public string cleanupErrorType;
        public string assertion;
        public string updatedUTC;
        public string temporaryCachePath;
        public string requestedRelayPolicy;
        public string actualRelayPolicy;
        public double elapsedSeconds;
        public double progressUpdatedSeconds;
        public double attackerHullTotal;
        public double defenderHullTotal;
        public bool authenticated;
        public bool forceRelay;
        public bool relayPolicyVerified;
        public bool hostReadyForJoin;
        public bool synchronized;
        public bool replica;
        public bool humanControllersVerified;
        public bool authoritativeStateObserved;
        public bool manualControlVerified;
        public bool localOrdersObserved;
        public bool remoteOrdersObserved;
        public bool hullDamageObserved;
        public bool completionAgreed;
        public bool dayBarrierVerified;
        public bool outcomeReady;
        public bool passed;
        public bool cleanupComplete;
        public bool guestWithdrawalSubmitted;
        public bool guestWithdrawalObserved;
        public int units;
        public int fighters;
        public int fighterCraft;
        public int capitalsPerSide;
        public int squadronsPerSide;
        public int commandsSubmitted;
        public int formationCommandsSubmitted;
        public int stateUpdatesObserved;
        public int shots;
        public int capitalShots;
        public int fighterShots;
        public int impacts;
        public int destructions;
        public int results;
        public int coordinatorResults;
        public int completedTicks;
        public int combatResumptions;
        public int systemTime;
        public int finalTick;
        public int guestWithdrawalAtSystemTime;
        public int guestWithdrawalSubmittedAtSystemTime;
        public int guestWithdrawalObservedAtSystemTime;
        public int withdrawalCommandsSubmitted;
        public int attackerAliveCapitals;
        public int defenderAliveCapitals;
        public int attackerAliveFighterSquadrons;
        public int defenderAliveFighterSquadrons;
        public int attackerAliveFighterCraft;
        public int defenderAliveFighterCraft;
        public EosTacticalRouteObservation[] routes = Array.Empty<EosTacticalRouteObservation>();
    }
}
