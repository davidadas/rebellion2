using System;
using System.Linq;
using System.Text.RegularExpressions;
using ArchUnitNET.Fluent;
using ArchUnitNET.Loader;
using ArchUnitNET.NUnit;
using NUnit.Framework;
using Rebellion.Game;
using Rebellion.Game.Results;
using Rebellion.Game.SpaceCombat;
using Rebellion.Tactical;
using Rebellion.Util.Logging;
using Rebellion.Util.Random;
using static ArchUnitNET.Fluent.ArchRuleDefinition;

namespace Rebellion.Architecture.Tests
{
    [TestFixture]
    public sealed class TacticalAssemblyTests
    {
        [Test]
        public void TacticalCore_UtilityDependencies_ExcludeApplicationMechanisms()
        {
            CreateUtilityRule("^Rebellion\\.Tactical(?:\\.|$)")
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
                CreateUtilityRule(sourcePattern).Check(architecture)
            );

            Assert.That(failure.Message, Does.Contain(nameof(TacticalLoggingDependency)));
            Assert.That(failure.Message, Does.Contain(nameof(GameLogger)));
        }

        [Test]
        public void TacticalCore_Compilation_IsSeparateFromCampaignAndContracts()
        {
            var core = typeof(TacticalCombatSession).Assembly;
            Assert.That(core, Is.Not.EqualTo(typeof(GameRoot).Assembly));
            Assert.That(core, Is.Not.EqualTo(typeof(TacticalCombatOutcome).Assembly));
            Assert.That(core, Is.Not.EqualTo(typeof(CombatSide).Assembly));
            Assert.That(core.GetTypes().Contains(typeof(TacticalCombatUnit)), Is.True);
        }

        [Test]
        public void TacticalCore_References_ExcludeCampaignEditorAndEngine()
        {
            string[] references = typeof(TacticalCombatSession)
                .Assembly.GetReferencedAssemblies()
                .Select(reference => reference.Name)
                .ToArray();

            Assert.That(references, Does.Not.Contain(typeof(GameRoot).Assembly.GetName().Name));
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
            Assert.That(
                references,
                Does.Contain(typeof(TacticalCombatOutcome).Assembly.GetName().Name)
            );
        }

        [TestCase(typeof(CombatSide))]
        [TestCase(typeof(TacticalCombatOutcome))]
        public void CombatContracts_References_ExcludeBothModeImplementations(System.Type contract)
        {
            string[] references = contract
                .Assembly.GetReferencedAssemblies()
                .Select(reference => reference.Name)
                .ToArray();

            Assert.That(references, Does.Not.Contain(typeof(GameRoot).Assembly.GetName().Name));
            Assert.That(
                references,
                Does.Not.Contain(typeof(TacticalCombatSession).Assembly.GetName().Name)
            );
        }

        /// <summary>
        /// Keeps application logging, file access, serialization, and service location outside battle execution.
        /// </summary>
        /// <param name="sourcePattern">The battle types or deliberately invalid fixture to inspect.</param>
        /// <returns>The direct utility dependency constraint.</returns>
        private static IArchRule CreateUtilityRule(string sourcePattern) =>
            Types()
                .That()
                .HaveFullNameMatching(sourcePattern)
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
