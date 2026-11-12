// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Desktop;

using CSharpFunctionalExtensions;

internal static class InspectionComparisonRunner
{
    internal static async Task RunAsync(
        TemplateComparison comparison,
        Func<Result<InspectionComparisonRequest, DesktopError>> snapshot,
        Func<InspectionComparisonRequest, bool> current,
        Func<
            InspectionComparisonRequest,
            InspectionComparisonOutput,
            UnitResult<DesktopError>
        > publish,
        Func<DesktopError, UnitResult<DesktopError>> failed
    )
    {
        var requested = snapshot();
        if (requested.IsFailure)
        {
            failed(requested.Error);
            return;
        }
        var value = requested.Value;
        var result = await Task.Run(() => InspectionComparisonOutput.Evaluate(comparison, value));
        if (!current(value))
            return;
        publish(value, result);
    }
}
