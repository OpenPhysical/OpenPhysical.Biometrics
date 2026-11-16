// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Packages;

/// <summary>A stable package failure without secret, identity, path, or biometric contents.</summary>
/// <param name="Code">Machine-readable failure identifier.</param>
/// <param name="Message">Operator-safe explanation.</param>
public sealed record PackageError(string Code, string Message);
