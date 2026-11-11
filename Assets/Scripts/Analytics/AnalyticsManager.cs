using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UnityConsent;

/// <summary>
/// Synchronizes player consent with Unity Analytics and initializes services only after consent.
/// </summary>
public sealed class AnalyticsManager : IAnalyticsConsentRuntime, IAnalyticsEventSink
{
#if REBELLION_ANALYTICS_ENABLED && !UNITY_EDITOR
    private const bool _collectionEnabledForBuild = true;
#else
    private const bool _collectionEnabledForBuild = false;
#endif

    private readonly Func<ConsentState> _getConsentState;
    private readonly Action<ConsentState> _setConsentState;
    private readonly Func<Unity.Services.Core.ServicesInitializationState> _getServicesState;
    private readonly Func<Task> _initializeServices;
    private readonly Action<string, IReadOnlyDictionary<string, object>> _recordEvent;
    private readonly Action _flush;
    private readonly bool _collectionEnabled;
    private readonly List<PendingAnalyticsEvent> _pendingEvents = new();
    private Task _initializationTask;

    /// <summary>
    /// Gets whether analytics was explicitly enabled for this player build.
    /// </summary>
    bool IAnalyticsEventSink.IsAvailable => _collectionEnabled;

    /// <summary>
    /// Gets whether the current build and persisted player choice permit collection.
    /// </summary>
    bool IAnalyticsEventSink.IsCollectionEnabled =>
        _collectionEnabled && _getConsentState().AnalyticsIntent == ConsentStatus.Granted;

    /// <summary>
    /// Creates an analytics manager backed by the active Unity services runtime.
    /// </summary>
    public AnalyticsManager()
        : this(
            EndUserConsent.GetConsentState,
            EndUserConsent.SetConsentState,
            () => Unity.Services.Core.UnityServices.State,
            Unity.Services.Core.UnityServices.InitializeAsync,
            RecordUnityEvent,
            FlushUnityEvents,
            _collectionEnabledForBuild
        ) { }

    /// <summary>
    /// Creates an analytics manager backed by explicit platform operations.
    /// </summary>
    /// <param name="getConsentState">Returns the current platform consent state.</param>
    /// <param name="setConsentState">Writes the platform consent state.</param>
    /// <param name="getServicesState">Returns the Unity services initialization state.</param>
    /// <param name="initializeServices">Initializes Unity services.</param>
    /// <param name="recordEvent">Records one event through the configured analytics service.</param>
    /// <param name="flush">Flushes buffered analytics events.</param>
    /// <param name="collectionEnabled">Whether this build may collect analytics.</param>
    internal AnalyticsManager(
        Func<ConsentState> getConsentState,
        Action<ConsentState> setConsentState,
        Func<Unity.Services.Core.ServicesInitializationState> getServicesState,
        Func<Task> initializeServices,
        Action<string, IReadOnlyDictionary<string, object>> recordEvent,
        Action flush,
        bool collectionEnabled
    )
    {
        _getConsentState =
            getConsentState ?? throw new ArgumentNullException(nameof(getConsentState));
        _setConsentState =
            setConsentState ?? throw new ArgumentNullException(nameof(setConsentState));
        _getServicesState =
            getServicesState ?? throw new ArgumentNullException(nameof(getServicesState));
        _initializeServices =
            initializeServices ?? throw new ArgumentNullException(nameof(initializeServices));
        _recordEvent = recordEvent ?? throw new ArgumentNullException(nameof(recordEvent));
        _flush = flush ?? throw new ArgumentNullException(nameof(flush));
        _collectionEnabled = collectionEnabled;
    }

    /// <summary>
    /// Creates an analytics manager backed by explicit consent and initialization operations.
    /// </summary>
    /// <param name="getConsentState">Returns the current platform consent state.</param>
    /// <param name="setConsentState">Writes the platform consent state.</param>
    /// <param name="getServicesState">Returns the Unity services initialization state.</param>
    /// <param name="initializeServices">Initializes Unity services.</param>
    /// <param name="collectionEnabled">Whether this build may collect analytics.</param>
    internal AnalyticsManager(
        Func<ConsentState> getConsentState,
        Action<ConsentState> setConsentState,
        Func<Unity.Services.Core.ServicesInitializationState> getServicesState,
        Func<Task> initializeServices,
        bool collectionEnabled
    )
        : this(
            getConsentState,
            setConsentState,
            getServicesState,
            initializeServices,
            RecordUnityEvent,
            FlushUnityEvents,
            collectionEnabled
        ) { }

    /// <summary>
    /// Applies persisted analytics consent and starts Unity services after consent is granted.
    /// </summary>
    /// <param name="settings">The persisted analytics settings.</param>
    public void ApplyConsent(UserAnalyticsSettings settings)
    {
        settings ??= new UserAnalyticsSettings();
        settings.Normalize();

        ConsentStatus desiredStatus = _collectionEnabled
            ? GetConsentStatus(settings.Consent)
            : ConsentStatus.Denied;
        ConsentState state = _getConsentState();
        if (state.AnalyticsIntent != desiredStatus)
        {
            state.AnalyticsIntent = desiredStatus;
            _setConsentState(state);
        }

        if (desiredStatus != ConsentStatus.Granted)
            _pendingEvents.Clear();

        if (desiredStatus == ConsentStatus.Granted)
            StartServicesInitialization();
    }

    /// <summary>
    /// Records or queues one consented analytics event without allowing analytics failures to
    /// interrupt gameplay.
    /// </summary>
    /// <param name="eventName">The dashboard schema name.</param>
    /// <param name="parameters">The schema parameters.</param>
    void IAnalyticsEventSink.RecordEvent(
        string eventName,
        IReadOnlyDictionary<string, object> parameters
    )
    {
        if (
            !_collectionEnabled
            || _getConsentState().AnalyticsIntent != ConsentStatus.Granted
            || string.IsNullOrWhiteSpace(eventName)
        )
        {
            return;
        }

        PendingAnalyticsEvent pending = new PendingAnalyticsEvent(eventName, parameters);
        if (_getServicesState() != Unity.Services.Core.ServicesInitializationState.Initialized)
        {
            _pendingEvents.Add(pending);
            StartServicesInitialization();
            return;
        }

        TryRecordEvent(pending);
    }

    /// <summary>
    /// Flushes buffered analytics data when collection is active and services are initialized.
    /// </summary>
    void IAnalyticsEventSink.Flush()
    {
        if (
            !_collectionEnabled
            || _getConsentState().AnalyticsIntent != ConsentStatus.Granted
            || _getServicesState() != Unity.Services.Core.ServicesInitializationState.Initialized
        )
        {
            return;
        }

        try
        {
            _flush();
        }
        catch (Exception exception)
        {
            Debug.LogWarning($"Analytics flush failed: {exception.Message}");
        }
    }

    /// <summary>
    /// Maps the persisted application choice to the Unity consent framework.
    /// </summary>
    /// <param name="consent">The persisted application choice.</param>
    /// <returns>The equivalent Unity consent status.</returns>
    private static ConsentStatus GetConsentStatus(UserAnalyticsConsent consent)
    {
        return consent switch
        {
            UserAnalyticsConsent.Granted => ConsentStatus.Granted,
            UserAnalyticsConsent.Denied => ConsentStatus.Denied,
            _ => ConsentStatus.Unspecified,
        };
    }

    /// <summary>
    /// Starts Unity services once when they are not already initialized or initializing.
    /// </summary>
    private void StartServicesInitialization()
    {
        if (
            _getServicesState() != Unity.Services.Core.ServicesInitializationState.Uninitialized
            || _initializationTask?.IsCompleted == false
        )
        {
            return;
        }

        _initializationTask = InitializeServicesAsync();
    }

    /// <summary>
    /// Initializes Unity services without allowing a network or configuration failure to block startup.
    /// </summary>
    /// <returns>A task representing the initialization attempt.</returns>
    private async Task InitializeServicesAsync()
    {
        try
        {
            await _initializeServices();
            DrainPendingEvents();
        }
        catch (Exception exception)
        {
            _pendingEvents.Clear();
            Debug.LogWarning($"Analytics initialization failed: {exception.Message}");
        }
    }

    /// <summary>
    /// Records all events queued while Unity services initialized.
    /// </summary>
    private void DrainPendingEvents()
    {
        if (_getConsentState().AnalyticsIntent != ConsentStatus.Granted)
        {
            _pendingEvents.Clear();
            return;
        }

        PendingAnalyticsEvent[] pending = _pendingEvents.ToArray();
        _pendingEvents.Clear();
        foreach (PendingAnalyticsEvent analyticsEvent in pending)
            TryRecordEvent(analyticsEvent);
    }

    /// <summary>
    /// Records one prepared event while isolating analytics service failures from gameplay.
    /// </summary>
    /// <param name="analyticsEvent">The prepared event.</param>
    private void TryRecordEvent(PendingAnalyticsEvent analyticsEvent)
    {
        try
        {
            _recordEvent(analyticsEvent.Name, analyticsEvent.Parameters);
        }
        catch (Exception exception)
        {
            Debug.LogWarning(
                $"Analytics event '{analyticsEvent.Name}' failed: {exception.Message}"
            );
        }
    }

    /// <summary>
    /// Records one custom event through the Unity Analytics SDK.
    /// </summary>
    /// <param name="eventName">The dashboard schema name.</param>
    /// <param name="parameters">The schema parameters.</param>
    private static void RecordUnityEvent(
        string eventName,
        IReadOnlyDictionary<string, object> parameters
    )
    {
        Unity.Services.Analytics.CustomEvent analyticsEvent =
            new Unity.Services.Analytics.CustomEvent(eventName);
        foreach (KeyValuePair<string, object> parameter in parameters)
            analyticsEvent.Add(parameter.Key, parameter.Value);
        Unity.Services.Analytics.AnalyticsService.Instance.RecordEvent(analyticsEvent);
    }

    /// <summary>
    /// Flushes buffered events through the Unity Analytics SDK.
    /// </summary>
    private static void FlushUnityEvents()
    {
        Unity.Services.Analytics.AnalyticsService.Instance.Flush();
    }

    /// <summary>
    /// Owns a detached event payload while Unity services initialize.
    /// </summary>
    private sealed class PendingAnalyticsEvent
    {
        /// <summary>
        /// Gets the event schema name.
        /// </summary>
        internal string Name { get; }

        /// <summary>
        /// Gets the detached schema parameters.
        /// </summary>
        internal IReadOnlyDictionary<string, object> Parameters { get; }

        /// <summary>
        /// Creates a detached event payload.
        /// </summary>
        /// <param name="name">The event schema name.</param>
        /// <param name="parameters">The event parameters.</param>
        internal PendingAnalyticsEvent(string name, IReadOnlyDictionary<string, object> parameters)
        {
            Name = name;
            Parameters = new Dictionary<string, object>(
                parameters ?? new Dictionary<string, object>()
            );
        }
    }
}
