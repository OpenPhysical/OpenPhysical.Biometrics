namespace OpenPhysical.Biometrics.Formats.Cbeff;

/// <summary>Reads and writes PIV CBEFF records while preserving the exact signed header and biometric bytes.</summary>
public static class CbeffCodec
{
    /// <summary>Parses a raw CBEFF record containing exactly one header, biometric block, and security block.</summary>
    /// <param name="data">Unwrapped CBEFF bytes.</param>
    /// <param name="options">Explicit opt-in for sensitive inspection values.</param>
    /// <returns>The parsed record or located structural errors.</returns>
    public static ParseResult<CbeffRecord> Parse(
        ReadOnlySpan<byte> data,
        InspectionOptions? options = null
    )
    {
        var fields = new List<InspectionField>();
        try
        {
            var reader = new BinaryFormatReader(data, 0, fields, options);
            var rawHeader = reader.Read(CbeffHeader.Size, "Header");
            var headerReader = new BinaryFormatReader(rawHeader, 0, fields, options);
            headerReader.Byte("Header.PatronHeaderVersion");
            headerReader.Byte("Header.SecurityOptions");
            headerReader.UInt32("Header.BiometricDataLength");
            headerReader.UInt16("Header.SignatureLength");
            headerReader.UInt16("Header.FormatOwner");
            headerReader.UInt16("Header.FormatType");
            foreach (var name in new[] { "CreationDate", "ValidityNotBefore", "ValidityNotAfter" })
            {
                var date = headerReader.Read(8, "Header." + name);
                try
                {
                    _ = CbeffHeader.DecodeDate(date);
                }
                catch (ArgumentException)
                {
                    BinaryFormatReader.Fail(
                        "CbeffTimestamp",
                        "A header timestamp contains an invalid UTC calendar time.",
                        headerReader.Offset - 8,
                        8
                    );
                }
            }
            headerReader.UInt24("Header.BiometricType");
            headerReader.Byte("Header.BiometricDataType");
            headerReader.Byte("Header.Quality");
            headerReader.Read(18, "Header.Creator");
            headerReader.Read(25, "Header.FascN");
            headerReader.Read(4, "Header.Reserved");
            var header = new CbeffHeader(rawHeader);
            if (
                (ulong)CbeffHeader.Size + header.BiometricDataLength + header.SignatureLength
                != (ulong)data.Length
            )
                BinaryFormatReader.Fail(
                    "CbeffLength",
                    "The declared biometric and security block lengths differ from the buffer length.",
                    2,
                    6
                );
            var biometric = reader.Bytes(checked((int)header.BiometricDataLength), "BiometricData");
            var signature = reader.Bytes(header.SignatureLength, "SecurityBlock");
            reader.RequireEnd();
            return new(new(header, biometric, signature), fields, []);
        }
        catch (FormatReadException error)
        {
            return new(null, fields, [error.Diagnostic]);
        }
    }

    /// <summary>Writes a record whose header lengths already match its contents.</summary>
    /// <param name="record">The complete record to serialize.</param>
    /// <returns>Fresh CBEFF bytes owned by the caller.</returns>
    /// <exception cref="ArgumentException">Declared lengths differ from the supplied immutable blocks.</exception>
    public static byte[] Write(CbeffRecord record)
    {
        Validate(record);
        var result = new byte[
            checked(CbeffHeader.Size + record.BiometricData.Length + record.Signature.Length)
        ];
        record.Header.RawBytes.CopyTo(result);
        record.BiometricData.CopyTo(result, CbeffHeader.Size);
        record.Signature.CopyTo(result, CbeffHeader.Size + record.BiometricData.Length);
        return result;
    }

    /// <summary>Returns the exact header followed by biometric data for detached CMS processing.</summary>
    /// <param name="record">The record whose lengths have been finalized before signing.</param>
    /// <returns>The exact header and biometric-record bytes covered by the signature.</returns>
    public static byte[] GetSignedData(CbeffRecord record)
    {
        ArgumentNullException.ThrowIfNull(record);
        ArgumentNullException.ThrowIfNull(record.Header);
        if (
            record.BiometricData.IsDefault
            || record.Header.BiometricDataLength != record.BiometricData.Length
        )
            throw new ArgumentException(
                "The biometric block length differs from the header.",
                nameof(record)
            );
        var result = new byte[checked(CbeffHeader.Size + record.BiometricData.Length)];
        record.Header.RawBytes.CopyTo(result);
        record.BiometricData.CopyTo(result, CbeffHeader.Size);
        return result;
    }

    private static void Validate(CbeffRecord record)
    {
        ArgumentNullException.ThrowIfNull(record);
        ArgumentNullException.ThrowIfNull(record.Header);
        if (
            record.BiometricData.IsDefault
            || record.Signature.IsDefault
            || record.Header.BiometricDataLength != record.BiometricData.Length
            || record.Header.SignatureLength != record.Signature.Length
        )
            throw new ArgumentException(
                "The biometric or security block length differs from the header.",
                nameof(record)
            );
    }
}
