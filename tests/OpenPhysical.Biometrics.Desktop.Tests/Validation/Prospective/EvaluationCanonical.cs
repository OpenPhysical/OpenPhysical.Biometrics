// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Desktop.Tests;

using CSharpFunctionalExtensions;
using OpenPhysical.Biometrics.Fingerprints;
using OpenPhysical.Biometrics.NBIS;

internal static class EvaluationCanonical
{
    internal static Result<FingerprintTemplate, DesktopError> Convert(
        CapturedFingerprint capture
    ) =>
        UnsignedTemplateService
            .Create([capture], new(0x0103, 0, 0))
            .Bind(record =>
                record
                    .ToMatchingTemplate(FingerPosition.RightIndex, 0)
                    .MapError(error => new DesktopError(error.Code, error.Message))
            );
}
