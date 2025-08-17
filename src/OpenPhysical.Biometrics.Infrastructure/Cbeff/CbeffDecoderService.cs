namespace OpenPhysical.Biometrics.Infrastructure.Cbeff;

using CSharpFunctionalExtensions;
using OpenPhysical.Biometrics.Core.Cbeff;
using OpenPhysical.Domains.Cbeff;
using System;
using System.Threading.Tasks;

/// <summary>
/// Service implementation for decoding CBEFF format biometric data.
/// </summary>
public class CbeffDecoderService : ICbeffDecoder
{
    /// <summary>
    /// Decodes a CBEFF payload from raw bytes.
    /// </summary>
    public async Task<Result<CbeffPayload>> DecodeAsync(byte[] cbeffData)
    {
        if (cbeffData == null)
            return Result.Failure<CbeffPayload>("CBEFF data cannot be null");

        if (cbeffData.Length < CbeffHeader.StandardHeaderSize)
            return Result.Failure<CbeffPayload>($"CBEFF data too short: {cbeffData.Length} bytes, minimum {CbeffHeader.StandardHeaderSize}");

        try
        {
            // Extract and parse header
            var headerBytes = new byte[CbeffHeader.StandardHeaderSize];
            Array.Copy(cbeffData, 0, headerBytes, 0, CbeffHeader.StandardHeaderSize);
            
            var headerResult = CbeffHeaderSerializer.FromBytes(headerBytes);
            if (headerResult.IsFailure)
                return Result.Failure<CbeffPayload>($"Invalid CBEFF header: {headerResult.Error}");
            
            var header = headerResult.Value;
            var offset = CbeffHeader.StandardHeaderSize;

            // Validate data length against header
            var expectedTotalLength = CbeffHeader.StandardHeaderSize + header.BdbLength + header.SbLength;
            if (cbeffData.Length != expectedTotalLength)
                return Result.Failure<CbeffPayload>(
                    $"CBEFF data length mismatch: expected {expectedTotalLength}, got {cbeffData.Length}");

            // Extract biometric data block
            if (header.BdbLength == 0)
                return Result.Failure<CbeffPayload>("CBEFF header indicates zero-length biometric data block");
            
            var biometricDataBlock = new byte[header.BdbLength];
            Array.Copy(cbeffData, offset, biometricDataBlock, 0, (int)header.BdbLength);
            offset += (int)header.BdbLength;

            // Extract signature if present
            Maybe<CbeffSignature> signature = Maybe<CbeffSignature>.None;
            if (header.SbLength > 0)
            {
                var signatureBytes = new byte[header.SbLength];
                Array.Copy(cbeffData, offset, signatureBytes, 0, header.SbLength);
                
                var signatureResult = CbeffSignature.Create(signatureBytes);
                if (signatureResult.IsFailure)
                    return Result.Failure<CbeffPayload>($"Invalid signature block: {signatureResult.Error}");
                    
                signature = Maybe<CbeffSignature>.From(signatureResult.Value);
            }

            // Create and return payload
            return CbeffPayload.Create(header, biometricDataBlock, signature);
        }
        catch (Exception ex)
        {
            return Result.Failure<CbeffPayload>($"Error decoding CBEFF data: {ex.Message}");
        }
    }

    /// <summary>
    /// Validates a CBEFF payload structure without fully decoding.
    /// </summary>
    public Result<bool> ValidateStructure(byte[] cbeffData)
    {
        if (cbeffData == null)
            return Result.Failure<bool>("CBEFF data cannot be null");

        if (cbeffData.Length < CbeffHeader.StandardHeaderSize)
            return Result.Success(false);

        try
        {
            // Try to parse the header
            var headerBytes = new byte[CbeffHeader.StandardHeaderSize];
            Array.Copy(cbeffData, 0, headerBytes, 0, CbeffHeader.StandardHeaderSize);
            
            var headerResult = CbeffHeaderSerializer.FromBytes(headerBytes);
            if (headerResult.IsFailure)
                return Result.Success(false);
            
            var header = headerResult.Value;
            
            // Check if total length matches expected
            var expectedTotalLength = CbeffHeader.StandardHeaderSize + header.BdbLength + header.SbLength;
            if (cbeffData.Length != expectedTotalLength)
                return Result.Success(false);

            // Basic validation passed
            return Result.Success(true);
        }
        catch
        {
            return Result.Success(false);
        }
    }

    /// <summary>
    /// Extracts just the biometric data block from CBEFF data.
    /// </summary>
    public Result<byte[]> ExtractBiometricData(byte[] cbeffData)
    {
        if (cbeffData == null)
            return Result.Failure<byte[]>("CBEFF data cannot be null");

        if (cbeffData.Length < CbeffHeader.StandardHeaderSize)
            return Result.Failure<byte[]>($"CBEFF data too short: {cbeffData.Length} bytes");

        try
        {
            // Parse header to get BDB length
            var headerBytes = new byte[CbeffHeader.StandardHeaderSize];
            Array.Copy(cbeffData, 0, headerBytes, 0, CbeffHeader.StandardHeaderSize);
            
            var headerResult = CbeffHeaderSerializer.FromBytes(headerBytes);
            if (headerResult.IsFailure)
                return Result.Failure<byte[]>($"Invalid CBEFF header: {headerResult.Error}");
            
            var header = headerResult.Value;
            
            // Validate we have enough data
            if (cbeffData.Length < CbeffHeader.StandardHeaderSize + header.BdbLength)
                return Result.Failure<byte[]>("CBEFF data too short for indicated BDB length");

            // Extract biometric data block
            var biometricDataBlock = new byte[header.BdbLength];
            Array.Copy(cbeffData, CbeffHeader.StandardHeaderSize, biometricDataBlock, 0, (int)header.BdbLength);
            
            return Result.Success(biometricDataBlock);
        }
        catch (Exception ex)
        {
            return Result.Failure<byte[]>($"Error extracting biometric data: {ex.Message}");
        }
    }
}