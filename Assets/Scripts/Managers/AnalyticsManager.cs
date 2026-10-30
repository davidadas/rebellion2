using System;
using System.Threading.Tasks;
using Unity.Services.Core;
using UnityEngine;
using UnityEngine.UnityConsent;

/// <summary>
/// Synchronizes player consent with Unity Analytics and initializes services only after consent.
/// </summary>
public sealed class AnalyticsManager : IAnalyticsConsentRuntime
{
    private readonly Func<ConsentState> _getConsentState;
    private readonly Action<ConsentState> _setConsentState;
    private readonly Func<ServicesInitializationState> _getServicesState;
    private readonly Func<Task> _initializeServices;
    private Task _initializationTask;

    /// <summary>
    /// Creates an analytics manager backed by the active Unity services runtime.
    /// </summary>
    public AnalyticsManager()
        : this(
            EndUserConsent.GetConsentState,
            EndUserConsent.SetConsentState,
            () => UnityServices.State,
            UnityServices.InitializeAsync
        ) { }

    /// <summary>
    /// Creates an analytics manager backed by explicit platform operations.
    /// </summary>
    /// <param name="getConsentState">Returns the current platform consent state.</param>
    /// <param name="setConsentState">Writes the platform consent state.</param>
    /// <param name="getServicesState">Returns the Unity services initialization state.</param>
    /// <param name="initializeServices">Initializes Unity services.</param>
    internal AnalyticsManager(
        Func<ConsentState> getConsentState,
        Action<ConsentState> setConsentState,
        Func<ServicesInitializationState> getServicesState,
        Func<Task> initializeServices
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
    }

    /// <summary>
    /// Applies persisted analytics consent and starts Unity services after consent is granted.
    /// </summary>
    /// <param name="settings">The persisted analytics settings.</param>
    public void ApplyConsent(UserAnalyticsSettings settings)
    {
        settings ??= new UserAnalyticsSettings();
        settings.Normalize();

        ConsentStatus desiredStatus = GetConsentStatus(settings.Consent);
        ConsentState state = _getConsentState();
        if (state.AnalyticsIntent != desiredStatus)
        {
            state.AnalyticsIntent = desiredStatus;
            _setConsentState(state);
        }

        if (desiredStatus == ConsentStatus.Granted)
            StartServicesInitialization();
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
            _getServicesState() != ServicesInitializationState.Uninitialized
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
        }
        catch (Exception exception)
        {
            Debug.LogWarning($"Analytics initialization failed: {exception.Message}");
        }
    }
}
