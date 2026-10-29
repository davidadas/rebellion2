using System;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using Rebellion.Multiplayer;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

/// <summary>
/// Reviews the actual multiplayer prefab with populated design data and the shared menu components.
/// </summary>
public sealed class MultiplayerInterfacePreviewWindow : EditorWindow
{
    private enum Page
    {
        Browser,
        EmptyBrowser,
        Create,
        Lobby,
        Saves,
        Password,
        Leave,
    }

    private static readonly string[] _pageNames =
    {
        "Server browser",
        "Empty browser",
        "Host game",
        "Lobby",
        "Load game",
        "Game password",
        "Leave lobby",
    };

    [SerializeField]
    private Page _page;
    private Texture2D _image;

    /// <summary>
    /// Opens the populated native preview of the production multiplayer window.
    /// </summary>
    [MenuItem("Rebellion/Preview/Multiplayer interface")]
    public static void Open()
    {
        MultiplayerInterfacePreviewWindow window = GetWindow<MultiplayerInterfacePreviewWindow>();
        if (window._image != null)
            DestroyImmediate(window._image);
        window._image = null;
        window.titleContent = new GUIContent("Multiplayer interface");
        window.minSize = new Vector2(1000, 780);
        window.position = new Rect(80, 80, 1440, 1040);
        window.Show();
        window.Focus();
        window.Repaint();
    }

    /// <summary>
    /// Draws the page selector and camera-rendered native controls.
    /// </summary>
    [SuppressMessage("Roslynator", "RCS1213", Justification = "Invoked by Unity.")]
    private void OnGUI()
    {
        EditorGUILayout.LabelField("Multiplayer interface", EditorStyles.boldLabel);
        EditorGUILayout.LabelField(
            "Production prefab and shared menu controls, populated with preview entries.",
            EditorStyles.miniLabel
        );
        EditorGUI.BeginChangeCheck();
        _page = (Page)GUILayout.Toolbar((int)_page, _pageNames, GUILayout.Height(28));
        if ((EditorGUI.EndChangeCheck() || _image == null) && !EditorApplication.isCompiling)
        {
            if (_image != null)
                DestroyImmediate(_image);
            _image = Render(_page);
        }
        Rect available = GUILayoutUtility.GetRect(
            100,
            10000,
            100,
            10000,
            GUILayout.ExpandHeight(true)
        );
        if (_image != null)
            GUI.DrawTexture(available, _image, ScaleMode.ScaleToFit);
        if (GUILayout.Button("Export populated views", GUILayout.Height(28)))
            ExportAll();
    }

    /// <summary>
    /// Releases the camera render owned by this preview window.
    /// </summary>
    [SuppressMessage("Roslynator", "RCS1213", Justification = "Invoked by Unity.")]
    private void OnDisable()
    {
        if (_image != null)
            DestroyImmediate(_image);
    }

    /// <summary>
    /// Exports each populated production screen using native Unity rendering.
    /// </summary>
    public static void ExportAll()
    {
        const string directory = "Design/NativeMultiplayerInterface";
        Directory.CreateDirectory(directory);
        foreach (Page page in Enum.GetValues(typeof(Page)))
        {
            Texture2D image = Render(page);
            File.WriteAllBytes(Path.Combine(directory, page + ".png"), image.EncodeToPNG());
            DestroyImmediate(image);
        }
    }

    /// <summary>
    /// Builds an isolated preview canvas from the same prefab used by the running game.
    /// </summary>
    /// <param name="page">The screen to review.</param>
    /// <returns>The caller-owned native canvas.</returns>
    private static GameObject Build(Page page)
    {
        GameObject root = new GameObject(
            "Multiplayer interface preview",
            typeof(RectTransform),
            typeof(Canvas),
            typeof(CanvasScaler),
            typeof(GraphicRaycaster)
        );
        Canvas canvas = root.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        RectTransform rectangle = (RectTransform)root.transform;
        rectangle.pivot = new Vector2(0, 1);
        rectangle.sizeDelta = new Vector2(1000, 700);
        GameObject window = Instantiate(
            AssetDatabase.LoadAssetAtPath<GameObject>(MultiplayerMenuPrefabBuilder.PrefabPath),
            root.transform,
            false
        );
        window.transform.localScale = Vector3.one / 0.64f;
        window.SetActive(true);
        ContentBindings.Apply(window, ContentPackEditor.Assets);
        MultiplayerMenuOverlay controller = window.GetComponent<MultiplayerMenuOverlay>();
        controller.enabled = false;
        SerializedObject authored = new SerializedObject(controller);
        bool lobby = page == Page.Lobby || page == Page.Saves || page == Page.Leave;
        Control<GameObject>(authored, "browserPage").SetActive(!lobby);
        Control<GameObject>(authored, "lobbyPage").SetActive(lobby);
        Control<GameObject>(authored, "createDialog").SetActive(page == Page.Create);
        Control<GameObject>(authored, "saveDialog").SetActive(page == Page.Saves);
        Control<GameObject>(authored, "passwordDialog").SetActive(page == Page.Password);
        Control<GameObject>(authored, "authDialog").SetActive(false);
        Control<GameObject>(authored, "leaveConfirmationRoot").SetActive(page == Page.Leave);
        Control<TMP_Text>(authored, "pageTitle").text = lobby
            ? "Evening campaign"
            : "SERVER BROWSER";
        Control<TMP_Text>(authored, "identityText").text = "DavidAdas  |  Online";
        Control<TMP_Text>(authored, "statusText").text = string.Empty;
        string[] names =
        {
            "Evening campaign",
            "The long campaign",
            "Galactic conquest",
            "Weekend campaign",
            "Turn 1,000 save",
            "A longer room name for checking table truncation",
            "Open campaign",
            "Another password-protected game",
        };
        string[] packs = { "Classic Galactic Civil War", "25th Anniversary (by Metasharp)" };
        GameObject template = Control<GameObject>(authored, "gameRowTemplate");
        RectTransform rows = Control<RectTransform>(authored, "gameRowsRoot");
        for (int index = 0; index < (page == Page.EmptyBrowser ? 0 : names.Length); index++)
        {
            GameObject row = Instantiate(template, rows, false);
            row.SetActive(true);
            ((RectTransform)row.transform).anchoredPosition = new Vector2(0, -index * 38);
            row.GetComponent<MenuServerRowView>()
                .Render(
                    names[index],
                    packs[index % 2],
                    index == 2 ? "2/2" : "1/2",
                    index % 2 == 1,
                    index == 1
                );
        }
        Control<ScrollAreaView>(authored, "gameScrollArea")
            .SetContentHeight(page == Page.EmptyBrowser ? 0 : names.Length * 38, 38, true);
        Control<GameObject>(authored, "emptyBrowserText").SetActive(page == Page.EmptyBrowser);
        Control<TMP_Text>(authored, "browserCountText").text =
            page == Page.EmptyBrowser ? string.Empty : names.Length + " games";
        SetOptions(
            Control<TMP_Dropdown>(authored, "packFilter"),
            new[] { "All content packs", packs[0], packs[1] }
        );
        SetOptions(Control<TMP_Dropdown>(authored, "contentPackDropdown"), packs);
        Control<TMP_InputField>(authored, "gameNameInput").text = "Evening campaign";
        Control<TMP_InputField>(authored, "hostPasswordInput").text = string.Empty;
        Control<TMP_InputField>(authored, "joinPasswordInput").text = string.Empty;
        Control<TMP_Text>(authored, "hostNameText").text = "DavidAdas  (Host)";
        Control<TMP_Text>(authored, "guestNameText").text = "Nova";
        Control<TMP_Text>(authored, "hostReadyText").text = "Not ready";
        Control<TMP_Text>(authored, "guestReadyText").text = "Ready";
        Control<TMP_Text>(authored, "campaignText").text =
            "Classic Galactic Civil War\nStandard Campaign\nNew game";
        TMP_Dropdown hostFaction = Control<TMP_Dropdown>(authored, "hostFactionDropdown");
        SetOptions(hostFaction, new[] { "Empire", "Alliance" });
        hostFaction.SetValueWithoutNotify(1);
        TMP_Dropdown guestFaction = Control<TMP_Dropdown>(authored, "guestFactionDropdown");
        SetOptions(guestFaction, new[] { "Empire", "Alliance" });
        guestFaction.SetValueWithoutNotify(0);
        guestFaction.interactable = false;
        Control<Button>(authored, "closeButton").gameObject.SetActive(!lobby);
        Control<TMP_Text>(authored, "generalChatText").text =
            "Nova: Anyone up for a campaign?\nDavidAdas: Hosting a new game.\nNova: Joining now.";
        Control<TMP_Text>(authored, "lobbyChatText").text =
            "Nova joined the lobby.\nDavidAdas: Ready when you are.\nNova: Choosing my faction now.\nOrion left the lobby.";
        Control<Button>(authored, "launchButton").interactable = false;
        if (page == Page.Saves)
        {
            OptionsSaveListView list = Control<OptionsSaveListView>(authored, "saveList");
            Texture2D allianceIcon = ContentPackEditor.Assets.GetTexture(
                "Pack/Factions/Alliance/Strategy/UI/Options/ui_settingsmenu_savedgames_faction_icon"
            );
            Texture2D empireIcon = ContentPackEditor.Assets.GetTexture(
                "Pack/Factions/Empire/Strategy/UI/Options/ui_settingsmenu_savedgames_faction_icon"
            );
            list.SetLoadOnly(true);
            list.Render(
                new[]
                {
                    new OptionsSaveSlot(
                        "Evening campaign",
                        "26/10/2026 15:42",
                        allianceIcon,
                        false,
                        "preview-evening",
                        "Easy",
                        40
                    ),
                    new OptionsSaveSlot(
                        "The long campaign",
                        "25/10/2026 20:17",
                        empireIcon,
                        false,
                        "preview-long",
                        "Medium",
                        1000
                    ),
                    new OptionsSaveSlot(
                        "Outer frontier",
                        "24/10/2026 18:04",
                        empireIcon,
                        false,
                        "preview-frontier",
                        "Hard",
                        120
                    ),
                    new OptionsSaveSlot(
                        "Weekend campaign",
                        "23/10/2026 21:35",
                        allianceIcon,
                        false,
                        "preview-weekend",
                        "Custom",
                        60
                    ),
                    new OptionsSaveSlot(
                        "An extended saved-game name to check the shared row layout",
                        "22/10/2026 09:16",
                        allianceIcon,
                        false,
                        "preview-long-name",
                        "Easy",
                        200
                    ),
                    new OptionsSaveSlot(
                        "Autosave",
                        "21/10/2026 19:52",
                        empireIcon,
                        false,
                        "preview-auto",
                        "Medium",
                        20
                    ),
                },
                1,
                false,
                true
            );
        }
        if (page == Page.Leave)
            Control<ConfirmationDialogView>(authored, "leaveConfirmation")
                .Show("Are you sure you want to leave this lobby?");
        return root;
    }

    /// <summary>
    /// Resolves an authored prefab reference through Unity's serialization contract.
    /// </summary>
    /// <typeparam name="T">The authored object type.</typeparam>
    /// <param name="authored">The production view's serialized references.</param>
    /// <param name="name">The serialized reference name.</param>
    /// <returns>The authored object.</returns>
    private static T Control<T>(SerializedObject authored, string name)
        where T : Object
    {
        Object value = authored.FindProperty(name).objectReferenceValue;
        if (value is GameObject gameObject && typeof(T) == typeof(GameObject))
            return (T)(Object)gameObject;
        if (typeof(T) == typeof(GameObject) && value is Component component)
            return (T)(Object)component.gameObject;
        return (T)value;
    }

    /// <summary>
    /// Populates the shared selector using the same native TMP contract as the live presenter.
    /// </summary>
    /// <param name="dropdown">The shared selector.</param>
    /// <param name="values">The preview choices.</param>
    private static void SetOptions(TMP_Dropdown dropdown, string[] values)
    {
        dropdown.ClearOptions();
        dropdown.AddOptions(new System.Collections.Generic.List<string>(values));
        dropdown.RefreshShownValue();
    }

    /// <summary>
    /// Renders the populated production canvas through the project's native HDRP camera setup.
    /// </summary>
    /// <param name="page">The screen to render.</param>
    /// <returns>The caller-owned screenshot.</returns>
    private static Texture2D Render(Page page)
    {
        PreviewRenderUtility preview = new PreviewRenderUtility();
        try
        {
            Camera camera = preview.camera;
            camera.cameraType = CameraType.Game;
            camera.orthographic = true;
            camera.orthographicSize = 350;
            camera.nearClipPlane = 0.1f;
            camera.farClipPlane = 2000;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Color.black;
            camera.transform.position = new Vector3(500, -350, -1000);
            camera.transform.rotation = Quaternion.identity;
            HdrpRenderTargetBuilder.ConfigureCamera(camera);
            GameObject root = Build(page);
            preview.AddSingleGO(root);
            preview.BeginStaticPreview(new Rect(0, 0, 1800, 1260));
            camera.aspect = 1000f / 700;
            Canvas canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = camera;
            canvas.planeDistance = 100;
            CanvasScaler scaler = root.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1000, 700);
            scaler.matchWidthOrHeight = 0.5f;
            Canvas.ForceUpdateCanvases();
            preview.Render(true);
            return preview.EndStaticPreview();
        }
        finally
        {
            preview.Cleanup();
        }
    }
}
