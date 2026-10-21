using System;
using System.Collections.Generic;
using UnityEngine;

namespace TND.FrameGen
{
    [DisallowMultipleComponent]
    public class TNDFrameGeneration: FrameGenController
    {
        /// <summary>
        /// Returns the currently active `TNDFrameGeneration` script.
        /// Note that this may be null if there is no `TNDFrameGeneration` script in the active scene.
        /// </summary>
        public static TNDFrameGeneration Instance => s_instance as TNDFrameGeneration;

        /// <summary>
        /// Enable frame generation with the currently selected frame generation method.
        /// This may trigger a brief resolution switch and window repositioning.
        /// </summary>
        public void EnableFrameGeneration()
        {
            SwitchFrameGenProvider(SelectedProvider);
        }

        /// <summary>
        /// Disable frame generation and switch to a standard backbuffer setup.
        /// This may trigger a brief resolution switch and window repositioning.
        /// </summary>
        public void DisableFrameGeneration()
        {
            SwitchFrameGenProvider(FrameGenName.None);
        }

        /// <summary>
        /// Returns whether frame generation is currently enabled.
        /// </summary>
        public bool IsFrameGenerationEnabled()
        {
            return GetActiveFrameFrameGenProvider() != FrameGenName.None;
        }

        /// <summary>
        /// Sets the active frame generation method.
        /// If the chosen method is not supported, this will fall back to the next method on the priority list.
        /// This may trigger a brief resolution switch and window repositioning.
        /// </summary>
        public bool SetFrameGeneration(FrameGenName frameGenName)
        {
            return SwitchFrameGenProvider(frameGenName);
        }

        /// <summary>
        /// Gets the currently selected frame generation method.
        /// This may differ from the currently active frame generation method; if the selected method is not supported then a different frame generation method will be activated.
        /// </summary>
        public FrameGenName GetSelectedFrameGeneration()
        {
            return SelectedProvider;
        }

        /// <summary>
        /// Gets the currently active frame generation method, if any.
        /// This may differ from the currently selected frame generation method; if the selected method is not supported then a different frame generation method will be activated.
        /// </summary>
        public FrameGenName GetActiveFrameGeneration()
        {
            return GetActiveFrameFrameGenProvider();
        }

        /// <summary>
        /// Pause the active frame generation from producing interpolated frames.
        /// This can be used to temporarily stop frame generation, without triggering a resolution switch and window repositioning.
        /// Use this for example on loading screens or when displaying menus or full-screen UI.
        /// </summary>
        public void PauseFrameGeneration()
        {
            SetFrameGenPaused(true);
        }

        /// <summary>
        /// Unpause the active frame generation, allowing it to produce interpolated frames again.
        /// This does not trigger any resolution switch or window repositioning.
        /// </summary>
        public void UnpauseFrameGeneration()
        {
            SetFrameGenPaused(false);
        }

        /// <summary>
        /// Returns whether the active frame generation is currently paused.
        /// </summary>
        public bool IsFrameGenerationPaused()
        {
            return GetFrameGenPaused();
        }

        /// <summary>
        /// Gets whether the currently active frame generation method supports dynamic multi-frame generation.
        /// Dynamic MFG automatically adjusts the frame multiplier based on a target framerate value.
        /// </summary>
        public bool IsDynamicMultiFrameGenerationSupported()
        {
            return FrameGenManager.SupportsDynamicMFG;
        }

        /// <summary>
        /// Gets the maximum frame multiplier supported by the currently active frame generation method.
        /// For frame generation methods or hardware platforms that don't support multi-frame generation, this will always return 2.
        /// </summary>
        public uint GetMaxFrameMultiplier()
        {
            return FrameGenManager.MaxInterpolatedFrames + 1;
        }

        /// <summary>
        /// Returns the currently active frame generation method's internal version for the technique that's being used, in the form of (major, minor, patch).
        /// What this version number means exactly is fairly arbitrary and differs per frame generation method.
        /// It can be used to identify at run-time which exact technique is in use, for example to distinguish between FSR 3 and FSR 4.
        /// </summary>
        public Vector3Int GetTechVersion()
        {
            var version = FrameGenManager.TechVersion;
            return new Vector3Int((int)version.major, (int)version.minor, (int)version.patch);
        }

        /// <summary>
        /// Sets the frame multiplier for multi-frame generation.
        /// For frame generation methods or hardware platforms that don't support multi-frame generation, this will default to 2.
        /// </summary>
        public void SetFrameMultiplier(uint multiplier)
        {
            frameMultiplier = Math.Clamp(multiplier, 2, MaxFrameMultiplier);
        }

        /// <summary>
        /// Gets the currently set frame multiplier for multi-frame generation.
        /// </summary>
        public uint GetFrameMultiplier()
        {
            return frameMultiplier;
        }

        /// <summary>
        /// Enables or disables dynamic multi-frame generation. Dynamic MFG automatically adjusts the frame multiplier based on a target framerate value.
        /// When enabled, the frame multiplier setting is ignored and the dynamic framerate target setting will be used instead.
        /// </summary>
        public void SetDynamicMultiFrameGenerationEnabled(bool enable)
        {
            useDynamicMFG = enable;
        }

        /// <summary>
        /// Gets whether dynamic multi-frame generation is currently enabled.
        /// </summary>
        public bool GetDynamicMultiFrameGenerationEnabled()
        {
            return useDynamicMFG;
        }

        /// <summary>
        /// Sets the framerate target for dynamic multi-frame generation. Setting this to 0 will use the current monitor's refresh rate as the target.
        /// If dynamic multi-frame generation is not supported or disabled, this setting will do nothing.
        /// </summary>
        public void SetDynamicFramerateTarget(uint targetFramerate)
        {
            dynamicFramerateTarget = targetFramerate;
        }

        /// <summary>
        /// Gets the framerate target for dynamic multi-frame generation.
        /// If set to 0, the current monitor's refresh rate will be used as the target.
        /// </summary>
        public uint GetDynamicFramerateTarget()
        {
            return dynamicFramerateTarget;
        }

        /// <summary>
        /// Sets a texture to be used as a distortion field input for frame interpolation.
        /// Use this to indicate how pixels are being moved in screen-space by post-processing distortion effects, such as lens distortion.
        /// If no distortion effects are in use, then this texture can remain empty.
        /// </summary>
        public void SetDistortionField(Texture distortionFieldTexture)
        {
            distortionField = distortionFieldTexture;
        }

        /// <summary>
        /// Gets the current distortion field texture.
        /// </summary>
        public Texture GetDistortionField()
        {
            return distortionField;
        }

        /// <summary>
        /// Sets debug display features on the active frame generation.
        /// </summary>
        public void SetFrameGenerationDebugFlags(FrameGenDebugFlags flags)
        {
            DebugFlags = flags;
        }

        /// <summary>
        /// Gets the debug display features on the active frame generation.
        /// </summary>
        public FrameGenDebugFlags GetFrameGenerationDebugFlags()
        {
            return DebugFlags;
        }

        /// <summary>
        /// Use this method to retrieve a list of all frame generation methods supported on the current hardware.
        /// </summary>
        public static List<FrameGenName> GetSupportedFrameGeneration()
        {
            List<FrameGenName> result = new();
            GetSupportedFrameGeneration(result);
            return result;
        }

        /// <summary>
        /// Use this method to retrieve a list of all frame generation methods supported on the current hardware.
        /// This method takes a pre-allocated list, so you can prevent unnecessary GC allocations.
        /// </summary>
        public static void GetSupportedFrameGeneration(List<FrameGenName> supported)
        {
            GetSupportedFrameGenNames(supported);
        }

        /// <summary>
        /// Returns if the given frame generation method is available and supported on the current hardware.
        /// </summary>
        public static bool IsSupportedFrameGeneration(FrameGenName frameGenName)
        {
            return IsFrameGenProviderSupported(frameGenName);
        }
    }
}
