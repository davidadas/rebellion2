using System;
using System.Collections.Generic;
using UnityEngine;

namespace TND.FrameGen
{
    public abstract class FrameGenController: MonoBehaviour
    {
        protected static FrameGenController s_instance;

        protected const uint MaxFrameMultiplier = 6;

        [SerializeField]
        internal bool enableFrameGenerationOnStart = true;

        [SerializeField]
        internal List<string> providerChain = new();

        [SerializeField, Range(2, MaxFrameMultiplier)]
        internal uint frameMultiplier = 2;

        [SerializeField]
        internal bool useDynamicMFG = false;

        [SerializeField, Range(0, 500)]
        internal uint dynamicFramerateTarget = 0;

        [SerializeField]
        internal Texture distortionField;

        [SerializeField]
        internal FrameGenDebugFlags debugFlags;

        protected FrameGenDebugFlags DebugFlags
        {
            get => (FrameGenDebugFlags)FrameGenManager.DebugFlags;
            set => FrameGenManager.SetDebugFlags((FrameGenPlugin.DebugFlags)value);
        }

        [field: NonSerialized]
        protected FrameGenName SelectedProvider { get; private set; }

        protected virtual void Awake()
        {
            if (s_instance != null)
            {
                Debug.LogWarning($"Duplicate FrameGenController instance detected, destroying this one (existing = {s_instance.name}, this = {name})");
                Destroy(this);
                return;
            }

            s_instance = this;
        }

        protected virtual void Start()
        {
            // Select the topmost provider from the fallback chain as the initial "selected" provider
            IEnumerator<IFrameGenProvider> fallbackChain = EnumerateFrameGenChain();
            while (fallbackChain.MoveNext())
            {
                IFrameGenProvider provider = fallbackChain.Current;
                if (provider != null)
                {
                    SelectedProvider = (FrameGenName)provider.UniqueId;
                    break;
                }
            }

            if (enableFrameGenerationOnStart)
            {
                UpdateFrameGenProvider();
            }
            else
            {
                FrameGenManager.SwitchFrameGenProvider(null);
            }

            FrameGenManager.SetFrameGenEnabled(true);
            FrameGenManager.SetDebugFlags((FrameGenPlugin.DebugFlags)debugFlags);
        }

        protected virtual void Update()
        {
            FrameGenManager.SetNumFramesToGenerate(frameMultiplier - 1);
            FrameGenManager.SetUseDynamicMFG(useDynamicMFG);
            FrameGenManager.SetDynamicFramerateTarget(dynamicFramerateTarget);
            FrameGenManager.SetDistortionField(distortionField);
        }

        protected virtual void OnDestroy()
        {
            if (s_instance == this)
            {
                s_instance = null;
            }
        }

        protected void SetFrameGenPaused(bool paused)
        {
            // This might seem somewhat unintuitive, but "pausing" basically means temporarily stopping FG without switching FG providers.
            // This allows for quick enable/disable behavior, without any noticeable hitches from swapchain switching.
            FrameGenManager.SetFrameGenEnabled(!paused);
        }

        protected bool GetFrameGenPaused()
        {
            return !FrameGenManager.IsFrameGenEnabled;
        }

        protected bool SwitchFrameGenProvider(FrameGenName frameGenName)
        {
            if (frameGenName == FrameGenName.None)
            {
                FrameGenManager.SwitchFrameGenProvider(null);
                return true;
            }

            SelectedProvider = frameGenName;
            return UpdateFrameGenProvider();
        }

        protected FrameGenName GetActiveFrameFrameGenProvider()
        {
            IFrameGenProvider activeProvider = FrameGenManager.ActiveProvider;
            if (activeProvider != null)
            {
                return (FrameGenName)activeProvider.UniqueId;
            }

            return FrameGenName.None;
        }

        private bool UpdateFrameGenProvider()
        {
            // Turning off FG entirely switches back to a stock swapchain
            if (SelectedProvider == FrameGenName.None)
            {
                FrameGenManager.SwitchFrameGenProvider(null);
                return true;
            }

            // Try to switch to the currently selected FG provider first
            IFrameGenProvider provider = FrameGenProviderRegistry.FindFrameGenProvider((ulong)SelectedProvider);
            if (provider != null && provider.IsSupported)
            {
                FrameGenManager.SwitchFrameGenProvider(provider);
                return true;
            }

            // Switch to the highest FG provider in the priority list that is supported on the current hardware
            IEnumerator<IFrameGenProvider> fallbackChain = EnumerateFrameGenChain();
            while (fallbackChain.MoveNext())
            {
                provider = fallbackChain.Current;
                if (provider != null && provider.IsSupported)
                {
                    FrameGenManager.SwitchFrameGenProvider(provider);
                    return true;
                }
            }

            // No supported FG provider found, turn FG off entirely and switch to stock swapchain
            FrameGenManager.SwitchFrameGenProvider(null);
            return false;
        }

        internal IEnumerator<IFrameGenProvider> EnumerateFrameGenChain()
        {
            if (providerChain == null)
                yield break;

            for (int i = 0; i < providerChain.Count; ++i)
            {
                IFrameGenProvider provider = FrameGenProviderRegistry.FindFrameGenProvider(providerChain[i]);
                if (provider != null)
                    yield return provider;
            }
        }

        protected internal static void GetSupportedFrameGenNames(List<FrameGenName> frameGenNames)
        {
            foreach (var provider in FrameGenProviderRegistry.GetFrameGenProviders())
            {
                if (provider != null && provider.IsSupported)
                {
                    frameGenNames.Add((FrameGenName)provider.UniqueId);
                }
            }
        }

        protected internal static bool IsFrameGenProviderSupported(FrameGenName frameGenName)
        {
            var provider = FrameGenProviderRegistry.FindFrameGenProvider((ulong)frameGenName);
            return provider != null && provider.IsSupported;
        }
    }
}
