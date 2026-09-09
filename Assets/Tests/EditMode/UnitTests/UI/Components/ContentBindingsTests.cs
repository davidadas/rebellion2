using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public sealed class ContentBindingsTests
{
    private const string _textureAddress = "Application/Test/UI/ui_test_texture";
    private const string _spriteAddress = "Application/Test/UI/ui_test_sprite";
    private const string _upAddress = "Application/Test/UI/ui_test_button";
    private const string _downAddress = "Application/Test/UI/ui_test_button_pressed";

    private readonly List<UnityEngine.Object> _createdObjects = new List<UnityEngine.Object>();

    [TearDown]
    public void TearDown()
    {
        for (int index = _createdObjects.Count - 1; index >= 0; index--)
        {
            UnityEngine.Object createdObject = _createdObjects[index];
            if (createdObject != null)
                UnityEngine.Object.DestroyImmediate(createdObject);
        }

        _createdObjects.Clear();
    }

    [Test]
    public void Apply_StrippedRawImageTexture_RestoresFromContent()
    {
        Texture2D expectedTexture = CreateTexture();
        FakeContentAssetSource contentAssets = new FakeContentAssetSource();
        contentAssets.AddTexture(_textureAddress, expectedTexture);

        RawImage rawImage = CreateComponent<RawImage>("TextureBinding");
        rawImage.texture = null;
        rawImage.gameObject.AddComponent<ContentTextureBinding>().SetAddress(_textureAddress);

        ContentBindings.Apply(rawImage.gameObject, contentAssets);

        Assert.AreEqual(expectedTexture, rawImage.texture);
    }

    [Test]
    public void Apply_StrippedImageSprite_RestoresFromContent()
    {
        Sprite expectedSprite = CreateSprite();
        FakeContentAssetSource contentAssets = new FakeContentAssetSource();
        contentAssets.AddSprite(_spriteAddress, expectedSprite);

        Image image = CreateComponent<Image>("SpriteBinding");
        image.sprite = null;
        image.gameObject.AddComponent<ContentSpriteBinding>().SetAddress(_spriteAddress);

        ContentBindings.Apply(image.gameObject, contentAssets);

        Assert.AreEqual(expectedSprite, image.sprite);
    }

    /// <summary>
    /// Verifies a sprite binding forwards its explicit nine-slice border to the content source.
    /// </summary>
    [Test]
    public void Apply_BorderedImageSprite_RequestsExplicitBorder()
    {
        Sprite expectedSprite = CreateSprite();
        Vector4 expectedBorder = new Vector4(6f, 6f, 6f, 6f);
        FakeContentAssetSource contentAssets = new FakeContentAssetSource();
        contentAssets.AddSprite(_spriteAddress, expectedSprite);

        Image image = CreateComponent<Image>("BorderedSpriteBinding");
        image
            .gameObject.AddComponent<ContentSpriteBinding>()
            .SetAddress(_spriteAddress, expectedBorder);

        ContentBindings.Apply(image.gameObject, contentAssets);

        Assert.AreEqual(expectedSprite, image.sprite);
        Assert.AreEqual(expectedBorder, contentAssets.LastSpriteBorder);
    }

    [Test]
    public void Apply_StrippedPressVisual_RestoresReleasedTexture()
    {
        Texture2D releasedTexture = CreateTexture();
        Texture2D pressedTexture = CreateTexture();
        FakeContentAssetSource contentAssets = new FakeContentAssetSource();
        contentAssets.AddTexture(_upAddress, releasedTexture);
        contentAssets.AddTexture(_downAddress, pressedTexture);

        RawImage rawImage = CreateComponent<RawImage>("PressVisualBinding");
        rawImage.texture = null;
        RawImagePressVisual pressVisual = rawImage.gameObject.AddComponent<RawImagePressVisual>();
        typeof(RawImagePressVisual)
            .GetField("image", BindingFlags.Instance | BindingFlags.NonPublic)
            .SetValue(pressVisual, rawImage);
        rawImage
            .gameObject.AddComponent<ContentPressVisualBinding>()
            .SetAddresses(_upAddress, _downAddress);

        ContentBindings.Apply(rawImage.gameObject, contentAssets);

        Assert.AreEqual(releasedTexture, rawImage.texture);
    }

    [Test]
    public void Apply_StrippedPressVisual_RestoresPressedTexture()
    {
        Texture2D releasedTexture = CreateTexture();
        Texture2D pressedTexture = CreateTexture();
        FakeContentAssetSource contentAssets = new FakeContentAssetSource();
        contentAssets.AddTexture(_upAddress, releasedTexture);
        contentAssets.AddTexture(_downAddress, pressedTexture);

        RawImage rawImage = CreateComponent<RawImage>("PressVisualBinding");
        RawImagePressVisual pressVisual = rawImage.gameObject.AddComponent<RawImagePressVisual>();
        typeof(RawImagePressVisual)
            .GetField("image", BindingFlags.Instance | BindingFlags.NonPublic)
            .SetValue(pressVisual, rawImage);
        rawImage
            .gameObject.AddComponent<ContentPressVisualBinding>()
            .SetAddresses(_upAddress, _downAddress);

        ContentBindings.Apply(rawImage.gameObject, contentAssets);
        pressVisual.OnPointerDown(
            new PointerEventData(null) { button = PointerEventData.InputButton.Left }
        );

        Assert.AreEqual(pressedTexture, rawImage.texture);
    }

    [Test]
    public void Apply_InactiveDescendantBinding_RestoresFromContent()
    {
        Texture2D expectedTexture = CreateTexture();
        FakeContentAssetSource contentAssets = new FakeContentAssetSource();
        contentAssets.AddTexture(_textureAddress, expectedTexture);

        GameObject root = CreateGameObject("Root");
        RawImage rawImage = CreateComponent<RawImage>("InactiveChild");
        rawImage.transform.SetParent(root.transform, false);
        rawImage.texture = null;
        rawImage.gameObject.AddComponent<ContentTextureBinding>().SetAddress(_textureAddress);
        rawImage.gameObject.SetActive(false);

        ContentBindings.Apply(root, contentAssets);

        Assert.AreEqual(expectedTexture, rawImage.texture);
    }

    [Test]
    public void Apply_InactiveInitializable_InitializesFromContent()
    {
        FakeContentAssetSource contentAssets = new FakeContentAssetSource();
        ContentInitializableStub initializable = CreateComponent<ContentInitializableStub>(
            "InactiveInitializable"
        );
        initializable.gameObject.SetActive(false);

        ContentBindings.Apply(initializable.gameObject, contentAssets);

        Assert.AreSame(contentAssets, initializable.ContentAssets);
    }

    [Test]
    public void Apply_UnresolvableAddress_ThrowsWithAddressInMessage()
    {
        FakeContentAssetSource contentAssets = new FakeContentAssetSource();
        RawImage rawImage = CreateComponent<RawImage>("MissingBinding");
        rawImage.gameObject.AddComponent<ContentTextureBinding>().SetAddress(_textureAddress);

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() =>
            ContentBindings.Apply(rawImage.gameObject, contentAssets)
        );
        StringAssert.Contains(_textureAddress, exception.Message);
    }

    private GameObject CreateGameObject(string name)
    {
        GameObject created = new GameObject(name);
        _createdObjects.Add(created);
        return created;
    }

    private T CreateComponent<T>(string name)
        where T : Component
    {
        return CreateGameObject(name).AddComponent<T>();
    }

    private Texture2D CreateTexture()
    {
        Texture2D texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        _createdObjects.Add(texture);
        return texture;
    }

    private Sprite CreateSprite()
    {
        Texture2D texture = CreateTexture();
        Sprite sprite = Sprite.Create(
            texture,
            new Rect(0f, 0f, texture.width, texture.height),
            new Vector2(0.5f, 0.5f)
        );
        _createdObjects.Add(sprite);
        return sprite;
    }

    private sealed class FakeContentAssetSource : IContentAssetSource
    {
        private readonly Dictionary<string, Texture2D> _textures = new Dictionary<
            string,
            Texture2D
        >(StringComparer.Ordinal);
        private readonly Dictionary<string, Sprite> _sprites = new Dictionary<string, Sprite>(
            StringComparer.Ordinal
        );

        public Vector4 LastSpriteBorder { get; private set; }

        public void AddTexture(string address, Texture2D texture)
        {
            _textures[address] = texture;
        }

        public void AddSprite(string address, Sprite sprite)
        {
            _sprites[address] = sprite;
        }

        public Texture2D GetTexture(string address)
        {
            return _textures.TryGetValue(address, out Texture2D texture) ? texture : null;
        }

        /// <summary>
        /// Resolves a sprite from the test content collection.
        /// </summary>
        /// <param name="address">The test content address.</param>
        /// <returns>The configured test sprite, or null when none exists.</returns>
        public Sprite GetSprite(string address)
        {
            return _sprites.TryGetValue(address, out Sprite sprite) ? sprite : null;
        }

        /// <summary>
        /// Resolves a test sprite while recording the requested border.
        /// </summary>
        /// <param name="address">The test content address.</param>
        /// <param name="border">The requested sprite border.</param>
        /// <returns>The configured test sprite.</returns>
        public Sprite GetSprite(string address, Vector4 border)
        {
            LastSpriteBorder = border;
            return GetSprite(address);
        }
    }

    private sealed class ContentInitializableStub : MonoBehaviour, IContentInitializable
    {
        public IContentAssetSource ContentAssets { get; private set; }

        public void InitializeContent(IContentAssetSource contentAssets)
        {
            ContentAssets = contentAssets;
        }
    }
}
