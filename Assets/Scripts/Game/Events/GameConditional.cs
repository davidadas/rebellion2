using Rebellion.SceneGraph;
using Rebellion.Util.Serialization;

namespace Rebellion.Game.Events
{
    /// <summary>
    /// Represents an authored condition evaluated within one game-event activation.
    /// </summary>
    [PersistableObject]
    public abstract class GameConditional : BaseGameEntity { }
}
