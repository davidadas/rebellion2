using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using NUnit.Framework;
using Rebellion.Game;
using Rebellion.Game.Factions;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Messages;
using Rebellion.Game.Units;
using UnityEngine;
using GalaxyPlanetSector = Rebellion.Game.Galaxy.PlanetSector;

namespace Rebellion.Tests.UI.SceneUI.StrategyView.Screen
{
    [TestFixture]
    public class StrategyBriefingControllerTests
    {
        private const string _opponentFactionID = "OPPONENT";
        private const string _playerFactionID = "PLAYER";
        private const string _prefabPath = "Assets/Prefabs/UI/StrategyView/StrategyViewRoot.prefab";

        /// <summary>
        /// Verifies the briefing owner prepares its opening segment and skip response.
        /// </summary>
        [Test]
        public async Task PrepareAsync_Briefing_LoadsOpeningMediaAndRegistersAudioAsync()
        {
            GameRoot game = CreateGame();
            StrategyAdvisorTheme advisorTheme = CreateAdvisorTheme();
            StrategyBriefingTheme briefing = CreateBriefing();
            StrategyBriefingSegmentTheme first = CreateSegment("First");
            first.Audio = "FirstVoice";
            StrategyBriefingSegmentTheme second = CreateSegment("Second");
            second.Audio = "SecondVoice";
            StrategyBriefingSegmentTheme skip = CreateSegment("Skip");
            skip.Audio = "SkipVoice";
            briefing.Segments.Add(first);
            briefing.Segments.Add(second);
            briefing.Skip = skip;
            Texture2D idle = new Texture2D(1, 1);
            ContentPreloadManifest loadedManifest = null;
            string[] registeredAudio = null;
            GameObject rootObject = UIComponentTestHelper.InstantiatePrefab(_prefabPath);
            try
            {
                StrategyBriefingController controller = CreateController(
                    game,
                    advisorTheme,
                    CreateTextures(advisorTheme, idle),
                    rootObject,
                    manifest =>
                    {
                        loadedManifest = manifest;
                        return Task.CompletedTask;
                    },
                    paths => registeredAudio = paths.ToArray()
                );

                await controller.PrepareAsync(briefing);

                CollectionAssert.AreEquivalent(
                    new[]
                    {
                        $"{briefing.AnimationImageRoot}/{first.Animation}",
                        $"{briefing.AnimationImageRoot}/{skip.Animation}",
                    },
                    loadedManifest.TextureDirectories
                );
                CollectionAssert.AreEquivalent(
                    new[] { briefing.GetAudioPath(first.Audio), briefing.GetAudioPath(skip.Audio) },
                    registeredAudio
                );
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(idle);
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        /// <summary>
        /// Verifies that briefing segments preload and play sequentially before completion.
        /// </summary>
        [Test]
        public async Task Play_MultipleSegments_PlaysInOrderAndCompletesAsync()
        {
            GameRoot game = CreateGame();
            StrategyAdvisorTheme advisorTheme = CreateAdvisorTheme();
            StrategyBriefingTheme briefing = CreateBriefing();
            StrategyBriefingSegmentTheme firstSegment = CreateSegment("First");
            StrategyBriefingSegmentTheme secondSegment = CreateSegment("Second");
            secondSegment.Audio = "SecondVoice";
            briefing.Segments.Add(firstSegment);
            briefing.Segments.Add(secondSegment);
            Texture2D idle = new Texture2D(1, 1);
            Texture2D first = new Texture2D(1, 1);
            Texture2D second = new Texture2D(1, 1);
            Dictionary<string, Texture2D> textures = CreateTextures(advisorTheme, idle);
            textures[briefing.GetFramePath("First", 0)] = first;
            textures[briefing.GetFramePath("Second", 0)] = second;
            GameObject rootObject = UIComponentTestHelper.InstantiatePrefab(_prefabPath);
            try
            {
                StrategyAdvisorView advisorView =
                    rootObject.GetComponentInChildren<StrategyAdvisorView>(true);
                StrategyBriefingController controller = CreateController(
                    game,
                    advisorTheme,
                    textures,
                    rootObject
                );
                bool? skipped = null;

                controller.Play(briefing, wasSkipped => skipped = wasSkipped);

                Assert.AreSame(first, GetProtocolImage(rootObject).texture);
                advisorView.AdvanceAnimation(0.5f);
                await WaitUntilAsync(() => GetProtocolImage(rootObject).texture == second);
                Assert.AreSame(second, GetProtocolImage(rootObject).texture);
                Assert.IsNull(skipped);
                advisorView.AdvanceAnimation(0.5f);
                Assert.IsFalse(skipped);
                Assert.AreSame(idle, GetProtocolImage(rootObject).texture);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(second);
                UnityEngine.Object.DestroyImmediate(first);
                UnityEngine.Object.DestroyImmediate(idle);
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        /// <summary>
        /// Verifies the next segment starts loading only after the current segment completes.
        /// </summary>
        [Test]
        public async Task Play_OpeningSegment_DoesNotLoadOrPlayNextUntilReadyAsync()
        {
            GameRoot game = CreateGame();
            StrategyAdvisorTheme advisorTheme = CreateAdvisorTheme();
            StrategyBriefingTheme briefing = CreateBriefing();
            StrategyBriefingSegmentTheme firstSegment = CreateSegment("First");
            StrategyBriefingSegmentTheme secondSegment = CreateSegment("Second");
            secondSegment.Audio = "SecondVoice";
            briefing.Segments.Add(firstSegment);
            briefing.Segments.Add(secondSegment);
            Texture2D idle = new Texture2D(1, 1);
            Texture2D first = new Texture2D(1, 1);
            Texture2D second = new Texture2D(1, 1);
            Dictionary<string, Texture2D> textures = CreateTextures(advisorTheme, idle);
            textures[briefing.GetFramePath("First", 0)] = first;
            textures[briefing.GetFramePath("Second", 0)] = second;
            List<ContentPreloadManifest> preloadRequests = new List<ContentPreloadManifest>();
            string preloadedSfx = null;
            TaskCompletionSource<bool> secondSegmentReady = new TaskCompletionSource<bool>();
            GameObject rootObject = UIComponentTestHelper.InstantiatePrefab(_prefabPath);
            try
            {
                StrategyBriefingController controller = CreateController(
                    game,
                    advisorTheme,
                    textures,
                    rootObject,
                    manifest =>
                    {
                        preloadRequests.Add(manifest);
                        return secondSegmentReady.Task;
                    },
                    paths => preloadedSfx = paths.Single()
                );
                StrategyAdvisorView advisorView =
                    rootObject.GetComponentInChildren<StrategyAdvisorView>();

                controller.Play(briefing, null);

                Assert.IsEmpty(preloadRequests);
                Assert.AreSame(first, GetProtocolImage(rootObject).texture);

                advisorView.AdvanceAnimation(0.5f);

                Assert.IsEmpty(preloadRequests);
                await WaitUntilAsync(() => preloadRequests.Count == 1);

                Assert.AreEqual(1, preloadRequests.Count);
                CollectionAssert.AreEqual(
                    new[] { $"{briefing.AnimationImageRoot}/{secondSegment.Animation}" },
                    preloadRequests[0].TextureDirectories
                );
                Assert.AreSame(first, GetProtocolImage(rootObject).texture);

                secondSegmentReady.SetResult(true);
                await WaitUntilAsync(() => GetProtocolImage(rootObject).texture == second);

                Assert.AreSame(second, GetProtocolImage(rootObject).texture);
                Assert.AreEqual(briefing.GetAudioPath(secondSegment.Audio), preloadedSfx);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(second);
                UnityEngine.Object.DestroyImmediate(first);
                UnityEngine.Object.DestroyImmediate(idle);
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        public void Play_NoSegments_CompletesWithoutSkipping()
        {
            GameRoot game = CreateGame();
            StrategyAdvisorTheme advisorTheme = CreateAdvisorTheme();
            Texture2D idle = new Texture2D(1, 1);
            GameObject rootObject = UIComponentTestHelper.InstantiatePrefab(_prefabPath);
            try
            {
                StrategyBriefingController controller = CreateController(
                    game,
                    advisorTheme,
                    CreateTextures(advisorTheme, idle),
                    rootObject
                );
                bool? skipped = null;

                controller.Play(CreateBriefing(), wasSkipped => skipped = wasSkipped);

                Assert.IsFalse(skipped);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(idle);
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [TestCase(null, 0)]
        [TestCase("Missing", 1)]
        public void Play_InvalidSegment_ThrowsInvalidOperationException(
            string animation,
            int frameCount
        )
        {
            GameRoot game = CreateGame();
            StrategyAdvisorTheme advisorTheme = CreateAdvisorTheme();
            StrategyBriefingTheme briefing = CreateBriefing();
            briefing.Segments.Add(
                new StrategyBriefingSegmentTheme { Animation = animation, FrameCount = frameCount }
            );
            Texture2D idle = new Texture2D(1, 1);
            GameObject rootObject = UIComponentTestHelper.InstantiatePrefab(_prefabPath);
            try
            {
                StrategyBriefingController controller = CreateController(
                    game,
                    advisorTheme,
                    CreateTextures(advisorTheme, idle),
                    rootObject
                );

                Assert.Throws<InvalidOperationException>(() => controller.Play(briefing, null));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(idle);
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        /// <summary>
        /// Verifies skipping abandons a segment transition that is still loading.
        /// </summary>
        [Test]
        public async Task Skip_SegmentLoading_DoesNotStartAbandonedSegmentAsync()
        {
            GameRoot game = CreateGame();
            StrategyAdvisorTheme advisorTheme = CreateAdvisorTheme();
            StrategyBriefingTheme briefing = CreateBriefing();
            StrategyBriefingSegmentTheme firstSegment = CreateSegment("First");
            StrategyBriefingSegmentTheme secondSegment = CreateSegment("Second");
            secondSegment.Audio = "SecondVoice";
            briefing.Segments.Add(firstSegment);
            briefing.Segments.Add(secondSegment);
            Texture2D idle = new Texture2D(1, 1);
            Texture2D first = new Texture2D(1, 1);
            Texture2D second = new Texture2D(1, 1);
            Dictionary<string, Texture2D> textures = CreateTextures(advisorTheme, idle);
            textures[briefing.GetFramePath("First", 0)] = first;
            textures[briefing.GetFramePath("Second", 0)] = second;
            TaskCompletionSource<bool> segmentReady = new TaskCompletionSource<bool>();
            bool loadRequested = false;
            bool sfxPreloaded = false;
            GameObject rootObject = UIComponentTestHelper.InstantiatePrefab(_prefabPath);
            try
            {
                StrategyBriefingController controller = CreateController(
                    game,
                    advisorTheme,
                    textures,
                    rootObject,
                    _ =>
                    {
                        loadRequested = true;
                        return segmentReady.Task;
                    },
                    _ => sfxPreloaded = true
                );
                StrategyAdvisorView advisorView =
                    rootObject.GetComponentInChildren<StrategyAdvisorView>();
                bool? skipped = null;
                controller.Play(briefing, wasSkipped => skipped = wasSkipped);
                advisorView.AdvanceAnimation(0.5f);
                await WaitUntilAsync(() => loadRequested);

                controller.Skip();
                segmentReady.SetResult(true);
                await Task.Yield();

                Assert.IsTrue(skipped);
                Assert.AreNotSame(second, GetProtocolImage(rootObject).texture);
                Assert.IsFalse(sfxPreloaded);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(second);
                UnityEngine.Object.DestroyImmediate(first);
                UnityEngine.Object.DestroyImmediate(idle);
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        public void Skip_ConfiguredResponse_CompletesImmediatelyAndPlaysResponse()
        {
            GameRoot game = CreateGame();
            StrategyAdvisorTheme advisorTheme = CreateAdvisorTheme();
            StrategyBriefingTheme briefing = CreateBriefing();
            briefing.Segments.Add(CreateSegment("First"));
            briefing.Skip = CreateSegment("Skip");
            Texture2D idle = new Texture2D(1, 1);
            Texture2D first = new Texture2D(1, 1);
            Texture2D skip = new Texture2D(1, 1);
            Dictionary<string, Texture2D> textures = CreateTextures(advisorTheme, idle);
            textures[briefing.GetFramePath("First", 0)] = first;
            textures[briefing.GetFramePath("Skip", 0)] = skip;
            GameObject rootObject = UIComponentTestHelper.InstantiatePrefab(_prefabPath);
            try
            {
                StrategyAdvisorView advisorView =
                    rootObject.GetComponentInChildren<StrategyAdvisorView>(true);
                StrategyBriefingController controller = CreateController(
                    game,
                    advisorTheme,
                    textures,
                    rootObject
                );
                bool? skipped = null;
                controller.Play(briefing, wasSkipped => skipped = wasSkipped);

                controller.Skip();

                Assert.AreSame(skip, GetProtocolImage(rootObject).texture);
                Assert.IsTrue(skipped);
                advisorView.AdvanceAnimation(0.5f);
                Assert.IsTrue(skipped);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(skip);
                UnityEngine.Object.DestroyImmediate(first);
                UnityEngine.Object.DestroyImmediate(idle);
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        public void Skip_NoActiveBriefing_DoesNotThrow()
        {
            GameRoot game = CreateGame();
            StrategyAdvisorTheme advisorTheme = CreateAdvisorTheme();
            Texture2D idle = new Texture2D(1, 1);
            GameObject rootObject = UIComponentTestHelper.InstantiatePrefab(_prefabPath);
            try
            {
                StrategyBriefingController controller = CreateController(
                    game,
                    advisorTheme,
                    CreateTextures(advisorTheme, idle),
                    rootObject
                );

                Assert.DoesNotThrow(controller.Skip);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(idle);
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        public void PauseAndResume_NoActiveBriefing_DoNotThrow()
        {
            GameRoot game = CreateGame();
            StrategyAdvisorTheme advisorTheme = CreateAdvisorTheme();
            Texture2D idle = new Texture2D(1, 1);
            GameObject rootObject = UIComponentTestHelper.InstantiatePrefab(_prefabPath);
            try
            {
                StrategyBriefingController controller = CreateController(
                    game,
                    advisorTheme,
                    CreateTextures(advisorTheme, idle),
                    rootObject
                );

                Assert.DoesNotThrow(controller.Pause);
                Assert.DoesNotThrow(controller.Resume);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(idle);
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        public void CreateMapPresentation_Target_ResolvesPlanetAndSector()
        {
            GameRoot game = CreateGame();
            GalaxyPlanetSector planetSector = new GalaxyPlanetSector { InstanceID = "SECTOR" };
            Planet planet = new Planet { InstanceID = "PLANET" };
            game.AttachNode(planetSector, game.Galaxy);
            game.AttachNode(planet, planetSector);
            StrategyBriefingSegmentTheme segment = new StrategyBriefingSegmentTheme
            {
                Focus = StrategyBriefingFocus.Target,
                TargetInstanceID = planet.InstanceID,
            };

            StrategyBriefingMapPresentation presentation =
                StrategyBriefingController.CreateMapPresentation(game, segment);

            Assert.AreEqual(StrategyBriefingMapMode.Spotlight, presentation.Mode);
            Assert.AreEqual(planetSector.InstanceID, presentation.TargetSectorInstanceID);
            Assert.AreEqual(planet.InstanceID, presentation.TargetPlanetInstanceID);
            Assert.IsTrue(presentation.DimBackground);
        }

        [Test]
        public void CreateMapPresentation_MissingTarget_ThrowsInvalidOperationException()
        {
            GameRoot game = CreateGame();
            StrategyBriefingSegmentTheme segment = new StrategyBriefingSegmentTheme
            {
                Focus = StrategyBriefingFocus.Target,
                TargetInstanceID = "MISSING",
            };

            Assert.Throws<InvalidOperationException>(() =>
                StrategyBriefingController.CreateMapPresentation(game, segment)
            );
        }

        [Test]
        public void CreateMapPresentation_NullGame_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() =>
                StrategyBriefingController.CreateMapPresentation(null, CreateSegment("First"))
            );
        }

        [Test]
        public void CreateMapPresentation_NullSegment_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() =>
                StrategyBriefingController.CreateMapPresentation(CreateGame(), null)
            );
        }

        [TestCase(StrategyBriefingFocus.PlayerHeadquarters)]
        [TestCase(StrategyBriefingFocus.OpponentHeadquarters)]
        public void CreateMapPresentation_HeadquartersWithoutTarget_ThrowsInvalidOperationException(
            StrategyBriefingFocus focus
        )
        {
            StrategyBriefingSegmentTheme segment = new StrategyBriefingSegmentTheme
            {
                Focus = focus,
            };

            Assert.Throws<InvalidOperationException>(() =>
                StrategyBriefingController.CreateMapPresentation(CreateGame(), segment)
            );
        }

        [Test]
        public void CreateMapPresentation_TargetOutsidePlanetSector_ThrowsInvalidOperationException()
        {
            GameRoot game = CreateGame();
            Officer target = new Officer { InstanceID = "OFFICER" };
            game.AttachNode(target, game.Galaxy);
            StrategyBriefingSegmentTheme segment = new StrategyBriefingSegmentTheme
            {
                Focus = StrategyBriefingFocus.Target,
                TargetInstanceID = target.InstanceID,
            };

            Assert.Throws<InvalidOperationException>(() =>
                StrategyBriefingController.CreateMapPresentation(game, segment)
            );
        }

        private static StrategyBriefingController CreateController(
            GameRoot game,
            StrategyAdvisorTheme advisorTheme,
            IReadOnlyDictionary<string, Texture2D> textures,
            GameObject rootObject,
            Func<ContentPreloadManifest, Task> preloadContent = null,
            Action<IEnumerable<string>> preloadSfx = null
        )
        {
            FactionTheme factionTheme = new FactionTheme { StrategyAdvisor = advisorTheme };
            StrategyHudController hudController = new StrategyHudController(
                game.GetPlayerFaction,
                () => factionTheme,
                path => textures.TryGetValue(path, out Texture2D texture) ? texture : null,
                _ => { }
            );
            hudController.Initialize(new TestHudActions());
            StrategyHudView hudView = rootObject.GetComponentInChildren<StrategyHudView>(true);
            hudController.BindView(hudView);
            hudController.Render(new StrategyHudRenderData("", "", "", "", TickSpeed.Paused, null));
            GalaxyMapController mapController = new GalaxyMapController(() => null);
            mapController.Initialize(new TestGalaxyMapActions());
            return new StrategyBriefingController(
                game,
                path => textures.TryGetValue(path, out Texture2D texture) ? texture : null,
                _ => 0f,
                preloadContent ?? (_ => Task.CompletedTask),
                preloadSfx ?? (_ => { }),
                hudController,
                mapController,
                () => { }
            );
        }

        private static GameRoot CreateGame()
        {
            GameRoot game = new GameRoot(TestConfig.Create());
            game.GetFactions().Add(new Faction { InstanceID = _playerFactionID });
            game.GetFactions().Add(new Faction { InstanceID = _opponentFactionID });
            game.Summary.PlayerFactionID = _playerFactionID;
            return game;
        }

        private static StrategyAdvisorTheme CreateAdvisorTheme()
        {
            return new StrategyAdvisorTheme
            {
                AnimationImageRoot = "Pack/Test/Advisor",
                ProtocolIdleAnimation = "Idle",
                DroidIdleAnimation = "Standard",
                FrameIntervalSeconds = 0.5f,
            };
        }

        private static StrategyBriefingTheme CreateBriefing()
        {
            return new StrategyBriefingTheme
            {
                AnimationImageRoot = "Pack/Test/Briefing/Animations",
                AudioRoot = "Pack/Test/Briefing/Audio",
            };
        }

        private static StrategyBriefingSegmentTheme CreateSegment(string animation)
        {
            return new StrategyBriefingSegmentTheme { Animation = animation, FrameCount = 1 };
        }

        private static Dictionary<string, Texture2D> CreateTextures(
            StrategyAdvisorTheme theme,
            Texture2D idle
        )
        {
            return new Dictionary<string, Texture2D>
            {
                [theme.GetFramePath(theme.ProtocolIdleAnimation, 0, false)] = idle,
                [theme.GetFramePath(theme.DroidIdleAnimation, 0, true)] = idle,
            };
        }

        private static UnityEngine.UI.RawImage GetProtocolImage(GameObject rootObject)
        {
            return Array.Find(
                rootObject.GetComponentsInChildren<UnityEngine.UI.RawImage>(true),
                image => image.name == "ProtocolImage"
            );
        }

        /// <summary>
        /// Advances asynchronous test continuations until a condition is satisfied.
        /// </summary>
        /// <param name="condition">The condition expected to become true.</param>
        /// <returns>A task that completes when the condition is satisfied.</returns>
        private static async Task WaitUntilAsync(Func<bool> condition)
        {
            for (int attempt = 0; attempt < 20; attempt++)
            {
                if (condition())
                    return;
                await Task.Yield();
            }

            Assert.Fail("The asynchronous briefing condition was not satisfied.");
        }

        private sealed class TestGalaxyMapActions : IGalaxyMapActions
        {
            public void OpenPlanetSectorWindow(
                GalaxyPlanetSector planetSector,
                int sourceX,
                int sourceY
            ) { }

            public void RequestGalaxyMapRender() { }
        }

        private sealed class TestHudActions : IStrategyHudActions
        {
            public void BeginAdvisorConstruction(
                ManufacturingType manufacturingType,
                int sourceX,
                int sourceY
            ) { }

            public void OpenAdvisorCommandContextMenu(
                ContextMenuRequest request,
                int sourceX,
                int sourceY
            ) { }

            public void OpenAdvisorNotificationContextMenu(
                ContextMenuRequest request,
                int sourceX,
                int sourceY
            ) { }

            public void OpenAdvisorReport(AdvisorReportMode mode) { }

            public void OpenMessagesTab(MessagesTab tab) { }

            public void ProcessAdvisorAutomation(Faction faction) { }

            public void OpenSpeedContextMenu(
                ContextMenuRequest request,
                int sourceX,
                int sourceY
            ) { }

            public void ReleaseHudButton(StrategyHudAction action, int sourceX, int sourceY) { }

            public void RequestHudRender() { }

            public void SetGameSpeed(TickSpeed speed) { }
        }
    }
}
