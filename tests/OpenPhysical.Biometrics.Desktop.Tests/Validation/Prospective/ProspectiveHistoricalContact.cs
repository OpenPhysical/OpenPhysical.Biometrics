// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Desktop.Tests;

using System.Text.Json;
using CSharpFunctionalExtensions;

internal static class ProspectiveHistoricalContact
{
    internal static Result<Maybe<DesktopError>, DesktopError> Read(JsonElement image)
    {
        var present = image.GetProperty("legacyPresent").GetBoolean();
        var count = image.GetProperty("legacyComponentCount").GetInt32();
        var issue = image.GetProperty("legacyIssue").GetString();
        if (present && count == 1 && issue == "None")
            return Maybe<DesktopError>.None;
        var code = (present, count, issue) switch
        {
            (false, 0, "TooLight") => "Capture.ContactTooLight",
            (false, 0, "InsufficientRidgeEvidence") => "Capture.UnclearContact",
            (false, 0, "None") => "Capture.NoFinger",
            (true, > 1, "None") => "Capture.MultipleFingers",
            _ => string.Empty,
        };
        return code.Length > 0
            ? Maybe.From(
                new DesktopError(code, "Frozen historical contact evidence: " + issue + ".")
            )
            : Result.Failure<Maybe<DesktopError>, DesktopError>(
                new("Evaluation.LegacyEvidence", "The historical contact evidence is inconsistent.")
            );
    }
}
