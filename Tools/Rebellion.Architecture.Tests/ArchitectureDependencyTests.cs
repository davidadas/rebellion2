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
            .LoadAssemblies(typeof(GameRoot).Assembly, typeof(ContentPackEditorProject).Assembly)
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
                .ResideInNamespaceMatching("^Rebellion\\.(?:Systems|Gameplay)(?:\\.|$)")
                .Should()
                .NotDependOnAny(
                    Types().That().ResideInNamespaceMatching("^Rebellion\\.UI(?:\\.|$)")
                );

            rule.Check(_architecture);
        }

        /// <summary>
        /// Keeps content loading policy independent of scene presentation and bootstrap handoffs.
        /// </summary>
        [Test]
        public void ContentLoadOperation_Dependencies_DoNotReferenceSceneOrBootstrapOwners()
        {
            IArchRule rule = Types()
                .That()
                .HaveFullName("ContentLoadOperation")
                .Should()
                .NotDependOnAny(
                    Types()
                        .That()
                        .HaveFullName("AppBootstrap")
                        .Or()
                        .HaveFullName("GameLaunchContext")
                        .Or()
                        .HaveFullName("LoadingScreenController")
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

        [TestCase("AppBootstrap")]
        [TestCase("LoadingScreenController")]
        [TestCase("GameLaunchContext")]
        [TestCase("GameSessionManager")]
        [TestCase("IGameSessionOwner")]
        [TestCase("SaveGameManager")]
        [TestCase("GameContentBinding")]
        [TestCase("Rebellion.Gameplay.GameSession")]
        [TestCase("Rebellion.Game.Scripting.GameConditional")]
        [TestCase("Rebellion.Multiplayer.MultiplayerCoordinator")]
        public void OwnershipBoundary_TypeName_ExistsInInspectedAssembly(string typeName)
        {
            Assert.That(typeof(GameRoot).Assembly.GetType(typeName, false), Is.Not.Null);
        }

        [Test]
        public void GameConditional_Dependencies_ExcludeSceneGraphIdentity()
        {
            Types()
                .That()
                .HaveFullName("Rebellion.Game.Scripting.GameConditional")
                .Should()
                .NotDependOnAny(
                    Types().That().ResideInNamespaceMatching("^Rebellion\\.SceneGraph(?:\\.|$)")
                )
                .Check(_architecture);
        }

        [Test]
        public void GameSessionManager_Dependencies_ExcludePresentationAndMultiplayerImplementations()
        {
            Types()
                .That()
                .HaveFullName("GameSessionManager")
                .Should()
                .NotDependOnAny(
                    Types()
                        .That()
                        .ResideInNamespaceMatching("^Rebellion\\.(?:Multiplayer|UI)(?:\\.|$)")
                        .Or()
                        .HaveFullName("AppBootstrap")
                        .Or()
                        .HaveFullName("LoadingScreenController")
                )
                .Check(_architecture);
        }

        [Test]
        public void MultiplayerCoordinator_Dependencies_ExcludeApplicationBootstrapAndScenePresentation()
        {
            Types()
                .That()
                .HaveFullName("Rebellion.Multiplayer.MultiplayerCoordinator")
                .Should()
                .NotDependOnAny(
                    Types()
                        .That()
                        .HaveFullName("AppBootstrap")
                        .Or()
                        .HaveFullName("LoadingScreenController")
                        .Or()
                        .HaveFullName("GameLaunchContext")
                )
                .Check(_architecture);
        }

        [Test]
        public void GameSession_Dependencies_ExcludeSessionManagementAndSaveStorage()
        {
            Types()
                .That()
                .HaveFullName("Rebellion.Gameplay.GameSession")
                .Should()
                .NotDependOnAny(
                    Types()
                        .That()
                        .HaveFullName("GameSessionManager")
                        .Or()
                        .HaveFullName("IGameSessionOwner")
                        .Or()
                        .HaveFullName("SaveGameManager")
                        .Or()
                        .HaveFullName("AppBootstrap")
                        .Or()
                        .ResideInNamespaceMatching("^Rebellion\\.(?:Multiplayer|UI)(?:\\.|$)")
                )
                .Check(_architecture);
        }

        [Test]
        public void SaveGameManager_Dependencies_ExcludeSessionAndMultiplayerCoordination()
        {
            Types()
                .That()
                .HaveFullName("SaveGameManager")
                .Should()
                .NotDependOnAny(
                    Types()
                        .That()
                        .HaveFullName("GameSessionManager")
                        .Or()
                        .HaveFullName("IGameSessionOwner")
                        .Or()
                        .ResideInNamespaceMatching("^Rebellion\\.Multiplayer(?:\\.|$)")
                )
                .Check(_architecture);
        }

        [Test]
        public void GameContentBinding_Dependencies_ExcludeApplicationAndStorageOwners()
        {
            Types()
                .That()
                .HaveFullName("GameContentBinding")
                .Should()
                .NotDependOnAny(
                    Types()
                        .That()
                        .HaveFullName("GameSessionManager")
                        .Or()
                        .HaveFullName("SaveGameManager")
                        .Or()
                        .HaveFullName("AppBootstrap")
                        .Or()
                        .ResideInNamespaceMatching("^Rebellion\\.(?:Multiplayer|UI)(?:\\.|$)")
                )
                .Check(_architecture);
        }

        [TestCase("Rebellion.Util.IO")]
        [TestCase("Rebellion.Util.Logging")]
        [TestCase("Rebellion.Util.Mathematics")]
        [TestCase("Rebellion.Util.Random")]
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
