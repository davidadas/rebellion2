using System.Collections.Generic;
using System.Linq;
using Rebellion.Core.Simulation;
using Rebellion.Game;
using Rebellion.Game.Results;
using Rebellion.SceneGraph;
using Rebellion.Util.Common;
using Rebellion.Util.Extensions;

public class SabotageMission : Mission
{
    public override bool CanceledOnOwnershipChange => false;

    /// <summary>
    /// Default constructor used for deserialization.
    /// </summary>
    public SabotageMission()
        : base()
    {
        ConfigKey = "Sabotage";
        DisplayName = ConfigKey;
        ParticipantSkill = MissionParticipantSkill.Combat;
    }

    public SabotageMission(
        string ownerInstanceId,
        ISceneNode target,
        List<IMissionParticipant> mainParticipants,
        List<IMissionParticipant> decoyParticipants,
        ProbabilityTable successProbabilityTable = null
    )
        : base(
            "Sabotage",
            ownerInstanceId,
            RequirePlanetTarget(target, "Sabotage").GetInstanceID(),
            mainParticipants,
            decoyParticipants,
            MissionParticipantSkill.Combat,
            successProbabilityTable
        ) { }

    /// <summary>
    /// Returns false if the target planet has no buildings remaining before execution.
    /// </summary>
    protected override bool IsTargetValid(GameRoot game)
    {
        return GetParent() is Planet p && p.GetAllBuildings().Count > 0;
    }

    /// <summary>
    /// Destroys the first building on the target planet.
    /// </summary>
    protected override List<GameResult> OnSuccess(GameRoot game, IRandomNumberProvider provider)
    {
        Planet planet = GetParent() as Planet;
        List<Building> buildings = planet.GetAllBuildings();
        Building target = buildings[0];
        game.DetachNode(target);

        return new List<GameResult>
        {
            new GameObjectSabotagedResult
            {
                SabotagedObject = target,
                Context = planet,
                Tick = game.CurrentTick,
            },
        };
    }

    /// <summary>
    /// Sabotage awards both Combat +1 and Espionage +1 on success.
    /// </summary>
    protected override void ImproveMissionParticipantsSkill()
    {
        base.ImproveMissionParticipantsSkill();
        foreach (IMissionParticipant participant in MainParticipants.Concat(DecoyParticipants))
        {
            if (participant.CanImproveMissionSkill)
            {
                participant.SetMissionSkillValue(
                    MissionParticipantSkill.Espionage,
                    participant.GetMissionSkillValue(MissionParticipantSkill.Espionage) + 1
                );
            }
        }
    }

    /// <summary>
    /// Sabotage missions do not repeat — one attempt per mission.
    /// </summary>
    public override bool CanContinue(GameRoot game)
    {
        return false;
    }
}
