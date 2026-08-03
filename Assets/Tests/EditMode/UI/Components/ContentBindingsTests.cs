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

    private readonly List<GameObject> createdObjects = new List<GameObject>();

    [TearDown]
    public void TearDown()
    {
        foreach (GameObject createdObject in createdObjects)
        {
            if (createdObject != null)
                UnityEngine.Object.DestroyImmediate(createdObject);
        }

        createdObjects.Clear();
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
        createdObjects.Add(created);
        return created;
    }

    private T CreateComponent<T>(string name)
        where T : Component
    {
        return CreateGameObject(name).AddComponent<T>();
    }

    private static Texture2D CreateTexture()
    {
        return new Texture2D(2, 2, TextureFormat.RGBA32, false);
    }

    private static Sprite CreateSprite()
    {
        Texture2D texture = CreateTexture();
        return Sprite.Create(
            texture,
            new Rect(0f, 0f, texture.width, texture.height),
            new Vector2(0.5f, 0.5f)
        );
    }

    private sealed class FakeContentAssetSource : IContentAssetSource
    {
        private readonly Dictionary<string, Texture2D> textures = new Dictionary<string, Texture2D>(
            StringComparer.Ordinal
        );
        private readonly Dictionary<string, Sprite> sprites = new Dictionary<string, Sprite>(
            StringComparer.Ordinal
        );

        public void AddTexture(string address, Texture2D texture)
        {
            textures[address] = texture;
        }

        public void AddSprite(string address, Sprite sprite)
        {
            sprites[address] = sprite;
        }

        public Texture2D GetTexture(string address)
        {
            return textures.TryGetValue(address, out Texture2D texture) ? texture : null;
        }

        public Sprite GetSprite(string address)
        {
            return sprites.TryGetValue(address, out Sprite sprite) ? sprite : null;
        }
    }
}
