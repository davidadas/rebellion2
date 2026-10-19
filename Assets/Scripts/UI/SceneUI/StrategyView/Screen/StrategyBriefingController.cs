using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Units;
using Rebellion.SceneGraph;
using UnityEngine;

/// <summary>
/// Sequences faction briefings and projects each segment onto the strategy map.
/// </summary>
public sealed class StrategyBriefingController
{
    private readonly Func<string, float> getAudioDuration;
    private readonly Func<string, Texture2D> getTexture;
    private readonly Func<UIContext> getUIContext;
    private readonly GalaxyMapController galaxyMapController;
    private readonly Action playPresentationSfx;
    private readonly Func<ContentPreloadManifest, Task> preloadContent;
    private readonly Action<IEnumerable<string>> preloadSfx;
    private readonly StrategyHudController strategyHudController;

    private StrategyBriefingTheme activeBriefing;
    private CancellationTokenSource briefingCancellation;
    private Action<bool> completed;
    private int segmentIndex;

    /// <summary>
    /// Creates a briefing coordinator from its direct playback and map dependencies.
    /// </summary>
    /// <param name="getUIContext">Returns presentation-safe faction identity metadata.</param>
    /// <param name="getTexture">Resolves a preloaded briefing frame.</param>
    /// <param name="getAudioDuration">Resolves the duration of a preloaded voice clip.</param>
    /// <param name="preloadContent">Makes a declared content manifest resident.</param>
    /// <param name="preloadSfx">Registers one resident voice clip for immediate playback.</param>
    /// <param name="strategyHudController">Presents advisor animation playback.</param>
    /// <param name="galaxyMapController">Presents briefing map cues.</param>
    /// <param name="playPresentationSfx">Plays the map-presentation transition sound.</param>
    public StrategyBriefingController(
        Func<UIContext> getUIContext,
        Func<string, Texture2D> getTexture,
        Func<string, float> getAudioDuration,
        Func<ContentPreloadManifest, Task> preloadContent,
        Action<IEnumerable<string>> preloadSfx,
        StrategyHudController strategyHudController,
        GalaxyMapController galaxyMapController,
        Action playPresentationSfx
    )
    {
        this.getUIContext = getUIContext ?? throw new ArgumentNullException(nameof(getUIContext));
        this.getTexture = getTexture ?? throw new ArgumentNullException(nameof(getTexture));
        this.getAudioDuration =
            getAudioDuration ?? throw new ArgumentNullException(nameof(getAudioDuration));
        this.preloadContent =
            preloadContent ?? throw new ArgumentNullException(nameof(preloadContent));
        this.preloadSfx = preloadSfx ?? throw new ArgumentNullException(nameof(preloadSfx));
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
        if (activeBriefing != null)
            throw new InvalidOperationException("A strategy briefing is already active.");

        activeBriefing = briefing;
        briefingCancellation = new CancellationTokenSource();
        completed = onCompleted;
        segmentIndex = 0;
        UIContext uiContext = getUIContext();
        string playerFactionId = uiContext?.GetPlayerFactionInstanceID();
        string opponentFactionId = uiContext
            ?.GetFactionPresentations()
            .FirstOrDefault(faction => faction.InstanceID != playerFactionId)
            ?.InstanceID;
        galaxyMapController.SetBriefingPresentation(
            new StrategyBriefingMapPresentation(
                StrategyBriefingMapMode.Default,
                "Briefing",
                null,
                null,
                playerFactionId,
                opponentFactionId
            )
        );
        BeginSegmentPlayback(activeBriefing.Segments[0]);
    }

    /// <summary>
    /// Loads the opening segment and skip response before briefing playback begins.
    /// </summary>
    /// <param name="briefing">The briefing whose opening media must be resident.</param>
    /// <returns>A task that completes when the briefing can begin immediately.</returns>
    public async Task PrepareAsync(StrategyBriefingTheme briefing)
    {
        if (briefing == null)
            throw new ArgumentNullException(nameof(briefing));
        if (activeBriefing != null)
            throw new InvalidOperationException("Cannot prepare media during briefing playback.");

        await Task.Yield();
        ContentPreloadManifest manifest = briefing.CreateOpeningPreloadManifest();
        await preloadContent(manifest);
        preloadSfx(manifest.Audio);
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
            activeBriefing.Skip == null ? null : CreatePlayback(activeBriefing.Skip, false);
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
    /// <param name="holdFinalFrame">Whether the final frame remains visible between segments.</param>
    /// <returns>The resolved animation presentation.</returns>
    private StrategyAdvisorAnimationViewData CreatePlayback(
        StrategyBriefingSegmentTheme segment,
        bool holdFinalFrame = true
    )
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
            string.IsNullOrEmpty(audioPath) ? 0f : getAudioDuration(audioPath),
            holdFinalFrame
        );
    }

    /// <summary>
    /// Advances to the next configured segment or completes the briefing.
    /// </summary>
    private void HandleSegmentCompleted()
    {
        if (activeBriefing == null)
            return;

        segmentIndex++;
        if (segmentIndex < activeBriefing.Segments.Count)
        {
            _ = TransitionToSegmentAsync(
                activeBriefing,
                activeBriefing.Segments[segmentIndex],
                segmentIndex,
                briefingCancellation.Token
            );
        }
        else
            Complete(false);
    }

    /// <summary>
    /// Loads and begins the next segment while the current briefing remains active.
    /// </summary>
    /// <param name="briefing">The briefing that owns the segment.</param>
    /// <param name="segment">The next segment to present.</param>
    /// <param name="expectedIndex">The segment position that requested the transition.</param>
    /// <param name="cancellationToken">The active briefing lifetime.</param>
    /// <returns>A task that completes after playback begins or the transition is abandoned.</returns>
    private async Task TransitionToSegmentAsync(
        StrategyBriefingTheme briefing,
        StrategyBriefingSegmentTheme segment,
        int expectedIndex,
        CancellationToken cancellationToken
    )
    {
        try
        {
            await Task.Yield();
            cancellationToken.ThrowIfCancellationRequested();
            ContentPreloadManifest manifest = briefing.CreateSegmentPreloadManifest(segment);
            await preloadContent(manifest);
            cancellationToken.ThrowIfCancellationRequested();
            preloadSfx(manifest.Audio);
            cancellationToken.ThrowIfCancellationRequested();
            if (activeBriefing != briefing || segmentIndex != expectedIndex)
                return;

            BeginSegmentPlayback(segment);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { }
        catch (Exception exception)
        {
            if (cancellationToken.IsCancellationRequested || activeBriefing != briefing)
                return;

            Debug.LogException(exception);
            Complete(false);
        }
    }

    /// <summary>
    /// Begins playback after the current segment's media is resident.
    /// </summary>
    /// <param name="segment">The resident segment to present.</param>
    private void BeginSegmentPlayback(StrategyBriefingSegmentTheme segment)
    {
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
        StrategyBriefingMapPresentation presentation = CreateMapPresentation(
            galaxyMapController.VisibleGalaxyMap,
            getUIContext()?.GetPlayerFactionInstanceID(),
            GetOpponentFactionId(),
            segment
        );
        if (presentation.DimBackground)
            playPresentationSfx();

        galaxyMapController.SetBriefingPresentation(presentation);
    }

    /// <summary>
    /// Resolves one semantic focus into a transient galaxy-map presentation.
    /// </summary>
    /// <param name="factionView">The player's fog-of-war-filtered galaxy view.</param>
    /// <param name="playerFactionId">The player faction identifier.</param>
    /// <param name="opponentFactionId">The opposing faction identifier.</param>
    /// <param name="segment">The segment whose playback is starting.</param>
    /// <returns>The resolved map presentation.</returns>
    internal static StrategyBriefingMapPresentation CreateMapPresentation(
        GalaxyMap factionView,
        string playerFactionId,
        string opponentFactionId,
        StrategyBriefingSegmentTheme segment
    )
    {
        if (segment == null)
            throw new ArgumentNullException(nameof(segment));

        string targetID = segment.TargetInstanceID;
        bool opponentHeadquartersFocus =
            segment.Focus == StrategyBriefingFocus.OpponentHeadquarters;
        if (segment.Focus == StrategyBriefingFocus.PlayerHeadquarters)
            targetID = FindVisibleHeadquarters(factionView, playerFactionId)?.InstanceID;
        else if (opponentHeadquartersFocus)
            targetID = FindVisibleHeadquarters(factionView, opponentFactionId)?.InstanceID;

        bool requiresTarget =
            segment.Focus == StrategyBriefingFocus.Target
            || segment.Focus == StrategyBriefingFocus.PlayerHeadquarters
            || segment.Focus == StrategyBriefingFocus.OpponentHeadquarters;
        if (opponentHeadquartersFocus)
            requiresTarget = !string.IsNullOrEmpty(targetID);
        if (requiresTarget && string.IsNullOrEmpty(targetID))
        {
            throw new InvalidOperationException(
                $"Briefing focus {segment.Focus} requires a target instance ID."
            );
        }

        ISceneNode target = FindVisibleNode(factionView, targetID);
        if (requiresTarget && target == null)
            throw new InvalidOperationException($"Briefing target '{targetID}' was not found.");

        Planet targetPlanet = target as Planet ?? target?.GetParentOfType<Planet>();
        PlanetSector targetSector =
            target as PlanetSector ?? target?.GetParentOfType<PlanetSector>();
        if (requiresTarget && targetSector == null)
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
            targetSector?.InstanceID,
            targetPlanet?.InstanceID,
            playerFactionId,
            opponentFactionId,
            presentsMap
        );
    }

    /// <summary>
    /// Gets the opposing faction identifier from presentation-safe faction metadata.
    /// </summary>
    /// <returns>The opposing faction identifier, or null.</returns>
    private string GetOpponentFactionId()
    {
        UIContext uiContext = getUIContext();
        string playerFactionId = uiContext?.GetPlayerFactionInstanceID();
        return uiContext
            ?.GetFactionPresentations()
            .FirstOrDefault(faction => faction.InstanceID != playerFactionId)
            ?.InstanceID;
    }

    /// <summary>
    /// Finds a headquarters planet revealed by the faction-filtered galaxy view.
    /// </summary>
    /// <param name="factionView">The visible galaxy.</param>
    /// <param name="factionId">The headquarters owner.</param>
    /// <returns>The visible headquarters planet, or null.</returns>
    private static Planet FindVisibleHeadquarters(GalaxyMap factionView, string factionId)
    {
        if (factionView == null || string.IsNullOrEmpty(factionId))
            return null;

        return factionView
            .GetChildren<Planet>(recursive: true)
            .FirstOrDefault(planet =>
                planet.IsUnexploredView == false
                && (
                    planet.IsHeadquarters && planet.OwnerInstanceID == factionId
                    || planet
                        .GetChildren<Building>()
                        .Any(building =>
                            building.BuildingType == BuildingType.Headquarters
                            && building.OwnerInstanceID == factionId
                        )
                )
            );
    }

    /// <summary>
    /// Finds a node revealed by the faction-filtered galaxy view.
    /// </summary>
    /// <param name="factionView">The visible galaxy.</param>
    /// <param name="instanceId">The requested node identifier.</param>
    /// <returns>The visible node, or null.</returns>
    private static ISceneNode FindVisibleNode(GalaxyMap factionView, string instanceId)
    {
        return factionView == null || string.IsNullOrEmpty(instanceId)
            ? null
            : factionView
                .GetChildren<ISceneNode>(recursive: true, includeDisabled: true)
                .FirstOrDefault(node => node.InstanceID == instanceId);
    }

    /// <summary>
    /// Restores normal map presentation and reports final completion once.
    /// </summary>
    /// <param name="skipped">Whether the player skipped the remaining briefing.</param>
    private void Complete(bool skipped)
    {
        strategyHudController.CancelAdvisorAnimation();
        briefingCancellation?.Cancel();
        briefingCancellation?.Dispose();
        briefingCancellation = null;
        activeBriefing = null;
        segmentIndex = 0;
        galaxyMapController.SetBriefingPresentation(null);
        Action<bool> onCompleted = completed;
        completed = null;
        onCompleted?.Invoke(skipped);
    }
}
