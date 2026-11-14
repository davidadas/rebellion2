using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using Rebellion.Util.IO;

namespace Rebellion.Tests.Util.IO
{
    [TestFixture]
    public class AtomicFileTests
    {
        private string _directory;
        private string _path;

        /// <summary>
        /// Creates an isolated directory and checkpoint destination.
        /// </summary>
        [SetUp]
        public void SetUp()
        {
            _directory = Path.Combine(
                Path.GetTempPath(),
                nameof(AtomicFileTests),
                Guid.NewGuid().ToString("N")
            );
            Directory.CreateDirectory(_directory);
            _path = Path.Combine(_directory, "checkpoint.sav");
        }

        /// <summary>
        /// Removes the completed fixture's owned files.
        /// </summary>
        [TearDown]
        public void TearDown()
        {
            Directory.Delete(_directory, true);
        }

        [Test]
        public async Task WriteAsync_CallerChangesSourceAfterReturn_PublishesCapturedBytesAsync()
        {
            string value = "captured";
            int writerThread = 0;
            int callingThread = Thread.CurrentThread.ManagedThreadId;
            bool completionOnWorker = false;

            Task publication = AtomicFile.WriteAsync(
                _path,
                stream =>
                {
                    writerThread = Thread.CurrentThread.ManagedThreadId;
                    byte[] bytes = Encoding.UTF8.GetBytes(value);
                    stream.Write(bytes, 0, bytes.Length);
                },
                () => completionOnWorker = Thread.CurrentThread.IsThreadPoolThread
            );
            value = "changed after capture";
            await publication;

            Assert.That(File.ReadAllText(_path), Is.EqualTo("captured"));
            Assert.That(writerThread, Is.EqualTo(callingThread));
            Assert.That(completionOnWorker, Is.True);
        }

        [Test]
        public void WriteAsync_WriterThrows_PreservesExistingFileAndCleansOwnedTemporary()
        {
            File.WriteAllText(_path, "previous checkpoint");

            Assert.Throws<InvalidDataException>(() =>
                AtomicFile.WriteAsync(
                    _path,
                    stream =>
                    {
                        stream.WriteByte(42);
                        throw new InvalidDataException("Invalid serialized state.");
                    }
                )
            );

            Assert.That(File.ReadAllText(_path), Is.EqualTo("previous checkpoint"));
            Assert.That(Directory.GetFiles(_directory, "*.tmp"), Is.Empty);
        }

        [Test]
        public async Task WriteAsync_PublicationFails_CleansOwnedTemporaryAndReleasesDirectoryAsync()
        {
            Directory.CreateDirectory(_path);

            IOException failure = null;
            try
            {
                await AtomicFile.WriteAsync(_path, stream => stream.WriteByte(42));
            }
            catch (IOException exception)
            {
                failure = exception;
            }
            Assert.That(failure, Is.Not.Null);
            Directory.Delete(_path);
            await AtomicFile.WriteAsync(_path, stream => stream.WriteByte(7));

            CollectionAssert.AreEqual(new byte[] { 7 }, File.ReadAllBytes(_path));
            Assert.That(Directory.GetFiles(_directory, "*.tmp"), Is.Empty);
        }

        [Test]
        public async Task WriteAsync_CompletionWorkStillRunning_DoesNotReportCompletionAsync()
        {
            TaskCompletionSource<bool> entered = new TaskCompletionSource<bool>(
                TaskCreationOptions.RunContinuationsAsynchronously
            );
            using ManualResetEventSlim release = new ManualResetEventSlim();
            Task publication = AtomicFile.WriteAsync(
                _path,
                stream => stream.WriteByte(42),
                () =>
                {
                    entered.SetResult(true);
                    release.Wait();
                }
            );
            try
            {
                await entered.Task;
                Assert.That(publication.IsCompleted, Is.False);
            }
            finally
            {
                release.Set();
                await publication;
            }
        }

        [Test]
        public async Task WriteAsync_CompletionWorkFails_PreservesPublishedPayloadAndReleasesDirectoryAsync()
        {
            IOException failure = null;
            try
            {
                await AtomicFile.WriteAsync(
                    _path,
                    stream => stream.WriteByte(42),
                    () => throw new IOException("Post-publication cleanup failed.")
                );
            }
            catch (IOException exception)
            {
                failure = exception;
            }
            Assert.That(failure, Is.Not.Null);
            CollectionAssert.AreEqual(new byte[] { 42 }, File.ReadAllBytes(_path));

            await AtomicFile.WriteAsync(_path, stream => stream.WriteByte(7));

            CollectionAssert.AreEqual(new byte[] { 7 }, File.ReadAllBytes(_path));
        }
    }
}
