// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Desktop.Tests;

using System.Collections.Immutable;
using CSharpFunctionalExtensions;

internal static class ProspectiveInputValidation
{
    internal static UnitResult<DesktopError> Check(
        ImmutableArray<ProspectiveImage> images,
        ImmutableArray<ProspectiveArm> arms
    )
    {
        if (
            !arms.SequenceEqual(Enum.GetValues<ProspectiveArm>())
            || images.IsDefaultOrEmpty
            || images.Select(image => image.Id).Distinct().Count() != images.Length
        )
            return Invalid();
        foreach (var image in images)
        {
            if (
                string.IsNullOrWhiteSpace(image.PersonId)
                || string.IsNullOrWhiteSpace(image.FingerId)
                || !image.Representations.Select(value => value.Arm).SequenceEqual(arms)
            )
                return Invalid();
            foreach (var arm in image.Representations)
            {
                if (arm.Template.HasValue == arm.Rejection.HasValue)
                    return Invalid();
                if (arm.Rejection.HasValue)
                {
                    var reason = ProspectiveFailure.Classify(arm.Rejection.Value);
                    if (reason.IsFailure)
                        return UnitResult.Failure(reason.Error);
                }
            }
        }
        return UnitResult.Success<DesktopError>();
    }

    private static UnitResult<DesktopError> Invalid() =>
        UnitResult.Failure(
            new DesktopError(
                "Evaluation.Attempts",
                "Every unique identified attempt requires every frozen arm as available or explicitly rejected representations."
            )
        );
}
