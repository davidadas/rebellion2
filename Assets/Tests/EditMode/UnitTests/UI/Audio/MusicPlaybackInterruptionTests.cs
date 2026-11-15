using NUnit.Framework;
using UnityEngine;

[TestFixture]
public sealed class MusicPlaybackInterruptionTests
{
    [Test]
    public void TryConsume_CapturedClip_ReturnsBoundedSampleAndClearsSuspension()
    {
        AudioClip clip = AudioClip.Create("Music", 100, 1, 44100, false);
        try
        {
            MusicPlaybackInterruption interruption = new MusicPlaybackInterruption();
            interruption.Capture(clip, 150);

            bool consumed = interruption.TryConsume(clip, out int resumeTimeSamples);

            Assert.IsTrue(consumed);
            Assert.AreEqual(99, resumeTimeSamples);
            Assert.IsFalse(interruption.IsSuspended);
        }
        finally
        {
            Object.DestroyImmediate(clip);
        }
    }

    [Test]
    public void TryConsume_DifferentAssignedClip_DoesNotRestoreStalePosition()
    {
        AudioClip interruptedClip = AudioClip.Create("Interrupted", 100, 1, 44100, false);
        AudioClip replacementClip = AudioClip.Create("Replacement", 100, 1, 44100, false);
        try
        {
            MusicPlaybackInterruption interruption = new MusicPlaybackInterruption();
            interruption.Capture(interruptedClip, 25);

            bool consumed = interruption.TryConsume(replacementClip, out int resumeTimeSamples);

            Assert.IsFalse(consumed);
            Assert.AreEqual(0, resumeTimeSamples);
            Assert.IsFalse(interruption.IsSuspended);
        }
        finally
        {
            Object.DestroyImmediate(interruptedClip);
            Object.DestroyImmediate(replacementClip);
        }
    }

    [Test]
    public void IsPlaybackPending_SuspendedClip_HoldsPlaylistAfterSourceStops()
    {
        AudioClip clip = AudioClip.Create("Music", 100, 1, 44100, false);
        try
        {
            MusicPlaybackInterruption interruption = new MusicPlaybackInterruption();
            interruption.Capture(clip, 25);

            bool isPending = interruption.IsPlaybackPending(false);

            Assert.IsTrue(isPending);
        }
        finally
        {
            Object.DestroyImmediate(clip);
        }
    }

    [Test]
    public void IsPlaybackPending_InactiveSourceWithoutSuspension_ReleasesPlaylist()
    {
        MusicPlaybackInterruption interruption = new MusicPlaybackInterruption();

        bool isPending = interruption.IsPlaybackPending(false);

        Assert.IsFalse(isPending);
    }

    [Test]
    public void Clear_CapturedClip_DiscardsSuspension()
    {
        AudioClip clip = AudioClip.Create("Music", 100, 1, 44100, false);
        try
        {
            MusicPlaybackInterruption interruption = new MusicPlaybackInterruption();
            interruption.Capture(clip, 25);

            interruption.Clear();

            Assert.IsFalse(interruption.IsSuspended);
            Assert.IsFalse(interruption.TryConsume(clip, out _));
        }
        finally
        {
            Object.DestroyImmediate(clip);
        }
    }
}
