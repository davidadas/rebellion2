using System;

namespace TND.FrameGen
{
    public interface IFrameGenProvider
    {
        /// <summary>
        /// Human-readable identifier for this frame gen provider plugin.
        /// This identifier should be considered opaque; code making use of it cannot make any assumptions about the contents of this string.
        /// </summary>
        string Identifier { get; }

        /// <summary>
        /// Longer name for the frame gen provider plugin that can be used to display it in a user interface.
        /// </summary>
        string DisplayName { get; }

        /// <summary>
        /// Whether this frame gen provider is supported on the current platform.
        /// Use this to determine ahead of time whether initialization of this frame gen provider will succeed.
        /// </summary>
        bool IsSupported { get; }

        /// <summary>
        /// Opaque pointer to a native object that will be passed between DLLs.
        /// This contains the actual frame gen provider implementation.
        /// </summary>
        IntPtr NativeObject { get; }

        /// <summary>
        /// Opaque unique identifier used to register the frame gen provider in native code.
        /// This is also used to validate whether registration of the native object pointer succeeded.
        /// </summary>
        ulong UniqueId { get; }
    }

    public abstract class FrameGenProviderBase: IFrameGenProvider
    {
        public string Identifier => GetType().FullName;
        public abstract string DisplayName { get; }
        public abstract bool IsSupported { get; }
        public abstract IntPtr NativeObject { get; }
        public abstract ulong UniqueId { get; }

        protected static void RegisterFrameGenProvider<TFrameGenProvider>()
            where TFrameGenProvider : IFrameGenProvider, new()
        {
            FrameGenProviderRegistry.RegisterFrameGenProvider(new TFrameGenProvider());
        }
    }
}
