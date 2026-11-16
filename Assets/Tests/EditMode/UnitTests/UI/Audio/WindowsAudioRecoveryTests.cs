using System;
using System.Collections.Generic;
using NUnit.Framework;

[TestFixture]
public sealed class WindowsAudioRecoveryTests
{
    [TestCase(true, "existing game render session")]
    [TestCase(null, "inconclusive")]
    public void Open_ExistingOrUnknownOutput_DoesNotOpenFallback(bool? hasSession, string reason)
    {
        bool opened = false;
        List<string> reports = new List<string>();
        WindowsWaveOutput output = WindowsAudioRecovery.Open(
            false,
            () => hasSession,
            () =>
            {
                opened = true;
                return null;
            },
            reports.Add
        );

        Assert.IsNull(output);
        Assert.IsFalse(opened);
        Assert.That(reports, Has.Count.EqualTo(1));
        Assert.That(reports[0], Does.Contain(reason));
    }

    [Test]
    public void Open_MissingOutput_OpensFallback()
    {
        bool opened = false;
        List<string> reports = new List<string>();

        WindowsAudioRecovery.Open(
            false,
            () => false,
            () =>
            {
                opened = true;
                return null;
            },
            reports.Add
        );

        Assert.IsTrue(opened);
        Assert.That(reports, Has.Count.EqualTo(1));
        Assert.That(reports[0], Does.Contain("no existing game render session"));
    }

    [Test]
    public void Open_ExplicitOverride_BypassesSessionInspection()
    {
        bool opened = false;
        bool inspected = false;
        List<string> reports = new List<string>();

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
            },
            reports.Add
        );

        Assert.IsFalse(inspected);
        Assert.IsTrue(opened);
        Assert.That(reports, Has.Count.EqualTo(1));
        Assert.That(reports[0], Does.Contain("explicit override"));
    }

    [Test]
    public void Open_StreamOpeningFails_ReportsDecisionBeforeFailure()
    {
        List<string> reports = new List<string>();

        Assert.Throws<InvalidOperationException>(() =>
            WindowsAudioRecovery.Open(
                false,
                () => false,
                () => throw new InvalidOperationException("Device unavailable."),
                reports.Add
            )
        );

        Assert.That(reports, Has.Count.EqualTo(1));
        Assert.That(reports[0], Does.Contain("Opening stereo output"));
    }
}
