// SPDX-License-Identifier: GPL-2.0-or-later

namespace OpenPhysical.Biometrics.Capture;

/// <summary>A stable acquisition failure without raw driver exception details.</summary>
/// <param name="Code">Machine-readable failure code.</param>
/// <param name="Message">Safe explanation of the failure.</param>
public sealed record CaptureError(string Code, string Message);
