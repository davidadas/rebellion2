using System;
using System.Collections.Generic;

namespace Rebellion.SceneGraph
{
    /// <summary>
    /// Identifies the independently drawn parts of a unit's gameplay artwork.
    /// </summary>
    public enum GameplayArtworkLayer
    {
        Entity,
        Travel,
        Damage,
        Captured,
    }

    /// <summary>
    /// Resolves optional artwork ordering without dropping unconfigured status layers.
    /// </summary>
    public static class GameplayArtworkOrder
    {
        private static readonly IReadOnlyList<GameplayArtworkLayer> _default = Array.AsReadOnly(
            new[]
            {
                GameplayArtworkLayer.Entity,
                GameplayArtworkLayer.Travel,
                GameplayArtworkLayer.Damage,
                GameplayArtworkLayer.Captured,
            }
        );

        /// <summary>
        /// Resolves a back-to-front stack, retaining the established order for omitted entries.
        /// </summary>
        /// <param name="configured">The optional order authored for this unit.</param>
        /// <returns>Each supported layer exactly once, in drawing order.</returns>
        public static IReadOnlyList<GameplayArtworkLayer> Resolve(
            IEnumerable<GameplayArtworkLayer> configured
        )
        {
            if (configured == null)
                return _default;
            var result = new List<GameplayArtworkLayer>(4);
            foreach (GameplayArtworkLayer layer in configured)
                if ((uint)layer <= (uint)GameplayArtworkLayer.Captured && !result.Contains(layer))
                    result.Add(layer);
            foreach (GameplayArtworkLayer layer in _default)
                if (!result.Contains(layer))
                    result.Add(layer);
            return result;
        }
    }
}
