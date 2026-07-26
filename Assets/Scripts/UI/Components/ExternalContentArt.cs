using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEngine;
using UnityEngine.UI;

public sealed class ExternalContentArt : MonoBehaviour
{
    private readonly HashSet<Image> activeAnimationImages = new HashSet<Image>();
    private readonly Dictionary<Image, Dictionary<string, Sprite>> animationFrames =
        new Dictionary<Image, Dictionary<string, Sprite>>();
    private readonly List<Sprite> ownedSprites = new List<Sprite>();
    private readonly Dictionary<Sprite, Sprite> replacementSprites =
        new Dictionary<Sprite, Sprite>();

    private void Awake()
    {
        Dictionary<string, List<Action<Texture2D>>> replacements = CaptureReplacements();
        foreach (KeyValuePair<string, List<Action<Texture2D>>> replacement in replacements)
        {
            Texture2D texture = ResourceManager.GetTexture(replacement.Key);
            if (texture == null)
                continue;

            foreach (Action<Texture2D> apply in replacement.Value)
                apply(texture);
        }

        ActivateExternalAnimations();
    }

    private void OnDestroy()
    {
        foreach (Sprite sprite in ownedSprites)
        {
            if (sprite != null)
                Destroy(sprite);
        }

        animationFrames.Clear();
        activeAnimationImages.Clear();
        ownedSprites.Clear();
        replacementSprites.Clear();
    }

    private void LateUpdate()
    {
        foreach (Image image in activeAnimationImages)
        {
            if (image?.isActiveAndEnabled != true)
                continue;

            ApplyAnimatedFrame(image);
        }
    }

    private Dictionary<string, List<Action<Texture2D>>> CaptureReplacements()
    {
        Dictionary<string, List<Action<Texture2D>>> replacements = new Dictionary<
            string,
            List<Action<Texture2D>>
        >(StringComparer.Ordinal);

        foreach (RawImage image in GetComponentsInChildren<RawImage>(true))
        {
            if (!IsOwnedByThis(image))
                continue;

            CaptureTexture(
                image.texture,
                texture => ApplyRawImageTexture(image, texture),
                replacements
            );
        }
        foreach (Image image in GetComponentsInChildren<Image>(true))
        {
            if (!IsOwnedByThis(image))
                continue;

            CaptureSprite(image.sprite, sprite => ApplyImageSprite(image, sprite), replacements);
        }
        foreach (Selectable selectable in GetComponentsInChildren<Selectable>(true))
        {
            if (!IsOwnedByThis(selectable))
                continue;

            CaptureSelectableSprites(selectable, replacements);
        }
        foreach (MonoBehaviour component in GetComponentsInChildren<MonoBehaviour>(true))
        {
            if (!IsOwnedByThis(component))
                continue;

            CaptureSerializedFields(component, replacements);
        }
        CaptureAnimations(replacements);

        return replacements;
    }

    private void CaptureAnimations(Dictionary<string, List<Action<Texture2D>>> replacements)
    {
        foreach (Animator animator in GetComponentsInChildren<Animator>(true))
        {
            if (!IsOwnedByThis(animator))
                continue;

            Image image = animator.GetComponent<Image>();
            if (image == null || image.sprite == null || animator.runtimeAnimatorController == null)
                continue;

            IReadOnlyList<string> framePaths = ResourceManager.GetExternalAnimationFramePaths(
                image.sprite.texture
            );
            if (framePaths.Count < 2)
                continue;

            Sprite original = image.sprite;
            Dictionary<string, Sprite> frames = new Dictionary<string, Sprite>(
                StringComparer.Ordinal
            );
            foreach (string framePath in framePaths)
            {
                string frameName = Path.GetFileName(framePath);
                frames.Add(frameName, null);
                CapturePath(
                    framePath,
                    texture =>
                    {
                        Sprite frame = CreateReplacementSprite(original, texture);
                        ownedSprites.Add(frame);
                        frames[texture.name] = frame;
                    },
                    replacements
                );
            }

            animationFrames.Add(image, frames);
        }
    }

    private void CaptureSelectableSprites(
        Selectable selectable,
        Dictionary<string, List<Action<Texture2D>>> replacements
    )
    {
        SpriteState state = selectable.spriteState;
        CaptureSprite(
            state.highlightedSprite,
            sprite => ApplyHighlightedSprite(selectable, sprite),
            replacements
        );
        CaptureSprite(
            state.pressedSprite,
            sprite => ApplyPressedSprite(selectable, sprite),
            replacements
        );
        CaptureSprite(
            state.selectedSprite,
            sprite => ApplySelectedSprite(selectable, sprite),
            replacements
        );
        CaptureSprite(
            state.disabledSprite,
            sprite => ApplyDisabledSprite(selectable, sprite),
            replacements
        );
    }

    private void CaptureSerializedFields(
        MonoBehaviour component,
        Dictionary<string, List<Action<Texture2D>>> replacements
    )
    {
        if (
            component == null
            || component == this
            || component.GetType().Assembly != typeof(ExternalContentArt).Assembly
        )
            return;

        for (
            Type type = component.GetType();
            type != null && type != typeof(MonoBehaviour);
            type = type.BaseType
        )
        {
            foreach (
                FieldInfo field in type.GetFields(
                    BindingFlags.Instance
                        | BindingFlags.Public
                        | BindingFlags.NonPublic
                        | BindingFlags.DeclaredOnly
                )
            )
            {
                if (!IsSerializedField(field))
                    continue;

                CaptureField(component, field, replacements);
            }
        }
    }

    private void CaptureField(
        MonoBehaviour component,
        FieldInfo field,
        Dictionary<string, List<Action<Texture2D>>> replacements
    )
    {
        object value = field.GetValue(component);
        if (value is Sprite sprite)
        {
            CaptureSprite(
                sprite,
                replacement => ApplyFieldValue(component, field, replacement),
                replacements
            );
            return;
        }
        if (value is Texture texture)
        {
            CaptureTexture(
                texture,
                replacement => ApplyFieldValue(component, field, replacement),
                replacements
            );
            return;
        }
        if (value is not IList list)
            return;

        for (int index = 0; index < list.Count; index++)
        {
            int capturedIndex = index;
            if (list[index] is Sprite itemSprite)
            {
                CaptureSprite(
                    itemSprite,
                    replacement =>
                        ApplyListValue(component, field, list, capturedIndex, replacement),
                    replacements
                );
            }
            else if (list[index] is Texture itemTexture)
            {
                CaptureTexture(
                    itemTexture,
                    replacement =>
                        ApplyListValue(component, field, list, capturedIndex, replacement),
                    replacements
                );
            }
        }
    }

    private void CaptureTexture(
        Texture texture,
        Action<Texture2D> apply,
        Dictionary<string, List<Action<Texture2D>>> replacements
    )
    {
        if (!ResourceManager.TryGetExternalArtPath(texture, out string path))
            return;

        CapturePath(path, apply, replacements);
    }

    private static void CapturePath(
        string path,
        Action<Texture2D> apply,
        Dictionary<string, List<Action<Texture2D>>> replacements
    )
    {
        if (!replacements.TryGetValue(path, out List<Action<Texture2D>> applications))
        {
            applications = new List<Action<Texture2D>>();
            replacements.Add(path, applications);
        }

        applications.Add(apply);
    }

    private void CaptureSprite(
        Sprite sprite,
        Action<Sprite> apply,
        Dictionary<string, List<Action<Texture2D>>> replacements
    )
    {
        if (sprite == null)
            return;

        CaptureTexture(
            sprite.texture,
            texture =>
            {
                if (!replacementSprites.TryGetValue(sprite, out Sprite replacement))
                {
                    replacement = CreateReplacementSprite(sprite, texture);
                    replacementSprites.Add(sprite, replacement);
                    ownedSprites.Add(replacement);
                }

                apply(replacement);
            },
            replacements
        );
    }

    private static Sprite CreateReplacementSprite(Sprite original, Texture2D texture)
    {
        float scaleX = (float)texture.width / original.texture.width;
        float scaleY = (float)texture.height / original.texture.height;
        Rect originalRect = original.rect;
        Rect rect = new Rect(
            originalRect.x * scaleX,
            originalRect.y * scaleY,
            originalRect.width * scaleX,
            originalRect.height * scaleY
        );
        Vector2 pivot = new Vector2(
            original.pivot.x / originalRect.width,
            original.pivot.y / originalRect.height
        );
        Vector4 border = original.border;
        border.x *= scaleX;
        border.z *= scaleX;
        border.y *= scaleY;
        border.w *= scaleY;
        float pixelsPerUnit = original.pixelsPerUnit * Mathf.Min(scaleX, scaleY);

        Sprite replacement = Sprite.Create(
            texture,
            rect,
            pivot,
            pixelsPerUnit,
            0,
            SpriteMeshType.FullRect,
            border
        );
        replacement.name = original.name;
        return replacement;
    }

    private static bool IsSerializedField(FieldInfo field)
    {
        return !field.IsStatic
            && !field.IsInitOnly
            && (field.IsPublic || field.GetCustomAttribute<SerializeField>() != null);
    }

    private bool IsOwnedByThis(Component component)
    {
        return component != null
            && component.GetComponentInParent<ExternalContentArt>(true) == this;
    }

    private void ActivateExternalAnimations()
    {
        foreach (KeyValuePair<Image, Dictionary<string, Sprite>> animation in animationFrames)
        {
            if (animation.Value.ContainsValue(null) || animation.Key == null)
                continue;

            activeAnimationImages.Add(animation.Key);
            ApplyAnimatedFrame(animation.Key);
        }
    }

    private void ApplyAnimatedFrame(Image image)
    {
        Sprite current = image.sprite;
        if (
            current?.texture != null
            && animationFrames[image].TryGetValue(current.texture.name, out Sprite replacement)
        )
        {
            image.sprite = replacement;
        }
    }

    private static void ApplyRawImageTexture(RawImage image, Texture2D texture)
    {
        if (image != null)
            image.texture = texture;
    }

    private static void ApplyImageSprite(Image image, Sprite sprite)
    {
        if (image != null)
            image.sprite = sprite;
    }

    private static void ApplyHighlightedSprite(Selectable selectable, Sprite sprite)
    {
        if (selectable == null)
            return;

        SpriteState state = selectable.spriteState;
        state.highlightedSprite = sprite;
        selectable.spriteState = state;
    }

    private static void ApplyPressedSprite(Selectable selectable, Sprite sprite)
    {
        if (selectable == null)
            return;

        SpriteState state = selectable.spriteState;
        state.pressedSprite = sprite;
        selectable.spriteState = state;
    }

    private static void ApplySelectedSprite(Selectable selectable, Sprite sprite)
    {
        if (selectable == null)
            return;

        SpriteState state = selectable.spriteState;
        state.selectedSprite = sprite;
        selectable.spriteState = state;
    }

    private static void ApplyDisabledSprite(Selectable selectable, Sprite sprite)
    {
        if (selectable == null)
            return;

        SpriteState state = selectable.spriteState;
        state.disabledSprite = sprite;
        selectable.spriteState = state;
    }

    private static void ApplyFieldValue(
        MonoBehaviour component,
        FieldInfo field,
        UnityEngine.Object value
    )
    {
        if (component != null)
            field.SetValue(component, value);
    }

    private static void ApplyListValue(
        MonoBehaviour component,
        FieldInfo field,
        IList list,
        int index,
        UnityEngine.Object value
    )
    {
        if (component == null || index >= list.Count)
            return;

        list[index] = value;
        if (field.FieldType.IsArray)
            field.SetValue(component, list);
    }
}
