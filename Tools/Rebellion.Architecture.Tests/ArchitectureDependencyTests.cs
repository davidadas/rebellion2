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

        /// <summary>
        /// Verifies that game-domain code is fully contained within the game namespace.
        /// </summary>
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

        /// <summary>
        /// Verifies that gameplay systems do not depend on presentation code.
        /// </summary>
        [Test]
        public void GameplaySystems_Dependencies_DoNotReferenceUserInterface()
        {
            IArchRule rule = Types()
                .That()
                .ResideInNamespaceMatching("^Rebellion\\.Systems(?:\\.|$)")
                .Should()
                .NotDependOnAny(
                    Types().That().ResideInNamespaceMatching("^Rebellion\\.UI(?:\\.|$)")
                );

            rule.Check(_architecture);
        }

        /// <summary>
        /// Verifies that scene-graph code is self-contained except for persistence annotations.
        /// </summary>
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
                        .And()
                        .DoNotHaveFullName(
                            "Rebellion.Util.Serialization.PersistableObjectAttribute"
                        )
                );

            rule.Check(_architecture);
        }

        /// <summary>
        /// Verifies that each utility area depends only on types from its own namespace.
        /// </summary>
        /// <param name="utilityNamespace">The utility namespace to verify.</param>
        [TestCase("Rebellion.Util.Logging")]
        [TestCase("Rebellion.Util.Mathematics")]
        [TestCase("Rebellion.Util.Random")]
        [TestCase("Rebellion.Util.Reflection")]
        [TestCase("Rebellion.Util.Serialization")]
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
