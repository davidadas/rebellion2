/// <summary>
/// The IGameEntity interface serves as the foundational contract for all entities in the game.
/// It defines essential properties and methods that ensure consistency and interoperability
/// across all game objects. This interface standardizes how entities are identified, categorized,
/// and described, making it easier to manage, serialize, and interact with them in the game.
/// </summary>
/// <remarks>
/// This interface, along with the <see cref="ISceneNode"/> interface, was designed to allow other
/// interfaces to declare themselves as objects within the game. While classes implementing interfaces
/// that extend this will naturally inherit the associated properties and methods, this explicit structure
/// eliminates the need for cumbersome type casts or checks when interacting with game entities. This
/// approach is particularly beneficial when working with collections of entities, as it allows seamless
/// iteration and method calls without verifying types.
/// </remarks>
public interface IGameEntity
{
    public string InstanceID { get; set; }
    public string TypeID { get; set; }
    public string DisplayName { get; set; }
    public string Description { get; set; }

    /// <summary>
    /// Called to get the instance ID of the entity.
    /// </summary>
    /// <returns>The instance ID of the entity.</returns>
    public string GetInstanceID();

    /// <summary>
    /// Called to get the TypeID of the entity.
    /// </summary>
    /// <returns>The TypeID of the entity.</returns>
    public string GetTypeID();

    /// <summary>
    /// Called to get the DisplayName of the entity.
    /// </summary>
    /// <returns>The DisplayName of the entity.</returns>
    public string GetDisplayName();
}
