using ArchUnitNET.Fluent;
using ArchUnitNET.Loader;
using ArchUnitNET.NUnit;
using NUnit.Framework;
using Rebellion.Game;
using static ArchUnitNET.Fluent.ArchRuleDefinition;

namespace Rebellion.Architecture.Tests
{
    [TestFixture]
    public sealed class ArchitectureDependencyTests
    {
        private static readonly ArchUnitNET.Domain.Architecture _architecture = new ArchLoader()
            .LoadAssemblies(typeof(GameRoot).Assembly)
            .Build();

        [Test]
        public void GameDomain_Dependencies_ReferenceOnlyGameDomain()
        {
            IArchRule rule = Types()
                .That()
                .ResideInNamespaceMatching("^Rebellion\\.Game(?:\\.|$)")
                .Should()
                .NotDependOnAny(
                    Types()
                        .That()
                        .DoNotResideInNamespaceMatching("^Rebellion\\.Game(?:\\.|$)")
                        .And()
                        .DoNotResideInNamespaceMatching("^Rebellion\\.SceneGraph(?:\\.|$)")
                        .And()
                        .DoNotResideInNamespaceMatching("^Rebellion\\.Util(?:\\.|$)")
                );

            rule.Check(_architecture);
        }

        [Test]
        public void GameplayRuntime_Dependencies_DoNotReferenceUserInterface()
        {
            IArchRule rule = Types()
                .That()
                .ResideInNamespaceMatching("^Rebellion\\.(?:Systems|Simulation)(?:\\.|$)")
                .Should()
                .NotDependOnAny(
                    Types().That().ResideInNamespaceMatching("^Rebellion\\.UI(?:\\.|$)")
                );

            rule.Check(_architecture);
        }

        [Test]
        public void SceneGraph_Dependencies_ReferenceOnlySceneGraph()
        {
            IArchRule rule = Types()
                .That()
                .ResideInNamespaceMatching("^Rebellion\\.SceneGraph(?:\\.|$)")
                .Should()
                .NotDependOnAny(
                    Types()
                        .That()
                        .DoNotResideInNamespaceMatching("^Rebellion\\.SceneGraph(?:\\.|$)")
                        .And()
                        .DoNotHaveFullName(
                            "Rebellion.Util.Serialization.PersistableObjectAttribute"
                        )
                        .And()
                        .DoNotHaveFullName(
                            "Rebellion.Util.Serialization.PersistableIgnoreAttribute"
                        )
                );

            rule.Check(_architecture);
        }

        [TestCase("Rebellion.Util.Logging")]
        [TestCase("Rebellion.Util.Mathematics")]
        [TestCase("Rebellion.Util.Random")]
        [TestCase("Rebellion.Util.Reflection")]
        [TestCase("Rebellion.Util.Serialization")]
        [TestCase("Rebellion.Util.DependencyInjection")]
        public void UtilityArea_Dependencies_ReferenceOnlySameUtilityArea(string utilityNamespace)
        {
            string escapedNamespace = utilityNamespace.Replace(".", "\\.");
            IArchRule rule = Types()
                .That()
                .ResideInNamespaceMatching($"^{escapedNamespace}(?:\\.|$)")
                .Should()
                .NotDependOnAny(
                    Types()
                        .That()
                        .ResideInNamespaceMatching("^Rebellion(?:\\.|$)")
                        .And()
                        .DoNotResideInNamespaceMatching($"^{escapedNamespace}(?:\\.|$)")
                );

            rule.Check(_architecture);
        }
    }
}
