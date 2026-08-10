using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

namespace Rebellion.Tests.UI.SceneUI.TacticalBattle
{
    [TestFixture]
    public class TacticalCameraRigTests
    {
        private GameObject root;
        private Camera battleCamera;
        private TacticalCameraRig rig;
        private Button[] controls;

        [SetUp]
        public void SetUp()
        {
            root = new GameObject("TacticalCameraRigTests");
            root.SetActive(false);
            battleCamera = root.AddComponent<Camera>();
            rig = root.AddComponent<TacticalCameraRig>();
            controls = CreateButtons(9);
            rig.Configure(battleCamera, controls);
            UIComponentTestHelper.InvokeLifecycle(rig, "Awake");
            rig.Initialize(150f);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(root);
        }

        [Test]
        public void Initialize_FactionYaw_AppliesDefaultOrbit()
        {
            Assert.That(battleCamera.transform.eulerAngles.x, Is.EqualTo(30f).Within(0.01f));
            Assert.That(battleCamera.transform.eulerAngles.y, Is.EqualTo(150f).Within(0.01f));
            Assert.That(battleCamera.transform.position.magnitude, Is.EqualTo(240f).Within(0.01f));
        }

        [Test]
        public void CameraControls_ZoomAndRotate_UseZoomDerivedAdjustmentStep()
        {
            controls[0].onClick.Invoke();
            controls[2].onClick.Invoke();

            Assert.That(battleCamera.transform.position.magnitude, Is.EqualTo(192f).Within(0.01f));
            Assert.That(battleCamera.transform.eulerAngles.y, Is.EqualTo(148f).Within(0.01f));
        }

        [Test]
        public void ResetView_RememberedView_RestoresCompleteCameraState()
        {
            controls[3].onClick.Invoke();
            controls[4].onClick.Invoke();
            controls[6].onClick.Invoke();
            Quaternion rememberedRotation = battleCamera.transform.rotation;

            controls[3].onClick.Invoke();
            controls[5].onClick.Invoke();
            controls[7].onClick.Invoke();

            Assert.That(
                Quaternion.Angle(battleCamera.transform.rotation, rememberedRotation),
                Is.LessThan(0.01f)
            );
        }

        [Test]
        public void ResetSubject_SelectedSubject_ReCentersWithoutChangingOrientation()
        {
            controls[3].onClick.Invoke();
            Quaternion rotation = battleCamera.transform.rotation;
            Vector3 subject = new Vector3(20f, 5f, -10f);
            rig.SetSelectedSubject(subject);

            controls[8].onClick.Invoke();

            Assert.That(
                Quaternion.Angle(battleCamera.transform.rotation, rotation),
                Is.LessThan(0.01f)
            );
            Vector3 projectedSubject =
                battleCamera.transform.position + battleCamera.transform.forward * 240f;
            Assert.That(Vector3.Distance(projectedSubject, subject), Is.LessThan(0.01f));
        }

        [Test]
        public void Awake_IncorrectControlCount_ThrowsMissingReferenceException()
        {
            GameObject invalidRoot = new GameObject("InvalidTacticalCameraRig");
            invalidRoot.SetActive(false);
            Camera invalidCamera = invalidRoot.AddComponent<Camera>();
            TacticalCameraRig invalidRig = invalidRoot.AddComponent<TacticalCameraRig>();
            invalidRig.Configure(invalidCamera, CreateButtons(8));

            try
            {
                Assert.Throws<MissingReferenceException>(() =>
                    UIComponentTestHelper.InvokeLifecycle(invalidRig, "Awake")
                );
            }
            finally
            {
                Object.DestroyImmediate(invalidRoot);
            }
        }

        private Button[] CreateButtons(int count)
        {
            Button[] buttons = new Button[count];
            for (int index = 0; index < count; index++)
            {
                GameObject buttonObject = new GameObject($"CameraControl{index}");
                buttonObject.transform.SetParent(root.transform, false);
                buttons[index] = buttonObject.AddComponent<Button>();
            }

            return buttons;
        }
    }
}
