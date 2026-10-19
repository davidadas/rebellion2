using System;
using NUnit.Framework;
using TMPro;
using UnityEngine;

namespace Rebellion.Tests.UI.Components
{
    [TestFixture]
    public sealed class FrameRateMonitorTests
    {
        private GameObject _applicationRoot;

        /// <summary>
        /// Creates an isolated application root for each monitor test.
        /// </summary>
        [SetUp]
        public void SetUp()
        {
            _applicationRoot = new GameObject("FrameRateMonitorTests.ApplicationRoot");
        }

        /// <summary>
        /// Destroys the application root and its generated monitor hierarchy.
        /// </summary>
        [TearDown]
        public void TearDown()
        {
            UnityEngine.Object.DestroyImmediate(_applicationRoot);
        }

        /// <summary>
        /// Verifies repeated initialization reuses the application-owned monitor and refreshes its
        /// visibility source.
        /// </summary>
        [Test]
        public void EnsureExists_ExistingMonitor_ReusesInstanceAndRefreshesVisibility()
        {
            FrameRateMonitor first = FrameRateMonitor.EnsureExists(
                _applicationRoot.transform,
                () => false
            );
            TMP_Text label = first.GetComponentInChildren<TMP_Text>(true);
            Assert.IsNotNull(label);
            Assert.IsFalse(label.gameObject.activeSelf);

            FrameRateMonitor second = FrameRateMonitor.EnsureExists(
                _applicationRoot.transform,
                () => true
            );

            Assert.AreSame(first, second);
            Assert.IsTrue(label.gameObject.activeSelf);
        }

        /// <summary>
        /// Verifies the monitor rejects creation without an application owner.
        /// </summary>
        [Test]
        public void EnsureExists_MissingParent_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() =>
                FrameRateMonitor.EnsureExists(null, () => true)
            );
        }
    }
}
