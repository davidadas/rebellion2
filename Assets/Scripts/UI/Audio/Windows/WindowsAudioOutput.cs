#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
using System;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Recovers a missing Windows output while preserving Unity's existing mixer and playback controls.
/// </summary>
internal sealed class WindowsAudioOutput : MonoBehaviour
{
    private readonly object gate = new object();
    private Task opening = Task.CompletedTask;
    private volatile WindowsWaveOutput output;
    private OpenResult ready;
    private int generation;
    private bool stopping;
    private bool forceStereo;

    /// <summary>
    /// Installs Windows-only recovery before the first scene, excluding explicit audio disablement.
    /// </summary>
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Initialize()
    {
        if (Application.platform != RuntimePlatform.WindowsPlayer || Application.isBatchMode)
            return;
        string[] arguments = Environment.GetCommandLineArgs();
        if (
            Array.IndexOf(arguments, "-nosound") >= 0
            || Array.IndexOf(arguments, "-system-audio") >= 0
        )
            return;
        GameObject owner = new GameObject("Windows audio output");
        DontDestroyOnLoad(owner);
        WindowsAudioOutput recovery = owner.AddComponent<WindowsAudioOutput>();
        recovery.forceStereo = Array.IndexOf(arguments, "-windows-stereo-audio") >= 0;
        recovery.BeginOpen();
    }

    /// <summary>
    /// Subscribes to scene and audio-device changes while this recovery owner is alive.
    /// </summary>
    private void Awake()
    {
        SceneManager.sceneLoaded += HandleSceneLoaded;
        AudioSettings.OnAudioConfigurationChanged += HandleConfigurationChanged;
    }

    /// <summary>
    /// Adopts a completed native stream and reports a device failure once on the main thread.
    /// </summary>
    private void Update()
    {
        OpenResult completed;
        lock (gate)
        {
            completed = ready;
            ready = null;
            if (completed != null)
                output = completed.Output;
        }
        if (completed != null)
        {
            if (completed.Failure != null)
                Debug.LogWarning($"Windows audio recovery: {completed.Failure}");
            else if (completed.Output != null)
            {
                Debug.Log("Windows audio recovery: using 16-bit stereo output.");
                for (int index = 0; index < SceneManager.sceneCount; index++)
                    AttachListeners(SceneManager.GetSceneAt(index));
            }
        }
        WindowsWaveOutput active = output;
        if (active?.Failure == null)
            return;
        output = null;
        Debug.LogWarning($"Windows audio recovery stopped: {active.Failure}");
        lock (gate)
            opening = opening.ContinueWith(_ => active.Dispose(), TaskScheduler.Default);
    }

    /// <summary>
    /// Stops ownership transfers and releases late native results after shutdown.
    /// </summary>
    private void OnDestroy()
    {
        SceneManager.sceneLoaded -= HandleSceneLoaded;
        AudioSettings.OnAudioConfigurationChanged -= HandleConfigurationChanged;
        WindowsWaveOutput active;
        WindowsWaveOutput pending;
        lock (gate)
        {
            stopping = true;
            generation++;
            active = output;
            output = null;
            pending = ready?.Output;
            ready = null;
        }
        active?.Dispose();
        pending?.Dispose();
    }

    /// <summary>
    /// Redirects the final mix only while an opened stream owns playback.
    /// </summary>
    /// <param name="samples">The existing Unity listener mix.</param>
    /// <param name="channels">The mix channel count.</param>
    internal void ProcessMix(float[] samples, int channels)
    {
        output?.TryWrite(samples, channels);
    }

    /// <summary>
    /// Rechecks native output ownership after Unity handles a configuration change.
    /// </summary>
    /// <param name="deviceWasChanged">Whether Windows reported a device change.</param>
    private void HandleConfigurationChanged(bool deviceWasChanged)
    {
        BeginOpen();
    }

    /// <summary>
    /// Attaches the active fallback to listeners introduced by a newly loaded scene.
    /// </summary>
    /// <param name="scene">The loaded scene.</param>
    /// <param name="mode">The scene loading mode.</param>
    private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (output != null)
            AttachListeners(scene);
    }

    /// <summary>
    /// Adds one mix tap per existing listener without scanning during audio or frame callbacks.
    /// </summary>
    /// <param name="scene">The scene whose listeners should use the opened fallback.</param>
    private void AttachListeners(Scene scene)
    {
        if (!scene.isLoaded)
            return;
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            foreach (AudioListener listener in root.GetComponentsInChildren<AudioListener>(true))
            {
                WindowsAudioMixTap tap = listener.GetComponent<WindowsAudioMixTap>();
                if (tap == null)
                    tap = listener.gameObject.AddComponent<WindowsAudioMixTap>();
                tap.Owner = this;
            }
        }
    }

    /// <summary>
    /// Serializes native teardown, session inspection and opening outside the game thread.
    /// </summary>
    private void BeginOpen()
    {
        AudioSettings.GetDSPBufferSize(out int frames, out int buffers);
        int sampleRate = AudioSettings.outputSampleRate;
        lock (gate)
        {
            int requestedGeneration = ++generation;
            WindowsWaveOutput previous = output;
            output = null;
            WindowsWaveOutput pending = ready?.Output;
            ready = null;
            opening = opening.ContinueWith(
                _ =>
                {
                    previous?.Dispose();
                    pending?.Dispose();
                    lock (gate)
                    {
                        if (stopping || generation != requestedGeneration)
                            return;
                    }
                    OpenResult result = Open(sampleRate, frames, buffers);
                    lock (gate)
                    {
                        if (!stopping && generation == requestedGeneration)
                        {
                            ready = result;
                            return;
                        }
                    }
                    result.Output?.Dispose();
                },
                TaskScheduler.Default
            );
        }
    }

    /// <summary>
    /// Opens a private stream after inspecting Windows on a background COM apartment.
    /// </summary>
    /// <param name="sampleRate">The current Unity sample rate.</param>
    /// <param name="frames">The current DSP buffer frame count.</param>
    /// <param name="buffers">The current DSP buffer count.</param>
    /// <returns>The opened stream or a captured initialization failure.</returns>
    private OpenResult Open(int sampleRate, int frames, int buffers)
    {
        try
        {
            return new OpenResult(
                WindowsAudioRecovery.Open(
                    forceStereo,
                    WindowsAudioSessionProbe.HasOutputSession,
                    () => new WindowsWaveOutput(new WindowsWaveApi(), sampleRate, frames, buffers)
                ),
                null
            );
        }
        catch (Exception exception)
        {
            return new OpenResult(null, exception.Message);
        }
    }

    /// <summary>
    /// Carries an initialized stream or its failure back to the Unity thread.
    /// </summary>
    private sealed class OpenResult
    {
        public WindowsWaveOutput Output { get; }
        public string Failure { get; }

        /// <summary>
        /// Records the outcome of one native initialization request.
        /// </summary>
        /// <param name="output">The successfully opened stream.</param>
        /// <param name="failure">The initialization failure, when present.</param>
        public OpenResult(WindowsWaveOutput output, string failure)
        {
            Output = output;
            Failure = failure;
        }
    }
}
#endif
