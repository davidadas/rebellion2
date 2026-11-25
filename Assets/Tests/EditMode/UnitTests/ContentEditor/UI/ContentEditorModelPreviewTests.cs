using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Rebellion.Tests.ContentEditor.UI
{
    [TestFixture]
    public sealed class ContentEditorModelPreviewTests
    {
        [Test]
        public async Task Show_SingleRootModels_TiltKeepsCameraFixedAcrossFileSwitches()
        {
            string folder = Path.Combine(Path.GetTempPath(), "model-preview-" + Guid.NewGuid());
            Directory.CreateDirectory(folder);
            GameObject rig = new GameObject("Preview rig");
            try
            {
                WriteTriangle(Path.Combine(folder, "first.glb"));
                WriteTriangle(Path.Combine(folder, "second.glb"));
                using ContentAssets assets = new(new ContentFileResolver(folder, folder));
                Camera camera = new GameObject(
                    "Preview camera",
                    typeof(Camera)
                ).GetComponent<Camera>();
                camera.transform.SetParent(rig.transform, false);
                GameObject viewport = new GameObject("Viewport", typeof(RawImage));
                viewport.transform.SetParent(rig.transform, false);
                ContentEditorModelPreview preview =
                    viewport.AddComponent<ContentEditorModelPreview>();
                using SerializedObject serialized = new(preview);
                serialized.FindProperty("camera").objectReferenceValue = camera;
                serialized.ApplyModifiedPropertiesWithoutUndo();

                await ShowAsync(preview, assets, "Pack/first");
                Vector3 cameraPosition = camera.transform.position;
                Quaternion cameraRotation = camera.transform.rotation;
                float cameraSize = camera.orthographicSize;
                Transform mount = rig.transform.Find("Preview model");
                Quaternion modelRotation = mount.rotation;

                preview.OnDrag(
                    new PointerEventData(null)
                    {
                        button = PointerEventData.InputButton.Left,
                        delta = new Vector2(0, 160),
                    }
                );

                Assert.That(camera.transform.position, Is.EqualTo(cameraPosition));
                Assert.That(camera.transform.rotation, Is.EqualTo(cameraRotation));
                Assert.That(camera.orthographicSize, Is.EqualTo(cameraSize));
                Assert.That(Quaternion.Angle(mount.rotation, modelRotation), Is.GreaterThan(1f));

                preview.OnScroll(new PointerEventData(null) { scrollDelta = Vector2.up * 10f });
                Assert.That(
                    camera.orthographicSize,
                    Is.EqualTo(cameraSize / 1.05f).Within(0.0001f)
                );
                preview.ResetView();
                Assert.That(camera.orthographicSize, Is.EqualTo(cameraSize));
                Assert.That(Quaternion.Angle(mount.rotation, modelRotation), Is.LessThan(0.01f));

                preview.ResizeViewport(600, 900);
                Assert.That(preview.Texture.width, Is.EqualTo(600));
                Assert.That(preview.Texture.height, Is.EqualTo(900));
                Assert.That(camera.aspect, Is.EqualTo(2f / 3f).Within(0.0001f));
                Assert.That(camera.transform.position, Is.EqualTo(cameraPosition));
                Assert.That(camera.transform.rotation, Is.EqualTo(cameraRotation));
                Assert.That(camera.orthographicSize, Is.EqualTo(cameraSize * 1.5f).Within(0.0001f));

                await ShowAsync(preview, assets, "Pack/second");
                Assert.That(preview.Texture.width, Is.EqualTo(600));
                Assert.That(preview.Texture.height, Is.EqualTo(900));
                preview.Clear();
                Assert.That(camera.transform.parent, Is.SameAs(rig.transform));
                await ShowAsync(preview, assets, "Pack/first");
            }
            finally
            {
                if (rig != null)
                    UnityEngine.Object.DestroyImmediate(rig);
                Directory.Delete(folder, true);
            }
        }

        /// <summary>
        /// Waits for the selected file through the same callbacks as the model tab.
        /// </summary>
        /// <param name="preview">The preview under test.</param>
        /// <param name="assets">The fixture's resolved content.</param>
        /// <param name="address">The model to load.</param>
        /// <returns>A task that completes once the preview is ready.</returns>
        private static async Task ShowAsync(
            ContentEditorModelPreview preview,
            ContentAssets assets,
            string address
        )
        {
            TaskCompletionSource<bool> ready = new();
            using CancellationTokenSource timeout = new(TimeSpan.FromSeconds(8));
            using CancellationTokenRegistration registration = timeout.Token.Register(() =>
                ready.TrySetCanceled()
            );
            preview.Show(
                assets,
                address,
                () => ready.TrySetResult(true),
                message => ready.TrySetException(new InvalidOperationException(message))
            );
            await ready.Task;
            Assert.That(preview.Texture, Is.Not.Null);
        }

        /// <summary>
        /// Writes a single-node GLB whose scene can be instantiated directly under its parent.
        /// </summary>
        /// <param name="path">The destination file.</param>
        private static void WriteTriangle(string path)
        {
            const int byteLength = 9 * sizeof(float);
            string document =
                @"{""asset"":{""version"":""2.0""},""scene"":0,""scenes"":[{""nodes"":[0]}],"
                + @"""nodes"":[{""mesh"":0}],""meshes"":[{""primitives"":[{""attributes"":{""POSITION"":0}}]}],"
                + @"""buffers"":[{""byteLength"":36}],""bufferViews"":[{""buffer"":0,""byteLength"":36}],"
                + @"""accessors"":[{""bufferView"":0,""componentType"":5126,""count"":3,"
                + @"""type"":""VEC3"",""min"":[0,0,0],""max"":[1,1,0]}]}";
            byte[] json = Encoding.UTF8.GetBytes(document.PadRight((document.Length + 3) / 4 * 4));
            using BinaryWriter writer = new(File.Create(path));
            writer.Write(0x46546c67);
            writer.Write(2);
            writer.Write(28 + json.Length + byteLength);
            writer.Write(json.Length);
            writer.Write(0x4e4f534a);
            writer.Write(json);
            writer.Write(byteLength);
            writer.Write(0x004e4942);
            foreach (float coordinate in new float[] { 0, 0, 0, 1, 0, 0, 0, 1, 0 })
                writer.Write(coordinate);
        }
    }
}
