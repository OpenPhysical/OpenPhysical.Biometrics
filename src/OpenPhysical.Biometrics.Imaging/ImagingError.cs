// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Imaging;

/// <summary>Describes why an imaging operation failed.</summary>
/// <param name="Code">Stable machine-readable identifier, one of <see cref="ImagingErrorCodes"/>.</param>
/// <param name="Message">Human-readable explanation.</param>
public sealed record ImagingError(string Code, string Message);
