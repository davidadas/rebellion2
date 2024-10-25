using System;
using System.Collections.Generic;
using ICollectionExtensions;

public interface IMissionService
{   
     public void InitiateMission(string missionType, List<string> mainParticipantInstanceIds, List<string> decoyParticipantInstanceIds, string targetInstanceId);
}

/// <summary>
/// 
/// </summary>
public class MissionService : IMissionService 
{
    private ILookupService lookupService;
    private IMovementService movementService;
    private Game game;

    /// <summary>
    /// 
    /// </summary>
    /// <param name="game"></param>
    public MissionService(ILookupService lookupService, IMovementService movementService, Game game)
    {
        this.lookupService = lookupService;
        this.movementService = movementService;
        this.game = game;
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="missionType"></param>
    /// <param name="mainParticipantInstanceIds"></param>
    /// <param name="decoyParticipantInstanceIds"></param>
    /// <param name="targetInstanceId"></param>
    public void InitiateMission(string missionType, List<string> mainParticipantInstanceIds, List<string> decoyParticipantInstanceIds, string targetInstanceId)
    {
        Mission mission = new Mission(missionType, mainParticipantInstanceIds, decoyParticipantInstanceIds, targetInstanceId);
        
        List<string> allParticipantInstanceIds = mainParticipantInstanceIds.AddAll(decoyParticipantInstanceIds);
        
        //
        GameEvent movementEvent = 
    }
}
