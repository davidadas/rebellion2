using System;
using System.IO;
using NUnit.Framework;
using Rebellion.Game;
using Rebellion.Util.Serialization;

namespace Rebellion.Tests.App
{
    [TestFixture]
    public sealed class NewGameLaunchRequestTests
    {
        [Test]
        public void Read_NormalStartup_DoesNotRequestACampaign()
        {
            Assert.IsNull(NewGameLaunchRequest.Read(new[] { "game", "-screen-width", "1280" }));
        }

        [TestCase("-newGame")]
        [TestCase("-newGame -userDataPath")]
        [TestCase("-newGame one -newGame two")]
        public void Read_InvalidArguments_RejectsThem(string arguments)
        {
            Assert.Throws<ArgumentException>(() => NewGameLaunchRequest.Read(arguments.Split(' ')));
        }

        [Test]
        public void Read_LaunchFile_PreservesTheCompleteCampaignSettings()
        {
            string path = Path.GetTempFileName();
            try
            {
                using (FileStream output = File.Create(path))
                    new GameSerializer(typeof(NewGameLaunchRequest)).Serialize(
                        output,
                        new NewGameLaunchRequest
                        {
                            PackDirectory = "working pack",
                            Summary = new GameSummary
                            {
                                PackID = "pack-one",
                                ScenarioID = "scenario-one",
                                PlayerFactionID = "side-a",
                                GalaxySize = GameSize.Medium,
                                Difficulty = GameDifficulty.Hard,
                                Seed = 1234,
                            },
                        }
                    );
                NewGameLaunchRequest request = NewGameLaunchRequest.Read(
                    new[] { "-newGame", path }
                );
                Assert.AreEqual("working pack", request.PackDirectory);
                Assert.AreEqual("pack-one", request.Summary.PackID);
                Assert.AreEqual("scenario-one", request.Summary.ScenarioID);
                Assert.AreEqual("side-a", request.Summary.PlayerFactionID);
                Assert.AreEqual(GameSize.Medium, request.Summary.GalaxySize);
                Assert.AreEqual(GameDifficulty.Hard, request.Summary.Difficulty);
                Assert.AreEqual(1234, request.Summary.Seed);
            }
            finally
            {
                File.Delete(path);
            }
        }
    }
}
