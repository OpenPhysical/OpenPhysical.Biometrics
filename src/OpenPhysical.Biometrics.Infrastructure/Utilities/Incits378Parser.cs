namespace OpenPhysical.Biometrics.Infrastructure.Utilities;

using System.Collections.Immutable;
using CSharpFunctionalExtensions;
using Microsoft.Extensions.Logging;
using OpenPhysical.Domains.Cbeff.Formats.Incits378;
using Canonical = OpenPhysical.Biometrics.Formats.Fingerprints;

/// <summary>Provides the compatibility entry point for canonical INCITS 378 decoding.</summary>
public static class Incits378Parser
{
    /// <summary>Parses a fingerprint record and retains typed extensions for inspection callers.</summary>
    /// <param name="data">The complete INCITS 378 record bytes.</param>
    /// <param name="logger">Optional logger receiving structural status only.</param>
    /// <returns>The compatibility record or stable structural diagnostic codes.</returns>
    public static Result<FingerprintRecord> ParseIncits378Record(
        ImmutableArray<byte> data,
        ILogger? logger = null
    )
    {
        var parsed = Canonical.Incits378Codec.Parse(data.AsSpan());
        if (!parsed.IsSuccess)
        {
            logger?.LogDebug("INCITS 378 structural validation failed.");
            return Result.Failure<FingerprintRecord>(
                string.Join("; ", parsed.Diagnostics.Select(diagnostic => diagnostic.Code))
            );
        }
        try
        {
            return Result.Success(LegacyFingerprintProjection.FromCanonical(parsed.Value!));
        }
        catch (Exception error)
            when (error is ArgumentException or InvalidOperationException or ResultFailureException)
        {
            logger?.LogDebug("INCITS 378 compatibility projection failed.");
            return Result.Failure<FingerprintRecord>("INCITS378.CompatibilityProjection");
        }
    }
}
