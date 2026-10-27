using Rebellion.SceneGraph;
using Rebellion.Util.Serialization;

namespace Rebellion.Game.Rules
{
    /// <summary>
    /// Represents an authored condition evaluated against game state and bindings.
    /// </summary>
    [PersistableObject]
    public abstract class GameConditional : BaseGameEntity { }
}
