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
        public void ViewData_CompletePresentation_StoresAllValues()
        {
            Texture2D protocol = new Texture2D(4, 4);
            Texture2D droid = new Texture2D(4, 4);
            RectInt protocolBounds = new RectInt(10, 20, 30, 40);
            RectInt droidBounds = new RectInt(50, 60, 70, 80);

            StrategyAdvisorViewData data = new StrategyAdvisorViewData(
                true,
                protocol,
                droid,
                protocolBounds,
                droidBounds,
                0.25f
            );

            Assert.IsTrue(data.Visible);
            Assert.AreSame(protocol, data.ProtocolIdleTexture);
            Assert.AreSame(droid, data.DroidIdleTexture);
            Assert.AreEqual(protocolBounds, data.ProtocolBounds);
            Assert.AreEqual(droidBounds, data.DroidBounds);
            Assert.AreEqual(0.25f, data.FrameIntervalSeconds);

            UnityEngine.Object.DestroyImmediate(droid);
            UnityEngine.Object.DestroyImmediate(protocol);
        }

        [Test]
        public void AnimationData_NullFrames_UsesEmptySnapshot()
        {
            StrategyAdvisorAnimationViewData data = new StrategyAdvisorAnimationViewData(
                null,
                true,
                "Audio/Advisor"
            );

            Assert.IsEmpty(data.Frames);
            Assert.IsTrue(data.UsesDroid);
            Assert.AreEqual("Audio/Advisor", data.AudioPath);
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
