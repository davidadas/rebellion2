using System;
using System.Collections.Generic;

public interface IMissionService
{
     public void StartMission(string missionType, List<string> mainParticipantInstanceIds, List<string> decoyParticipantInstanceIds, string targetInstanceId);
}

/// <summary>
/// 
/// </summary>
public class MissionService : IMissionService 
{
    private Game game;

    /// <summary>
    /// 
    /// </summary>
    /// <param name="game"></param>
    public MissionService(Game game)
    {
        this.game = game;
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="missionType"></param>
    /// <param name="mainParticipantInstanceIds"></param>
    /// <param name="decoyParticipantInstanceIds"></param>
    /// <param name="targetInstanceId"></param>
    public void StartMission(string missionType, List<string> mainParticipantInstanceIds, List<string> decoyParticipantInstanceIds, string targetInstanceId)
    {
        
    }
}
