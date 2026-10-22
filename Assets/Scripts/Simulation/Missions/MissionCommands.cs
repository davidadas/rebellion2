using System;
using System.Collections.Generic;
using System.Linq;
using Rebellion.Game;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Missions;
using Rebellion.Game.Results;
using Rebellion.Game.Units;
using Rebellion.SceneGraph;
using Rebellion.Util.Random;

namespace Rebellion.Simulation
{
    /// <summary>
    /// Starts and aborts missions requested by players, AI, and game events.
    /// </summary>
    public class MissionCommands
    {
        private readonly GameRoot _game;
        private readonly MissionQueries _queries;
        private readonly MissionResolver _resolver;
        private readonly OfficerCommandCommands _officerCommands;

        /// <summary>
        /// Raised when an immediate mission operation produces results.
        /// </summary>
        public event Action<IReadOnlyList<GameResult>> ResultsProduced;

        /// <summary>
        /// Creates mission commands with their creation and resolution dependencies.
        /// </summary>
        /// <param name="game">The active game state.</param>
        /// <param name="provider">The random number provider for mission resolution.</param>
        /// <param name="movementManager">The movement system used for participant travel.</param>
        /// <param name="uprisingSystem">The uprising system used by uprising missions.</param>
        /// <param name="queries">The mission eligibility and probability queries.</param>
        /// <param name="movementQueries">The mission-return destination rules.</param>
        /// <param name="betrayalResolver">The mission-betrayal resolver.</param>
        /// <param name="personnelCommands">The personnel lifecycle commands.</param>
        /// <param name="officerCommands">The officer command-assignment operations.</param>
        public MissionCommands(
            GameRoot game,
            IRandomNumberProvider provider,
            MovementCommands movementManager,
            UprisingResolver uprisingSystem,
            MissionQueries queries,
            MovementQueries movementQueries,
            MissionBetrayalResolver betrayalResolver = null,
            PersonnelCommands personnelCommands = null,
            OfficerCommandCommands officerCommands = null
        )
        {
            _game = game ?? throw new ArgumentNullException(nameof(game));
            _queries = queries ?? throw new ArgumentNullException(nameof(queries));
            _officerCommands = officerCommands ?? new OfficerCommandCommands(game);
            _resolver = new MissionResolver(
                game,
                provider,
                movementManager,
                uprisingSystem,
                queries,
                movementQueries,
                betrayalResolver,
                personnelCommands
            );
        }

        /// <summary>
        /// Creates mission commands around the registered mission resolver.
        /// </summary>
        /// <param name="game">The active game state.</param>
        /// <param name="queries">The mission eligibility and probability queries.</param>
        /// <param name="resolver">The registered mission lifecycle resolver.</param>
        /// <param name="officerCommands">The officer command-assignment operations.</param>
        internal MissionCommands(
            GameRoot game,
            MissionQueries queries,
            MissionResolver resolver,
            OfficerCommandCommands officerCommands
        )
        {
            _game = game ?? throw new ArgumentNullException(nameof(game));
            _queries = queries ?? throw new ArgumentNullException(nameof(queries));
            _resolver = resolver ?? throw new ArgumentNullException(nameof(resolver));
            _officerCommands =
                officerCommands ?? throw new ArgumentNullException(nameof(officerCommands));
        }

        /// <summary>
        /// Creates, attaches, and starts a mission from the supplied context.
        /// </summary>
        /// <param name="context">The mission context to resolve and start.</param>
        /// <returns>True when the mission was started.</returns>
        public bool InitiateMission(MissionContext context)
        {
            if (!_queries.TryCreateMission(context, out Mission mission))
                return false;

            ISceneNode liveLocation = _queries.ResolveSceneNode(context.Location);
            Planet planet = liveLocation is Planet p ? p : liveLocation?.GetParentOfType<Planet>();
            if (planet == null)
                return false;

            _game.AttachNode(mission, planet);
            List<IMissionParticipant> startingParticipants = mission.GetAllParticipants();
            foreach (Officer officer in startingParticipants.OfType<Officer>())
                _officerCommands.TrySetRank(
                    officer.InstanceID,
                    OfficerRank.None,
                    officer.GetOwnerInstanceID()
                );
            List<GameResult> results = new List<GameResult>();
            results.Add(
                new MissionStartedResult
                {
                    Mission = mission,
                    MissionTypeID = mission.ConfigKey,
                    Location = planet,
                    Participants = mission.GetAllParticipants(),
                    SourceEventInstanceID = mission.SourceEventInstanceID,
                    Tick = _game.CurrentTick,
                }
            );

            _resolver.BeginMission(mission, startingParticipants, results);
            ResultsProduced?.Invoke(results);
            return true;
        }

        /// <summary>
        /// Aborts an active mission and resolves its participants' post-mission location.
        /// </summary>
        /// <param name="missionInstanceID">The instance ID of the mission to abort.</param>
        /// <returns>True when the mission was found and aborted.</returns>
        public bool AbortMission(string missionInstanceID)
        {
            return AbortMissions(new[] { missionInstanceID });
        }

        /// <summary>
        /// Aborts a complete mission selection after resolving every persistent identity.
        /// </summary>
        /// <param name="missionInstanceIDs">The instance IDs of the missions to abort.</param>
        /// <returns>True when every mission was found and aborted.</returns>
        public bool AbortMissions(IReadOnlyList<string> missionInstanceIDs)
        {
            if (missionInstanceIDs == null || missionInstanceIDs.Count == 0)
                return false;

            List<Mission> missions = new List<Mission>();
            HashSet<string> resolvedInstanceIDs = new HashSet<string>(StringComparer.Ordinal);
            foreach (string missionInstanceID in missionInstanceIDs)
            {
                if (string.IsNullOrEmpty(missionInstanceID))
                    return false;
                if (!resolvedInstanceIDs.Add(missionInstanceID))
                    continue;

                Mission mission = _game.GetSceneNodeByInstanceID<Mission>(missionInstanceID);
                if (mission == null)
                    return false;

                missions.Add(mission);
            }

            if (missions.Count == 0)
                return false;

            List<GameResult> results = new List<GameResult>();
            foreach (Mission mission in missions)
                _resolver.InterruptMission(mission, results);
            ResultsProduced?.Invoke(results);
            return true;
        }
    }
}
