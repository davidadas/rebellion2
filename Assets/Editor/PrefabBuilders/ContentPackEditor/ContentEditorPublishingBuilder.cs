using TMPro;
using UnityEngine;
using UnityEngine.UI;
using static ContentEditorControlBuilder;

/// <summary>
/// Authors the publishing workflow using the editor's existing controls and appearance bindings.
/// </summary>
internal static class ContentEditorPublishingBuilder
{
    /// <summary>
    /// Creates the listing, release, and export-review dialog beneath the editor workbench.
    /// </summary>
    /// <param name="parent">The owning authoring canvas.</param>
    /// <returns>The dialog whose operations are coordinated by the editor controller.</returns>
    internal static ContentEditorPublishingView Build(Transform parent)
    {
        Image overlay = Surface("Publishing", parent, new Color(0, 0, 0, 0.7f));
        Stretch(overlay.rectTransform);
        overlay.raycastTarget = true;
        ContentEditorPublishingView view =
            overlay.gameObject.AddComponent<ContentEditorPublishingView>();
        Image panel = Surface("Panel", overlay.transform, ContentEditorColorRole.Panel);
        RectTransform rect = panel.rectTransform;
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = new Vector2(1040, 730);
        Rect(
            Label("Heading", rect, "Prepare a Thunderstore release", 24).rectTransform,
            28,
            20,
            900,
            38
        );
        TextMeshProUGUI identity = Label("Identity", rect, string.Empty, 15, true);
        Rect(identity.rectTransform, 28, 60, 950, 24);
        Assign(view, "_identity", identity);
        string[] names = { "Listing", "Release", "Review" };
        Button[] steps = new Button[names.Length];
        GameObject[] stages = new GameObject[names.Length];
        for (int i = 0; i < names.Length; i++)
        {
            steps[i] = PlainButton(names[i] + "Step", rect, i + 1 + ". " + names[i]);
            Rect((RectTransform)steps[i].transform, 28 + i * 150, 96, 140, 36);
            RectTransform stage = Layer(names[i], rect);
            Rect(stage, 28, 150, 650, 440);
            stages[i] = stage.gameObject;
        }
        Assign(view, "_stepButtons", steps);
        Assign(view, "_stages", stages);
        TMP_InputField[] fields = new TMP_InputField[7];
        Transform listing = stages[0].transform;
        fields[0] = Field(listing, "PackageName", "Package name", 0, 36);
        fields[1] = Field(listing, "Description", "Short description", 78, 76);
        fields[1].characterLimit = 250;
        fields[2] = Field(listing, "Website", "Website (optional)", 196, 36);
        fields[3] = Field(listing, "Readme", "Readme (Markdown)", 274, 134);
        Transform release = stages[1].transform;
        fields[6] = Field(release, "Version", "Release version", 0, 36);
        fields[6].readOnly = true;
        TextMeshProUGUI versionHelp = Label(
            "VersionHelp",
            release,
            "Uses the version from your content pack. Edit it in the pack properties.",
            14,
            true
        );
        Rect(versionHelp.rectTransform, 0, 64, 650, 28);
        fields[4] = Field(
            release,
            "Dependencies",
            "Required Thunderstore packages (one Team-Package-1.2.0 per line)",
            106,
            90
        );
        fields[5] = Field(release, "Changelog", "Release notes (Markdown)", 238, 170);
        string[] fieldNames =
        {
            "_packageName",
            "_description",
            "_website",
            "_readme",
            "_dependencies",
            "_changelog",
            "_version",
        };
        for (int i = 0; i < fields.Length; i++)
            Assign(view, fieldNames[i], fields[i]);
        TextMeshProUGUI review = Label("ReviewText", stages[2].transform, string.Empty, 17);
        Rect(review.rectTransform, 0, 0, 650, 410);
        review.alignment = TextAlignmentOptions.TopLeft;
        review.textWrappingMode = TextWrappingModes.Normal;
        Assign(view, "_review", review);
        Button community = Button("OpenThunderstore", stages[2].transform, "Open Thunderstore");
        Rect((RectTransform)community.transform, 0, 410, 200, 36);
        Assign(view, "_communityButton", community);
        Rect(
            Label("PreviewHeading", rect, "Listing preview", 15, true).rectTransform,
            726,
            150,
            286,
            24
        );
        RawImage icon = Layer("Icon", rect).gameObject.AddComponent<RawImage>();
        Rect(icon.rectTransform, 726, 188, 128, 128);
        icon.raycastTarget = false;
        Assign(view, "_icon", icon);
        Button browse = Button("ChooseIcon", rect, "Choose icon...");
        Rect((RectTransform)browse.transform, 726, 330, 180, 36);
        Assign(view, "_browseButton", browse);
        TextMeshProUGUI iconLabel = Label("IconSize", rect, string.Empty, 14, true);
        Rect(iconLabel.rectTransform, 726, 374, 286, 28);
        Assign(view, "_iconLabel", iconLabel);
        TextMeshProUGUI preview = Label("ListingPreview", rect, string.Empty, 16);
        Rect(preview.rectTransform, 726, 418, 286, 184);
        preview.alignment = TextAlignmentOptions.TopLeft;
        preview.textWrappingMode = TextWrappingModes.Normal;
        Assign(view, "_preview", preview);
        TextMeshProUGUI status = Label("PublishingStatus", rect, string.Empty, 15);
        Rect(status.rectTransform, 28, 608, 984, 54);
        status.alignment = TextAlignmentOptions.TopLeft;
        status.textWrappingMode = TextWrappingModes.Normal;
        Assign(view, "_status", status);
        Button save = Button("SaveDraft", rect, "Save draft");
        Rect((RectTransform)save.transform, 28, 674, 144, 36);
        Assign(view, "_saveButton", save);
        Button close = Button("ClosePublishing", rect, "Close");
        Rect((RectTransform)close.transform, 648, 674, 110, 36);
        Assign(view, "_closeButton", close);
        Button next = Button("ContinuePublishing", rect, "Continue to release");
        Rect((RectTransform)next.transform, 774, 674, 238, 36);
        Assign(view, "_nextButton", next);
        overlay.gameObject.SetActive(false);
        return view;
    }

    /// <summary>
    /// Creates a labelled single-line or multiline publishing field with shared text-edit behavior.
    /// </summary>
    /// <param name="parent">The publishing stage.</param>
    /// <param name="name">The authored control name.</param>
    /// <param name="caption">The user-facing field label.</param>
    /// <param name="top">The label's vertical source coordinate.</param>
    /// <param name="height">The input's source height.</param>
    /// <returns>The configured text input.</returns>
    private static TMP_InputField Field(
        Transform parent,
        string name,
        string caption,
        int top,
        int height
    )
    {
        Rect(Label(name + "Label", parent, caption, 14).rectTransform, 0, top, 650, 24);
        TMP_InputField input = Input("Publishing" + name, parent, string.Empty);
        Rect((RectTransform)input.transform, 0, top + 30, 650, height);
        if (height > 36)
        {
            input.lineType = TMP_InputField.LineType.MultiLineNewline;
            input.textComponent.alignment = TextAlignmentOptions.TopLeft;
            input.textComponent.textWrappingMode = TextWrappingModes.Normal;
        }
        return input;
    }
}
