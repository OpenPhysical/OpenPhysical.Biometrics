// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Desktop;

using System.Collections.Immutable;
using CSharpFunctionalExtensions;
using OpenPhysical.Biometrics.Formats.Fingerprints;

/// <summary>Builds the unsigned ANSI 378 record displayed by the capture workspace.</summary>
public static class UnsignedTemplateService
{
    /// <summary>Creates one or two canonical finger views without changing their selected points.</summary>
    /// <param name="captures">The selected captures in export order.</param>
    /// <param name="metadata">The supplied product and acquisition identifiers.</param>
    /// <returns>The unsigned record or an invalid capture or metadata failure.</returns>
    public static Result<FingerprintRecord, DesktopError> Create(
        ImmutableArray<CapturedFingerprint> captures,
        TemplateMetadata metadata
    )
    {
        if (metadata.ProductOwner == 0 || metadata.EquipmentId > 0x0FFF)
            return new DesktopError(
                "Template.Metadata",
                "Supply a nonzero product owner and a 12-bit equipment identifier."
            );
        if (!ValidCaptures(captures))
            return new DesktopError(
                "Template.Captures",
                "Select one or two different captured fingers with valid image coordinates."
            );
        var record = new FingerprintRecord(
            metadata.ProductOwner,
            metadata.ProductType,
            metadata.EquipmentId,
            (ushort)captures.Max(capture => capture.Frame.Width),
            (ushort)captures.Max(capture => capture.Frame.Height),
            197,
            197,
            captures.Select(capture => capture.View).ToImmutableArray()
        );
        return Incits378Codec
            .Write(record)
            .MapError(error => new DesktopError("Template.Record", error.Message))
            .Map(_ => record);
    }

    internal static bool ValidCaptures(ImmutableArray<CapturedFingerprint> captures) =>
        !captures.IsDefaultOrEmpty
        && captures.Length <= 2
        && captures.Select(capture => capture.Finger).Distinct().Count() == captures.Length
        && captures.All(ValidCapture);

    private static bool ValidCapture(CapturedFingerprint capture) =>
        (byte)capture.Finger is >= 1 and <= 10
        && capture.View.FingerPosition == (byte)capture.Finger
        && capture.View.ViewNumber == 0
        && capture.View.ImpressionType == 0
        && capture.View.Quality == capture.Quality.PivQuality
        && !capture.View.Minutiae.IsDefaultOrEmpty
        && capture.View.Minutiae.Length <= 128
        && !capture.View.ExtendedBlocks.IsDefault
        && capture.View.ExtendedBlocks.IsEmpty
        && capture.Frame.Width <= ushort.MaxValue
        && capture.Frame.Height <= ushort.MaxValue
        && capture.View.Minutiae.All(point =>
            point.X < capture.Frame.Width && point.Y < capture.Frame.Height
        );
}
