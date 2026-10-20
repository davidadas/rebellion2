using Rebellion.SceneGraph;

namespace Rebellion.Game.Missions
{
    /// <summary>
    /// Repeats the espionage objective while its agents remain assigned to the planet.
    /// </summary>
    public sealed class SurveillanceMission : EspionageMission
    {
        public new const string MissionTypeID = "Surveillance";

        internal override string RulesKey => EspionageMission.MissionTypeID;

        /// <summary>
        /// Creates an empty surveillance mission for deserialization.
        /// </summary>
        public SurveillanceMission()
        {
            ConfigKey = MissionTypeID;
            DisplayName = MissionTypeID;
        }

        /// <summary>
        /// Assigns the shared espionage behavior to a continuous surveillance mission.
        /// </summary>
        /// <param name="context">The owner, target planet, and assigned participants.</param>
        private SurveillanceMission(MissionContext context)
            : base(MissionTypeID, context) { }

        /// <summary>
        /// Creates a surveillance mission for a planet visited by its owner.
        /// </summary>
        /// <param name="context">The requested mission assignment.</param>
        /// <returns>The new mission, or null when the target is unavailable.</returns>
        public static new SurveillanceMission TryCreate(MissionContext context)
        {
            return HasVisitedTarget(context) ? new SurveillanceMission(context) : null;
        }

        /// <summary>
        /// Creates an empty copy that retains the surveillance mission type.
        /// </summary>
        /// <returns>A new surveillance mission.</returns>
        protected override BaseSceneNode CreateNodeCopy() => new SurveillanceMission();

        /// <summary>
        /// Keeps agents assigned after each resolved espionage attempt.
        /// </summary>
        /// <param name="game">The game containing the mission.</param>
        /// <returns>True; the shared lifecycle handles cancellation and interruptions.</returns>
        public override bool ShouldRepeatAfterCompletion(GameRoot game) => true;
    }
}
