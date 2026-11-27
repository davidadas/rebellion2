using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using ArchUnitNET.Fluent;
using ArchUnitNET.Loader;
using ArchUnitNET.NUnit;
using NUnit.Framework;
using Rebellion.Game;
using Rebellion.Util.Random;
using static ArchUnitNET.Fluent.ArchRuleDefinition;

namespace Rebellion.Architecture.Tests
{
    [TestFixture]
    public sealed class ArchitectureDependencyTests
    {
        private static readonly Assembly[] _runtimeAssemblies = RuntimeAssemblyLoader.Load();

        private static readonly ArchUnitNET.Domain.Architecture _architecture = new ArchLoader()
            .LoadAssemblies(
                _runtimeAssemblies.Append(typeof(ContentPackEditorProject).Assembly).ToArray()
            )
            .Build();

        [Test]
        public void RuntimeAssemblies_Dependencies_FollowModuleDirection()
        {
            Dictionary<string, string[]> allowedReferences = new()
            {
                ["Rebellion.Util"] = [],
                ["Rebellion.SceneGraph"] = ["Rebellion.Util"],
                ["Rebellion.Game"] = ["Rebellion.SceneGraph", "Rebellion.Util"],
                ["Rebellion.Tactical.Core"] =
                [
                    "Rebellion.Content",
                    "Rebellion.Game",
                    "Rebellion.SceneGraph",
                    "Rebellion.Util",
                ],
                ["Rebellion.UserSettings"] = [],
                ["Rebellion.Content"] =
                [
                    "Rebellion.Game",
                    "Rebellion.SceneGraph",
                    "Rebellion.Util",
                ],
                ["Rebellion.Generation"] =
                [
                    "Rebellion.Content",
                    "Rebellion.Game",
                    "Rebellion.SceneGraph",
                    "Rebellion.Util",
                ],
                ["Rebellion.Saves"] =
                [
                    "Rebellion.Game",
                    "Rebellion.UserSettings",
                    "Rebellion.Util",
                ],
                ["Rebellion.Analytics"] =
                [
                    "Rebellion.Game",
                    "Rebellion.SceneGraph",
                    "Rebellion.UserSettings",
                    "Rebellion.Util",
                ],
                ["Rebellion.Gameplay"] =
                [
                    "Rebellion.Content",
                    "Rebellion.Game",
                    "Rebellion.SceneGraph",
                    "Rebellion.Tactical.Core",
                    "Rebellion.Util",
                ],
                ["Rebellion.AI"] =
                [
                    "Rebellion.Game",
                    "Rebellion.Gameplay",
                    "Rebellion.SceneGraph",
                    "Rebellion.Tactical.Core",
                    "Rebellion.Util",
                ],
                ["Rebellion.Sessions"] =
                [
                    "Rebellion.AI",
                    "Rebellion.Analytics",
                    "Rebellion.Content",
                    "Rebellion.Game",
                    "Rebellion.Gameplay",
                    "Rebellion.Saves",
                    "Rebellion.SceneGraph",
                    "Rebellion.Tactical.Core",
                    "Rebellion.UserSettings",
                    "Rebellion.Util",
                ],
                ["Rebellion.Networking.Transport"] = [],
                ["Rebellion.Multiplayer"] =
                [
                    "Rebellion.Analytics",
                    "Rebellion.Content",
                    "Rebellion.Game",
                    "Rebellion.Gameplay",
                    "Rebellion.Networking.Transport",
                    "Rebellion.Saves",
                    "Rebellion.SceneGraph",
                    "Rebellion.Sessions",
                    "Rebellion.Tactical.Core",
                    "Rebellion.UserSettings",
                    "Rebellion.Util",
                ],
            };

            foreach ((string assemblyName, string[] allowed) in allowedReferences)
            {
                Assembly assembly = _runtimeAssemblies.Single(candidate =>
                    candidate.GetName().Name == assemblyName
                );
                string[] projectReferences = assembly
                    .GetReferencedAssemblies()
                    .Select(reference => reference.Name)
                    .Where(reference => reference.StartsWith("Rebellion."))
                    .ToArray();
                CollectionAssert.IsSubsetOf(projectReferences, allowed, assemblyName);
            }
        }

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
            Assert.That(
                _runtimeAssemblies.Any(assembly => assembly.GetType(typeName, false) != null),
                Is.True
            );
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
            Assert.That(
                typeof(IRandomNumberProvider)
                    .Assembly.GetTypes()
                    .Any(type => type.Namespace == utilityNamespace),
                Is.True,
                "The utility area must be present in the inspected assembly."
            );
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
