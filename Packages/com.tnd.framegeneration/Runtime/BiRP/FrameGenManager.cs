using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Scripting;

[assembly: AlwaysLinkAssembly]

namespace TND.FrameGen.BiRP
{
    internal class FrameGenManager: FrameGenManagerBase<FrameGenManager>
    {
        private FrameGenController _currentFrameGenController;

        [Preserve]
        private FrameGenManager() { }

        [RuntimeInitializeOnLoadMethod]
        private static void Initialize()
        {
            BaseInitialize();
        }

        protected override void UpdateCameraAssociation(Camera finalBackbufferCamera)
        {
            if (finalBackbufferCamera == null)
            {
                if (_currentFrameGenController != null)
                {
                    FrameGenUtils.DestroyObject(_currentFrameGenController);
                    _currentFrameGenController = null;
                }

                using CommandBuffer cmd = new();
                DispatchDisabledConfigPass(cmd);
                Graphics.ExecuteCommandBuffer(cmd);
                return;
            }

            // Check if it matches FG controller camera
            if (_currentFrameGenController != null && _currentFrameGenController.Camera != finalBackbufferCamera)
            {
                FrameGenUtils.DestroyObject(_currentFrameGenController);
                _currentFrameGenController = null;
            }

            // (Re)create FG controller if necessary
            if (_currentFrameGenController == null)
            {
                _currentFrameGenController = finalBackbufferCamera.gameObject.AddComponent<FrameGenController>();
                _currentFrameGenController.hideFlags = HideFlags.HideAndDontSave | HideFlags.HideInInspector;
            }

            _currentFrameGenController.frameGenEnabled = IsFrameGenActiveAndEnabled;
            _currentFrameGenController.numFramesToGenerate = s_numFramesToGenerate;
            _currentFrameGenController.useDynamicMFG = s_useDynamicMFG;
            _currentFrameGenController.dynamicFramerateTarget = s_dynamicFramerateTarget;
            _currentFrameGenController.distortionField = s_distortionField;
            _currentFrameGenController.debugFlags = s_debugFlags;
        }
    }
}
