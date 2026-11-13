// SPDX-License-Identifier: GPL-2.0-or-later
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using OpenPhysical.Cli.Hosting;
using OpenPhysical.Middleware.Cli.Commands;
using OpenPhysical.Middleware.Cli.Inspection;
using OpenPhysical.Workstation.Infrastructure;
using Spectre.Console.Cli;

namespace OpenPhysical.Biometrics.Desktop;

/// <summary>Capture-only credential verification commands hosted by Biometric Studio.</summary>
public static class StudioVerificationApplication
{
    /// <summary>Runs authenticated PIV/TWIC verification or capture discovery using the real shared services.</summary>
    /// <param name="args">Capture command arguments.</param>
    /// <param name="builder">Host with explicitly configured providers and policy.</param>
    /// <param name="output">Bounded command output destination.</param>
    /// <param name="cancellationToken">Cancellation for this operation.</param>
    /// <returns>The bounded operation result or a typed failure.</returns>
    public static Task<int> RunHostedAsync(
        string[] args,
        HostApplicationBuilder builder,
        TextWriter output,
        CancellationToken cancellationToken = default
    )
    {
        builder.Services.TryAddSingleton(TimeProvider.System);
        builder.Services.AddWorkstationCardVerification();
        var capture = builder.Services.AddWorkstationCapture(
            builder.Configuration["OpenPhysical:Capture:LibUsbPath"]
        );
        if (capture.IsFailure)
            return Task.FromResult(
                new OperationResultRenderer(
                    output,
                    CommandOutputOptions.Parse(args).Options
                ).RenderFailure(new(OperationFailureKind.Validation, Guid.NewGuid()))
            );
        builder.Services.TryAddSingleton<OfflineVerificationInputReader>();
        builder.Services.TryAddSingleton<CliCardSessionSource>();
        return new HostedCommandApplication(builder, output).RunAsync(
            args,
            Configure,
            cancellationToken
        );
    }

    private static void Configure(IConfigurator commands)
    {
        commands.UseStrictParsing();
        commands.Settings.ApplicationName = "OpenPhysical Biometric Studio verify-card";
        commands.AddBranch(
            "capture",
            branch =>
                branch
                    .AddCommand<CaptureDevicesListCommand>("devices")
                    .WithDescription("List fingerprint scanners without opening a capture session")
        );
        commands.AddBranch(
            "fingerprint",
            branch =>
                branch
                    .AddCommand<FingerprintVerifyCommand>("verify")
                    .WithDescription(
                        "Verify a presented PIV card and its holder using signed policy"
                    )
        );
        commands.AddBranch(
            "twic",
            branch =>
                branch
                    .AddCommand<TwicVerifyCommand>("verify")
                    .WithDescription(
                        "Verify a TWIC and its holder using signed cancellation policy"
                    )
        );
    }
}
