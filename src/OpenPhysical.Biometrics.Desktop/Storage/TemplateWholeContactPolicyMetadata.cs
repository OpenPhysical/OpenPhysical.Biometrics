// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Desktop;

using System.Text.Json;
using CSharpFunctionalExtensions;
using OpenPhysical.Biometrics.Capture.Futronic;

internal sealed record TemplateWholeContactPolicyMetadata(
    Fs50WholeContactOptions Options,
    Fs50EvidenceOptions Evidence,
    Fs50EmptySensorOptions EmptySensor,
    JsonElement Presentation
)
{
    internal static TemplateWholeContactPolicyMetadata From(Fs50WholeContactPolicy policy) =>
        new(
            policy.Options,
            policy.Evidence.Options,
            policy.EmptySensor.Options,
            JsonSerializer.SerializeToElement(
                CapturePresentationPolicyJson.Write(policy.Presentation)
            )
        );

    internal Result<Fs50WholeContactPolicy, DesktopError> Read()
    {
        if (Options is null || Evidence is null || EmptySensor is null)
            return TemplatePngFiles.Invalid(
                "Original qualification policy parameters are missing."
            );
        return Fs50EvidencePolicy
            .Create(Evidence)
            .MapError(error => TemplatePngFiles.Invalid(error.Message))
            .Bind(evidence =>
                Fs50EmptySensorPolicy
                    .Create(EmptySensor)
                    .MapError(error => TemplatePngFiles.Invalid(error.Message))
                    .Bind(empty =>
                        CapturePresentationPolicyJson
                            .Read(Presentation)
                            .MapError(error => TemplatePngFiles.Invalid(error.Message))
                            .Bind(presentation =>
                                Fs50WholeContactPolicy
                                    .Create(Options, evidence, empty, presentation)
                                    .MapError(error => TemplatePngFiles.Invalid(error.Message))
                            )
                    )
            );
    }
}
