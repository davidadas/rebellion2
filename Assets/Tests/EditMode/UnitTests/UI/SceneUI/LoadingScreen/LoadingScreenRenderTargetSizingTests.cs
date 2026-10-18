using NUnit.Framework;
using UnityEngine;

namespace Rebellion.Tests.UI.SceneUI.LoadingScreen
{
    [TestFixture]
    public sealed class LoadingScreenRenderTargetSizingTests
    {
        [Test]
        public void Calculate_SixteenByNineDisplay_ReturnsReferenceResolution()
        {
            Vector2Int result = LoadingScreenRenderTargetSizing.Calculate(1920, 1080);

            Assert.AreEqual(new Vector2Int(1280, 720), result);
        }

        [Test]
        public void Calculate_SixteenByTenDisplay_PreservesDisplayAspect()
        {
            Vector2Int result = LoadingScreenRenderTargetSizing.Calculate(2560, 1600);

            Assert.AreEqual(new Vector2Int(1152, 720), result);
        }

        [Test]
        public void Calculate_UltrawideDisplay_PreservesDisplayAspectWithinWidthLimit()
        {
            Vector2Int result = LoadingScreenRenderTargetSizing.Calculate(5120, 1440);

            Assert.AreEqual(new Vector2Int(1920, 540), result);
        }

        [Test]
        public void Calculate_ZeroDisplayDimensions_ReturnsValidResolution()
        {
            Vector2Int result = LoadingScreenRenderTargetSizing.Calculate(0, 0);

            Assert.AreEqual(new Vector2Int(720, 720), result);
        }
    }
}
