using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace Rebellion.Tests.UI.SceneUI.StrategyView.Hud
{
    [TestFixture]
    public class StrategyAdvisorViewDataTests
    {
        [Test]
        public void AnimationData_NullFrames_UsesEmptySnapshot()
        {
            StrategyAdvisorAnimationViewData data = new StrategyAdvisorAnimationViewData(
                null,
                true,
                "Audio/Advisor",
                -1f,
                -2f
            );

            Assert.IsEmpty(data.Frames);
            Assert.IsTrue(data.UsesDroid);
            Assert.AreEqual("Audio/Advisor", data.AudioPath);
            Assert.AreEqual(0f, data.DelayBeforeSeconds);
            Assert.AreEqual(0f, data.MinimumPlaybackSeconds);
        }

        [Test]
        public void AnimationData_SourceFrames_CopiesIntoReadOnlySnapshot()
        {
            Texture2D first = new Texture2D(4, 4);
            Texture2D second = new Texture2D(4, 4);
            List<Texture2D> source = new List<Texture2D> { first, second };

            StrategyAdvisorAnimationViewData data = new StrategyAdvisorAnimationViewData(
                source,
                false,
                null
            );
            source[0] = second;

            CollectionAssert.AreEqual(new[] { first, second }, data.Frames);
            Assert.IsFalse(data.UsesDroid);
            Assert.IsNull(data.AudioPath);
            Assert.Throws<NotSupportedException>(() => ((IList<Texture2D>)data.Frames).Add(first));

            UnityEngine.Object.DestroyImmediate(second);
            UnityEngine.Object.DestroyImmediate(first);
        }
    }
}
