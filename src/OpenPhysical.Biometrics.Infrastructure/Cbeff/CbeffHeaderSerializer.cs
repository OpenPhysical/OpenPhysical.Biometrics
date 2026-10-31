namespace OpenPhysical.Biometrics.Infrastructure.Cbeff;

using CSharpFunctionalExtensions;
using OpenPhysical.Domains.Cbeff;
using Canonical = OpenPhysical.Biometrics.Formats.Cbeff;

/// <summary>Projects compatibility headers through the authoritative PIV CBEFF wire model.</summary>
public static class CbeffHeaderSerializer
{
    /// <summary>Parses the complete 88-byte patron header without changing its field values.</summary>
    /// <param name="headerBytes">The complete header bytes.</param>
    /// <returns>The compatibility header or a structural failure.</returns>
    public static Result<CbeffHeader> FromBytes(byte[] headerBytes)
    {
        if (headerBytes is null)
            return Result.Failure<CbeffHeader>("CBEFF.HeaderMissing");
        try
        {
            var header = new Canonical.CbeffHeader(headerBytes);
            return CbeffHeader.Create(
                header.PatronHeaderVersion,
                header.SecurityOptions,
                header.BiometricDataLength,
                header.SignatureLength,
                header.FormatOwner,
                header.FormatType,
                header.CreationDate.UtcDateTime,
                header.ValidityNotBefore.UtcDateTime,
                header.ValidityNotAfter.UtcDateTime,
                header.BiometricType,
                header.BiometricDataType,
                header.Quality,
                header.Creator.ToArray(),
                header.FascN.ToArray(),
                header.Reserved.ToArray()
            );
        }
        catch (ArgumentException)
        {
            return Result.Failure<CbeffHeader>("CBEFF.HeaderRepresentation");
        }
    }

    /// <summary>Serializes a compatibility header with canonical CBEFF field encoding.</summary>
    /// <param name="header">The header to encode.</param>
    /// <returns>A new caller-owned 88-byte buffer.</returns>
    /// <exception cref="ArgumentNullException">The header is null.</exception>
    /// <exception cref="ArgumentException">A header field cannot be represented.</exception>
    public static byte[] ToBytes(CbeffHeader header)
    {
        ArgumentNullException.ThrowIfNull(header);
        var canonical = Canonical.CbeffHeader.Create(
            header.BdbLength,
            header.SbLength,
            header.BdbFormatOwner,
            header.BdbFormatType,
            new DateTimeOffset(header.CreationDate.ToUniversalTime()),
            new DateTimeOffset(header.ValidityNotBefore.ToUniversalTime()),
            new DateTimeOffset(header.ValidityNotAfter.ToUniversalTime()),
            header.BiometricType,
            header.BiometricSubtype,
            header.Quality,
            header.GetCreator(),
            header.GetFascN(),
            header.SbhSecurityOptions,
            header.PatronHeaderVersion
        );
        var bytes = canonical.ToArray();
        // Generic headers retain reserved bytes for exact inspection and round trips.
        header.GetReserved().CopyTo(bytes, 84);
        return bytes;
    }
}
