namespace OpenPhysical.Biometrics.Infrastructure.Cbeff;

using CSharpFunctionalExtensions;
using OpenPhysical.Biometrics.Core.Cbeff;
using OpenPhysical.Biometrics.Core.Cbeff.Formats;
using OpenPhysical.Biometrics.Core.Domain;
using OpenPhysical.Biometrics.Infrastructure.Display;
using OpenPhysical.Domains.Cbeff;
using System.Collections.Immutable;
using System.Reflection;

/// <summary>
/// Extended CBEFF serializer that integrates with the format registry.
/// </summary>
public sealed class CbeffSerializerExtended : ICbeffSerializerExtended
{
    private readonly ICbeffSerializer _baseSerializer;
    private readonly IBiometricFormatRegistry _formatRegistry;
    
    /// <summary>
    /// Initializes a new instance of the CbeffSerializerExtended class.
    /// </summary>
    /// <param name="baseSerializer">The base CBEFF serializer.</param>
    /// <param name="formatRegistry">The biometric format registry for data parsing.</param>
    public CbeffSerializerExtended(ICbeffSerializer baseSerializer, IBiometricFormatRegistry formatRegistry)
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
        var formatHandler = _formatRegistry.GetFormatHandler(
            payload.Header.PatronHeaderVersion,
            payload.Header.BdbFormatOwner,
            payload.Header.BdbFormatType);
        
        var parsedRecord = formatHandler.HasValue 
            ? ParseWithHandler(formatHandler.Value, payload.BiometricDataBlock.ToImmutableArray())
            : Maybe<object>.None;
        
        var displayRepresentation = parsedRecord
            .Bind(record => 
            {
                var transformResult = BiometricDisplayTransformations.Transform(record);
                return transformResult.IsSuccess 
                    ? Maybe<BiometricDisplay>.From(transformResult.Value)
                    : Maybe<BiometricDisplay>.None;
            });
        
        return CbeffPayloadExtended.Create(payload, parsedRecord, displayRepresentation);
    }
    
    private Maybe<object> ParseWithHandler(object handler, ImmutableArray<byte> data)
    {
        // Use reflection to invoke the generic ParseBiometricData method
        var handlerType = handler.GetType();
        var parseMethod = handlerType.GetMethod("ParseBiometricData");
        
        if (parseMethod == null)
            return Maybe<object>.None;
            
        try
        {
            var result = parseMethod.Invoke(handler, new object[] { data });
            
            // The result is a Result<TDomainRecord> - we need to extract the value
            if (result != null)
            {
                var resultType = result.GetType();
                var isSuccessProperty = resultType.GetProperty("IsSuccess");
                var valueProperty = resultType.GetProperty("Value");
                
                if (isSuccessProperty != null && valueProperty != null)
                {
                    var isSuccess = (bool)isSuccessProperty.GetValue(result)!;
                    if (isSuccess)
                    {
                        var value = valueProperty.GetValue(result);
                        if (value != null)
                        {
                            return Maybe<object>.From(value);
                        }
                    }
                }
            }
        }
        catch
        {
            // Parsing failed, return None
        }
        
        return Maybe<object>.None;
    }
}