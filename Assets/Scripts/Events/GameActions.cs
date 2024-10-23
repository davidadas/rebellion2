using System.Collections.Generic;
using System.Linq;
using System;
using DependencyInjectionExtensions;

/// <summary>
/// 
/// </summary>
public class StartMissionAction : GameAction
{
    /// <summary>
    /// Default constructor used for serialization.
    /// </summary>
    public StartMissionAction() : base() { }
    
    /// <summary>
    /// 
    /// </summary>
    /// <param name="parameters"></param>
    public StartMissionAction(SerializableDictionary<string, object> parameters) : base(parameters) { }
    
    /// <summary>
    /// 
    /// </summary>
    /// <param name="mainParticipantIds"></param>
    /// <param name="decoyParticipantIds"></param>
    /// <param name="missionType"></param>
    /// <param name="targetId"></param>
    public StartMissionAction(List<string> mainParticipantIds, List<string> decoyParticipantIds, string missionType, string targetId)
        : base(new SerializableDictionary<string, object>
        {
            { "MainParticipantInstanceIDs", mainParticipantIds },
            { "DecoyParticipantInstanceIDs", decoyParticipantIds },
            { "MissionType", missionType },
            { "TargetInstanceID", targetId }
        }) { }

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
    /// <summary>
    /// Default constructor used for serialization.
    /// </summary>
    public ExecuteMissionAction() : base() { }
    
    /// <summary>
    /// 
    /// </summary>
    /// <param name="parameters"></param>
    public ExecuteMissionAction(SerializableDictionary<string, object> parameters) : base(parameters) { }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="locator"></param>
    public override void Execute(IServiceLocator locator)
    {
        ILookupService lookupService = locator.GetService<LookupService>();     

        // Get the parameters for the action.   
        string missionId = (string)Parameters["MissionInstanceID"];

        // Execute the mission.
        missionService.Execute(missionId);
    }
}

/// <summary>
/// 
/// </summary>
public class MoveUnitsAction : GameAction
{
    /// <summary>
    /// Default constructor used for serialization.
    /// </summary>
    public MoveUnitsAction() : base() { }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="parameters"></param>
    public MoveUnitsAction(SerializableDictionary<string, object> parameters) : base(parameters) { }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="units"></param>
    /// <param name="target"></param>
    public MoveUnitsAction(List<SceneNode> units, SceneNode target)
        : base(new SerializableDictionary<string, object>
        {
            { "UnitInstanceIDs", units.Select(unit => unit.InstanceID).ToList() },
            { "TargetInstanceID", target.InstanceID }
        }) { }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="locator"></param>
    public override void Execute(IServiceLocator locator)
    {
        IMovementService movementService = locator.GetService<MovementService>();
        ILookupService lookupService = locator.GetService<LookupService>();
        
        // Get the parameters for the action.
        List<string> unitInstanceIds = (List<string>)Parameters["UnitInstanceIDs"];
        string targetInstanceId = (string)Parameters["TargetInstanceID"];
        
        // Move the units.
        movementService.MoveUnits(unitInstanceIds, targetInstanceId);
    }
}
