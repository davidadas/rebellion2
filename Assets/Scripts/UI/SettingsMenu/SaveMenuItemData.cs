using UnityEngine;
using UnityEngine.UI;

public enum SaveMenuItemType
{
    NewSave,
    ExistingSave,
}

public sealed class SaveMenuItemData
{
    public string Id;
    public string DisplayName;

    public Sprite FactionIcon;

    public bool CanLoad;
    public bool CanSave;

    public SaveMenuItemType Type;
}
