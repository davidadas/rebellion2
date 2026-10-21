using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.LowLevel;

namespace TND.FrameGen
{
    public static class FrameGenUtils
    {
        // TODO: for XR support, we typically need to check camera.xr.isHDRDisplayOutputActive instead
        public static bool HDRDisplayActive => SystemInfo.hdrDisplaySupportFlags.HasFlag(HDRDisplaySupportFlags.Supported) && HDROutputSettings.main.active;

        public static bool YFlipRequired => SystemInfo.graphicsUVStartsAtTop && FrameGenPlugin.RequiresYFlip;

        public static void DestroyObject(UnityEngine.Object obj)
        {
            if (obj == null)
                return;

#if UNITY_EDITOR
            if (Application.isPlaying && !UnityEditor.EditorApplication.isPaused)
                UnityEngine.Object.Destroy(obj);
            else
                UnityEngine.Object.DestroyImmediate(obj);
#else
            UnityEngine.Object.Destroy(obj);
#endif
        }

        public static Vector2 DeriveJitterOffset(in Matrix4x4 projMatrix, in Matrix4x4 nonJitteredProjMatrix)
        {
            Matrix4x4 jitterMatrix = projMatrix * nonJitteredProjMatrix.inverse;
            return new Vector2(jitterMatrix[0, 3], jitterMatrix[1, 3]);
        }

        public static IEnumerator SwitchFrameGenProvider(ulong providerId, Action<bool> resultCallback = null)
        {
            if (!FrameGenPlugin.IsAvailable)
            {
                resultCallback?.Invoke(false);
                yield break;
            }

            IFrameGenProvider frameGenProvider = FrameGenProviderRegistry.FindFrameGenProvider(providerId);
            if (frameGenProvider == null && providerId != 0)
            {
                Debug.LogError($"[FG] Could not find FrameGen Provider with ID: 0x{providerId:x}");
                resultCallback?.Invoke(false);
                yield break;
            }

            string frameGenName = frameGenProvider?.DisplayName ?? "None";
            Debug.Log($"[FG] Switching to FrameGen Provider: {frameGenName}");

            // Initiate the FG provider switch
            FrameGenPlugin.SwitchFrameGenProviderScope scope = new(providerId);
            if (!scope.Result)
            {
                Debug.LogError("[FG] FrameGen Provider switch failed in native plugin");
                resultCallback?.Invoke(false);
                yield break;
            }

            yield return null;
            yield return null;

            // Modify and restore screen resolution, to force a resize of the backbuffers and trigger a rebuild of the swapchain.
            // This is spread out over multiple frames, to ensure Unity actually processes the resolution switches.
            if (Screen.fullScreen)
            {
                int windowWidth = PlayerPrefs.GetInt("Screenmanager Resolution Window Width", Screen.currentResolution.width);
                int windowHeight = PlayerPrefs.GetInt("Screenmanager Resolution Window Height", Screen.currentResolution.height);
                int screenWidth = Screen.width;
                int screenHeight = Screen.height;

                // Switch to a windowed resolution that's guaranteed different from our full-screen resolution
                if (windowWidth != screenWidth || windowHeight != screenHeight)
                {
                    Screen.SetResolution(windowWidth, windowHeight, false);
                }
                else
                {
                    Screen.SetResolution(screenWidth, screenHeight - 1, false);
                    yield return null;
                    yield return null;
                    Screen.SetResolution(windowWidth, windowHeight, false);     // Restore the original windowed resolution
                }

                // Switch back to full-screen at its original resolution
                yield return null;
                yield return null;
                Screen.SetResolution(screenWidth, screenHeight, true);
            }
            else
            {
                int height = Screen.height;
                Screen.SetResolution(Screen.width, height - 1, false);
                yield return null;
                yield return null;
                Screen.SetResolution(Screen.width, height, false);
            }

            yield return null;
            yield return null;

            // Finalize the FG provider switch
            scope.Dispose();
            resultCallback?.Invoke(scope.Result);
        }

        public static bool AllocateRenderTexture(ref RenderTexture rt, int width, int height, GraphicsFormat format, string name)
        {
            if (rt == null || rt.width != width || rt.height != height || rt.graphicsFormat != format || rt.name != name)
            {
                if (rt != null)
                {
                    rt.Release();
                    rt = null;
                }

                rt = new RenderTexture(width, height, format, GraphicsFormat.None) { name = name };
                return rt.Create();
            }

            return true;
        }
    }
}
