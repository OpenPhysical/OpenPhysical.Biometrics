// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Desktop.Tests;

using System.Text;
using CSharpFunctionalExtensions;
using OpenPhysical.Biometrics.Capture;
using OpenPhysical.Biometrics.Capture.Futronic;

internal static class SyntheticRecordedQualificationFixtures
{
    private const string SessionId = "synthetic-recorded-session";

    internal static CapturedFingerprint Capture()
    {
        var capture = HullStorageFixtures.Capture(Pixels());
        return capture with
        {
            Processing = capture.Processing.Value with
            {
                InputRole = CaptureProcessingInputRole.LiveSensor,
                OriginalQualification = Record(capture),
            },
        };
    }

    internal static CapturedFingerprint SpatialCapture()
    {
        var capture = Capture();
        var record = capture.Processing.Value.OriginalQualification.Value;
        return capture with
        {
            Processing = capture.Processing.Value with
            {
                OriginalQualification = record with
                {
                    Decision = Fs50WholeContactDecision.SpatialCapture,
                    ReferenceDigest = Maybe<string>.None,
                    ReferenceKeyDigest = Maybe<string>.None,
                    QualifiedAnchorDigest = Maybe<string>.None,
                    Metrics = Unknown(record.Metrics.ContactArea),
                    ObservedMetrics = Unknown(record.ObservedMetrics.ContactArea),
                },
            },
        };
    }

    private static Fs50WholeContactMetrics Unknown(int area) => new(area, 0, area, 0, area, area);

    private static Fs50WholeContactRecord Record(CapturedFingerprint capture)
    {
        // Recorded metadata only. This fixture cannot authorize live processing.
        var contour = capture.Processing.Value.Contour;
        var policy = Fs50WholeContactPolicy
            .Create(
                new Fs50WholeContactOptions(),
                capture.Processing.Value.ContactDiagnostics.Policy,
                Fs50EmptySensorPolicy.Default,
                FingerCapturePolicy.Default
            )
            .Value;
        var area = contour.Statistics.SelectedContactArea;
        return new Fs50WholeContactRecord(
            Fs50WholeContactDecision.QualifiedReference,
            Identity(capture),
            policy,
            PackageTestFiles.Digest(Encoding.UTF8.GetBytes("synthetic recorded reference")),
            SessionKey(capture),
            default,
            new Fs50WholeContactMetrics(area, area, 0, 0, 0, 0),
            Ridge(capture),
            new Fs50WholeContactMetrics(
                capture.Processing.Value.ContactDiagnostics.ObservedContactMask.Count(value =>
                    value != 0
                ),
                capture.Processing.Value.ContactDiagnostics.ObservedContactMask.Count(value =>
                    value != 0
                ),
                0,
                0,
                0,
                0
            )
        );
    }

    private static Fs50WholeContactRidgeEvidence Ridge(CapturedFingerprint capture)
    {
        var value = capture.Processing.Value.ContactDiagnostics;
        return new(
            PackageTestFiles.Digest(value.ObservedContactMask.ToArray()),
            value.ObservedContactMask.Count(pixel => pixel != 0),
            value.RidgeExteriorArea,
            value.LargestRidgeExteriorArea
        );
    }

    private static byte[] Pixels()
    {
        var pixels = Enumerable.Repeat((byte)255, 800 * 750).ToArray();
        for (var y = 150; y < 550; y++)
        for (var x = 250; x < 550; x++)
            if (Math.Pow((x - 400) / 150.0, 2) + Math.Pow((y - 350) / 200.0, 2) <= 1)
                pixels[y * 800 + x] = (byte)Math.Round(145 + 65 * Math.Sin(x * Math.PI / 4));
        return pixels;
    }

    private static Fs50WholeContactIdentity Identity(CapturedFingerprint capture)
    {
        var evidence = capture.Processing.Value;
        var source = evidence.Artifact.Source;
        var contour = evidence.Contour;
        return new Fs50WholeContactIdentity(
            contour.SourceDigest,
            source.Sequence,
            source.CapturedAt,
            contour.ContactDigest,
            contour.FeatureDigest,
            contour.EvidencePolicyDigest,
            contour.Version,
            SessionId,
            SessionKey(capture),
            (byte)source.Exposure
        );
    }

    private static string SessionKey(CapturedFingerprint capture) =>
        Fs50EmptySensorKey.Create(capture.Frame, SessionId).Value.Digest;
}
