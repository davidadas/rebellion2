using System.IO;
using NUnit.Framework;

[TestFixture]
public sealed class AudioProjectSettingsTests
{
    private const int _requestedDspBufferSize = 512;
    private const string _audioSettingsPath = "ProjectSettings/AudioManager.asset";

    [Test]
    public void RequestedDspBufferSize_IsConfiguredForLowLatency()
    {
        string audioSettings = File.ReadAllText(_audioSettingsPath);

        StringAssert.Contains(
            $"m_RequestedDSPBufferSize: {_requestedDspBufferSize}",
            audioSettings
        );
    }
}
