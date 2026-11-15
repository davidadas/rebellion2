using NUnit.Framework;

[TestFixture]
public sealed class WindowsAudioRecoveryTests
{
    [TestCase(true)]
    [TestCase(null)]
    public void Open_ExistingOrUnknownOutput_DoesNotOpenFallback(bool? hasSession)
    {
        bool opened = false;
        WindowsWaveOutput output = WindowsAudioRecovery.Open(
            false,
            () => hasSession,
            () =>
            {
                opened = true;
                return null;
            }
        );

        Assert.IsNull(output);
        Assert.IsFalse(opened);
    }

    [Test]
    public void Open_MissingOutput_OpensFallback()
    {
        bool opened = false;

        WindowsAudioRecovery.Open(
            false,
            () => false,
            () =>
            {
                opened = true;
                return null;
            }
        );

        Assert.IsTrue(opened);
    }

    [Test]
    public void Open_ExplicitOverride_BypassesSessionInspection()
    {
        bool opened = false;
        bool inspected = false;

        WindowsAudioRecovery.Open(
            true,
            () =>
            {
                inspected = true;
                return true;
            },
            () =>
            {
                opened = true;
                return null;
            }
        );

        Assert.IsFalse(inspected);
        Assert.IsTrue(opened);
    }
}
