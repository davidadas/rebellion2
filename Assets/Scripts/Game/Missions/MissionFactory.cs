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
        /// Creates a mission when all supplied inputs are valid.
        /// </summary>
        /// <param name="missionTypeId">The mission type ID to create.</param>
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
            string missionTypeId,
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
            mission = null;
            mainParticipants ??= new List<IMissionParticipant>();
            decoyParticipants ??= new List<IMissionParticipant>();

            if (mainParticipants.Count == 0)
                return false;

            Faction faction = _game.Factions.Find(f => f.InstanceID == ownerInstanceId);
            if (faction == null)
                return false;

            if (faction.DisallowedMissionTypeIDs.Contains(missionTypeId))
                return false;

            foreach (
                IMissionParticipant missionParticipant in mainParticipants.Concat(decoyParticipants)
            )
            {
                if (missionParticipant?.CanPerformMission(missionTypeId) != true)
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

            mission = TryCreateByType(missionTypeId, ctx);
            return mission != null;
        }

        /// <summary>
        /// Dispatches to the appropriate mission subclass's TryCreate based on mission type.
        /// </summary>
        /// <param name="missionTypeId">The mission type ID to create.</param>
        /// <param name="ctx">Context containing participants, target, and owner info.</param>
        /// <returns>The created mission, or null if creation fails.</returns>
        private static Mission TryCreateByType(string missionTypeId, MissionContext ctx)
        {
            return missionTypeId switch
            {
                ReconnaissanceMission.MissionTypeID => ReconnaissanceMission.TryCreate(ctx),
                DiplomacyMission.MissionTypeID => DiplomacyMission.TryCreate(ctx),
                RecruitmentMission.MissionTypeID => RecruitmentMission.TryCreate(ctx),
                SubdueUprisingMission.MissionTypeID => SubdueUprisingMission.TryCreate(ctx),
                AbductionMission.MissionTypeID => AbductionMission.TryCreate(ctx),
                AssassinationMission.MissionTypeID => AssassinationMission.TryCreate(ctx),
                EspionageMission.MissionTypeID => EspionageMission.TryCreate(ctx),
                SabotageMission.MissionTypeID => SabotageMission.TryCreate(ctx),
                InciteUprisingMission.MissionTypeID => InciteUprisingMission.TryCreate(ctx),
                RescueMission.MissionTypeID => RescueMission.TryCreate(ctx),
                ResearchMission.MissionTypeID => ctx.Discipline.HasValue
                    ? ResearchMission.TryCreate(ctx, ctx.Discipline.Value)
                    : null,
                JediTrainingMission.MissionTypeID => JediTrainingMission.TryCreate(ctx),
                _ => null,
            };
        }
    }
}
