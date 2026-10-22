using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using NUnit.Framework;
using Rebellion.Game;
using Rebellion.Game.Encyclopedia;
using Rebellion.Game.Factions;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Messages;
using Rebellion.Game.Units;
using UnityEngine;
using GalaxyMapModel = Rebellion.Game.Galaxy.GalaxyMap;
using GalaxyPlanetSector = Rebellion.Game.Galaxy.PlanetSector;

namespace Rebellion.Tests.UI.SceneUI.StrategyView.Screen
{
    [TestFixture]
    public class StrategyBriefingControllerTests
    {
        private const string _opponentFactionID = "OPPONENT";
        private const string _playerFactionID = "PLAYER";
        private const string _prefabPath = "Assets/Prefabs/UI/StrategyView/StrategyViewRoot.prefab";

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
                StrategyBriefingController.CreateMapPresentation(
                    CreateFactionView(game),
                    _playerFactionID,
                    _opponentFactionID,
                    segment
                );

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
                StrategyBriefingController.CreateMapPresentation(
                    CreateFactionView(game),
                    _playerFactionID,
                    _opponentFactionID,
                    segment
                )
            );
        }

        [Test]
        public void CreateMapPresentation_NullFactionViewWithoutTarget_ReturnsDefaultPresentation()
        {
            StrategyBriefingMapPresentation presentation =
                StrategyBriefingController.CreateMapPresentation(
                    null,
                    _playerFactionID,
                    _opponentFactionID,
                    CreateSegment("First")
                );

            Assert.AreEqual(StrategyBriefingMapMode.Default, presentation.Mode);
            Assert.IsFalse(presentation.DimBackground);
        }

        [Test]
        public void CreateMapPresentation_NullSegment_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() =>
                StrategyBriefingController.CreateMapPresentation(
                    CreateFactionView(CreateGame()),
                    _playerFactionID,
                    _opponentFactionID,
                    null
                )
            );
        }

        [Test]
        public void CreateMapPresentation_PlayerHeadquartersWithoutTarget_ThrowsInvalidOperationException()
        {
            StrategyBriefingSegmentTheme segment = new StrategyBriefingSegmentTheme
            {
                Focus = StrategyBriefingFocus.PlayerHeadquarters,
            };

            Assert.Throws<InvalidOperationException>(() =>
                StrategyBriefingController.CreateMapPresentation(
                    CreateFactionView(CreateGame()),
                    _playerFactionID,
                    _opponentFactionID,
                    segment
                )
            );
        }

        [Test]
        public void CreateMapPresentation_OpponentHeadquartersWithoutTarget_DoesNotRevealLocation()
        {
            StrategyBriefingSegmentTheme segment = new StrategyBriefingSegmentTheme
            {
                Focus = StrategyBriefingFocus.OpponentHeadquarters,
            };

            StrategyBriefingMapPresentation presentation =
                StrategyBriefingController.CreateMapPresentation(
                    CreateFactionView(CreateGame()),
                    _playerFactionID,
                    _opponentFactionID,
                    segment
                );

            Assert.IsNull(presentation.TargetSectorInstanceID);
            Assert.IsNull(presentation.TargetPlanetInstanceID);
        }

        [Test]
        public void CreateMapPresentation_UnknownOpponentHeadquarters_DoesNotRevealLocation()
        {
            GameRoot game = CreateGame();
            Faction opponent = game.GetFactions()
                .Single(faction => faction.InstanceID == _opponentFactionID);
            opponent.HQInstanceID = "REBEL_HQ";
            GalaxyPlanetSector sector = new GalaxyPlanetSector { InstanceID = "SECTOR" };
            Planet headquarters = new Planet
            {
                InstanceID = opponent.HQInstanceID,
                IsHeadquarters = true,
            };
            game.AttachNode(sector, game.Galaxy);
            game.AttachNode(headquarters, sector);

            GalaxyMapModel factionView = new GalaxyMapModel();
            GalaxyPlanetSector viewSector = (GalaxyPlanetSector)sector.CreateCopy();
            Planet unexploredHeadquarters = new Planet
            {
                InstanceID = headquarters.InstanceID,
                IsUnexploredView = true,
            };
            viewSector.SetParent(factionView);
            factionView.AddChild(viewSector);
            unexploredHeadquarters.SetParent(viewSector);
            viewSector.AddChild(unexploredHeadquarters);
            StrategyBriefingSegmentTheme segment = new StrategyBriefingSegmentTheme
            {
                Focus = StrategyBriefingFocus.OpponentHeadquarters,
            };

            StrategyBriefingMapPresentation presentation =
                StrategyBriefingController.CreateMapPresentation(
                    factionView,
                    _playerFactionID,
                    _opponentFactionID,
                    segment
                );

            Assert.IsNull(presentation.TargetSectorInstanceID);
            Assert.IsNull(presentation.TargetPlanetInstanceID);
        }

        [Test]
        public void CreateMapPresentation_KnownOpponentHeadquarters_UsesFactionViewLocation()
        {
            GameRoot game = CreateGame();
            Faction opponent = game.GetFactions()
                .Single(faction => faction.InstanceID == _opponentFactionID);
            opponent.HQInstanceID = "REBEL_HQ";
            GalaxyPlanetSector sector = new GalaxyPlanetSector { InstanceID = "SECTOR" };
            Planet headquarters = new Planet
            {
                InstanceID = opponent.HQInstanceID,
                IsHeadquarters = true,
                OwnerInstanceID = _opponentFactionID,
            };
            game.AttachNode(sector, game.Galaxy);
            game.AttachNode(headquarters, sector);

            GalaxyMapModel factionView = new GalaxyMapModel();
            GalaxyPlanetSector viewSector = (GalaxyPlanetSector)sector.CreateCopy();
            Planet knownHeadquarters = (Planet)headquarters.CreateCopy();
            viewSector.SetParent(factionView);
            factionView.AddChild(viewSector);
            knownHeadquarters.SetParent(viewSector);
            viewSector.AddChild(knownHeadquarters);
            StrategyBriefingSegmentTheme segment = new StrategyBriefingSegmentTheme
            {
                Focus = StrategyBriefingFocus.OpponentHeadquarters,
            };

            StrategyBriefingMapPresentation presentation =
                StrategyBriefingController.CreateMapPresentation(
                    factionView,
                    _playerFactionID,
                    _opponentFactionID,
                    segment
                );

            Assert.AreEqual(sector.InstanceID, presentation.TargetSectorInstanceID);
            Assert.AreEqual(headquarters.InstanceID, presentation.TargetPlanetInstanceID);
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
                StrategyBriefingController.CreateMapPresentation(
                    CreateFactionView(game),
                    _playerFactionID,
                    _opponentFactionID,
                    segment
                )
            );
        }

        /// <summary>
        /// Creates controller.
        /// </summary>
        /// <param name="game">The game.</param>
        /// <param name="advisorTheme">The advisor theme.</param>
        /// <param name="textures">The textures.</param>
        /// <param name="rootObject">The root object.</param>
        /// <param name="preloadContent">The preload content.</param>
        /// <param name="preloadSfx">The preload sfx.</param>
        /// <returns>The created controller.</returns>
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
            hudController.Render(new StrategyHudRenderData("", "", "", TickSpeed.Paused, null));
            GalaxyMapController mapController = new GalaxyMapController(() => null);
            mapController.Initialize(new TestGalaxyMapActions());
            return new StrategyBriefingController(
                () =>
                    TestContent.CreateUIContext(
                        game,
                        TestContent.CreateThemeLibrary(),
                        new EncyclopediaCatalog(Array.Empty<EncyclopediaEntry>())
                    ),
                path => textures.TryGetValue(path, out Texture2D texture) ? texture : null,
                _ => 0f,
                preloadContent ?? (_ => Task.CompletedTask),
                preloadSfx ?? (_ => { }),
                hudController,
                mapController,
                () => { }
            );
        }

        /// <summary>
        /// Creates game.
        /// </summary>
        /// <returns>The created game.</returns>
        private static GameRoot CreateGame()
        {
            GameRoot game = TestGame.Create(TestConfig.Create());
            game.GetFactions().Add(new Faction { InstanceID = _playerFactionID });
            game.GetFactions().Add(new Faction { InstanceID = _opponentFactionID });
            game.Summary.PlayerFactionID = _playerFactionID;
            game.SetFactionController(_playerFactionID, "PLAYER1", PlayerControllerType.Human);
            return game;
        }

        /// <summary>
        /// Returns the test galaxy as the explicitly supplied visible view.
        /// </summary>
        /// <param name="game">The game supplying the test galaxy.</param>
        /// <returns>The visible galaxy used by the projection.</returns>
        private static GalaxyMapModel CreateFactionView(GameRoot game)
        {
            return game?.Galaxy;
        }

        /// <summary>
        /// Creates advisor theme.
        /// </summary>
        /// <returns>The created advisor theme.</returns>
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

        /// <summary>
        /// Creates briefing.
        /// </summary>
        /// <returns>The created briefing.</returns>
        private static StrategyBriefingTheme CreateBriefing()
        {
            return new StrategyBriefingTheme
            {
                AnimationImageRoot = "Pack/Test/Briefing/Animations",
                AudioRoot = "Pack/Test/Briefing/Audio",
            };
        }

        /// <summary>
        /// Creates segment.
        /// </summary>
        /// <param name="animation">The animation.</param>
        /// <returns>The created segment.</returns>
        private static StrategyBriefingSegmentTheme CreateSegment(string animation)
        {
            return new StrategyBriefingSegmentTheme { Animation = animation, FrameCount = 1 };
        }

        /// <summary>
        /// Creates textures.
        /// </summary>
        /// <param name="theme">The theme.</param>
        /// <param name="idle">The idle.</param>
        /// <returns>The created textures.</returns>
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

        /// <summary>
        /// Gets protocol image.
        /// </summary>
        /// <param name="rootObject">The root object.</param>
        /// <returns>The requested protocol image.</returns>
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
            /// <summary>
            /// Opens planet sector window.
            /// </summary>
            /// <param name="planetSector">The planet sector.</param>
            /// <param name="sourceX">The source x.</param>
            /// <param name="sourceY">The source y.</param>
            public void OpenPlanetSectorWindow(
                GalaxyPlanetSector planetSector,
                int sourceX,
                int sourceY
            ) { }

            /// <summary>
            /// Executes request galaxy map render.
            /// </summary>
            public void RequestGalaxyMapRender() { }
        }

        private sealed class TestHudActions : IStrategyHudActions
        {
            /// <summary>
            /// Executes begin advisor construction.
            /// </summary>
            /// <param name="manufacturingType">The manufacturing type.</param>
            /// <param name="sourceX">The source x.</param>
            /// <param name="sourceY">The source y.</param>
            public void BeginAdvisorConstruction(
                ManufacturingType manufacturingType,
                int sourceX,
                int sourceY
            ) { }

            /// <summary>
            /// Opens advisor command context menu.
            /// </summary>
            /// <param name="request">The request.</param>
            /// <param name="sourceX">The source x.</param>
            /// <param name="sourceY">The source y.</param>
            public void OpenAdvisorCommandContextMenu(
                ContextMenuRequest request,
                int sourceX,
                int sourceY
            ) { }

            /// <summary>
            /// Opens advisor notification context menu.
            /// </summary>
            /// <param name="request">The request.</param>
            /// <param name="sourceX">The source x.</param>
            /// <param name="sourceY">The source y.</param>
            public void OpenAdvisorNotificationContextMenu(
                ContextMenuRequest request,
                int sourceX,
                int sourceY
            ) { }

            /// <summary>
            /// Opens advisor report.
            /// </summary>
            /// <param name="mode">The mode.</param>
            public void OpenAdvisorReport(AdvisorReportMode mode) { }

            /// <summary>
            /// Opens messages tab.
            /// </summary>
            /// <param name="tab">The tab.</param>
            public void OpenMessagesTab(MessagesTab tab) { }

            /// <summary>
            /// Processes advisor automation.
            /// </summary>
            /// <param name="faction">The faction.</param>
            public void ProcessAdvisorAutomation(Faction faction) { }

            /// <summary>
            /// Opens speed context menu.
            /// </summary>
            /// <param name="request">The request.</param>
            /// <param name="sourceX">The source x.</param>
            /// <param name="sourceY">The source y.</param>
            public void OpenSpeedContextMenu(
                ContextMenuRequest request,
                int sourceX,
                int sourceY
            ) { }

            /// <summary>
            /// Executes release hud button.
            /// </summary>
            /// <param name="action">The action.</param>
            /// <param name="sourceX">The source x.</param>
            /// <param name="sourceY">The source y.</param>
            public void ReleaseHudButton(StrategyHudAction action, int sourceX, int sourceY) { }

            /// <summary>
            /// Executes request hud render.
            /// </summary>
            public void RequestHudRender() { }

            /// <summary>
            /// Sets game speed.
            /// </summary>
            /// <param name="speed">The speed.</param>
            public void SetGameSpeed(TickSpeed speed) { }
        }
    }
}
