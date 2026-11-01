// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Facial;

using CSharpFunctionalExtensions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

/// <summary>Registers optional MediaPipe portrait processing.</summary>
public static class PortraitServiceCollectionExtensions
{
    /// <summary>Adds the lazily initialized portrait preparer.</summary>
    /// <param name="services">Application services.</param>
    /// <returns>The same collection for composition.</returns>
    public static IServiceCollection AddPortraitPreparation(this IServiceCollection services)
    {
        services.TryAddSingleton(provider => new PortraitPreparer(
            Maybe
                .From(provider.GetService<ILogger<PortraitPreparer>>())
                .GetValueOrDefault(NullLogger<PortraitPreparer>.Instance)
        ));
        return services;
    }
}
