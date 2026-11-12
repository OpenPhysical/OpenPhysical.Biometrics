// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Desktop.Tests;

using System.Collections.Immutable;
using CSharpFunctionalExtensions;

internal static class ProspectivePairs
{
    internal static Result<ImmutableArray<ProspectivePair>, DesktopError> Create(
        ImmutableArray<ProspectiveImage> images,
        ImmutableArray<ProspectiveArm> arms,
        Func<ProspectiveImage, ProspectiveImage, ProspectiveArm, Result<int, DesktopError>> match
    )
    {
        var valid = ProspectiveInputValidation.Check(images, arms);
        if (valid.IsFailure)
            return valid.Error;
        var pairs = ImmutableArray.CreateBuilder<ProspectivePair>();
        for (var left = 0; left < images.Length; left++)
        for (var right = left + 1; right < images.Length; right++)
        {
            var a = images[left];
            var b = images[right];
            var scores = Scores(a, b, arms, match);
            if (scores.IsFailure)
                return scores.Error;
            pairs.Add(
                new(
                    a.Id,
                    b.Id,
                    a.PersonId == b.PersonId && a.FingerId == b.FingerId,
                    a.PersonId != b.PersonId,
                    scores.Value
                )
            );
        }
        return pairs.ToImmutable();
    }

    private static Result<ImmutableArray<ProspectiveScore>, DesktopError> Scores(
        ProspectiveImage a,
        ProspectiveImage b,
        ImmutableArray<ProspectiveArm> arms,
        Func<ProspectiveImage, ProspectiveImage, ProspectiveArm, Result<int, DesktopError>> match
    )
    {
        var scores = ImmutableArray.CreateBuilder<ProspectiveScore>();
        foreach (var arm in arms)
        {
            if (a.For(arm).Template.HasNoValue || b.For(arm).Template.HasNoValue)
            {
                scores.Add(new(arm, Maybe<int>.None));
                continue;
            }
            var score = match(a, b, arm);
            if (score.IsFailure)
                return score.Error;
            if (score.Value < 0)
                return new DesktopError(
                    "Evaluation.InvalidScore",
                    "A matcher score must be nonnegative."
                );
            scores.Add(new(arm, score.Value));
        }
        return scores.ToImmutable();
    }
}
