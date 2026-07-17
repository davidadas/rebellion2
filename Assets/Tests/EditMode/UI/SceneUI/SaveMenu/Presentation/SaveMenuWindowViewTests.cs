using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Rebellion.Tests.UI.SceneUI.SaveMenu.Presentation
{
    [TestFixture]
    public class SaveMenuWindowViewTests
    {
        private const string _prefabPath = "Assets/Prefabs/UI/SaveMenu/SaveMenuWindow.prefab";

        private readonly List<Texture2D> _textures = new List<Texture2D>();
        private GameObject _rootObject;
        private GameObject _viewportObject;
        private SaveMenuWindowView _view;

        [SetUp]
        public void SetUp()
        {
            _rootObject = UIComponentTestHelper.InstantiatePrefab(_prefabPath);
            _view = _rootObject.GetComponent<SaveMenuWindowView>();
        }

        [TearDown]
        public void TearDown()
        {
            foreach (Texture2D texture in _textures)
                UnityEngine.Object.DestroyImmediate(texture);

            if (_rootObject != null)
                UnityEngine.Object.DestroyImmediate(_rootObject);
            if (_viewportObject != null)
                UnityEngine.Object.DestroyImmediate(_viewportObject);
        }

        [Test]
        public void VerifyReferences_AuthoredPrefab_DoesNotThrow()
        {
            Assert.DoesNotThrow(_view.VerifyReferences);
        }

        [Test]
        public void Render_NullData_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() => _view.Render(null));
        }

        [Test]
        public void Render_CompleteData_AppliesWindowAndChildPresentation()
        {
            Texture2D returnUpTexture = CreateTexture("Return Up");
            Texture2D returnDownTexture = CreateTexture("Return Down");
            SaveMenuWindowRenderData data = CreateRenderData(
                returnUpTexture,
                returnDownTexture,
                0.5f,
                0.25f,
                "Version 2",
                "Exit?",
                1
            );

            _view.Render(data);

            Assert.AreSame(
                returnUpTexture,
                GetPressVisualImage(
                    GetField<RawImagePressVisual>(_view, "returnStrategyButtonPressVisual")
                ).texture
            );
            Assert.AreEqual("ON", GetField<TextMeshProUGUI>(_view, "playMusicStateTextField").text);
            Assert.AreEqual("Version 2", GetField<TextMeshProUGUI>(_view, "versionTextField").text);
            Assert.AreEqual(
                0.5f,
                GetSliderValue(GetField<SaveMenuSliderView>(_view, "musicSlider"))
            );
            Assert.AreEqual(
                0.25f,
                GetSliderValue(GetField<SaveMenuSliderView>(_view, "sfxSlider"))
            );

            SaveMenuTacticalOptionRowView[] tacticalRows =
                GetField<SaveMenuTacticalOptionRowView[]>(_view, "tacticalOptionRows");
            foreach (SaveMenuTacticalOptionRowView row in tacticalRows)
            {
                bool expected = data.TacticalOptions[row.Option];
                Assert.AreEqual(
                    expected ? "ON" : "OFF",
                    GetField<TextMeshProUGUI>(row, "stateTextField").text
                );
            }

            SaveMenuSlotRowView[] slotRows = GetField<SaveMenuSlotRowView[]>(_view, "saveSlotRows");
            Assert.AreEqual(
                "Campaign 1",
                GetField<TMP_InputField>(slotRows[0], "nameInputField").text
            );
            Assert.IsFalse(GetField<TMP_InputField>(slotRows[0], "nameInputField").readOnly);
            Assert.AreEqual(
                string.Empty,
                GetField<TMP_InputField>(slotRows[1], "nameInputField").text
            );
            Assert.IsTrue(GetField<TMP_InputField>(slotRows[1], "nameInputField").readOnly);

            SaveMenuConfirmDialogView confirmDialog = GetField<SaveMenuConfirmDialogView>(
                _view,
                "confirmDialog"
            );
            Assert.IsTrue(confirmDialog.gameObject.activeSelf);
            Assert.AreEqual(
                "Exit?",
                GetField<TextMeshProUGUI>(confirmDialog, "messageTextField").text
            );
        }

        [Test]
        public void Render_NoCustomReturnTexture_UsesAuthoredNormalTexture()
        {
            Texture2D authoredTexture = GetField<Texture2D>(_view, "returnStrategyButtonUpTexture");

            _view.Render(CreateRenderData(null, null, 0f, 0f, null, null, 0));

            Assert.AreSame(
                authoredTexture,
                GetPressVisualImage(
                    GetField<RawImagePressVisual>(_view, "returnStrategyButtonPressVisual")
                ).texture
            );
        }

        [Test]
        public void Render_MissingTacticalOption_ThrowsInvalidOperationException()
        {
            SaveMenuWindowRenderData data = new SaveMenuWindowRenderData(
                null,
                null,
                0f,
                0f,
                null,
                new Dictionary<UserTacticalOption, bool>(),
                Array.Empty<SaveSlotRenderData>(),
                null
            );

            Assert.Throws<InvalidOperationException>(() => _view.Render(data));
        }

        [Test]
        public void Render_EmptyConfirmation_HidesVisibleDialog()
        {
            _view.Render(CreateRenderData(null, null, 0f, 0f, null, "Exit?", 0));

            _view.Render(CreateRenderData(null, null, 0f, 0f, null, null, 0));

            Assert.IsFalse(
                GetField<SaveMenuConfirmDialogView>(_view, "confirmDialog").gameObject.activeSelf
            );
        }

        [Test]
        public void CommandButtons_Click_RaiseSemanticRequests()
        {
            _view.Render(CreateRenderData(null, null, 0f, 0f, null, null, 0));
            int cockpitCount = 0;
            int exitCount = 0;
            int strategyCount = 0;
            int musicCount = 0;
            _view.ReturnCockpitRequested += () => cockpitCount++;
            _view.ExitRequested += () => exitCount++;
            _view.ReturnStrategyRequested += () => strategyCount++;
            _view.MusicToggleRequested += () => musicCount++;

            GetField<Button>(_view, "cockpitButton").onClick.Invoke();
            GetField<Button>(_view, "exitButton").onClick.Invoke();
            GetField<Button>(_view, "returnStrategyButton").onClick.Invoke();
            GetField<Button>(_view, "musicButton").onClick.Invoke();

            Assert.AreEqual(1, cockpitCount);
            Assert.AreEqual(1, exitCount);
            Assert.AreEqual(1, strategyCount);
            Assert.AreEqual(1, musicCount);
        }

        [Test]
        public void ChildControls_Change_ForwardSemanticRequests()
        {
            _view.Render(CreateRenderData(null, null, 0f, 0f, null, "Exit?", 1));
            float musicVolume = -1f;
            float sfxVolume = -1f;
            UserTacticalOption? tacticalOption = null;
            int saveSlot = -1;
            string saveName = null;
            int loadSlot = -1;
            int acceptedCount = 0;
            int canceledCount = 0;
            _view.MusicVolumeChanged += value => musicVolume = value;
            _view.SfxVolumeChanged += value => sfxVolume = value;
            _view.TacticalOptionToggleRequested += option => tacticalOption = option;
            _view.SaveRequested += (slot, name) =>
            {
                saveSlot = slot;
                saveName = name;
            };
            _view.LoadRequested += slot => loadSlot = slot;
            _view.ConfirmationAccepted += () => acceptedCount++;
            _view.ConfirmationCanceled += () => canceledCount++;

            SetSliderValue(GetField<SaveMenuSliderView>(_view, "musicSlider"), 0.75f);
            SetSliderValue(GetField<SaveMenuSliderView>(_view, "sfxSlider"), 0.5f);
            SaveMenuTacticalOptionRowView tacticalRow = GetField<SaveMenuTacticalOptionRowView[]>(
                _view,
                "tacticalOptionRows"
            )[0];
            GetField<Button>(tacticalRow, "button").onClick.Invoke();
            SaveMenuSlotRowView slotRow = GetField<SaveMenuSlotRowView[]>(_view, "saveSlotRows")[0];
            GetField<TMP_InputField>(slotRow, "nameInputField").text = "Renamed";
            GetField<Button>(slotRow, "saveButton").onClick.Invoke();
            GetField<Button>(slotRow, "loadButton").onClick.Invoke();
            SaveMenuConfirmDialogView confirmDialog = GetField<SaveMenuConfirmDialogView>(
                _view,
                "confirmDialog"
            );
            GetField<Button>(confirmDialog, "confirmButton").onClick.Invoke();
            confirmDialog.Show("Exit?");
            GetField<Button>(confirmDialog, "cancelButton").onClick.Invoke();

            Assert.AreEqual(0.75f, musicVolume);
            Assert.AreEqual(0.5f, sfxVolume);
            Assert.AreEqual(tacticalRow.Option, tacticalOption);
            Assert.AreEqual(0, saveSlot);
            Assert.AreEqual("Renamed", saveName);
            Assert.AreEqual(0, loadSlot);
            Assert.AreEqual(1, acceptedCount);
            Assert.AreEqual(1, canceledCount);
        }

        [Test]
        public void RenderAudioSettings_ChangedValues_UpdatesOnlyAudioPresentation()
        {
            _view.Render(CreateRenderData(null, null, 1f, 0.25f, "Version", null, 1));
            SaveMenuSlotRowView slotRow = GetField<SaveMenuSlotRowView[]>(_view, "saveSlotRows")[0];
            GetField<TMP_InputField>(slotRow, "nameInputField").text = "Draft";

            _view.RenderAudioSettings(0f, 0.8f);

            Assert.AreEqual(
                "OFF",
                GetField<TextMeshProUGUI>(_view, "playMusicStateTextField").text
            );
            Assert.AreEqual(0f, GetSliderValue(GetField<SaveMenuSliderView>(_view, "musicSlider")));
            Assert.AreEqual(0.8f, GetSliderValue(GetField<SaveMenuSliderView>(_view, "sfxSlider")));
            Assert.AreEqual("Version", GetField<TextMeshProUGUI>(_view, "versionTextField").text);
            Assert.AreEqual("Draft", GetField<TMP_InputField>(slotRow, "nameInputField").text);
        }

        [Test]
        public void OnDisable_BoundWindow_UnsubscribesParentFromControls()
        {
            _view.Render(CreateRenderData(null, null, 0f, 0f, null, "Exit?", 1));
            int requestCount = 0;
            _view.ReturnCockpitRequested += () => requestCount++;
            _view.MusicVolumeChanged += _ => requestCount++;
            _view.TacticalOptionToggleRequested += _ => requestCount++;
            _view.SaveRequested += (_, _) => requestCount++;
            _view.ConfirmationAccepted += () => requestCount++;

            UIComponentTestHelper.InvokeLifecycle(_view, "OnDisable");
            GetField<Button>(_view, "cockpitButton").onClick.Invoke();
            SetSliderValue(GetField<SaveMenuSliderView>(_view, "musicSlider"), 1f);
            SaveMenuTacticalOptionRowView tacticalRow = GetField<SaveMenuTacticalOptionRowView[]>(
                _view,
                "tacticalOptionRows"
            )[0];
            GetField<Button>(tacticalRow, "button").onClick.Invoke();
            SaveMenuSlotRowView slotRow = GetField<SaveMenuSlotRowView[]>(_view, "saveSlotRows")[0];
            GetField<Button>(slotRow, "saveButton").onClick.Invoke();
            SaveMenuConfirmDialogView confirmDialog = GetField<SaveMenuConfirmDialogView>(
                _view,
                "confirmDialog"
            );
            GetField<Button>(confirmDialog, "confirmButton").onClick.Invoke();

            Assert.AreEqual(0, requestCount);
        }

        [Test]
        public void FitWithinViewport_ValidHost_CentersAndUniformlyScalesSourceWindow()
        {
            _viewportObject = new GameObject("Viewport", typeof(RectTransform));
            RectTransform viewport = _viewportObject.GetComponent<RectTransform>();
            viewport.sizeDelta = new Vector2(800f, 600f);
            GameObject contentObject = new GameObject("Content", typeof(RectTransform));
            RectTransform content = contentObject.GetComponent<RectTransform>();
            content.SetParent(viewport, false);
            _view.transform.SetParent(content, false);
            Vector2 sourceSize = ((RectTransform)_view.transform).sizeDelta;
            float expectedScale = Mathf.Min(800f / sourceSize.x, 600f / sourceSize.y);

            _view.FitWithinViewport(content);

            Assert.AreEqual(new Vector2(0.5f, 0.5f), content.anchorMin);
            Assert.AreEqual(new Vector2(0.5f, 0.5f), content.anchorMax);
            Assert.AreEqual(new Vector2(0.5f, 0.5f), content.pivot);
            Assert.AreEqual(Vector2.zero, content.anchoredPosition);
            Assert.AreEqual(sourceSize, content.sizeDelta);
            Assert.AreEqual(new Vector3(expectedScale, expectedScale, 1f), content.localScale);
        }

        [Test]
        public void FitWithinViewport_NullHost_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() => _view.FitWithinViewport(null));
        }

        [Test]
        public void FitWithinViewport_HostWithoutRectParent_ThrowsMissingReferenceException()
        {
            _viewportObject = new GameObject("Content", typeof(RectTransform));
            RectTransform content = _viewportObject.GetComponent<RectTransform>();

            Assert.Throws<MissingReferenceException>(() => _view.FitWithinViewport(content));
        }

        private SaveMenuWindowRenderData CreateRenderData(
            Texture2D returnUpTexture,
            Texture2D returnDownTexture,
            float musicVolume,
            float sfxVolume,
            string version,
            string confirmation,
            int slotCount
        )
        {
            List<SaveSlotRenderData> slots = new List<SaveSlotRenderData>();
            for (int slot = 0; slot < slotCount; slot++)
            {
                slots.Add(new SaveSlotRenderData(slot, $"Campaign {slot + 1}", true, true, null));
            }

            return new SaveMenuWindowRenderData(
                returnUpTexture,
                returnDownTexture,
                musicVolume,
                sfxVolume,
                version,
                CreateTacticalOptions(),
                slots,
                confirmation
            );
        }

        private Texture2D CreateTexture(string textureName)
        {
            Texture2D texture = new Texture2D(4, 4) { name = textureName };
            _textures.Add(texture);
            return texture;
        }

        private static IReadOnlyDictionary<UserTacticalOption, bool> CreateTacticalOptions()
        {
            Dictionary<UserTacticalOption, bool> options =
                new Dictionary<UserTacticalOption, bool>();
            foreach (UserTacticalOption option in Enum.GetValues(typeof(UserTacticalOption)))
                options.Add(option, (int)option % 2 == 0);

            return options;
        }

        private static float GetSliderValue(SaveMenuSliderView sliderView)
        {
            return GetField<Slider>(sliderView, "slider").value;
        }

        private static void SetSliderValue(SaveMenuSliderView sliderView, float value)
        {
            GetField<Slider>(sliderView, "slider").value = value;
        }

        private static RawImage GetPressVisualImage(RawImagePressVisual pressVisual)
        {
            return GetField<RawImage>(pressVisual, "image");
        }

        private static T GetField<T>(object target, string fieldName)
        {
            return (T)
                target
                    .GetType()
                    .GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic)
                    .GetValue(target);
        }
    }
}
