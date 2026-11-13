// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Capture.Camera;

using CSharpFunctionalExtensions;

/// <summary>A safe camera failure without native exception or captured image details.</summary>
/// <param name="Code">Stable machine-readable code.</param>
/// <param name="Message">Operator-safe explanation.</param>
public sealed record CameraError(string Code, string Message)
{
    /// <summary>Gets optional scalar native layout evidence without retaining image bytes or hashes.</summary>
    public Maybe<CameraNativeLayout> NativeLayout { get; init; }
}
