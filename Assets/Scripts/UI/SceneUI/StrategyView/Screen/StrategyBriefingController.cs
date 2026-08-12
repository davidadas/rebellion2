using System;
using System.Linq;
using Rebellion.Game;
using Rebellion.Game.Galaxy;
using Rebellion.SceneGraph;
using UnityEngine;
using Faction = Rebellion.Game.Factions.Faction;

/// <summary>
/// Sequences faction briefings and projects each segment onto the strategy map.
/// </summary>
public sealed class StrategyBriefingController
{
    private readonly Func<string, float> getAudioDuration;
    private readonly Func<string, Texture2D> getTexture;
    private readonly GameRoot game;
    private readonly GalaxyMapController galaxyMapController;
    private readonly Action playPresentationSfx;
    private readonly StrategyHudController strategyHudController;

    private StrategyBriefingTheme activeBriefing;
    private Action<bool> completed;
    private int segmentIndex;

    /// <summary>
    /// Creates a briefing coordinator from its direct playback and map dependencies.
    /// </summary>
    /// <param name="game">The active game.</param>
    /// <param name="getTexture">Resolves a preloaded briefing frame.</param>
    /// <param name="getAudioDuration">Resolves the duration of a preloaded voice clip.</param>
    /// <param name="strategyHudController">Presents advisor animation playback.</param>
    /// <param name="galaxyMapController">Presents briefing map cues.</param>
    /// <param name="playPresentationSfx">Plays the map-presentation transition sound.</param>
    public StrategyBriefingController(
        GameRoot game,
        Func<string, Texture2D> getTexture,
        Func<string, float> getAudioDuration,
        StrategyHudController strategyHudController,
        GalaxyMapController galaxyMapController,
        Action playPresentationSfx
    )
    {
        this.game = game ?? throw new ArgumentNullException(nameof(game));
        this.getTexture = getTexture ?? throw new ArgumentNullException(nameof(getTexture));
        this.getAudioDuration =
            getAudioDuration ?? throw new ArgumentNullException(nameof(getAudioDuration));
        this.strategyHudController =
            strategyHudController ?? throw new ArgumentNullException(nameof(strategyHudController));
        this.galaxyMapController =
            galaxyMapController ?? throw new ArgumentNullException(nameof(galaxyMapController));
        this.playPresentationSfx =
            playPresentationSfx ?? throw new ArgumentNullException(nameof(playPresentationSfx));
    }

    /// <summary>
    /// Plays every configured segment in order.
    /// </summary>
    /// <param name="briefing">The active faction briefing.</param>
    /// <param name="onCompleted">
    /// Invoked with whether the briefing was skipped after playback relinquishes control.
    /// </param>
    public void Play(StrategyBriefingTheme briefing, Action<bool> onCompleted)
    {
        if (briefing == null || briefing.Segments.Count == 0)
        {
            onCompleted?.Invoke(false);
            return;
        }

        activeBriefing = briefing;
        completed = onCompleted;
        segmentIndex = 0;
        Faction playerFaction = game.GetPlayerFaction();
        Faction opponentFaction = game.GetFactions()
            .FirstOrDefault(faction => faction != playerFaction);
        galaxyMapController.SetBriefingPresentation(
            new StrategyBriefingMapPresentation(
                StrategyBriefingMapMode.Default,
                "Briefing",
                null,
                null,
                playerFaction?.InstanceID,
                opponentFaction?.InstanceID
            )
        );
        PlayCurrentSegment();
    }

    /// <summary>
    /// Cancels the remaining segments, relinquishes tutorial control, and plays the configured
    /// skip response when available.
    /// </summary>
    public void Skip()
    {
        if (activeBriefing == null)
            return;

        StrategyAdvisorAnimationViewData skipPlayback =
            activeBriefing.Skip == null ? null : CreatePlayback(activeBriefing.Skip);
        strategyHudController.CancelAdvisorAnimation();
        Complete(true);
        if (skipPlayback != null)
            strategyHudController.ReplaceAdvisorAnimation(skipPlayback, null, null);
    }

    /// <summary>
    /// Pauses the active advisor animation without discarding briefing state.
    /// </summary>
    public void Pause()
    {
        strategyHudController.PauseAdvisorAnimation();
    }

    /// <summary>
    /// Resumes the active advisor animation from its paused position.
    /// </summary>
    public void Resume()
    {
        strategyHudController.ResumeAdvisorAnimation();
    }

    /// <summary>
    /// Resolves one segment into resident advisor playback data.
    /// </summary>
    /// <param name="segment">The configured briefing segment.</param>
    /// <returns>The resolved animation presentation.</returns>
    private StrategyAdvisorAnimationViewData CreatePlayback(StrategyBriefingSegmentTheme segment)
    {
        if (segment == null || segment.FrameCount <= 0)
            throw new InvalidOperationException("Briefing contains an empty animation segment.");

        Texture2D[] frames = new Texture2D[segment.FrameCount];
        for (int frameIndex = 0; frameIndex < segment.FrameCount; frameIndex++)
        {
            string path = activeBriefing.GetFramePath(segment.Animation, frameIndex);
            frames[frameIndex] = getTexture(path);
            if (frames[frameIndex] == null)
                throw new InvalidOperationException($"Briefing frame is missing: {path}");
        }

        string audioPath = string.IsNullOrWhiteSpace(segment.Audio)
            ? null
            : activeBriefing.GetAudioPath(segment.Audio);
        return new StrategyAdvisorAnimationViewData(
            frames,
            false,
            audioPath,
            segment.DelayBeforeSeconds,
            string.IsNullOrEmpty(audioPath) ? 0f : getAudioDuration(audioPath)
        );
    }

    /// <summary>
    /// Advances to the next configured segment or completes the briefing.
    /// </summary>
    private void HandleSegmentCompleted()
    {
        segmentIndex++;
        if (segmentIndex < activeBriefing.Segments.Count)
            PlayCurrentSegment();
        else
            Complete(false);
    }

    /// <summary>
    /// Resolves and begins the current segment.
    /// </summary>
    private void PlayCurrentSegment()
    {
        StrategyBriefingSegmentTheme segment = activeBriefing.Segments[segmentIndex];
        strategyHudController.ReplaceAdvisorAnimation(
            CreatePlayback(segment),
            () => PresentSegment(segment),
            HandleSegmentCompleted
        );
    }

    /// <summary>
    /// Resolves one semantic focus into a transient galaxy-map presentation.
    /// </summary>
    /// <param name="segment">The segment whose playback is starting.</param>
    private void PresentSegment(StrategyBriefingSegmentTheme segment)
    {
        StrategyBriefingMapPresentation presentation = CreateMapPresentation(game, segment);
        if (presentation.DimBackground)
            playPresentationSfx();

        galaxyMapController.SetBriefingPresentation(presentation);
    }

    /// <summary>
    /// Resolves one semantic focus into a transient galaxy-map presentation.
    /// </summary>
    /// <param name="game">The active game.</param>
    /// <param name="segment">The segment whose playback is starting.</param>
    /// <returns>The resolved map presentation.</returns>
    internal static StrategyBriefingMapPresentation CreateMapPresentation(
        GameRoot game,
        StrategyBriefingSegmentTheme segment
    )
    {
        if (game == null)
            throw new ArgumentNullException(nameof(game));
        if (segment == null)
            throw new ArgumentNullException(nameof(segment));

        string targetID = segment.TargetInstanceID;
        Faction playerFaction = game.GetPlayerFaction();
        Faction opponentFaction = game.GetFactions().Single(faction => faction != playerFaction);
        if (segment.Focus == StrategyBriefingFocus.PlayerHeadquarters)
            targetID = playerFaction?.HQInstanceID;
        else if (segment.Focus == StrategyBriefingFocus.OpponentHeadquarters)
            targetID = opponentFaction?.HQInstanceID;

        bool requiresTarget =
            segment.Focus == StrategyBriefingFocus.Target
            || segment.Focus == StrategyBriefingFocus.PlayerHeadquarters
            || segment.Focus == StrategyBriefingFocus.OpponentHeadquarters;
        if (requiresTarget && string.IsNullOrEmpty(targetID))
        {
            throw new InvalidOperationException(
                $"Briefing focus {segment.Focus} requires a target instance ID."
            );
        }

        ISceneNode target = game.GetSceneNodeByInstanceID<ISceneNode>(targetID);
        if (requiresTarget && target == null)
            throw new InvalidOperationException($"Briefing target '{targetID}' was not found.");

        Planet targetPlanet = target as Planet ?? target?.GetParentOfType<Planet>();
        PlanetSystem targetSystem =
            target as PlanetSystem ?? target?.GetParentOfType<PlanetSystem>();
        if (requiresTarget && targetSystem == null)
        {
            throw new InvalidOperationException(
                $"Briefing target '{targetID}' cannot be presented on the galaxy map."
            );
        }

        string label = segment.Label;
        if (string.IsNullOrEmpty(label) && segment.Focus != StrategyBriefingFocus.None)
            label = target?.DisplayName;
        StrategyBriefingMapMode mapMode = segment.MapMode;
        if (mapMode == StrategyBriefingMapMode.Default && targetPlanet != null)
            mapMode = StrategyBriefingMapMode.Spotlight;

        bool presentsMap =
            mapMode != StrategyBriefingMapMode.Default
            || segment.Focus != StrategyBriefingFocus.None;
        return new StrategyBriefingMapPresentation(
            mapMode,
            label,
            targetSystem?.InstanceID,
            targetPlanet?.InstanceID,
            playerFaction?.InstanceID,
            opponentFaction?.InstanceID,
            presentsMap
        );
    }

    /// <summary>
    /// Restores normal map presentation and reports final completion once.
    /// </summary>
    /// <param name="skipped">Whether the player skipped the remaining briefing.</param>
    private void Complete(bool skipped)
    {
        activeBriefing = null;
        segmentIndex = 0;
        galaxyMapController.SetBriefingPresentation(null);
        Action<bool> onCompleted = completed;
        completed = null;
        onCompleted?.Invoke(skipped);
    }
}
