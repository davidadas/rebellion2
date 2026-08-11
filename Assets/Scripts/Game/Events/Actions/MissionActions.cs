using System;
using System.Collections.Generic;
using Rebellion.Game.Missions;
using Rebellion.Game.Results;
using Rebellion.SceneGraph;
using Rebellion.Util.Serialization;

namespace Rebellion.Game.Events
{
    /// <summary>
    /// Starts a content-defined mission with its target and standard participant groups.
    /// </summary>
    [PersistableObject(Name = "CreateMission")]
    public sealed class CreateMissionAction : GameAction
    {
        [PersistableAttribute]
        public string MissionDefinitionID { get; set; }

        public MissionUnitReference Target { get; set; }
        public List<MissionUnitReference> Participants { get; set; } =
            new List<MissionUnitReference>();
        public List<MissionUnitReference> Decoys { get; set; } = new List<MissionUnitReference>();

        public override List<GameResult> Execute(GameRoot game)
        {
            if (Target == null || string.IsNullOrWhiteSpace(Target.UnitInstanceID))
                throw new InvalidOperationException("CreateMission requires a target unit.");
            ResolveUnit(game, Target);
            foreach (MissionUnitReference participant in Participants)
                ResolveUnit(game, participant);
            foreach (MissionUnitReference participant in Decoys)
                ResolveUnit(game, participant);

            return new List<GameResult>
            {
                new CustomMissionRequestedResult
                {
                    MissionDefinitionID = MissionDefinitionID,
                    TargetInstanceID = Target.UnitInstanceID,
                    MainParticipantInstanceIDs = Participants.ConvertAll(participant =>
                        participant.UnitInstanceID
                    ),
                    DecoyParticipantInstanceIDs = Decoys.ConvertAll(participant =>
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
                    $"CreateMission could not resolve unit '{reference?.UnitInstanceID}'."
                );
        }
    }
}
