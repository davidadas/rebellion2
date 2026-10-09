using System;
using NUnit.Framework;
using Rebellion.Game.Combat;
using UnityEditor;
using UnityEngine;

namespace Rebellion.Tests.UI.SceneUI.TacticalView
{
    [TestFixture]
    public sealed class TacticalViewControllerTests
    {
        private const string _prefabPath = "Assets/Prefabs/UI/TacticalView/TacticalViewRoot.prefab";

        private GameObject instance;
        private TacticalViewController controller;

        /// <summary>
        /// Loads an isolated generated tactical root for each test.
        /// </summary>
        [SetUp]
        public void SetUp()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(_prefabPath);
            Assert.IsNotNull(prefab, $"Missing generated tactical prefab: {_prefabPath}");
            instance = UnityEngine.Object.Instantiate(prefab);
            controller = instance.GetComponent<TacticalViewController>();
        }

        /// <summary>
        /// Releases the generated tactical root after each test.
        /// </summary>
        [TearDown]
        public void TearDown()
        {
            UnityEngine.Object.DestroyImmediate(instance);
        }

        [Test]
        public void GeneratedPrefab_RequiredSceneReferences_AreAssigned()
        {
            Assert.IsNotNull(controller);
            Assert.IsNotNull(controller.BattleCamera);
            Assert.IsNotNull(controller.BattleRoot);
        }

        [Test]
        public void Initialize_MatchingBattleAndMap_BindsPresentationState()
        {
            ActiveBattle battle = new ActiveBattle
            {
                BattleMapInstanceID = "SPACE_MAP",
                Kind = BattleKind.Space,
            };
            BattleMap map = new BattleMap { InstanceID = "SPACE_MAP", Kind = BattleKind.Space };

            controller.Initialize(battle, map);

            Assert.AreSame(battle, controller.ActiveBattle);
            Assert.AreSame(map, controller.BattleMap);
        }

        [Test]
        public void Initialize_MismatchedMapIdentifier_ThrowsArgumentException()
        {
            ActiveBattle battle = new ActiveBattle
            {
                BattleMapInstanceID = "SPACE_MAP",
                Kind = BattleKind.Space,
            };
            BattleMap map = new BattleMap { InstanceID = "OTHER_MAP", Kind = BattleKind.Space };

            Assert.Throws<ArgumentException>(() => controller.Initialize(battle, map));
        }

        [Test]
        public void Initialize_MismatchedBattleKind_ThrowsArgumentException()
        {
            ActiveBattle battle = new ActiveBattle
            {
                BattleMapInstanceID = "MAP",
                Kind = BattleKind.Space,
            };
            BattleMap map = new BattleMap { InstanceID = "MAP", Kind = BattleKind.Ground };

            Assert.Throws<ArgumentException>(() => controller.Initialize(battle, map));
        }
    }
}
