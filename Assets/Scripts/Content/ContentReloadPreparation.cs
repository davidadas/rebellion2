using System;
using System.Linq;

/// <summary>
/// Carries validated replacement content and the complete settings selection that produced it.
/// </summary>
internal sealed class ContentReloadPreparation
{
    /// <summary>
    /// Gets the validated replacement content pack.
    /// </summary>
    internal ContentPack Pack { get; }

    /// <summary>
    /// Gets every mod identifier disabled in the pending settings selection.
    /// </summary>
    internal string[] DisabledModIDs { get; }

    /// <summary>
    /// Creates a validated content-reload preparation.
    /// </summary>
    /// <param name="pack">The validated replacement content pack.</param>
    /// <param name="disabledModIDs">The complete pending disabled-mod selection.</param>
    internal ContentReloadPreparation(ContentPack pack, string[] disabledModIDs)
    {
        Pack = pack ?? throw new ArgumentNullException(nameof(pack));
        DisabledModIDs = (disabledModIDs ?? Array.Empty<string>()).ToArray();
    }
}
