using System;
using System.Collections.Generic;
using Rebellion.Game.Advisor;
using Rebellion.Util.Serialization;

/// <summary>
/// Defines one advisor animation and its associated audio cue.
/// </summary>
[PersistableObject]
public class StrategyAdvisorAnimationTheme
{
    public string Animation { get; set; }

    public string AnimationPath { get; set; }

    public int FrameCount { get; set; }

    public string Audio { get; set; }

    public string AudioPath { get; set; }

    public float DelayBeforeSeconds { get; set; }

    public bool RequiresAnnouncementsEnabled { get; set; }
}

/// <summary>
/// Defines one ordered briefing segment and its galaxy-map presentation.
/// </summary>
[PersistableObject(Name = "Segment")]
public class StrategyBriefingSegmentTheme
{
    public string Animation { get; set; }

    public int FrameCount { get; set; }

    public string Audio { get; set; }

    public float DelayBeforeSeconds { get; set; }

    public StrategyBriefingFocus Focus { get; set; }

    public StrategyBriefingMapMode MapMode { get; set; }

    public string TargetInstanceID { get; set; }

    public string Label { get; set; }
}

/// <summary>
/// Identifies the game object or galaxy region emphasized during a briefing segment.
/// </summary>
public enum StrategyBriefingFocus
{
    None,
    Galaxy,
    Target,
    PlayerHeadquarters,
    OpponentHeadquarters,
}

/// <summary>
/// Identifies the galaxy-map presentation shown while one briefing segment plays.
/// </summary>
public enum StrategyBriefingMapMode
{
    Default,
    PopularSupport,
    PlayerLoyalty,
    OpponentLoyalty,
    MilitaryControl,
    UnexploredSystems,
    IdleFleets,
    AllDefenses,
    Spotlight,
}

/// <summary>
/// Defines a faction's ordered new-game briefing using externally supplied advisor media.
/// </summary>
[PersistableObject]
public class StrategyBriefingTheme
{
    private const int _texturesPerFrame = 64;

    public string AnimationImageRoot { get; set; }

    public string AudioRoot { get; set; }

    public List<StrategyBriefingSegmentTheme> Segments { get; set; } =
        new List<StrategyBriefingSegmentTheme>();

    public StrategyBriefingSegmentTheme Skip { get; set; }

    /// <summary>
    /// Builds the media manifest required to open the briefing and handle an immediate skip.
    /// </summary>
    /// <returns>The first segment and skip-response media.</returns>
    public ContentPreloadManifest CreateOpeningPreloadManifest()
    {
        ContentPreloadManifest manifest = CreateEmptyPreloadManifest();
        HashSet<string> animations = new HashSet<string>();
        HashSet<string> audioNames = new HashSet<string>();
        if (Segments.Count > 0)
            AddPreloadAssets(Segments[0], animations, audioNames, manifest);
        AddPreloadAssets(Skip, animations, audioNames, manifest);
        return manifest;
    }

    /// <summary>
    /// Builds the media manifest required to play one briefing segment.
    /// </summary>
    /// <param name="segment">The briefing segment to load.</param>
    /// <returns>The segment's animation frames and voice clip.</returns>
    public ContentPreloadManifest CreateSegmentPreloadManifest(StrategyBriefingSegmentTheme segment)
    {
        if (segment == null)
            throw new ArgumentNullException(nameof(segment));

        ContentPreloadManifest manifest = CreateEmptyPreloadManifest();
        AddPreloadAssets(segment, new HashSet<string>(), new HashSet<string>(), manifest);
        return manifest;
    }

    /// <summary>
    /// Builds the content address for one briefing animation frame.
    /// </summary>
    /// <param name="animation">The configured animation name.</param>
    /// <param name="frameIndex">The zero-based frame index.</param>
    /// <returns>The external content address for the frame.</returns>
    public string GetFramePath(string animation, int frameIndex)
    {
        return $"{AnimationImageRoot}/{animation}/frame-{frameIndex:D3}";
    }

    /// <summary>
    /// Builds the content address for one briefing voice clip.
    /// </summary>
    /// <param name="audio">The configured audio name.</param>
    /// <returns>The external content address for the voice clip.</returns>
    public string GetAudioPath(string audio)
    {
        return $"{AudioRoot}/{audio}";
    }

    /// <summary>
    /// Creates a briefing manifest with the configured per-frame decode budget.
    /// </summary>
    /// <returns>An empty briefing preload manifest.</returns>
    private static ContentPreloadManifest CreateEmptyPreloadManifest()
    {
        return new ContentPreloadManifest { TexturesPerFrame = _texturesPerFrame };
    }

    /// <summary>
    /// Adds one segment's distinct animation directory and voice clip to a preload manifest.
    /// </summary>
    /// <param name="segment">The configured briefing segment.</param>
    /// <param name="animations">The animation names already added.</param>
    /// <param name="audioNames">The audio names already added.</param>
    /// <param name="manifest">The manifest receiving distinct content addresses.</param>
    private void AddPreloadAssets(
        StrategyBriefingSegmentTheme segment,
        ISet<string> animations,
        ISet<string> audioNames,
        ContentPreloadManifest manifest
    )
    {
        if (segment == null)
            return;

        if (!string.IsNullOrWhiteSpace(segment.Animation) && animations.Add(segment.Animation))
            manifest.TextureDirectories.Add($"{AnimationImageRoot}/{segment.Animation}");
        if (!string.IsNullOrWhiteSpace(segment.Audio) && audioNames.Add(segment.Audio))
            manifest.Audio.Add(GetAudioPath(segment.Audio));
    }
}

/// <summary>
/// Defines the presentation for a semantic advisor notification.
/// </summary>
[PersistableObject]
public class StrategyAdvisorNotificationTheme
{
    public AdvisorNotificationType NotificationType { get; set; }

    public string SubjectTypeID { get; set; }

    public AdvisorSubjectNotification SubjectNotification { get; set; }

    public AdvisorNotificationType QueueGroup { get; set; }

    public int LifetimeTicks { get; set; }

    public StrategyAdvisorAnimationTheme Droid { get; set; }

    public StrategyAdvisorAnimationTheme Protocol { get; set; }
}

/// <summary>
/// Defines strategy advisor placement, animation resources, audio resources, and notification maps.
/// </summary>
[PersistableObject]
public class StrategyAdvisorTheme
{
    public SourceRectLayout ProtocolSourceLayout { get; set; }

    public SourceRectLayout DroidSourceLayout { get; set; }

    public string AnimationImageRoot { get; set; }

    public string AudioRoot { get; set; }

    public string ProtocolIdleAnimation { get; set; }

    public string DroidIdleAnimation { get; set; }

    public float FrameIntervalSeconds { get; set; }

    public int RepeatCooldownTicks { get; set; }

    public StrategyAdvisorAnimationTheme InTransitOrderRejected { get; set; }

    public StrategyAdvisorAnimationTheme UnitUnderConstructionOrderRejected { get; set; }

    public List<StrategyAdvisorNotificationTheme> Notifications { get; set; } =
        new List<StrategyAdvisorNotificationTheme>();

    /// <summary>
    /// Gets the presentation mapped directly to a semantic advisor notification.
    /// </summary>
    /// <param name="notificationType">The general notification type.</param>
    /// <param name="subjectTypeID">The optional subject type identifier.</param>
    /// <param name="subjectNotification">The optional subject notification.</param>
    /// <returns>The matching notification theme, or <see langword="null"/>.</returns>
    public StrategyAdvisorNotificationTheme GetNotification(
        AdvisorNotificationType notificationType,
        string subjectTypeID,
        AdvisorSubjectNotification subjectNotification
    )
    {
        if (subjectNotification == AdvisorSubjectNotification.None)
        {
            return Notifications.Find(notification =>
                notification != null
                && notification.SubjectNotification == AdvisorSubjectNotification.None
                && notification.NotificationType == notificationType
            );
        }

        StrategyAdvisorNotificationTheme exactMatch = Notifications.Find(notification =>
            notification != null
            && notification.SubjectNotification == subjectNotification
            && notification.SubjectTypeID == subjectTypeID
        );
        return exactMatch
            ?? Notifications.Find(notification =>
                notification != null
                && notification.SubjectNotification == subjectNotification
                && string.IsNullOrEmpty(notification.SubjectTypeID)
            );
    }

    /// <summary>
    /// Builds the stable semantic queue key for one notification presentation.
    /// </summary>
    /// <param name="notification">The authored notification presentation.</param>
    /// <returns>A semantic key, or <see langword="null"/> for an invalid entry.</returns>
    public static string GetNotificationKey(StrategyAdvisorNotificationTheme notification)
    {
        if (notification == null)
            return null;

        if (notification.QueueGroup != AdvisorNotificationType.None)
            return $"Group:{notification.QueueGroup}";

        if (notification.SubjectNotification == AdvisorSubjectNotification.None)
        {
            return notification.NotificationType == AdvisorNotificationType.None
                ? null
                : $"Notification:{notification.NotificationType}";
        }

        return string.IsNullOrEmpty(notification.SubjectTypeID)
            ? $"Subject:{notification.SubjectNotification}"
            : $"Subject:{notification.SubjectTypeID}:{notification.SubjectNotification}";
    }

    /// <summary>
    /// Builds the resource path for an advisor animation frame.
    /// </summary>
    /// <param name="animation">The configured animation name.</param>
    /// <param name="frameIndex">The zero-based frame index.</param>
    /// <param name="droid">Whether the frame belongs to the droid advisor.</param>
    /// <returns>The animation frame resource path.</returns>
    public string GetFramePath(string animation, int frameIndex, bool droid)
    {
        string roleDirectory = droid ? "Alert" : "Report";
        return $"{AnimationImageRoot}/{roleDirectory}/{animation}/frame-{frameIndex:D3}";
    }

    /// <summary>
    /// Builds the resource path for an advisor audio clip.
    /// </summary>
    /// <param name="audio">The configured audio name.</param>
    /// <returns>The advisor audio resource path.</returns>
    public string GetAudioPath(string audio)
    {
        return $"{AudioRoot}/{audio}";
    }
}
