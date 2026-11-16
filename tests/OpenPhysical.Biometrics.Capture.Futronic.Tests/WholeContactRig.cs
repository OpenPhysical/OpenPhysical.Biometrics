// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Capture.Futronic.Tests;

internal sealed class WholeContactRig : IDisposable
{
    internal EmptySensorClock Clock { get; } = new();
    internal Fs50EmptySensorLearning Learning { get; }
    internal Fs50WholeContactQualification Qualification { get; }
    private long sequence;
    private readonly Fs50WholeContactPolicy policy;

    internal WholeContactRig()
        : this(Fs50WholeContactPolicy.Default) { }

    internal WholeContactRig(Fs50WholeContactPolicy policy)
    {
        this.policy = policy;
        Learning = Fs50EmptySensorLearning.Create(policy.EmptySensor, Clock).Value;
        Qualification = Fs50WholeContactQualification.Create(policy, Clock).Value;
    }

    internal bool Learn() => Learn(Fs50EvidenceFrameFixture.Empty());

    internal bool Learn(byte[] pixels, int exposure = 200)
    {
        for (var i = 0; i < 4; i++)
        {
            var frame = Fs50EvidenceFrameFixture.Create(pixels, sequence++, exposure);
            var contact = Fs50ContactAnalysis.Analyze(pixels, policy.Evidence).Value;
            var observed = Learning.Observe(frame, contact, "session").Value;
            if (!observed.ConfidentlyEmpty)
                return false;
            _ = Qualification.Observe(frame, contact, observed, "session", CancellationToken.None);
            Clock.Advance(250);
        }
        return true;
    }

    internal (CaptureFrame Frame, Fs50ContactRegion Contact, Fs50WholeContactProof Proof) Observe(
        byte[] pixels,
        int exposure = 200,
        string session = "session",
        byte calibration = 0
    )
    {
        var frame = Fs50EvidenceFrameFixture.Create(pixels, sequence++, exposure, calibration);
        var contact = Fs50ContactAnalysis.Analyze(pixels, policy.Evidence).Value;
        var observed = Learning.Observe(frame, contact, session).Value;
        var proof = Qualification
            .Observe(frame, contact, observed, session, CancellationToken.None)
            .Value;
        Clock.Advance(20);
        return (frame, contact, proof);
    }

    internal static bool Ridges(byte[] pixels, int x, int y, int width, int height)
    {
        for (var row = y; row < y + height; row++)
        for (var column = x; column < x + width; column++)
            pixels[row * 800 + column] = column % 8 < 4 ? (byte)65 : (byte)165;
        return true;
    }

    public void Dispose()
    {
        Qualification.Dispose();
        Learning.Dispose();
    }
}
