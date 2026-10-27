using System.Collections.Generic;
using NUnit.Framework;
using Rebellion.Game.Combat;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Rebellion.Tests.UI.SceneUI.TacticalView
{
    [TestFixture]
    public sealed class TacticalRadarViewTests
    {
        private GameObject _instance;
        private TacticalRadarView _radar;
        private TacticalViewCameraController _navigation;

        /// <summary>
        /// Creates a generated instrument bound to a configured battle camera.
        /// </summary>
        [SetUp]
        public void SetUp()
        {
            _instance = UnityEngine.Object.Instantiate(
                AssetDatabase.LoadAssetAtPath<GameObject>(
                    "Assets/Prefabs/UI/TacticalView/TacticalViewRoot.prefab"
                )
            );
            _instance.GetComponent<TacticalViewController>().enabled = false;
            _radar = _instance.GetComponentInChildren<TacticalRadarView>();
            UnityEngine.Camera camera = _instance
                .GetComponent<TacticalViewController>()
                .BattleCamera;
            _navigation = camera.GetComponent<TacticalViewCameraController>();
            camera.transform.SetPositionAndRotation(
                new Vector3(0f, 50f, -50f),
                Quaternion.Euler(45f, 0f, 0f)
            );
            BattleMapBounds bounds = new BattleMapBounds
            {
                MinimumX = -100f,
                MaximumX = 100f,
                MinimumY = -10f,
                MaximumY = 10f,
                MinimumZ = -100f,
                MaximumZ = 100f,
            };
            _navigation.Configure(
                bounds,
                new Bounds(Vector3.zero, new Vector3(200f, 20f, 200f)),
                Vector3.zero
            );
            _radar.Bind(
                new Dictionary<CombatUnit, Transform>(),
                bounds,
                camera,
                "VIEWER",
                new CombatUnit[0]
            );
            Canvas.ForceUpdateCanvases();
        }

        /// <summary>
        /// Releases the generated instrument.
        /// </summary>
        [TearDown]
        public void TearDown() => UnityEngine.Object.DestroyImmediate(_instance);

        /// <summary>
        /// Checks an empty minimap separates its housing from its playable area.
        /// </summary>
        [Test]
        public void Bind_EmptyBattle_DrawsHousingAndPlayableArea()
        {
            _radar.Rebuild(CanvasUpdate.PreRender);
            Canvas.ForceUpdateCanvases();
            Mesh mesh = _radar.canvasRenderer.GetMesh();
            try
            {
                Assert.IsNotNull(mesh);
                Assert.AreEqual(8, mesh.vertexCount);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(mesh);
            }
        }

        /// <summary>
        /// Checks radar input recenters navigation without changing the orbit distance.
        /// </summary>
        [Test]
        public void OnPointerDown_InsideRadar_RepositionsCameraPivot()
        {
            Rect r = _radar.rectTransform.rect;
            Vector3 local = new Vector3(
                Mathf.Lerp(r.xMin + 4f, r.xMax - 4f, 0.75f),
                Mathf.Lerp(r.yMin + 4f, r.yMax - 4f, 0.25f)
            );
            Vector2 screen = RectTransformUtility.WorldToScreenPoint(
                null,
                _radar.transform.TransformPoint(local)
            );
            float distance = _navigation.Distance;

            _radar.OnPointerDown(
                new PointerEventData(EventSystem.current)
                {
                    position = screen,
                    button = PointerEventData.InputButton.Left,
                }
            );

            Assert.That(_navigation.Pivot.x, Is.EqualTo(50f).Within(0.01f));
            Assert.That(_navigation.Pivot.z, Is.EqualTo(-50f).Within(0.01f));
            Assert.AreEqual(distance, _navigation.Distance);
        }

        /// <summary>
        /// Checks non-navigation pointer buttons cannot move the camera.
        /// </summary>
        [Test]
        public void OnPointerDown_RightButton_PreservesCameraPivot()
        {
            Vector3 previous = _navigation.Pivot;
            _radar.OnPointerDown(
                new PointerEventData(EventSystem.current)
                {
                    position = Vector2.zero,
                    button = PointerEventData.InputButton.Right,
                }
            );
            Assert.AreEqual(previous, _navigation.Pivot);
        }

        /// <summary>
        /// Checks unused radar space outside a narrow playable area cannot navigate the camera.
        /// </summary>
        [Test]
        public void OnPointerDown_OutsideAspectCorrectPlayableArea_PreservesCameraPivot()
        {
            BattleMapBounds bounds = new BattleMapBounds
            {
                MinimumX = -50f,
                MaximumX = 50f,
                MinimumY = -10f,
                MaximumY = 10f,
                MinimumZ = -100f,
                MaximumZ = 100f,
            };
            _radar.Bind(
                new Dictionary<CombatUnit, Transform>(),
                bounds,
                _navigation.GetComponent<UnityEngine.Camera>(),
                "VIEWER",
                new CombatUnit[0]
            );
            Rect r = _radar.rectTransform.rect;
            Vector2 screen = RectTransformUtility.WorldToScreenPoint(
                null,
                _radar.transform.TransformPoint(new Vector3(r.xMin + 5f, r.center.y))
            );
            Vector3 previous = _navigation.Pivot;

            _radar.OnPointerDown(
                new PointerEventData(EventSystem.current)
                {
                    position = screen,
                    button = PointerEventData.InputButton.Left,
                }
            );

            Assert.AreEqual(previous, _navigation.Pivot);
        }
    }
}
