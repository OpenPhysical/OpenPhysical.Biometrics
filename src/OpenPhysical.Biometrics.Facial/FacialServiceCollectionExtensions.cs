// SPDX-License-Identifier: GPL-2.0-or-later
// Copyright (c) 2026 Mistial Developer

namespace OpenPhysical.Biometrics.Facial;

using CSharpFunctionalExtensions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

/// <summary>Registers facial record preparation independently of fingerprint processing.</summary>
public static class FacialServiceCollectionExtensions
{
    /// <summary>Registers the captured-pixel JPEG 2000 facial record preparer.</summary>
    /// <param name="services">The application's service collection.</param>
    /// <returns>The same collection for chained registration.</returns>
    public static IServiceCollection AddFacialRecordPreparation(this IServiceCollection services)
    {
        services.TryAddSingleton<IFacialRecordPreparer>(provider => new FacialRecordPreparer(
            Maybe
                .From(provider.GetService<ILogger<FacialRecordPreparer>>())
                .GetValueOrDefault(NullLogger<FacialRecordPreparer>.Instance)
        ));
        return services;
    }
}
