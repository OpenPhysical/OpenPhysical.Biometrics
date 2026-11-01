// SPDX-License-Identifier: GPL-2.0-or-later
// Copyright (c) 2026 Mistial Developer

namespace OpenPhysical.Biometrics.Piv;

using System.Collections.Immutable;
using OpenPhysical.Biometrics.Formats;
using OpenPhysical.Biometrics.Formats.Cbeff;
using OpenPhysical.Biometrics.Formats.Faces;
using OpenPhysical.Biometrics.Formats.Fingerprints;

/// <summary>Identifies a failed biometric profile requirement.</summary>
/// <param name="Code">Stable diagnostic code.</param>
/// <param name="Field">The affected typed field path.</param>
public sealed record BiometricProfileDiagnostic(string Code, string Field);

/// <summary>Applies the SP 800-76-2 PIV record profiles independently of cryptographic verification.</summary>
public static class PivBiometricProfile
{
    /// <summary>Checks the fingerprint record fields required for new PIV issuance.</summary>
    /// <param name="record">The canonical parsed or prepared record.</param>
    /// <returns>All detected profile errors, with no biometric field contents.</returns>
    /// <exception cref="ArgumentNullException">The record is null.</exception>
    public static ImmutableArray<BiometricProfileDiagnostic> Validate(FingerprintRecord record)
    {
        ArgumentNullException.ThrowIfNull(record);
        var errors = ValidateFingerprintHeader(record).ToBuilder();
        if (record.Views.IsDefault || record.Views.Length != 2)
        {
            errors.Add(new("TwoFingerViews", "Views"));
            return errors.ToImmutable();
        }
        if (record.Views.Any(view => view is null))
        {
            errors.Add(new("MissingFingerView", "Views"));
            return errors.ToImmutable();
        }
        if (record.Views.Select(view => view.FingerPosition).Distinct().Count() != 2)
            errors.Add(new("DistinctFingers", "Views"));
        if (record.Views[0].Quality is 254 or 255 && record.Views[1].Quality <= 100)
            errors.Add(new("AvailableFingerOrder", "Views"));
        for (var i = 0; i < record.Views.Length; i++)
        {
            var view = record.Views[i];
            errors.AddRange(
                Validate(view)
                    .Select(diagnostic =>
                        diagnostic with
                        {
                            Field = $"Views[{i}].{diagnostic.Field}",
                        }
                    )
            );
        }
        // The codec is authoritative for representability, coordinates and angles.
        try
        {
            if (Incits378Codec.Write(record).Length > 1574)
                errors.Add(new("RecordLength", "Record"));
        }
        catch (ArgumentException)
        {
            errors.Add(new("InvalidRecord", "Record"));
        }
        return errors.ToImmutable();
    }

    /// <summary>Checks the PIV fingerprint header independently of the number of views.</summary>
    /// <param name="record">A canonical record carrying the original header fields.</param>
    /// <returns>Profile findings for the supplied header.</returns>
    /// <exception cref="ArgumentNullException">The record is null.</exception>
    public static ImmutableArray<BiometricProfileDiagnostic> ValidateFingerprintHeader(
        FingerprintRecord record
    )
    {
        ArgumentNullException.ThrowIfNull(record);
        var errors = ImmutableArray.CreateBuilder<BiometricProfileDiagnostic>();
        if (record.ProductOwner == 0 || record.ProductType == 0)
            errors.Add(new("ProductIdentifier", "Product"));
        if ((record.CaptureEquipment & 0xf000) != 0x8000 || (record.CaptureEquipment & 0x0fff) == 0)
            errors.Add(new("CaptureEquipment", "CaptureEquipment"));
        if (record.XResolution != 197 || record.YResolution != 197)
            errors.Add(new("Resolution", "Resolution"));
        if (record.Width == 0 || record.Height == 0)
            errors.Add(new("ImageDimensions", "Dimensions"));
        return errors.ToImmutable();
    }

    /// <summary>Checks the intrinsic PIV requirements of one selected finger view.</summary>
    /// <param name="view">The canonical view with explicit quality and availability values.</param>
    /// <returns>View profile findings; source-geometry checks remain part of full record validation.</returns>
    /// <exception cref="ArgumentNullException">The view is null.</exception>
    public static ImmutableArray<BiometricProfileDiagnostic> Validate(FingerprintView view)
    {
        ArgumentNullException.ThrowIfNull(view);
        var errors = ImmutableArray.CreateBuilder<BiometricProfileDiagnostic>();
        if (view.FingerPosition is < 1 or > 10)
            errors.Add(new("FingerPosition", "FingerPosition"));
        if (view.ViewNumber != 0)
            errors.Add(new("ViewNumber", "ViewNumber"));
        if (view.ImpressionType is not (0 or 2))
            errors.Add(new("PlainImpression", "ImpressionType"));
        if (view.Quality is not (20 or 40 or 60 or 80 or 100 or 254 or 255))
            errors.Add(new("Nfiq1Quality", "Quality"));
        if (view.Minutiae.IsDefault || view.Minutiae.Length > 128)
            errors.Add(new("MinutiaCount", "Minutiae"));
        if (view.ExtendedBlocks.IsDefault || !view.ExtendedBlocks.IsEmpty)
            errors.Add(new("ExtendedData", "ExtendedBlocks"));
        if (!view.Minutiae.IsDefault && view.Quality is 254 or 255 && !view.Minutiae.IsEmpty)
            errors.Add(new("UnavailableFingerMinutiae", "Minutiae"));
        if (!view.Minutiae.IsDefault && view.Minutiae.Any(point => point is null))
            errors.Add(new("MissingMinutia", "Minutiae"));
        if (
            !view.Minutiae.IsDefault
            && view.Minutiae.All(point => point is not null)
            && view.Minutiae.Select(point => (point.X, point.Y)).Distinct().Count()
                != view.Minutiae.Length
        )
            errors.Add(new("DuplicateMinutiae", "Minutiae"));
        return errors.ToImmutable();
    }

    /// <summary>Checks the encoded facial record metadata required by the PIV profile.</summary>
    /// <param name="record">The canonical facial record.</param>
    /// <returns>Metadata/profile errors, separate from acquisition and image-processing assessments.</returns>
    /// <exception cref="ArgumentNullException">The record is null.</exception>
    public static ImmutableArray<BiometricProfileDiagnostic> Validate(FaceRecord record)
    {
        ArgumentNullException.ThrowIfNull(record);
        var errors = ImmutableArray.CreateBuilder<BiometricProfileDiagnostic>();
        if (record.Images.IsDefaultOrEmpty)
        {
            errors.Add(new("FacialImageRequired", "Images"));
            return errors.ToImmutable();
        }
        for (var i = 0; i < record.Images.Length; i++)
        {
            var image = record.Images[i];
            var path = $"Images[{i}]";
            if (image is null || image.Information is null || image.ImageInformation is null)
            {
                errors.Add(new("MissingFacialMetadata", path));
                continue;
            }
            var info = image.ImageInformation;
            if (info.FaceImageType != 1)
                errors.Add(new("FullFrontalImage", path));
            if (info.ImageDataType is not (0 or 1))
                errors.Add(new("ImageEncoding", path));
            if (info.ColorSpace != 1)
                errors.Add(new("SrgbColorSpace", path));
            if (info.SourceType is not (2 or 6))
                errors.Add(new("CaptureSource", path));
            if (info.Width <= 420 || info.Height == 0)
                errors.Add(new("ImageDimensions", path));
            if (info.Quality > 100)
                errors.Add(new("ImageQuality", path));
            if (image.Information.Expression != 1)
                errors.Add(new("NeutralExpression", path));
            if (image.Information.PoseAngle != 0)
                errors.Add(new("FrontalPose", path));
            if (image.Information.PoseUncertainty != 0)
                errors.Add(new("PoseUncertainty", path));
            if (image.ImageData.IsDefaultOrEmpty)
                errors.Add(new("ImageDataRequired", path));
        }
        try
        {
            _ = Incits385Codec.Write(record);
        }
        catch (ArgumentException)
        {
            errors.Add(new("InvalidRecord", "Record"));
        }
        return errors.ToImmutable();
    }

    /// <summary>Checks common signed PIV header fields and the enclosed biometric profile.</summary>
    /// <param name="record">The raw CBEFF record.</param>
    /// <param name="evaluationTime">The explicit instant for validity evaluation.</param>
    /// <returns>Header and biometric profile findings, independently of signature-service findings.</returns>
    /// <exception cref="ArgumentNullException">The record is null.</exception>
    public static ImmutableArray<BiometricProfileDiagnostic> Validate(
        CbeffRecord record,
        DateTimeOffset evaluationTime
    )
    {
        ArgumentNullException.ThrowIfNull(record);
        ArgumentNullException.ThrowIfNull(record.Header);
        var errors = ImmutableArray.CreateBuilder<BiometricProfileDiagnostic>();
        var header = record.Header;
        if (header.PatronHeaderVersion != 3)
            errors.Add(new("PatronHeaderVersion", "Header"));
        if (header.SecurityOptions != 0x0D)
            errors.Add(new("SignedPivSecurityOptions", "Header"));
        if (header.FormatOwner != 0x001B)
            errors.Add(new("FormatOwner", "Header"));
        if (header.Reserved.Any(value => value != 0))
            errors.Add(new("ReservedBytes", "Header"));
        if (
            header.ValidityNotBefore > header.ValidityNotAfter
            || evaluationTime < header.ValidityNotBefore
            || evaluationTime > header.ValidityNotAfter
        )
            errors.Add(new("ValidityPeriod", "Header"));
        if (header.CreationDate > evaluationTime)
            errors.Add(new("AcquisitionDate", "Header"));
        if (header.ValidityNotBefore < header.CreationDate)
            errors.Add(new("ValidityStartsBeforeAcquisition", "Header"));
        var creator = header.Creator;
        var terminator = creator.AsSpan().IndexOf((byte)0);
        if (terminator < 0 || creator.Take(terminator).Any(value => value is < 0x20 or > 0x7E))
            errors.Add(new("CreatorEncoding", "Header"));
        if (header.Quality is < -2 or > 100)
            errors.Add(new("BiometricQuality", "Header"));
        if (
            record.BiometricData.IsDefault
            || header.BiometricDataLength != record.BiometricData.Length
        )
            errors.Add(new("BiometricDataLength", "Header"));
        if (
            record.Signature.IsDefault
            || header.SignatureLength != record.Signature.Length
            || record.Signature.IsEmpty
        )
            errors.Add(new("SignatureBlockLength", "Header"));
        switch (header.FormatType)
        {
            case 0x0201:
                if (header.Quality is not (20 or 40 or 60 or 80 or 100 or -1 or -2))
                    errors.Add(new("FingerprintHeaderQuality", "Header"));
                if ((header.BiometricDataType & 0xE0) != 0x80)
                    errors.Add(new("ProcessedFingerprintData", "Header"));
                if (header.BiometricType != 8)
                    errors.Add(new("FingerprintModality", "Header"));
                var fingerprint = Incits378Codec.Parse(record.BiometricData.AsSpan());
                if (fingerprint.Value is not null)
                {
                    errors.AddRange(Validate(fingerprint.Value));
                    var views = fingerprint.Value.Views;
                    if (!views.IsDefaultOrEmpty && views.All(view => view is not null))
                    {
                        var allUnavailable = views.All(view => view.Quality is 254 or 255);
                        var unavailableQuality = views.Any(view => view.Quality == 255) ? -1 : -2;
                        if (
                            allUnavailable
                                ? header.Quality != unavailableQuality
                                : header.Quality < 0
                        )
                            errors.Add(new("FingerprintHeaderQuality", "Header"));
                    }
                }
                else
                    errors.AddRange(
                        fingerprint.Diagnostics.Select(d => new BiometricProfileDiagnostic(
                            d.Code,
                            "BiometricData"
                        ))
                    );
                break;
            case 0x0501:
                if (header.Quality == -1)
                    errors.Add(new("FacialHeaderQuality", "Header"));
                if ((header.BiometricDataType & 0xE0) != 0x20)
                    errors.Add(new("RawFacialData", "Header"));
                if (header.BiometricType != 2)
                    errors.Add(new("FacialModality", "Header"));
                var face = Incits385Codec.Parse(record.BiometricData.AsSpan());
                if (face.Value is not null)
                    errors.AddRange(Validate(face.Value));
                else
                    errors.AddRange(
                        face.Diagnostics.Select(d => new BiometricProfileDiagnostic(
                            d.Code,
                            "BiometricData"
                        ))
                    );
                break;
            default:
                errors.Add(new("BiometricFormat", "Header"));
                break;
        }
        return errors.ToImmutable();
    }
}
