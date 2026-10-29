using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using Rebellion.Multiplayer;

namespace Rebellion.Tests.Multiplayer
{
    public sealed class MultiplayerRecoveryDirectoryTests
    {
        [Test]
        public async Task FindAsync_PreviousRoomRetained_ReturnsItWithoutSearchingDirectoryAsync()
        {
            MatchmakerGame retained = new() { id = "old-room", campaignID = "campaign" };
            MatchmakerGame result = await MultiplayerRecoveryDirectory.FindAsync(
                "old-room",
                "campaign",
                (_, _) => Task.FromResult(retained),
                _ => throw new InvalidOperationException("The retained room should be used."),
                default
            );
            Assert.AreSame(retained, result);
        }

        [Test]
        public async Task FindAsync_PreviousRoomDeleted_FindsReplacementForSameCampaignAsync()
        {
            MatchmakerGame replacement = new() { id = "replacement", campaignID = "campaign" };
            MatchmakerGame result = await MultiplayerRecoveryDirectory.FindAsync(
                "old-room",
                "campaign",
                (_, _) => Task.FromResult<MatchmakerGame>(null),
                _ =>
                    Task.FromResult<IReadOnlyList<MatchmakerGame>>(
                        new[]
                        {
                            new MatchmakerGame { campaignID = "another" },
                            replacement,
                        }
                    ),
                default
            );
            Assert.AreSame(replacement, result);
        }

        [Test]
        public async Task FindAsync_BothRoomsGone_ReturnsNullSoHostCanReopenCheckpointAsync()
        {
            MatchmakerGame result = await MultiplayerRecoveryDirectory.FindAsync(
                "old-room",
                "campaign",
                (_, _) => Task.FromResult<MatchmakerGame>(null),
                _ => Task.FromResult<IReadOnlyList<MatchmakerGame>>(Array.Empty<MatchmakerGame>()),
                default
            );
            Assert.IsNull(result);
        }

        [Test]
        public async Task FindAsync_PreviousRoomHasDifferentCampaign_DoesNotReuseItAsync()
        {
            MatchmakerGame result = await MultiplayerRecoveryDirectory.FindAsync(
                "old-room",
                "campaign",
                (_, _) =>
                    Task.FromResult(new MatchmakerGame { id = "old-room", campaignID = "another" }),
                _ => Task.FromResult<IReadOnlyList<MatchmakerGame>>(Array.Empty<MatchmakerGame>()),
                default
            );
            Assert.IsNull(result);
        }

        [Test]
        public void FindAsync_ServiceFailure_DoesNotMistakeItForDeletedRoom()
        {
            Assert.ThrowsAsync<IOException>(async () =>
                await MultiplayerRecoveryDirectory.FindAsync(
                    "old-room",
                    "campaign",
                    (_, _) => throw new IOException("The directory is offline."),
                    _ =>
                        throw new InvalidOperationException(
                            "A failed lookup must not create another room."
                        ),
                    default
                )
            );
        }

        [Test]
        public void FindAsync_CanceledLookup_DoesNotReturnOutgoingRoom()
        {
            using CancellationTokenSource cancellation = new();
            Assert.CatchAsync<OperationCanceledException>(async () =>
                await MultiplayerRecoveryDirectory.FindAsync(
                    "old-room",
                    "campaign",
                    (_, _) =>
                    {
                        cancellation.Cancel();
                        return Task.FromResult(new MatchmakerGame { campaignID = "campaign" });
                    },
                    _ => throw new InvalidOperationException("The canceled lookup must stop."),
                    cancellation.Token
                )
            );
        }
    }
}
