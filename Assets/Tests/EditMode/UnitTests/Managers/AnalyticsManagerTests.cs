using System.Collections.Generic;
using System.Threading.Tasks;
using NUnit.Framework;
using Unity.Services.Core;
using UnityEngine.UnityConsent;

namespace Rebellion.Tests.Managers
{
    [TestFixture]
    public sealed class AnalyticsManagerTests
    {
        [Test]
        public void ApplyConsent_UndecidedChoice_ClearsAnalyticsIntentWithoutInitializing()
        {
            ConsentState state = new ConsentState
            {
                AdsIntent = ConsentStatus.Granted,
                AnalyticsIntent = ConsentStatus.Granted,
            };
            int initializationCount = 0;
            AnalyticsManager manager = CreateManager(
                () => state,
                value => state = value,
                () => initializationCount++
            );

            manager.ApplyConsent(new UserAnalyticsSettings());

            Assert.AreEqual(ConsentStatus.Unspecified, state.AnalyticsIntent);
            Assert.AreEqual(ConsentStatus.Granted, state.AdsIntent);
            Assert.AreEqual(0, initializationCount);
        }

        [Test]
        public void ApplyConsent_GrantedChoice_GrantsAnalyticsIntentAndInitializesServices()
        {
            ConsentState state = new ConsentState();
            int initializationCount = 0;
            AnalyticsManager manager = CreateManager(
                () => state,
                value => state = value,
                () => initializationCount++
            );

            manager.ApplyConsent(
                new UserAnalyticsSettings { Consent = UserAnalyticsConsent.Granted }
            );

            Assert.AreEqual(ConsentStatus.Granted, state.AnalyticsIntent);
            Assert.AreEqual(1, initializationCount);
        }

        [Test]
        public void ApplyConsent_DeniedChoice_DeniesAnalyticsIntentWithoutInitializing()
        {
            ConsentState state = new ConsentState { AnalyticsIntent = ConsentStatus.Granted };
            int initializationCount = 0;
            AnalyticsManager manager = CreateManager(
                () => state,
                value => state = value,
                () => initializationCount++
            );

            manager.ApplyConsent(
                new UserAnalyticsSettings { Consent = UserAnalyticsConsent.Denied }
            );

            Assert.AreEqual(ConsentStatus.Denied, state.AnalyticsIntent);
            Assert.AreEqual(0, initializationCount);
        }

        [Test]
        public void ApplyConsent_GrantedChoiceWhenCollectionDisabled_DeniesIntentWithoutInitializing()
        {
            ConsentState state = new ConsentState { AnalyticsIntent = ConsentStatus.Granted };
            int initializationCount = 0;
            AnalyticsManager manager = CreateManager(
                () => state,
                value => state = value,
                () => initializationCount++,
                collectionEnabled: false
            );

            manager.ApplyConsent(
                new UserAnalyticsSettings { Consent = UserAnalyticsConsent.Granted }
            );

            Assert.AreEqual(ConsentStatus.Denied, state.AnalyticsIntent);
            Assert.AreEqual(0, initializationCount);
        }

        [Test]
        public void RecordEvent_GrantedAndInitialized_RecordsParameters()
        {
            ConsentState state = new ConsentState { AnalyticsIntent = ConsentStatus.Granted };
            string recordedName = null;
            IReadOnlyDictionary<string, object> recordedParameters = null;
            AnalyticsManager manager = new AnalyticsManager(
                () => state,
                value => state = value,
                () => ServicesInitializationState.Initialized,
                () => Task.CompletedTask,
                (name, parameters) =>
                {
                    recordedName = name;
                    recordedParameters = parameters;
                },
                () => { },
                true
            );

            ((IAnalyticsEventSink)manager).RecordEvent(
                "campaign_started",
                new Dictionary<string, object> { ["current_tick"] = 12 }
            );

            Assert.AreEqual("campaign_started", recordedName);
            Assert.AreEqual(12, recordedParameters["current_tick"]);
        }

        [Test]
        public void RecordEvent_CollectionDisabled_DoesNotRecord()
        {
            ConsentState state = new ConsentState { AnalyticsIntent = ConsentStatus.Granted };
            int recordedCount = 0;
            AnalyticsManager manager = new AnalyticsManager(
                () => state,
                value => state = value,
                () => ServicesInitializationState.Initialized,
                () => Task.CompletedTask,
                (_, _) => recordedCount++,
                () => { },
                false
            );

            ((IAnalyticsEventSink)manager).RecordEvent(
                "campaign_started",
                new Dictionary<string, object>()
            );

            Assert.AreEqual(0, recordedCount);
        }

        /// <summary>
        /// Creates a manager backed by mutable test delegates.
        /// </summary>
        /// <param name="getConsentState">Returns the current consent state.</param>
        /// <param name="setConsentState">Stores the updated consent state.</param>
        /// <param name="initializeServices">Records a services initialization request.</param>
        /// <param name="collectionEnabled">Whether analytics collection is available.</param>
        /// <returns>The configured analytics manager.</returns>
        private static AnalyticsManager CreateManager(
            System.Func<ConsentState> getConsentState,
            System.Action<ConsentState> setConsentState,
            System.Action initializeServices,
            bool collectionEnabled = true
        )
        {
            return new AnalyticsManager(
                getConsentState,
                setConsentState,
                () => ServicesInitializationState.Uninitialized,
                () =>
                {
                    initializeServices();
                    return Task.CompletedTask;
                },
                collectionEnabled
            );
        }
    }
}
