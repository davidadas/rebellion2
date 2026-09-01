using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;

namespace Rebellion.Tests.Editor;

public sealed class HeadlessSimulationRunnerTests
{
    [TestCase("Easy", 0)]
    [TestCase("medium", 1)]
    [TestCase("Hard", 2)]
    [TestCase("999", 1)]
    [TestCase("invalid", 1)]
    public void SimulationOptions_ParseDifficulty_ReturnsDefinedValueOrDefault(
        string value,
        int expected
    )
    {
        Type optionsType = AppDomain
            .CurrentDomain.GetAssemblies()
            .Select(assembly => assembly.GetType("HeadlessSimulationRunner+SimulationOptions"))
            .First(type => type != null);
        MethodInfo parse = optionsType.GetMethod(
            "Parse",
            BindingFlags.Public | BindingFlags.Static
        );

        object options = parse.Invoke(null, new object[] { new[] { "-simDifficulty", value } });
        object difficulty = optionsType.GetProperty("Difficulty").GetValue(options);

        Assert.That(Convert.ToInt32(difficulty), Is.EqualTo(expected));
    }
}
