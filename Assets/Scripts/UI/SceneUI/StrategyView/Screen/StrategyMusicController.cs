using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Rebellion.Game.Galaxy;
using Rebellion.Game.UIState;

internal sealed class StrategyMusicController
{
    private const string _neutralTracksRemainingStateID = "MusicNeutralTracksRemaining";
    private readonly Func<string> getPlayerFactionId;
    private readonly Func<int, int, int> getRandomIndex;
    private readonly Func<StrategyMusicTheme> getTheme;
    private readonly Func<UIStateSection> getUIState;
    private readonly Func<IReadOnlyList<Planet>> getVisiblePlanets;
    private readonly Action<Func<string>> playDynamicPlaylist;
    private readonly Action stopMusic;

    /// <summary>
    /// Initializes a new instance of the StrategyMusicController class.
    /// </summary>
    /// <param name="getVisiblePlanets">Returns planets from the current faction-visible snapshot.</param>
    /// <param name="getPlayerFactionId">Returns the viewing player's faction identifier.</param>
    /// <param name="getTheme">The get theme.</param>
    /// <param name="getUIState">Returns the active player's saved strategy interface state.</param>
    /// <param name="getRandomIndex">The get random index.</param>
    /// <param name="playDynamicPlaylist">The play dynamic playlist.</param>
    /// <param name="stopMusic">The stop music.</param>
    internal StrategyMusicController(
        Func<IReadOnlyList<Planet>> getVisiblePlanets,
        Func<string> getPlayerFactionId,
        Func<StrategyMusicTheme> getTheme,
        Func<UIStateSection> getUIState,
        Func<int, int, int> getRandomIndex,
        Action<Func<string>> playDynamicPlaylist,
        Action stopMusic
    )
    {
        this.getVisiblePlanets =
            getVisiblePlanets ?? throw new ArgumentNullException(nameof(getVisiblePlanets));
        this.getPlayerFactionId =
            getPlayerFactionId ?? throw new ArgumentNullException(nameof(getPlayerFactionId));
        this.getTheme = getTheme ?? throw new ArgumentNullException(nameof(getTheme));
        this.getUIState = getUIState ?? throw new ArgumentNullException(nameof(getUIState));
        this.getRandomIndex =
            getRandomIndex ?? throw new ArgumentNullException(nameof(getRandomIndex));
        this.playDynamicPlaylist =
            playDynamicPlaylist ?? throw new ArgumentNullException(nameof(playDynamicPlaylist));
        this.stopMusic = stopMusic ?? throw new ArgumentNullException(nameof(stopMusic));
    }

    /// <summary>
    /// Resumes dynamic strategy music selection.
    /// </summary>
    internal void Resume()
    {
        playDynamicPlaylist(SelectNextTrack);
    }

    /// <summary>
    /// Stops strategy music while preserving the saved rotation position.
    /// </summary>
    internal void Reset()
    {
        stopMusic();
    }

    /// <summary>
    /// Selects next track.
    /// </summary>
    /// <returns>The selected next track.</returns>
    private string SelectNextTrack()
    {
        StrategyMusicTheme theme =
            getTheme()
            ?? throw new InvalidOperationException(
                "The player faction has no strategy music theme."
            );
        ValidateTheme(theme);
        UIStateSection state =
            getUIState()
            ?? throw new InvalidOperationException("Strategy music has no saved interface state.");
        int neutralTracksRemaining =
            state.Values.TryGetValue(_neutralTracksRemainingStateID, out string value)
            && int.TryParse(
                value,
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out int saved
            )
                ? Math.Clamp(saved, 0, theme.NeutralTracksBetweenStrategicTracks)
                : 0;
        string track =
            neutralTracksRemaining > 0 ? SelectNeutralTrack(theme) : SelectStrategicTrack(theme);
        state.Values[_neutralTracksRemainingStateID] = (
            neutralTracksRemaining > 0
                ? neutralTracksRemaining - 1
                : theme.NeutralTracksBetweenStrategicTracks
        ).ToString(CultureInfo.InvariantCulture);
        return track;
    }

    /// <summary>
    /// Selects strategic track.
    /// </summary>
    /// <param name="theme">The theme.</param>
    /// <returns>The selected strategic track.</returns>
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

    /// <summary>
    /// Gets planet ratio.
    /// </summary>
    /// <param name="theme">The theme.</param>
    /// <returns>The requested planet ratio.</returns>
    private int GetPlanetRatio(StrategyMusicTheme theme)
    {
        IReadOnlyList<Planet> planets =
            getVisiblePlanets()
            ?? throw new InvalidOperationException(
                "Strategy music has no faction-visible galaxy snapshot."
            );
        string playerFactionId = getPlayerFactionId();
        int playerPlanetCount = planets.Count(planet =>
            planet?.IsColonized == true && planet.OwnerInstanceID == playerFactionId
        );
        int opponentPlanetCount = planets.Count(planet =>
            planet?.IsColonized == true
            && !string.IsNullOrEmpty(planet.OwnerInstanceID)
            && planet.OwnerInstanceID != playerFactionId
        );

        return opponentPlanetCount == 0
            ? playerPlanetCount * theme.NoOpponentPlanetMultiplier
            : playerPlanetCount * theme.PlanetRatioScale / opponentPlanetCount;
    }

    /// <summary>
    /// Selects neutral track.
    /// </summary>
    /// <param name="theme">The theme.</param>
    /// <returns>The selected neutral track.</returns>
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

    /// <summary>
    /// Requires track path.
    /// </summary>
    /// <param name="resourcePath">The resource path.</param>
    /// <param name="propertyName">The property name.</param>
    /// <returns>The result of require track path.</returns>
    private static string RequireTrackPath(string resourcePath, string propertyName)
    {
        if (string.IsNullOrWhiteSpace(resourcePath))
        {
            throw new InvalidOperationException($"Strategy music theme is missing {propertyName}.");
        }

        return resourcePath;
    }

    /// <summary>
    /// Validates theme.
    /// </summary>
    /// <param name="theme">The theme.</param>
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
