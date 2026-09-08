using System.Collections.Generic;

namespace Rebellion.Game.Traits
{
    /// <summary>
    /// Identifies a game object that owns permanent content-authored traits.
    /// </summary>
    public interface ITraitTarget
    {
        List<string> TraitIDs { get; set; }
    }
}
