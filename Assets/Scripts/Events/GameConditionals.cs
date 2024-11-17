using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>
///
/// </summary>
public class AndConditional : GameConditional
{
    public AndConditional()
        : base() { }

    public AndConditional(Dictionary<string, object> parameters)
        : base(parameters) { }

    public override bool IsMet(Game game)
    {
        List<GameConditional> conditionals = (List<GameConditional>)Parameters["Conditionals"];
        return conditionals.All(conditional => conditional.IsMet(game));
    }
}

/// <summary>
///
/// </summary>
public class OrConditional : GameConditional
{
    public OrConditional()
        : base() { }

    public OrConditional(Dictionary<string, object> parameters)
        : base(parameters) { }

    public override bool IsMet(Game game)
    {
        List<GameConditional> conditionals = (List<GameConditional>)Parameters["Conditionals"];
        return conditionals.Any(conditional => conditional.IsMet(game));
    }
}

/// <summary>
///
/// </summary>
public class NotConditional : GameConditional
{
    public NotConditional()
        : base() { }

    public NotConditional(Dictionary<string, object> parameters)
        : base(parameters) { }

    public override bool IsMet(Game game)
    {
        List<GameConditional> conditionals = (List<GameConditional>)Parameters["Conditionals"];
        return conditionals.All(conditional => !conditional.IsMet(game));
    }
}

/// <summary>
///
/// </summary>
public class XorConditional : GameConditional
{
    public XorConditional()
        : base() { }

    public XorConditional(Dictionary<string, object> parameters)
        : base(parameters) { }

    public override bool IsMet(Game game)
    {
        List<GameConditional> conditionals = (List<GameConditional>)Parameters["Conditionals"];
        return conditionals.Count(conditional => conditional.IsMet(game)) == 1;
    }
}

/// <summary>
///
/// </summary>
public class AreOnSamePlanetConditional : GameConditional
{
    public AreOnSamePlanetConditional()
        : base() { }

    public AreOnSamePlanetConditional(Dictionary<string, object> parameters)
        : base(parameters) { }

    public override bool IsMet(Game game)
    {
        List<string> instanceIDs = (List<string>)Parameters["UnitInstanceIDs"];
        List<SceneNode> sceneNodes = game.GetSceneNodesByInstanceIDs(instanceIDs);
        Planet comparator = null;

        // Check if all units are on the same planet.
        foreach (SceneNode node in sceneNodes)
        {
            if (node == null)
            {
                return false;
            }

            Planet planet = node.GetParentOfType<Planet>();
            comparator ??= planet;

            if (comparator != planet)
            {
                return false;
            }
        }

        return true;
    }
}

/// <summary>
///
/// </summary>
public class AreOnOpposingFactionsConditional : GameConditional
{
    public AreOnOpposingFactionsConditional()
        : base() { }

    public AreOnOpposingFactionsConditional(Dictionary<string, object> parameters)
        : base(parameters) { }

    public override bool IsMet(Game game)
    {
        List<string> instanceIDs = (List<string>)Parameters["UnitInstanceIDs"];

        // Get the scene nodes for the units.
        List<SceneNode> sceneNodes = game.GetSceneNodesByInstanceIDs(instanceIDs);

        // Check if the units are on opposing factions.
        return sceneNodes.Count == 2 && sceneNodes[0].OwnerTypeID != sceneNodes[1].OwnerTypeID;
    }
}

/// <summary>
///
/// </summary>
public class IsOnMissionConditional : GameConditional
{
    public IsOnMissionConditional()
        : base() { }

    public IsOnMissionConditional(Dictionary<string, object> parameters)
        : base(parameters) { }

    public override bool IsMet(Game game)
    {
        string instanceID = (string)Value;
        SceneNode sceneNode = game.GetSceneNodeByInstanceID<SceneNode>(instanceID);

        // Check if the unit is on a mission.
        return sceneNode != null && sceneNode.GetParent() is Mission;
    }
}

/// <summary>
///
/// </summary>
public class IsMovableConditional : GameConditional
{
    public IsMovableConditional()
        : base() { }

    public IsMovableConditional(Dictionary<string, object> parameters)
        : base(parameters) { }

    public override bool IsMet(Game game)
    {
        string instanceID = (string)Value;
        SceneNode sceneNode = game.GetSceneNodeByInstanceID<SceneNode>(instanceID);

        // Check if the SceneNode implements IMovable and is movable.
        if (sceneNode is IMovable movable)
        {
            return movable.IsMovable();
        }

        return false;
    }
}

/// <summary>
///
/// </summary>
public class AreOnPlanetConditional : GameConditional
{
    public AreOnPlanetConditional()
        : base() { }

    public AreOnPlanetConditional(Dictionary<string, object> parameters)
        : base(parameters) { }

    public override bool IsMet(Game game)
    {
        // Get the instance IDs of the units to check.
        List<string> instanceIDs = (List<string>)Parameters["UnitInstanceIDs"];
        List<SceneNode> sceneNodes = game.GetSceneNodesByInstanceIDs(instanceIDs);

        // Check if all units are on a planet.
        return sceneNodes.All(node => node.GetParentOfType<Planet>() != null);
    }
}

/// <summary>
///
/// </summary>
public class TickCountConditional : GameConditional
{
    private enum ComparisonType
    {
        EqualTo,
        GreaterThan,
        LessThan,
    }

    public TickCountConditional()
        : base() { }

    public TickCountConditional(Dictionary<string, object> parameters)
        : base(parameters) { }

    public override bool IsMet(Game game)
    {
        string comparisonValue = (string)Parameters["Comparison"];
        ComparisonType comparison = Enum.TryParse(comparisonValue, out ComparisonType result)
            ? result
            : ComparisonType.EqualTo;
        int targetTickCount = Convert.ToInt32(Parameters["Value"]);

        // Check if the current tick count meets the comparison.
        return comparison switch
        {
            ComparisonType.EqualTo => game.CurrentTick == targetTickCount,
            ComparisonType.GreaterThan => game.CurrentTick > targetTickCount,
            ComparisonType.LessThan => game.CurrentTick < targetTickCount,
            _ => throw new InvalidSceneOperationException(
                "Invalid comparison type for TickCountConditional."
            ),
        };
    }
}

/// <summary>
///
/// </summary>
public class IsEventCompleteConditional : GameConditional
{
    public IsEventCompleteConditional()
        : base() { }

    public IsEventCompleteConditional(Dictionary<string, object> parameters)
        : base(parameters) { }

    public override bool IsMet(Game game)
    {
        string eventInstanceId = (string)Value;

        // Check if the event is complete.
        return game.IsEventComplete(eventInstanceId);
    }
}
