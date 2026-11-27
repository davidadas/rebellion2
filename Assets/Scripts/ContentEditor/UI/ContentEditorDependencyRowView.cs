using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Edits one pinned package dependency through published versions and explicit removal.
/// </summary>
public sealed class ContentEditorDependencyRowView : MonoBehaviour
{
    [SerializeField]
    private TMP_Text packageName;

    [SerializeField]
    private TMP_Text author;

    [SerializeField]
    private TMP_Dropdown version;

    [SerializeField]
    private Button remove;

    private string reference;
    private Action<string, string> changed;
    private Action<string> removed;
    private bool bound;

    /// <summary>
    /// Displays a saved pin while retaining versions missing from the current catalogue.
    /// </summary>
    /// <param name="value">The exact saved dependency reference.</param>
    /// <param name="listing">The matching catalogue listing, when available.</param>
    /// <param name="onChanged">The confirmed version change.</param>
    /// <param name="onRemoved">The requested dependency removal.</param>
    internal void Render(
        string value,
        ThunderstorePackage listing,
        Action<string, string> onChanged,
        Action<string> onRemoved
    )
    {
        if (!bound)
        {
            version.onValueChanged.AddListener(ChangeVersion);
            remove.onClick.AddListener(Remove);
            bound = true;
        }
        reference = value;
        changed = onChanged;
        removed = onRemoved;
        string id = ContentEditorDependenciesView.GetPackageID(value);
        string current = value.Length > id.Length ? value[(id.Length + 1)..] : string.Empty;
        int separator = id.IndexOf('-');
        packageName.text = (listing?.name ?? (separator < 0 ? id : id[(separator + 1)..])).Replace(
            '_',
            ' '
        );
        author.text = listing?.owner ?? (separator < 0 ? "Unavailable package" : id[..separator]);
        string[] versions = (
            listing?.versions.Select(item => item.version_number) ?? Enumerable.Empty<string>()
        )
            .Prepend(current)
            .Where(item => !string.IsNullOrEmpty(item))
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        version.options =
            versions.Length > 0
                ? versions
                    .Select(value =>
                        (TMP_Dropdown.OptionData)new DropdownOption<string>(value, value)
                    )
                    .ToList()
                : new List<TMP_Dropdown.OptionData>
                {
                    new DropdownOption<string>(null, "Unavailable"),
                };
        version.SetValueWithoutNotify(0);
        version.RefreshShownValue();
        version.interactable = listing != null && versions.Length > 1;
        gameObject.SetActive(true);
    }

    /// <summary>
    /// Fits the name and controls to the list's visible width.
    /// </summary>
    /// <param name="top">The row's vertical offset.</param>
    /// <param name="width">The available content width.</param>
    internal void SetLayout(int top, int width)
    {
        UILayout.SetSourceRect((RectTransform)transform, 0, top, width, 52);
        UILayout.SetSourceRect(packageName.rectTransform, 12, 2, width - 280, 26);
        UILayout.SetSourceRect(author.rectTransform, 12, 28, width - 280, 20);
        UILayout.SetSourceRect((RectTransform)version.transform, width - 244, 8, 132, 36);
        UILayout.SetSourceRect((RectTransform)remove.transform, width - 100, 8, 88, 36);
    }

    /// <summary>
    /// Applies only a version supplied by the current package listing.
    /// </summary>
    /// <param name="index">The chosen version index.</param>
    private void ChangeVersion(int index)
    {
        if (
            index >= 0
            && index < version.options.Count
            && version.options[index] is DropdownOption<string> { Value: not null } option
        )
            changed?.Invoke(
                reference,
                ContentEditorDependenciesView.GetPackageID(reference) + "-" + option.Value
            );
    }

    /// <summary>
    /// Removes this exact saved pin from the draft.
    /// </summary>
    private void Remove() => removed?.Invoke(reference);

    /// <summary>
    /// Releases the controls' owned event subscriptions.
    /// </summary>
    private void OnDestroy()
    {
        if (!bound)
            return;
        version.onValueChanged.RemoveListener(ChangeVersion);
        remove.onClick.RemoveListener(Remove);
    }
}
