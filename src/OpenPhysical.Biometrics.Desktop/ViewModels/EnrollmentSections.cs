// SPDX-License-Identifier: GPL-2.0-or-later
using System.Collections.Immutable;
using CSharpFunctionalExtensions;

namespace OpenPhysical.Biometrics.Desktop;

internal sealed class EnrollmentSections(ImmutableArray<IEnrollmentSection> sections)
{
    internal ImmutableArray<IEnrollmentSection> Registered => sections;
    internal CapturePlanChoice DefaultPlan =>
        sections.Length > 1 ? CapturePlanChoice.Full
        : sections[0].Id == "face" ? CapturePlanChoice.Face
        : CapturePlanChoice.Fingerprints;
    internal ImmutableArray<CapturePlanChoice> Plans =>
        [
            .. CapturePlanChoice.All.Where(choice =>
                choice == CapturePlanChoice.Full
                    ? sections.Length > 1
                    : sections.Any(section => section.Id == choice.Id)
            ),
        ];

    internal static UnitResult<DesktopError> Validate(
        ImmutableArray<IEnrollmentSection> sections
    ) =>
        !sections.IsDefaultOrEmpty
        && sections.All(section => section is not null && section.Id is "face" or "fingerprints")
        && sections.Select(section => section.Id).Distinct().Count() == sections.Length
            ? UnitResult.Success<DesktopError>()
            : new DesktopError(
                "Session.Sections",
                "Register the face or fingerprint capture sections once each."
            );
}
