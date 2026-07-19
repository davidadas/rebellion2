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

    public bool RequiresAnnouncementsEnabled { get; set; }
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
        return $"{AnimationImageRoot}/{roleDirectory}/{bitmapID}/{AnimationFilePrefix}_{roleName}_{bitmapID}_frame_{frameIndex:D3}";
    }

    /// <summary>
    /// Builds the resource path for an advisor audio clip.
    /// </summary>
    /// <param name="waveID">The audio wave identifier.</param>
    /// <returns>The advisor audio resource path.</returns>
    public string GetAudioPath(int waveID)
    {
        return $"{AudioRoot}/{AudioFilePrefix}_{waveID:D4}";
    }
}
