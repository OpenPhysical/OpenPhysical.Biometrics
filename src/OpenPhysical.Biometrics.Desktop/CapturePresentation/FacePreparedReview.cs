// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Desktop;

using CSharpFunctionalExtensions;
using OpenPhysical.Biometrics.Facial;

internal sealed record FacePreparedReview(
    PortraitPreparationResult Portrait,
    Maybe<PortraitQualityMeasurements> Quality
);
