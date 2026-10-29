using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using Rebellion.Util.IO;

namespace Rebellion.Tests.Util.IO
{
    [TestFixture]
    public class SerialFileOperationsTests
    {
        private string _directory;

        /// <summary>
        /// Creates a unique logical directory key without touching the filesystem.
        /// </summary>
        [SetUp]
        public void SetUp()
        {
            _directory = Path.Combine(
                Path.GetTempPath(),
                nameof(SerialFileOperationsTests),
                Guid.NewGuid().ToString("N")
            );
        }

        [Test]
        public async Task Reserve_MultipleOperations_ReleasesInReservationOrderAsync()
        {
            using SerialFileOperations.Reservation first = SerialFileOperations.Reserve(_directory);
            SerialFileOperations.Reservation second = SerialFileOperations.Reserve(_directory);
            SerialFileOperations.Reservation third = SerialFileOperations.Reserve(_directory);
            List<int> order = new List<int>();
            Task secondWork = second.RunAsync(() => order.Add(2));
            Task thirdWork = third.RunAsync(() => order.Add(3));

            first.Dispose();
            await Task.WhenAll(secondWork, thirdWork);

            CollectionAssert.AreEqual(new[] { 2, 3 }, order);
        }

        [Test]
        public async Task Dispose_UnstartedMiddleReservation_PreservesEarlierBarrierAsync()
        {
            using SerialFileOperations.Reservation first = SerialFileOperations.Reserve(_directory);
            SerialFileOperations.Reservation abandoned = SerialFileOperations.Reserve(_directory);
            using SerialFileOperations.Reservation last = SerialFileOperations.Reserve(_directory);

            abandoned.Dispose();

            Assert.That(abandoned.Completion.IsCompleted, Is.False);
            first.Dispose();
            await abandoned.Completion;
            last.Wait();
        }

        [Test]
        public async Task RunAsync_FailedEarlierOperation_AllowsIndependentSuccessorAsync()
        {
            SerialFileOperations.Reservation first = SerialFileOperations.Reserve(_directory);
            SerialFileOperations.Reservation second = SerialFileOperations.Reserve(_directory);
            bool completed = false;
            Task failed = first.RunAsync(() => throw new IOException("Rejected file publication."));
            Task successor = second.RunAsync(() => completed = true);

            IOException failure = null;
            try
            {
                await failed;
            }
            catch (IOException exception)
            {
                failure = exception;
            }
            Assert.That(failure, Is.Not.Null);
            await successor;

            Assert.That(completed, Is.True);
        }

        [Test]
        public void Run_UnrelatedDirectory_RunsOnCallingThread()
        {
            using SerialFileOperations.Reservation first = SerialFileOperations.Reserve(_directory);
            int callingThread = Thread.CurrentThread.ManagedThreadId;

            int result = SerialFileOperations.Run(
                _directory + "-other",
                () => Thread.CurrentThread.ManagedThreadId
            );

            Assert.That(result, Is.EqualTo(callingThread));
        }

        [Test]
        public void Dispose_RepeatedRelease_AllowsLaterOperation()
        {
            SerialFileOperations.Reservation first = SerialFileOperations.Reserve(_directory);

            first.Dispose();
            first.Dispose();

            Assert.That(SerialFileOperations.Run(_directory, () => 42), Is.EqualTo(42));
        }
    }
}
