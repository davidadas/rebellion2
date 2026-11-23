using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;

internal static class CutscenePlayerFixture
{
    /// <summary>
    /// Creates a cutscene player wired to a video source, audio source, and display.
    /// </summary>
    /// <param name="parent">The optional parent for the fixture player.</param>
    /// <returns>The configured player object.</returns>
    internal static GameObject Create(Transform parent = null)
    {
        GameObject template = new GameObject("Cutscene player fixture");
        template.SetActive(false);
        template.transform.SetParent(parent, false);
        VideoPlayer video = template.AddComponent<VideoPlayer>();
        video.playOnAwake = false;
        video.renderMode = VideoRenderMode.APIOnly;
        AudioSource audio = template.AddComponent<AudioSource>();
        audio.playOnAwake = false;
        RawImage screen = new GameObject("Video screen").AddComponent<RawImage>();
        screen.transform.SetParent(template.transform, false);
        screen.gameObject.AddComponent<AspectRatioFitter>().aspectMode = AspectRatioFitter
            .AspectMode
            .FitInParent;
        CutscenePlayer player = template.AddComponent<CutscenePlayer>();
        using SerializedObject serialized = new SerializedObject(player);
        serialized.FindProperty("screen").objectReferenceValue = screen;
        serialized.FindProperty("videoPlayer").objectReferenceValue = video;
        serialized.FindProperty("audioSource").objectReferenceValue = audio;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        template.SetActive(true);
        return template;
    }
}
