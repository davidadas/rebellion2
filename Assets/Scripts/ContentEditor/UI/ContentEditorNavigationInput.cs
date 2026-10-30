using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// Keeps application shortcuts from also activating the editor's ordinary UI navigation axes.
/// </summary>
[RequireComponent(typeof(StandaloneInputModule))]
[DisallowMultipleComponent]
public sealed class ContentEditorNavigationInput : BaseInput
{
    private StandaloneInputModule module;

    /// <summary>
    /// Supplies the authored input module with the editor's navigation policy.
    /// </summary>
    protected override void OnEnable()
    {
        base.OnEnable();
        module = GetComponent<StandaloneInputModule>();
        module.inputOverride = this;
    }

    /// <summary>
    /// Releases the input override without replacing another owner's override.
    /// </summary>
    protected override void OnDisable()
    {
        if (module != null && module.inputOverride == this)
            module.inputOverride = null;
        base.OnDisable();
    }

    /// <summary>
    /// Uses arrow keys for desktop navigation and suppresses movement during modifier shortcuts.
    /// </summary>
    /// <param name="axisName">The axis requested by the UI module.</param>
    /// <returns>The arrow-key axis value, or zero while a shortcut modifier is held.</returns>
    public override float GetAxisRaw(string axisName)
    {
        if (
            Input.GetKey(KeyCode.LeftControl)
            || Input.GetKey(KeyCode.RightControl)
            || Input.GetKey(KeyCode.LeftCommand)
            || Input.GetKey(KeyCode.RightCommand)
            || Input.GetKey(KeyCode.LeftAlt)
            || Input.GetKey(KeyCode.RightAlt)
        )
            return 0;
        if (axisName == module.horizontalAxis)
            return (Input.GetKey(KeyCode.RightArrow) ? 1 : 0)
                - (Input.GetKey(KeyCode.LeftArrow) ? 1 : 0);
        if (axisName == module.verticalAxis)
            return (Input.GetKey(KeyCode.UpArrow) ? 1 : 0)
                - (Input.GetKey(KeyCode.DownArrow) ? 1 : 0);
        return base.GetAxisRaw(axisName);
    }
}
