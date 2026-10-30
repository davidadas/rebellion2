using System.Collections.Generic;

/// <summary>
/// Accepts application analytics events after build eligibility and player consent are applied.
/// </summary>
internal interface IAnalyticsEventSink
{
    /// <summary>
    /// Gets whether the current build can ever collect analytics.
    /// </summary>
    bool IsAvailable { get; }

    /// <summary>
    /// Gets whether the player currently permits analytics collection.
    /// </summary>
    bool IsCollectionEnabled { get; }

    /// <summary>
    /// Records one event for eventual upload.
    /// </summary>
    /// <param name="eventName">The dashboard schema name.</param>
    /// <param name="parameters">The schema parameters.</param>
    void RecordEvent(string eventName, IReadOnlyDictionary<string, object> parameters);

    /// <summary>
    /// Requests upload of buffered events when the analytics runtime is ready.
    /// </summary>
    void Flush();
}
