// SPDX-License-Identifier: GPL-2.0-or-later
using System.Collections.Immutable;
using CommunityToolkit.Mvvm.ComponentModel;
using CSharpFunctionalExtensions;

namespace OpenPhysical.Biometrics.Desktop;

internal sealed class FingerprintEnrollmentSection(
    FingerprintEnrollmentViewModel fingerprints,
    ITemplateSessionStorage storage,
    Func<Result<TemplateMetadata, DesktopError>> metadata,
    Maybe<Func<TemplateSaveOptions>> options = default
) : IEnrollmentSection
{
    public string Id => "fingerprints";
    public string Title => "Fingerprints";
    public bool Complete => fingerprints.Complete;
    public ObservableObject ViewModel => fingerprints;

    public async Task<Result<ImmutableArray<EnrollmentArtifact>, DesktopError>> WriteAsync(
        CapturePerson person,
        string folder,
        CancellationToken cancellationToken
    )
    {
        if (!Complete)
            return new DesktopError(
                "Session.Incomplete",
                "Accept the required fingerprints before saving."
            );
        var selected = fingerprints.Selection();
        var details = metadata();
        if (selected.IsFailure)
            return selected.Error;
        if (details.IsFailure)
            return details.Error;
        var record = UnsignedTemplateService.Create(selected.Value, details.Value);
        if (record.IsFailure)
            return record.Error;
        var saved = await storage.SaveSessionAsync(
            record.Value,
            selected.Value,
            person,
            folder,
            options.Match(value => value(), () => TemplateSaveOptions.Default),
            cancellationToken
        );
        return saved.Map(path =>
            ImmutableArray.Create(new EnrollmentArtifact(Id, "application/octet-stream", path))
        );
    }

    public Task<UnitResult<DesktopError>> ResetAsync() => fingerprints.ResetAsync();
}
