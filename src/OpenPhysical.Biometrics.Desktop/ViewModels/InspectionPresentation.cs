// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Desktop;

using CSharpFunctionalExtensions;

internal static class InspectionPresentation
{
    internal static Result<FingerprintScene, DesktopError> Render(
        Maybe<TemplateDocument> document,
        int selected
    ) =>
        document
            .ToResult(new DesktopError("ViewSelection", "Select a reference fingerprint."))
            .Ensure(
                value => selected >= 0 && selected < value.Record.Views.Length,
                new DesktopError("ViewSelection", "Select an existing reference finger view.")
            )
            .Bind(value => SceneFactory.Document(value, selected));
}
