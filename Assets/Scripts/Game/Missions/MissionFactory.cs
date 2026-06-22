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

        /// <summary>
        /// Initializes a mission factory for the supplied game state.
        /// </summary>
        /// <param name="game">The game state used for validation and mission configuration.</param>
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
        /// <param name="provider">RNG provider for target selection; pass the game's live provider because missions like Recruitment always need it.</param>
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

        /// <summary>
        /// Returns whether a mission can be created for the supplied participant lists and target.
        /// </summary>
        /// <param name="missionType">The type of mission to check.</param>
        /// <param name="ownerInstanceId">The faction attempting the mission.</param>
        /// <param name="mainParticipants">Primary participants assigned to the mission.</param>
        /// <param name="decoyParticipants">Decoy participants assigned to the mission.</param>
        /// <param name="target">The mission target.</param>
        /// <param name="provider">RNG provider for missions that choose a target during creation.</param>
        /// <param name="targetOfficer">Optional officer target for officer-targeted missions.</param>
        /// <param name="discipline">Optional research discipline for research missions.</param>
        /// <param name="specificTarget">Optional concrete target nested under the mission target.</param>
        /// <returns>True when the mission can be created; otherwise false.</returns>
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
            return TryBuildMission(
                missionType,
                ownerInstanceId,
                mainParticipants,
                decoyParticipants,
                target,
                provider,
                targetOfficer,
                discipline,
                specificTarget,
                requireMainParticipants: false,
                configureMission: false,
                out _
            );
        }

        /// <summary>
        /// Creates a mission when all supplied inputs are valid.
        /// </summary>
        /// <param name="missionType">The type of mission to create.</param>
        /// <param name="ownerInstanceId">The faction attempting the mission.</param>
        /// <param name="mainParticipants">Primary participants assigned to the mission.</param>
        /// <param name="decoyParticipants">Decoy participants assigned to the mission.</param>
        /// <param name="target">The mission target.</param>
        /// <param name="mission">The created mission when validation succeeds.</param>
        /// <param name="provider">RNG provider for missions that choose a target during creation.</param>
        /// <param name="targetOfficer">Optional officer target for officer-targeted missions.</param>
        /// <param name="discipline">Optional research discipline for research missions.</param>
        /// <param name="specificTarget">Optional concrete target nested under the mission target.</param>
        /// <returns>True when a mission was created; otherwise false.</returns>
        public bool TryCreateMission(
            MissionType missionType,
            string ownerInstanceId,
            List<IMissionParticipant> mainParticipants,
            List<IMissionParticipant> decoyParticipants,
            ISceneNode target,
            out Mission mission,
            IRandomNumberProvider provider = null,
            Officer targetOfficer = null,
            ResearchDiscipline? discipline = null,
            ISceneNode specificTarget = null
        )
        {
            return TryBuildMission(
                missionType,
                ownerInstanceId,
                mainParticipants,
                decoyParticipants,
                target,
                provider,
                targetOfficer,
                discipline,
                specificTarget,
                requireMainParticipants: true,
                configureMission: true,
                out mission
            );
        }

        /// <summary>
        /// Creates a mission of the specified type, or throws if creation fails.
        /// Performs shared validation, then delegates to the mission-specific TryCreate.
        /// </summary>
        /// <param name="missionType">The type of mission to create.</param>
        /// <param name="ownerInstanceId">The faction attempting the mission.</param>
        /// <param name="mainParticipants">Primary participants assigned to the mission.</param>
        /// <param name="decoyParticipants">Decoy participants assigned to the mission.</param>
        /// <param name="target">The mission target.</param>
        /// <param name="provider">RNG provider for missions that choose a target during creation.</param>
        /// <param name="targetOfficer">Optional officer target for officer-targeted missions.</param>
        /// <param name="discipline">Optional research discipline for research missions.</param>
        /// <param name="specificTarget">Optional concrete target nested under the mission target.</param>
        /// <returns>The created mission.</returns>
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
            if (
                !TryCreateMission(
                    missionType,
                    ownerInstanceId,
                    mainParticipants,
                    decoyParticipants,
                    target,
                    out Mission mission,
                    provider,
                    targetOfficer,
                    discipline,
                    specificTarget
                )
            )
                throw new InvalidOperationException(
                    $"Cannot create {missionType} mission with the given parameters."
                );

            return mission;
        }

        /// <summary>
        /// Applies configured probability and tick tables to a mission.
        /// </summary>
        /// <param name="mission">The mission to configure.</param>
        /// <param name="missionTables">Configured mission probability and tick tables.</param>
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

        /// <summary>
        /// Returns the mission's existing success table or a default table.
        /// </summary>
        /// <param name="mission">The mission whose success table should be read.</param>
        /// <returns>A success probability table.</returns>
        private static ProbabilityTable GetExistingOrDefaultSuccessTable(Mission mission)
        {
            return mission.SuccessProbabilityTable
                ?? new ProbabilityTable(new Dictionary<int, int> { { 0, 50 } });
        }

        /// <summary>
        /// Runs the shared mission validation and optional configuration path.
        /// </summary>
        /// <param name="missionType">The type of mission to create.</param>
        /// <param name="ownerInstanceId">The faction attempting the mission.</param>
        /// <param name="mainParticipants">Primary participants assigned to the mission.</param>
        /// <param name="decoyParticipants">Decoy participants assigned to the mission.</param>
        /// <param name="target">The mission target.</param>
        /// <param name="provider">RNG provider for missions that choose a target during creation.</param>
        /// <param name="targetOfficer">Optional officer target for officer-targeted missions.</param>
        /// <param name="discipline">Optional research discipline for research missions.</param>
        /// <param name="specificTarget">Optional concrete target nested under the mission target.</param>
        /// <param name="requireMainParticipants">Whether at least one primary participant is required.</param>
        /// <param name="configureMission">Whether to apply configured mission probability and tick tables.</param>
        /// <param name="mission">The created mission when validation succeeds.</param>
        /// <returns>True when a mission was created; otherwise false.</returns>
        private bool TryBuildMission(
            MissionType missionType,
            string ownerInstanceId,
            List<IMissionParticipant> mainParticipants,
            List<IMissionParticipant> decoyParticipants,
            ISceneNode target,
            IRandomNumberProvider provider,
            Officer targetOfficer,
            ResearchDiscipline? discipline,
            ISceneNode specificTarget,
            bool requireMainParticipants,
            bool configureMission,
            out Mission mission
        )
        {
            mission = null;
            mainParticipants ??= new List<IMissionParticipant>();
            decoyParticipants ??= new List<IMissionParticipant>();

            if (requireMainParticipants && mainParticipants.Count == 0)
                return false;

            Faction faction = _game.Factions.Find(f => f.InstanceID == ownerInstanceId);
            if (faction?.DisallowedMissionTypes.Contains(missionType) == true)
                return false;

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

            mission = TryCreateByType(missionType, ctx);
            if (mission == null)
                return false;

            if (configureMission)
                ConfigureMission(mission, _game.Config?.ProbabilityTables?.Mission);

            return true;
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
