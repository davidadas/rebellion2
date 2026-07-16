using System.Collections.Generic;
using Rebellion.Game.Messages;
using Rebellion.Util.Serialization;

/// <summary>
/// Defines one advisor animation and its associated audio cue.
/// </summary>
[PersistableObject]
public class StrategyAdvisorAnimationTheme
{
    /// <summary>
    /// Gets or sets the bitmap ID.
    /// </summary>
    public int BitmapID { get; set; }

    /// <summary>
    /// Gets or sets the frame count.
    /// </summary>
    public int FrameCount { get; set; }

    /// <summary>
    /// Gets or sets the wave ID.
    /// </summary>
    public int WaveID { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether announcements are required.
    /// </summary>
    public bool RequiresAnnouncementsEnabled { get; set; }
}

/// <summary>
/// Defines the droid and protocol animations for an advisor notification table entry.
/// </summary>
[PersistableObject]
public class StrategyAdvisorNotificationTheme
{
    /// <summary>
    /// Gets or sets the table ID.
    /// </summary>
    public int TableID { get; set; }

    /// <summary>
    /// Gets or sets the droid.
    /// </summary>
    public StrategyAdvisorAnimationTheme Droid { get; set; }

    /// <summary>
    /// Gets or sets the protocol.
    /// </summary>
    public StrategyAdvisorAnimationTheme Protocol { get; set; }
}

/// <summary>
/// Maps an advisor notification code to a table entry and display lifetime.
/// </summary>
[PersistableObject]
public class StrategyAdvisorNotificationCodeTheme
{
    /// <summary>
    /// Gets or sets the code.
    /// </summary>
    public int Code { get; set; }

    /// <summary>
    /// Gets or sets the table ID.
    /// </summary>
    public int TableID { get; set; }

    /// <summary>
    /// Gets or sets the lifetime ticks.
    /// </summary>
    public int LifetimeTicks { get; set; }
}

/// <summary>
/// Maps advisor subject notifications to report codes for one subject type.
/// </summary>
[PersistableObject]
public class StrategyAdvisorSubjectTheme
{
    /// <summary>
    /// Gets or sets the type ID.
    /// </summary>
    public string TypeID { get; set; }

    /// <summary>
    /// Gets or sets the report code.
    /// </summary>
    public int ReportCode { get; set; }

    /// <summary>
    /// Gets or sets the captured code.
    /// </summary>
    public int CapturedCode { get; set; }

    /// <summary>
    /// Gets or sets the released code.
    /// </summary>
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
    /// <summary>
    /// Gets or sets the protocol source layout.
    /// </summary>
    public SourceRectLayout ProtocolSourceLayout { get; set; }

    /// <summary>
    /// Gets or sets the droid source layout.
    /// </summary>
    public SourceRectLayout DroidSourceLayout { get; set; }

    /// <summary>
    /// Gets or sets the animation image root.
    /// </summary>
    public string AnimationImageRoot { get; set; }

    /// <summary>
    /// Gets or sets the animation file prefix.
    /// </summary>
    public string AnimationFilePrefix { get; set; }

    /// <summary>
    /// Gets or sets the audio root.
    /// </summary>
    public string AudioRoot { get; set; }

    /// <summary>
    /// Gets or sets the audio file prefix.
    /// </summary>
    public string AudioFilePrefix { get; set; }

    /// <summary>
    /// Gets or sets the protocol IDle bitmap ID.
    /// </summary>
    public int ProtocolIdleBitmapID { get; set; }

    /// <summary>
    /// Gets or sets the droid IDle bitmap ID.
    /// </summary>
    public int DroidIdleBitmapID { get; set; }

    /// <summary>
    /// Gets or sets the frame interval seconds.
    /// </summary>
    public float FrameIntervalSeconds { get; set; }

    /// <summary>
    /// Gets or sets the repeat cooldown ticks.
    /// </summary>
    public int RepeatCooldownTicks { get; set; }

    /// <summary>
    /// Gets or sets the default report code.
    /// </summary>
    public int DefaultReportCode { get; set; }

    /// <summary>
    /// Gets or sets the default captured code.
    /// </summary>
    public int DefaultCapturedCode { get; set; }

    /// <summary>
    /// Gets or sets the default released code.
    /// </summary>
    public int DefaultReleasedCode { get; set; }

    /// <summary>
    /// Gets or sets the notification codes.
    /// </summary>
    public List<StrategyAdvisorNotificationCodeTheme> NotificationCodes { get; set; } =
        new List<StrategyAdvisorNotificationCodeTheme>();

    /// <summary>
    /// Gets or sets the notifications.
    /// </summary>
    public List<StrategyAdvisorNotificationTheme> Notifications { get; set; } =
        new List<StrategyAdvisorNotificationTheme>();

    /// <summary>
    /// Gets or sets the subjects.
    /// </summary>
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
