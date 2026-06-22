using System;
using System.Collections.Generic;
using System.Linq;
using Rebellion.Game.Factions;
using Rebellion.Game.Research;
using Rebellion.Game.Units;
using Rebellion.SceneGraph;
using Rebellion.Util.Common;

namespace Rebellion.Game.Missions
{
    /// <summary>
    /// Thin router that delegates mission creation to each Mission subclass's TryCreate method.
    /// Handles shared validation (faction restrictions, participant eligibility) before routing.
    /// </summary>
    public class MissionFactory
    {
        private readonly GameRoot _game;

        public MissionFactory(GameRoot game)
        {
            _game = game;
        }

        /// <summary>
        /// Returns whether a mission of the given type can be created for the target.
        /// Uses the same TryCreate path as CreateMission without requiring participants.
        /// </summary>
        /// <param name="missionType">The type of mission to check.</param>
        /// <param name="ownerInstanceId">The faction attempting the mission.</param>
        /// <param name="target">The target scene node.</param>
        /// <param name="provider">RNG provider for target selection; pass the game's live provider — missions like Recruitment always need it.</param>
        /// <param name="targetOfficer">Optional pre-selected officer target.</param>
        /// <param name="discipline">Research discipline for Research missions.</param>
        /// <param name="participant">Optional acting participant for participant-specific mission gates.</param>
        /// <param name="specificTarget">Optional concrete target nested under the mission target.</param>
        /// <returns>True if the mission can be created with the given parameters.</returns>
        public bool CanCreateMission(
            MissionType missionType,
            string ownerInstanceId,
            ISceneNode target,
            IRandomNumberProvider provider,
            Officer targetOfficer = null,
            ResearchDiscipline? discipline = null,
            IMissionParticipant participant = null,
            ISceneNode specificTarget = null
        )
        {
            List<IMissionParticipant> mainParticipants = new List<IMissionParticipant>();
            if (participant != null)
                mainParticipants.Add(participant);

            return CanCreateMission(
                missionType,
                ownerInstanceId,
                mainParticipants,
                new List<IMissionParticipant>(),
                target,
                provider,
                targetOfficer,
                discipline,
                specificTarget
            );
        }

        public bool CanCreateMission(
            MissionType missionType,
            string ownerInstanceId,
            List<IMissionParticipant> mainParticipants,
            List<IMissionParticipant> decoyParticipants,
            ISceneNode target,
            IRandomNumberProvider provider,
            Officer targetOfficer = null,
            ResearchDiscipline? discipline = null,
            ISceneNode specificTarget = null
        )
        {
            Faction faction = _game.Factions.Find(f => f.InstanceID == ownerInstanceId);
            if (faction?.DisallowedMissionTypes.Contains(missionType) == true)
                return false;

            mainParticipants ??= new List<IMissionParticipant>();
            decoyParticipants ??= new List<IMissionParticipant>();

            foreach (
                IMissionParticipant missionParticipant in mainParticipants.Concat(decoyParticipants)
            )
            {
                if (missionParticipant?.CanPerformMission(missionType) != true)
                    return false;
            }

            MissionContext ctx = new MissionContext
            {
                Game = _game,
                OwnerInstanceId = ownerInstanceId,
                Target = target,
                SpecificTarget = specificTarget,
                MainParticipants = mainParticipants,
                DecoyParticipants = decoyParticipants,
                RandomProvider = provider,
                TargetOfficer = targetOfficer,
                Discipline = discipline,
            };

            return TryCreateByType(missionType, ctx) != null;
        }

        /// <summary>
        /// Creates a mission of the specified type, or throws if creation fails.
        /// Performs shared validation, then delegates to the mission-specific TryCreate.
        /// </summary>
        public Mission CreateMission(
            MissionType missionType,
            string ownerInstanceId,
            List<IMissionParticipant> mainParticipants,
            List<IMissionParticipant> decoyParticipants,
            ISceneNode target,
            IRandomNumberProvider provider = null,
            Officer targetOfficer = null,
            ResearchDiscipline? discipline = null,
            ISceneNode specificTarget = null
        )
        {
            if (mainParticipants == null || mainParticipants.Count == 0)
                throw new ArgumentException(
                    "At least one main participant is required.",
                    nameof(mainParticipants)
                );

            Faction faction = _game.Factions.Find(f => f.InstanceID == ownerInstanceId);
            if (faction?.DisallowedMissionTypes.Contains(missionType) == true)
                throw new InvalidOperationException(
                    $"Faction '{ownerInstanceId}' cannot perform {missionType} missions."
                );

            foreach (IMissionParticipant participant in mainParticipants.Concat(decoyParticipants))
            {
                if (!participant.CanPerformMission(missionType))
                    throw new InvalidOperationException(
                        $"Participant '{participant.GetDisplayName()}' cannot perform {missionType} missions."
                    );
            }

            MissionContext ctx = new MissionContext
            {
                Game = _game,
                OwnerInstanceId = ownerInstanceId,
                Target = target,
                SpecificTarget = specificTarget,
                MainParticipants = mainParticipants,
                DecoyParticipants = decoyParticipants,
                RandomProvider = provider,
                TargetOfficer = targetOfficer,
                Discipline = discipline,
            };

            Mission mission =
                TryCreateByType(missionType, ctx)
                ?? throw new InvalidOperationException(
                    $"Cannot create {missionType} mission with the given parameters."
                );

            ConfigureMission(mission, _game.Config?.ProbabilityTables?.Mission);

            return mission;
        }

        public static void ConfigureMission(
            Mission mission,
            GameConfig.MissionProbabilityTablesConfig missionTables
        )
        {
            if (mission == null)
                throw new ArgumentNullException(nameof(mission));
            if (missionTables == null)
                return;

            Dictionary<int, int> successTable = missionTables.GetSuccessTable(mission.ConfigKey);
            GameConfig.MissionTickConfig tickConfig = missionTables.TickRanges.GetTickConfig(
                mission.ConfigKey
            );

            mission.Configure(
                successTable != null
                    ? new ProbabilityTable(successTable)
                    : GetExistingOrDefaultSuccessTable(mission),
                new ProbabilityTable(missionTables.Decoy),
                new ProbabilityTable(missionTables.Foil),
                new ProbabilityTable(missionTables.KillOrCapture),
                missionTables.DecoyDefenderScalingPercent,
                tickConfig != null ? tickConfig.Base : mission.BaseTicks,
                tickConfig != null ? tickConfig.Spread : mission.SpreadTicks
            );
        }

        private static ProbabilityTable GetExistingOrDefaultSuccessTable(Mission mission)
        {
            return mission.SuccessProbabilityTable
                ?? new ProbabilityTable(new Dictionary<int, int> { { 0, 50 } });
        }

        /// <summary>
        /// Dispatches to the appropriate mission subclass's TryCreate based on mission type.
        /// </summary>
        /// <param name="missionType">The type of mission to create.</param>
        /// <param name="ctx">Context containing participants, target, and owner info.</param>
        /// <returns>The created mission, or null if creation fails.</returns>
        private static Mission TryCreateByType(MissionType missionType, MissionContext ctx)
        {
            return missionType switch
            {
                MissionType.Reconnaissance => ReconnaissanceMission.TryCreate(ctx),
                MissionType.Diplomacy => DiplomacyMission.TryCreate(ctx),
                MissionType.Recruitment => RecruitmentMission.TryCreate(ctx),
                MissionType.SubdueUprising => SubdueUprisingMission.TryCreate(ctx),
                MissionType.Abduction => AbductionMission.TryCreate(ctx),
                MissionType.Assassination => AssassinationMission.TryCreate(ctx),
                MissionType.Espionage => EspionageMission.TryCreate(ctx),
                MissionType.Sabotage => SabotageMission.TryCreate(ctx),
                MissionType.InciteUprising => InciteUprisingMission.TryCreate(ctx),
                MissionType.Rescue => RescueMission.TryCreate(ctx),
                MissionType.Research => ctx.Discipline.HasValue
                    ? ResearchMission.TryCreate(ctx, ctx.Discipline.Value)
                    : null,
                MissionType.JediTraining => JediTrainingMission.TryCreate(ctx),
                _ => null,
            };
        }
    }
}
