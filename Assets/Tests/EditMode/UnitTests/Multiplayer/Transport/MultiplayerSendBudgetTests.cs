using System.IO;
using System.Linq;
using System.Threading.Tasks;
using NUnit.Framework;
using Rebellion.Multiplayer;

namespace Rebellion.Tests.Multiplayer
{
    public sealed class MultiplayerSendBudgetTests
    {
        [Test]
        public void Reserve_MaximumLegalCampaignPacket_AdmitsBulkWithoutTacticalRestriction()
        {
            MultiplayerSendBudget budget = new();

            using MultiplayerSendBudget.Reservation reservation = budget.Reserve(
                MultiplayerPacketCodec.MaximumPacketLength,
                false
            );

            Assert.AreEqual(1, budget.RetainedPackets);
            Assert.AreEqual(MultiplayerPacketCodec.MaximumPacketLength, budget.RetainedBytes);
        }

        [Test]
        public void Reserve_TacticalBytesExhausted_RetainsSeparateCampaignAllowance()
        {
            MultiplayerSendBudget budget = new(maximumBytes: 1000, maximumTacticalBytes: 100);
            using MultiplayerSendBudget.Reservation tactical = budget.Reserve(80, true);

            Assert.Throws<InvalidDataException>(() => budget.Reserve(21, true));
            using MultiplayerSendBudget.Reservation campaign = budget.Reserve(120, false);

            Assert.AreEqual(2, budget.RetainedPackets);
            Assert.AreEqual(200, budget.RetainedBytes);
        }

        [Test]
        public void Reserve_PacketAndByteCaps_RejectsWithoutRetainingOverflow()
        {
            MultiplayerSendBudget budget = new(maximumPackets: 2, maximumBytes: 100);
            using MultiplayerSendBudget.Reservation first = budget.Reserve(40, false);
            MultiplayerSendBudget.Reservation second = budget.Reserve(40, false);
            Assert.Throws<InvalidDataException>(() => budget.Reserve(1, false));
            second.Dispose();

            Assert.Throws<InvalidDataException>(() => budget.Reserve(61, false));

            Assert.AreEqual(1, budget.RetainedPackets);
            Assert.AreEqual(40, budget.RetainedBytes);
        }

        [Test]
        public async Task Dispose_ConcurrentRepeatedCleanup_ReleasesReservationOnceAsync()
        {
            MultiplayerSendBudget budget = new();
            MultiplayerSendBudget.Reservation reservation = budget.Reserve(1024, true);

            await Task.WhenAll(Enumerable.Range(0, 32).Select(_ => Task.Run(reservation.Dispose)));
            reservation.Dispose();

            Assert.Zero(budget.RetainedPackets);
            Assert.Zero(budget.RetainedBytes);
            using MultiplayerSendBudget.Reservation next = budget.Reserve(
                MultiplayerPacketCodec.MaximumPacketLength,
                false
            );
            Assert.AreEqual(1, budget.RetainedPackets);
        }
    }
}
