using System.Collections.Generic;
using Rebellion.Game.Messages;
using Rebellion.Util.Serialization;

/// <summary>
/// Defines one advisor animation and its associated audio cue.
/// </summary>
[PersistableObject]
public class StrategyAdvisorAnimationTheme
{
    public int BitmapID { get; set; }

    public int FrameCount { get; set; }

    public int WaveID { get; set; }

    public float DelayBeforeSeconds { get; set; }

    public bool RequiresAnnouncementsEnabled { get; set; }

    public StrategyBriefingFocus BriefingFocus { get; set; }

    public StrategyBriefingMapMode BriefingMapMode { get; set; }

    public string BriefingTargetInstanceID { get; set; }

    public string BriefingLabel { get; set; }
}

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

    public string AnimationFilePrefix { get; set; }

    public string AudioRoot { get; set; }

    public string AudioFilePrefix { get; set; }

    public List<StrategyAdvisorAnimationTheme> Segments { get; set; } =
        new List<StrategyAdvisorAnimationTheme>();

    public StrategyAdvisorAnimationTheme Skip { get; set; }

    /// <summary>
    /// Builds the active faction's briefing preload manifest so playback never performs disk
    /// decoding between spoken segments.
    /// </summary>
    /// <returns>The textures and audio required by the complete briefing.</returns>
    public ContentPreloadManifest CreatePreloadManifest()
    {
        ContentPreloadManifest manifest = new ContentPreloadManifest
        {
            TexturesPerFrame = _texturesPerFrame,
        };
        HashSet<int> bitmapIDs = new HashSet<int>();
        HashSet<int> waveIDs = new HashSet<int>();
        for (int i = 0; i < Segments.Count; i++)
            AddPreloadAssets(Segments[i], bitmapIDs, waveIDs, manifest);
        AddPreloadAssets(Skip, bitmapIDs, waveIDs, manifest);
        return manifest;
    }

    /// <summary>
    /// Builds the content address for one briefing animation frame.
    /// </summary>
    /// <param name="bitmapID">The source animation bitmap identifier.</param>
    /// <param name="frameIndex">The zero-based frame index.</param>
    /// <returns>The external content address for the frame.</returns>
    public string GetFramePath(int bitmapID, int frameIndex)
    {
        return $"{AnimationImageRoot}/{bitmapID}/{AnimationFilePrefix}-protocol-{bitmapID}-frame-{frameIndex:D3}";
    }

    /// <summary>
    /// Builds the content address for one briefing voice clip.
    /// </summary>
    /// <param name="waveID">The source wave resource identifier.</param>
    /// <returns>The external content address for the voice clip.</returns>
    public string GetAudioPath(int waveID)
    {
        return $"{AudioRoot}/{AudioFilePrefix}-{waveID:D4}";
    }

    /// <summary>
    /// Adds one segment's distinct animation directory and voice clip to a preload manifest.
    /// </summary>
    /// <param name="segment">The configured briefing segment.</param>
    /// <param name="bitmapIDs">The bitmap identifiers already added.</param>
    /// <param name="waveIDs">The wave identifiers already added.</param>
    /// <param name="manifest">The manifest receiving distinct content addresses.</param>
    private void AddPreloadAssets(
        StrategyAdvisorAnimationTheme segment,
        ISet<int> bitmapIDs,
        ISet<int> waveIDs,
        ContentPreloadManifest manifest
    )
    {
        if (segment == null)
            return;

        if (segment.BitmapID > 0 && bitmapIDs.Add(segment.BitmapID))
            manifest.TextureDirectories.Add($"{AnimationImageRoot}/{segment.BitmapID}");
        if (segment.WaveID > 0 && waveIDs.Add(segment.WaveID))
            manifest.Audio.Add(GetAudioPath(segment.WaveID));
    }
}

/// <summary>
/// Defines the droid and protocol animations for an advisor notification table entry.
/// </summary>
[PersistableObject]
public class StrategyAdvisorNotificationTheme
{
    public int TableID { get; set; }

    public StrategyAdvisorAnimationTheme Droid { get; set; }

    public StrategyAdvisorAnimationTheme Protocol { get; set; }
}

/// <summary>
/// Maps an advisor notification code to a table entry and display lifetime.
/// </summary>
[PersistableObject]
public class StrategyAdvisorNotificationCodeTheme
{
    public int Code { get; set; }

    public int TableID { get; set; }

    public int LifetimeTicks { get; set; }
}

/// <summary>
/// Maps advisor subject notifications to report codes for one subject type.
/// </summary>
[PersistableObject]
public class StrategyAdvisorSubjectTheme
{
    public string TypeID { get; set; }

    public int ReportCode { get; set; }

    public int CapturedCode { get; set; }

    public int ReleasedCode { get; set; }

    /// <summary>
    /// Gets the configured report code for a subject notification.
    /// </summary>
    /// <param name="notification">The subject notification.</param>
    /// <returns>The configured report code, or zero when unsupported.</returns>
    public int GetCode(AdvisorSubjectNotification notification)
    {
        return notification switch
        {
            AdvisorSubjectNotification.Report => ReportCode,
            AdvisorSubjectNotification.Captured => CapturedCode,
            AdvisorSubjectNotification.Released => ReleasedCode,
            _ => 0,
        };
    }
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

    public string AnimationFilePrefix { get; set; }

    public string AudioRoot { get; set; }

    public string AudioFilePrefix { get; set; }

    public int ProtocolIdleBitmapID { get; set; }

    public int DroidIdleBitmapID { get; set; }

    public float FrameIntervalSeconds { get; set; }

    public int RepeatCooldownTicks { get; set; }

    public int DefaultReportCode { get; set; }

    public int DefaultCapturedCode { get; set; }

    public int DefaultReleasedCode { get; set; }

    public List<StrategyAdvisorNotificationCodeTheme> NotificationCodes { get; set; } =
        new List<StrategyAdvisorNotificationCodeTheme>();

    public List<StrategyAdvisorNotificationTheme> Notifications { get; set; } =
        new List<StrategyAdvisorNotificationTheme>();

    public List<StrategyAdvisorSubjectTheme> Subjects { get; set; } =
        new List<StrategyAdvisorSubjectTheme>();

    /// <summary>
    /// Gets the notification theme mapped to an advisor report code.
    /// </summary>
    /// <param name="code">The advisor report code.</param>
    /// <param name="lifetimeTicks">Receives the configured display lifetime.</param>
    /// <returns>The matching notification theme, or <see langword="null"/>.</returns>
    public StrategyAdvisorNotificationTheme GetNotification(int code, out int lifetimeTicks)
    {
        lifetimeTicks = 0;
        int tableID = 0;
        foreach (StrategyAdvisorNotificationCodeTheme notificationCode in NotificationCodes)
        {
            if (notificationCode?.Code != code)
                continue;

            tableID = notificationCode.TableID;
            lifetimeTicks = notificationCode.LifetimeTicks;
            break;
        }

        foreach (StrategyAdvisorNotificationTheme notification in Notifications)
        {
            if (notification?.TableID == tableID)
                return notification;
        }

        return null;
    }

    /// <summary>
    /// Gets the advisor notification code for a subject type and notification.
    /// </summary>
    /// <param name="typeID">The subject type identifier.</param>
    /// <param name="notification">The subject notification.</param>
    /// <returns>The subject-specific code or the configured default code.</returns>
    public int GetSubjectNotificationCode(string typeID, AdvisorSubjectNotification notification)
    {
        foreach (StrategyAdvisorSubjectTheme subject in Subjects)
        {
            if (subject?.TypeID == typeID)
                return subject.GetCode(notification);
        }

        return notification switch
        {
            AdvisorSubjectNotification.Report => DefaultReportCode,
            AdvisorSubjectNotification.Captured => DefaultCapturedCode,
            AdvisorSubjectNotification.Released => DefaultReleasedCode,
            _ => 0,
        };
    }

    /// <summary>
    /// Builds the resource path for an advisor animation frame.
    /// </summary>
    /// <param name="bitmapID">The animation bitmap identifier.</param>
    /// <param name="frameIndex">The zero-based frame index.</param>
    /// <param name="droid">Whether the frame belongs to the droid advisor.</param>
    /// <returns>The animation frame resource path.</returns>
    public string GetFramePath(int bitmapID, int frameIndex, bool droid)
    {
        string roleDirectory = droid ? "Droid" : "Protocol";
        string roleName = droid ? "droid" : "protocol";
        return $"{AnimationImageRoot}/{roleDirectory}/{bitmapID}/{AnimationFilePrefix}-{roleName}-{bitmapID}-frame-{frameIndex:D3}";
    }

    /// <summary>
    /// Builds the resource path for an advisor audio clip.
    /// </summary>
    /// <param name="waveID">The audio wave identifier.</param>
    /// <returns>The advisor audio resource path.</returns>
    public string GetAudioPath(int waveID)
    {
        return $"{AudioRoot}/{AudioFilePrefix}-{waveID:D4}";
    }
}
