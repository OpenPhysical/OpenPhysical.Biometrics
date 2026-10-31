namespace OpenPhysical.Biometrics.Infrastructure.Cbeff;

using System;
using System.Collections.Immutable;
using System.Linq;
using CSharpFunctionalExtensions;
using OpenPhysical.Biometrics.Core.Cbeff;
using OpenPhysical.Biometrics.Infrastructure.Utilities;
using OpenPhysical.Domains.Cbeff;

/// <summary>
/// Implements CBEFF serialization and deserialization with support for PIV wrappers.
/// </summary>
/// <remarks>
/// This implementation follows functional programming principles with immutable data
/// and Result-based error handling. It transparently handles PIV BC tag wrappers.
/// </remarks>
public sealed class CbeffSerializer : ICbeffSerializer
{
    private const byte PivBiometricTag = 0xBC;

    /// <summary>
    /// Detects if the data has a PIV wrapper (BC tag).
    /// </summary>
    /// <param name="data">The data to check.</param>
    /// <returns>True if the data has a PIV wrapper, false otherwise.</returns>
    public static bool HasPivWrapper(ImmutableArray<byte> data)
    {
        return BerTlv.HasBerTag(data, PivBiometricTag);
    }

    /// <summary>
    /// Detects if the data has a PIV wrapper (BC tag).
    /// </summary>
    /// <param name="data">The data to check.</param>
    /// <returns>True if the data has a PIV wrapper, false otherwise.</returns>
    public static bool HasPivWrapper(byte[] data)
    {
        return data != null && HasPivWrapper(data.ToImmutableArray());
    }

    /// <summary>
    /// Strips PIV wrapper if present, otherwise returns the raw data unchanged.
    /// </summary>
    /// <param name="data">The data to process.</param>
    /// <returns>Raw CBEFF data without PIV wrapper.</returns>
    public static Result<ImmutableArray<byte>> StripPivWrapperIfPresent(ImmutableArray<byte> data)
    {
        return HasPivWrapper(data)
            ? BerTlv.ExtractValueIfTagged(data, PivBiometricTag)
            : Result.Success(data);
    }

    /// <summary>
    /// Strips PIV wrapper if present, otherwise returns the raw data unchanged.
    /// </summary>
    /// <param name="data">The data to process.</param>
    /// <returns>Raw CBEFF data without PIV wrapper.</returns>
    public static Result<byte[]> StripPivWrapperIfPresent(byte[] data)
    {
        return Maybe<byte[]>
            .From(data)
            .ToResult("Data cannot be null")
            .Map(d => d.ToImmutableArray())
            .Bind(StripPivWrapperIfPresent)
            .Map(result => result.ToArray());
    }

    /// <summary>
    /// Adds PIV wrapper to raw CBEFF data.
    /// </summary>
    /// <param name="rawCbeffData">Raw CBEFF data to wrap.</param>
    /// <returns>PIV-wrapped CBEFF data with BC tag.</returns>
    public static Result<ImmutableArray<byte>> AddPivWrapper(ImmutableArray<byte> rawCbeffData)
    {
        return BerTlv.WrapWithTlv(PivBiometricTag, rawCbeffData);
    }

    /// <summary>
    /// Adds PIV wrapper to raw CBEFF data.
    /// </summary>
    /// <param name="rawCbeffData">Raw CBEFF data to wrap.</param>
    /// <returns>PIV-wrapped CBEFF data with BC tag.</returns>
    public static Result<byte[]> AddPivWrapper(byte[] rawCbeffData)
    {
        return Maybe<byte[]>
            .From(rawCbeffData)
            .ToResult("Data cannot be null")
            .Map(d => d.ToImmutableArray())
            .Bind(AddPivWrapper)
            .Map(result => result.ToArray());
    }

    /// <inheritdoc />
    public Result<CbeffPayload> Deserialize(ImmutableArray<byte> data)
    {
        return ExtractCbeffData(data)
            .Bind(ValidateMinimumSize)
            .Bind(ParseHeader)
            .Bind(ValidateDataLength)
            .Bind(ExtractComponents)
            .Bind(CreatePayload);
    }

    /// <inheritdoc />
    public Result<CbeffPayload> Deserialize(byte[] data)
    {
        return Maybe<byte[]>
            .From(data)
            .ToResult("Data cannot be null")
            .Map(d => d.ToImmutableArray())
            .Bind(Deserialize);
    }

    /// <summary>
    /// Deserializes CBEFF payload with automatic PIV wrapper detection.
    /// This method can handle both raw CBEFF data and PIV-wrapped CBEFF data.
    /// </summary>
    /// <param name="data">The CBEFF data to deserialize.</param>
    /// <returns>A Result containing the CbeffPayload or an error.</returns>
    public Result<CbeffPayload> DeserializeAuto(ImmutableArray<byte> data)
    {
        return StripPivWrapperIfPresent(data)
            .Bind(ValidateMinimumSize)
            .Bind(ParseHeader)
            .Bind(ValidateDataLength)
            .Bind(ExtractComponents)
            .Bind(CreatePayload);
    }

    /// <summary>
    /// Deserializes CBEFF payload with automatic PIV wrapper detection.
    /// This method can handle both raw CBEFF data and PIV-wrapped CBEFF data.
    /// </summary>
    /// <param name="data">The CBEFF data to deserialize.</param>
    /// <returns>A Result containing the CbeffPayload or an error.</returns>
    public Result<CbeffPayload> DeserializeAuto(byte[] data)
    {
        return Maybe<byte[]>
            .From(data)
            .ToResult("Data cannot be null")
            .Map(d => d.ToImmutableArray())
            .Bind(DeserializeAuto);
    }

    /// <inheritdoc />
    public Result<ImmutableArray<byte>> Serialize(CbeffPayload payload)
    {
        return Maybe<CbeffPayload>
            .From(payload)
            .ToResult("Payload cannot be null")
            .Bind(ValidatePayload)
            .Map(SerializeToBytes);
    }

    /// <inheritdoc />
    public Result<byte[]> SerializeToArray(CbeffPayload payload)
    {
        return Serialize(payload).Map(data => data.ToArray());
    }

    /// <inheritdoc />
    public Result<ImmutableArray<byte>> SerializeWithWrapper(CbeffPayload payload, byte tag)
    {
        return Serialize(payload).Bind(data => BerTlv.WrapWithTlv(tag, data));
    }

    /// <inheritdoc />
    public Result<byte[]> SerializeWithWrapperToArray(CbeffPayload payload, byte tag)
    {
        return SerializeWithWrapper(payload, tag).Map(data => data.ToArray());
    }

    /// <inheritdoc />
    public Result<bool> ValidateStructure(ImmutableArray<byte> data)
    {
        return ExtractCbeffData(data)
            .Bind(ValidateMinimumSize)
            .Bind(ParseHeader)
            .Bind(ValidateDataLength)
            .Map(_ => true)
            .Match(_ => Result.Success(true), _ => Result.Success(false));
    }

    /// <inheritdoc />
    public Result<bool> ValidateStructure(byte[] data)
    {
        return Maybe<byte[]>
            .From(data)
            .ToResult("Data cannot be null")
            .Map(d => d.ToImmutableArray())
            .Bind(ValidateStructure);
    }

    // Deserialization pipeline steps

    private static Result<ImmutableArray<byte>> ExtractCbeffData(ImmutableArray<byte> data)
    {
        return BerTlv.ExtractValueIfTagged(data, PivBiometricTag);
    }

    private static Result<ImmutableArray<byte>> ValidateMinimumSize(ImmutableArray<byte> data)
    {
        return data.Length >= CbeffHeader.StandardHeaderSize
            ? Result.Success(data)
            : Result.Failure<ImmutableArray<byte>>(
                $"CBEFF data too short: {data.Length} bytes, minimum {CbeffHeader.StandardHeaderSize}"
            );
    }

    private static Result<ParsedHeaderData> ParseHeader(ImmutableArray<byte> data)
    {
        var headerBytes = data.Take(CbeffHeader.StandardHeaderSize).ToArray();

        return CbeffHeaderSerializer
            .FromBytes(headerBytes)
            .Map(header => new ParsedHeaderData(header, data));
    }

    private static Result<ParsedHeaderData> ValidateDataLength(ParsedHeaderData data)
    {
        var expectedLength =
            CbeffHeader.StandardHeaderSize + data.Header.BdbLength + data.Header.SbLength;

        return data.RawData.Length == expectedLength
            ? Result.Success(data)
            : Result.Failure<ParsedHeaderData>(
                $"CBEFF data length mismatch: expected {expectedLength}, got {data.RawData.Length}"
            );
    }

    private static Result<ExtractedComponents> ExtractComponents(ParsedHeaderData data)
    {
        var offset = CbeffHeader.StandardHeaderSize;

        return ExtractBiometricDataBlock(data, offset)
            .Bind(tuple => ExtractSignature(tuple.data, tuple.bdb, tuple.offset))
            .Map(components => new ExtractedComponents(
                data.Header,
                components.bdb,
                components.signature
            ));
    }

    private static Result<(
        ParsedHeaderData data,
        ImmutableArray<byte> bdb,
        int offset
    )> ExtractBiometricDataBlock(ParsedHeaderData data, int offset)
    {
        if (data.Header.BdbLength == 0)
            return Result.Failure<(ParsedHeaderData, ImmutableArray<byte>, int)>(
                "CBEFF header indicates zero-length biometric data block"
            );

        var bdb = data.RawData.Skip(offset).Take((int)data.Header.BdbLength).ToImmutableArray();

        return Result.Success((data, bdb, offset + (int)data.Header.BdbLength));
    }

    private static Result<(
        ImmutableArray<byte> bdb,
        Maybe<CbeffSignature> signature
    )> ExtractSignature(ParsedHeaderData data, ImmutableArray<byte> bdb, int offset)
    {
        if (data.Header.SbLength == 0)
            return Result.Success((bdb, Maybe<CbeffSignature>.None));

        var signatureBytes = data.RawData.Skip(offset).Take(data.Header.SbLength).ToArray();

        return CbeffSignature
            .Create(signatureBytes)
            .Map(sig => (bdb, Maybe<CbeffSignature>.From(sig)));
    }

    private static Result<CbeffPayload> CreatePayload(ExtractedComponents components)
    {
        return CbeffPayload.Create(
            components.Header,
            components.BiometricDataBlock.ToArray(),
            components.Signature
        );
    }

    // Serialization logic

    private static Result<CbeffPayload> ValidatePayload(CbeffPayload payload)
    {
        if (!payload.IsValid())
            return Result.Failure<CbeffPayload>("Invalid CBEFF payload structure");

        return Result.Success(payload);
    }

    private static ImmutableArray<byte> SerializeToBytes(CbeffPayload payload)
    {
        var headerBytes = CbeffHeaderSerializer.ToBytes(payload.Header);
        var bdbBytes = payload.GetBiometricDataBlock();

        var result = ImmutableArray.Create<byte>().AddRange(headerBytes).AddRange(bdbBytes);

        return payload
            .Signature.Map(sig => result.AddRange(sig.SignatureData))
            .GetValueOrDefault(result);
    }

    // Helper types for pipeline data flow

    private sealed record ParsedHeaderData(CbeffHeader Header, ImmutableArray<byte> RawData);

    private sealed record ExtractedComponents(
        CbeffHeader Header,
        ImmutableArray<byte> BiometricDataBlock,
        Maybe<CbeffSignature> Signature
    );
}
