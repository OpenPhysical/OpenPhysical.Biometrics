// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Desktop.Tests;

using System.Collections.Immutable;
using CSharpFunctionalExtensions;
using OpenPhysical.Biometrics.Capture;

internal sealed record EvaluationInput(
    string Id,
    string FingerId,
    CaptureFrame Frame,
    ImmutableArray<byte> WholeMask,
    Maybe<DesktopError> LegacyRejection
);
