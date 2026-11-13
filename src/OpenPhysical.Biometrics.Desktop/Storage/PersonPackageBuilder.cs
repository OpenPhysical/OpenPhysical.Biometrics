// SPDX-License-Identifier: GPL-2.0-or-later
using System.Text.Json;
using CSharpFunctionalExtensions;
using OpenPhysical.Biometrics.Packages;

namespace OpenPhysical.Biometrics.Desktop;

internal static class PersonPackageBuilder
{
    internal static Result<BiometricPackage, DesktopError> Create(
        string id,
        EnrollmentViewModel enrollment
    )
    {
        var person = enrollment.Person.Create();
        var artifacts = enrollment.PrepareArtifacts();
        if (person.IsFailure)
            return person.Error;
        if (artifacts.IsFailure)
            return artifacts.Error;
        var evidence = Evidence(enrollment, AcceptedQualification(enrollment));
        return BiometricPackage
            .Create(
                enrollment.Person.AuthoritativePersonId.Match(
                    value => value.ToString("D"),
                    () => id
                ),
                person.Value.DisplayName,
                string.IsNullOrEmpty(person.Value.SubjectIdentifier)
                    ? Maybe<string>.None
                    : person.Value.SubjectIdentifier,
                evidence,
                artifacts.Value
            )
            .MapError(error => new DesktopError(error.Code, error.Message));
    }

    private static Maybe<CameraOpticalQualification> AcceptedQualification(
        EnrollmentViewModel enrollment
    ) =>
        enrollment
            .Sections.Where(section => section.Id == "face")
            .Select(section => section.ViewModel)
            .OfType<FaceCaptureViewModel>()
            .Select(face => face.Accepted.Bind(value => value.OpticalQualification))
            .FirstOrDefault();

    private static byte[] Evidence(
        EnrollmentViewModel enrollment,
        Maybe<CameraOpticalQualification> qualification
    )
    {
        return JsonSerializer.SerializeToUtf8Bytes(
            new
            {
                Version = 1,
                Plan = enrollment.Plan.Id,
                Sections = enrollment.Sections.Select(section => section.Id).ToArray(),
                CreatedAt = DateTimeOffset.UtcNow,
                Purpose = "unsigned-issuance-staging",
                CameraQualification = qualification.Match(
                    value => new[] { value },
                    () => Array.Empty<CameraOpticalQualification>()
                ),
            }
        );
    }
}
