using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Image))]
public sealed class ApplicationSpriteSequence : MonoBehaviour
{
    [SerializeField]
    private Image target;

    [SerializeField]
    private string[] addresses = Array.Empty<string>();

    [SerializeField]
    private float[] frameTimes = Array.Empty<float>();

    [SerializeField]
    private float duration;

    private readonly List<Sprite> ownedSprites = new List<Sprite>();
    private Sprite[] frames = Array.Empty<Sprite>();
    private float elapsed;

    public void Configure(Image image, string[] textureAddresses, float[] times, float seconds)
    {
        target = image;
        addresses = textureAddresses;
        frameTimes = times;
        duration = seconds;
    }

    private void Awake()
    {
        if (!Application.isPlaying)
            return;
        if (
            target == null
            || addresses.Length == 0
            || addresses.Length != frameTimes.Length
            || duration <= 0f
        )
            throw new InvalidOperationException($"{name} has an invalid sprite sequence.");

        ContentAssets assets = AppBootstrap.EnsureExists().GetContentAssets();
        frames = new Sprite[addresses.Length];
        for (int index = 0; index < addresses.Length; index++)
        {
            Texture2D texture = assets.GetTexture(addresses[index]);
            Sprite sprite = Sprite.Create(
                texture,
                new Rect(0f, 0f, texture.width, texture.height),
                new Vector2(0.5f, 0.5f),
                100f
            );
            sprite.name = texture.name;
            frames[index] = sprite;
            ownedSprites.Add(sprite);
        }

        target.sprite = frames[0];
    }

    private void Update()
    {
        if (frames.Length < 2)
            return;

        elapsed = (elapsed + Time.unscaledDeltaTime) % duration;
        int index = Array.BinarySearch(frameTimes, elapsed);
        if (index < 0)
            index = Math.Max(0, ~index - 1);
        target.sprite = frames[index];
    }

    private void OnDestroy()
    {
        foreach (Sprite sprite in ownedSprites)
        {
            if (sprite != null)
                Destroy(sprite);
        }

        ownedSprites.Clear();
    }
}
