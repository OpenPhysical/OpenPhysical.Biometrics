// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Desktop;

using CSharpFunctionalExtensions;
using OpenPhysical.Biometrics.Capture.Camera;
using OpenPhysical.Biometrics.Facial;

internal sealed record FacePolicySnapshot(
    PortraitPreparationResult Portrait,
    PortraitAnalysis Analysis,
    Maybe<PortraitQualityMeasurements> Quality,
    Maybe<CameraFrame> Source
)
{
    internal static FacePolicySnapshot From(FaceCaptureDraft draft) =>
        new(draft.Portrait.Value, draft.Analysis.Value, draft.Quality, draft.Source);

    internal bool Matches(FaceCaptureDraft draft) =>
        Same(draft.Portrait, Portrait) && Same(draft.Source, Source);

    private static bool Same<T>(Maybe<T> first, Maybe<T> second)
        where T : class =>
        first.Match(
            value => second.Match(other => ReferenceEquals(value, other), () => false),
            () => second.HasNoValue
        );
}
