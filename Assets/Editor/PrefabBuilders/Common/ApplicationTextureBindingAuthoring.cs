using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.UI;

public static class ApplicationTextureBindingAuthoring
{
    private const string _applicationAddressPrefix = "application/";
    private const string _packAddressPrefix = "pack/";
    private const string _editorContentRoot = "Assets/Editor/ContentPreview";
    private static readonly Dictionary<UnityEngine.Object, string> _addressesByAsset =
        new Dictionary<UnityEngine.Object, string>();

    public static Texture2D LoadTexture(string contentAddress)
    {
        string address = NormalizeAddress(contentAddress);
        string assetPath = ResolveEditorAssetPath(address);
        Texture2D texture =
            AssetDatabase.LoadAssetAtPath<Texture2D>(assetPath)
            ?? throw new InvalidDataException(
                $"Editor content texture could not be loaded: {assetPath}"
            );
        _addressesByAsset[texture] = address;
        return texture;
    }

    public static Sprite LoadSprite(string contentAddress)
    {
        string address = NormalizeAddress(contentAddress);
        string assetPath = ResolveEditorAssetPath(address);
        TextureImporter importer =
            AssetImporter.GetAtPath(assetPath) as TextureImporter
            ?? throw new InvalidDataException($"Editor content texture is not imported: {assetPath}");
        if (
            importer.textureType != TextureImporterType.Sprite
            || importer.spriteImportMode != SpriteImportMode.Single
        )
        {
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.SaveAndReimport();
        }

        Sprite sprite =
            AssetDatabase.LoadAssetAtPath<Sprite>(assetPath)
            ?? throw new InvalidDataException(
                $"Editor content sprite could not be loaded: {assetPath}"
            );
        _addressesByAsset[sprite] = address;
        return sprite;
    }

    /// <summary>
    /// Restores editor-visible defaults from an existing runtime binding component.
    /// </summary>
    /// <param name="root">The prefab root whose defaults should be restored.</param>
    public static void RestoreDefaults(GameObject root)
    {
        ApplicationTextureBindings bindings = root.GetComponent<ApplicationTextureBindings>();
        if (bindings == null)
            return;

        foreach (ApplicationTextureBindings.RawImageBinding binding in bindings.RawImages)
            binding.Target.texture = LoadTexture(binding.Address);
        foreach (ApplicationTextureBindings.ImageBinding binding in bindings.Images)
            binding.Target.sprite = LoadSprite(binding.Address);
        foreach (ApplicationTextureBindings.ReceiverBinding binding in bindings.Receivers)
        {
            SerializedObject serializedObject = new SerializedObject(binding.Target);
            SerializedProperty property = serializedObject.FindProperty(binding.Key);
            if (property == null)
                throw new MissingMemberException(binding.Target.GetType().Name, binding.Key);
            property.objectReferenceValue = property.type.Contains("Sprite")
                ? LoadSprite(binding.Address)
                : LoadTexture(binding.Address);
            serializedObject.ApplyModifiedPropertiesWithoutUndo();
        }
        foreach (ApplicationTextureBindings.ButtonBinding binding in bindings.Buttons)
        {
            SpriteState state = binding.Target.spriteState;
            state.highlightedSprite = LoadOptionalSprite(binding.HighlightedAddress);
            state.pressedSprite = LoadOptionalSprite(binding.PressedAddress);
            state.selectedSprite = LoadOptionalSprite(binding.SelectedAddress);
            state.disabledSprite = LoadOptionalSprite(binding.DisabledAddress);
            binding.Target.spriteState = state;
        }
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
            EditorUtility.SetDirty(image);
        }

        foreach (Image image in root.GetComponentsInChildren<Image>(true))
        {
            if (!TryGetAddress(image.sprite, out string address))
                continue;

            images.RemoveAll(binding => binding.Target == image);
            images.Add(new ApplicationTextureBindings.ImageBinding(image, address));
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
            button.spriteState = state;
            EditorUtility.SetDirty(button);
        }

        foreach (MonoBehaviour component in root.GetComponentsInChildren<MonoBehaviour>(true))
        {
            if (component == null || component is Graphic || component is Selectable)
                continue;

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

    private static Sprite LoadOptionalSprite(string address)
    {
        return string.IsNullOrWhiteSpace(address) ? null : LoadSprite(address);
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
        if (asset != null && _addressesByAsset.TryGetValue(asset, out address))
            return true;

        if (asset != null && TryFindApplicationAddress(asset, out address))
        {
            _addressesByAsset[asset] = address;
            return true;
        }

        address = null;
        return false;
    }

    private static bool TryFindApplicationAddress(UnityEngine.Object asset, out string address)
    {
        string assetPath = AssetDatabase.GetAssetPath(asset);
        if (
            string.IsNullOrWhiteSpace(assetPath)
            || !assetPath.StartsWith("Assets/", StringComparison.Ordinal)
        )
        {
            address = null;
            return false;
        }

        string projectRoot =
            Directory.GetParent(Application.dataPath)?.FullName
            ?? throw new InvalidOperationException("Could not resolve the project directory.");
        string applicationRoot = Path.Combine(projectRoot, "Content", "application");
        string assetName = Path.GetFileNameWithoutExtension(assetPath);
        string[] matches = Directory
            .EnumerateFiles(applicationRoot, assetName + ".*", SearchOption.AllDirectories)
            .Where(path =>
                string.Equals(Path.GetExtension(path), ".png", StringComparison.OrdinalIgnoreCase)
                || string.Equals(
                    Path.GetExtension(path),
                    ".jpg",
                    StringComparison.OrdinalIgnoreCase
                )
                || string.Equals(
                    Path.GetExtension(path),
                    ".jpeg",
                    StringComparison.OrdinalIgnoreCase
                )
            )
            .Take(2)
            .ToArray();
        if (matches.Length != 1)
        {
            address = null;
            return false;
        }

        string contentRoot = Path.Combine(projectRoot, "Content");
        address = Path
            .ChangeExtension(Path.GetRelativePath(contentRoot, matches[0]), null)
            .Replace('\\', '/');
        return true;
    }

    private static string NormalizeAddress(string contentAddress)
    {
        string address = contentAddress?.Trim().Replace('\\', '/');
        if (
            string.IsNullOrWhiteSpace(address)
            || (
                !address.StartsWith(_applicationAddressPrefix, StringComparison.Ordinal)
                && !address.StartsWith(_packAddressPrefix, StringComparison.Ordinal)
            )
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
        string path;
        if (address.StartsWith(_packAddressPrefix, StringComparison.Ordinal))
        {
            ContentPack pack = ContentPackLoader.OpenActive();
            path = Path.GetFullPath(
                Path.Combine(pack.PackRootPath, address[_packAddressPrefix.Length..])
            );
        }
        else
        {
            path = Path.GetFullPath(Path.Combine(contentRoot, address));
        }
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

    private static string ResolveEditorAssetPath(string address)
    {
        string contentFile = ResolveContentFile(address);
        string projectRoot =
            Directory.GetParent(Application.dataPath)?.FullName
            ?? throw new InvalidOperationException("Could not resolve the project directory.");
        string contentRoot = Path.Combine(projectRoot, "Content");
        string relativePath = Path.GetRelativePath(contentRoot, contentFile).Replace('\\', '/');
        string assetPath = $"{_editorContentRoot}/{relativePath}";
        if (File.Exists(Path.Combine(projectRoot, assetPath)))
            return assetPath;

        throw new FileNotFoundException($"Imported editor content texture not found: {assetPath}");
    }
}
