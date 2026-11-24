using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.Video;

namespace Rebellion.Tests.UI.SceneUI.Cutscenes
{
    [TestFixture]
    public sealed class CutsceneManagerTests
    {
        private GameObject _root;
        private CutsceneManager _manager;
        private GameObject _template;

        /// <summary>
        /// Creates a live manager with a fixture player template.
        /// </summary>
        [SetUp]
        public void SetUp()
        {
            _root = new GameObject("Cutscene manager fixture");
            _manager = _root.AddComponent<CutsceneManager>();
            _template = CutscenePlayerFixture.Create(_root.transform);
            _manager.Initialize(_template);
        }

        /// <summary>
        /// Destroys the manager and its template after playback.
        /// </summary>
        /// <returns>The frame needed to finish the destruction.</returns>
        [UnityTearDown]
        public IEnumerator TearDown()
        {
            Object.Destroy(_root);
            yield return null;
        }

        [UnityTest]
        public IEnumerator Play_SkippedVideo_ReleasesPlayerBeforeStartingNextVideo()
        {
            VideoClip clip = AssetDatabase.LoadAssetAtPath<VideoClip>(
                "Assets/Tests/EditMode/UnitTests/UI/SceneUI/Cutscenes/CutsceneTestClip.webm"
            );
            Assert.IsNotNull(clip);
            CutscenePlayer firstPlayer = null;
            bool firstPlayerReleasedBeforeCompletion = false;
            _manager.Play(
                clip,
                () =>
                {
                    firstPlayerReleasedBeforeCompletion = firstPlayer == null;
                    _manager.Play(clip, null);
                }
            );
            firstPlayer = FindPlayer();
            Assert.IsNotNull(firstPlayer);

            firstPlayer.Skip();

            Assert.IsFalse(firstPlayerReleasedBeforeCompletion);
            yield return null;

            Assert.IsTrue(firstPlayerReleasedBeforeCompletion);
            CutscenePlayer secondPlayer = FindPlayer();
            Assert.IsNotNull(secondPlayer);
            Assert.AreNotSame(firstPlayer, secondPlayer);
        }

        /// <summary>
        /// Finds the live player created by the manager.
        /// </summary>
        /// <returns>The playback instance, if present.</returns>
        private CutscenePlayer FindPlayer()
        {
            return Object
                .FindObjectsByType<CutscenePlayer>()
                .SingleOrDefault(player => player.gameObject != _template);
        }
    }
}
