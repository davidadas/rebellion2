using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class SavedGameMenuItem : MonoBehaviour
{
    [SerializeField]
    private Image factionIcon;

    [SerializeField]
    private IconButton loadButton;

    [SerializeField]
    private IconButton saveButton;

    [SerializeField]
    private TextMeshProUGUI label;

    private string id;

    private UIButton loadUIButton;
    private UIButton saveUIButton;

    private void Awake()
    {
        if (factionIcon == null)
            throw new InvalidOperationException("FactionIcon is not assigned.");

        if (loadButton == null)
            throw new InvalidOperationException("LoadButton is not assigned.");

        if (saveButton == null)
            throw new InvalidOperationException("SaveButton is not assigned.");

        if (label == null)
            throw new InvalidOperationException("Label is not assigned.");

        loadUIButton = loadButton.GetComponent<UIButton>();
        saveUIButton = saveButton.GetComponent<UIButton>();

        if (loadUIButton == null)
            throw new InvalidOperationException("LoadButton missing UIButton.");

        if (saveUIButton == null)
            throw new InvalidOperationException("SaveButton missing UIButton.");
    }

    public void Bind(
        SaveMenuItemData data,
        Action<string> onLoadClicked,
        Action<string> onSaveClicked
    )
    {
        if (data == null)
            throw new ArgumentNullException(nameof(data));

        id = data.Id;

        label.text = data.DisplayName;

        if (data.FactionIcon != null)
        {
            factionIcon.sprite = data.FactionIcon;
            factionIcon.gameObject.SetActive(true);
        }
        else
        {
            factionIcon.gameObject.SetActive(false);
        }

        loadButton.SetEnabled(data.CanLoad);
        saveButton.SetEnabled(data.CanSave);

        loadUIButton.OnClick.RemoveAllListeners();
        loadUIButton.OnClick.AddListener(() => onLoadClicked(id));

        saveUIButton.OnClick.RemoveAllListeners();
        saveUIButton.OnClick.AddListener(() => onSaveClicked(id));
    }
}
