// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Capture.Futronic;

/// <summary>Source and geometry failures for contact-contour evidence.</summary>
public static class Fs50ContactContourErrorCodes
{
    /// <summary>Original pixels and contact evidence do not share one source.</summary>
    public const string SourceMismatch = "capture.ContactContourSourceMismatch";

    /// <summary>No selected contact exists from which to construct image context.</summary>
    public const string NoSupport = "capture.NoContactContour";
}
