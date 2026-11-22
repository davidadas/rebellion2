using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Rebellion.Tests.UI.Audio
{
    [TestFixture]
    public sealed class AudioManagerTests
    {
        private AudioManager manager;

        /// <summary>
        /// Releases the audio manager after the runtime test.
        /// </summary>
        /// <returns>The cleanup sequence that waits for Unity to destroy the manager.</returns>
        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (manager != null)
                Object.Destroy(manager.gameObject);
            yield return null;
        }

        [UnityTest]
        public IEnumerator FadeOutMusic_InterruptedTrack_DoesNotResumeAfterFocusReturns()
        {
            manager = AudioManager.EnsureExists();
            manager.InitializeContent(TestContent.Assets);
            manager.SendMessage("OnApplicationFocus", true);
            manager.PlayTrack("Application/MainMenu/Audio/battle-of-endor-1-medley", true);

            AudioSource[] sources = manager.GetComponents<AudioSource>();
            float timeout = Time.realtimeSinceStartup + 10f;
            while (
                !sources.Any(source => source.loop && source.isPlaying)
                && Time.realtimeSinceStartup < timeout
            )
                yield return null;
            AudioSource track = sources.SingleOrDefault(source => source.loop && source.isPlaying);
            Assert.IsNotNull(track, "The test music track did not start.");

            manager.SendMessage("OnApplicationFocus", false);
            manager.FadeOutMusic(0f);
            manager.SendMessage("OnApplicationFocus", true);
            yield return null;

            Assert.IsFalse(track.isPlaying);
        }
    }
}
