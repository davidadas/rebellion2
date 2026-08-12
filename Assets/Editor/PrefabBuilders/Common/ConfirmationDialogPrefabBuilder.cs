using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Authors the shared confirmation dialog hierarchy.
/// </summary>
public static partial class CommonUIPrefabBuilder
{
    private const string _confirmationDialogTexturePath =
        "Application/Common/UI/ui_common_confirmation_dialog.png";
    private const string _confirmationYesTexturePath =
        "Application/Common/UI/ui_common_confirmation_yes_button.png";
    private const string _confirmationYesPressedTexturePath =
        "Application/Common/UI/ui_common_confirmation_yes_button_pressed.png";
    private const string _confirmationNoTexturePath =
        "Application/Common/UI/ui_common_confirmation_no_button.png";
    private const string _confirmationNoPressedTexturePath =
        "Application/Common/UI/ui_common_confirmation_no_button_pressed.png";
    private const int _confirmationSurfaceWidth = 640;
    private const int _confirmationSurfaceHeight = 480;

    /// <summary>
    /// Creates a confirmation dialog under the supplied surface.
    /// </summary>
    /// <param name="parent">The surface that owns the modal dialog.</param>
    /// <returns>The configured confirmation dialog.</returns>
    internal static ConfirmationDialogView CreateConfirmationDialog(Transform parent)
    {
        GameObject root = new GameObject(
            "ConfirmDialog",
            typeof(RectTransform),
            typeof(ConfirmationDialogView)
        );
        root.transform.SetParent(parent, false);
        SetSourceRect(
            root.GetComponent<RectTransform>(),
            0,
            0,
            _confirmationSurfaceWidth,
            _confirmationSurfaceHeight
        );
        ConfirmationDialogView view = root.GetComponent<ConfirmationDialogView>();
        view.enabled = true;

        GameObject blockerObject = CreateRectObject("InputBlocker", root.transform);
        blockerObject.AddComponent<CanvasRenderer>();
        Image blocker = blockerObject.AddComponent<Image>();
        blocker.color = new Color(0f, 0f, 0f, 0.8f);
        blocker.raycastTarget = true;
        SetSourceRect(
            blocker.rectTransform,
            0,
            0,
            _confirmationSurfaceWidth,
            _confirmationSurfaceHeight
        );

        RawImage background = CreateRawImage(
            "BackgroundImage",
            root.transform,
            _confirmationDialogTexturePath
        );
        SetSourceRect(background.rectTransform, 114, 150, 412, 176);

        RawImage confirmImage = CreateRawImage(
            "ConfirmButtonImage",
            root.transform,
            _confirmationYesTexturePath
        );
        SetSourceRect(confirmImage.rectTransform, 252, 285, 57, 28);
        Button confirmButton = CreateConfirmationButton(
            confirmImage,
            _confirmationYesPressedTexturePath,
            out RawImagePressVisual confirmPressVisual
        );

        RawImage cancelImage = CreateRawImage(
            "CancelButtonImage",
            root.transform,
            _confirmationNoTexturePath
        );
        SetSourceRect(cancelImage.rectTransform, 343, 285, 57, 28);
        Button cancelButton = CreateConfirmationButton(
            cancelImage,
            _confirmationNoPressedTexturePath,
            out RawImagePressVisual cancelPressVisual
        );

        TextMeshProUGUI message = CreateInputText(
            "MessageTextField",
            root.transform,
            "Are you sure you want to quit?"
        );
        message.color = Color.white;
        message.fontSize = 13;
        message.alignment = TextAlignmentOptions.Center;
        message.textWrappingMode = TextWrappingModes.Normal;
        message.overflowMode = TextOverflowModes.Overflow;
        SetSourceRect(message.rectTransform, 160, 172, 320, 100);

        AssignReference(view, "backgroundImage", background);
        AssignColor(view, "messageTextColor", Color.white);
        AssignReference(view, "confirmButtonImage", confirmImage);
        AssignReference(view, "confirmButton", confirmButton);
        AssignReference(view, "confirmButtonPressVisual", confirmPressVisual);
        AssignReference(view, "confirmButtonUpTexture", confirmImage.texture);
        AssignReference(
            view,
            "confirmButtonDownTexture",
            LoadRequiredTexture(_confirmationYesPressedTexturePath)
        );
        AssignReference(view, "cancelButtonImage", cancelImage);
        AssignReference(view, "cancelButton", cancelButton);
        AssignReference(view, "cancelButtonPressVisual", cancelPressVisual);
        AssignReference(view, "cancelButtonUpTexture", cancelImage.texture);
        AssignReference(
            view,
            "cancelButtonDownTexture",
            LoadRequiredTexture(_confirmationNoPressedTexturePath)
        );
        AssignReference(view, "messageTextField", message);
        root.SetActive(false);
        return view;
    }

    private static Button CreateConfirmationButton(
        RawImage image,
        string pressedTexturePath,
        out RawImagePressVisual pressVisual
    )
    {
        image.raycastTarget = true;
        Button button = image.gameObject.AddComponent<Button>();
        button.targetGraphic = image;
        button.transition = Selectable.Transition.None;

        pressVisual = image.gameObject.AddComponent<RawImagePressVisual>();
        pressVisual.enabled = true;
        AssignReference(pressVisual, "image", image);
        AssignReference(pressVisual, "button", button);
        pressVisual.SetTextures(image.texture, LoadRequiredTexture(pressedTexturePath));
        return button;
    }

    private static void AssignColor(Object target, string propertyName, Color value)
    {
        UnityEditor.SerializedObject serializedObject = new UnityEditor.SerializedObject(target);
        UnityEditor.SerializedProperty property = serializedObject.FindProperty(propertyName);
        if (property == null)
            throw new System.MissingMemberException(target.GetType().Name, propertyName);
        property.colorValue = value;
        serializedObject.ApplyModifiedPropertiesWithoutUndo();
    }
}
