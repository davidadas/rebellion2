#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
using UnityEngine;

/// <summary>
/// Passes the existing listener mix to the Windows recovery owner.
/// </summary>
[RequireComponent(typeof(AudioListener))]
internal sealed class WindowsAudioMixTap : MonoBehaviour
{
    internal volatile WindowsAudioOutput Owner;

    /// <summary>
    /// Hands the complete mixed audio to the private output when recovery owns playback.
    /// </summary>
    /// <param name="samples">The interleaved listener mix.</param>
    /// <param name="channels">The listener mix channel count.</param>
    private void OnAudioFilterRead(float[] samples, int channels)
    {
        Owner?.ProcessMix(samples, channels);
    }
}
#endif
