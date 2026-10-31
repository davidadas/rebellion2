using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using NUnit.Framework;
using Rebellion.Multiplayer;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Rebellion.Tests.UI.Components
{
    public sealed class MultiplayerSessionEndControllerTests
    {
        private GameObject root;
        private GameObject eventSystemRoot;
        private MultiplayerSessionEndController controller;
        private MultiplayerSessionEndNotice notice;
        private UIWindowManager windowManager;
        private ConfirmationDialogView template;
        private CancelStack cancelStack;
        private Texture2D texture;
        private readonly List<Action<Exception>> failures = new();
        private int endCount;
        private int navigationCount;
        private int suppressionCount;
        private int releaseCount;
        private bool navigationAccepted;
        private bool visibleDuringNavigation;
        private Exception synchronousFailure;

        /// <summary>
        /// Creates isolated notice, input, navigation, and authored-dialog owners for each test.
        /// </summary>
        [SetUp]
        public void SetUp()
        {
            root = new GameObject("Session-end controller test", typeof(RectTransform));
            controller = root.AddComponent<MultiplayerSessionEndController>();
            windowManager = root.AddComponent<UIWindowManager>();
            template = UIComponentTestHelper.InstantiatePrefabComponent<ConfirmationDialogView>(
                "Assets/Prefabs/UI/Common/ConfirmationDialog.prefab"
            );
            template.transform.SetParent(root.transform, false);
            template.gameObject.AddComponent<UIWindow>();
            ((RectTransform)template.transform).sizeDelta = new Vector2(640, 480);
            texture = new Texture2D(1, 1);
            eventSystemRoot = new GameObject("EventSystem", typeof(EventSystem));
            UIComponentTestHelper.InvokeLifecycle(
                eventSystemRoot.GetComponent<EventSystem>(),
                "OnEnable"
            );
            notice = new MultiplayerSessionEndNotice();
            cancelStack = new CancelStack();
            failures.Clear();
            endCount = navigationCount = suppressionCount = releaseCount = 0;
            navigationAccepted = true;
            visibleDuringNavigation = false;
            synchronousFailure = null;
        }

        /// <summary>
        /// Releases the scene objects, event routing, and test artwork after each test.
        /// </summary>
        [TearDown]
        public void TearDown()
        {
            controller?.Dispose();
            UnityEngine.Object.DestroyImmediate(root);
            UIComponentTestHelper.InvokeLifecycle(
                eventSystemRoot.GetComponent<EventSystem>(),
                "OnDisable"
            );
            UnityEngine.Object.DestroyImmediate(eventSystemRoot);
            UnityEngine.Object.DestroyImmediate(texture);
        }

        [Test]
        public void Initialize_PendingNotice_BindsContentAndRegistersBlockingModal()
        {
            UIWindow underlying = CreateUnderlyingWindow(false);
            notice.ShowOpponentDeparture();

            Initialize();

            UIWindow modal = windowManager.ActiveWindow;
            ConfirmationDialogView dialog = GetDialog();
            Assert.IsTrue(modal.Modal);
            Assert.IsFalse(windowManager.CanInteractWithWindow(underlying));
            Assert.IsTrue(windowManager.CanInteractWithWindow(modal));
            Assert.AreEqual(
                MultiplayerSessionEndNotice.Message,
                dialog.GetComponentInChildren<TextMeshProUGUI>().text
            );
            Assert.AreSame(
                texture,
                dialog
                    .transform.Find("DialogSurface/BackgroundImage")
                    .GetComponent<RawImage>()
                    .texture
            );
            Assert.IsFalse(GetButton(dialog, "CancelButtonImage").gameObject.activeSelf);
            Assert.AreEqual(1, suppressionCount);
        }

        [TestCase("Assets/Prefabs/UI/MainMenu/MainMenuRoot.prefab")]
        [TestCase("Assets/Prefabs/UI/StrategyView/StrategyViewRoot.prefab")]
        public void AuthoredPrefab_SceneRoot_ProvidesIndependentNativeNoticeCanvas(
            string prefabPath
        )
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            MultiplayerSessionEndController owner =
                prefab.GetComponentInChildren<MultiplayerSessionEndController>(true);

            Assert.IsNotNull(owner);
            Assert.AreSame(prefab.transform.Find("UI"), owner.transform.parent);
            Assert.IsNotNull(owner.GetComponent<Canvas>());
            Assert.IsNotNull(owner.GetComponent<GraphicRaycaster>());
            Assert.AreEqual(1, prefab.GetComponentsInChildren<UIWindowManager>(true).Length);
            ConfirmationDialogView authored = owner.GetComponentInChildren<ConfirmationDialogView>(
                true
            );
            Assert.IsNotNull(authored.GetComponent<UIWindow>());
            Assert.IsFalse(authored.gameObject.activeSelf);
        }

        [Test]
        public void ShowOpponentDeparture_AlreadyPending_KeepsOneModalAndInputLease()
        {
            Initialize();
            notice.ShowOpponentDeparture();
            UIWindow first = windowManager.ActiveWindow;

            notice.ShowOpponentDeparture();

            Assert.AreSame(first, windowManager.ActiveWindow);
            Assert.AreEqual(1, windowManager.Windows.Count);
            Assert.AreEqual(1, suppressionCount);
            Assert.AreEqual(0, releaseCount);
        }

        [Test]
        public void Initialize_LaterModalOpens_KeepsNoticeInteractableAboveNewWindow()
        {
            Initialize();
            notice.ShowOpponentDeparture();
            UIWindow modal = windowManager.ActiveWindow;

            UIWindow later = CreateUnderlyingWindow(true);

            Assert.AreSame(modal, windowManager.ActiveWindow);
            Assert.IsTrue(windowManager.CanInteractWithWindow(modal));
            Assert.IsFalse(windowManager.CanInteractWithWindow(later));
            Assert.AreEqual(2, windowManager.Windows.Count);
            Assert.IsTrue(GetDialog().GetComponent<CanvasGroup>().interactable);
        }

        [Test]
        public void TryCancel_PendingNotice_ConsumesCancellationWithoutDismissing()
        {
            Initialize();
            notice.ShowOpponentDeparture();
            int closeRequests = 0;
            windowManager.WindowCloseRequested += _ => closeRequests++;

            Assert.IsTrue(cancelStack.TryCancel());
            Assert.IsTrue(windowManager.TryCancel());

            Assert.IsTrue(notice.IsPending);
            Assert.IsTrue(GetDialog().gameObject.activeSelf);
            Assert.AreEqual(0, closeRequests);
            Assert.AreEqual(0, navigationCount);
        }

        [Test]
        public void Confirmed_AcceptedNavigation_EndsGameOnceAndClearsNoticeAfterAcceptance()
        {
            Initialize();
            notice.ShowOpponentDeparture();
            Button.ButtonClickedEvent click = GetButton(GetDialog(), "ConfirmButtonImage").onClick;

            click.Invoke();
            click.Invoke();

            Assert.AreEqual(1, endCount);
            Assert.AreEqual(1, navigationCount);
            Assert.IsTrue(visibleDuringNavigation);
            Assert.IsFalse(notice.IsPending);
            Assert.IsEmpty(windowManager.Windows);
            Assert.IsFalse(controller.TryCancel());
            Assert.IsFalse(cancelStack.TryCancel());
            Assert.AreEqual(1, releaseCount);
        }

        [Test]
        public void Submit_VisibleNotice_AcknowledgesAndRequestsMainMenu()
        {
            Initialize();
            notice.ShowOpponentDeparture();
            GameObject selected = EventSystem.current.currentSelectedGameObject;

            ExecuteEvents.Execute(
                selected,
                new BaseEventData(EventSystem.current),
                ExecuteEvents.submitHandler
            );

            Assert.AreEqual(1, navigationCount);
            Assert.IsFalse(notice.IsPending);
        }

        [Test]
        public void Confirmed_MainMenuWithExistingModal_AcknowledgesAndRestoresUnderlyingModal()
        {
            UIWindow underlying = CreateUnderlyingWindow(true);
            Initialize(true);
            notice.ShowOpponentDeparture();
            Assert.IsFalse(windowManager.CanInteractWithWindow(underlying));

            GetButton(GetDialog(), "ConfirmButtonImage").onClick.Invoke();

            Assert.AreEqual(0, endCount);
            Assert.AreEqual(0, navigationCount);
            Assert.IsFalse(notice.IsPending);
            Assert.AreEqual(1, windowManager.Windows.Count);
            Assert.AreSame(underlying, windowManager.ActiveWindow);
            Assert.IsTrue(windowManager.CanInteractWithWindow(underlying));
            Assert.AreEqual(1, releaseCount);
        }

        [Test]
        public void Confirmed_RejectedNavigation_RetainsNoticeAndAllowsRetry()
        {
            Initialize();
            notice.ShowOpponentDeparture();
            navigationAccepted = false;
            ConfirmationDialogView dialog = GetDialog();

            GetButton(dialog, "ConfirmButtonImage").onClick.Invoke();

            Assert.IsTrue(notice.IsPending);
            Assert.AreSame(dialog, GetDialog());
            Assert.AreEqual(0, releaseCount);
            navigationAccepted = true;
            GetButton(dialog, "ConfirmButtonImage").onClick.Invoke();

            Assert.AreEqual(2, navigationCount);
            Assert.IsFalse(notice.IsPending);
        }

        [Test]
        public void Confirmed_LoadFailure_RestoresNoticeAndAllowsRetry()
        {
            Initialize();
            notice.ShowOpponentDeparture();
            ConfirmationDialogView original = GetDialog();
            GetButton(original, "ConfirmButtonImage").onClick.Invoke();
            Assert.IsFalse(original.gameObject.activeSelf);
            LogAssert.Expect(LogType.Exception, new Regex("Menu loading failed\\."));

            failures[0](new IOException("Menu loading failed."));

            Assert.IsTrue(notice.IsPending);
            Assert.AreSame(original, GetDialog());
            Assert.IsTrue(GetDialog().gameObject.activeSelf);
            Assert.AreEqual(2, suppressionCount);
            GetButton(GetDialog(), "ConfirmButtonImage").onClick.Invoke();
            Assert.AreEqual(2, navigationCount);
            Assert.IsFalse(notice.IsPending);
        }

        [Test]
        public void Confirmed_SynchronousLoadFailure_RetainsVisibleNotice()
        {
            Initialize();
            notice.ShowOpponentDeparture();
            synchronousFailure = new IOException("Menu loading failed.");
            LogAssert.Expect(LogType.Exception, new Regex("Menu loading failed\\."));

            GetButton(GetDialog(), "ConfirmButtonImage").onClick.Invoke();

            Assert.IsTrue(notice.IsPending);
            Assert.IsTrue(GetDialog().gameObject.activeSelf);
            Assert.AreEqual(0, releaseCount);
        }

        [Test]
        public void Confirmed_OlderFailureAfterRetry_DoesNotRestoreAcknowledgedNotice()
        {
            Initialize();
            notice.ShowOpponentDeparture();
            GetButton(GetDialog(), "ConfirmButtonImage").onClick.Invoke();
            LogAssert.Expect(LogType.Exception, new Regex("Menu loading failed\\."));
            failures[0](new IOException("Menu loading failed."));
            GetButton(GetDialog(), "ConfirmButtonImage").onClick.Invoke();

            failures[0](new IOException("Stale loading callback."));

            Assert.IsFalse(notice.IsPending);
            Assert.IsEmpty(windowManager.Windows);
        }

        [Test]
        public void Dispose_PendingNotice_ReleasesSceneOwnershipWithoutAcknowledging()
        {
            Initialize();
            notice.ShowOpponentDeparture();

            controller.Dispose();
            controller.Dispose();

            Assert.IsTrue(notice.IsPending);
            Assert.IsEmpty(windowManager.Windows);
            Assert.IsFalse(cancelStack.TryCancel());
            Assert.AreEqual(1, releaseCount);
            notice.Acknowledge();
            notice.ShowOpponentDeparture();
            Assert.IsEmpty(windowManager.Windows);
            Assert.AreEqual(1, suppressionCount);
        }

        [Test]
        public void Dispose_AcceptedTransition_IgnoresLaterFailureCallback()
        {
            Initialize();
            notice.ShowOpponentDeparture();
            GetButton(GetDialog(), "ConfirmButtonImage").onClick.Invoke();
            controller.Dispose();

            failures[0](new IOException("The outgoing scene is gone."));

            Assert.IsFalse(notice.IsPending);
            Assert.IsEmpty(windowManager.Windows);
        }

        [Test]
        public void Dispose_AcknowledgedCachedDialog_DestroysControlAndReleasesCallbacks()
        {
            Initialize();
            notice.ShowOpponentDeparture();
            ConfirmationDialogView original = GetDialog();
            GetButton(original, "ConfirmButtonImage").onClick.Invoke();
            Assert.IsFalse(original.gameObject.activeSelf);

            controller.Dispose();

            Assert.IsTrue(original == null);
            Assert.IsFalse(notice.IsPending);
            Assert.IsEmpty(windowManager.Windows);
            Assert.AreEqual(1, releaseCount);
            notice.ShowOpponentDeparture();
            Assert.IsEmpty(windowManager.Windows);
        }

        /// <summary>
        /// Connects the controller to observable navigation and input-ownership dependencies.
        /// </summary>
        /// <param name="alreadyInMainMenu">Whether acknowledgment should dismiss without navigation.</param>
        private void Initialize(bool alreadyInMainMenu = false)
        {
            controller.Initialize(
                notice,
                windowManager,
                template,
                alreadyInMainMenu,
                () => endCount++,
                failure =>
                {
                    navigationCount++;
                    failures.Add(failure);
                    visibleDuringNavigation = GetDialog().gameObject.activeSelf && notice.IsPending;
                    if (synchronousFailure != null)
                        failure(synchronousFailure);
                    return navigationAccepted;
                },
                () =>
                {
                    suppressionCount++;
                    return new ReleaseCallback(() => releaseCount++);
                },
                cancelStack,
                new TestContentAssets(texture)
            );
        }

        /// <summary>
        /// Resolves the native dialog currently owning the modal window registry.
        /// </summary>
        /// <returns>The active registered confirmation view.</returns>
        private ConfirmationDialogView GetDialog()
        {
            Assert.IsTrue(
                windowManager.ActiveWindow.TryGetContent(out ConfirmationDialogView dialog)
            );
            return dialog;
        }

        /// <summary>
        /// Registers another scene window to exercise acknowledgment input precedence.
        /// </summary>
        /// <param name="modal">Whether the competing window also requests modal ownership.</param>
        /// <returns>The registered competing window.</returns>
        private UIWindow CreateUnderlyingWindow(bool modal)
        {
            GameObject windowObject = new GameObject(
                "Underlying window",
                typeof(RectTransform),
                typeof(UIWindow)
            );
            windowObject.transform.SetParent(root.transform, false);
            UIWindow window = windowObject.GetComponent<UIWindow>();
            window.Configure(100, 0, 0, 640, 480, modal, true, false);
            windowManager.Register(window, false);
            return window;
        }

        /// <summary>
        /// Finds an authored response control through the dialog's visible hierarchy.
        /// </summary>
        /// <param name="dialog">The dialog containing the response control.</param>
        /// <param name="name">The authored response-control object name.</param>
        /// <returns>The response button exposed by the authored hierarchy.</returns>
        private static Button GetButton(ConfirmationDialogView dialog, string name)
        {
            return dialog.transform.Find("DialogSurface/" + name).GetComponent<Button>();
        }

        private sealed class ReleaseCallback : IDisposable
        {
            private readonly Action release;

            /// <summary>
            /// Retains the observable release action for an injected shortcut-ownership lease.
            /// </summary>
            /// <param name="release">The action recording release of the lease.</param>
            public ReleaseCallback(Action release)
            {
                this.release = release;
            }

            /// <summary>
            /// Records release of the injected shortcut-ownership lease.
            /// </summary>
            public void Dispose()
            {
                release();
            }
        }

        private sealed class TestContentAssets : IContentAssetSource
        {
            private readonly Texture2D texture;

            /// <summary>
            /// Supplies shared test artwork for the dialog's installed-content bindings.
            /// </summary>
            /// <param name="texture">The texture returned for every requested artwork address.</param>
            public TestContentAssets(Texture2D texture)
            {
                this.texture = texture;
            }

            /// <summary>
            /// Resolves dialog artwork to the fixture's injected texture.
            /// </summary>
            /// <param name="address">The requested installed-content texture address.</param>
            /// <returns>The fixture's injected artwork texture.</returns>
            public Texture2D GetTexture(string address) => texture;

            /// <summary>
            /// Leaves sprite resolution unavailable to this texture-only dialog fixture.
            /// </summary>
            /// <param name="address">The requested installed-content sprite address.</param>
            /// <returns>Null since the fixture supplies no sprite artwork.</returns>
            public Sprite GetSprite(string address) => null;

            /// <summary>
            /// Leaves bordered-sprite resolution unavailable to this texture-only dialog fixture.
            /// </summary>
            /// <param name="address">The requested installed-content sprite address.</param>
            /// <param name="border">The requested sprite border in source pixels.</param>
            /// <returns>Null since the fixture supplies no sprite artwork.</returns>
            public Sprite GetSprite(string address, Vector4 border) => null;
        }
    }
}
