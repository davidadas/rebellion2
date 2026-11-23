using System.Collections;
using NUnit.Framework;
using UnityEngine.TestTools;

namespace Rebellion.Tests.Multiplayer
{
    [TestFixture]
    [Category("RequiresMedia")]
    public sealed class MultiplayerMissionConfirmationTests
    {
        [UnityTest]
        public IEnumerator ExecuteAsync_AllMissionFamiliesFromConfirmationButton_StartsMatchingMissionOnBothPeers()
        {
            yield return MultiplayerTurnProtocolTests.RunMissionConfirmationCases();
        }
    }
}
