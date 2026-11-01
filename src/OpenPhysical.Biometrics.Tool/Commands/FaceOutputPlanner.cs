// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Tool.Commands;

using System.Collections.Immutable;
using CSharpFunctionalExtensions;
using Microsoft.Extensions.Logging.Abstractions;
using OpenPhysical.Biometrics.Facial;
using OpenPhysical.Biometrics.Formats.Faces;

// Encodes the prepared pixels into the requested files, or reports why an issuance record needs review.
internal static class FaceOutputPlanner
{
    internal static Result<FaceOutputPlan, ToolError> Plan(
        FaceArguments arguments,
        PortraitPreparationResult result
    ) =>
        (
            arguments.Record.HasValue
                ? RecordOutputs(arguments, result)
                : Jp2Outputs(arguments, result)
        ).Bind(plan => AddPreview(arguments, result, plan));

    private static Result<FaceOutputPlan, ToolError> Jp2Outputs(
        FaceArguments arguments,
        PortraitPreparationResult result
    ) =>
        PortraitEncoding
            .EncodeJpeg2000(result.RgbPixels.AsSpan(), 480, 640)
            .MapError(ToolErrors.FromFacial)
            .Map(jp2 => FaceOutputPlan.Write([(arguments.Output, jp2)]));

    private static Result<FaceOutputPlan, ToolError> RecordOutputs(
        FaceArguments arguments,
        PortraitPreparationResult result
    )
    {
        var reviewed = result
            .Measurements.Select(measurements => measurements.ReviewedHeadBoundary)
            .GetValueOrDefault(false);
        if (!reviewed)
            return Review(
                    result,
                    "ReviewHeadBoundary",
                    "Confirm crown and both ear attachments before preparing an issuance record."
                )
                .Succeed();
        return arguments
            .Acquisition.ToResult(ToolErrors.FaceMetadata())
            .Bind(FaceMetadata.Read<FacialAcquisitionMetadata>)
            .Bind(acquisition => Record(arguments, result, acquisition));
    }

    private static Result<FaceOutputPlan, ToolError> Record(
        FaceArguments arguments,
        PortraitPreparationResult result,
        FacialAcquisitionMetadata acquisition
    ) =>
        new FacialRecordPreparer(NullLogger<FacialRecordPreparer>.Instance)
            .Prepare(result.RgbPixels.AsSpan(), 480, 640, acquisition)
            .MapError(ToolErrors.FromFacial)
            .Bind(prepared =>
                prepared.Status != FacialPreparationStatus.Prepared
                    ? Review(
                            result,
                            prepared.Status.ToString(),
                            "Check acquisition evidence and encoded profile findings."
                        )
                        .Succeed()
                    : prepared
                        .Record.ToResult(ToolErrors.FacePreparation())
                        .Bind(record => Encode(arguments, record))
            );

    private static Result<FaceOutputPlan, ToolError> Encode(
        FaceArguments arguments,
        FaceRecord record
    ) =>
        arguments
            .Record.ToResult(ToolErrors.FaceInput())
            .Bind(recordPath =>
                Incits385Codec
                    .Write(record)
                    .MapError(_ => ToolErrors.FaceInput())
                    .Map(encoded =>
                        FaceOutputPlan.Write([
                            (recordPath, encoded),
                            (arguments.Output, record.Images[0].ImageData.ToArray()),
                        ])
                    )
            );

    private static Result<FaceOutputPlan, ToolError> AddPreview(
        FaceArguments arguments,
        PortraitPreparationResult result,
        FaceOutputPlan plan
    ) =>
        plan.Review.HasValue || arguments.Preview.HasNoValue
            ? plan.Succeed()
            : PortraitEncoding
                .EncodePng(result.RgbPixels.AsSpan(), 480, 640)
                .MapError(ToolErrors.FromFacial)
                .Map(png =>
                    FaceOutputPlan.Write([.. plan.Outputs, (arguments.Preview.Value, png)])
                );

    private static FaceOutputPlan Review(
        PortraitPreparationResult result,
        string code,
        string message
    ) =>
        FaceOutputPlan.NeedsReview(
            result with
            {
                Status = PortraitStatus.ReviewRequired,
                Findings = [new PortraitFinding(code, message)],
            }
        );
}
