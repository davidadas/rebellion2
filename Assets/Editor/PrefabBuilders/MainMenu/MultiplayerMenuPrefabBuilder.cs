using System.IO;
using Rebellion.Multiplayer;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using static MenuPrefabBuilder;
using Object = UnityEngine.Object;

/// <summary>
/// Authors the wide multiplayer console from shared native controls and runtime content bindings.
/// </summary>
public static class MultiplayerMenuPrefabBuilder
{
    public const string PrefabPath = "Assets/Prefabs/UI/MainMenu/MultiplayerMenu.prefab";
    private static readonly Color _text = new Color(0.875f, 0.910f, 0.941f);
    private static readonly Color _muted = new Color(0.573f, 0.635f, 0.706f);
    private static readonly Color _accent = new Color(0.373f, 0.659f, 0.925f);

    /// <summary>
    /// Rebuilds the browser, create dialog, waiting room, save picker, and account presentation.
    /// </summary>
    public static void Rebuild()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(PrefabPath));
        GameObject root = new GameObject(
            "MultiplayerMenu",
            typeof(RectTransform),
            typeof(UIWindow)
        );
        root.SetActive(false);
        ConfigureWindowRoot(root.GetComponent<UIWindow>());
        RectTransform rootRect = (RectTransform)root.transform;
        SetSourceRect(rootRect, 0, 0, 640, 448);
        MultiplayerMenuOverlay view = root.AddComponent<MultiplayerMenuOverlay>();
        RectTransform console = MenuControlPrefabBuilder.CreateWideWindow(root.transform);
        console.name = "Console";
        console.localScale = Vector3.one * 0.64f;
        Label(console, "MULTIPLAYER", new RectInt(49, 52, 400, 30), 24, _accent);
        Bind(
            view,
            "pageTitle",
            Label(console, "SERVER BROWSER", new RectInt(50, 86, 600, 21), 12, _muted)
        );
        Bind(
            view,
            "identityText",
            Label(
                console,
                "",
                new RectInt(650, 55, 304, 25),
                14,
                _text,
                TextAlignmentOptions.MidlineRight
            )
        );
        Bind(
            view,
            "closeButton",
            Action(console, "Close", "CANCEL", new RectInt(774, 630, 180, 28))
        );
        Bind(view, "statusText", Label(console, "", new RectInt(50, 630, 704, 28), 13, _accent));
        BuildBrowser(console, view);
        BuildLobby(console, view);
        BuildCreateDialog(console, view);
        BuildSaveDialog(console, view);
        BuildPasswordDialog(console, view);
        BuildAuthentication(console, view);
        RectTransform confirmation = Group(
            console,
            "LeaveConfirmation",
            new RectInt(0, 0, 1000, 700)
        );
        Surface(
            confirmation,
            "Scrim",
            new RectInt(20, 20, 960, 660),
            new Color(0, 0, 0, 0.8f)
        ).raycastTarget = true;
        ConfirmationDialogView confirm = CommonUIPrefabBuilder.InstantiateConfirmationDialog(
            confirmation
        );
        SetSourceRect((RectTransform)confirm.transform, 180, 110, 640, 480);
        Bind(view, "leaveConfirmation", confirm);
        Bind(view, "leaveConfirmationRoot", confirmation.gameObject);
        confirmation.gameObject.SetActive(false);
        PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        Object.DestroyImmediate(root);
    }

    /// <summary>
    /// Builds the full-width discovery table and general conversation area.
    /// </summary>
    /// <param name="parent">The console surface.</param>
    /// <param name="view">The runtime controller.</param>
    private static void BuildBrowser(Transform parent, MultiplayerMenuOverlay view)
    {
        RectTransform page = Group(parent, "Browser", new RectInt(0, 0, 1000, 700));
        Bind(view, "browserPage", page.gameObject);
        Bind(
            view,
            "searchInput",
            Input(page, "Search", "Search games...", new RectInt(50, 111, 340, 28))
        );
        Bind(view, "packFilter", Dropdown(page, "PackFilter", new RectInt(404, 111, 300, 28)));
        Bind(
            view,
            "browserCountText",
            Label(
                page,
                "",
                new RectInt(718, 111, 236, 28),
                13,
                _muted,
                TextAlignmentOptions.MidlineRight
            )
        );
        Panel(page, "Table", new RectInt(50, 154, 904, 226));
        Button[] sortButtons =
        {
            SortHeader(page, "Game", "GAME \u25b2", new RectInt(70, 161, 350, 24)),
            SortHeader(page, "Content", "CONTENT PACK", new RectInt(432, 161, 290, 24)),
            SortHeader(page, "Players", "PLAYERS", new RectInt(762, 161, 90, 24)),
            SortHeader(page, "Access", "ACCESS", new RectInt(870, 161, 64, 24)),
        };
        SerializedObject headerReferences = new SerializedObject(view);
        SerializedProperty headers = headerReferences.FindProperty("sortButtons");
        headers.arraySize = sortButtons.Length;
        for (int index = 0; index < sortButtons.Length; index++)
            headers.GetArrayElementAtIndex(index).objectReferenceValue = sortButtons[index];
        headerReferences.ApplyModifiedPropertiesWithoutUndo();
        ScrollAreaView scroll = Scroll(
            page,
            "Games",
            new RectInt(62, 187, 880, 181),
            out RectTransform rows
        );
        Bind(view, "gameScrollArea", scroll);
        Bind(view, "gameRowsRoot", rows);
        GameObject rowPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(
            MenuControlPrefabBuilder.ServerRowPath
        );
        GameObject rowInstance = Object.Instantiate(rowPrefab, rows, false);
        rowInstance.name = "GameRowTemplate";
        Button template = rowInstance.GetComponent<Button>();
        Bind(view, "gameRowTemplate", template.gameObject);
        template.gameObject.SetActive(false);
        Bind(
            view,
            "emptyBrowserText",
            Label(
                page,
                "No games found.",
                new RectInt(68, 195, 350, 28),
                15,
                _muted,
                TextAlignmentOptions.TopLeft
            )
        );
        Bind(
            view,
            "refreshButton",
            Action(page, "Refresh", "REFRESH", new RectInt(50, 394, 144, 30))
        );
        Bind(view, "hostButton", Action(page, "Host", "HOST GAME", new RectInt(586, 394, 170, 30)));
        Bind(
            view,
            "resumeButton",
            Action(page, "Resume", "RESUME GAME", new RectInt(212, 394, 180, 30))
        );
        Bind(view, "joinButton", Action(page, "Join", "JOIN GAME", new RectInt(774, 394, 180, 30)));
        Chat(page, view, false, new RectInt(50, 444, 904, 174));
    }

    /// <summary>
    /// Builds the two human player rows, campaign information, save command, chat, and launch controls.
    /// </summary>
    /// <param name="parent">The console surface.</param>
    /// <param name="view">The runtime controller.</param>
    private static void BuildLobby(Transform parent, MultiplayerMenuOverlay view)
    {
        RectTransform page = Group(parent, "Lobby", new RectInt(0, 0, 1000, 700));
        Bind(view, "lobbyPage", page.gameObject);
        Panel(page, "Campaign", new RectInt(50, 111, 304, 263));
        CreateRawImage(
            "CampaignPreview",
            page,
            "Pack/Shared/Strategy/Advisor/Images/advisor-report-galaxy.png",
            61,
            122,
            282,
            117
        );
        Bind(view, "campaignText", Label(page, "", new RectInt(65, 251, 274, 112), 15, _text));
        Label(page, "PLAYERS", new RectInt(376, 111, 578, 25), 12, _muted);
        for (int i = 0; i < 2; i++)
        {
            int top = 148 + i * 90;
            Image row = Panel(page, "Player" + i, new RectInt(374, top, 580, 78));
            Bind(
                view,
                i == 0 ? "hostNameText" : "guestNameText",
                Label(row.transform, "", new RectInt(12, 9, 258, 25), 17, _text)
            );
            Bind(
                view,
                i == 0 ? "hostReadyText" : "guestReadyText",
                Label(row.transform, "", new RectInt(12, 41, 258, 23), 13, _muted)
            );
            Bind(
                view,
                i == 0 ? "hostFactionDropdown" : "guestFactionDropdown",
                Dropdown(row.transform, "Faction", new RectInt(289, 13, 275, 30))
            );
        }
        Bind(
            view,
            "loadButton",
            Action(page, "LoadGame", "LOAD GAME", new RectInt(374, 342, 167, 30))
        );
        Label(page, "GALAXY SIZE", new RectInt(554, 322, 167, 16), 12, _muted);
        Bind(
            view,
            "galaxySizeDropdown",
            Dropdown(page, "GalaxySize", new RectInt(554, 342, 167, 30))
        );
        Label(page, "VICTORY CONDITION", new RectInt(734, 322, 220, 16), 12, _muted);
        Bind(
            view,
            "victoryConditionDropdown",
            Dropdown(page, "VictoryCondition", new RectInt(734, 342, 220, 30))
        );
        Chat(page, view, true, new RectInt(50, 394, 904, 192));
        Bind(
            view,
            "leaveButton",
            Action(page, "Leave", "LEAVE LOBBY", new RectInt(50, 600, 173, 30))
        );
        Bind(view, "readyButton", Action(page, "Ready", "READY", new RectInt(239, 600, 170, 30)));
        Bind(
            view,
            "launchButton",
            Action(page, "Launch", "START GAME", new RectInt(774, 600, 180, 30))
        );
        page.gameObject.SetActive(false);
    }

    /// <summary>
    /// Builds the content-pack, name, and optional password creation dialog.
    /// </summary>
    /// <param name="parent">The console surface.</param>
    /// <param name="view">The runtime controller.</param>
    private static void BuildCreateDialog(Transform parent, MultiplayerMenuOverlay view)
    {
        RectTransform dialog = Dialog(
            parent,
            "CreateDialog",
            "HOST GAME",
            new RectInt(180, 170, 640, 360)
        );
        Bind(view, "createDialog", dialog.parent.gameObject);
        Label(dialog, "GAME NAME", new RectInt(26, 62, 581, 21), 12, _muted);
        Bind(
            view,
            "gameNameInput",
            Input(dialog, "GameName", "Name your game", new RectInt(26, 86, 588, 32))
        );
        Label(dialog, "PASSWORD (OPTIONAL)", new RectInt(26, 135, 581, 21), 12, _muted);
        TMP_InputField password = Input(
            dialog,
            "Password",
            string.Empty,
            new RectInt(26, 159, 588, 32)
        );
        password.contentType = TMP_InputField.ContentType.Password;
        Bind(view, "hostPasswordInput", password);
        Label(dialog, "CONTENT PACK", new RectInt(26, 210, 581, 21), 12, _muted);
        Bind(
            view,
            "contentPackDropdown",
            Dropdown(dialog, "ContentPack", new RectInt(26, 234, 588, 32))
        );
        Bind(
            view,
            "createStatusText",
            Label(dialog, "", new RectInt(26, 275, 588, 26), 13, _muted)
        );
        Bind(
            view,
            "cancelCreateButton",
            Action(dialog, "Cancel", "CANCEL", new RectInt(26, 306, 170, 30))
        );
        Bind(
            view,
            "createButton",
            Action(dialog, "Create", "CREATE GAME", new RectInt(414, 306, 200, 30))
        );
    }

    /// <summary>
    /// Builds the native save picker using the same reusable scrolling controls as discovery.
    /// </summary>
    /// <param name="parent">The console surface.</param>
    /// <param name="view">The runtime controller.</param>
    private static void BuildSaveDialog(Transform parent, MultiplayerMenuOverlay view)
    {
        RectTransform dialog = Dialog(
            parent,
            "SaveDialog",
            "LOAD GAME",
            new RectInt(100, 60, 800, 580)
        );
        Bind(view, "saveDialog", dialog.parent.gameObject);
        Image container = Panel(dialog, "SaveTable", new RectInt(26, 62, 748, 400));
        OptionsSaveListView list = MenuControlPrefabBuilder.CreateSaveList(container.transform);
        list.SetLoadOnlyLayout(748, 400);
        Bind(view, "saveList", list);
        Bind(view, "saveStatusText", Label(dialog, "", new RectInt(26, 473, 748, 36), 13, _muted));
        Bind(
            view,
            "cancelSaveButton",
            Action(dialog, "Cancel", "CANCEL", new RectInt(26, 524, 170, 30))
        );
        Bind(
            view,
            "saveLoadButton",
            Action(dialog, "Load", "LOAD GAME", new RectInt(574, 524, 200, 30))
        );
    }

    /// <summary>
    /// Builds the protected-game admission dialog without exposing the host's password.
    /// </summary>
    /// <param name="parent">The console surface.</param>
    /// <param name="view">The runtime controller.</param>
    private static void BuildPasswordDialog(Transform parent, MultiplayerMenuOverlay view)
    {
        RectTransform dialog = Dialog(
            parent,
            "PasswordDialog",
            "ENTER GAME PASSWORD",
            new RectInt(240, 230, 520, 230)
        );
        Bind(view, "passwordDialog", dialog.parent.gameObject);
        TMP_InputField password = Input(
            dialog,
            "JoinPassword",
            string.Empty,
            new RectInt(26, 77, 468, 32)
        );
        password.contentType = TMP_InputField.ContentType.Password;
        Bind(view, "joinPasswordInput", password);
        Bind(
            view,
            "passwordStatusText",
            Label(dialog, "", new RectInt(26, 121, 468, 34), 13, _muted)
        );
        Bind(
            view,
            "cancelPasswordButton",
            Action(dialog, "Cancel", "CANCEL", new RectInt(26, 165, 170, 30))
        );
        Bind(
            view,
            "confirmJoinButton",
            Action(dialog, "Join", "JOIN GAME", new RectInt(324, 165, 170, 30))
        );
    }

    /// <summary>
    /// Builds the store sign-in gate displayed only when no saved account session is available.
    /// </summary>
    /// <param name="parent">The console surface.</param>
    /// <param name="view">The runtime controller.</param>
    private static void BuildAuthentication(Transform parent, MultiplayerMenuOverlay view)
    {
        RectTransform dialog = Dialog(
            parent,
            "Authentication",
            "PLAY ONLINE",
            new RectInt(220, 190, 560, 320)
        );
        Bind(view, "authDialog", dialog.parent.gameObject);
        Bind(view, "authStatusText", Label(dialog, "", new RectInt(26, 207, 508, 32), 13, _muted));
        Label(
            dialog,
            "Continue with your Steam or GOG account. Your sign-in is remembered.",
            new RectInt(26, 72, 508, 74),
            17,
            _text
        );
        Bind(
            view,
            "steamSignInButton",
            Action(dialog, "Steam", "STEAM", new RectInt(26, 167, 242, 34))
        );
        Bind(view, "gogSignInButton", Action(dialog, "Gog", "GOG", new RectInt(292, 167, 242, 34)));
        Bind(
            view,
            "authBackButton",
            Action(dialog, "Back", "CANCEL", new RectInt(26, 266, 170, 30))
        );
    }

    /// <summary>
    /// Creates a scrolling, plain-text transcript and an Enter-to-send input row.
    /// </summary>
    /// <param name="parent">The current page.</param>
    /// <param name="view">The runtime controller.</param>
    /// <param name="lobby">Whether this conversation belongs to the room.</param>
    /// <param name="bounds">The conversation bounds.</param>
    private static void Chat(
        Transform parent,
        MultiplayerMenuOverlay view,
        bool lobby,
        RectInt bounds
    )
    {
        string prefix = lobby ? "lobby" : "general";
        GameObject root = Object.Instantiate(
            AssetDatabase.LoadAssetAtPath<GameObject>(MenuControlPrefabBuilder.ChatPanelPath),
            parent,
            false
        );
        root.name = prefix + "Chat";
        SetSourceRect(
            (RectTransform)root.transform,
            bounds.x,
            bounds.y,
            bounds.width,
            bounds.height
        );
        MenuChatPanelView chat = root.GetComponent<MenuChatPanelView>();
        chat.SetLayout(lobby ? "LOBBY CHAT" : "GENERAL CHAT", bounds.width, bounds.height);
        Bind(view, prefix + "ChatText", chat.Transcript);
        Bind(view, prefix + "ChatInput", chat.MessageInput);
        Bind(view, prefix + "SendButton", chat.SendButton);
    }

    /// <summary>
    /// Creates an isolated dialog and a raycast-blocking backdrop within the native console.
    /// </summary>
    /// <param name="parent">The console surface.</param>
    /// <param name="name">The dialog hierarchy name.</param>
    /// <param name="title">The dialog heading.</param>
    /// <param name="bounds">The dialog bounds.</param>
    /// <returns>The opaque dialog content surface.</returns>
    private static RectTransform Dialog(Transform parent, string name, string title, RectInt bounds)
    {
        RectTransform root = Group(parent, name, new RectInt(0, 0, 1000, 700));
        Surface(
            root,
            "Scrim",
            new RectInt(20, 20, 960, 660),
            new Color(0, 0, 0, 0.7f)
        ).raycastTarget = true;
        MenuControlPrefabBuilder.CreateMenuBacking(root, "Backing", bounds);
        Image panel = Panel(root, "Dialog", bounds);
        Label(panel.transform, title, new RectInt(26, 20, bounds.width - 52, 32), 22, _accent);
        root.gameObject.SetActive(false);
        return panel.rectTransform;
    }

    /// <summary>
    /// Creates a logical group using the project's top-left source-coordinate convention.
    /// </summary>
    /// <param name="parent">The group's owner.</param>
    /// <param name="name">The hierarchy name.</param>
    /// <param name="bounds">The group's bounds.</param>
    /// <returns>The authored group.</returns>
    private static RectTransform Group(Transform parent, string name, RectInt bounds)
    {
        RectTransform rect = new GameObject(
            name,
            typeof(RectTransform)
        ).GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        SetSourceRect(rect, bounds.x, bounds.y, bounds.width, bounds.height);
        return rect;
    }

    /// <summary>
    /// Places an existing shared action-button prefab without changing its artwork or feedback.
    /// </summary>
    /// <param name="parent">The button owner.</param>
    /// <param name="name">The hierarchy name.</param>
    /// <param name="caption">The button label.</param>
    /// <param name="bounds">The button bounds.</param>
    /// <returns>The native button.</returns>
    private static Button Action(Transform parent, string name, string caption, RectInt bounds)
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
            "Assets/Prefabs/UI/Common/MenuActionButton.prefab"
        );
        GameObject instance = Object.Instantiate(prefab, parent, false);
        instance.name = name;
        SetSourceRect(
            (RectTransform)instance.transform,
            bounds.x,
            bounds.y,
            bounds.width,
            bounds.height
        );
        TMP_Text label = instance.GetComponentInChildren<TMP_Text>(true);
        label.text = caption;
        label.fontSize = 14;
        label.color = _text;
        return instance.GetComponent<Button>();
    }

    /// <summary>
    /// Reuses the shared content panel independently of the old menu's fixed navigation column.
    /// </summary>
    /// <param name="parent">The panel owner.</param>
    /// <param name="name">The hierarchy name.</param>
    /// <param name="bounds">The panel bounds.</param>
    /// <returns>The shared panel image.</returns>
    private static Image Panel(Transform parent, string name, RectInt bounds)
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
            "Assets/Prefabs/UI/Common/MenuWindow.prefab"
        );
        GameObject instance = Object.Instantiate(
            prefab.transform.Find("ContentPanel").gameObject,
            parent,
            false
        );
        instance.name = name;
        SetSourceRect(
            (RectTransform)instance.transform,
            bounds.x,
            bounds.y,
            bounds.width,
            bounds.height
        );
        LayoutElement layout = instance.GetComponent<LayoutElement>();
        if (layout != null)
            Object.DestroyImmediate(layout);
        return instance.GetComponent<Image>();
    }

    /// <summary>
    /// Creates a native text label using the shared font and text-shadow setup.
    /// </summary>
    /// <param name="parent">The label owner.</param>
    /// <param name="value">The initial text.</param>
    /// <param name="bounds">The label bounds.</param>
    /// <param name="size">The source-space font size.</param>
    /// <param name="color">The text colour.</param>
    /// <param name="alignment">The text alignment.</param>
    /// <returns>The native text label.</returns>
    private static TMP_Text Label(
        Transform parent,
        string value,
        RectInt bounds,
        int size,
        Color color,
        TextAlignmentOptions alignment = TextAlignmentOptions.TopLeft
    )
    {
        TMP_Text label = CreateTextLabel("Label", parent);
        label.text = value;
        label.fontSize = size;
        label.color = color;
        label.alignment = alignment;
        label.textWrappingMode = TextWrappingModes.Normal;
        label.overflowMode = TextOverflowModes.Ellipsis;
        label.richText = false;
        SetSourceRect(label.rectTransform, bounds.x, bounds.y, bounds.width, bounds.height);
        return label;
    }

    /// <summary>
    /// Creates a shared text input inside its matching native panel.
    /// </summary>
    /// <param name="parent">The input owner.</param>
    /// <param name="name">The hierarchy name.</param>
    /// <param name="placeholder">The empty-field hint.</param>
    /// <param name="bounds">The field bounds.</param>
    /// <returns>The authored input.</returns>
    private static TMP_InputField Input(
        Transform parent,
        string name,
        string placeholder,
        RectInt bounds
    )
    {
        Panel(parent, name + "Frame", bounds);
        TMP_InputField input = CreateTextInputField(
            name,
            parent,
            placeholder,
            bounds.x + 5,
            bounds.y + 3,
            bounds.width - 10,
            bounds.height - 6
        );
        input.textComponent.fontSize = 15;
        input.textComponent.richText = false;
        input.textComponent.alignment = TextAlignmentOptions.MidlineLeft;
        if (input.placeholder is TMP_Text hint)
        {
            hint.fontSize = 15;
            hint.alignment = TextAlignmentOptions.MidlineLeft;
        }
        return input;
    }

    /// <summary>
    /// Creates a clickable table heading with the shared menu typography.
    /// </summary>
    /// <param name="parent">The table owner.</param>
    /// <param name="name">The column identifier.</param>
    /// <param name="caption">The displayed heading.</param>
    /// <param name="bounds">The heading rectangle.</param>
    /// <returns>The sortable header button.</returns>
    private static Button SortHeader(Transform parent, string name, string caption, RectInt bounds)
    {
        Image hit = Surface(parent, "Sort" + name, bounds, Color.clear);
        hit.raycastTarget = true;
        Button button = hit.gameObject.AddComponent<Button>();
        button.targetGraphic = hit;
        button.transition = Selectable.Transition.None;
        TMP_Text label = Label(
            hit.transform,
            caption,
            new RectInt(0, 0, bounds.width, bounds.height),
            12,
            _muted,
            TextAlignmentOptions.MidlineLeft
        );
        label.raycastTarget = false;
        return button;
    }

    /// <summary>
    /// Creates a native dropdown using the shared panel, font, and arrow artwork.
    /// </summary>
    /// <param name="parent">The dropdown owner.</param>
    /// <param name="name">The hierarchy name.</param>
    /// <param name="bounds">The dropdown bounds.</param>
    /// <returns>The runtime-populated dropdown.</returns>
    private static TMP_Dropdown Dropdown(Transform parent, string name, RectInt bounds)
    {
        return MenuControlPrefabBuilder.CreateDropdown(parent, name, new[] { "" }, bounds);
    }

    /// <summary>
    /// Places the shared scroll-area prefab with its native scrollbar.
    /// </summary>
    /// <param name="parent">The scroll owner.</param>
    /// <param name="name">The hierarchy name.</param>
    /// <param name="bounds">The scroll bounds.</param>
    /// <param name="rows">Receives the content root.</param>
    /// <returns>The scrolling control.</returns>
    private static ScrollAreaView Scroll(
        Transform parent,
        string name,
        RectInt bounds,
        out RectTransform rows
    )
    {
        return CreateScrollAreaView(
            parent,
            name,
            bounds.x,
            bounds.y,
            bounds.width,
            bounds.height,
            0,
            0,
            bounds.width,
            bounds.height,
            bounds.width - 13,
            0,
            13,
            bounds.height,
            out rows
        );
    }

    /// <summary>
    /// Creates a flat native backing or modal scrim.
    /// </summary>
    /// <param name="parent">The image owner.</param>
    /// <param name="name">The hierarchy name.</param>
    /// <param name="bounds">The image bounds.</param>
    /// <param name="color">The fill colour.</param>
    /// <returns>The backing image.</returns>
    private static Image Surface(Transform parent, string name, RectInt bounds, Color color)
    {
        Image image = Group(parent, name, bounds).gameObject.AddComponent<Image>();
        image.color = color;
        image.raycastTarget = false;
        return image;
    }

    /// <summary>
    /// Assigns an authored reference to the runtime view's serialized contract.
    /// </summary>
    /// <param name="view">The runtime controller.</param>
    /// <param name="field">The serialized field name.</param>
    /// <param name="value">The authored component or object.</param>
    private static void Bind(MultiplayerMenuOverlay view, string field, Object value)
    {
        AssignReference(view, field, value);
    }
}
