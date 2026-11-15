using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// Retains owner-selection presentation while an open menu keeps its commands and hover state.
/// </summary>
public sealed class ContextMenuSelectionState
{
    public IReadOnlyList<Color32> Colors { get; private set; }

    /// <summary>
    /// Creates an empty selection presentation before its owner supplies a snapshot.
    /// </summary>
    public ContextMenuSelectionState()
    {
        Colors = Array.Empty<Color32>();
    }

    /// <summary>
    /// Replaces the selecting owners without changing the menu command.
    /// </summary>
    /// <param name="colors">The current selecting owners' faction colors.</param>
    public void SetColors(IReadOnlyList<Color32> colors)
    {
        if (colors == null)
            throw new ArgumentNullException(nameof(colors));
        if (!Colors.SequenceEqual(colors))
            Colors = Array.AsReadOnly(colors.ToArray());
    }
}
