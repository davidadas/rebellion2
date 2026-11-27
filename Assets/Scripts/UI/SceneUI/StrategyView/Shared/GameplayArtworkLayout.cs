using System.Collections.Generic;
using Rebellion.SceneGraph;
using UnityEngine;

/// <summary>
/// Orders unit images inside their existing slots while keeping selection and badges above them.
/// </summary>
internal static class GameplayArtworkLayout
{
    /// <summary>
    /// Applies the unit's drawing order and keeps its statistics attached to the entity image.
    /// </summary>
    /// <param name="order">The resolved back-to-front order.</param>
    /// <param name="entity">The unit image.</param>
    /// <param name="travel">The movement image.</param>
    /// <param name="damage">The damage or injury image.</param>
    /// <param name="captured">The capture image.</param>
    /// <param name="stats">The optional statistics drawn immediately above the unit.</param>
    internal static void Apply(
        IReadOnlyList<GameplayArtworkLayer> order,
        Transform entity,
        Transform travel,
        Transform damage,
        Transform captured = null,
        Transform stats = null
    )
    {
        int first = Mathf.Min(
            Mathf.Min(entity.GetSiblingIndex(), travel.GetSiblingIndex()),
            Mathf.Min(
                damage.GetSiblingIndex(),
                captured != null ? captured.GetSiblingIndex() : int.MaxValue
            )
        );
        int next = first;
        bool needsOrder = false;
        foreach (GameplayArtworkLayer layer in order)
        {
            Transform image = Image(layer, entity, travel, damage, captured);
            if (image == null)
                continue;
            needsOrder |= image.GetSiblingIndex() != next++;
            if (layer == GameplayArtworkLayer.Entity && stats != null)
                needsOrder |= stats.GetSiblingIndex() != next++;
        }
        if (!needsOrder)
            return;
        // Move the image group back-to-front without disturbing unrelated siblings.
        for (int i = order.Count - 1; i >= 0; i--)
        {
            Transform image = Image(order[i], entity, travel, damage, captured);
            if (image == null)
                continue;
            if (order[i] == GameplayArtworkLayer.Entity && stats != null)
                stats.SetSiblingIndex(first);
            image.SetSiblingIndex(first);
        }
    }

    /// <summary>
    /// Finds the existing image that owns one semantic layer.
    /// </summary>
    /// <param name="layer">The unit artwork layer.</param>
    /// <param name="entity">The unit image.</param>
    /// <param name="travel">The movement image.</param>
    /// <param name="damage">The damage or injury image.</param>
    /// <param name="captured">The capture image.</param>
    /// <returns>The image occupying that layer.</returns>
    private static Transform Image(
        GameplayArtworkLayer layer,
        Transform entity,
        Transform travel,
        Transform damage,
        Transform captured
    ) =>
        layer switch
        {
            GameplayArtworkLayer.Travel => travel,
            GameplayArtworkLayer.Damage => damage,
            GameplayArtworkLayer.Captured => captured,
            _ => entity,
        };
}
