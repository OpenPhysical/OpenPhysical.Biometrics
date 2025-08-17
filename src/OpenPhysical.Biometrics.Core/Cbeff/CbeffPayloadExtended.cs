namespace OpenPhysical.Biometrics.Core.Cbeff;

using CSharpFunctionalExtensions;
using OpenPhysical.Biometrics.Core.Cbeff.Formats;
using OpenPhysical.Domains.Cbeff;
using System.Collections.Immutable;

/// <summary>
/// Extended CBEFF payload that includes parsed biometric data.
/// </summary>
public sealed record CbeffPayloadExtended
{
    /// <summary>
    /// Gets the CBEFF header.
    /// </summary>
    public CbeffHeader Header { get; }
    
    /// <summary>
    /// Gets the raw biometric data block.
    /// </summary>
    public ImmutableArray<byte> BiometricDataBlock { get; }
    
    /// <summary>
    /// Gets the optional signature.
    /// </summary>
    public Maybe<CbeffSignature> Signature { get; }
    
    /// <summary>
    /// Gets the parsed biometric record if a format handler was found.
    /// </summary>
    public Maybe<BiometricRecord> ParsedBiometricRecord { get; }
    
    /// <summary>
    /// Gets the format handler used to parse the biometric data.
    /// </summary>
    public Maybe<Formats.ICbeffFormat> FormatHandler { get; }
    
    private CbeffPayloadExtended(
        CbeffHeader header,
        ImmutableArray<byte> biometricDataBlock,
        Maybe<CbeffSignature> signature,
        Maybe<BiometricRecord> parsedBiometricRecord,
        Maybe<Formats.ICbeffFormat> formatHandler)
    {
        Header = header;
        BiometricDataBlock = biometricDataBlock;
        Signature = signature;
        ParsedBiometricRecord = parsedBiometricRecord;
        FormatHandler = formatHandler;
    }
    
    /// <summary>
    /// Creates an extended payload from a basic payload and optional parsed data.
    /// </summary>
    public static CbeffPayloadExtended Create(
        CbeffPayload payload,
        Maybe<BiometricRecord> parsedRecord,
        Maybe<Formats.ICbeffFormat> formatHandler)
    {
        return new CbeffPayloadExtended(
            payload.Header,
            payload.BiometricDataBlock.ToImmutableArray(),
            payload.Signature,
            parsedRecord,
            formatHandler);
    }
    
    /// <summary>
    /// Creates an extended payload without parsed data.
    /// </summary>
    public static CbeffPayloadExtended CreateUnparsed(CbeffPayload payload)
    {
        return new CbeffPayloadExtended(
            payload.Header,
            payload.BiometricDataBlock.ToImmutableArray(),
            payload.Signature,
            Maybe<BiometricRecord>.None,
            Maybe<Formats.ICbeffFormat>.None);
    }
    
    /// <summary>
    /// Converts to a basic CBEFF payload, discarding parsed data.
    /// </summary>
    public Result<CbeffPayload> ToBasicPayload()
    {
        return CbeffPayload.Create(Header, BiometricDataBlock.ToArray(), Signature);
    }
}