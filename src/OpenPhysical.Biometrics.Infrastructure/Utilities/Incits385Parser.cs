namespace OpenPhysical.Biometrics.Infrastructure.Utilities;

using System.Collections.Immutable;
using CSharpFunctionalExtensions;
using OpenPhysical.Domains.Cbeff.Formats.Incits385;
using Canonical = OpenPhysical.Biometrics.Formats.Faces;

/// <summary>Provides the compatibility entry point for canonical INCITS 385 decoding.</summary>
public static class Incits385Parser
{
    /// <summary>Parses one facial image through the authoritative INCITS 385 codec.</summary>
    /// <param name="data">The complete facial record bytes.</param>
    /// <returns>The single-image compatibility model or structural diagnostic codes.</returns>
    public static Result<FacialImageRecord> ParseIncits385Record(ImmutableArray<byte> data)
    {
        var parsed = Canonical.Incits385Codec.Parse(data.AsSpan());
        if (!parsed.IsSuccess)
            return Result.Failure<FacialImageRecord>(
                string.Join("; ", parsed.Diagnostics.Select(diagnostic => diagnostic.Code))
            );
        try
        {
            return Result.Success(LegacyFacialProjection.FromCanonical(parsed.Value!));
        }
        catch (Exception error)
            when (error
                    is ArgumentException
                        or InvalidOperationException
                        or ResultFailureException
                        or OverflowException
            )
        {
            return Result.Failure<FacialImageRecord>("INCITS385.CompatibilityProjection");
        }
    }
}
