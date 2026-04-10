using System.Collections.Generic;
using Rebellion.Game;
using Rebellion.SceneGraph;
using Rebellion.Systems;
using Rebellion.Util.Common;

/// <summary>
/// Bundles all inputs needed to create a mission.
/// </summary>
public class MissionContext
{
    public GameRoot Game { get; set; }
    public string OwnerInstanceId { get; set; }
    public ISceneNode Target { get; set; }
    public List<IMissionParticipant> MainParticipants { get; set; }
    public List<IMissionParticipant> DecoyParticipants { get; set; }
    public IRandomNumberProvider RNG { get; set; }
    public FogOfWarSystem FogOfWar { get; set; }
}
