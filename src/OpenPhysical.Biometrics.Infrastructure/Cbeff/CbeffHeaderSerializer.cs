namespace OpenPhysical.Biometrics.Infrastructure.Cbeff;

using System;
using CSharpFunctionalExtensions;
using OpenPhysical.Domains.Cbeff;

/// <summary>
/// Service for serializing and deserializing CBEFF headers.
/// This handles the binary encoding/decoding of CBEFF headers per NIST SP 800-76-2.
/// </summary>
public static class CbeffHeaderSerializer
{
    /// <summary>
    /// Parses a CBEFF header from raw bytes.
    /// </summary>
    /// <param name="headerBytes">The raw header bytes (must be exactly 88 bytes).</param>
    /// <returns>A Result containing the CbeffHeader or an error.</returns>
    public static Result<CbeffHeader> FromBytes(byte[] headerBytes)
    {
        if (headerBytes == null)
            return Result.Failure<CbeffHeader>("Header bytes cannot be null");

        if (headerBytes.Length != CbeffHeader.StandardHeaderSize)
            return Result.Failure<CbeffHeader>($"Header must be exactly {CbeffHeader.StandardHeaderSize} bytes");

        try
        {
            var offset = 0;

            // Parse header fields
            var patronHeaderVersion = headerBytes[offset++];
            var sbhSecurityOptions = headerBytes[offset++];
            
            // Multi-byte fields are big-endian in CBEFF
            var bdbLength = ReadUInt32BigEndian(headerBytes, offset);
            offset += 4;
            var sbLength = ReadUInt16BigEndian(headerBytes, offset);
            offset += 2;
            var bdbFormatOwner = ReadUInt16BigEndian(headerBytes, offset);
            offset += 2;
            var bdbFormatType = ReadUInt16BigEndian(headerBytes, offset);
            offset += 2;

            // Parse dates (8 bytes each in NIST format)
            var creationDateResult = DateTimeCodec.Decode(ExtractBytes(headerBytes, offset, 8));
            if (creationDateResult.IsFailure)
                return Result.Failure<CbeffHeader>($"Invalid creation date: {creationDateResult.Error}");
            offset += 8;
            
            var validityNotBeforeResult = DateTimeCodec.Decode(ExtractBytes(headerBytes, offset, 8));
            if (validityNotBeforeResult.IsFailure)
                return Result.Failure<CbeffHeader>($"Invalid validity start date: {validityNotBeforeResult.Error}");
            offset += 8;
            
            var validityNotAfterResult = DateTimeCodec.Decode(ExtractBytes(headerBytes, offset, 8));
            if (validityNotAfterResult.IsFailure)
                return Result.Failure<CbeffHeader>($"Invalid validity end date: {validityNotAfterResult.Error}");
            offset += 8;

            // Parse biometric information
            // Biometric Type is 3 bytes per NIST SP 800-76-2 Table 14
            var biometricType = (uint)((headerBytes[offset] << 16) | (headerBytes[offset + 1] << 8) | headerBytes[offset + 2]);
            offset += 3;
            var biometricSubtype = headerBytes[offset++];
            var quality = (sbyte)headerBytes[offset++];

            // Parse creator (18 bytes)
            if (offset + 18 > headerBytes.Length)
                return Result.Failure<CbeffHeader>($"Creator field extends beyond header: offset {offset}, need 18 bytes, header size {headerBytes.Length}");
            var creator = new byte[18];
            Array.Copy(headerBytes, offset, creator, 0, 18);
            offset += 18;

            // Parse FASC-N (25 bytes)
            if (offset + 25 > headerBytes.Length)
                return Result.Failure<CbeffHeader>($"FASC-N field extends beyond header: offset {offset}, need 25 bytes, header size {headerBytes.Length}");
            var fascN = new byte[25];
            Array.Copy(headerBytes, offset, fascN, 0, 25);
            offset += 25;

            // Parse reserved (4 bytes)
            if (offset + 4 > headerBytes.Length)
                return Result.Failure<CbeffHeader>($"Reserved field extends beyond header: offset {offset}, need 4 bytes, header size {headerBytes.Length}");
            var reserved = new byte[4];
            Array.Copy(headerBytes, offset, reserved, 0, 4);

            return CbeffHeader.Create(
                patronHeaderVersion,
                sbhSecurityOptions,
                bdbLength,
                sbLength,
                bdbFormatOwner,
                bdbFormatType,
                creationDateResult.Value,
                validityNotBeforeResult.Value,
                validityNotAfterResult.Value,
                biometricType,
                biometricSubtype,
                quality,
                creator,
                fascN,
                reserved);
        }
        catch (Exception ex)
        {
            return Result.Failure<CbeffHeader>($"Failed to parse CBEFF header: {ex.Message}");
        }
    }

    /// <summary>
    /// Serializes a CBEFF header to its byte representation.
    /// </summary>
    /// <param name="header">The header to serialize.</param>
    /// <returns>The header as a byte array.</returns>
    public static byte[] ToBytes(CbeffHeader header)
    {
        var bytes = new byte[CbeffHeader.StandardHeaderSize];
        var offset = 0;

        // Write header fields
        bytes[offset++] = header.PatronHeaderVersion;
        bytes[offset++] = header.SbhSecurityOptions;
        
        // Multi-byte fields are big-endian in CBEFF
        WriteUInt32BigEndian(bytes, offset, header.BdbLength);
        offset += 4;
        WriteUInt16BigEndian(bytes, offset, header.SbLength);
        offset += 2;
        WriteUInt16BigEndian(bytes, offset, header.BdbFormatOwner);
        offset += 2;
        WriteUInt16BigEndian(bytes, offset, header.BdbFormatType);
        offset += 2;

        // Write dates
        DateTimeCodec.Encode(header.CreationDate).CopyTo(bytes, offset);
        offset += 8;
        DateTimeCodec.Encode(header.ValidityNotBefore).CopyTo(bytes, offset);
        offset += 8;
        DateTimeCodec.Encode(header.ValidityNotAfter).CopyTo(bytes, offset);
        offset += 8;

        // Write biometric information
        // Biometric Type is 3 bytes per NIST SP 800-76-2 Table 14
        bytes[offset] = (byte)(header.BiometricType >> 16);
        bytes[offset + 1] = (byte)(header.BiometricType >> 8);
        bytes[offset + 2] = (byte)header.BiometricType;
        offset += 3;
        bytes[offset++] = header.BiometricSubtype;
        bytes[offset++] = (byte)header.Quality;

        // Write creator, FASC-N, and reserved
        header.GetCreator().CopyTo(bytes, offset);
        offset += 18;
        header.GetFascN().CopyTo(bytes, offset);
        offset += 25;
        header.GetReserved().CopyTo(bytes, offset);

        return bytes;
    }

    /// <summary>
    /// Reads a big-endian 16-bit unsigned integer from the buffer.
    /// </summary>
    private static ushort ReadUInt16BigEndian(byte[] buffer, int offset)
    {
        return (ushort)((buffer[offset] << 8) | buffer[offset + 1]);
    }

    /// <summary>
    /// Reads a big-endian 32-bit unsigned integer from the buffer.
    /// </summary>
    private static uint ReadUInt32BigEndian(byte[] buffer, int offset)
    {
        return (uint)((buffer[offset] << 24) | (buffer[offset + 1] << 16) | 
                      (buffer[offset + 2] << 8) | buffer[offset + 3]);
    }

    /// <summary>
    /// Writes a big-endian 16-bit unsigned integer to the buffer.
    /// </summary>
    private static void WriteUInt16BigEndian(byte[] buffer, int offset, ushort value)
    {
        buffer[offset] = (byte)(value >> 8);
        buffer[offset + 1] = (byte)value;
    }

    /// <summary>
    /// Writes a big-endian 32-bit unsigned integer to the buffer.
    /// </summary>
    private static void WriteUInt32BigEndian(byte[] buffer, int offset, uint value)
    {
        buffer[offset] = (byte)(value >> 24);
        buffer[offset + 1] = (byte)(value >> 16);
        buffer[offset + 2] = (byte)(value >> 8);
        buffer[offset + 3] = (byte)value;
    }

    /// <summary>
    /// Extracts a subset of bytes from an array.
    /// </summary>
    private static byte[] ExtractBytes(byte[] source, int offset, int length)
    {
        var result = new byte[length];
        Array.Copy(source, offset, result, 0, length);
        return result;
    }
}