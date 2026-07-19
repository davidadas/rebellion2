using System;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Rebellion.Tests.UI.SceneUI.SaveMenu
{
    [TestFixture]
    public class SaveMenuSceneControllerTests
    {
        private const string _prefabPath = "Assets/Prefabs/UI/SaveMenu/SaveMenuRoot.prefab";

        private GameObject _rootObject;
        private SaveMenuSceneController _controller;

        [SetUp]
        public void SetUp()
        {
            _rootObject = PrefabUtility.LoadPrefabContents(_prefabPath);
            if (_rootObject == null)
                throw new InvalidOperationException($"Missing test prefab at {_prefabPath}.");

            _controller = _rootObject.GetComponent<SaveMenuSceneController>();
        }

        [TearDown]
        public void TearDown()
        {
            PrefabUtility.UnloadPrefabContents(_rootObject);
        }

        [Test]
        public void VerifyReferences_AuthoredPrefab_DoesNotThrow()
        {
            Assert.DoesNotThrow(() =>
                UIComponentTestHelper.InvokeLifecycle(_controller, "VerifyReferences")
            );
        }

        [Test]
        public void VerifyReferences_MissingContentHost_ThrowsMissingReferenceException()
        {
            SetField("contentHost", null);

            MissingReferenceException exception = Assert.Throws<MissingReferenceException>(() =>
                UIComponentTestHelper.InvokeLifecycle(_controller, "VerifyReferences")
            );

            Assert.AreEqual("ContentHost is missing.", exception.Message);
        }

        [Test]
        public void VerifyReferences_MissingWindow_ThrowsMissingReferenceException()
        {
            SetField("saveMenuWindow", null);

            MissingReferenceException exception = Assert.Throws<MissingReferenceException>(() =>
                UIComponentTestHelper.InvokeLifecycle(_controller, "VerifyReferences")
            );

            Assert.AreEqual("SaveMenuWindow is missing.", exception.Message);
        }

        private void SetField(string fieldName, object value)
        {
            typeof(SaveMenuSceneController)
                .GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(_controller, value);
        }
    }
}
