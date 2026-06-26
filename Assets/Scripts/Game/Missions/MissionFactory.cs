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

        /// <summary>
        /// Returns the mission options that can create a mission from the supplied request.
        /// </summary>
        /// <param name="request">The start request containing the target and participants to evaluate.</param>
        /// <returns>The mission options that pass mission creation validation.</returns>
        public List<MissionOption> GetAvailableMissionOptions(MissionStartRequest request)
        {
            List<MissionOption> options = new List<MissionOption>();
            if (request == null)
                return options;

            foreach (MissionOption option in MissionDefinitionCatalog.Options)
            {
                MissionStartRequest optionRequest = CreateOptionRequest(request, option);
                if (TryCreateMission(optionRequest, out _))
                    options.Add(option);
            }

            return options;
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

            MissionDefinition definition = MissionDefinitionCatalog.Get(request.MissionTypeID);
            if (definition == null)
                return false;

            MissionBehavior behavior = definition.Behavior;
            if (behavior == null)
                return false;

            request.Game = _game;
            request.MainParticipants = mainParticipants;
            request.DecoyParticipants = decoyParticipants;
            mission = behavior.TryCreate(request, definition);
            return mission != null;
        }

        /// <summary>
        /// Creates a mission start request for a specific mission option.
        /// </summary>
        /// <param name="request">The source request containing target and participant context.</param>
        /// <param name="option">The mission option being evaluated.</param>
        /// <returns>The request populated with the option's mission type and discipline.</returns>
        private static MissionStartRequest CreateOptionRequest(
            MissionStartRequest request,
            MissionOption option
        )
        {
            return new MissionStartRequest
            {
                Game = request.Game,
                MissionTypeID = option.MissionTypeID,
                OwnerInstanceID = request.OwnerInstanceID,
                Target = request.Target,
                SpecificTarget = request.SpecificTarget,
                MainParticipants = request.MainParticipants,
                DecoyParticipants = request.DecoyParticipants,
                RandomProvider = request.RandomProvider,
                TargetOfficer = request.TargetOfficer,
                Discipline = option.Discipline,
            };
        }
    }
}
