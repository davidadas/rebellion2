using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace TND.Common
{
    public class EditorVisuals : MonoBehaviour
    {
        public static string publisherURL = "https://assetstore.unity.com/publishers/14941";
        public static string discordURL = "https://discord.gg/r9jkRzaPtC";

        private static Texture s_logo;
        private static Texture2D s_background;
        private static Texture2D s_questionMark;
        private static Texture2D s_stars;

        public static Texture2D QuestionMark
        {
            get
            {
                if (!s_questionMark)
                {
                    s_questionMark = Resources.Load<Texture2D>("tnd_question") as Texture2D;
                }
                return s_questionMark;
            }
        }
        public static Texture2D Stars
        {
            get
            {
                if (!s_stars)
                {
                    s_stars = Resources.Load<Texture2D>("tnd_stars") as Texture2D;
                }
                return s_stars;
            }
        }

        private static readonly float HeaderSize = 80;
        private static readonly float FooterSize = 60;

        /// <summary>
        /// Draws the Header area
        /// </summary>
        public static void GenerateHeader(string documentationURL)
        {
            if (!s_background)
            {
                s_background = GenerateRadialGradient(256);
            }

            if (!s_logo)
            {
                s_logo = Resources.Load<Texture2D>("tnd_logo") as Texture2D;
            }

            //Draw BG
            var rect = GUILayoutUtility.GetRect(0, 10000, HeaderSize, HeaderSize);
            GUI.DrawTexture(rect, s_background, ScaleMode.StretchToFill);

            //Draw Logo
            float x = rect.x + (rect.width - HeaderSize) / 2;
            float y = rect.y + (rect.height - HeaderSize) / 2;
            GUI.DrawTexture(new Rect(x, y, HeaderSize, HeaderSize), s_logo, ScaleMode.ScaleToFit, true);

            //Clickable Header
            EditorGUIUtility.AddCursorRect(rect, MouseCursor.Link);
            if (GUI.Button(rect, "", new GUIStyle(GUI.skin.label)))
            {
                Application.OpenURL(publisherURL);
            }

            //Clickable Documentation Button
            var docRect = GUILayoutUtility.GetRect(0, 10000, 16, 16);
            EditorGUIUtility.AddCursorRect(docRect, MouseCursor.Link);
            GUIStyle buttonStyleDocumentation = new GUIStyle(GUI.skin.textArea)
            {
                alignment = TextAnchor.MiddleRight,
                fontSize = 10,
                fontStyle = FontStyle.Normal,
                normal = { textColor = new Color(0.3f, 0.3f, 1) },
                hover = { textColor = Color.white },
                richText = true,
                border = new RectOffset(0, 0, 10, 10)
            };
            if (GUI.Button(docRect, "Online Documentation", buttonStyleDocumentation))
            {
                Application.OpenURL(documentationURL);
            }

            EditorGUILayout.Space();
        }

        public static void GenerateCrossPromo()
        {
            int crossPromoCount = 0;

#if !TND_UPSCALING || !TND_FRAMEGEN || !TND_ANTILAG
            GUIStyle centerLabel = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                richText = true,
                fontSize = 12,
                fontStyle = FontStyle.Normal,
                normal = { textColor = Color.white }
            };

            GUIStyle richLabel = new GUIStyle(EditorStyles.label)
            {
                alignment = TextAnchor.MiddleLeft,
                richText = true,
                wordWrap = true,
                fontSize = 10,
                normal =
                {
                    textColor = EditorStyles.label.normal.textColor
                }
            };

            GUILayout.Label("Did you know that we offer more <b>Advanced Performance Technologies for Unity?</b>", centerLabel, GUILayout.Height(32));
#endif

#if !TND_UPSCALING
            crossPromoCount++;

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            GUILayout.BeginHorizontal();
            GUILayout.Label(EditorGUIUtility.IconContent("console.infoicon"), GUILayout.Width(32), GUILayout.Height(32));
            GUILayout.Label("Unlock higher framerates and sharper visuals with <b><color=white>Upscaling for Unity</color></b>.", richLabel, GUILayout.Height(32));
            GUILayout.EndHorizontal();
            EditorGUILayout.EndVertical();
#endif
#if !TND_FRAMEGEN
            crossPromoCount++;

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            GUILayout.BeginHorizontal();
            GUILayout.Label(EditorGUIUtility.IconContent("console.infoicon"), GUILayout.Width(32), GUILayout.Height(32));
            GUILayout.Label("Dramatically increase framerate with cutting edge <b><color=white>Frame Generation for Unity</color></b>.", richLabel, GUILayout.Height(32));
            GUILayout.EndHorizontal();
            EditorGUILayout.EndVertical();
#endif
#if !TND_ANTILAG
            crossPromoCount++;

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            GUILayout.BeginHorizontal();
            GUILayout.Label(EditorGUIUtility.IconContent("console.infoicon"), GUILayout.Width(32), GUILayout.Height(32));
            GUILayout.Label("Reduce latency and improve responsiveness with <b><color=white>Anti-Input Lag for Unity</color></b>.", richLabel, GUILayout.Height(32));
            GUILayout.EndHorizontal();
            EditorGUILayout.EndVertical();
#endif

            if (crossPromoCount > 0)
            {
                EditorGUILayout.Space();
                var docRect = GUILayoutUtility.GetRect(0, 0, 25, 16);
                EditorGUIUtility.AddCursorRect(docRect, MouseCursor.Link);

                string buttonText = "Get them here!";
                if (crossPromoCount == 1)
                {
                    buttonText = "Get it here!";
                }
                if (GUI.Button(docRect, buttonText))
                {
                    Application.OpenURL(publisherURL);
                }
                EditorGUILayout.Space(15);
            }
        }

        /// <summary>
        /// Draws the footer area
        /// </summary>
        public static void GenerateFooter()
        {
            if (!s_background)
            {
                s_background = GenerateRadialGradient(256);
            }

            EditorGUILayout.Space();

            // Tekst gecentreerd
            GUIStyle centerLabel = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 12,
                fontStyle = FontStyle.Normal,
                normal = { textColor = Color.white }
            };

            GUILayout.Label("Please consider leaving a review!", centerLabel);

            // Klikbare icon buttons
            // Stijl voor icoon-knoppen zonder achtergrond/padding
            GUIStyle iconButton = new GUIStyle(GUI.skin.label)
            {
                padding = new RectOffset(0, 0, 0, 0),
                margin  = new RectOffset(0, 0, 0, 0),
                fixedWidth = 30,
                fixedHeight = 30,
            };

            GUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();

            for (int i = 0; i < 5; i++)
            {
                // Teken de knop met alleen de texture
                if (GUILayout.Button(new GUIContent(Stars), iconButton, GUILayout.Width(30), GUILayout.Height(30)))
                {
                    Application.OpenURL(publisherURL);
                }
                // Link-cursor over de laatst getekende knop
                var last = GUILayoutUtility.GetLastRect();
                EditorGUIUtility.AddCursorRect(last, MouseCursor.Link);

                if (i < 4) GUILayout.Space(4);
            }
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();


            EditorGUILayout.Space();

            //Draw BG
            var rect = GUILayoutUtility.GetRect(0, 10000, FooterSize, FooterSize);
            GUI.DrawTexture(rect, s_background, ScaleMode.StretchToFill);

            //Clickable Footer
            EditorGUIUtility.AddCursorRect(rect, MouseCursor.Link);
            GUIStyle buttonStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 16,
                fontStyle = FontStyle.Bold,
                normal = { background = s_background, textColor = new Color(0.9215686274509804f, 0.5372549019607843f, 0.4431372549019608f) },
                hover = { background = s_background, textColor = Color.white },
                richText = true,
            };

            if (GUI.Button(rect, "Click here for more \n 'The Naked Dev' assets!", buttonStyle))
            {
                Application.OpenURL(publisherURL);
            }
        }

        /// <summary>
        ///  Generates the BG texture for the header
        /// </summary>
        /// <param name="size"></param>
        /// <returns></returns>
        private static Texture2D GenerateRadialGradient(int size)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.wrapMode = TextureWrapMode.Clamp;

            var center = new Vector2(size / 2f, size / 2f);
            float maxDist = center.magnitude;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dist = Vector2.Distance(new Vector2(x, y), center);
                    float t = dist / maxDist;
                    float brightness = Mathf.Lerp(0.05f, 0.2f, 1 - t);
                    tex.SetPixel(x, y, new Color(brightness, brightness, brightness, 1f));
                }
            }

            tex.Apply();
            return tex;
        }
    }
}
