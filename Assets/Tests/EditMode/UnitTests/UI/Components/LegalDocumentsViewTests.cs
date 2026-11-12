using System.Collections;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Rebellion.Tests.UI.Components
{
    [TestFixture]
    public sealed class LegalDocumentsViewTests
    {
        private GameObject _root;
        private GameObject _eventSystemObject;
        private LegalDocumentsView _view;
        private Button _accept;
        private Button _close;
        private Button _privacy;

        /// <summary>
        /// Instantiates the authored reader in a live canvas with keyboard selection available.
        /// </summary>
        /// <returns>The play-mode setup sequence.</returns>
        [UnitySetUp]
        public IEnumerator SetUp()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            yield return new EnterPlayMode();
            _eventSystemObject = new GameObject("TestEventSystem", typeof(EventSystem));
            _root = new GameObject("TestCanvas", typeof(Canvas), typeof(CanvasScaler));
            _root.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            CanvasScaler scaler = _root.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(853.33f, 480f);
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/Prefabs/UI/MainMenu/MainMenuRoot.prefab"
            );
            LegalDocumentsView template = prefab.GetComponentInChildren<LegalDocumentsView>(true);
            _view = Object.Instantiate(template, _root.transform);
            _accept = _view
                .transform.Find("DocumentPanel/Console/AcceptButton")
                .GetComponent<Button>();
            _close = _view
                .transform.Find("DocumentPanel/Console/CloseButton")
                .GetComponent<Button>();
            _privacy = _view
                .transform.Find("DocumentPanel/Console/PrivacyButton")
                .GetComponent<Button>();
        }

        /// <summary>
        /// Removes the reader and restores edit mode after each interaction test.
        /// </summary>
        /// <returns>The play-mode cleanup sequence.</returns>
        [UnityTearDown]
        public IEnumerator TearDown()
        {
            Object.Destroy(_root);
            Object.Destroy(_eventSystemObject);
            yield return null;
            yield return new ExitPlayMode();
        }

        [Test]
        public void TryCancel_RequiredAcceptance_KeepsPromptOpen()
        {
            int accepted = 0;
            _view.Accepted += () => accepted++;
            _view.Show(true);

            Assert.IsTrue(_view.TryCancel());
            Assert.IsTrue(_view.IsVisible);
            Assert.AreEqual(0, accepted);
        }

        [Test]
        public void Show_ReadOnly_HidesAcceptanceButton()
        {
            _view.Show(false);

            Assert.IsFalse(_accept.gameObject.activeSelf);
            Assert.AreEqual("CLOSE", _close.GetComponentInChildren<TMP_Text>().text);
        }

        [Test]
        public void Show_PrivacySelected_DoesNotAcceptAgreement()
        {
            int accepted = 0;
            _view.Accepted += () => accepted++;
            _view.Show(true);

            _privacy.onClick.Invoke();

            Assert.AreEqual(0, accepted);
            Assert.AreEqual(
                "PRIVACY NOTICE",
                _view.transform.Find("DocumentPanel/Console/Heading").GetComponent<TMP_Text>().text
            );
        }

        [Test]
        public void Show_BothDocumentsAgreed_EmitsAcceptanceWithoutClosingEarly()
        {
            int accepted = 0;
            _view.Accepted += () => accepted++;
            _view.Show(true);

            _accept.onClick.Invoke();
            Assert.AreEqual(0, accepted);
            _accept.onClick.Invoke();

            Assert.AreEqual(1, accepted);
            Assert.IsTrue(_view.IsVisible);
        }

        [Test]
        public void Show_QuitPressed_RequestsExitWithoutAccepting()
        {
            int accepted = 0;
            int quit = 0;
            _view.Accepted += () => accepted++;
            _view.QuitRequested += () => quit++;
            _view.Show(true);

            _close.onClick.Invoke();

            Assert.AreEqual(1, quit);
            Assert.AreEqual(0, accepted);
        }

        [Test]
        public void TryCancel_ReadOnly_RestoresPreviousSelection()
        {
            GameObject previous = new GameObject(
                "PreviousSelection",
                typeof(RectTransform),
                typeof(Button)
            );
            previous.transform.SetParent(_root.transform, false);
            EventSystem.current.SetSelectedGameObject(previous);
            _view.Show(false);

            _view.TryCancel();

            Assert.IsFalse(_view.IsVisible);
            Assert.AreSame(previous, EventSystem.current.currentSelectedGameObject);
        }

        [Test]
        public void Show_LongDocument_ProvidesScrollableFullText()
        {
            _view.Show(true);
            Canvas.ForceUpdateCanvases();
            ScrollAreaView scroll = _view.GetComponentInChildren<ScrollAreaView>();

            Assert.Greater(scroll.ContentRoot.rect.height, scroll.ViewportHeight);
            Assert.AreEqual(1f, scroll.VerticalNormalizedPosition);
            Assert.AreEqual(
                _view.Agreement.Text,
                scroll.ContentRoot.GetComponentInChildren<TMP_Text>().text
            );
        }

        [Test]
        public void ShowMultiplayerAgreement_Opened_DisplaysAgreementWithAgreeAndBack()
        {
            _view.ShowMultiplayerAgreement();

            Assert.AreEqual(
                _view.Agreement.Text,
                _view
                    .GetComponentInChildren<ScrollAreaView>()
                    .ContentRoot.GetComponentInChildren<TMP_Text>()
                    .text
            );
            Assert.AreEqual("AGREE", _accept.GetComponentInChildren<TMP_Text>().text);
            Assert.AreEqual("BACK", _close.GetComponentInChildren<TMP_Text>().text);
        }

        [Test]
        public void ShowMultiplayerAgreement_BothDocumentsAgreed_EntersMultiplayerOnce()
        {
            int continued = 0;
            int accepted = 0;
            _view.MultiplayerAccepted += () =>
            {
                continued++;
                _view.Hide();
            };
            _view.Accepted += () => accepted++;
            _view.ShowMultiplayerAgreement();

            _accept.onClick.Invoke();
            Assert.AreEqual(0, continued);
            Assert.IsTrue(_view.IsVisible);
            Assert.AreEqual(
                "PRIVACY NOTICE",
                _view.transform.Find("DocumentPanel/Console/Heading").GetComponent<TMP_Text>().text
            );
            _accept.onClick.Invoke();
            _accept.onClick.Invoke();

            Assert.AreEqual(1, continued);
            Assert.AreEqual(0, accepted);
            Assert.IsFalse(_view.IsVisible);
        }

        [Test]
        public void ShowMultiplayerAgreement_AcceptanceNotSaved_AllowsRetryWithoutAgreeingAgain()
        {
            int attempts = 0;
            _view.MultiplayerAccepted += () =>
            {
                attempts++;
                if (attempts == 1)
                    _view.ShowError("Your acceptance could not be saved. Please try again.");
                else
                    _view.Hide();
            };
            _view.ShowMultiplayerAgreement();

            _accept.onClick.Invoke();
            _accept.onClick.Invoke();

            Assert.IsTrue(_view.IsVisible);
            Assert.AreEqual(1, attempts);
            Assert.AreEqual(
                "Your acceptance could not be saved. Please try again.",
                _view.transform.Find("DocumentPanel/Console/Status").GetComponent<TMP_Text>().text
            );

            _accept.onClick.Invoke();

            Assert.AreEqual(2, attempts);
            Assert.IsFalse(_view.IsVisible);
        }

        [Test]
        public void ShowMultiplayerAgreement_BackPressed_ClosesWithoutAcceptingOrQuitting()
        {
            int continued = 0;
            int quit = 0;
            int closed = 0;
            _view.MultiplayerAccepted += () => continued++;
            _view.QuitRequested += () => quit++;
            _view.Closed += () => closed++;
            _view.ShowMultiplayerAgreement();

            _close.onClick.Invoke();

            Assert.IsFalse(_view.IsVisible);
            Assert.AreEqual(1, closed);
            Assert.AreEqual(0, continued);
            Assert.AreEqual(0, quit);
        }

        [Test]
        public void TryCancel_MultiplayerAgreement_ClosesWithoutAccepting()
        {
            int continued = 0;
            int closed = 0;
            _view.MultiplayerAccepted += () => continued++;
            _view.Closed += () => closed++;
            _view.ShowMultiplayerAgreement();

            Assert.IsTrue(_view.TryCancel());

            Assert.IsFalse(_view.IsVisible);
            Assert.AreEqual(1, closed);
            Assert.AreEqual(0, continued);
        }

        [Test]
        public void ShowMultiplayerAgreement_Reopened_RequiresBothDocumentsAgain()
        {
            int accepted = 0;
            _view.MultiplayerAccepted += () =>
            {
                accepted++;
                _view.Hide();
            };
            _view.ShowMultiplayerAgreement();
            _accept.onClick.Invoke();
            _accept.onClick.Invoke();

            _view.ShowMultiplayerAgreement();
            _accept.onClick.Invoke();

            Assert.IsTrue(_view.IsVisible);
            Assert.AreEqual(1, accepted);
            Assert.AreEqual(
                "PRIVACY NOTICE",
                _view.transform.Find("DocumentPanel/Console/Heading").GetComponent<TMP_Text>().text
            );
        }

        [Test]
        public void Show_RequiredAfterMultiplayerAgreement_RestoresAgreementActions()
        {
            int continued = 0;
            int accepted = 0;
            _view.MultiplayerAccepted += () => continued++;
            _view.Accepted += () => accepted++;
            _view.ShowMultiplayerAgreement();
            _view.TryCancel();

            _view.Show(true);
            _accept.onClick.Invoke();
            _accept.onClick.Invoke();

            Assert.AreEqual("AGREE", _accept.GetComponentInChildren<TMP_Text>().text);
            Assert.AreEqual("QUIT", _close.GetComponentInChildren<TMP_Text>().text);
            Assert.AreEqual(1, accepted);
            Assert.AreEqual(0, continued);
            Assert.IsTrue(_view.IsVisible);
        }

        [Test]
        public void Show_PrivacyAgreedFirst_StillRequiresPlayerAgreement()
        {
            int accepted = 0;
            _view.Accepted += () => accepted++;
            _view.Show(true);
            _privacy.onClick.Invoke();

            _accept.onClick.Invoke();

            Assert.AreEqual(0, accepted);
            Assert.AreEqual(
                "PLAYER AGREEMENT",
                _view.transform.Find("DocumentPanel/Console/Heading").GetComponent<TMP_Text>().text
            );
            _accept.onClick.Invoke();
            Assert.AreEqual(1, accepted);
        }
    }
}
