namespace OpenPhysical.Biometrics.Core.Cbeff;

using CSharpFunctionalExtensions;
using OpenPhysical.Biometrics.Core.Cbeff.Formats;
using OpenPhysical.Biometrics.Core.Domain;
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
    public Maybe<object> ParsedBiometricRecord { get; }
    
    /// <summary>
    /// Gets the display representation if the parsed record could be transformed.
    /// </summary>
    public Maybe<BiometricDisplay> DisplayRepresentation { get; }
    
    private CbeffPayloadExtended(
        CbeffHeader header,
        ImmutableArray<byte> biometricDataBlock,
        Maybe<CbeffSignature> signature,
        Maybe<object> parsedBiometricRecord,
        Maybe<BiometricDisplay> displayRepresentation)
    {
        Header = header;
        BiometricDataBlock = biometricDataBlock;
        Signature = signature;
        ParsedBiometricRecord = parsedBiometricRecord;
        DisplayRepresentation = displayRepresentation;
    }
    
    /// <summary>
    /// Creates an extended payload from a basic payload and optional parsed data.
    /// </summary>
    public static CbeffPayloadExtended Create(
        CbeffPayload payload,
        Maybe<object> parsedRecord,
        Maybe<BiometricDisplay> displayRepresentation)
    {
        return new CbeffPayloadExtended(
            payload.Header,
            payload.BiometricDataBlock.ToImmutableArray(),
            payload.Signature,
            parsedRecord,
            displayRepresentation);
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
            Maybe<object>.None,
            Maybe<BiometricDisplay>.None);
    }
    
    /// <summary>
    /// Converts to a basic CBEFF payload, discarding parsed data.
    /// </summary>
    public Result<CbeffPayload> ToBasicPayload()
    {
        return CbeffPayload.Create(Header, BiometricDataBlock.ToArray(), Signature);
    }
}