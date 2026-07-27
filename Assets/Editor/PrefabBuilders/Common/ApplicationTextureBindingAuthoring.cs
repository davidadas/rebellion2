using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.UI;

public static class ApplicationTextureBindingAuthoring
{
    private const string _applicationAddressPrefix = "application/";
    private static readonly Dictionary<UnityEngine.Object, string> _addressesByTransientAsset =
        new Dictionary<UnityEngine.Object, string>();

    public static Texture2D LoadTexture(string contentAddress)
    {
        string address = NormalizeAddress(contentAddress);
        string filePath = ResolveContentFile(address);
        Texture2D texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        if (!ImageConversion.LoadImage(texture, File.ReadAllBytes(filePath), true))
        {
            UnityEngine.Object.DestroyImmediate(texture);
            throw new InvalidDataException($"Application texture could not be decoded: {filePath}");
        }

        texture.name = Path.GetFileNameWithoutExtension(filePath);
        texture.filterMode = FilterMode.Point;
        texture.wrapMode = TextureWrapMode.Clamp;
        _addressesByTransientAsset.Add(texture, address);
        return texture;
    }

    public static Sprite LoadSprite(string contentAddress)
    {
        Texture2D texture = LoadTexture(contentAddress);
        Sprite sprite = Sprite.Create(
            texture,
            new Rect(0f, 0f, texture.width, texture.height),
            new Vector2(0.5f, 0.5f),
            100f
        );
        sprite.name = texture.name;
        _addressesByTransientAsset.Add(sprite, _addressesByTransientAsset[texture]);
        return sprite;
    }

    public static void Capture(GameObject root)
    {
        ApplicationTextureBindings bindings = root.GetComponent<ApplicationTextureBindings>();
        List<ApplicationTextureBindings.RawImageBinding> rawImages =
            bindings == null
                ? new List<ApplicationTextureBindings.RawImageBinding>()
                : new List<ApplicationTextureBindings.RawImageBinding>(bindings.RawImages);
        List<ApplicationTextureBindings.ImageBinding> images =
            bindings == null
                ? new List<ApplicationTextureBindings.ImageBinding>()
                : new List<ApplicationTextureBindings.ImageBinding>(bindings.Images);
        List<ApplicationTextureBindings.ReceiverBinding> receivers =
            bindings == null
                ? new List<ApplicationTextureBindings.ReceiverBinding>()
                : new List<ApplicationTextureBindings.ReceiverBinding>(bindings.Receivers);
        List<ApplicationTextureBindings.ButtonBinding> buttons =
            bindings == null
                ? new List<ApplicationTextureBindings.ButtonBinding>()
                : new List<ApplicationTextureBindings.ButtonBinding>(bindings.Buttons);

        rawImages.RemoveAll(binding =>
            binding?.Target == null || !binding.Target.transform.IsChildOf(root.transform)
        );
        images.RemoveAll(binding =>
            binding?.Target == null || !binding.Target.transform.IsChildOf(root.transform)
        );
        receivers.RemoveAll(binding =>
            binding?.Target == null
            || !binding.Target.transform.IsChildOf(root.transform)
            || binding.Target is not IApplicationTextureReceiver
            || new SerializedObject(binding.Target).FindProperty(binding.Key) == null
        );
        buttons.RemoveAll(binding =>
            binding?.Target == null || !binding.Target.transform.IsChildOf(root.transform)
        );

        CaptureSpriteSequences(root);

        foreach (RawImage image in root.GetComponentsInChildren<RawImage>(true))
        {
            if (!TryGetAddress(image.texture, out string address))
                continue;

            rawImages.RemoveAll(binding => binding.Target == image);
            rawImages.Add(new ApplicationTextureBindings.RawImageBinding(image, address));
            image.texture = null;
            EditorUtility.SetDirty(image);
        }

        foreach (Image image in root.GetComponentsInChildren<Image>(true))
        {
            if (!TryGetAddress(image.sprite, out string address))
                continue;

            images.RemoveAll(binding => binding.Target == image);
            images.Add(new ApplicationTextureBindings.ImageBinding(image, address));
            image.sprite = null;
            EditorUtility.SetDirty(image);
        }

        foreach (Button button in root.GetComponentsInChildren<Button>(true))
        {
            SpriteState state = button.spriteState;
            bool hasApplicationSprite =
                TryGetAddress(state.highlightedSprite, out string highlightedAddress)
                | TryGetAddress(state.pressedSprite, out string pressedAddress)
                | TryGetAddress(state.selectedSprite, out string selectedAddress)
                | TryGetAddress(state.disabledSprite, out string disabledAddress);
            if (!hasApplicationSprite)
                continue;

            buttons.RemoveAll(binding => binding.Target == button);
            buttons.Add(
                new ApplicationTextureBindings.ButtonBinding(
                    button,
                    highlightedAddress,
                    pressedAddress,
                    selectedAddress,
                    disabledAddress
                )
            );
            state.highlightedSprite = null;
            state.pressedSprite = null;
            state.selectedSprite = null;
            state.disabledSprite = null;
            button.spriteState = state;
            EditorUtility.SetDirty(button);
        }

        foreach (MonoBehaviour component in root.GetComponentsInChildren<MonoBehaviour>(true))
        {
            SerializedObject serializedObject = new SerializedObject(component);
            SerializedProperty property = serializedObject.GetIterator();
            bool enterChildren = true;
            while (property.NextVisible(enterChildren))
            {
                enterChildren = false;
                if (
                    property.propertyType != SerializedPropertyType.ObjectReference
                    || !TryGetAddress(property.objectReferenceValue, out string address)
                )
                    continue;
                if (component is not IApplicationTextureReceiver)
                {
                    throw new InvalidOperationException(
                        $"{component.GetType().Name}.{property.propertyPath} references application art "
                            + "but does not implement IApplicationTextureReceiver."
                    );
                }

                receivers.RemoveAll(binding =>
                    binding.Target == component && binding.Key == property.propertyPath
                );
                receivers.Add(
                    new ApplicationTextureBindings.ReceiverBinding(
                        component,
                        property.propertyPath,
                        address
                    )
                );
                property.objectReferenceValue = null;
            }

            serializedObject.ApplyModifiedPropertiesWithoutUndo();
        }

        if (rawImages.Count == 0 && images.Count == 0 && receivers.Count == 0 && buttons.Count == 0)
        {
            if (bindings != null)
                UnityEngine.Object.DestroyImmediate(bindings);
            return;
        }

        bindings ??= root.AddComponent<ApplicationTextureBindings>();
        bindings.Configure(
            rawImages.ToArray(),
            images.ToArray(),
            receivers.ToArray(),
            buttons.ToArray()
        );
        EditorUtility.SetDirty(bindings);
    }

    private static void CaptureSpriteSequences(GameObject root)
    {
        foreach (Animator animator in root.GetComponentsInChildren<Animator>(true))
        {
            if (animator.runtimeAnimatorController is not AnimatorController controller)
                continue;

            AnimationClip[] clips = controller.animationClips;
            if (clips.Length != 1)
                continue;

            AnimationClip clip = clips[0];
            List<(float Time, string Address)> frames = new List<(float, string)>();
            foreach (
                EditorCurveBinding binding in AnimationUtility.GetObjectReferenceCurveBindings(clip)
            )
            {
                foreach (
                    ObjectReferenceKeyframe keyframe in AnimationUtility.GetObjectReferenceCurve(
                        clip,
                        binding
                    )
                )
                {
                    if (!TryGetAddress(keyframe.value, out string address))
                    {
                        frames.Clear();
                        break;
                    }

                    frames.Add((keyframe.time, address));
                }
            }

            if (frames.Count == 0)
                continue;

            Image image =
                animator.GetComponent<Image>()
                ?? throw new InvalidOperationException(
                    $"{animator.name} animates application sprites without an Image."
                );
            frames.Sort((left, right) => left.Time.CompareTo(right.Time));
            ApplicationSpriteSequence sequence =
                animator.GetComponent<ApplicationSpriteSequence>()
                ?? animator.gameObject.AddComponent<ApplicationSpriteSequence>();
            float playbackSpeed = controller.layers[0].stateMachine.defaultState.speed;
            if (playbackSpeed <= 0f)
                throw new InvalidOperationException($"{controller.name} has no playback speed.");
            sequence.Configure(
                image,
                frames.ConvertAll(frame => frame.Address).ToArray(),
                frames.ConvertAll(frame => frame.Time / playbackSpeed).ToArray(),
                clip.length / playbackSpeed
            );
            UnityEngine.Object.DestroyImmediate(animator);
            EditorUtility.SetDirty(sequence);
        }
    }

    private static bool TryGetAddress(UnityEngine.Object asset, out string address)
    {
        if (asset != null && _addressesByTransientAsset.TryGetValue(asset, out address))
            return true;

        address = null;
        return false;
    }

    private static string NormalizeAddress(string contentAddress)
    {
        string address = contentAddress?.Trim().Replace('\\', '/');
        if (
            string.IsNullOrWhiteSpace(address)
            || !address.StartsWith(_applicationAddressPrefix, StringComparison.Ordinal)
        )
        {
            throw new ArgumentException(
                $"Not an application content address: {contentAddress}",
                nameof(contentAddress)
            );
        }

        string extension = Path.GetExtension(address);
        if (string.IsNullOrEmpty(extension))
            return address;
        if (
            !string.Equals(extension, ".png", StringComparison.OrdinalIgnoreCase)
            && !string.Equals(extension, ".jpg", StringComparison.OrdinalIgnoreCase)
            && !string.Equals(extension, ".jpeg", StringComparison.OrdinalIgnoreCase)
        )
            throw new ArgumentException($"Not a texture address: {contentAddress}");

        return address[..^extension.Length];
    }

    private static string ResolveContentFile(string address)
    {
        string projectRoot =
            Directory.GetParent(Application.dataPath)?.FullName
            ?? throw new InvalidOperationException("Could not resolve the project directory.");
        string contentRoot = Path.GetFullPath(Path.Combine(projectRoot, "Content"));
        string path = Path.GetFullPath(Path.Combine(contentRoot, address));
        string contentPrefix =
            contentRoot.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        if (!path.StartsWith(contentPrefix, StringComparison.Ordinal))
            throw new ArgumentException($"Content address leaves the content root: {address}");

        foreach (string extension in new[] { ".png", ".jpg", ".jpeg" })
        {
            string candidate = path + extension;
            if (File.Exists(candidate))
                return candidate;
        }

        throw new FileNotFoundException($"Application content texture not found: {address}");
    }
}
