using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Rebellion.AI.Director;
using Rebellion.AI.Phases;
using Rebellion.AI.Planners;
using Rebellion.AI.Planners.Demand;
using Rebellion.AI.Proposals;
using Rebellion.Game;
using Rebellion.Game.Combat;
using Rebellion.Game.Factions;
using Rebellion.Game.FogOfWar;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Missions;
using Rebellion.Game.Research;
using Rebellion.Game.Results;
using Rebellion.Game.Units;
using Rebellion.Generation;
using Rebellion.SceneGraph;
using Rebellion.Systems;
using Rebellion.Util.Common;

public static partial class HeadlessSimulationRunner
{
    [Serializable]
    private sealed class PersonnelOutcomeSimulationSummary
    {
        public int Captures;
        public int MissionFailureCaptures;
        public int AbductionCaptures;
        public int PlanetLossCaptures;
        public int OtherCaptures;
        public int Releases;
        public int Killed;
        public int CurrentlyCaptured;
        public OfficerCaptureSimulationSummary[] CaptureRecords;
    }

    [Serializable]
    private sealed class OfficerCaptureSimulationSummary
    {
        public int Tick;
        public string OfficerId;
        public string OfficerName;
        public string Cause;
        public string MissionTypeId;
        public string MissionRole;
        public bool HadSpecialForcesDecoy;
    }

    [Serializable]
    private sealed class AttackReadinessSimulationSummary
    {
        public int BuildingFleetSamples;
        public AttackReadinessBlockerSummary[] Blockers;
    }

    [Serializable]
    private sealed class AttackReadinessBlockerSummary
    {
        public string Blocker;
        public int Samples;
        public int SoleBlockerSamples;
    }

    [Serializable]
    private sealed class PlanetaryAssaultSimulationSummary
    {
        public int Attempted;
        public int Succeeded;
        public int Failed;
        public int ImmediateUprisings;
        public PlanetaryAssaultSimulationResult[] Results;
    }

    [Serializable]
    private sealed class PlanetaryAssaultSimulationResult
    {
        public int Tick;
        public string PlanetId;
        public string PlanetName;
        public bool Success;
        public int InitialAttackerRegimentCount;
        public int RemainingAttackerRegimentCount;
        public int InitialDefenderRegimentCount;
        public int RemainingDefenderRegimentCount;
        public bool ImmediateUprising;
        public int RequiredGarrisonCount;
        public int GarrisonDeficit;
    }

    [Serializable]
    private sealed class GarrisonRemovalBombardmentSimulationSummary
    {
        public int Triggered;
        public int SupportShiftPerAffectedPlanet;
        public int AdditionalPlanetsFlipped;
        public GarrisonRemovalBombardmentSimulationResult[] Results;
    }

    [Serializable]
    private sealed class GarrisonRemovalBombardmentSimulationResult
    {
        public int Tick;
        public string AttackerFactionId;
        public string PlanetId;
        public string PlanetName;
        public string PreviousOwnerFactionId;
        public string NewOwnerFactionId;
        public string[] AdditionalFlippedPlanets;
    }

    [Serializable]
    private sealed class ConstructionFacilityExpansionSimulationSummary
    {
        public int ProducerConstructionCapacityLimit;
        public int PrimaryCandidateCount;
        public int FinalCandidateCount;
        public int ActiveConstructionFacilityCount;
        public int ProjectedConstructionFacilityCount;
        public int ConstructionFacilityPlanetCount;
        public int LargestPlanetConstructionFacilityCount;
        public int LargestSectorConstructionFacilityCount;
        public double LargestPlanetConstructionFacilityShare;
        public double LargestSectorConstructionFacilityShare;
    }

    [Serializable]
    private sealed class TroopProductionSimulationSummary
    {
        public int CandidateTargetCount;
        public int FinalCandidateTargetCount;
        public int CandidateRegimentCount;
        public int OwnedTrainingPlanetCount;
    }

    [Serializable]
    private sealed class TroopReinforcementPackageSimulationSummary
    {
        public int SecondaryCandidateCount;
        public int SelectedCandidateTrainingFacilityCount;
        public int SelectedCandidateRegimentCount;
    }

    [Serializable]
    private sealed class CapitalShipProductionSimulationSummary
    {
        public int OwnedShipyardPlanetCount;
        public int AvailableShipyardPlanetCount;
        public int OwnedPlanetIdleStarfighterCount;
        public int OwnedFleetFreeStarfighterCapacity;
        public int CapitalTechnologyCount;
        public int InfrastructureCapitalTechnologyCount;
        public bool ProducerFound;
        public int ProducerShipCapacity;
        public int ProducerShipQueueCount;
        public int ProducerActiveCapitalShipCount;
    }

    [Serializable]
    private sealed class StarfighterCoverageSimulationSummary
    {
        public int OwnedUsablePlanetCount;
        public int CoveredPlanetCount;
        public int UncoveredPlanetCount;
    }

    [Serializable]
    private sealed class EconomySimulationSummary
    {
        public int RawResourceNodes;
        public int ActiveMines;
        public int QueuedMines;
        public int ProjectedMines;
        public int ActiveRefineries;
        public int QueuedRefineries;
        public int ProjectedRefineries;
        public int ProjectedMinedResources;
        public int ProjectedRefineryCapacity;
        public int EffectiveRefinedOutput;
        public int MineDeficit;
        public int RefineryDeficit;
        public int UnusedMinedResources;
        public int UnusedRefineryCapacity;
    }

    [Serializable]
    private sealed class FactionActivitySummary
    {
        public MissionActivitySummary[] Missions;
        public MissionTargetActivitySummary[] MissionTargets;
        public SabotageTargetActivitySummary[] SabotageTargets;
        public int PlanetsAcquired;
        public int PlanetsLost;
        public int PlanetsColonized;
        public int ShipResearchAdvances;
        public int FacilityResearchAdvances;
        public int TroopResearchAdvances;
        public int FinalShipResearchOrder;
        public int FinalFacilityResearchOrder;
        public int FinalTroopResearchOrder;
    }

    [Serializable]
    private sealed class MissionActivitySummary
    {
        public string MissionTypeId;
        public int Started;
        public int OfficerLedHostileStarted;
        public int OfficerLedHostileStartedWithDecoy;
        public int OfficerLedHostileStartedWithSpecialForcesDecoy;
        public int Ended;
        public int Active;
    }

    [Serializable]
    private sealed class MissionTargetActivitySummary
    {
        public string MissionTypeId;
        public string PlanetId;
        public int Started;
        public int Ended;
        public int Active;
        public int IntelRefreshes;
        public int EarlyInterruptions;
        public int ArrivalInterruptions;
        public int MinimumMainRating;
        public int MaximumMainRating;
        public double AverageMainRating;
    }

    [Serializable]
    private sealed class SabotageTargetActivitySummary
    {
        public string TargetId;
        public string PlanetId;
        public string TargetType;
        public int Started;
        public int Ended;
        public int Destroyed;
    }

    private sealed class PersonnelOutcomeTracker
    {
        private readonly Dictionary<string, Officer> _knownOfficers = new(StringComparer.Ordinal);
        private readonly Dictionary<string, TrackedOfficer> _officers = new(StringComparer.Ordinal);
        private readonly Dictionary<string, PersonnelOutcomeCounts> _outcomes = new(
            StringComparer.Ordinal
        );

        /// <summary>
        /// Records the personnel state before simulation ticks are processed.
        /// </summary>
        /// <param name="game">The initial game state.</param>
        /// <param name="abductionTargetIds">Officers targeted by active abduction missions.</param>
        public void RecordInitialState(GameRoot game, ISet<string> abductionTargetIds)
        {
            _knownOfficers.Clear();
            _officers.Clear();
            foreach (Officer officer in game.GetSceneNodesByType<Officer>())
            {
                _knownOfficers[officer.InstanceID] = officer;
                _officers[officer.InstanceID] = TrackedOfficer.From(officer, abductionTargetIds);
            }
        }

        /// <summary>
        /// Records capture transitions, releases, and current officer state.
        /// </summary>
        /// <param name="game">The current game state.</param>
        /// <param name="abductionTargetIds">Officers targeted by active abduction missions.</param>
        public void RecordTick(GameRoot game, ISet<string> abductionTargetIds)
        {
            Dictionary<string, Officer> currentOfficers = GetFactionOwnedNodes<Officer>(game)
                .ToDictionary(officer => officer.InstanceID, StringComparer.Ordinal);
            foreach (Officer officer in currentOfficers.Values)
                _knownOfficers[officer.InstanceID] = officer;

            foreach (TrackedOfficer previous in _officers.Values)
            {
                PersonnelOutcomeCounts counts = GetCounts(previous.OwnerInstanceID);
                if (!currentOfficers.TryGetValue(previous.InstanceID, out Officer current))
                    continue;

                if (!previous.IsCaptured && current.IsCaptured)
                {
                    counts.Captures++;
                    string cause;
                    if (previous.WasOnMission)
                    {
                        counts.MissionFailureCaptures++;
                        cause = "MissionFailure";
                    }
                    else if (previous.WasAbductionTarget)
                    {
                        counts.AbductionCaptures++;
                        cause = "Abduction";
                    }
                    else if (WasCapturedByPlanetLoss(previous, current))
                    {
                        counts.PlanetLossCaptures++;
                        cause = "PlanetLoss";
                    }
                    else
                    {
                        counts.OtherCaptures++;
                        cause = "Other";
                    }
                    counts.CaptureRecords.Add(
                        previous.BuildCaptureSummary(game.CurrentTick, cause)
                    );
                }
                else if (previous.IsCaptured && !current.IsCaptured)
                    counts.Releases++;
            }

            _officers.Clear();
            foreach (Officer officer in currentOfficers.Values)
                _officers[officer.InstanceID] = TrackedOfficer.From(officer, abductionTargetIds);
        }

        /// <summary>
        /// Builds the personnel outcome summary for one faction.
        /// </summary>
        /// <param name="factionId">The faction instance identifier.</param>
        /// <returns>The recorded personnel outcomes.</returns>
        public PersonnelOutcomeSimulationSummary BuildSummary(string factionId)
        {
            PersonnelOutcomeCounts counts = GetCounts(factionId);
            return new PersonnelOutcomeSimulationSummary
            {
                Captures = counts.Captures,
                MissionFailureCaptures = counts.MissionFailureCaptures,
                AbductionCaptures = counts.AbductionCaptures,
                PlanetLossCaptures = counts.PlanetLossCaptures,
                OtherCaptures = counts.OtherCaptures,
                Releases = counts.Releases,
                Killed = _knownOfficers.Values.Count(officer =>
                    officer.OwnerInstanceID == factionId && officer.IsKilled
                ),
                CurrentlyCaptured = _officers.Values.Count(officer =>
                    officer.OwnerInstanceID == factionId && officer.IsCaptured
                ),
                CaptureRecords = counts.CaptureRecords.ToArray(),
            };
        }

        /// <summary>
        /// Gets the mutable outcome counters for one faction.
        /// </summary>
        /// <param name="factionId">The faction instance identifier.</param>
        /// <returns>The faction's outcome counters.</returns>
        private PersonnelOutcomeCounts GetCounts(string factionId)
        {
            factionId ??= string.Empty;
            if (!_outcomes.TryGetValue(factionId, out PersonnelOutcomeCounts counts))
            {
                counts = new PersonnelOutcomeCounts();
                _outcomes[factionId] = counts;
            }

            return counts;
        }

        /// <summary>
        /// Returns whether an officer became captive as their friendly planet changed owners.
        /// </summary>
        /// <param name="previous">The officer state before the tick.</param>
        /// <param name="current">The officer after the tick.</param>
        /// <returns>True when planetary control changed away from the officer's faction.</returns>
        private static bool WasCapturedByPlanetLoss(TrackedOfficer previous, Officer current)
        {
            string currentPlanetOwnerId = current.GetParentOfType<Planet>()?.GetOwnerInstanceID();
            return previous.PlanetOwnerInstanceId == previous.OwnerInstanceID
                && !string.IsNullOrEmpty(currentPlanetOwnerId)
                && currentPlanetOwnerId != previous.OwnerInstanceID;
        }

        private sealed class PersonnelOutcomeCounts
        {
            public int Captures;
            public int MissionFailureCaptures;
            public int AbductionCaptures;
            public int PlanetLossCaptures;
            public int OtherCaptures;
            public int Releases;
            public readonly List<OfficerCaptureSimulationSummary> CaptureRecords = new();
        }

        private sealed class TrackedOfficer
        {
            public string InstanceID;
            public string DisplayName;
            public string OwnerInstanceID;
            public bool IsCaptured;
            public bool IsKilled;
            public bool WasOnMission;
            public bool WasAbductionTarget;
            public string PlanetOwnerInstanceId;
            public string MissionTypeId;
            public string MissionRole;
            public bool HadSpecialForcesDecoy;

            /// <summary>
            /// Captures the state needed to compare an officer across simulation ticks.
            /// </summary>
            /// <param name="officer">The officer to record.</param>
            /// <param name="abductionTargetIds">Officers targeted by active abductions.</param>
            /// <returns>The tracked officer state.</returns>
            public static TrackedOfficer From(Officer officer, ISet<string> abductionTargetIds)
            {
                Mission mission = officer.GetParentOfType<Mission>();
                return new TrackedOfficer
                {
                    InstanceID = officer.InstanceID,
                    DisplayName = officer.GetDisplayName(),
                    OwnerInstanceID = officer.OwnerInstanceID,
                    IsCaptured = officer.IsCaptured,
                    IsKilled = officer.IsKilled,
                    WasOnMission = officer.IsOnMission(),
                    WasAbductionTarget = abductionTargetIds.Contains(officer.InstanceID),
                    PlanetOwnerInstanceId = officer.GetParentOfType<Planet>()?.GetOwnerInstanceID(),
                    MissionTypeId = mission?.GetTypeID(),
                    MissionRole =
                        mission == null ? null
                        : mission.GetDecoyParticipants().Contains(officer) ? "Decoy"
                        : "Main",
                    HadSpecialForcesDecoy =
                        mission?.GetDecoyParticipants().OfType<SpecialForces>().Any() == true,
                };
            }

            /// <summary>
            /// Builds the diagnostic record for a newly captured officer.
            /// </summary>
            /// <param name="tick">The simulation tick when capture occurred.</param>
            /// <param name="cause">The attributed capture cause.</param>
            /// <returns>The capture diagnostic record.</returns>
            public OfficerCaptureSimulationSummary BuildCaptureSummary(int tick, string cause)
            {
                return new OfficerCaptureSimulationSummary
                {
                    Tick = tick,
                    OfficerId = InstanceID,
                    OfficerName = DisplayName,
                    Cause = cause,
                    MissionTypeId = MissionTypeId,
                    MissionRole = MissionRole,
                    HadSpecialForcesDecoy = HadSpecialForcesDecoy,
                };
            }
        }
    }
}
