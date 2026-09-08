using System.Collections.Generic;

namespace Rebellion.Game.Traits
{
    /// <summary>
    /// Identifies a game object that can hold temporary, content-authored status effects.
    /// </summary>
    public interface IStatusEffectTarget
    {
        /// <summary>
        /// Maps each currently applied status-effect ID to its expiration tick.
        /// </summary>
        Dictionary<string, int?> ActiveStatusEffects { get; set; }
    }
}
