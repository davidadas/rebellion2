using System;
using System.Text.RegularExpressions;
using ArchUnitNET.Fluent;
using ArchUnitNET.Loader;
using ArchUnitNET.NUnit;
using NUnit.Framework;
using Rebellion.Game;
using Rebellion.Multiplayer;
using Rebellion.Tactical.Networking;
using Rebellion.Tactical.Presentation;
using Rebellion.Util.Random;
using static ArchUnitNET.Fluent.ArchRuleDefinition;

namespace Rebellion.Architecture.Tests
{
    [TestFixture]
    public sealed class GameplayArchitectureTests
    {
        private const string _runtimePattern = "^Rebellion\\.(?:Systems|Gameplay)(?:\\.|$)";
        private const string _presentationPattern =
            "^(?:Rebellion\\.(?:UI|Multiplayer|Tactical\\.(?:Presentation|Networking))(?:\\..*)?|[^.<][^.]*)$";
        private static readonly ArchUnitNET.Domain.Architecture _architecture =
            ProductionArchitecture.Load();

        [Test]
        public void GameplayRuntime_Dependencies_ExcludePresentationTransportAndUnqualifiedServices()
        {
            CreateGameplayRule(_runtimePattern).Check(_architecture);
        }

        [TestCase(typeof(OptionsMenuController))]
        public void GameplayBoundary_GlobalUiType_IsCovered(System.Type presentationType)
        {
            Assert.That(presentationType.Namespace, Is.Null.Or.Empty);
            Assert.That(Regex.IsMatch(presentationType.FullName, _presentationPattern), Is.True);
        }

        [TestCase(typeof(TacticalCombatController))]
        [TestCase(typeof(TacticalCombatRenderer))]
        [TestCase(typeof(TacticalCombatHudPanel))]
        [TestCase(typeof(TacticalCombatReplicaPlayback))]
        public void GameplayBoundary_TacticalImplementation_IsCovered(
            System.Type implementationType
        )
        {
            Assert.That(Regex.IsMatch(implementationType.FullName, _presentationPattern), Is.True);
        }

        [Test]
        public void GameplayBoundary_GlobalUiDependency_RejectsArchitecture()
        {
            var architecture = new ArchLoader()
                .LoadAssemblies(typeof(GameRoot).Assembly, typeof(GameplayUiDependency).Assembly)
                .Build();
            string sourcePattern = "^" + Regex.Escape(typeof(GameplayUiDependency).FullName) + "$";

            Exception failure = Assert.Catch(() =>
                CreateGameplayRule(sourcePattern).Check(architecture)
            );

            Assert.That(failure.Message, Does.Contain(nameof(GameplayUiDependency)));
            Assert.That(failure.Message, Does.Contain(nameof(TacticalCombatHudState)));
        }

        [Test]
        public void GameplayBoundary_MultiplayerPlaybackDependency_RejectsArchitecture()
        {
            var architecture = new ArchLoader()
                .LoadAssemblies(
                    typeof(GameRoot).Assembly,
                    typeof(GameplayTransportDependency).Assembly
                )
                .Build();
            string sourcePattern =
                "^" + Regex.Escape(typeof(GameplayTransportDependency).FullName) + "$";

            Exception failure = Assert.Catch(() =>
                CreateGameplayRule(sourcePattern).Check(architecture)
            );

            Assert.That(failure.Message, Does.Contain(nameof(GameplayTransportDependency)));
            Assert.That(failure.Message, Does.Contain("TacticalCombatReplicaPlayback"));
        }

        private static IArchRule CreateGameplayRule(string sourcePattern)
        {
            // Runtime services use qualified domain names. The existing unqualified content
            // catalog is their sole data boundary; all other global types remain outside gameplay.
            // Excluding names beginning with '<' leaves compiler-generated implementation types alone.
            return Types()
                .That()
                .HaveFullNameMatching(sourcePattern)
                .Should()
                .NotDependOnAny(
                    Types()
                        .That()
                        .HaveFullNameMatching(_presentationPattern)
                        .And()
                        .DoNotHaveFullName(nameof(GameDataCatalog))
                );
        }
    }

    internal sealed class GameplayUiDependency
    {
        public TacticalCombatHudState View { get; set; }
    }

    internal sealed class GameplayTransportDependency
    {
        public TacticalCombatReplicaPlayback Playback { get; set; }
    }
}
