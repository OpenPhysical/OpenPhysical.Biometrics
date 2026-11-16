// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Facial;

using System.Collections.Immutable;
using CSharpFunctionalExtensions;

/// <summary>Explicit acknowledgement of automatic failures for one unsigned source capture.</summary>
public sealed record FacialCaptureOverride
{
    private FacialCaptureOverride(
        string sourceSha256,
        ImmutableArray<string> codes,
        DateTimeOffset time,
        PortraitMouthPolicy mouthPolicy
    )
    {
        SourceSha256 = sourceSha256;
        FindingCodes = codes;
        AcknowledgedAt = time;
        MouthPolicy = mouthPolicy;
    }

    /// <summary>Gets the exact original image identity acknowledged by the operator.</summary>
    public string SourceSha256 { get; }

    /// <summary>Gets the complete, deterministically ordered automatic failure set.</summary>
    public ImmutableArray<string> FindingCodes { get; }

    /// <summary>Gets when the operator explicitly chose the override.</summary>
    public DateTimeOffset AcknowledgedAt { get; }

    /// <summary>Gets the mouth tolerance in effect when the failures were acknowledged.</summary>
    public PortraitMouthPolicy MouthPolicy { get; }

    /// <summary>Creates evidence only for eligible failures with a real source-supported inspection portrait.</summary>
    /// <param name="portrait">Rejected capture, retaining pixels, transform and findings.</param>
    /// <param name="acknowledgedAt">Explicit acknowledgement time.</param>
    /// <returns>Immutable evidence, or a specific eligibility failure.</returns>
    public static Result<FacialCaptureOverride, FacialError> Create(
        PortraitPreparationResult portrait,
        DateTimeOffset acknowledgedAt
    ) =>
        Codes(portrait)
            .Map(codes => new FacialCaptureOverride(
                portrait.SourceSha256,
                codes,
                acknowledgedAt,
                portrait.MouthPolicy
            ));

    /// <summary>Rejects evidence transferred to another image or a changed failure set.</summary>
    /// <param name="portrait">The current source-bound inspection portrait.</param>
    /// <returns>Success only while this exact acknowledgement still applies.</returns>
    public UnitResult<FacialError> Validate(PortraitPreparationResult portrait)
    {
        var current = Codes(portrait);
        return
            current.IsSuccess
            && portrait.SourceSha256 == SourceSha256
            && portrait.MouthPolicy == MouthPolicy
            && current.Value.SequenceEqual(FindingCodes, StringComparer.Ordinal)
            ? UnitResult.Success<FacialError>()
            : UnitResult.Failure(Invalid());
    }

    private static Result<ImmutableArray<string>, FacialError> Codes(
        PortraitPreparationResult portrait
    )
    {
        if (!PortraitInspectionData.IsUsable(portrait))
            return Invalid();
        var failures = portrait
            .Findings.Where(f => f.Action == PortraitFindingAction.Retake)
            .ToArray();
        if (
            portrait.Status != PortraitStatus.ReviewRequired
            || string.IsNullOrWhiteSpace(portrait.SourceSha256)
            || failures.Length == 0
            || failures.Any(f => !f.CanOverride || string.IsNullOrWhiteSpace(f.Code))
            || portrait.Findings.Any(f =>
                f.Action != PortraitFindingAction.Retake && f.Code != "EstimatedHeadBoundary"
            )
        )
            return Invalid();
        return failures
            .Select(f => f.Code)
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToImmutableArray();
    }

    private static FacialError Invalid() =>
        new(
            FacialErrorCodes.InvalidCaptureOverride,
            "This override does not apply to the current capture and failed checks."
        );
}
