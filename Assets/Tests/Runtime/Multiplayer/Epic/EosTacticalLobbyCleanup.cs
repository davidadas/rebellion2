using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Epic.OnlineServices;
using Epic.OnlineServices.Lobby;
using EOSManager = PlayEveryWare.EpicOnlineServices.EOSManager;

namespace Rebellion.Tests.MultiplayerIntegration
{
    /// <summary>
    /// Waits for native service evidence that the exact completed test lobby has disappeared.
    /// </summary>
    internal static class EosTacticalLobbyCleanup
    {
        /// <summary>
        /// Searches the exact lobby until EOS reports no result while the application's ordinary SDK update continues.
        /// </summary>
        /// <param name="localUserID">The genuinely authenticated product user making the search.</param>
        /// <param name="lobbyID">The exact completed lobby whose cleanup must be observed.</param>
        /// <param name="timeout">The complete bounded search deadline.</param>
        /// <returns>The observed service-side absence, or a failure when absence cannot be established.</returns>
        internal static async Task WaitForDisappearanceAsync(
            string localUserID,
            string lobbyID,
            TimeSpan timeout
        )
        {
            ProductUserId localUser = ProductUserId.FromString(localUserID);
            if (localUser?.IsValid() != true)
                throw new ArgumentException(
                    "An authenticated cleanup search identity is required.",
                    nameof(localUserID)
                );
            if (string.IsNullOrWhiteSpace(lobbyID))
                throw new ArgumentException(
                    "The exact completed test lobby is required.",
                    nameof(lobbyID)
                );
            if (timeout <= TimeSpan.Zero || timeout > TimeSpan.FromSeconds(30))
                throw new ArgumentOutOfRangeException(nameof(timeout));
            var platform =
                EOSManager.Instance.GetEOSPlatformInterface()
                ?? throw new InvalidOperationException(
                    "EOS must remain initialized until lobby cleanup is observed."
                );
            LobbyInterface lobbies = platform.GetLobbyInterface();
            Stopwatch clock = Stopwatch.StartNew();
            while (clock.Elapsed < timeout)
            {
                CreateLobbySearchOptions create = new() { MaxResults = 1 };
                Result created = lobbies.CreateLobbySearch(ref create, out LobbySearch search);
                if (created != Result.Success)
                    throw new InvalidOperationException("Create exact cleanup search: " + created);
                try
                {
                    LobbySearchSetLobbyIdOptions exact = new() { LobbyId = lobbyID };
                    Result configured = search.SetLobbyId(ref exact);
                    if (configured != Result.Success)
                        throw new InvalidOperationException(
                            "Configure exact cleanup search: " + configured
                        );
                    TaskCompletionSource<Result> completion = new(
                        TaskCreationOptions.RunContinuationsAsynchronously
                    );
                    LobbySearchFindOptions find = new() { LocalUserId = localUser };
                    search.Find(
                        ref find,
                        null,
                        (ref LobbySearchFindCallbackInfo info) =>
                        {
                            if (Common.IsOperationComplete(info.ResultCode))
                                completion.TrySetResult(info.ResultCode);
                        }
                    );
                    TimeSpan remaining = timeout - clock.Elapsed;
                    if (
                        remaining <= TimeSpan.Zero
                        || await Task.WhenAny(completion.Task, Task.Delay(remaining))
                            != completion.Task
                    )
                        throw new TimeoutException(
                            "The exact EOS lobby cleanup search exceeded its deadline."
                        );
                    Result found = await completion.Task;
                    if (found == Result.NotFound)
                        return;
                    if (found != Result.Success)
                        throw new InvalidOperationException(
                            "Find exact completed test lobby: " + found
                        );
                    LobbySearchGetSearchResultCountOptions count = new();
                    if (search.GetSearchResultCount(ref count) == 0)
                        return;
                }
                finally
                {
                    search.Release();
                }
                TimeSpan interval = timeout - clock.Elapsed;
                if (interval > TimeSpan.Zero)
                    await Task.Delay(
                        interval < TimeSpan.FromMilliseconds(250)
                            ? interval
                            : TimeSpan.FromMilliseconds(250)
                    );
            }
            throw new TimeoutException(
                "The exact completed EOS lobby remained visible after the cleanup deadline."
            );
        }
    }
}
