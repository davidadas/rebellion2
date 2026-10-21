using System;
using System.Collections.Generic;
using System.Diagnostics;
using Debug = UnityEngine.Debug;

namespace TND.FrameGen
{
    public static class FrameGenProviderRegistry
    {
        private static readonly Dictionary<ulong, IFrameGenProvider> FrameGenProviders = new();
        public static IReadOnlyCollection<IFrameGenProvider> GetFrameGenProviders() => FrameGenProviders.Values;

        public static void RegisterFrameGenProvider(IFrameGenProvider frameGenProvider)
        {
            if (frameGenProvider == null)
                return;

            // Ensure we don't add any duplicate providers
            Type providerType = frameGenProvider.GetType();
            foreach (IFrameGenProvider provider in FrameGenProviders.Values)
            {
                if (provider.GetType() == providerType)
                    return;
            }

            // Registering with the core native FG plugin only happens in standalone builds on supported platforms
            ulong providerId = frameGenProvider.UniqueId;
            if (FrameGenPlugin.IsAvailable)
            {
                IntPtr providerPtr = frameGenProvider.NativeObject;
                if (providerPtr == IntPtr.Zero)
                {
                    Debug.LogError($"Failed to obtain FG provider {frameGenProvider.DisplayName} from native plugin! Plugin or one of its dependencies might be missing.");
                    return;
                }

                ulong resultId = FrameGenPlugin.RegisterProvider(providerPtr);
                if (resultId == ulong.MaxValue || resultId != providerId)
                {
                    Debug.LogError($"Failed to register FG provider {frameGenProvider.DisplayName} with native plugin! Expected = {providerId}, result = {resultId}");
                    return;
                }
            }

            // Registration in C# is allowed on all platforms and in-editor, making it possible to configure things
            DebugLog($"Registering FG provider: {frameGenProvider.DisplayName}");
            FrameGenProviders[providerId] = frameGenProvider;
        }

        public static IFrameGenProvider FindFrameGenProvider(string providerIdentifier)
        {
            foreach (IFrameGenProvider provider in FrameGenProviders.Values)
            {
                if (provider.Identifier == providerIdentifier)
                {
                    return provider;
                }
            }

            return null;
        }

        public static IFrameGenProvider FindFrameGenProvider(ulong providerUniqueId)
        {
            return FrameGenProviders.GetValueOrDefault(providerUniqueId);
        }

        [Conditional("TND_DEBUG")]
        private static void DebugLog(string message)
        {
            Debug.Log(message);
        }
    }
}
