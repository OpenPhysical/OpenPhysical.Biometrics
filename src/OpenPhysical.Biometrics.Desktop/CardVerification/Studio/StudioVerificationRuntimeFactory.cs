// SPDX-License-Identifier: GPL-2.0-or-later
using CSharpFunctionalExtensions;
using Microsoft.Extensions.DependencyInjection;
using OpenPhysical.Biometrics.Capture;
using OpenPhysical.Workstation.Infrastructure;
using OpenPhysical.Workstation.Infrastructure.Security;

namespace OpenPhysical.Biometrics.Desktop;

internal sealed class StudioVerificationRuntimeFactory(CaptureDriverRegistry registry)
    : IStudioVerificationBackendFactory
{
    public async Task<Result<IStudioVerificationBackend, DesktopError>> OpenAsync(
        CancellationToken token
    )
    {
        var opened = await DesktopEdge.TryAsync(
            () => CreateAsync(token),
            "Verification.Setup",
            "Card checking could not open its protected local settings."
        );
        return opened.IsSuccess
            ? opened.Value
            : new DesktopError(
                "Verification.Setup",
                "Card checking requires macOS and an available private verification profile."
            );
    }

    private async Task<Result<IStudioVerificationBackend, DesktopError>> CreateAsync(
        CancellationToken token
    )
    {
        var path = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "OpenPhysical",
            "Verification"
        );
        var installation = new WorkstationInstallation(path);
        Maybe<ServiceProvider> owned = Maybe<ServiceProvider>.None;
        var handedOff = false;
        try
        {
            var configured = ConfigureServices(installation);
            if (configured.IsFailure)
                return configured.Error;
            owned = configured.Value.BuildServiceProvider(
                new ServiceProviderOptions { ValidateScopes = true }
            );
            var initialized = await owned.Value.InitializeStandaloneVerificationStateAsync(token);
            if (initialized.IsFailure)
                return StudioVerificationErrors.From(initialized.Error);
            handedOff = true;
            return new StudioVerificationBackend(owned.Value);
        }
        finally
        {
            if (!handedOff)
            {
                if (owned.HasValue)
                    await owned.Value.DisposeAsync();
                installation.Dispose();
            }
        }
    }

    private Result<ServiceCollection, DesktopError> ConfigureServices(
        WorkstationInstallation installation
    )
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton(registry);
        services.AddStandaloneWorkstationVerification(installation, ClockUncertainty());
        services.AddWorkstationCardVerification();
        var capture = services.AddWorkstationCapture();
        return capture.IsSuccess ? services : capture.Error;
    }

    private static Maybe<TimeSpan> ClockUncertainty() =>
        double.TryParse(
            Environment.GetEnvironmentVariable(
                "OPENPHYSICAL_VERIFICATION_CLOCK_UNCERTAINTY_SECONDS"
            ),
            System.Globalization.NumberStyles.AllowDecimalPoint,
            System.Globalization.CultureInfo.InvariantCulture,
            out var value
        ) && value is >= 0 and <= 300
            ? TimeSpan.FromSeconds(value)
            : Maybe<TimeSpan>.None;
}
