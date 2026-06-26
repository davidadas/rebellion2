using System.Collections.Generic;
using System.Linq;
using Rebellion.Game.Factions;

namespace Rebellion.Game.Missions
{
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

        public bool TryCreateMission(MissionStartRequest request, out Mission mission)
        {
            mission = null;
            if (request == null || string.IsNullOrEmpty(request.MissionTypeID))
                return false;

            List<IMissionParticipant> mainParticipants =
                request.MainParticipants ?? new List<IMissionParticipant>();
            List<IMissionParticipant> decoyParticipants =
                request.DecoyParticipants ?? new List<IMissionParticipant>();

            if (mainParticipants.Count == 0)
                return false;

            Faction faction = _game.Factions.Find(f => f.InstanceID == request.OwnerInstanceID);
            if (faction == null)
                return false;

            if (faction.DisallowedMissionTypeIDs.Contains(request.MissionTypeID))
                return false;

            foreach (
                IMissionParticipant missionParticipant in mainParticipants.Concat(decoyParticipants)
            )
            {
                if (missionParticipant?.CanPerformMission(request.MissionTypeID) != true)
                    return false;
            }

            MissionContext ctx = new MissionContext
            {
                Game = _game,
                OwnerInstanceId = request.OwnerInstanceID,
                Target = request.Target,
                SpecificTarget = request.SpecificTarget,
                MainParticipants = mainParticipants,
                DecoyParticipants = decoyParticipants,
                RandomProvider = request.RandomProvider,
                TargetOfficer = request.TargetOfficer,
                Discipline = request.Discipline,
            };

            mission = TryCreateByType(request.MissionTypeID, ctx);
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
