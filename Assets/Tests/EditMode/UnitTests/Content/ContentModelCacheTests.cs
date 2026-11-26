using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;

namespace Rebellion.Tests.Content
{
    [TestFixture]
    public sealed class ContentModelCacheTests
    {
        private string root;

        /// <summary>
        /// Creates an isolated model content root.
        /// </summary>
        [SetUp]
        public void SetUp()
        {
            root = Path.Combine(
                Path.GetTempPath(),
                "rebellion2-model-cache-" + Guid.NewGuid().ToString("N")
            );
            Directory.CreateDirectory(root);
        }

        /// <summary>
        /// Removes the model files created by each test.
        /// </summary>
        [TearDown]
        public void TearDown() => Directory.Delete(root, true);

        [TestCase(false)]
        [TestCase(true)]
        public async Task InstantiateAsync_OpaqueModel_EnablesMotionVectorsAsync(
            bool pipelineInitialized
        )
        {
            WriteTriangle();
            using ContentAssets assets = new ContentAssets(new ContentFileResolver(root, root));
            using ContentModelCache models = new ContentModelCache(assets);
            GameObject parent = new GameObject("Model test");
            string originalPipeline = Shader.globalRenderPipeline;
            try
            {
                Shader.globalRenderPipeline = pipelineInitialized
                    ? "UniversalPipeline"
                    : string.Empty;
                using ContentModelInstance model = await models.InstantiateAsync(
                    "Pack/triangle",
                    parent.transform,
                    default
                );
                Material material = model
                    .ModelRoot.GetComponentInChildren<Renderer>()
                    .sharedMaterial;
                Assert.IsTrue(material.GetShaderPassEnabled("MotionVectors"));
                Assert.IsTrue(material.GetShaderPassEnabled("MOTIONVECTORS"));
            }
            finally
            {
                Shader.globalRenderPipeline = originalPipeline;
                UnityEngine.Object.DestroyImmediate(parent);
            }
        }

        /// <summary>
        /// Writes one opaque triangle with an authored material as a self-contained GLB.
        /// </summary>
        private void WriteTriangle()
        {
            const string description =
                "{\"asset\":{\"version\":\"2.0\"},\"scene\":0,\"scenes\":[{\"nodes\":[0]}],\"nodes\":[{\"mesh\":0}],\"meshes\":[{\"primitives\":[{\"attributes\":{\"POSITION\":0,\"NORMAL\":1},\"material\":0}]}],\"materials\":[{\"pbrMetallicRoughness\":{\"baseColorFactor\":[1,1,1,1],\"metallicFactor\":0}}],\"buffers\":[{\"byteLength\":72}],\"bufferViews\":[{\"buffer\":0,\"byteLength\":36},{\"buffer\":0,\"byteOffset\":36,\"byteLength\":36}],\"accessors\":[{\"bufferView\":0,\"componentType\":5126,\"count\":3,\"type\":\"VEC3\",\"min\":[-0.5,-0.5,0],\"max\":[0.5,0.5,0]},{\"bufferView\":1,\"componentType\":5126,\"count\":3,\"type\":\"VEC3\"}]}";
            byte[] json = Encoding.UTF8.GetBytes(
                description.PadRight((description.Length + 3) & ~3)
            );
            using BinaryWriter writer = new BinaryWriter(
                File.Create(Path.Combine(root, "triangle.glb"))
            );
            writer.Write(0x46546c67);
            writer.Write(2);
            writer.Write(28 + json.Length + 72);
            writer.Write(json.Length);
            writer.Write(0x4e4f534a);
            writer.Write(json);
            writer.Write(72);
            writer.Write(0x004e4942);
            foreach (
                float coordinate in new[]
                {
                    -0.5f,
                    -0.5f,
                    0f,
                    0.5f,
                    -0.5f,
                    0f,
                    0f,
                    0.5f,
                    0f,
                    0f,
                    0f,
                    1f,
                    0f,
                    0f,
                    1f,
                    0f,
                    0f,
                    1f,
                }
            )
                writer.Write(coordinate);
        }
    }
}
