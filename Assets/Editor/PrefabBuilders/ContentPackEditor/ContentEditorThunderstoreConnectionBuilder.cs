using System.IO;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using static ContentEditorControlBuilder;

/// <summary>
/// Authors the shared Thunderstore connection and protected-token setup controls.
/// </summary>
internal static class ContentEditorThunderstoreConnectionBuilder
{
    /// <summary>
    /// Creates the shared account controls independently of a publishing draft.
    /// </summary>
    /// <param name="parent">The application workbench.</param>
    /// <returns>The shared Thunderstore connection dialog.</returns>
    internal static ContentEditorThunderstoreConnectionView Build(Transform parent)
    {
        Image overlay = Surface("Connection", parent, new Color(0, 0, 0, 0.7f));
        Stretch(overlay.rectTransform);
        overlay.raycastTarget = true;
        ContentEditorThunderstoreConnectionView view =
            overlay.gameObject.AddComponent<ContentEditorThunderstoreConnectionView>();
        Image panel = Surface("ConnectionPanel", overlay.transform, ContentEditorColorRole.Panel);
        RectTransform rect = panel.rectTransform;
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = new Vector2(1040, 740);
        Assign(view, "_panel", rect);
        TMP_Text heading = Label("Heading", rect, "Connect Thunderstore", 24);
        Rect(heading.rectTransform, 28, 20, 984, 36);
        Assign(view, "_heading", heading);
        RectTransform setup = Layer("Setup", rect);
        Rect(setup, 0, 0, 1040, 656);
        Assign(view, "_setup", setup.gameObject);
        Rect(
            Label(
                "Help",
                setup,
                "Create a team token on Thunderstore, then paste it here.",
                15,
                true
            ).rectTransform,
            28,
            64,
            720,
            28
        );
        Button tokenHelp = Button("GetToken", setup, "Open team settings");
        Rect((RectTransform)tokenHelp.transform, 766, 60, 246, 36);
        Assign(view, "_tokenHelpButton", tokenHelp);
        Rect(
            Label(
                "ChooseTeamStep",
                setup,
                "1. Sign in, then choose or create your team.",
                16
            ).rectTransform,
            28,
            116,
            600,
            28
        );
        Screenshot(setup, "Teams", new UnityEngine.Rect(0, 126, 720, 204), 28, 156, 600, 170);
        TextMeshProUGUI accountStep = Label(
            "CreateAccountStep",
            setup,
            "2. Open Service Accounts, then choose Add service account.",
            16
        );
        Rect(accountStep.rectTransform, 28, 350, 600, 40);
        accountStep.textWrappingMode = TextWrappingModes.Normal;
        Screenshot(
            setup,
            "ServiceAccounts",
            new UnityEngine.Rect(0, 135, 720, 240),
            28,
            404,
            600,
            200
        );
        Rect(
            Label(
                "CopyTokenStep",
                setup,
                "3. Create the account and copy its token.",
                16
            ).rectTransform,
            656,
            116,
            356,
            28
        );
        Screenshot(setup, "Token", new UnityEngine.Rect(0, 0, 660, 370), 656, 156, 356, 200);
        TextMeshProUGUI reminder = Label(
            "TokenReminder",
            setup,
            "Give the account a nickname when asked. The token is shown only once; use the copy button beside it.",
            14,
            true
        );
        Rect(reminder.rectTransform, 656, 372, 356, 64);
        reminder.textWrappingMode = TextWrappingModes.Normal;
        Rect(
            Label("PasteTokenStep", setup, "4. Paste the token and connect.", 16).rectTransform,
            656,
            456,
            356,
            28
        );
        TMP_InputField token = Input(
            "ThunderstoreToken",
            setup,
            "Paste your service-account token"
        );
        Rect((RectTransform)token.transform, 656, 492, 356, 40);
        token.contentType = TMP_InputField.ContentType.Password;
        token.characterLimit = 4096;
        Assign(view, "_token", token);
        TextMeshProUGUI session = Label(
            "ConnectionDuration",
            setup,
            "Connect saves your token securely on this device. Disconnect removes it.",
            14,
            true
        );
        Rect(session.rectTransform, 656, 546, 356, 48);
        session.textWrappingMode = TextWrappingModes.Normal;
        Button disconnect = PlainButton("DisconnectThunderstore", rect, "Disconnect");
        Rect((RectTransform)disconnect.transform, 876, 604, 136, 32);
        Assign(view, "_disconnectButton", disconnect);
        Rect(
            Label(
                "ScreenshotCredit",
                setup,
                "Example screenshots: mbround18 / valheim-docker (BSD-3-Clause).",
                11,
                true
            ).rectTransform,
            28,
            626,
            600,
            20
        );
        RectTransform connected = Layer("Connected", rect);
        Rect(connected, 24, 64, 432, 28);
        TMP_Text teams = Label("ConnectedTeams", connected, string.Empty, 17);
        Rect(teams.rectTransform, 0, 0, 432, 28);
        teams.GetComponent<ContentEditorColorBinding>().SetRole(ContentEditorColorRole.Success);
        teams.textWrappingMode = TextWrappingModes.Normal;
        teams.richText = false;
        Assign(view, "_connected", connected.gameObject);
        Assign(view, "_connectedTeams", teams);
        connected.gameObject.SetActive(false);
        TextMeshProUGUI status = Label("ConnectionStatus", rect, string.Empty, 14);
        Rect(status.rectTransform, 28, 656, 680, 60);
        status.textWrappingMode = TextWrappingModes.Normal;
        status.richText = false;
        Assign(view, "_connectionStatus", status);
        Button close = Button("CloseConnection", rect, "Close");
        Rect((RectTransform)close.transform, 744, 680, 120, 36);
        Assign(view, "_connectionCloseButton", close);
        Button connect = Button("ConnectThunderstore", rect, "Connect");
        Rect((RectTransform)connect.transform, 880, 680, 132, 36);
        Assign(view, "_connectButton", connect);
        overlay.gameObject.SetActive(false);
        return view;
    }

    /// <summary>
    /// Shows a focused region of an instructional screenshot without changing its source image.
    /// </summary>
    /// <param name="parent">The connection dialog.</param>
    /// <param name="name">The screenshot asset name.</param>
    /// <param name="crop">The visible source pixels, measured from the top-left corner.</param>
    /// <param name="left">The display's left coordinate.</param>
    /// <param name="top">The display's top coordinate.</param>
    /// <param name="width">The display width.</param>
    /// <param name="height">The display height.</param>
    private static void Screenshot(
        Transform parent,
        string name,
        UnityEngine.Rect crop,
        float left,
        float top,
        float width,
        float height
    )
    {
        Texture2D texture =
            AssetDatabase.LoadAssetAtPath<Texture2D>(
                $"Assets/Art/ContentEditor/Thunderstore/{name}.png"
            )
            ?? throw new FileNotFoundException(
                $"Thunderstore guide screenshot '{name}' is missing."
            );
        RawImage image = Layer(name + "Screenshot", parent).gameObject.AddComponent<RawImage>();
        image.texture = texture;
        image.raycastTarget = false;
        image.uvRect = new UnityEngine.Rect(
            crop.x / texture.width,
            1 - crop.yMax / texture.height,
            crop.width / texture.width,
            crop.height / texture.height
        );
        Rect(image.rectTransform, left, top, width, height);
    }
}
