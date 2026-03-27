using Rebellion.Util.Common;
using UnityEngine;

public static class GameInput
{
    public static bool ToggleSettingsMenuPressed()
    {
        return Input.GetKeyDown(KeyCode.Escape);
    }

    public static bool QuickSavePressed()
    {
        return (Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl))
            && Input.GetKeyDown(KeyCode.S);
    }

    public static bool QuickLoadPressed()
    {
        return (Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl))
            && Input.GetKeyDown(KeyCode.L);
    }
}
