using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Global input handler that processes keyboard input across all scenes.
/// Translates input into intent and delegates to GameRuntime for execution.
/// </summary>
public sealed class GlobalInputHandler : MonoBehaviour
{
    private GameRuntime runtime;
    private readonly Stack<InputContext> contextStack = new();

    private InputContext CurrentContext =>
        contextStack.Count > 0 ? contextStack.Peek() : InputContext.None;

    private void Awake()
    {
        contextStack.Push(InputContext.None);
    }

    /// <summary>
    /// Initialize with the application runtime.
    /// Called by AppBootstrap.
    /// </summary>
    public void Initialize(GameRuntime runtime)
    {
        this.runtime = runtime;
    }

    /// <summary>
    /// Configure default settings for auto-created input handlers.
    /// Called when AppBootstrap creates the handler programmatically.
    /// </summary>
    public void ConfigureDefaults()
    {
        // Future: set default bindings, flags, etc
        // For now, no additional configuration needed
    }

    private void Update()
    {
        if (runtime == null)
            return;

        ProcessInput();
    }

    public void SetContext(InputContext context)
    {
        contextStack.Clear();
        contextStack.Push(context);
    }

    public void PushContext(InputContext context)
    {
        contextStack.Push(context);
    }

    private void HandleGameplayInput()
    {
        if (GameInput.ToggleSettingsMenuPressed())
            runtime.ToggleSettingsMenu();

        if (GameInput.QuickSavePressed())
            runtime.QuickSave();

        if (GameInput.QuickLoadPressed())
            runtime.QuickLoad();
    }

    private void HandleMenuInput()
    {
        if (GameInput.ToggleSettingsMenuPressed())
            runtime.ToggleSettingsMenu();
    }

    public void PopContext()
    {
        if (contextStack.Count > 1)
            contextStack.Pop();
    }

    private void ProcessInput()
    {
        switch (CurrentContext)
        {
            case InputContext.Cutscene:
                // Only allow skip behavior (handled elsewhere).
                return;

            case InputContext.Menu:
                HandleMenuInput();
                return;

            case InputContext.Strategy:
            case InputContext.Tactical:
            case InputContext.None:
                HandleGameplayInput();
                return;
        }
    }
}
