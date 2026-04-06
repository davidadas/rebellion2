using System;
using System.Collections.Generic;
using System.Linq;
using Rebellion.Game;
using Rebellion.SceneGraph;
using Rebellion.Util.Attributes;

/// <summary>
/// A <see cref="GameConditional"/> that is met when all child conditions are met.
/// </summary>
[PersistableObject(Name = "And")]
public class AndConditional : GameConditional
{
    [PersistableMember(Name = "Conditionals")]
    public List<GameConditional> Conditionals = new List<GameConditional>();

    public AndConditional()
        : base() { }

    public override bool IsMet(GameRoot game)
    {
        return Conditionals.All(conditional => conditional.IsMet(game));
    }
}

/// <summary>
/// A <see cref="GameConditional"/> that is met when any child condition is met.
/// </summary>
[PersistableObject(Name = "Or")]
public class OrConditional : GameConditional
{
    [PersistableMember(Name = "Conditionals")]
    public List<GameConditional> Conditionals = new List<GameConditional>();

    public OrConditional()
        : base() { }

    public override bool IsMet(GameRoot game)
    {
        return Conditionals.Any(conditional => conditional.IsMet(game));
    }
}

/// <summary>
/// A <see cref="GameConditional"/> that is met when none of the child conditions are met.
/// </summary>
[PersistableObject(Name = "Not")]
public class NotConditional : GameConditional
{
    [PersistableMember(Name = "Conditionals")]
    public List<GameConditional> Conditionals = new List<GameConditional>();

    public NotConditional()
        : base() { }

    public override bool IsMet(GameRoot game)
    {
        return Conditionals.All(conditional => !conditional.IsMet(game));
    }
}

/// <summary>
/// A <see cref="GameConditional"/> that is met when exactly one child condition is met.
/// </summary>
[PersistableObject(Name = "Xor")]
public class XorConditional : GameConditional
{
    [PersistableMember(Name = "Conditionals")]
    public List<GameConditional> Conditionals = new List<GameConditional>();

    public XorConditional()
        : base() { }

    public override bool IsMet(GameRoot game)
    {
        return Conditionals.Count(conditional => conditional.IsMet(game)) == 1;
    }
}

/// <summary>
/// A <see cref="GameConditional"/> that is met when all specified units are located on the same planet.
/// </summary>
[PersistableObject(Name = "AreOnSamePlanet")]
public class AreOnSamePlanetConditional : GameConditional
{
    [PersistableMember(Name = "UnitInstanceIDs")]
    public List<string> UnitInstanceIDs { get; set; }

    public AreOnSamePlanetConditional()
        : base() { }

    public override bool IsMet(GameRoot game)
    {
        List<ISceneNode> sceneNodes = game.GetSceneNodesByInstanceIDs(UnitInstanceIDs);
        Planet comparator = null;

        // Check if all units are on the same planet.
        foreach (ISceneNode node in sceneNodes)
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
/// A <see cref="GameConditional"/> that is met when exactly two units belong to different factions.
/// </summary>
[PersistableObject(Name = "AreOnOpposingFactions")]
public class AreOnOpposingFactionsConditional : GameConditional
{
    List<string> UnitInstanceIDs { get; set; } = new List<string>();

    public AreOnOpposingFactionsConditional()
        : base() { }

    public override bool IsMet(GameRoot game)
    {
        // Get the scene nodes for the units.
        List<ISceneNode> sceneNodes = game.GetSceneNodesByInstanceIDs(UnitInstanceIDs);

        // Check if the units are on opposing factions.
        return sceneNodes.Count == 2
            && sceneNodes[0].OwnerInstanceID != sceneNodes[1].OwnerInstanceID;
    }
}

/// <summary>
/// A <see cref="GameConditional"/> that is met when the specified unit is currently assigned to a mission.
/// </summary>
[PersistableObject(Name = "IsOnMission")]
public class IsOnMissionConditional : GameConditional
{
    public IsOnMissionConditional()
        : base() { }

    public override bool IsMet(GameRoot game)
    {
        string instanceId = this.GetConditionalValue();
        ISceneNode sceneNode = game.GetSceneNodeByInstanceID<ISceneNode>(instanceId);
        // Check if the unit is on a mission.
        return sceneNode?.GetParent() is Mission;
    }
}

/// <summary>
/// A <see cref="GameConditional"/> that is met when the specified unit implements <see cref="IMovable"/> and is currently movable.
/// </summary>
[PersistableObject(Name = "IsMovable")]
public class IsMovableConditional : GameConditional
{
    public IsMovableConditional()
        : base() { }

    public override bool IsMet(GameRoot game)
    {
        string instanceId = this.GetConditionalValue();
        ISceneNode sceneNode = game.GetSceneNodeByInstanceID<ISceneNode>(instanceId);

        // Check if the ISceneNode implements IMovable and is movable.
        if (sceneNode is IMovable movable)
        {
            return movable.IsMovable();
        }

        return false;
    }
}

/// <summary>
/// A <see cref="GameConditional"/> that is met when all specified units are located on any planet.
/// </summary>
[PersistableObject(Name = "AreOnPlanet")]
public class AreOnPlanetConditional : GameConditional
{
    public List<string> UnitInstanceIDs { get; set; }

    public AreOnPlanetConditional()
        : base() { }

    public override bool IsMet(GameRoot game)
    {
        // Get the instance IDs of the units to check.
        List<ISceneNode> sceneNodes = game.GetSceneNodesByInstanceIDs(UnitInstanceIDs);

        // Check if all units are on a planet.
        return sceneNodes.All(node => node.GetParentOfType<Planet>() != null);
    }
}

/// <summary>
/// A <see cref="GameConditional"/> that is met when the current tick count satisfies a comparison against a target value.
/// </summary>
[PersistableObject(Name = "TickCount")]
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

    public override bool IsMet(GameRoot game)
    {
        ComparisonType comparison = Enum.TryParse(
            this.GetConditionalType(),
            out ComparisonType result
        )
            ? result
            : ComparisonType.EqualTo;

        return comparison switch
        {
            ComparisonType.EqualTo => game.CurrentTick
                == Convert.ToInt32(this.GetConditionalValue()),
            ComparisonType.GreaterThan => game.CurrentTick
                > Convert.ToInt32(this.GetConditionalValue()),
            ComparisonType.LessThan => game.CurrentTick
                < Convert.ToInt32(this.GetConditionalValue()),
            _ => throw new InvalidOperationException(
                $"Invalid comparison type \"{comparison}\" for TickCountConditional."
            ),
        };
    }
}

/// <summary>
/// A <see cref="GameConditional"/> that is met when the specified game event has been completed.
/// </summary>
[PersistableObject(Name = "IsEventComplete")]
public class IsEventCompleteConditional : GameConditional
{
    public IsEventCompleteConditional()
        : base() { }

    public override bool IsMet(GameRoot game)
    {
        string eventInstanceId = this.GetConditionalValue();

        // Check if the event is complete.
        return game.IsEventComplete(eventInstanceId);
    }
}
