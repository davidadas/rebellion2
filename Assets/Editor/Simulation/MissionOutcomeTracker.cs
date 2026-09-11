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
    public sealed class SimulationRunResult
    {
        public int TicksCompleted;
        public string OutputPath;
        public int Seed = -1;
        public string SavePath;
    }

    [Serializable]
    private sealed class SimulationSummary
    {
        public int TicksRequested;
        public int TicksCompleted;
        public int Seed = -1;
        public string GalaxySize;
        public string OutputPath;
        public int AITickInterval;
        public int MinimumAttackStrength;
        public int MinimumAttackRegimentCount;
        public VictorySimulationSummary Victory;
        public FleetHistorySnapshot[] FleetHistory;
        public FactionSimulationSummary[] Factions;
    }

    [Serializable]
    private sealed class VictorySimulationSummary
    {
        public string WinnerFactionId;
        public string Winner;
        public string LoserFactionId;
        public string Loser;
        public int Tick;
        public string Mode;
    }

    [Serializable]
    private sealed class FactionSimulationSummary
    {
        public string[] OwnedPlanets;
        public string FactionId;
        public string DisplayName;
        public int PlanetCount;
        public int OperationalPlanetCount;
        public int FleetCount;
        public int BuildingCount;
        public int DefenseFacilityCount;
        public int WeaponFacilityCount;
        public int ProjectedDefenseFacilityCount;
        public int ProjectedWeaponFacilityCount;
        public int ShieldedPlanetCount;
        public int FullyShieldedPlanetCount;
        public int WeaponDefendedPlanetCount;
        public int FullyStaticDefendedPlanetCount;
        public int ProjectedFullyShieldedPlanetCount;
        public int ProjectedWeaponDefendedPlanetCount;
        public int ProjectedFullyStaticDefendedPlanetCount;
        public int AdvancedConstructionFacilityCount;
        public int ConstructionFacilityCount;
        public int ProjectedConstructionFacilityCount;
        public int AdvancedShipyardCount;
        public int ShipyardCount;
        public int ProjectedShipyardCount;
        public int AdvancedTrainingFacilityCount;
        public int TrainingFacilityCount;
        public int ProjectedTrainingFacilityCount;
        public int CapitalShipCount;
        public int StarfighterCount;
        public int RegimentCount;
        public int SpecialForcesCount;
        public int OfficerCount;
        public int UnlockedSpecialForcesTechCount;
        public int RawMaterialSupply;
        public int RefinedMaterialSupply;
        public int RawMaterialStockpile;
        public int RefinedMaterialStockpile;
        public int MaintenanceCapacity;
        public int MaintenanceHeadroom;
        public EconomySimulationSummary Economy;
        public int Energy;
        public int UnitCost;
        public StarfighterCoverageSimulationSummary StarfighterCoverage;
        public int TotalManufacturedCapitalShips;
        public int TotalManufacturedStarfighters;
        public int TotalManufacturedRegiments;
        public int TotalManufacturedSpecialForces;
        public ManufacturedUnitTypeSummary[] ManufacturedUnitTypes;
        public SpecialForcesLifecycleSimulationSummary[] SpecialForcesLifecycle;
        public int TotalManufacturedBuildings;
        public int TotalManufacturedMines;
        public int TotalManufacturedRefineries;
        public int TotalManufacturedConstructionFacilities;
        public int TotalManufacturedShipyards;
        public int TotalManufacturedTrainingFacilities;
        public int TotalManufacturedDefenseFacilities;
        public int TotalManufacturedWeapons;
        public int ProductionDemandCount;
        public int ProductionProposalCount;
        public int SelectedProductionProposalCount;
        public int PlanetaryDefenseDemandCount;
        public int PlanetaryDefenseDemandQuantity;
        public int PlanetaryDefenseProposalCount;
        public int SelectedPlanetaryDefenseProposalCount;
        public int GarrisonDemandCount;
        public int GarrisonProposalCount;
        public int SelectedGarrisonProposalCount;
        public int BuildingProductionProposalCount;
        public int SelectedBuildingProductionProposalCount;
        public int SelectedProductionMaintenanceCost;
        public ConstructionFacilityExpansionSimulationSummary ConstructionFacilityExpansion;
        public TroopProductionSimulationSummary TroopProduction;
        public TroopReinforcementPackageSimulationSummary TroopReinforcementPackages;
        public CapitalShipProductionSimulationSummary CapitalShipProduction;
        public ManufacturingIdleSummary ManufacturingIdle;
        public FactionActivitySummary Activity;
        public MissionOutcomeSimulationSummary MissionOutcomes;
        public PersonnelOutcomeSimulationSummary PersonnelOutcomes;
        public PlanetaryAssaultSimulationSummary PlanetaryAssaults;
        public GarrisonRemovalBombardmentSimulationSummary GarrisonRemovalBombardments;
        public AttackReadinessSimulationSummary AttackReadiness;
        public ProductionFacilityPlanetSummary[] ProductionFacilityPlanets;
        public CurrentIdlePlanetSummary[] CurrentIdlePlanets;
        public FleetSimulationSummary[] Fleets;
    }

    [Serializable]
    private sealed class MissionOutcomeSimulationSummary
    {
        public int Succeeded;
        public int Failed;
        public int Foiled;
        public int Injuries;
        public int Captures;
        public int FoiledMissionInjuries;
        public int FoiledMissionCaptures;
        public MissionTypeOutcomeSimulationSummary[] ByMissionType;
    }

    [Serializable]
    private sealed class MissionTypeOutcomeSimulationSummary
    {
        public string MissionTypeId;
        public int Succeeded;
        public int Failed;
        public int Foiled;
        public int Injuries;
        public int Captures;
        public int FoiledMissionInjuries;
        public int FoiledMissionCaptures;
    }

    private sealed class MissionOutcomeTracker
    {
        private readonly Dictionary<string, Dictionary<string, MissionOutcomeCounts>> _counts = new(
            StringComparer.Ordinal
        );

        /// <summary>
        /// Records authoritative mission and participant outcomes from one resolved result batch.
        /// </summary>
        /// <param name="results">The resolved results to record.</param>
        public void Record(IReadOnlyList<GameResult> results)
        {
            if (results == null || results.Count == 0)
                return;

            Dictionary<string, MissionCompletedResult> missionsById = results
                .OfType<MissionCompletedResult>()
                .Where(result => !string.IsNullOrEmpty(result.MissionInstanceID))
                .GroupBy(result => result.MissionInstanceID, StringComparer.Ordinal)
                .ToDictionary(group => group.Key, group => group.Last(), StringComparer.Ordinal);

            foreach (MissionCompletedResult result in missionsById.Values)
            {
                string factionId = result.Mission?.GetOwnerInstanceID();
                MissionOutcomeCounts counts = GetCounts(factionId, result.MissionTypeID);
                switch (result.Outcome)
                {
                    case MissionOutcome.Success:
                        counts.Succeeded++;
                        break;
                    case MissionOutcome.Failed:
                        counts.Failed++;
                        break;
                    case MissionOutcome.Foiled:
                        counts.Foiled++;
                        break;
                }
            }

            foreach (OfficerInjuredResult result in results.OfType<OfficerInjuredResult>())
                RecordParticipantOutcome(result, result.Officer, missionsById, isCapture: false);

            foreach (
                OfficerCaptureStateResult result in results
                    .OfType<OfficerCaptureStateResult>()
                    .Where(result => result.IsCaptured)
            )
                RecordParticipantOutcome(
                    result,
                    result.TargetOfficer ?? result.CapturedOfficer,
                    missionsById,
                    isCapture: true
                );
        }

        /// <summary>
        /// Builds the mission outcome summary for one faction.
        /// </summary>
        /// <param name="factionId">The faction instance identifier.</param>
        /// <returns>The accumulated outcome counters.</returns>
        public MissionOutcomeSimulationSummary BuildSummary(string factionId)
        {
            Dictionary<string, MissionOutcomeCounts> factionCounts = GetFactionCounts(factionId);
            MissionOutcomeCounts total = new MissionOutcomeCounts();
            foreach (MissionOutcomeCounts counts in factionCounts.Values)
                total.Add(counts);

            return new MissionOutcomeSimulationSummary
            {
                Succeeded = total.Succeeded,
                Failed = total.Failed,
                Foiled = total.Foiled,
                Injuries = total.Injuries,
                Captures = total.Captures,
                FoiledMissionInjuries = total.FoiledMissionInjuries,
                FoiledMissionCaptures = total.FoiledMissionCaptures,
                ByMissionType = factionCounts
                    .OrderBy(pair => pair.Key, StringComparer.Ordinal)
                    .Select(pair => pair.Value.BuildSummary(pair.Key))
                    .ToArray(),
            };
        }

        /// <summary>
        /// Records one injury or capture and attributes it to its completing mission when present.
        /// </summary>
        /// <param name="result">The participant result.</param>
        /// <param name="officer">The affected officer.</param>
        /// <param name="missionsById">Completed missions in the same result batch.</param>
        /// <param name="isCapture">Whether the outcome is a capture rather than an injury.</param>
        private void RecordParticipantOutcome(
            GameResult result,
            Officer officer,
            IReadOnlyDictionary<string, MissionCompletedResult> missionsById,
            bool isCapture
        )
        {
            if (officer == null || string.IsNullOrEmpty(result.MissionInstanceID))
                return;

            missionsById.TryGetValue(
                result.MissionInstanceID,
                out MissionCompletedResult completedMission
            );
            string missionTypeId = completedMission?.MissionTypeID ?? string.Empty;
            MissionOutcomeCounts counts = GetCounts(officer.OwnerInstanceID, missionTypeId);
            bool wasFoiled = completedMission?.Outcome == MissionOutcome.Foiled;
            if (isCapture)
            {
                counts.Captures++;
                if (wasFoiled)
                    counts.FoiledMissionCaptures++;
            }
            else
            {
                counts.Injuries++;
                if (wasFoiled)
                    counts.FoiledMissionInjuries++;
            }
        }

        /// <summary>
        /// Gets the mission-type counters for one faction.
        /// </summary>
        /// <param name="factionId">The faction instance identifier.</param>
        /// <param name="missionTypeId">The mission type identifier.</param>
        /// <returns>The mutable outcome counters.</returns>
        private MissionOutcomeCounts GetCounts(string factionId, string missionTypeId)
        {
            Dictionary<string, MissionOutcomeCounts> factionCounts = GetFactionCounts(factionId);
            string key = missionTypeId ?? string.Empty;
            if (!factionCounts.TryGetValue(key, out MissionOutcomeCounts counts))
            {
                counts = new MissionOutcomeCounts();
                factionCounts[key] = counts;
            }

            return counts;
        }

        /// <summary>
        /// Gets the mission outcome map for one faction.
        /// </summary>
        /// <param name="factionId">The faction instance identifier.</param>
        /// <returns>The mutable mission-type map.</returns>
        private Dictionary<string, MissionOutcomeCounts> GetFactionCounts(string factionId)
        {
            string key = factionId ?? string.Empty;
            if (!_counts.TryGetValue(key, out Dictionary<string, MissionOutcomeCounts> counts))
            {
                counts = new Dictionary<string, MissionOutcomeCounts>(StringComparer.Ordinal);
                _counts[key] = counts;
            }

            return counts;
        }

        private sealed class MissionOutcomeCounts
        {
            public int Succeeded;
            public int Failed;
            public int Foiled;
            public int Injuries;
            public int Captures;
            public int FoiledMissionInjuries;
            public int FoiledMissionCaptures;

            /// <summary>
            /// Adds another set of outcome counters.
            /// </summary>
            /// <param name="other">The counters to add.</param>
            public void Add(MissionOutcomeCounts other)
            {
                Succeeded += other.Succeeded;
                Failed += other.Failed;
                Foiled += other.Foiled;
                Injuries += other.Injuries;
                Captures += other.Captures;
                FoiledMissionInjuries += other.FoiledMissionInjuries;
                FoiledMissionCaptures += other.FoiledMissionCaptures;
            }

            /// <summary>
            /// Builds the serializable summary for one mission type.
            /// </summary>
            /// <param name="missionTypeId">The mission type identifier.</param>
            /// <returns>The mission-type summary.</returns>
            public MissionTypeOutcomeSimulationSummary BuildSummary(string missionTypeId)
            {
                return new MissionTypeOutcomeSimulationSummary
                {
                    MissionTypeId = missionTypeId,
                    Succeeded = Succeeded,
                    Failed = Failed,
                    Foiled = Foiled,
                    Injuries = Injuries,
                    Captures = Captures,
                    FoiledMissionInjuries = FoiledMissionInjuries,
                    FoiledMissionCaptures = FoiledMissionCaptures,
                };
            }
        }
    }
}
