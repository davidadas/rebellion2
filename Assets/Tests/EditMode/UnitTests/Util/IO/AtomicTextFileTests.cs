using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using Rebellion.Util.IO;

namespace Rebellion.Tests.Util.IO
{
    [TestFixture]
    public sealed class AtomicTextFileTests
    {
        private string _directory;
        private string _path;

        /// <summary>
        /// Creates an isolated directory and destination for atomic publication tests.
        /// </summary>
        [SetUp]
        public void SetUp()
        {
            _directory = Path.Combine(
                Path.GetTempPath(),
                "rebellion-atomic-text-" + Guid.NewGuid().ToString("N")
            );
            Directory.CreateDirectory(_directory);
            _path = Path.Combine(_directory, "document.json");
        }

        /// <summary>
        /// Removes the files and directory owned by this fixture.
        /// </summary>
        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(_directory))
                Directory.Delete(_directory, true);
        }

        [Test]
        public async Task WriteAllText_ConcurrentInitialPublishers_PublishesOneCompleteDocumentAndCleansOwnedFilesAsync()
        {
            string[] documents = Enumerable
                .Range(0, 2)
                .Select(index =>
                    "{\"writer\":"
                    + index
                    + ",\"payload\":\""
                    + new string((char)('a' + index), 65536)
                    + "\"}"
                )
                .ToArray();
            using CountdownEvent ready = new CountdownEvent(documents.Length);
            using ManualResetEventSlim start = new ManualResetEventSlim(false);
            Task[] writers = documents
                .Select(document =>
                    Task.Run(() =>
                    {
                        ready.Signal();
                        start.Wait();
                        AtomicTextFile.WriteAllText(_path, document);
                    })
                )
                .ToArray();
            ready.Wait();
            start.Set();

            await Task.WhenAll(writers);

            CollectionAssert.Contains(documents, AtomicTextFile.ReadAllText(_path));
            Assert.That(Directory.GetFiles(_directory, "*.tmp"), Is.Empty);
        }

        [Test]
        public void WriteAllText_ExistingReaderAllowsReplacement_RetainsOldDocumentForReaderAndPublishesNewDocument()
        {
            const string first = "{\"value\":\"first complete document\"}";
            const string second = "{\"value\":\"second complete document\"}";
            AtomicTextFile.WriteAllText(_path, first);
            using FileStream previous = new FileStream(
                _path,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read | FileShare.Delete
            );

            AtomicTextFile.WriteAllText(_path, second);

            using StreamReader oldReader = new StreamReader(previous, Encoding.UTF8);
            Assert.That(oldReader.ReadToEnd(), Is.EqualTo(first));
            Assert.That(AtomicTextFile.ReadAllText(_path), Is.EqualTo(second));
        }

        [Test]
        public void WriteAllText_PublicationFails_CleansOnlyOwnedTemporaryFile()
        {
            Directory.CreateDirectory(_path);
            string otherTemporaryPath = _path + ".other.tmp";
            File.WriteAllText(otherTemporaryPath, "another owner's document");

            Assert.Throws<IOException>(() => AtomicTextFile.WriteAllText(_path, "replacement"));

            Assert.That(Directory.Exists(_path), Is.True);
            Assert.That(
                File.ReadAllText(otherTemporaryPath),
                Is.EqualTo("another owner's document")
            );
            CollectionAssert.AreEqual(
                new[] { otherTemporaryPath },
                Directory.GetFiles(_directory, "*.tmp")
            );
        }
    }
}
