// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Middleware.Cli.Inspection;

using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CSharpFunctionalExtensions;
using OpenPhysical.Cli.Hosting;
using OpenPhysical.Middleware.Cli.Settings;
using OpenPhysical.Middleware.Domain.Trust;
using OpenPhysical.Middleware.Infrastructure.Trust;
using Spectre.Console;

internal static class CliFingerprintGuidance
{
    internal static async Task<
        Result<FingerprintReferenceSelection, VerificationError>
    > SelectAsync(
        ImmutableArray<FingerprintReferenceSelection> choices,
        FingerprintCaptureSettings settings,
        CommandOutputOptions output,
        IAnsiConsole console,
        CancellationToken cancellationToken
    )
    {
        var selected = Choose(choices, settings);
        if (selected.IsFailure)
            return selected;
        var name = FingerName(selected.Value.Finger);
        if (name.IsFailure)
            return Result.Failure<FingerprintReferenceSelection, VerificationError>(name.Error);
        return await CaptureVerificationEdge
            .RunAsync(
                () =>
                    ConfirmAsync(
                        selected.Value,
                        name.Value,
                        settings,
                        output,
                        console,
                        cancellationToken
                    ),
                "capture.guidance-unavailable"
            )
            .ConfigureAwait(false);
    }

    private static Result<FingerprintReferenceSelection, VerificationError> Choose(
        ImmutableArray<FingerprintReferenceSelection> choices,
        FingerprintCaptureSettings settings
    )
    {
        if (choices.IsDefaultOrEmpty)
            return Unavailable();
        if (!settings.Finger.HasValue)
            return choices[0];
        var matching = choices
            .Where(value => value.Finger == settings.Finger.Value && value.View == settings.View)
            .ToImmutableArray();
        return matching.Length == 1 ? matching[0] : Unavailable();
    }

    private static async Task<
        Result<FingerprintReferenceSelection, VerificationError>
    > ConfirmAsync(
        FingerprintReferenceSelection selected,
        string name,
        FingerprintCaptureSettings settings,
        CommandOutputOptions output,
        IAnsiConsole console,
        CancellationToken cancellationToken
    )
    {
        if (!output.Json)
            console.WriteLine($"Place your {name} on the scanner.");
        if (!settings.CaptureReady)
        {
            if (!output.Json)
                console.WriteLine("Press Enter to capture, or Escape to cancel.");
            await CliPinInput.WaitForCaptureAsync(cancellationToken).ConfigureAwait(false);
        }
        return Result.Success<FingerprintReferenceSelection, VerificationError>(selected);
    }

    private static Result<string, VerificationError> FingerName(byte finger) =>
        finger switch
        {
            1 => "right thumb",
            2 => "right index finger",
            3 => "right middle finger",
            4 => "right ring finger",
            5 => "right little finger",
            6 => "left thumb",
            7 => "left index finger",
            8 => "left middle finger",
            9 => "left ring finger",
            10 => "left little finger",
            _ => Result.Failure<string, VerificationError>(
                new(VerificationErrorCodes.EvidenceUnavailable, "capture.finger-not-enrolled")
            ),
        };

    private static Result<FingerprintReferenceSelection, VerificationError> Unavailable() =>
        Result.Failure<FingerprintReferenceSelection, VerificationError>(
            new(VerificationErrorCodes.EvidenceUnavailable, "capture.finger-not-enrolled")
        );
}
