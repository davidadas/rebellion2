using System;
using System.Reflection;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Rebellion.Tests.UI.SceneUI.SaveMenu.Presentation
{
    [TestFixture]
    public class SaveMenuSlotRowViewTests
    {
        private const string _prefabPath = "Assets/Prefabs/UI/SaveMenu/SaveSlotRow.prefab";

        private Texture2D _factionTexture;
        private GameObject _rootObject;
        private SaveMenuSlotRowView _view;

        [SetUp]
        public void SetUp()
        {
            _rootObject = UIComponentTestHelper.InstantiatePrefab(_prefabPath);
            _view = _rootObject.GetComponent<SaveMenuSlotRowView>();
            _factionTexture = new Texture2D(24, 24);
        }

        [TearDown]
        public void TearDown()
        {
            UnityEngine.Object.DestroyImmediate(_factionTexture);
            UnityEngine.Object.DestroyImmediate(_rootObject);
        }

        [Test]
        public void Render_EnabledSlot_AppliesFactionNameButtonsAndNormalTextures()
        {
            _view.Render(new SaveSlotRenderData(2, "Campaign", true, true, _factionTexture));

            RawImage factionImage = GetField<RawImage>("factionImage");
            Assert.AreSame(_factionTexture, factionImage.texture);
            Assert.IsTrue(factionImage.enabled);
            Assert.AreEqual("Campaign", GetField<TMP_InputField>("nameInputField").text);
            Assert.IsFalse(GetField<TMP_InputField>("nameInputField").readOnly);
            Assert.IsTrue(GetField<Button>("saveButton").interactable);
            Assert.IsTrue(GetField<Button>("loadButton").interactable);
            Assert.AreSame(
                GetField<Texture2D>("saveButtonUpTexture"),
                GetPressVisualImage("saveButtonPressVisual").texture
            );
            Assert.AreSame(
                GetField<Texture2D>("loadButtonUpTexture"),
                GetPressVisualImage("loadButtonPressVisual").texture
            );
        }

        [Test]
        public void Render_DisabledEmptySlot_HidesFactionAndAppliesDisabledControls()
        {
            _view.Render(SaveSlotRenderData.Empty(1));

            Assert.IsFalse(GetField<RawImage>("factionImage").enabled);
            Assert.AreEqual(string.Empty, GetField<TMP_InputField>("nameInputField").text);
            Assert.IsTrue(GetField<TMP_InputField>("nameInputField").readOnly);
            Assert.IsFalse(GetField<Button>("saveButton").interactable);
            Assert.IsFalse(GetField<Button>("loadButton").interactable);
            Assert.AreSame(
                GetField<Texture2D>("saveButtonDisabledTexture"),
                GetPressVisualImage("saveButtonPressVisual").texture
            );
            Assert.AreSame(
                GetField<Texture2D>("loadButtonDisabledTexture"),
                GetPressVisualImage("loadButtonPressVisual").texture
            );
        }

        [Test]
        public void Render_UpdatedSameSlot_PreservesInProgressDraft()
        {
            _view.Render(new SaveSlotRenderData(2, "Campaign", true, true, null));
            GetField<TMP_InputField>("nameInputField").text = "Draft Name";

            _view.Render(new SaveSlotRenderData(2, "Saved Elsewhere", true, true, null));

            Assert.AreEqual("Draft Name", GetField<TMP_InputField>("nameInputField").text);
        }

        [Test]
        public void Render_DifferentSlot_ReplacesInProgressDraft()
        {
            _view.Render(new SaveSlotRenderData(2, "Campaign", true, true, null));
            GetField<TMP_InputField>("nameInputField").text = "Draft Name";

            _view.Render(new SaveSlotRenderData(3, "Other Campaign", true, true, null));

            Assert.AreEqual("Other Campaign", GetField<TMP_InputField>("nameInputField").text);
        }

        [Test]
        public void SaveButton_Click_EnabledSlot_RaisesCurrentDraftName()
        {
            _view.Render(new SaveSlotRenderData(2, "Campaign", true, true, null));
            GetField<TMP_InputField>("nameInputField").text = "New Name";
            int requestedSlot = -1;
            string requestedName = null;
            _view.SaveRequested += (slot, name) =>
            {
                requestedSlot = slot;
                requestedName = name;
            };

            GetField<Button>("saveButton").onClick.Invoke();

            Assert.AreEqual(2, requestedSlot);
            Assert.AreEqual("New Name", requestedName);
        }

        [Test]
        public void NameInput_SubmitEnabledSlot_RaisesSubmittedName()
        {
            _view.Render(new SaveSlotRenderData(2, "Campaign", true, true, null));
            int requestedSlot = -1;
            string requestedName = null;
            _view.SaveRequested += (slot, name) =>
            {
                requestedSlot = slot;
                requestedName = name;
            };

            GetField<TMP_InputField>("nameInputField").onSubmit.Invoke("Submitted Name");

            Assert.AreEqual(2, requestedSlot);
            Assert.AreEqual("Submitted Name", requestedName);
        }

        [Test]
        public void LoadButton_Click_LoadableSlot_RaisesSlotIndex()
        {
            _view.Render(new SaveSlotRenderData(4, "Campaign", true, true, null));
            int requestedSlot = -1;
            _view.LoadRequested += slot => requestedSlot = slot;

            GetField<Button>("loadButton").onClick.Invoke();

            Assert.AreEqual(4, requestedSlot);
        }

        [Test]
        public void DisabledControls_Invoke_DoNotRaiseRequests()
        {
            _view.Render(SaveSlotRenderData.Empty(1));
            int saveCount = 0;
            int loadCount = 0;
            _view.SaveRequested += (_, _) => saveCount++;
            _view.LoadRequested += _ => loadCount++;

            GetField<Button>("saveButton").onClick.Invoke();
            GetField<Button>("loadButton").onClick.Invoke();
            GetField<TMP_InputField>("nameInputField").onSubmit.Invoke("Name");

            Assert.AreEqual(0, saveCount);
            Assert.AreEqual(0, loadCount);
        }

        [Test]
        public void OnDisable_BoundRow_UnbindsAllControls()
        {
            _view.Render(new SaveSlotRenderData(2, "Campaign", true, true, null));
            int saveCount = 0;
            int loadCount = 0;
            _view.SaveRequested += (_, _) => saveCount++;
            _view.LoadRequested += _ => loadCount++;

            UIComponentTestHelper.InvokeLifecycle(_view, "OnDisable");
            GetField<Button>("saveButton").onClick.Invoke();
            GetField<Button>("loadButton").onClick.Invoke();
            GetField<TMP_InputField>("nameInputField").onSubmit.Invoke("Name");

            Assert.AreEqual(0, saveCount);
            Assert.AreEqual(0, loadCount);
        }

        private RawImage GetPressVisualImage(string fieldName)
        {
            RawImagePressVisual visual = GetField<RawImagePressVisual>(fieldName);
            return (RawImage)
                typeof(RawImagePressVisual)
                    .GetField("image", BindingFlags.Instance | BindingFlags.NonPublic)
                    .GetValue(visual);
        }

        private T GetField<T>(string fieldName)
        {
            return (T)
                typeof(SaveMenuSlotRowView)
                    .GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic)
                    .GetValue(_view);
        }
    }
}
