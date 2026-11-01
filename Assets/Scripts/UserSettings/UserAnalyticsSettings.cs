using System;

/// <summary>
/// Identifies the player's persisted analytics choice.
/// </summary>
public enum UserAnalyticsConsent
{
    Undecided,
    Granted,
    Denied,
}

/// <summary>
/// Stores the player's analytics consent choice.
/// </summary>
[Serializable]
public sealed class UserAnalyticsSettings
{
    public UserAnalyticsConsent Consent = UserAnalyticsConsent.Undecided;

    /// <summary>
    /// Gets whether analytics collection has been allowed.
    /// </summary>
    public bool IsEnabled => Consent == UserAnalyticsConsent.Granted;

    /// <summary>
    /// Replaces unsupported persisted values with an undecided choice.
    /// </summary>
    public void Normalize()
    {
        if (!Enum.IsDefined(typeof(UserAnalyticsConsent), Consent))
            Consent = UserAnalyticsConsent.Undecided;
    }
}
