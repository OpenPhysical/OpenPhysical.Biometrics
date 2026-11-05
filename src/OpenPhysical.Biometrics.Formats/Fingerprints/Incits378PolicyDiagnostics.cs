// SPDX-License-Identifier: GPL-2.0-or-later
// Copyright (c) 2026 Mistial Developer

namespace OpenPhysical.Biometrics.Formats.Fingerprints;

using System.Collections.Immutable;

// Warnings describe retained coordinates without changing wire indices or extension references.
internal static class Incits378PolicyDiagnostics
{
    internal static InspectionReport Report(FingerprintRecord record, InspectionReport original)
    {
        var offsets = original.Fields.ToDictionary(field => field.Name, field => field.Offset);
        var warnings = ImmutableArray.CreateBuilder<FormatDiagnostic>();
        for (var viewIndex = 0; viewIndex < record.Views.Length; viewIndex++)
        {
            var points = record.Views[viewIndex].Minutiae;
            for (var pointIndex = 0; pointIndex < points.Length; pointIndex++)
            {
                var point = points[pointIndex];
                if (point.X < record.Width && point.Y < record.Height)
                    continue;
                var field = $"Views[{viewIndex}].Minutiae[{pointIndex}].TypeAndX";
                warnings.Add(
                    new FormatDiagnostic(
                        "MinutiaOutsideImage",
                        "The selected policy retains this point for caller-controlled interpretation.",
                        offsets[field],
                        4,
                        FormatDiagnosticSeverity.Warning
                    )
                );
            }
        }
        return original with { Diagnostics = original.Diagnostics.AddRange(warnings) };
    }
}
