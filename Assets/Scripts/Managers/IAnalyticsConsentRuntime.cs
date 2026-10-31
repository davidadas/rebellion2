/// <summary>
/// Applies persisted analytics consent to the active analytics runtime.
/// </summary>
internal interface IAnalyticsConsentRuntime
{
    /// <summary>
    /// Applies one normalized analytics consent choice.
    /// </summary>
    /// <param name="settings">The persisted analytics settings.</param>
    void ApplyConsent(UserAnalyticsSettings settings);
}
