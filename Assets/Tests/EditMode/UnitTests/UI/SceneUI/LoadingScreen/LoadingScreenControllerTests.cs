using System;
using System.IO;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Rebellion.Tests.UI.SceneUI.LoadingScreen
{
    [TestFixture]
    public sealed class LoadingScreenControllerTests
    {
        /// <summary>
        /// Removes the persistent startup-failure presentation between tests.
        /// </summary>
        [TearDown]
        public void TearDown()
        {
            foreach (
                LoadingScreenController controller in Object.FindObjectsByType<LoadingScreenController>(
                    FindObjectsInactive.Include
                )
            )
                Object.DestroyImmediate(controller.gameObject);
        }

        [Test]
        public void ShowStartupFailure_WithoutBootstrap_PresentsRootCauseAndRepairInstructions()
        {
            Assert.IsNull(AppBootstrap.Instance);
            Exception failure = new InvalidOperationException(
                "AppBootstrap initialization failed.",
                new FileNotFoundException("Content definition not found: Content/catalog.xml")
            );

            LoadingScreenController.ShowStartupFailure(failure);

            LoadingScreenView view = Object.FindAnyObjectByType<LoadingScreenView>();
            Assert.IsNotNull(view);
            Assert.IsTrue(LoadingScreenController.IsLoadInProgress);
            Assert.AreEqual(
                "GAME STARTUP FAILED",
                view.transform.Find("Canvas/Background/StatusTextField")
                    .GetComponent<TMP_Text>()
                    .text
            );
            string details = view
                .transform.Find("Canvas/Background/FailureDetailTextField")
                .GetComponent<TMP_Text>()
                .text;
            StringAssert.Contains("Reopen the launcher", details);
            StringAssert.Contains("Content/catalog.xml", details);
            StringAssert.DoesNotContain("AppBootstrap initialization failed", details);
        }

        [Test]
        public void ShowStartupFailure_WithoutBootstrap_OffersQuit()
        {
            LoadingScreenController.ShowStartupFailure(new IOException("Content is unavailable."));

            LoadingScreenView view = Object.FindAnyObjectByType<LoadingScreenView>();
            Button button = view
                .transform.Find("Canvas/Background/ContinueButton")
                .GetComponent<Button>();
            Assert.IsTrue(button.gameObject.activeInHierarchy);
            Assert.IsTrue(button.interactable);
            Assert.AreEqual("QUIT", button.GetComponentInChildren<TMP_Text>().text);
        }

        [Test]
        public void ShowStartupFailure_RepeatedFailure_ReusesPresentation()
        {
            LoadingScreenController.ShowStartupFailure(new IOException("First failure."));
            LoadingScreenView first = Object.FindAnyObjectByType<LoadingScreenView>();

            LoadingScreenController.ShowStartupFailure(new IOException("Second failure."));

            LoadingScreenView[] views = Object.FindObjectsByType<LoadingScreenView>(
                FindObjectsInactive.Include
            );
            Assert.AreEqual(1, views.Length);
            Assert.AreSame(first, views[0]);
            StringAssert.Contains(
                "Second failure.",
                first
                    .transform.Find("Canvas/Background/FailureDetailTextField")
                    .GetComponent<TMP_Text>()
                    .text
            );
        }
    }
}
