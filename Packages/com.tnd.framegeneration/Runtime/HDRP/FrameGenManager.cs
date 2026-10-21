using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;
using UnityEngine.Scripting;

[assembly: AlwaysLinkAssembly]

namespace TND.FrameGen.HDRP
{
    internal class FrameGenManager: FrameGenManagerBase<FrameGenManager>
    {
        private GameObject _preparePassGameObject;
        private CustomPassVolume _preparePassVolume;
        private FrameGenPreparePass _preparePass;

        [Preserve]
        private FrameGenManager() { }

        [RuntimeInitializeOnLoadMethod]
        private static void Initialize()
        {
            BaseInitialize();

            RenderPipelineManager.beginContextRendering += OnBeginContextRendering;
            RenderPipelineManager.endContextRendering += OnEndContextRendering;
        }

        protected override void UpdateCameraAssociation(Camera finalBackbufferCamera)
        {
            // Set up the Prepare custom pass volume
            if (_preparePassGameObject == null)
            {
                _preparePassGameObject = new GameObject("FrameGen Prepare Pass");
                _preparePassGameObject.hideFlags = HideFlags.HideAndDontSave | HideFlags.HideInHierarchy;
                Object.DontDestroyOnLoad(_preparePassGameObject);
            }

            if (_preparePassVolume == null && !_preparePassGameObject.TryGetComponent(out _preparePassVolume))
            {
                _preparePassVolume = _preparePassGameObject.AddComponent<CustomPassVolume>();
                _preparePassVolume.injectionPoint = CustomPassInjectionPoint.BeforeTransparent;   // At this point, the motion vectors buffer is complete
            }

            if (_preparePass == null && !TryGetPassOfType(_preparePassVolume, out _preparePass))
            {
                _preparePass = (FrameGenPreparePass)_preparePassVolume.AddPassOfType(typeof(FrameGenPreparePass));
            }

            // Associate custom pass volume with camera
            _preparePassVolume.targetCamera = finalBackbufferCamera;

            // Update FG settings
            if (finalBackbufferCamera != null)
            {
                _preparePass.enabled = IsFrameGenActiveAndEnabled;
            }
            else
            {
                // In case we don't have a camera to associate with (e.g. loading screens), just temporarily disable FG
                _preparePass.enabled = false;
            }
        }

        private static bool TryGetPassOfType<TPass>(CustomPassVolume customPassVolume, out TPass pass)
            where TPass: CustomPass
        {
            foreach (CustomPass customPass in customPassVolume.customPasses)
            {
                if (customPass is TPass passOfType)
                {
                    pass = passOfType;
                    return true;
                }
            }

            pass = null;
            return false;
        }
    }
}
