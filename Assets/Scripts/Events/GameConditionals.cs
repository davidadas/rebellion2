using System.Collections.Generic;
using System.Linq;
using System;
using DependencyInjectionExtensions;

/// <summary>
/// 
/// </summary>
public class AndConditional : GameConditional
{
    public AndConditional() : base() { }

    public AndConditional(SerializableDictionary<string, object> parameters) : base(parameters) { }
    
    public override bool IsMet(IServiceLocator serviceLocator)
    {
        List<GameConditional> conditionals = (List<GameConditional>)Parameters["Conditionals"];
        return conditionals.All(conditional => conditional.IsMet(serviceLocator));
    }
}

/// <summary>
/// 
/// </summary>
public class OrConditional : GameConditional
{
    public OrConditional() : base() { }
    
    public OrConditional(SerializableDictionary<string, object> parameters) : base(parameters) { }
    
    public override bool IsMet(IServiceLocator serviceLocator)
    {
        List<GameConditional> conditionals = (List<GameConditional>)Parameters["Conditionals"];
        return conditionals.Any(conditional => conditional.IsMet(serviceLocator));
    }
}

/// <summary>
/// 
/// </summary>
public class NotConditional : GameConditional
{
    public NotConditional() : base() { }
    
    public NotConditional(SerializableDictionary<string, object> parameters) : base(parameters) { }
    
    public override bool IsMet(IServiceLocator serviceLocator)
    {
        List<GameConditional> conditionals = (List<GameConditional>)Parameters["Conditionals"];
        return conditionals.All(conditional => !conditional.IsMet(serviceLocator));
    }
}

public class XorConditional : GameConditional
{
    public XorConditional() : base() { }
    
    public XorConditional(SerializableDictionary<string, object> parameters) : base(parameters) { }
    
    public override bool IsMet(IServiceLocator serviceLocator)
    {
        List<GameConditional> conditionals = (List<GameConditional>)Parameters["Conditionals"];
        return conditionals.Count(conditional => conditional.IsMet(serviceLocator)) == 1;
    }
}

public class AreOnSamePlanetConditional : GameConditional
{
    public AreOnSamePlanetConditional() : base() { }
    
    public AreOnSamePlanetConditional(SerializableDictionary<string, object> parameters) : base(parameters) { }
    
    public override bool IsMet(IServiceLocator serviceLocator)
    {
        ILookupService lookupService = serviceLocator.GetService<LookupService>();
        
        List<string> instanceIDs = (List<string>)Parameters["UnitInstanceIDs"];
        List<SceneNode> sceneNodes = lookupService.GetSceneNodesByInstanceIDs(instanceIDs);
        Planet comparator = null;
        
        // Check if all units are on the same planet.
        foreach (SceneNode node in sceneNodes)
        {
            if (node == null)
            {
                return false;
            }
            
            Planet planet = node.GetClosestParentOfType<Planet>();
            comparator ??= planet;
            
            if (comparator != planet)
            {
                return false;
            }
        }
        
        return true;
    }
}

public class AreOnOpposingFactionsConditional : GameConditional
{
    public AreOnOpposingFactionsConditional() : base() { }
    
    public AreOnOpposingFactionsConditional(SerializableDictionary<string, object> parameters) : base(parameters) { }
    
    public override bool IsMet(IServiceLocator serviceLocator)
    {
        LookupService lookupService = serviceLocator.GetService<LookupService>();
        
        List<string> instanceIDs = (List<string>)Parameters["UnitInstanceIDs"];
        
        // Get the scene nodes for the units.
        List<SceneNode> sceneNodes = lookupService.GetSceneNodesByInstanceIDs(instanceIDs);
        
        // Check if the units are on opposing factions.
        return sceneNodes.Count == 2 && sceneNodes[0].OwnerTypeID != sceneNodes[1].OwnerTypeID;
    }
}

public class IsOnMissionConditional : GameConditional
{
    public IsOnMissionConditional() : base() { }
    
    public IsOnMissionConditional(SerializableDictionary<string, object> parameters) : base(parameters) { }
    
    public override bool IsMet(IServiceLocator serviceLocator)
    {
        LookupService lookupService = serviceLocator.GetService<LookupService>();
        
        string instanceID = (string)Parameters["UnitInstanceID"];
        SceneNode sceneNode = lookupService.GetSceneNodeByInstanceID<SceneNode>(instanceID);
        
        // Check if the unit is on a mission.
        return sceneNode != null && sceneNode.GetParent() is Mission;
    }
}

public class AreOnPlanetConditional : GameConditional
{
    public AreOnPlanetConditional() : base() { }
    
    public AreOnPlanetConditional(SerializableDictionary<string, object> parameters) : base(parameters) { }
    
    public override bool IsMet(IServiceLocator serviceLocator)
    {
        LookupService lookupService = serviceLocator.GetService<LookupService>();
        
        // Get the instance IDs of the units to check.
        List<string> instanceIDs = (List<string>)Parameters["UnitInstanceIDs"];
        List<SceneNode> sceneNodes = lookupService.GetSceneNodesByInstanceIDs(instanceIDs);

        // Check if all units are on a planet.
        return sceneNodes.All(node => node.GetClosestParentOfType<Planet>() != null);
    }
}
