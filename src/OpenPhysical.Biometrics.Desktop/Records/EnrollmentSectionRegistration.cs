// SPDX-License-Identifier: GPL-2.0-or-later
using CSharpFunctionalExtensions;

namespace OpenPhysical.Biometrics.Desktop;

/// <summary>Creates modality adapters for the shared public person-session contract.</summary>
public static class EnrollmentSectionRegistration
{
    /// <summary>Registers an attended face capture workflow without duplicating its writer or lifecycle.</summary>
    /// <param name="capture">The face workflow belonging to the same person model.</param>
    /// <returns>A section that joins the central encrypted-package transaction.</returns>
    public static IEnrollmentSection Face(FaceCaptureViewModel capture) =>
        new FaceEnrollmentSection(capture);

    internal static IEnrollmentSection Fingerprints(
        CaptureViewModel capture,
        EnrollmentFingerSuggestions suggestions,
        PersonEnrollmentViewModel person,
        Func<Result<TemplateMetadata, DesktopError>> metadata,
        Func<TemplateSaveOptions> options
    ) =>
        new FingerprintEnrollmentSection(
            new FingerprintEnrollmentViewModel(
                () => capture.Captured,
                capture,
                suggestions,
                person
            ),
            capture,
            metadata,
            options
        );
}
