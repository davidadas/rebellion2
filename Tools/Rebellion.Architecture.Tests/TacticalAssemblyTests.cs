using System;
using System.Linq;
using System.Text.RegularExpressions;
using ArchUnitNET.Fluent;
using ArchUnitNET.Loader;
using ArchUnitNET.NUnit;
using NUnit.Framework;
using Rebellion.Game;
using Rebellion.Tactical;
using Rebellion.UI.SceneUI.TacticalCombat;
using Rebellion.Util.Logging;
using Rebellion.Util.Random;
using static ArchUnitNET.Fluent.ArchRuleDefinition;

namespace Rebellion.Architecture.Tests
{
    [TestFixture]
    public sealed class TacticalAssemblyTests
    {
        [TestCase(
            "^Rebellion\\.AI\\.Tactical(?:\\.|$)",
            "^Rebellion\\.(?:Multiplayer|UI)(?:\\.|$)"
        )]
        [TestCase("^Rebellion\\.Multiplayer\\.Tactical(?:\\.|$)", "^Rebellion\\.UI(?:\\.|$)")]
        [TestCase(
            "^Rebellion\\.UI\\.SceneUI\\.TacticalCombat(?:\\.|$)",
            "^Rebellion\\.Multiplayer(?:\\.|$)"
        )]
        public void TacticalOwnership_ModeAreas_ExcludeAdjacentImplementations(
            string sourceNamespace,
            string excludedNamespace
        )
        {
            Assert.That(
                RuntimeAssemblyLoader
                    .Load()
                    .SelectMany(assembly => assembly.GetTypes())
                    .Any(type => Regex.IsMatch(type.Namespace ?? string.Empty, sourceNamespace)),
                Is.True,
                "The ownership rule must inspect actual production types."
            );
            Types()
                .That()
                .ResideInNamespaceMatching(sourceNamespace)
                .Should()
                .NotDependOnAny(Types().That().ResideInNamespaceMatching(excludedNamespace))
                .Check(ProductionArchitecture.Load());
        }

        [Test]
        public void TacticalCore_UtilityDependencies_ExcludeApplicationMechanisms()
        {
            CreateUtilityRule(
                    "^Rebellion\\.Tactical(?:\\.|$)",
                    typeof(TacticalCombatSession).Assembly.FullName
                )
                .Check(ProductionArchitecture.Load());
        }

        [Test]
        public void TacticalBoundary_LoggerDependency_RejectsIndirectEngineAccess()
        {
            var architecture = new ArchLoader()
                .LoadAssemblies(
                    typeof(TacticalLoggingDependency).Assembly,
                    typeof(GameLogger).Assembly
                )
                .Build();
            string sourcePattern =
                "^" + Regex.Escape(typeof(TacticalLoggingDependency).FullName) + "$";

            Exception failure = Assert.Catch(() =>
                CreateUtilityRule(
                        sourcePattern,
                        typeof(TacticalLoggingDependency).Assembly.FullName
                    )
                    .Check(architecture)
            );

            Assert.That(failure.Message, Does.Contain(nameof(TacticalLoggingDependency)));
            Assert.That(failure.Message, Does.Contain(nameof(GameLogger)));
        }

        [Test]
        public void TacticalCore_Compilation_IsSeparateFromCampaignAndApplication()
        {
            var core = typeof(TacticalCombatSession).Assembly;
            Assert.That(core, Is.Not.EqualTo(typeof(GameRoot).Assembly));
            Assert.That(core, Is.Not.EqualTo(typeof(TacticalCombatController).Assembly));
            Assert.That(core.GetTypes().Contains(typeof(TacticalCombatUnit)), Is.True);
        }

        [Test]
        public void TacticalCore_References_ExcludeApplicationEditorAndEngine()
        {
            string[] references = typeof(TacticalCombatSession)
                .Assembly.GetReferencedAssemblies()
                .Select(reference => reference.Name)
                .ToArray();

            Assert.That(
                references,
                Does.Not.Contain(typeof(TacticalCombatController).Assembly.GetName().Name)
            );
            Assert.That(
                references,
                Does.Not.Contain(typeof(ContentPackEditorProject).Assembly.GetName().Name)
            );
            Assert.That(
                references.Any(name => name.StartsWith("Unity", StringComparison.Ordinal)),
                Is.False
            );
            Assert.That(
                references,
                Does.Contain(typeof(IRandomNumberProvider).Assembly.GetName().Name)
            );
            Assert.That(references, Does.Contain(typeof(GameRoot).Assembly.GetName().Name));
        }

        /// <summary>
        /// Keeps application logging, file access, serialization, and service location outside battle execution.
        /// </summary>
        /// <param name="sourcePattern">The battle types or deliberately invalid fixture to inspect.</param>
        /// <param name="assemblyName">The compiled owner, independent of neighboring mode namespaces.</param>
        /// <returns>The direct utility dependency constraint.</returns>
        private static IArchRule CreateUtilityRule(string sourcePattern, string assemblyName) =>
            Types()
                .That()
                .HaveFullNameMatching(sourcePattern)
                .And()
                .ResideInAssembly(assemblyName)
                .Should()
                .NotDependOnAny(
                    Types()
                        .That()
                        .ResideInNamespaceMatching(
                            "^Rebellion\\.Util\\.(?:IO|Logging|DependencyInjection|Serialization)(?:\\.|$)"
                        )
                );
    }

    internal sealed class TacticalLoggingDependency
    {
        public GameLogger.LogLevel Level => GameLogger.MinimumLevel;
    }
}
