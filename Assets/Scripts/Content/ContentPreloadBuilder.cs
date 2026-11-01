using System;
using System.Collections.Generic;
using System.Linq;
using Rebellion.Game.Messages;

/// <summary>
/// Derives the strategy screen's warm-up assets from the composed ships and presentation themes.
/// </summary>
internal static class ContentPreloadBuilder
{
    /// <summary>
    /// Combines current thumbnails and short interface feedback with optional authored extras.
    /// </summary>
    /// <param name="data">The effective pack data, including active overrides.</param>
    /// <param name="extras">The optional additional warm-up assets.</param>
    /// <returns>An independent preload manifest with duplicate addresses removed.</returns>
    internal static ContentPreloadManifest CreateStrategy(
        GameDataCatalog data,
        ContentPreloadManifest extras
    )
    {
        List<string> audio = new(extras?.Audio ?? Enumerable.Empty<string>());
        foreach (FactionTheme theme in data.FactionThemes)
        {
            StrategyWindowSoundTheme windows = theme.StrategyWindowSounds;
            audio.AddRange(
                new[]
                {
                    windows?.PlanetWindowOpenSoundPath,
                    windows?.PlanetWindowExpandSoundPath,
                    windows?.PlanetWindowCollapseSoundPath,
                    windows?.PlanetWindowMinimizeSoundPath,
                    theme.ConfirmDialogTheme?.ScrapRetireSoundPath,
                    theme.ConfirmDialogTheme?.StopConstructionSoundPath,
                }
            );
            StrategyAdvisorTheme advisor = theme.StrategyAdvisor;
            if (advisor == null)
                continue;
            IEnumerable<StrategyAdvisorAnimationTheme> feedback = new[]
            {
                advisor.PlanetaryGarrisonManagementEnabled,
                advisor.PlanetaryGarrisonManagementDisabled,
                advisor.ResourceProductionManagementEnabled,
                advisor.ResourceProductionManagementDisabled,
                advisor.InvalidOrderRejected,
                advisor.InTransitOrderRejected,
                advisor.UnitUnderConstructionOrderRejected,
            }.Concat(
                advisor
                    .Notifications.Where(notification =>
                        notification.NotificationType == AdvisorNotificationType.PlanetaryAssault
                    )
                    .SelectMany(notification => new[] { notification.Droid, notification.Protocol })
            );
            foreach (StrategyAdvisorAnimationTheme cue in feedback.Where(cue => cue != null))
            {
                if (!string.IsNullOrEmpty(cue.AudioPath))
                    audio.Add(cue.AudioPath);
                else
                    foreach (
                        string name in cue
                            .AudioOptions.Append(cue.Audio)
                            .Where(name => !string.IsNullOrEmpty(name))
                    )
                    {
                        if (
                            name.StartsWith("Pack/", StringComparison.Ordinal)
                            || name.StartsWith("Application/", StringComparison.Ordinal)
                        )
                            audio.Add(name);
                        else if (!string.IsNullOrEmpty(advisor.AudioRoot))
                            audio.Add(advisor.AudioRoot.TrimEnd('/') + "/" + name);
                    }
            }
        }
        return new ContentPreloadManifest
        {
            TexturesPerFrame = extras?.TexturesPerFrame ?? 4,
            Textures = Distinct(
                (extras?.Textures ?? Enumerable.Empty<string>()).Concat(
                    data.CapitalShips.Select(ship => ship.SmallDisplayImagePath)
                )
            ),
            TextureDirectories = Distinct(extras?.TextureDirectories ?? Enumerable.Empty<string>()),
            Audio = Distinct(audio),
            Models = Distinct(extras?.Models ?? Enumerable.Empty<string>()),
        };
    }

    /// <summary>
    /// Retains nonempty asset addresses in their first-use order.
    /// </summary>
    /// <param name="paths">The current references.</param>
    /// <returns>The independent de-duplicated list.</returns>
    private static List<string> Distinct(IEnumerable<string> paths) =>
        paths
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Distinct(StringComparer.Ordinal)
            .ToList();
}
