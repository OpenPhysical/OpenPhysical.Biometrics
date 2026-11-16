// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Fingerprints;

using CSharpFunctionalExtensions;
using OpenPhysical.Biometrics.Capture;

/// <summary>A prepared selection whose acquisition lineage is retained by the processing service.</summary>
public sealed class PreparedAcquiredFingerprint
{
    internal PreparedAcquiredFingerprint(
        FingerprintEnrollmentFinger selection,
        Maybe<CaptureFrame> frame,
        Maybe<FingerprintAcquisitionProfile> profile
    )
    {
        Selection = selection;
        Frame = frame;
        Profile = profile;
    }

    /// <summary>Gets the reusable quality-assessed selection for operator review.</summary>
    public FingerprintEnrollmentFinger Selection { get; }

    internal Maybe<CaptureFrame> Frame { get; }
    internal Maybe<FingerprintAcquisitionProfile> Profile { get; }

    /// <summary>Records an unavailable finger without fabricating acquisition evidence.</summary>
    /// <param name="finger">Selected finger.</param>
    /// <param name="availability">Temporary or permanent unavailability.</param>
    /// <returns>A selection or the existing availability validation error.</returns>
    public static Result<PreparedAcquiredFingerprint, FingerprintError> Unavailable(
        FingerPosition finger,
        FingerprintAvailability availability
    ) =>
        FingerprintEnrollmentFinger
            .Unavailable(finger, availability)
            .Map(selection => new PreparedAcquiredFingerprint(
                selection,
                Maybe<CaptureFrame>.None,
                Maybe<FingerprintAcquisitionProfile>.None
            ));
}
