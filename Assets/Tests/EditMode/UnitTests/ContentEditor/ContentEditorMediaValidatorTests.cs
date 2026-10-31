using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Rebellion.Tests.ContentEditor
{
    [TestFixture]
    public sealed class ContentEditorMediaValidatorTests
    {
        private string root;
        private ContentFileResolver resolver;

        /// <summary>
        /// Creates an isolated media root for one decoder check.
        /// </summary>
        [SetUp]
        public void SetUp()
        {
            root = Path.Combine(
                Path.GetTempPath(),
                "content-editor-media-" + Guid.NewGuid().ToString("N")
            );
            Directory.CreateDirectory(root);
            resolver = new ContentFileResolver(root, root);
        }

        /// <summary>
        /// Removes only the files created by the decoder fixture.
        /// </summary>
        [TearDown]
        public void TearDown()
        {
            Directory.Delete(root, true);
        }

        [Test]
        public async Task ValidateAsync_ValidImage_AcceptsTheRuntimeDecoderResultAsync()
        {
            Texture2D image = new Texture2D(2, 2);
            try
            {
                File.WriteAllBytes(Path.Combine(root, "image.png"), image.EncodeToPNG());
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(image);
            }
            await ContentEditorMediaValidator.ValidateAsync(
                resolver,
                "Pack/image.png",
                ContentPackEditorMediaType.Image,
                default
            );
        }

        [Test]
        public async Task ValidateAsync_CorruptImage_ReportsTheAddressAsync()
        {
            File.WriteAllText(Path.Combine(root, "image.png"), "This is not image data.");

            Exception error = await CaptureFailureAsync(() =>
                ContentEditorMediaValidator.ValidateAsync(
                    resolver,
                    "Pack/image.png",
                    ContentPackEditorMediaType.Image,
                    default
                )
            );

            Assert.IsInstanceOf<InvalidDataException>(error);
            StringAssert.Contains("Pack/image.png", error.Message);
        }

        [TestCase("wav")]
        [TestCase("ogg")]
        [TestCase("mp3")]
        public async Task ValidateAsync_ValidAudio_AcceptsEachSupportedCodecAsync(string extension)
        {
            File.Copy(
                Path.Combine(
                    Application.dataPath,
                    "Tests/EditMode/UnitTests/Content/AudioFixtures/tone." + extension
                ),
                Path.Combine(root, "sound." + extension)
            );

            await ContentEditorMediaValidator.ValidateAsync(
                resolver,
                "Pack/sound." + extension,
                ContentPackEditorMediaType.Audio,
                default
            );
        }

        [Test]
        public async Task ValidateAsync_CancelledAudio_ObservesCancellationAndReleasesTheRequestAsync()
        {
            File.Copy(
                Path.Combine(
                    Application.dataPath,
                    "Tests/EditMode/UnitTests/Content/AudioFixtures/tone.wav"
                ),
                Path.Combine(root, "sound.wav")
            );
            using CancellationTokenSource cancellation = new CancellationTokenSource();
            Task pending = ContentEditorMediaValidator.ValidateAsync(
                resolver,
                "Pack/sound.wav",
                ContentPackEditorMediaType.Audio,
                cancellation.Token
            );
            cancellation.Cancel();

            Exception error = await CaptureFailureAsync(() => pending);

            Assert.IsInstanceOf<OperationCanceledException>(error);
            File.Delete(Path.Combine(root, "sound.wav"));
        }

        [TestCase(ContentPackEditorMediaType.Audio, "sound.wav")]
        [TestCase(ContentPackEditorMediaType.Video, "video.mp4")]
        [TestCase(ContentPackEditorMediaType.Model, "model.glb")]
        [Timeout(30000)]
        public async Task ValidateAsync_CorruptEncodedMedia_RejectsThePayloadAsync(
            ContentPackEditorMediaType type,
            string fileName
        )
        {
            File.WriteAllText(Path.Combine(root, fileName), "This is not valid encoded media.");
            using CancellationTokenSource cancellation = new CancellationTokenSource(
                TimeSpan.FromSeconds(20)
            );
            bool ignored = LogAssert.ignoreFailingMessages;
            Exception failure;
            try
            {
                LogAssert.ignoreFailingMessages = true;
                failure = await CaptureFailureAsync(() =>
                    ContentEditorMediaValidator.ValidateAsync(
                        resolver,
                        "Pack/" + fileName,
                        type,
                        cancellation.Token
                    )
                );
            }
            finally
            {
                LogAssert.ignoreFailingMessages = ignored;
            }

            Assert.IsInstanceOf<InvalidDataException>(failure);
            StringAssert.Contains("Pack/" + fileName, failure.Message);
            File.Delete(Path.Combine(root, fileName));
        }

        [Test]
        [Timeout(30000)]
        public async Task ValidateAsync_ValidVideo_PreparesWithThePlatformDecoderAsync()
        {
            File.Copy(
                Path.Combine(
                    Application.dataPath,
                    "Tests/EditMode/UnitTests/ContentEditor/MediaFixtures/black-frame.webm"
                ),
                Path.Combine(root, "video.webm")
            );
            using CancellationTokenSource cancellation = new CancellationTokenSource(
                TimeSpan.FromSeconds(20)
            );

            try
            {
                await ContentEditorMediaValidator.ValidateAsync(
                    resolver,
                    "Pack/video.webm",
                    ContentPackEditorMediaType.Video,
                    cancellation.Token
                );
            }
            catch (OperationCanceledException)
            {
                Assert.Fail(
                    "The valid video did not finish preparation before the cancellation limit."
                );
            }
        }

        [Test]
        public async Task ValidateAsync_EmbeddedGlb_AcceptsTheRuntimeModelImporterAsync()
        {
            byte[] json = Encoding.UTF8.GetBytes(
                "{\"asset\":{\"version\":\"2.0\"},\"scene\":0,\"scenes\":[{\"nodes\":[0]}],\"nodes\":[{}]}".PadRight(
                    104
                )
            );
            using (
                BinaryWriter writer = new BinaryWriter(File.Create(Path.Combine(root, "model.glb")))
            )
            {
                writer.Write(0x46546c67);
                writer.Write(2);
                writer.Write(20 + json.Length);
                writer.Write(json.Length);
                writer.Write(0x4e4f534a);
                writer.Write(json);
            }

            await ContentEditorMediaValidator.ValidateAsync(
                resolver,
                "Pack/model.glb",
                ContentPackEditorMediaType.Model,
                default
            );
        }

        /// <summary>
        /// Captures an expected asynchronous failure without blocking Unity's synchronization context.
        /// </summary>
        /// <param name="operation">The operation expected to fail.</param>
        /// <returns>The failure.</returns>
        private static async Task<Exception> CaptureFailureAsync(Func<Task> operation)
        {
            try
            {
                await operation();
            }
            catch (Exception exception)
            {
                return exception;
            }
            Assert.Fail("The invalid or cancelled media check must fail.");
            return null;
        }
    }
}
