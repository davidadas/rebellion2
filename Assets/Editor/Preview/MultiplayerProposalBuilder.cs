using System.IO;
using System.Xml.Linq;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using static MenuPrefabBuilder;
using Object = UnityEngine.Object;

/// <summary>
/// Identifies alternative arrangements of the same native multiplayer controls.
/// </summary>
public enum MultiplayerProposalLayout
{
    WideConsole,
    SplitBriefing,
    PlayerStations,
}

/// <summary>
/// Identifies the screen being reviewed before multiplayer implementation.
/// </summary>
public enum MultiplayerProposalPage
{
    Browser,
    CreateGame,
    Lobby,
}

/// <summary>
/// Selects existing application or faction artwork for a proposal.
/// </summary>
public enum MultiplayerProposalTheme
{
    Application,
    Empire,
    Alliance,
}

/// <summary>
/// Authors native Unity layout proposals from the project's existing controls and theme artwork.
/// </summary>
public static class MultiplayerProposalBuilder
{
    public const string AssetDirectory = "Assets/Editor/Preview/MultiplayerGenerated";
    public const int Width = 1000;
    public const int Height = 700;

    private const string _actionPrefab = "Assets/Prefabs/UI/Common/MenuActionButton.prefab";
    private const string _windowPrefab = "Assets/Prefabs/UI/Common/MenuWindow.prefab";
    private const string _packRoot = "Assets/Content/Packs/ClassicGalacticCivilWar";
    private static readonly Color _text = new Color(0.875f, 0.910f, 0.941f);
    private static readonly Color _muted = new Color(0.573f, 0.635f, 0.706f);

    /// <summary>
    /// Builds an isolated world-space canvas containing an actual Unity UI proposal.
    /// </summary>
    /// <param name="layout">The layout arrangement to inspect.</param>
    /// <param name="page">The browser, create dialog, or lobby screen.</param>
    /// <param name="theme">The existing artwork family to use.</param>
    /// <returns>The canvas hierarchy, owned by the preview renderer.</returns>
    public static GameObject Build(
        MultiplayerProposalLayout layout,
        MultiplayerProposalPage page,
        MultiplayerProposalTheme theme
    )
    {
        GameObject root = new GameObject(
            $"Multiplayer {layout} {page} {theme}",
            typeof(RectTransform),
            typeof(Canvas),
            typeof(CanvasScaler),
            typeof(GraphicRaycaster)
        );
        Canvas canvas = root.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        RectTransform rectangle = (RectTransform)root.transform;
        rectangle.pivot = new Vector2(0f, 1f);
        rectangle.sizeDelta = new Vector2(Width, Height);
        Color accent = GetAccent(theme);
        Fill(
            rectangle,
            "Backdrop",
            new RectInt(0, 0, Width, Height),
            new Color(0.025f, 0.035f, 0.05f)
        );
        Panel(rectangle, "WindowBody", new RectInt(34, 34, 932, 632));
        Frame(rectangle, new RectInt(20, 20, 960, 660), theme);
        Label(rectangle, "MULTIPLAYER", new RectInt(49, 44, 330, 30), 24, accent);
        Label(
            rectangle,
            page == MultiplayerProposalPage.Lobby ? "GAME LOBBY" : "SERVER BROWSER",
            new RectInt(50, 77, 430, 20),
            12,
            _muted
        );
        Label(
            rectangle,
            "DavidAdas  |  Online",
            new RectInt(656, 49, 232, 24),
            14,
            _text,
            TextAlignmentOptions.MidlineRight
        );
        Action(rectangle, "X", new RectInt(899, 45, 30, 28));
        if (page == MultiplayerProposalPage.Lobby)
            BuildLobby(rectangle, layout, accent);
        else
        {
            BuildBrowser(rectangle, layout, accent);
            if (page == MultiplayerProposalPage.CreateGame)
                BuildCreate(rectangle, theme, accent);
        }
        foreach (TMP_Text label in root.GetComponentsInChildren<TMP_Text>(true))
            label.ForceMeshUpdate(true);
        Canvas.ForceUpdateCanvases();
        return root;
    }

    /// <summary>
    /// Saves one inspectable native prefab using persistent source-asset references.
    /// </summary>
    /// <param name="root">The generated canvas hierarchy.</param>
    /// <param name="layout">The layout arrangement.</param>
    /// <param name="page">The current screen.</param>
    /// <param name="theme">The artwork family.</param>
    /// <returns>The generated prefab asset path.</returns>
    public static string SavePrefab(
        GameObject root,
        MultiplayerProposalLayout layout,
        MultiplayerProposalPage page,
        MultiplayerProposalTheme theme
    )
    {
        Directory.CreateDirectory(AssetDirectory);
        string path = $"{AssetDirectory}/{layout}-{page}-{theme}.prefab";
        PrefabUtility.SaveAsPrefabAsset(root, path);
        return path;
    }

    /// <summary>
    /// Builds the discoverable-game table and public-chat area without menu navigation.
    /// </summary>
    /// <param name="parent">The proposal canvas.</param>
    /// <param name="layout">The selected arrangement.</param>
    /// <param name="accent">The configured theme colour.</param>
    private static void BuildBrowser(
        Transform parent,
        MultiplayerProposalLayout layout,
        Color accent
    )
    {
        Input(parent, "Search", "Search games...", string.Empty, new RectInt(50, 111, 340, 28));
        Action(parent, "ALL CONTENT PACKS", new RectInt(404, 111, 246, 28));
        Label(
            parent,
            "Compatible games",
            new RectInt(676, 112, 208, 28),
            13,
            _muted,
            TextAlignmentOptions.MidlineRight
        );
        bool split = layout == MultiplayerProposalLayout.SplitBriefing;
        int tableWidth = split ? 594 : 868;
        ScrollAreaView table = CreateScrollAreaView(
            parent,
            "ServerBrowser",
            50,
            154,
            tableWidth,
            228,
            0,
            0,
            tableWidth - 17,
            228,
            tableWidth - 14,
            0,
            13,
            228,
            out RectTransform rows
        );
        Panel(table.transform, "TableSurface", new RectInt(0, 0, tableWidth - 16, 228))
            .transform.SetAsFirstSibling();
        int contentX = split ? 236 : 360;
        int playersX = split ? 445 : 656;
        Label(rows, "GAME", new RectInt(12, 9, 220, 21), 13, accent);
        Label(rows, "CONTENT PACK", new RectInt(contentX, 9, 250, 21), 13, accent);
        Label(rows, "PLAYERS", new RectInt(playersX, 9, 95, 21), 13, accent);
        if (!split)
            Label(rows, "PING", new RectInt(785, 9, 65, 21), 13, accent);
        string[] names = { "A galaxy divided", "The long campaign", "Evening campaign" };
        string[] content = { "25th Anniversary", "Classic Galactic Civil War", "25th Anniversary" };
        for (int index = 0; index < names.Length; index++)
        {
            int top = 39 + index * 46;
            if (index == 0)
                SelectedRow(rows, new RectInt(5, top, tableWidth - 26, 40));
            Label(rows, names[index], new RectInt(12, top + 3, contentX - 24, 23), 15, _text);
            Label(
                rows,
                index == 1 ? "Password protected" : "Open game",
                new RectInt(12, top + 24, 220, 16),
                10,
                _muted
            );
            Label(
                rows,
                content[index],
                new RectInt(contentX, top + 8, playersX - contentX - 12, 23),
                13,
                _text
            );
            Label(rows, "1 / 2", new RectInt(playersX, top + 8, 75, 23), 14, _text);
            if (!split)
                Label(
                    rows,
                    new[] { "42 ms", "68 ms", "31 ms" }[index],
                    new RectInt(785, top + 8, 65, 23),
                    13,
                    _text
                );
        }
        if (split)
        {
            Panel(parent, "SelectedGameDetails", new RectInt(660, 154, 258, 228));
            Label(parent, "A GALAXY DIVIDED", new RectInt(676, 166, 226, 26), 17, accent);
            PreviewImage(parent, new RectInt(676, 202, 226, 94));
            Label(
                parent,
                "25th Anniversary  |  1.0.2\nNew campaign\nYour content matches",
                new RectInt(676, 307, 230, 62),
                13,
                _text
            );
        }
        Action(parent, "REFRESH", new RectInt(50, 394, 146, 28));
        Label(parent, "3 games available", new RectInt(212, 396, 280, 24), 13, _muted);
        Action(parent, "HOST GAME", new RectInt(604, 394, 146, 28));
        Action(parent, "JOIN GAME", new RectInt(766, 394, 152, 28));
        BuildChat(parent, new RectInt(50, 440, 868, 200), "GENERAL CHAT", accent);
    }

    /// <summary>
    /// Builds the name, password, and content-pack selection dialog over the browser.
    /// </summary>
    /// <param name="parent">The proposal canvas.</param>
    /// <param name="theme">The existing artwork family.</param>
    /// <param name="accent">The configured theme colour.</param>
    private static void BuildCreate(Transform parent, MultiplayerProposalTheme theme, Color accent)
    {
        Fill(parent, "ModalScrim", new RectInt(34, 34, 932, 632), new Color(0f, 0f, 0f, 0.72f));
        Fill(parent, "CreateGameBacking", new RectInt(188, 153, 624, 390), Color.black);
        Panel(parent, "CreateGameSurface", new RectInt(188, 153, 624, 390));
        Frame(parent, new RectInt(176, 141, 648, 414), theme);
        Label(parent, "HOST A GAME", new RectInt(205, 169, 560, 31), 23, accent);
        Label(parent, "GAME NAME", new RectInt(206, 215, 255, 22), 13, accent);
        Input(parent, "GameName", "Game name", "David's campaign", new RectInt(206, 243, 272, 29));
        Label(parent, "PASSWORD (OPTIONAL)", new RectInt(500, 215, 280, 22), 13, accent);
        Input(
            parent,
            "Password",
            "Leave blank for an open game",
            string.Empty,
            new RectInt(500, 243, 278, 29)
        );
        Label(parent, "CONTENT PACK", new RectInt(206, 294, 280, 22), 13, accent);
        Action(parent, "25TH ANNIVERSARY", new RectInt(206, 327, 272, 38));
        Label(parent, "Version 1.0.2  |  Installed", new RectInt(215, 371, 260, 19), 12, _muted);
        Action(parent, "CLASSIC GALACTIC CIVIL WAR", new RectInt(206, 404, 272, 38));
        PreviewImage(parent, new RectInt(500, 325, 278, 120));
        Label(parent, "Two players. One faction each.", new RectInt(500, 452, 290, 22), 12, _muted);
        Action(parent, "CANCEL", new RectInt(206, 488, 136, 30));
        Action(parent, "CREATE LOBBY", new RectInt(611, 488, 167, 30));
    }

    /// <summary>
    /// Builds the shared campaign, two human seats, lobby chat, and explicit ready controls.
    /// </summary>
    /// <param name="parent">The proposal canvas.</param>
    /// <param name="layout">The selected arrangement.</param>
    /// <param name="accent">The configured theme colour.</param>
    private static void BuildLobby(Transform parent, MultiplayerProposalLayout layout, Color accent)
    {
        Label(parent, "David's campaign", new RectInt(50, 108, 560, 29), 22, _text);
        Label(
            parent,
            "2 / 2 players  |  New campaign",
            new RectInt(600, 111, 318, 25),
            13,
            _muted,
            TextAlignmentOptions.MidlineRight
        );
        if (layout == MultiplayerProposalLayout.PlayerStations)
        {
            Panel(parent, "CampaignStrip", new RectInt(50, 149, 868, 64));
            Label(parent, "25TH ANNIVERSARY", new RectInt(66, 160, 300, 23), 16, accent);
            Label(
                parent,
                "Version 1.0.2  |  Content matches",
                new RectInt(66, 185, 400, 18),
                12,
                _muted
            );
            Action(parent, "SETTINGS", new RectInt(605, 169, 130, 28));
            Action(parent, "LOAD GAME", new RectInt(752, 169, 150, 28));
            PlayerStation(parent, new RectInt(50, 231, 424, 143), "DavidAdas", true, accent);
            PlayerStation(parent, new RectInt(494, 231, 424, 143), "Nova", false, accent);
        }
        else
        {
            int campaignWidth = layout == MultiplayerProposalLayout.SplitBriefing ? 330 : 276;
            int rosterX = 50 + campaignWidth + 19;
            int rosterWidth = 918 - rosterX;
            Panel(parent, "CampaignBriefing", new RectInt(50, 149, campaignWidth, 225));
            PreviewImage(parent, new RectInt(64, 162, campaignWidth - 28, 89));
            Label(
                parent,
                "25TH ANNIVERSARY",
                new RectInt(64, 262, campaignWidth - 28, 24),
                16,
                accent
            );
            Label(
                parent,
                "Version 1.0.2  |  New campaign",
                new RectInt(64, 291, campaignWidth - 28, 19),
                12,
                _muted
            );
            Action(parent, "SETTINGS", new RectInt(64, 327, (campaignWidth - 40) / 2, 28));
            Action(
                parent,
                "LOAD GAME",
                new RectInt(76 + (campaignWidth - 40) / 2, 327, (campaignWidth - 40) / 2, 28)
            );
            Label(parent, "PLAYER", new RectInt(rosterX + 12, 151, 180, 24), 13, accent);
            Label(parent, "FACTION", new RectInt(rosterX + 215, 151, 180, 24), 13, accent);
            PlayerRow(
                parent,
                new RectInt(rosterX, 184, rosterWidth, 75),
                "DavidAdas",
                true,
                accent
            );
            PlayerRow(parent, new RectInt(rosterX, 269, rosterWidth, 75), "Nova", false, accent);
        }
        Label(
            parent,
            "Choose different factions. Both players must ready up before launch.",
            new RectInt(50, 385, 868, 23),
            13,
            _muted
        );
        BuildChat(parent, new RectInt(50, 422, 868, 181), "LOBBY CHAT", accent);
        Action(parent, "LEAVE LOBBY", new RectInt(50, 619, 160, 30));
        Label(
            parent,
            "1 / 2 players ready",
            new RectInt(402, 623, 220, 25),
            13,
            _muted,
            TextAlignmentOptions.MidlineRight
        );
        Action(parent, "I AM READY", new RectInt(640, 619, 130, 30));
        Button launch = Action(parent, "START GAME", new RectInt(787, 619, 131, 30));
        launch.interactable = false;
        launch.GetComponentInChildren<TMP_Text>(true).color = _muted;
    }

    /// <summary>
    /// Creates one compact player row using the shared native controls.
    /// </summary>
    /// <param name="parent">The owning screen.</param>
    /// <param name="bounds">The player row bounds.</param>
    /// <param name="name">The sample player name.</param>
    /// <param name="local">Whether this row represents the local host.</param>
    /// <param name="accent">The configured theme colour.</param>
    private static void PlayerRow(
        Transform parent,
        RectInt bounds,
        string name,
        bool local,
        Color accent
    )
    {
        RectTransform row = Panel(parent, "Player " + name, bounds).rectTransform;
        Label(row, name, new RectInt(12, 10, 195, 24), 18, _text);
        Label(row, local ? "Host  |  You" : "Connected", new RectInt(12, 41, 195, 20), 12, _muted);
        if (local)
            FactionDropdown(row, new RectInt(215, 7, bounds.width - 230, 30));
        else
            Label(row, "Alliance", new RectInt(225, 10, bounds.width - 245, 24), 15, _text);
        Label(
            row,
            local ? "Not ready" : "READY",
            new RectInt(225, 44, bounds.width - 245, 20),
            12,
            local ? _muted : accent
        );
    }

    /// <summary>
    /// Creates an equally sized player station for the two-seat arrangement.
    /// </summary>
    /// <param name="parent">The owning screen.</param>
    /// <param name="bounds">The station bounds.</param>
    /// <param name="name">The sample player name.</param>
    /// <param name="local">Whether this is the local host.</param>
    /// <param name="accent">The configured theme colour.</param>
    private static void PlayerStation(
        Transform parent,
        RectInt bounds,
        string name,
        bool local,
        Color accent
    )
    {
        RectTransform station = Panel(parent, "Station " + name, bounds).rectTransform;
        Label(station, name, new RectInt(17, 16, 370, 27), 21, _text);
        Label(
            station,
            local ? "HOST  |  YOU" : "CONNECTED PLAYER",
            new RectInt(18, 48, 370, 19),
            12,
            _muted
        );
        if (local)
            FactionDropdown(station, new RectInt(18, 85, 240, 30));
        else
            Label(station, "Alliance", new RectInt(18, 86, 240, 27), 16, _text);
        Label(
            station,
            local ? "Not ready" : "READY",
            new RectInt(270, 87, 138, 24),
            13,
            local ? _muted : accent
        );
    }

    /// <summary>
    /// Creates a native scrollable chat log and the existing text-input and action controls.
    /// </summary>
    /// <param name="parent">The owning screen.</param>
    /// <param name="bounds">The chat panel bounds.</param>
    /// <param name="title">The chat channel title.</param>
    /// <param name="accent">The configured theme colour.</param>
    private static void BuildChat(Transform parent, RectInt bounds, string title, Color accent)
    {
        RectTransform panel = Panel(parent, title, bounds).rectTransform;
        Label(panel, title, new RectInt(13, 9, bounds.width - 30, 25), 16, accent);
        CreateScrollAreaView(
            panel,
            "ChatMessages",
            13,
            43,
            bounds.width - 26,
            bounds.height - 88,
            0,
            0,
            bounds.width - 45,
            bounds.height - 88,
            bounds.width - 42,
            0,
            13,
            bounds.height - 88,
            out RectTransform messages
        );
        Label(
            messages,
            title == "GENERAL CHAT"
                ? "DavidAdas: Anyone up for a campaign?\nNova: Hosting 25th Anniversary.\nNova: One open seat."
                : "DavidAdas: Ready when you are.\nNova: I'll take the Alliance.\nNova is ready.",
            new RectInt(0, 1, bounds.width - 60, 90),
            15,
            _text
        );
        Input(
            panel,
            "ChatInput",
            "Type a message...",
            string.Empty,
            new RectInt(13, bounds.height - 36, bounds.width - 127, 26)
        );
        Action(panel, "SEND", new RectInt(bounds.width - 101, bounds.height - 36, 88, 26));
    }

    /// <summary>
    /// Instantiates the existing shared action-button prefab with proposal geometry.
    /// </summary>
    /// <param name="parent">The button's owner.</param>
    /// <param name="caption">The displayed caption.</param>
    /// <param name="bounds">The source-space button bounds.</param>
    /// <returns>The existing native button component.</returns>
    private static Button Action(Transform parent, string caption, RectInt bounds)
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(_actionPrefab);
        GameObject instance = Object.Instantiate(prefab, parent, false);
        instance.name = caption;
        SetSourceRect(
            (RectTransform)instance.transform,
            bounds.x,
            bounds.y,
            bounds.width,
            bounds.height
        );
        TMP_Text text = instance.GetComponentInChildren<TMP_Text>(true);
        text.text = caption;
        text.fontSize = 14;
        text.color = _text;
        text.alignment = TextAlignmentOptions.Midline;
        return instance.GetComponent<Button>();
    }

    /// <summary>
    /// Instantiates the existing menu content-panel component without its fixed navigation layout.
    /// </summary>
    /// <param name="parent">The panel's owner.</param>
    /// <param name="name">The hierarchy name.</param>
    /// <param name="bounds">The panel bounds.</param>
    /// <returns>The existing native panel image.</returns>
    private static Image Panel(Transform parent, string name, RectInt bounds)
    {
        GameObject window = AssetDatabase.LoadAssetAtPath<GameObject>(_windowPrefab);
        Transform source = window.transform.Find("ContentPanel");
        if (source == null)
            throw new MissingReferenceException("The shared MenuWindow ContentPanel is missing.");
        GameObject instance = Object.Instantiate(source.gameObject, parent, false);
        instance.name = name;
        SetSourceRect(
            (RectTransform)instance.transform,
            bounds.x,
            bounds.y,
            bounds.width,
            bounds.height
        );
        return instance.GetComponent<Image>();
    }

    /// <summary>
    /// Reuses the selected-state artwork already used by menu rows.
    /// </summary>
    /// <param name="parent">The row owner.</param>
    /// <param name="bounds">The selected-row bounds.</param>
    private static void SelectedRow(Transform parent, RectInt bounds)
    {
        CreateSlicedImage(
            "SelectedRow",
            parent,
            _menuRowActiveAddress,
            _panelBorder,
            bounds.x,
            bounds.y,
            bounds.width,
            bounds.height,
            Color.white
        );
    }

    /// <summary>
    /// Creates text through the project's existing font, shadow, and TMP component factory.
    /// </summary>
    /// <param name="parent">The text owner.</param>
    /// <param name="value">The displayed text.</param>
    /// <param name="bounds">The label bounds.</param>
    /// <param name="size">The font size in source units.</param>
    /// <param name="color">The existing theme or shared text colour.</param>
    /// <param name="alignment">The native TMP alignment.</param>
    /// <returns>The configured native text component.</returns>
    private static TMP_Text Label(
        Transform parent,
        string value,
        RectInt bounds,
        int size,
        Color color,
        TextAlignmentOptions alignment = TextAlignmentOptions.TopLeft
    )
    {
        TextMeshProUGUI label = CreateTextLabel("Label", parent);
        label.text = value;
        label.fontSize = size;
        label.color = color;
        label.alignment = alignment;
        label.textWrappingMode = TextWrappingModes.Normal;
        label.overflowMode = TextOverflowModes.Ellipsis;
        SetSourceRect(label.rectTransform, bounds.x, bounds.y, bounds.width, bounds.height);
        return label;
    }

    /// <summary>
    /// Places the project's existing reusable text-input prefab on the proposal.
    /// </summary>
    /// <param name="parent">The field owner.</param>
    /// <param name="name">The hierarchy name.</param>
    /// <param name="placeholder">The empty-field hint.</param>
    /// <param name="value">The demonstration value.</param>
    /// <param name="bounds">The field bounds.</param>
    private static void Input(
        Transform parent,
        string name,
        string placeholder,
        string value,
        RectInt bounds
    )
    {
        Panel(parent, name + " Frame", bounds);
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
        if (input.placeholder is TMP_Text hint)
        {
            hint.fontSize = 13;
            hint.color = _muted;
        }
        input.text = value;
    }

    /// <summary>
    /// Creates a native dropdown styled with the existing content panel and text components.
    /// </summary>
    /// <param name="parent">The field owner.</param>
    /// <param name="bounds">The dropdown bounds.</param>
    private static void FactionDropdown(Transform parent, RectInt bounds)
    {
        Image surface = Panel(parent, "FactionDropdown", bounds);
        TMP_Dropdown dropdown = surface.gameObject.AddComponent<TMP_Dropdown>();
        dropdown.targetGraphic = surface;
        TMP_Text caption = Label(
            surface.transform,
            "Empire",
            new RectInt(9, 4, bounds.width - 33, bounds.height - 6),
            15,
            _text
        );
        CreateRawImage(
            "DropdownArrow",
            surface.transform,
            "Application/Strategy/UI/Controls/ui_strategyview_scrollbar_arrow_pressed_2",
            bounds.width - 23,
            9,
            12,
            9
        );
        dropdown.captionText = caption;
        Image template = Panel(
            surface.transform,
            "Template",
            new RectInt(0, bounds.height, bounds.width, 64)
        );
        Image itemSurface = Panel(template.transform, "Item", new RectInt(0, 0, bounds.width, 30));
        GameObject item = itemSurface.gameObject;
        Toggle itemToggle = item.AddComponent<Toggle>();
        itemToggle.targetGraphic = itemSurface;
        TMP_Text itemText = Label(
            item.transform,
            "Empire",
            new RectInt(9, 4, bounds.width - 18, 25),
            15,
            _text
        );
        dropdown.template = template.rectTransform;
        dropdown.itemText = itemText;
        dropdown.options.Add(new TMP_Dropdown.OptionData("Empire"));
        dropdown.options.Add(new TMP_Dropdown.OptionData("Alliance"));
        dropdown.RefreshShownValue();
        template.gameObject.SetActive(false);
    }

    /// <summary>
    /// Places an existing content illustration inside a native framed panel.
    /// </summary>
    /// <param name="parent">The preview owner.</param>
    /// <param name="bounds">The illustration bounds.</param>
    private static void PreviewImage(Transform parent, RectInt bounds)
    {
        Panel(parent, "CampaignPreviewFrame", bounds);
        RawImage image = new GameObject(
            "CampaignPreview",
            typeof(RectTransform),
            typeof(RawImage)
        ).GetComponent<RawImage>();
        image.transform.SetParent(parent, false);
        image.texture = AssetDatabase.LoadAssetAtPath<Texture2D>(
            _packRoot + "/Shared/Strategy/Advisor/Images/advisor-report-galaxy.png"
        );
        SetSourceRect(
            image.rectTransform,
            bounds.x + 6,
            bounds.y + 6,
            bounds.width - 12,
            bounds.height - 12
        );
        image.raycastTarget = false;
    }

    /// <summary>
    /// Draws an isolated background or scrim using a native Unity image.
    /// </summary>
    /// <param name="parent">The image owner.</param>
    /// <param name="name">The hierarchy name.</param>
    /// <param name="bounds">The image bounds.</param>
    /// <param name="color">The fill colour.</param>
    private static void Fill(Transform parent, string name, RectInt bounds, Color color)
    {
        Image image = new GameObject(
            name,
            typeof(RectTransform),
            typeof(Image)
        ).GetComponent<Image>();
        image.transform.SetParent(parent, false);
        image.color = color;
        image.raycastTarget = false;
        SetSourceRect(image.rectTransform, bounds.x, bounds.y, bounds.width, bounds.height);
    }

    /// <summary>
    /// Reuses only the outer artwork of an existing window, preserving its texture and bevels.
    /// </summary>
    /// <param name="parent">The frame owner.</param>
    /// <param name="bounds">The new window bounds.</param>
    /// <param name="theme">The source artwork family.</param>
    private static void Frame(Transform parent, RectInt bounds, MultiplayerProposalTheme theme)
    {
        Image image = new GameObject(
            "ThemedOuterFrame",
            typeof(RectTransform),
            typeof(Image)
        ).GetComponent<Image>();
        image.transform.SetParent(parent, false);
        image.sprite = GetFrame(theme);
        image.type = Image.Type.Sliced;
        image.fillCenter = false;
        image.pixelsPerUnitMultiplier = theme == MultiplayerProposalTheme.Application ? 3f : 4.5f;
        image.raycastTarget = false;
        SetSourceRect(image.rectTransform, bounds.x, bounds.y, bounds.width, bounds.height);
    }

    /// <summary>
    /// Loads or creates a persistent nine-slice sprite referencing the existing frame texture.
    /// </summary>
    /// <param name="theme">The source artwork family.</param>
    /// <returns>The persistent native sprite asset.</returns>
    private static Sprite GetFrame(MultiplayerProposalTheme theme)
    {
        Directory.CreateDirectory(AssetDirectory);
        string target = $"{AssetDirectory}/{theme}Frame.asset";
        Sprite existing = AssetDatabase.LoadAssetAtPath<Sprite>(target);
        if (existing != null)
            return existing;
        string path =
            theme == MultiplayerProposalTheme.Application
                ? "Assets/Content/Application/OptionsMenu/UI/ui_settingsmenu_background.png"
                : $"{_packRoot}/Factions/{theme}/Strategy/UI/Windows/Encyclopedia/encyclopedia-window-overlay-frame.png";
        Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        if (texture == null)
            throw new MissingReferenceException(path);
        Vector4 border =
            theme == MultiplayerProposalTheme.Application
                ? new Vector4(58, 58, 58, 58)
                : new Vector4(48, 48, 210, 48);
        Sprite sprite = Sprite.Create(
            texture,
            new Rect(0, 0, texture.width, texture.height),
            new Vector2(0.5f, 0.5f),
            100f,
            0,
            SpriteMeshType.FullRect,
            border
        );
        sprite.name = theme + " multiplayer frame";
        AssetDatabase.CreateAsset(sprite, target);
        return sprite;
    }

    /// <summary>
    /// Reads faction colours from their actual theme definitions instead of introducing palettes.
    /// </summary>
    /// <param name="theme">The existing artwork family.</param>
    /// <returns>The configured faction or shared application accent.</returns>
    private static Color GetAccent(MultiplayerProposalTheme theme)
    {
        if (theme == MultiplayerProposalTheme.Application)
            return new Color(0.373f, 0.659f, 0.925f);
        XDocument document = XDocument.Load($"{_packRoot}/Factions/{theme}/Strategy/UI/theme.xml");
        string hex = document
            .Root?.Element("FactionTheme")
            ?.Element("FactionPrimaryColorHex")
            ?.Value;
        if (!ColorUtility.TryParseHtmlString("#" + hex, out Color color))
            throw new InvalidDataException("The faction theme primary colour is missing.");
        return color;
    }
}
