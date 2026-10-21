using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace TND.FrameGen
{
    public class PreBuildChecks: IPreprocessBuildWithReport
    {
        public int callbackOrder => 100;

        public void OnPreprocessBuild(BuildReport report)
        {
            if (report.summary.platform is not (BuildTarget.StandaloneWindows or BuildTarget.StandaloneWindows64))
                return;

            if (PlayerSettings.fullScreenMode == FullScreenMode.ExclusiveFullScreen)
            {
                Debug.LogWarning("Full-screen mode is set to Exclusive Fullscreen, which is not compatible with Frame Generation. Forcing this setting to Fullscreen Window to avoid issues.");
                PlayerSettings.fullScreenMode = FullScreenMode.FullScreenWindow;
            }
        }
    }
}
