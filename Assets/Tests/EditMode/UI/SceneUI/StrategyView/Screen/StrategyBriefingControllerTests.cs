using System;
using System.Collections.Generic;
using NUnit.Framework;
using Rebellion.Game;
using Rebellion.Game.Factions;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Messages;
using Rebellion.Game.Units;
using UnityEngine;
using GamePlanetSystem = Rebellion.Game.Galaxy.PlanetSystem;

namespace Rebellion.Tests.UI.SceneUI.StrategyView.Screen
{
    [TestFixture]
    public class StrategyBriefingControllerTests
    {
        private const string _opponentFactionID = "OPPONENT";
        private const string _playerFactionID = "PLAYER";
        private const string _prefabPath = "Assets/Prefabs/UI/StrategyView/StrategyViewRoot.prefab";

        [Test]
        public void Play_MultipleSegments_PlaysInOrderAndCompletes()
        {
            GameRoot game = CreateGame();
            StrategyAdvisorTheme advisorTheme = CreateAdvisorTheme();
            StrategyBriefingTheme briefing = CreateBriefing();
            briefing.Segments.Add(CreateSegment("First"));
            briefing.Segments.Add(CreateSegment("Second"));
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
                Assert.AreSame(second, GetProtocolImage(rootObject).texture);
                Assert.IsNull(skipped);
                advisorView.AdvanceAnimation(0.5f);
                Assert.IsFalse(skipped);
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
        public void CreateMapPresentation_Target_ResolvesPlanetAndSystem()
        {
            GameRoot game = CreateGame();
            GamePlanetSystem system = new GamePlanetSystem { InstanceID = "SYSTEM" };
            Planet planet = new Planet { InstanceID = "PLANET" };
            game.AttachNode(system, game.Galaxy);
            game.AttachNode(planet, system);
            StrategyBriefingSegmentTheme segment = new StrategyBriefingSegmentTheme
            {
                Focus = StrategyBriefingFocus.Target,
                TargetInstanceID = planet.InstanceID,
            };

            StrategyBriefingMapPresentation presentation =
                StrategyBriefingController.CreateMapPresentation(game, segment);

            Assert.AreEqual(StrategyBriefingMapMode.Spotlight, presentation.Mode);
            Assert.AreEqual(system.InstanceID, presentation.TargetSystemInstanceID);
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
        public void CreateMapPresentation_TargetOutsidePlanetSystem_ThrowsInvalidOperationException()
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
            GameObject rootObject
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
                hudController,
                mapController,
                () => { }
            );
        }

        private static GameRoot CreateGame()
        {
            GameRoot game = new GameRoot(TestConfig.Create());
            game.Factions.Add(new Faction { InstanceID = _playerFactionID });
            game.Factions.Add(new Faction { InstanceID = _opponentFactionID });
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

        private sealed class TestGalaxyMapActions : IGalaxyMapActions
        {
            public void OpenPlanetSystemWindow(
                GamePlanetSystem system,
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
