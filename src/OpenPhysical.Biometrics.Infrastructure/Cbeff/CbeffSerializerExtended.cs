namespace OpenPhysical.Biometrics.Infrastructure.Cbeff;

using CSharpFunctionalExtensions;
using OpenPhysical.Biometrics.Core.Cbeff;
using OpenPhysical.Biometrics.Core.Cbeff.Formats;
using OpenPhysical.Domains.Cbeff;
using System.Collections.Immutable;

/// <summary>
/// Extended CBEFF serializer that integrates with the format registry.
/// </summary>
public sealed class CbeffSerializerExtended : ICbeffSerializerExtended
{
    private readonly ICbeffSerializer _baseSerializer;
    private readonly Core.Cbeff.Formats.ICbeffFormatRegistry _formatRegistry;
    
    /// <summary>
    /// Initializes a new instance of the CbeffSerializerExtended class.
    /// </summary>
    /// <param name="baseSerializer">The base CBEFF serializer.</param>
    /// <param name="formatRegistry">The format registry for biometric data parsing.</param>
    public CbeffSerializerExtended(ICbeffSerializer baseSerializer, Core.Cbeff.Formats.ICbeffFormatRegistry formatRegistry)
    {
        _baseSerializer = baseSerializer ?? throw new ArgumentNullException(nameof(baseSerializer));
        _formatRegistry = formatRegistry ?? throw new ArgumentNullException(nameof(formatRegistry));
    }
    
    /// <inheritdoc />
    public Result<CbeffPayloadExtended> DeserializeExtended(ImmutableArray<byte> data)
    {
        return _baseSerializer.Deserialize(data)
            .Map(payload => CreateExtendedPayload(payload));
    }
    
    /// <inheritdoc />
    public Result<CbeffPayloadExtended> DeserializeExtended(byte[] data)
    {
        return Maybe<byte[]>.From(data)
            .ToResult("Data cannot be null")
            .Map(d => d.ToImmutableArray())
            .Bind(DeserializeExtended);
    }
    
    /// <inheritdoc />
    public Result<CbeffPayloadExtended> DeserializeAutoExtended(ImmutableArray<byte> data)
    {
        return _baseSerializer.DeserializeAuto(data)
            .Map(payload => CreateExtendedPayload(payload));
    }
    
    /// <inheritdoc />
    public Result<CbeffPayloadExtended> DeserializeAutoExtended(byte[] data)
    {
        return Maybe<byte[]>.From(data)
            .ToResult("Data cannot be null")
            .Map(d => d.ToImmutableArray())
            .Bind(DeserializeAutoExtended);
    }
    
    // Implement ICbeffSerializer methods by delegating to base serializer
    
    /// <inheritdoc />
    public Result<CbeffPayload> Deserialize(ImmutableArray<byte> data)
    {
        return _baseSerializer.Deserialize(data);
    }
    
    /// <inheritdoc />
    public Result<CbeffPayload> Deserialize(byte[] data)
    {
        return _baseSerializer.Deserialize(data);
    }
    
    /// <inheritdoc />
    public Result<CbeffPayload> DeserializeAuto(ImmutableArray<byte> data)
    {
        return _baseSerializer.DeserializeAuto(data);
    }
    
    /// <inheritdoc />
    public Result<CbeffPayload> DeserializeAuto(byte[] data)
    {
        return _baseSerializer.DeserializeAuto(data);
    }
    
    /// <inheritdoc />
    public Result<ImmutableArray<byte>> Serialize(CbeffPayload payload)
    {
        return _baseSerializer.Serialize(payload);
    }
    
    /// <inheritdoc />
    public Result<byte[]> SerializeToArray(CbeffPayload payload)
    {
        return _baseSerializer.SerializeToArray(payload);
    }
    
    /// <inheritdoc />
    public Result<ImmutableArray<byte>> SerializeWithWrapper(CbeffPayload payload, byte tag = 0xBC)
    {
        return _baseSerializer.SerializeWithWrapper(payload, tag);
    }
    
    /// <inheritdoc />
    public Result<byte[]> SerializeWithWrapperToArray(CbeffPayload payload, byte tag = 0xBC)
    {
        return _baseSerializer.SerializeWithWrapperToArray(payload, tag);
    }
    
    /// <inheritdoc />
    public Result<bool> ValidateStructure(ImmutableArray<byte> data)
    {
        return _baseSerializer.ValidateStructure(data);
    }
    
    /// <inheritdoc />
    public Result<bool> ValidateStructure(byte[] data)
    {
        return _baseSerializer.ValidateStructure(data);
    }
    
    private CbeffPayloadExtended CreateExtendedPayload(CbeffPayload payload)
    {
        var formatHandler = _formatRegistry.GetFormat(payload.Header);
        
        var parsedRecord = formatHandler
            .Bind(format => format.ParseBiometricData(payload.BiometricDataBlock.ToImmutableArray())
                .Map(record => Maybe<BiometricRecord>.From(record))
                .GetValueOrDefault(Maybe<BiometricRecord>.None));
        
        return CbeffPayloadExtended.Create(payload, parsedRecord, formatHandler);
    }
}