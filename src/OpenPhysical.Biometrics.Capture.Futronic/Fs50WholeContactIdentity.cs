// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Capture.Futronic;

/// <summary>Exact original frame, contact and acquisition-session association.</summary>
/// <param name="SourceDigest">Original corrected image SHA-256.</param>
/// <param name="Sequence">Exact acquisition frame sequence.</param>
/// <param name="CapturedAt">Exact acquisition timestamp.</param>
/// <param name="ContactDigest">Selected contact C SHA-256.</param>
/// <param name="FeatureDigest">Filled feature contour F SHA-256.</param>
/// <param name="EvidencePolicyDigest">Original contact-evidence policy SHA-256.</param>
/// <param name="ContourVersion">Original topology implementation.</param>
/// <param name="SessionId">Original open-scanner-session identity.</param>
/// <param name="SessionKeyDigest">Device, session, calibration, correction, geometry and exposure association.</param>
/// <param name="Exposure">Actual acquisition exposure.</param>
public sealed record Fs50WholeContactIdentity(
    string SourceDigest,
    long Sequence,
    DateTimeOffset CapturedAt,
    string ContactDigest,
    string FeatureDigest,
    string EvidencePolicyDigest,
    string ContourVersion,
    string SessionId,
    string SessionKeyDigest,
    byte Exposure
)
{
    internal static string KeyDigest(Fs50EmptySensorKey key) => key.Digest;
}
