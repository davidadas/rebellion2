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
    }
}
