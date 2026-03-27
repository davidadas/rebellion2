using UnityEngine;
using UnityEngine.SceneManagement;

public sealed class SettingsMenuController : MonoBehaviour
{
    [SerializeField]
    private GameObject contentRoot;

    private GameRuntime runtime;

    private void Start()
    {
        runtime = AppBootstrap.Instance.GetRuntime();
        runtime.ToggleSettingsMenuRequested += HandleToggle;

        Close();
    }

    private void OnDestroy()
    {
        if (runtime != null)
            runtime.ToggleSettingsMenuRequested -= HandleToggle;
    }

    public void Open()
    {
        if (contentRoot.activeSelf)
            return;

        contentRoot.SetActive(true);
    }

    public void Close()
    {
        if (!contentRoot.activeSelf)
            return;

        contentRoot.SetActive(false);
    }

    public void Toggle()
    {
        if (contentRoot.activeSelf)
            Close();
        else
            Open();
    }

    public bool IsOpen()
    {
        return contentRoot.activeSelf;
    }

    private void HandleToggle()
    {
        Toggle();
    }

    public void OnResumePressed()
    {
        Close();
    }

    public void OnQuitToMenuPressed()
    {
        runtime.EndGame();
        SceneManager.LoadScene("MainMenu");
    }
}
