// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Desktop;

using System.Text.Json;
using CSharpFunctionalExtensions;
using OpenPhysical.Biometrics.Capture.Futronic;

internal static class TemplateOriginalQualification
{
    internal static Result<JsonElement, DesktopError> Write(CapturedFingerprint capture)
    {
        var evidence = capture.Processing.Value;
        return Check(
                evidence.InputRole,
                evidence.OriginalQualification,
                evidence.Contour,
                evidence.ContactDiagnostics.ObservedContactMask,
                TemplateObservedContact.From(evidence.ContactDiagnostics),
                TemplateQualificationSource.From(capture.Frame, evidence.Artifact.Source),
                evidence.OriginalAcquisitionPolicy
            )
            .Map(() =>
                evidence.OriginalQualification.Match(
                    TemplateWholeContactMetadata.Write,
                    TemplateOptionalHashMetadata.Empty
                )
            );
    }

    internal static Result<TemplateProcessingDetails, DesktopError> Attach(
        TemplateProcessingManifest manifest,
        TemplateManifestImage image,
        TemplateProcessingDetails details,
        Fs50WholeContactRidgeEvidence ridgeEvidence
    )
    {
        if (
            !Enum.TryParse<CaptureProcessingInputRole>(manifest.InputRole, out var role)
            || !Enum.IsDefined(role)
            || manifest.InputRole != role.ToString()
        )
            return Invalid();
        return TemplateQualificationSource
            .Read(image, details.Source)
            .Bind(source =>
                ReadRecord(manifest.OriginalQualification)
                    .Check(record =>
                        Check(
                            role,
                            record,
                            details.Contour.Value,
                            details.ObservedContactMask,
                            ridgeEvidence,
                            source,
                            details.OriginalAcquisitionPolicy
                        )
                    )
                    .Map(
                        (Maybe<Fs50WholeContactRecord> record) =>
                            details with
                            {
                                InputRole = role,
                                OriginalQualification = record,
                            }
                    )
            );
    }

    private static Result<Maybe<Fs50WholeContactRecord>, DesktopError> ReadRecord(JsonElement value)
    {
        if (value.ValueKind == JsonValueKind.Null)
            return Maybe<Fs50WholeContactRecord>.None;
        if (value.ValueKind != JsonValueKind.Object)
            return Invalid();
        var record = TemplateWholeContactMetadata.Read(value);
        return record.IsFailure
            ? record.Error
            : Result.Success<Maybe<Fs50WholeContactRecord>, DesktopError>(Maybe.From(record.Value));
    }

    private static UnitResult<DesktopError> Check(
        CaptureProcessingInputRole role,
        Maybe<Fs50WholeContactRecord> record,
        Fs50ContactContourArtifact contour,
        System.Collections.Immutable.ImmutableArray<byte> observedContact,
        Fs50WholeContactRidgeEvidence ridgeEvidence,
        TemplateQualificationSource source,
        Maybe<CaptureAcquisitionPolicy> originalAcquisition
    )
    {
        if (role == CaptureProcessingInputRole.DeclaredOfflineImage)
            return record.HasNoValue ? UnitResult.Success<DesktopError>() : Invalid();
        if (role != CaptureProcessingInputRole.LiveSensor || record.HasNoValue)
            return Invalid();
        var value = record.Value;
        if (
            !value.Qualified
            || value.RidgeEvidence is null
            || !TemplateObservedContact.Same(value.RidgeEvidence, ridgeEvidence)
            || value.Policy is null
            || originalAcquisition.HasNoValue
            || !TemplatePngFiles.Same(
                value.PolicyDigest,
                originalAcquisition.Value.Settings.WholeContact.Digest
            )
        )
            return Invalid();
        return value
            .Validate(contour, observedContact.AsSpan())
            .MapError(error => TemplatePngFiles.Invalid(error.Message))
            .Bind(() => source.Check(value));
    }

    private static DesktopError Invalid() =>
        TemplatePngFiles.Invalid(
            "The declared input role and original whole-contact qualification do not agree with the acquisition."
        );
}
