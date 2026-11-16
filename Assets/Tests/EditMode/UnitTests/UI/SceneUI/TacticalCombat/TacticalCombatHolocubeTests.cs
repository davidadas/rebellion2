using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;

namespace Rebellion.Tests.UI.SceneUI.TacticalCombat
{
    [TestFixture]
    public sealed class TacticalCombatHolocubeTests
    {
        private GameObject _parent;
        private TacticalCombatHolocube _cube;

        /// <summary>
        /// Creates an isolated play-mode battle frame for the boundary geometry.
        /// </summary>
        /// <returns>The setup sequence.</returns>
        [UnitySetUp]
        public IEnumerator SetUp()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            yield return new EnterPlayMode();
            _parent = new GameObject("Battle frame");
            _parent.transform.SetPositionAndRotation(
                new Vector3(16000, 16000, 16000),
                Quaternion.Euler(0, 30, 0)
            );
        }

        /// <summary>
        /// Releases the owned geometry before leaving the isolated scene.
        /// </summary>
        /// <returns>The cleanup sequence.</returns>
        [UnityTearDown]
        public IEnumerator TearDown()
        {
            _cube?.Dispose();
            Object.Destroy(_parent);
            yield return null;
            _cube = null;
            _parent = null;
            yield return new ExitPlayMode();
        }

        [UnityTest]
        public IEnumerator Constructor_EncounterScale_AnchorsBoundaryPairToBattleFrame()
        {
            _cube = new TacticalCombatHolocube(
                _parent.transform,
                Shader.Find("Rebellion/Tactical fighter"),
                100,
                true
            );

            Transform root = _parent.transform.GetChild(0);
            Assert.AreEqual(Vector3.zero, root.localPosition);
            Assert.AreEqual(Quaternion.identity, root.localRotation);
            Assert.AreEqual(Vector3.one, root.localScale);
            Assert.AreEqual(_parent.transform.position, root.position);
            MeshFilter[] meshes = root.GetComponentsInChildren<MeshFilter>();
            Assert.AreEqual(2, meshes.Length);
            foreach (MeshFilter filter in meshes)
            {
                Assert.AreEqual(
                    new Bounds(Vector3.zero, Vector3.one * 100),
                    filter.sharedMesh.bounds
                );
                Assert.AreEqual(8, filter.sharedMesh.vertexCount);
                Assert.AreEqual(PresentationLayers.TacticalCombat, filter.gameObject.layer);
                MeshRenderer renderer = filter.GetComponent<MeshRenderer>();
                Assert.IsTrue(renderer.enabled);
                Assert.AreEqual(Color.blue, renderer.sharedMaterial.color);
                Assert.AreSame(Texture2D.whiteTexture, renderer.sharedMaterial.mainTexture);
                Assert.AreEqual("Rebellion/Tactical fighter", renderer.sharedMaterial.shader.name);
                Assert.IsFalse(renderer.receiveShadows);
                Assert.AreEqual(
                    UnityEngine.Rendering.ShadowCastingMode.Off,
                    renderer.shadowCastingMode
                );
            }
            yield return null;
        }

        [UnityTest]
        public IEnumerator Constructor_PolygonFaces_RetainsTwelveEdgesWithoutDiagonals()
        {
            _cube = new TacticalCombatHolocube(
                _parent.transform,
                Shader.Find("Rebellion/Tactical fighter"),
                100,
                true
            );

            Mesh[] meshes = _parent
                .GetComponentsInChildren<MeshFilter>()
                .Select(filter => filter.sharedMesh)
                .ToArray();
            HashSet<(int, int)> firstEdges = null;
            foreach (Mesh mesh in meshes)
            {
                Assert.AreEqual(MeshTopology.Lines, mesh.GetTopology(0));
                int[] indices = mesh.GetIndices(0);
                Assert.AreEqual(24, indices.Length);
                HashSet<(int, int)> edges = new();
                Vector3[] vertices = mesh.vertices;
                for (int index = 0; index < indices.Length; index += 2)
                {
                    int a = indices[index];
                    int b = indices[index + 1];
                    Assert.IsTrue(edges.Add((Mathf.Min(a, b), Mathf.Max(a, b))));
                    Vector3 difference = vertices[b] - vertices[a];
                    Assert.AreEqual(100, difference.magnitude);
                    Assert.AreEqual(
                        1,
                        (difference.x != 0 ? 1 : 0)
                            + (difference.y != 0 ? 1 : 0)
                            + (difference.z != 0 ? 1 : 0)
                    );
                }
                if (firstEdges == null)
                    firstEdges = edges;
                else
                    CollectionAssert.AreEquivalent(firstEdges, edges);
            }
            yield return null;
        }

        [UnityTest]
        public IEnumerator Constructor_DisabledOption_RetainsUnattachedBoundaryResources()
        {
            _cube = new TacticalCombatHolocube(
                _parent.transform,
                Shader.Find("Rebellion/Tactical fighter"),
                100,
                false
            );

            MeshRenderer[] renderers = _parent.GetComponentsInChildren<MeshRenderer>(true);
            Assert.AreEqual(2, renderers.Length);
            Assert.IsTrue(renderers.All(renderer => !renderer.enabled));
            Assert.IsTrue(
                _parent
                    .GetComponentsInChildren<MeshFilter>(true)
                    .All(filter => filter.sharedMesh != null)
            );
            yield return null;
        }

        [UnityTest]
        public IEnumerator Dispose_OwnedBoundary_ReleasesGeometryAndMaterialWithoutDestroyingParent()
        {
            _cube = new TacticalCombatHolocube(
                _parent.transform,
                Shader.Find("Rebellion/Tactical fighter"),
                100,
                true
            );
            Mesh[] meshes = _parent
                .GetComponentsInChildren<MeshFilter>()
                .Select(filter => filter.sharedMesh)
                .ToArray();
            Material material = _parent.GetComponentInChildren<MeshRenderer>().sharedMaterial;

            _cube.Dispose();
            _cube = null;
            yield return null;

            Assert.IsTrue(meshes.All(mesh => mesh == null));
            Assert.IsTrue(material == null);
            Assert.IsTrue(_parent != null);
            Assert.AreEqual(0, _parent.transform.childCount);
        }
    }
}
