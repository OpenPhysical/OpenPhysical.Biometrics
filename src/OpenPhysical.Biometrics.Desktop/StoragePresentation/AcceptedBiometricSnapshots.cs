// SPDX-License-Identifier: GPL-2.0-or-later
using CSharpFunctionalExtensions;

namespace OpenPhysical.Biometrics.Desktop;

internal static class AcceptedBiometricSnapshots
{
    internal static Result<AcceptedBiometricSnapshot, DesktopError> Prepare(
        ExportViewModel export,
        string personId
    ) =>
        PersonPackageBuilder
            .Create(personId, export.Enrollment)
            .Map(package => new AcceptedBiometricSnapshot(
                package,
                export.Enrollment.Plan.Id,
                export.Protection.Revision
            ));

    internal static UnitResult<DesktopError> Validate(
        ExportViewModel export,
        string personId,
        AcceptedBiometricSnapshot snapshot
    )
    {
        if (
            snapshot.PrivacyRevision != export.Protection.Revision
            || snapshot.PlanId != export.Enrollment.Plan.Id
            || snapshot.Package.PersonId
                != export.Enrollment.Person.AuthoritativePersonId.Match(
                    value => value.ToString("D"),
                    () => personId
                )
        )
            return Changed();
        var current = PersonPackageBuilder.Create(personId, export.Enrollment);
        if (current.IsFailure)
            return current.Error;
        return
            current.Value.Label == snapshot.Package.Label
            && current.Value.ExternalId.Equals(snapshot.Package.ExternalId)
            && current
                .Value.Artifacts.Select(value =>
                    (value.Name, value.Role, value.Format, value.Sha256)
                )
                .SequenceEqual(
                    snapshot.Package.Artifacts.Select(value =>
                        (value.Name, value.Role, value.Format, value.Sha256)
                    )
                )
            ? UnitResult.Success<DesktopError>()
            : Changed();
    }

    private static UnitResult<DesktopError> Changed() =>
        new DesktopError(
            "Session.Changed",
            "The person, accepted captures or privacy session changed. Prepare them again."
        );
}
