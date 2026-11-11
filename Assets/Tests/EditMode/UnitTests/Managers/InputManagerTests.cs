using System;
using NUnit.Framework;
using UnityEngine;

namespace Rebellion.Tests.Managers
{
    [TestFixture]
    public sealed class InputManagerTests
    {
        private GameObject root;
        private InputManager manager;

        /// <summary>
        /// Creates an input owner without creating its generated action asset.
        /// </summary>
        [SetUp]
        public void SetUp()
        {
            root = new GameObject("Input manager");
            manager = root.AddComponent<InputManager>();
        }

        /// <summary>
        /// Releases the owned input manager and any action maps it created.
        /// </summary>
        [TearDown]
        public void TearDown() => UnityEngine.Object.DestroyImmediate(root);

        [Test]
        public void SuppressApplicationShortcuts_OverlappingLeases_RetainsSuppressionUntilLastRelease()
        {
            IDisposable first = manager.SuppressApplicationShortcuts();
            IDisposable second = manager.SuppressApplicationShortcuts();
            first.Dispose();
            Assert.IsTrue(manager.AreApplicationShortcutsSuppressed);
            second.Dispose();
            Assert.IsFalse(manager.AreApplicationShortcutsSuppressed);
        }

        [Test]
        public void SuppressApplicationShortcuts_RepeatedDispose_DoesNotReleaseAnotherOwner()
        {
            IDisposable first = manager.SuppressApplicationShortcuts();
            using IDisposable second = manager.SuppressApplicationShortcuts();
            first.Dispose();
            first.Dispose();
            Assert.IsTrue(manager.AreApplicationShortcutsSuppressed);
        }

        [Test]
        public void SetApplicationShortcutsSuppressed_BindingEndsDuringLease_RetainsPresentationSuppression()
        {
            using IDisposable lease = manager.SuppressApplicationShortcuts();
            manager.SetApplicationShortcutsSuppressed(true);
            manager.SetApplicationShortcutsSuppressed(false);
            Assert.IsTrue(manager.AreApplicationShortcutsSuppressed);
        }
    }
}
