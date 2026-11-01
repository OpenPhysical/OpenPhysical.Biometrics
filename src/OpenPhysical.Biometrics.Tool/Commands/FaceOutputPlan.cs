// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Tool.Commands;

using System.Collections.Immutable;
using CSharpFunctionalExtensions;
using OpenPhysical.Biometrics.Facial;

// The files to write for a prepared portrait, or the review outcome that replaces them.
internal sealed record FaceOutputPlan(
    ImmutableArray<(string Path, byte[] Bytes)> Outputs,
    Maybe<PortraitPreparationResult> Review
)
{
    internal static FaceOutputPlan Write(ImmutableArray<(string Path, byte[] Bytes)> outputs) =>
        new(outputs, Maybe<PortraitPreparationResult>.None);

    internal static FaceOutputPlan NeedsReview(PortraitPreparationResult review) => new([], review);
}
