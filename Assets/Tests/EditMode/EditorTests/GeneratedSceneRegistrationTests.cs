using NUnit.Framework;
using UnityEditor;
using UnityEngine.SceneManagement;

namespace Rebellion.Tests.Editor;

public sealed class GeneratedSceneRegistrationTests
{
    [TestCase("Assets/Scenes/BootScene.unity", 0)]
    [TestCase("Assets/Scenes/MainMenu.unity", 1)]
    [TestCase("Assets/Scenes/StrategyView.unity", 2)]
    public void GeneratedSceneIsAvailableByBuildIndex(string scenePath, int expectedIndex)
    {
        Assert.That(EditorBuildSettings.scenes[expectedIndex].path, Is.EqualTo(scenePath));
        Assert.That(SceneUtility.GetBuildIndexByScenePath(scenePath), Is.EqualTo(expectedIndex));
    }
}
