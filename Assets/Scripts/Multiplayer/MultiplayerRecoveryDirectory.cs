using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Rebellion.Multiplayer
{
    /// <summary>
    /// Resolves a campaign's retained room before searching for its replacement after restart.
    /// </summary>
    internal static class MultiplayerRecoveryDirectory
    {
        /// <summary>
        /// Treats an absent previous room as a lookup miss while retaining real service failures.
        /// </summary>
        /// <param name="lobbyID">The last durable room identifier.</param>
        /// <param name="campaignID">The reserved campaign identifier.</param>
        /// <param name="findRoom">Looks up one room, returning null when it no longer exists.</param>
        /// <param name="listRooms">Searches currently published campaign rooms.</param>
        /// <param name="cancellationToken">Cancels an outgoing account's lookup.</param>
        /// <returns>The matching retained or replacement room, or null if both players are offline.</returns>
        public static async Task<MatchmakerGame> FindAsync(
            string lobbyID,
            string campaignID,
            Func<string, CancellationToken, Task<MatchmakerGame>> findRoom,
            Func<CancellationToken, Task<IReadOnlyList<MatchmakerGame>>> listRooms,
            CancellationToken cancellationToken
        )
        {
            cancellationToken.ThrowIfCancellationRequested();
            MatchmakerGame previous = await findRoom(lobbyID, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            if (previous?.campaignID == campaignID)
                return previous;
            IReadOnlyList<MatchmakerGame> rooms = await listRooms(cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            foreach (MatchmakerGame room in rooms)
                if (room.campaignID == campaignID)
                    return room;
            return null;
        }
    }
}
