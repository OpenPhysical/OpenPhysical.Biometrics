// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Desktop;

using System.Collections.Immutable;
using CSharpFunctionalExtensions;
using OpenPhysical.Biometrics.Capture;
using OpenPhysical.Biometrics.Capture.Futronic;

/// <summary>Explicit input provenance and original per-frame physical qualification capabilities.</summary>
public sealed class CaptureProcessingInput
{
    private CaptureProcessingInput(
        CaptureProcessingInputRole role,
        ImmutableArray<Fs50WholeContactProof> proofs
    )
    {
        Role = role;
        Proofs = proofs;
    }

    /// <summary>Gets an honest offline declaration without physical sensor qualification.</summary>
    public static CaptureProcessingInput DeclaredOfflineImage { get; } =
        new(CaptureProcessingInputRole.DeclaredOfflineImage, []);

    /// <summary>Gets the declared input role.</summary>
    public CaptureProcessingInputRole Role { get; }

    /// <summary>Gets original capabilities for the acknowledged frozen frames, never restored metadata.</summary>
    public ImmutableArray<Fs50WholeContactProof> Proofs { get; }

    /// <summary>Declares physical sensor inputs; absent proof remains explicitly unqualified.</summary>
    /// <param name="proofs">Immutable original per-frame capabilities for this frozen attempt.</param>
    /// <returns>Live input provenance, validated against each exact frame before native work.</returns>
    public static CaptureProcessingInput LiveSensor(ImmutableArray<Fs50WholeContactProof> proofs) =>
        new(CaptureProcessingInputRole.LiveSensor, proofs);

    internal static CaptureProcessingInput ForRevision(
        CaptureProcessingInputRole role,
        Maybe<Fs50WholeContactProof> proof
    ) => new(role, proof.Match(value => ImmutableArray.Create(value), () => []));

    internal Result<Maybe<Fs50WholeContactProof>, DesktopError> Resolve(
        CaptureFrame frame,
        Fs50ContactContourArtifact contour
    )
    {
        if (!Enum.IsDefined(Role) || Proofs.IsDefault || Proofs.Any(proof => proof is null))
            return new DesktopError("Capture.ProcessingInput", "Input evidence is invalid.");
        if (Role == CaptureProcessingInputRole.DeclaredOfflineImage)
            return Proofs.IsEmpty
                ? Maybe<Fs50WholeContactProof>.None
                : Result.Failure<Maybe<Fs50WholeContactProof>, DesktopError>(Unqualified());
        var matching = Proofs
            .Where(proof =>
                proof.Record.Identity.SourceDigest == contour.SourceDigest
                && proof.Record.Identity.Sequence == frame.Sequence
                && proof.Record.Identity.CapturedAt == frame.CapturedAt
            )
            .Take(2)
            .ToArray();
        return matching.Length == 1
            ? Validate(Role, matching[0], frame, contour).Map(() => Maybe.From(matching[0]))
            : Result.Failure<Maybe<Fs50WholeContactProof>, DesktopError>(Unqualified());
    }

    internal static UnitResult<DesktopError> Validate(
        CaptureProcessingInputRole role,
        Maybe<Fs50WholeContactProof> proof,
        CaptureFrame frame,
        Fs50ContactContourArtifact contour
    ) =>
        role == CaptureProcessingInputRole.DeclaredOfflineImage && proof.HasNoValue
            ? UnitResult.Success<DesktopError>()
        : role == CaptureProcessingInputRole.LiveSensor && proof.HasValue && proof.Value.Qualified
            ? proof
                .Value.ValidateFor(frame, contour)
                .MapError(error => new DesktopError(error.Code, error.Message))
        : UnitResult.Failure(Unqualified());

    private static DesktopError Unqualified() =>
        new(
            "Capture.WholeContactUnqualified",
            "The complete finger contact has not been qualified for this acquisition. Clean the sensor and try again."
        );
}
