using System;
using System.Linq;
using Rebellion.Game;
using Rebellion.Game.Factions;

internal sealed class StrategyMusicController
{
    private readonly Func<GameRoot> getGame;
    private readonly Func<int, int, int> getRandomIndex;
    private readonly Func<StrategyMusicTheme> getTheme;
    private readonly Action<Func<string>> playDynamicPlaylist;
    private int neutralTracksRemaining;

    internal StrategyMusicController(
        Func<GameRoot> getGame,
        Func<StrategyMusicTheme> getTheme,
        Func<int, int, int> getRandomIndex,
        Action<Func<string>> playDynamicPlaylist
    )
    {
        this.getGame = getGame ?? throw new ArgumentNullException(nameof(getGame));
        this.getTheme = getTheme ?? throw new ArgumentNullException(nameof(getTheme));
        this.getRandomIndex =
            getRandomIndex ?? throw new ArgumentNullException(nameof(getRandomIndex));
        this.playDynamicPlaylist =
            playDynamicPlaylist ?? throw new ArgumentNullException(nameof(playDynamicPlaylist));
    }

    internal void Resume()
    {
        playDynamicPlaylist(SelectNextTrack);
    }

    private string SelectNextTrack()
    {
        StrategyMusicTheme theme =
            getTheme()
            ?? throw new InvalidOperationException(
                "The player faction has no strategy music theme."
            );
        ValidateTheme(theme);

        if (neutralTracksRemaining > 0)
        {
            neutralTracksRemaining--;
            return SelectNeutralTrack(theme);
        }

        neutralTracksRemaining = theme.NeutralTracksBetweenStrategicTracks;
        return SelectStrategicTrack(theme);
    }

    private string SelectStrategicTrack(StrategyMusicTheme theme)
    {
        int planetRatio = GetPlanetRatio(theme);
        if (planetRatio >= theme.StrongAdvantageMinimumRatio)
            return RequireTrackPath(
                theme.StrongAdvantageTrackPath,
                nameof(theme.StrongAdvantageTrackPath)
            );
        if (planetRatio >= theme.AdvantageMinimumRatio)
            return RequireTrackPath(theme.AdvantageTrackPath, nameof(theme.AdvantageTrackPath));
        if (planetRatio <= theme.DisadvantageMaximumRatio)
            return RequireTrackPath(
                theme.DisadvantageTrackPath,
                nameof(theme.DisadvantageTrackPath)
            );

        return SelectNeutralTrack(theme);
    }

    private int GetPlanetRatio(StrategyMusicTheme theme)
    {
        GameRoot game =
            getGame() ?? throw new InvalidOperationException("Strategy music has no active game.");
        Faction playerFaction = game.GetPlayerFaction();
        Faction opponentFaction = game.GetFactions().Single(faction => faction != playerFaction);
        int playerPlanetCount = playerFaction.GetOwnedColonizedPlanets().Count;
        int opponentPlanetCount = opponentFaction.GetOwnedColonizedPlanets().Count;

        return opponentPlanetCount == 0
            ? playerPlanetCount * theme.NoOpponentPlanetMultiplier
            : playerPlanetCount * theme.PlanetRatioScale / opponentPlanetCount;
    }

    private string SelectNeutralTrack(StrategyMusicTheme theme)
    {
        int trackIndex = getRandomIndex(0, theme.NeutralTrackPaths.Count);
        if (trackIndex < 0 || trackIndex >= theme.NeutralTrackPaths.Count)
        {
            throw new InvalidOperationException(
                $"Strategy music selected invalid neutral track index {trackIndex}."
            );
        }

        return RequireTrackPath(
            theme.NeutralTrackPaths[trackIndex],
            $"{nameof(theme.NeutralTrackPaths)}[{trackIndex}]"
        );
    }

    private static string RequireTrackPath(string resourcePath, string propertyName)
    {
        if (string.IsNullOrWhiteSpace(resourcePath))
        {
            throw new InvalidOperationException($"Strategy music theme is missing {propertyName}.");
        }

        return resourcePath;
    }

    private static void ValidateTheme(StrategyMusicTheme theme)
    {
        if (theme.NeutralTrackPaths == null || theme.NeutralTrackPaths.Count == 0)
            throw new InvalidOperationException("Strategy music requires neutral tracks.");
        if (theme.NeutralTracksBetweenStrategicTracks < 0)
            throw new InvalidOperationException("Strategy music cadence cannot be negative.");
        if (theme.PlanetRatioScale <= 0)
            throw new InvalidOperationException(
                "Strategy music planet ratio scale must be positive."
            );
        if (theme.NoOpponentPlanetMultiplier <= 0)
        {
            throw new InvalidOperationException(
                "Strategy music no-opponent multiplier must be positive."
            );
        }
        if (
            theme.StrongAdvantageMinimumRatio <= theme.AdvantageMinimumRatio
            || theme.AdvantageMinimumRatio <= theme.DisadvantageMaximumRatio
        )
        {
            throw new InvalidOperationException(
                "Strategy music planet ratio thresholds are invalid."
            );
        }
    }
}
