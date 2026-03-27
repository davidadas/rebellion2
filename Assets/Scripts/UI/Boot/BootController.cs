using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Video;

/// <summary>
/// Controls the initial boot sequence of the application.
/// Relies on <see cref="CutsceneManager"/> for video playback orchestration.
/// </summary>
public sealed class BootController : MonoBehaviour
{
    /// <summary>
    /// Ordered list of video resource paths played during boot.
    /// </summary>
    private static readonly string[] BootVideoPaths = { "Videos/intro", "Videos/opening_crawl" };

    private int currentIndex = 0;

    /// <summary>
    /// Begins the boot video sequence when the scene starts.
    /// </summary>
    private void Start()
    {
        PlayNext();
    }

    /// <summary>
    /// Plays the next boot video in sequence.
    /// If all videos have been played, transitions to the MainMenu scene.
    /// </summary>
    private void PlayNext()
    {
        if (currentIndex >= BootVideoPaths.Length)
        {
            SceneManager.LoadScene("MainMenu");
            return;
        }

        string path = BootVideoPaths[currentIndex];
        currentIndex++;

        VideoClip clip = ResourceManager.Instance.GetVideo(path);

        CutsceneManager.Instance.Play(clip, PlayNext);
    }
}
