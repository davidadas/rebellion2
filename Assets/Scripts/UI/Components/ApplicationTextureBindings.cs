using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public interface IApplicationTextureReceiver
{
    void SetApplicationTexture(string key, Texture2D texture);
}

[DefaultExecutionOrder(-1000)]
[ExecuteAlways]
public sealed class ApplicationTextureBindings : MonoBehaviour
{
#if UNITY_EDITOR
    private ContentAssets editorPreviewAssets;
#endif
    [Serializable]
    public sealed class RawImageBinding
    {
        [SerializeField]
        private RawImage target;

        [SerializeField]
        private string address;

        public RawImage Target => target;

        public string Address => address;

        public RawImageBinding(RawImage target, string address)
        {
            this.target = target;
            this.address = address;
        }
    }

    [Serializable]
    public sealed class ImageBinding
    {
        [SerializeField]
        private Image target;

        [SerializeField]
        private string address;

        public Image Target => target;

        public string Address => address;

        public ImageBinding(Image target, string address)
        {
            this.target = target;
            this.address = address;
        }
    }

    [Serializable]
    public sealed class ReceiverBinding
    {
        [SerializeField]
        private MonoBehaviour target;

        [SerializeField]
        private string key;

        [SerializeField]
        private string address;

        public MonoBehaviour Target => target;

        public string Key => key;

        public string Address => address;

        public ReceiverBinding(MonoBehaviour target, string key, string address)
        {
            this.target = target;
            this.key = key;
            this.address = address;
        }
    }

    [Serializable]
    public sealed class ButtonBinding
    {
        [SerializeField]
        private Button target;

        [SerializeField]
        private string highlightedAddress;

        [SerializeField]
        private string pressedAddress;

        [SerializeField]
        private string selectedAddress;

        [SerializeField]
        private string disabledAddress;

        public Button Target => target;

        public string HighlightedAddress => highlightedAddress;

        public string PressedAddress => pressedAddress;

        public string SelectedAddress => selectedAddress;

        public string DisabledAddress => disabledAddress;

        public ButtonBinding(
            Button target,
            string highlightedAddress,
            string pressedAddress,
            string selectedAddress,
            string disabledAddress
        )
        {
            this.target = target;
            this.highlightedAddress = highlightedAddress;
            this.pressedAddress = pressedAddress;
            this.selectedAddress = selectedAddress;
            this.disabledAddress = disabledAddress;
        }
    }

    [SerializeField]
    private RawImageBinding[] rawImages = Array.Empty<RawImageBinding>();

    [SerializeField]
    private ImageBinding[] images = Array.Empty<ImageBinding>();

    [SerializeField]
    private ReceiverBinding[] receivers = Array.Empty<ReceiverBinding>();

    [SerializeField]
    private ButtonBinding[] buttons = Array.Empty<ButtonBinding>();

    private readonly List<Sprite> ownedSprites = new List<Sprite>();
    private bool isApplied;

    public IReadOnlyList<RawImageBinding> RawImages => rawImages;

    public IReadOnlyList<ImageBinding> Images => images;

    public IReadOnlyList<ReceiverBinding> Receivers => receivers;

    public IReadOnlyList<ButtonBinding> Buttons => buttons;

    public void Configure(
        RawImageBinding[] rawImageBindings,
        ImageBinding[] imageBindings,
        ReceiverBinding[] receiverBindings,
        ButtonBinding[] buttonBindings
    )
    {
        rawImages = rawImageBindings ?? Array.Empty<RawImageBinding>();
        images = imageBindings ?? Array.Empty<ImageBinding>();
        receivers = receiverBindings ?? Array.Empty<ReceiverBinding>();
        buttons = buttonBindings ?? Array.Empty<ButtonBinding>();
    }

    private void Awake()
    {
        if (!Application.isPlaying)
            return;

        Apply(AppBootstrap.EnsureExists().GetContentAssets());
    }

    private void OnEnable()
    {
#if UNITY_EDITOR
        if (Application.isPlaying || isApplied)
            return;

        try
        {
            if (editorPreviewAssets == null)
            {
                ContentPack pack = ContentPackLoader.OpenActive();
                editorPreviewAssets = new ContentAssets(pack.ContentRootPath, pack.PackRootPath);
            }

            Apply(editorPreviewAssets);
        }
        catch (Exception exception)
        {
            editorPreviewAssets?.Dispose();
            editorPreviewAssets = null;
            Debug.LogWarning(
                $"Application content could not be loaded for prefab preview: {exception.Message}",
                this
            );
        }
#endif
    }

    internal void Apply(ContentAssets assets)
    {
        if (assets == null)
            throw new ArgumentNullException(nameof(assets));
        if (isApplied)
            return;

        foreach (RawImageBinding binding in rawImages)
            binding.Target.texture = assets.GetTexture(binding.Address);
        foreach (ImageBinding binding in images)
            ApplySprite(binding, assets.GetTexture(binding.Address));
        foreach (ReceiverBinding binding in receivers)
        {
            if (binding.Target is not IApplicationTextureReceiver receiver)
            {
                throw new InvalidOperationException(
                    $"{binding.Target.name} cannot receive application textures."
                );
            }

            receiver.SetApplicationTexture(binding.Key, assets.GetTexture(binding.Address));
        }
        foreach (ButtonBinding binding in buttons)
            ApplySpriteState(binding, assets);

        isApplied = true;
    }

    private void OnDestroy()
    {
        foreach (Sprite sprite in ownedSprites)
        {
            if (sprite != null)
            {
                if (Application.isPlaying)
                    Destroy(sprite);
                else
                    DestroyImmediate(sprite);
            }
        }

        ownedSprites.Clear();

#if UNITY_EDITOR
        editorPreviewAssets?.Dispose();
        editorPreviewAssets = null;
#endif
    }

    private void ApplySprite(ImageBinding binding, Texture2D texture)
    {
        binding.Target.sprite = CreateSprite(texture);
    }

    private void ApplySpriteState(ButtonBinding binding, ContentAssets assets)
    {
        SpriteState state = binding.Target.spriteState;
        state.highlightedSprite = CreateOptionalSprite(assets, binding.HighlightedAddress);
        state.pressedSprite = CreateOptionalSprite(assets, binding.PressedAddress);
        state.selectedSprite = CreateOptionalSprite(assets, binding.SelectedAddress);
        state.disabledSprite = CreateOptionalSprite(assets, binding.DisabledAddress);
        binding.Target.spriteState = state;
    }

    private Sprite CreateOptionalSprite(ContentAssets assets, string address)
    {
        return string.IsNullOrWhiteSpace(address) ? null : CreateSprite(assets.GetTexture(address));
    }

    private Sprite CreateSprite(Texture2D texture)
    {
        Sprite sprite = Sprite.Create(
            texture,
            new Rect(0f, 0f, texture.width, texture.height),
            new Vector2(0.5f, 0.5f),
            100f
        );
        sprite.name = texture.name;
        ownedSprites.Add(sprite);
        return sprite;
    }
}
