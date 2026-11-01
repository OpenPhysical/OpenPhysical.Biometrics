// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Tool.Commands;

using System.Collections.Immutable;
using CSharpFunctionalExtensions;
using Microsoft.Extensions.Logging.Abstractions;
using OpenPhysical.Biometrics.Facial;

// Prepares a PIV portrait: validate arguments, run the model, then stage the requested outputs.
internal static class FacePreparationCommand
{
    private const int ReviewExitCode = 2;

    internal static Result<CommandOutcome, ToolError> Run(string[] args) =>
        args is ["--help"] or { Length: 0 }
            ? CommandOutcome.Of(0, FaceArguments.Usage).Succeed()
            : FaceArgumentParser
                .Parse(args)
                .Bind(FacePaths.Validate)
                .Bind(arguments =>
                    FaceMetadata
                        .ReadOptional<ReviewedPortraitGeometry>(arguments.Geometry)
                        .Bind(geometry => Prepare(arguments, geometry))
                        .Bind(result => Complete(arguments, result))
                );

    private static Result<PortraitPreparationResult, ToolError> Prepare(
        FaceArguments arguments,
        Maybe<ReviewedPortraitGeometry> geometry
    ) =>
        ToolEdge
            .Try(
                () => File.ReadAllBytes(Path.GetFullPath(arguments.Input)),
                ToolErrors.FaceFileAccess()
            )
            .Bind(bytes =>
            {
                using var service = new PortraitPreparer(NullLogger<PortraitPreparer>.Instance);
                return service
                    .Prepare(bytes, geometry, arguments.FaceIndex)
                    .MapError(ToolErrors.FromFacial);
            });

    private static Result<CommandOutcome, ToolError> Complete(
        FaceArguments arguments,
        PortraitPreparationResult result
    ) =>
        result.Status != PortraitStatus.Prepared
            ? Outcome(arguments, result, ReviewExitCode)
            : FaceOutputPlanner
                .Plan(arguments, result)
                .Bind(plan =>
                    plan.Review.HasValue
                        ? Outcome(arguments, plan.Review.Value, ReviewExitCode)
                        : Install(arguments, result, plan.Outputs)
                );

    private static Result<CommandOutcome, ToolError> Install(
        FaceArguments arguments,
        PortraitPreparationResult result,
        ImmutableArray<(string Path, byte[] Bytes)> outputs
    ) =>
        OutputStaging.Write(outputs, arguments.Overwrite).Bind(() => Outcome(arguments, result, 0));

    private static Result<CommandOutcome, ToolError> Outcome(
        FaceArguments arguments,
        PortraitPreparationResult result,
        int exitCode
    ) => FaceReport.Lines(result, arguments).Map(lines => new CommandOutcome(exitCode, lines));
}
