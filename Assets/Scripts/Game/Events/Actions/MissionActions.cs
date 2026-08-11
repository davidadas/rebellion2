using System;
using System.Collections.Generic;
using Rebellion.Game.Missions;
using Rebellion.Game.Results;
using Rebellion.Game.Units;
using Rebellion.SceneGraph;
using Rebellion.Util.Serialization;

namespace Rebellion.Game.Events
{
    /// <summary>
    /// Starts a content-defined mission with its target and standard participant groups.
    /// </summary>
    [PersistableObject(Name = "Mission")]
    public sealed class MissionAction : GameAction
    {
        [PersistableAttribute]
        public string MissionDefinitionID { get; set; }

        public MissionUnitReference Target { get; set; }
        public List<MissionUnitReference> MainParticipants { get; set; } =
            new List<MissionUnitReference>();
        public List<MissionUnitReference> DecoyParticipants { get; set; } =
            new List<MissionUnitReference>();

        public override List<GameResult> Execute(GameRoot game)
        {
            if (Target == null || string.IsNullOrWhiteSpace(Target.UnitInstanceID))
                throw new InvalidOperationException("Mission requires a target unit.");
            ResolveUnit(game, Target);
            foreach (MissionUnitReference participant in MainParticipants)
                ResolveUnit(game, participant);
            foreach (MissionUnitReference participant in DecoyParticipants)
                ResolveUnit(game, participant);

            return new List<GameResult>
            {
                new CustomMissionRequestedResult
                {
                    MissionDefinitionID = MissionDefinitionID,
                    TargetInstanceID = Target.UnitInstanceID,
                    MainParticipantInstanceIDs = MainParticipants.ConvertAll(participant =>
                        participant.UnitInstanceID
                    ),
                    DecoyParticipantInstanceIDs = DecoyParticipants.ConvertAll(participant =>
                        participant.UnitInstanceID
                    ),
                    Tick = game.CurrentTick,
                },
            };
        }

        private static void ResolveUnit(GameRoot game, MissionUnitReference reference)
        {
            if (game.GetSceneNodeByInstanceID<ISceneNode>(reference?.UnitInstanceID) == null)
                throw new InvalidOperationException(
                    $"Mission could not resolve unit '{reference?.UnitInstanceID}'."
                );
        }
    }

    /// <summary>
    /// Announces a bounty-hunter attack through the normal result pipeline.
    /// </summary>
    [PersistableObject(Name = "BountyAttack")]
    public sealed class BountyAttackAction : GameAction
    {
        [PersistableAttribute]
        public string OfficerInstanceID { get; set; }

        /// <inheritdoc />
        public override List<GameResult> Execute(GameRoot game)
        {
            Officer officer = game.GetSceneNodeByInstanceID<Officer>(OfficerInstanceID);
            if (officer == null)
                throw new InvalidOperationException(
                    $"BountyAttack could not resolve officer '{OfficerInstanceID}'."
                );

            return new List<GameResult>
            {
                new BountyAttackResult { Officer = officer, Tick = game.CurrentTick },
            };
        }
    }
}
