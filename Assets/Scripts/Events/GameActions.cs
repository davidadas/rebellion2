using System.Collections.Generic;
using System.Linq;
using System;
using DependencyInjectionExtensions;

/// <summary>
/// 
/// </summary>
public class StartMissionAction : GameAction
{
    public StartMissionAction() : base() { }
    
    public StartMissionAction(SerializableDictionary<string, object> parameters) : base(parameters) { }
    
    /// <summary>
    /// 
    /// </summary>
    /// <param name="locator"></param>
    public override void Execute(IServiceLocator locator)
    {
        IMissionService missionService = locator.GetService<MissionService>();
        ILookupService lookupService = locator.GetService<LookupService>();

        // Get the parameters for the action.
        List<string> mainParticipantIds = (List<string>)Parameters["MainParticipantInstanceIDs"];
        List<string> decoyParticipantIds = (List<string>)Parameters["DecoyParticipantInstanceIDs"];
        string missionType = (string)Parameters["MissionType"];
        string targetId = (string)Parameters["TargetInstanceID"];

        // Execute the mission.
        missionService.StartMission(missionType, mainParticipantIds, decoyParticipantIds, targetId);
    }
}

/// <summary>
/// 
/// </summary>
public class ExecuteMissionAction : GameAction
{
    public ExecuteMissionAction() : base() { }
    
    public ExecuteMissionAction(SerializableDictionary<string, object> parameters) : base(parameters) { }

    public override void Execute(IServiceLocator locator)
    {
        IMissionService missionService = locator.GetService<MissionService>();
        ILookupService lookupService = locator.GetService<LookupService>();     

        // Get the parameters for the action.   
        string missionId = (string)Parameters["MissionInstanceID"];
        Mission mission = lookupService.GetSceneNodeByInstanceID<Mission>(missionId);

        // @TODO: Execute the mission.
    }
}
