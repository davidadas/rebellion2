using TND.Common;
using UnityEditor;
using UnityEngine;

namespace TND.FrameGen
{
    [InitializeOnLoad]
    public static class InitialisationPopupTrigger
    {
        private const string Key = "TNDFrameGenInit";

        static InitialisationPopupTrigger()
        {
            if (!EditorPrefs.GetBool(Key, false))
            {
                EditorApplication.delayCall += () =>
                {
                    if (!EditorPrefs.GetBool(Key, false))
                    {
                        InitialisationPopupWindow.ShowWindow();
                        EditorPrefs.SetBool(Key, true);
                    }
                };
            }
        }
    }

    public class InitialisationPopupWindow : EditorWindow
    {
        [MenuItem("Window/The Naked Dev/Frame Generation Documentation")]
        public static void ShowWindow()
        {
            var window = GetWindow<InitialisationPopupWindow>(true, "The Naked Dev - Frame Generation for Unity");
            var size = new Vector2(500, 625);
            window.minSize = size;
            window.maxSize = size;

            var main = EditorGUIUtility.GetMainWindowPosition();
            window.position = new Rect(
            main.x + (main.width - size.x) / 2,
            main.y + (main.height - size.y) / 2,
            size.x,
            size.y);

            window.ShowUtility();
        }

        private void OnGUI()
        {
            EditorVisuals.GenerateHeader(FrameGenControllerEditor.OnlineDocumentation);

            GUILayout.Label("Thank you for using the 'The Naked Dev' assets!", EditorStyles.boldLabel);
            GUILayout.Space(15);

            GUILayout.Label("Quick Start:", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("Step 1: Add the 'TND Frame Generation' component to an empty game object in your scene.", MessageType.Info);
            EditorGUILayout.HelpBox("Step 2: Follow the additional setup steps, as prompted on the 'TND Frame Generation' component.", MessageType.Info);

            EditorGUILayout.HelpBox("Due to compatibility issues Frame Generation is currently not supported within the Unity Editor and will only activate in a build.", MessageType.Warning);

            GUILayout.Space(15);
            GUILayout.Label("Support:", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("Please note that Frame Generation is an advanced technology, and integrating it into Unity may introduce additional complexity. Our extensive online documentation is available to help you resolve any potential issues that might arise.", MessageType.Warning);
            if (GUILayout.Button("Open Online Documentation"))
            {
                Application.OpenURL(FrameGenControllerEditor.OnlineDocumentation);
            }

            GUILayout.BeginHorizontal(EditorStyles.helpBox);

            GUILayout.Label(EditorVisuals.QuestionMark, GUILayout.Width(32), GUILayout.Height(32));
            GUIStyle helpBoxStyle = new GUIStyle(EditorStyles.label);
            helpBoxStyle.wordWrap = true;
            helpBoxStyle.fontSize = 10;
            helpBoxStyle.richText = true;
            helpBoxStyle.margin = new RectOffset(0, 0, 0, 0);
            helpBoxStyle.padding = new RectOffset(0, 0, 3, 0);
            GUILayout.Label("And if you're in need of a more personal approach, feel free to join our Discord and reach out, we're always happy to help!", helpBoxStyle);
            GUILayout.EndHorizontal();
            if (GUILayout.Button("Contact 'The Naked Dev' on Discord"))
            {
                Application.OpenURL(EditorVisuals.discordURL);
            }

            GUILayout.Space(15);
            if (GUILayout.Button("Close Window - Forever"))
            {
                Close();
            }
            EditorGUILayout.HelpBox("If you want to reopen this window, you can find it under Window > The Naked Dev > Frame Generation Documentation.", MessageType.None);

            EditorVisuals.GenerateFooter();
        }
    }
}
