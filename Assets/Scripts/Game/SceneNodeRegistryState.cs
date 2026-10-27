using System.Collections.Generic;
using Rebellion.Util.Serialization;

namespace Rebellion.Game
{
    /// <summary>
    /// Persists registration order and deleted-slot reuse independently of lookup implementation.
    /// </summary>
    [PersistableObject]
    public sealed class SceneNodeRegistryState
    {
        public List<string> SlotIDs { get; set; } = new();
        public List<int> FreeSlots { get; set; } = new();
    }
}
