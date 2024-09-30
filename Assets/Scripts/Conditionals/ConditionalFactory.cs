using System.Collections.Generic;
using System.Linq;
using System;

/// <summary>
/// 
/// </summary>
/// @TODO: Add mechanism for to check parameters for validity before attempting to use them.
public static class ConditionalFactory
{
    
    private delegate GameConditional ConditionalCreator(Dictionary<string, object> parameters);
    private static readonly Dictionary<string, ConditionalCreator> conditionCreators = new Dictionary<string, ConditionalCreator>
    {
        { "IsOnSamePlanet", CreateAreOnSamePlanet },
        { "AreOnOpposingFactions", CreateAreOnOpposingFactions },
    };

    /// <summary>
    /// 
    /// </summary>
    /// <param name="conditionType"></param>
    /// <param name="parameters"></param>
    /// <returns></returns>
    /// <exception cref="ArgumentException"></exception>
    public static GameConditional CreateConditional(string conditionType, Dictionary<string, object> parameters)
    {
        if (conditionCreators.TryGetValue(conditionType, out var creator))
        {
            return creator(parameters);
        }
        else
        {
            throw new ArgumentException($"Invalid condition type: {conditionType}");
        }
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="parameters"></param>
    /// <returns></returns>
    private static GameConditional CreateAreOnSamePlanet(Dictionary<string, object> parameters)
    {
        return new GenericConditional(AreOnSamePlanet, parameters);
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="parameters"></param>
    /// <returns></returns>
    private static GameConditional CreateAreOnOpposingFactions(Dictionary<string, object> parameters)
    {
        return new GenericConditional(AreOnOpposingFactions, parameters);
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="game"></param>
    /// <param name="parameters"></param>
    /// <returns></returns>
    private static bool AreOnSamePlanet(Game game, Dictionary<string, object> parameters)
    {
        List<string> instanceIDs = (List<string>)parameters["InstanceIDs"];
        List<SceneNode> sceneNodes = instanceIDs.Select(instanceId => game.GetSceneNodeByInstanceID(instanceId)).ToList();
        Planet comparator = null;

        foreach (SceneNode node in sceneNodes)
        {
            // If any of the nodes are null, return false.
            if (node == null)
            {
                return false;
            }

            // If the comparator is null, set it to the planet of the current node.
            Planet planet = node.GetClosestParentOfType<Planet>();
            comparator ??= planet;
            
            // If the planet of the current node is not the same as the comparator, return false.
            if (comparator != planet)
            {
                return false;
            }
        }
 
        return true;
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="game"></param>
    /// <param name="parameters"></param>
    /// <returns></returns>
    private static bool AreOnOpposingFactions(Game game, Dictionary<string, object> parameters)
    {
        var instanceIDs = (List<string>)parameters["InstanceIDs"];
        var sceneNodes = instanceIDs
            .Select(game.GetSceneNodeByInstanceID)
            .Where(node => node != null)
            .ToList();

        return sceneNodes.Count == 2 && sceneNodes[0].OwnerGameID != sceneNodes[1].OwnerGameID;
    }
}
