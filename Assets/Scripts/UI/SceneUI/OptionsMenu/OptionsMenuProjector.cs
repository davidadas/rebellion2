using System;
using System.Collections.Generic;

/// <summary>
/// Projects the current Options session into immutable view data.
/// </summary>
internal static class OptionsMenuProjector
{
    /// <summary>
    /// Builds the presentation state for an open Options window.
    /// </summary>
    /// <param name="window">The open Options window shell.</param>
    /// <param name="activeTab">The page currently selected by the user.</param>
    /// <param name="settings">The staged settings transaction.</param>
    /// <param name="bindings">The staged binding transaction.</param>
    /// <param name="saveSlots">The save rows available in the current scene.</param>
    /// <param name="selectedSlot">The selected save-row index, or -1.</param>
    /// <param name="actions">The capabilities supplied by the owning scene.</param>
    /// <returns>The immutable state rendered by the Options view.</returns>
    internal static OptionsMenuRenderData Build(
        UIWindow window,
        OptionsMenuTab activeTab,
        OptionsSettingsSession settings,
        OptionsBindingEditor bindings,
        IReadOnlyList<OptionsSaveSlot> saveSlots,
        int selectedSlot,
        IOptionsMenuActions actions
    )
    {
        if (window == null)
            throw new ArgumentNullException(nameof(window));
        if (settings == null)
            throw new ArgumentNullException(nameof(settings));
        if (bindings == null)
            throw new ArgumentNullException(nameof(bindings));
        if (actions == null)
            throw new ArgumentNullException(nameof(actions));

        return new OptionsMenuRenderData(
            window.X,
            window.Y,
            activeTab,
            settings.ResolutionLabel,
            settings.FullScreenLabel,
            settings.GetTacticalStates(),
            settings.GetVolumes(),
            bindings.Rows,
            saveSlots,
            selectedSlot,
            actions.CanWriteSaves,
            true,
            actions.CanReturnToGame,
            actions.CanReturnToMainMenu,
            bindings.ListeningRow,
            bindings.ListeningSecondary
        );
    }
}
