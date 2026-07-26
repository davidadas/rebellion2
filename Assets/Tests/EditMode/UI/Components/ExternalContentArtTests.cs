using System;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Rebellion.Tests.UI.Components
{
    [TestFixture]
    public sealed class ExternalContentArtTests
    {
        private string _contentRoot;
        private Sprite _fallbackSprite;
        private Texture2D _fallbackTexture;
        private Sprite _nextAnimationSprite;
        private Texture2D _nextAnimationTexture;
        private GameObject _root;

        [SetUp]
        public void SetUp()
        {
            _contentRoot = Path.Combine(
                Path.GetTempPath(),
                nameof(ExternalContentArtTests),
                Guid.NewGuid().ToString("N")
            );
            Directory.CreateDirectory(Path.Combine(_contentRoot, "Configs"));
            Directory.CreateDirectory(Path.Combine(_contentRoot, "Data"));
            Directory.CreateDirectory(Path.Combine(_contentRoot, "Art", "HD", "UI"));
            ResourceManager.SetContentRootPathForTests(_contentRoot);
        }

        [TearDown]
        public void TearDown()
        {
            if (_root != null)
                UnityEngine.Object.DestroyImmediate(_root);
            if (_fallbackSprite != null)
                UnityEngine.Object.DestroyImmediate(_fallbackSprite);
            if (_fallbackTexture != null)
                UnityEngine.Object.DestroyImmediate(_fallbackTexture);
            if (_nextAnimationSprite != null)
                UnityEngine.Object.DestroyImmediate(_nextAnimationSprite);
            if (_nextAnimationTexture != null)
                UnityEngine.Object.DestroyImmediate(_nextAnimationTexture);

            ResourceManager.SetContentRootPathForTests(null);
            if (Directory.Exists(_contentRoot))
                Directory.Delete(_contentRoot, true);
        }

        [Test]
        public void Awake_ExternalImageExists_ReplacesAuthoredArtImmediately()
        {
            WritePng(Path.Combine(_contentRoot, "Art", "HD", "UI", "mod_image.png"), 3, 2);
            _fallbackTexture = new Texture2D(1, 1) { name = "mod_image" };
            _root = new GameObject("Root", typeof(RectTransform));
            GameObject imageObject = new GameObject(
                "Image",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(RawImage)
            );
            imageObject.transform.SetParent(_root.transform);
            RawImage image = imageObject.GetComponent<RawImage>();
            image.texture = _fallbackTexture;
            RawImagePressVisual pressVisual = imageObject.AddComponent<RawImagePressVisual>();
            SetField(pressVisual, "image", image);
            SetField(pressVisual, "upTexture", _fallbackTexture);
            SetField(pressVisual, "downTexture", _fallbackTexture);
            GameObject spriteObject = new GameObject(
                "Sprite",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image)
            );
            spriteObject.transform.SetParent(_root.transform);
            Image spriteImage = spriteObject.GetComponent<Image>();
            _fallbackSprite = Sprite.Create(
                _fallbackTexture,
                new Rect(0f, 0f, 1f, 1f),
                new Vector2(0.5f, 0.5f)
            );
            spriteImage.sprite = _fallbackSprite;
            ExternalContentArt overrides = _root.AddComponent<ExternalContentArt>();

            UIComponentTestHelper.InvokeLifecycle(overrides, "Awake");

            Assert.AreNotSame(_fallbackTexture, image.texture);
            Assert.AreEqual(3, image.texture.width);
            Assert.AreEqual(2, image.texture.height);
            Assert.AreNotSame(_fallbackSprite, spriteImage.sprite);
            Assert.AreSame(image.texture, spriteImage.sprite.texture);
            Assert.AreSame(image.texture, GetField<Texture>(pressVisual, "upTexture"));
            Assert.AreSame(image.texture, GetField<Texture>(pressVisual, "downTexture"));
        }

        [Test]
        public void Awake_NestedOverrideRoot_LeavesChildArtToChildOverride()
        {
            WritePng(Path.Combine(_contentRoot, "Art", "HD", "UI", "mod_image.png"), 3, 2);
            _fallbackTexture = new Texture2D(1, 1) { name = "mod_image" };
            _root = new GameObject("Root", typeof(RectTransform));
            GameObject childRoot = new GameObject("ChildRoot", typeof(RectTransform));
            childRoot.transform.SetParent(_root.transform);
            GameObject imageObject = new GameObject(
                "Image",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(RawImage)
            );
            imageObject.transform.SetParent(childRoot.transform);
            RawImage image = imageObject.GetComponent<RawImage>();
            image.texture = _fallbackTexture;
            ExternalContentArt childOverrides = childRoot.AddComponent<ExternalContentArt>();
            ExternalContentArt parentOverrides = _root.AddComponent<ExternalContentArt>();

            UIComponentTestHelper.InvokeLifecycle(parentOverrides, "Awake");

            Assert.AreSame(_fallbackTexture, image.texture);

            UIComponentTestHelper.InvokeLifecycle(childOverrides, "Awake");

            Assert.AreNotSame(_fallbackTexture, image.texture);
        }

        [Test]
        public void LateUpdate_AnimatorSelectsFrame_AppliesMatchingExternalArt()
        {
            WritePng(Path.Combine(_contentRoot, "Art", "HD", "UI", "mod_animation_00.png"), 2, 2);
            WritePng(Path.Combine(_contentRoot, "Art", "HD", "UI", "mod_animation_01.png"), 3, 2);
            _fallbackTexture = new Texture2D(1, 1) { name = "mod_animation_00" };
            _fallbackSprite = Sprite.Create(
                _fallbackTexture,
                new Rect(0f, 0f, 1f, 1f),
                new Vector2(0.5f, 0.5f)
            );
            _nextAnimationTexture = new Texture2D(1, 1) { name = "mod_animation_01" };
            _nextAnimationSprite = Sprite.Create(
                _nextAnimationTexture,
                new Rect(0f, 0f, 1f, 1f),
                new Vector2(0.5f, 0.5f)
            );
            _root = new GameObject("Root", typeof(RectTransform));
            GameObject imageObject = new GameObject(
                "Image",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image),
                typeof(Animator)
            );
            imageObject.transform.SetParent(_root.transform);
            Image image = imageObject.GetComponent<Image>();
            image.sprite = _fallbackSprite;
            Animator animator = imageObject.GetComponent<Animator>();
            animator.runtimeAnimatorController =
                AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(
                    "Assets/Prefabs/UI/MainMenu/ExitButton.controller"
                );
            ExternalContentArt overrides = _root.AddComponent<ExternalContentArt>();

            UIComponentTestHelper.InvokeLifecycle(overrides, "Awake");
            image.sprite = _nextAnimationSprite;
            UIComponentTestHelper.InvokeLifecycle(overrides, "LateUpdate");

            Assert.IsTrue(animator.enabled);
            Assert.AreNotSame(_nextAnimationSprite, image.sprite);
            Assert.AreEqual("mod_animation_01", image.sprite.texture.name);
            Assert.AreEqual(3, image.sprite.texture.width);
            Assert.AreEqual(2, image.sprite.texture.height);
        }

        private static void WritePng(string path, int width, int height)
        {
            Texture2D texture = new Texture2D(width, height);
            try
            {
                File.WriteAllBytes(path, texture.EncodeToPNG());
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(texture);
            }
        }

        private static T GetField<T>(object target, string fieldName)
        {
            return (T)
                target
                    .GetType()
                    .GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic)
                    .GetValue(target);
        }

        private static void SetField(object target, string fieldName, object value)
        {
            target
                .GetType()
                .GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(target, value);
        }
    }
}
