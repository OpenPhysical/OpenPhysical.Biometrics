namespace OpenPhysical.Biometrics.Infrastructure.Cbeff.Formats;

using CSharpFunctionalExtensions;
using Microsoft.Extensions.Logging;
using OpenPhysical.Biometrics.Core.Cbeff.Formats;
using OpenPhysical.Biometrics.Core.Domain;
using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.Reflection;

/// <summary>
/// Thread-safe registry for managing generic biometric format handlers with self-registration support.
/// </summary>
public sealed class GenericBiometricFormatRegistry : IBiometricFormatRegistry
{
    private readonly ConcurrentDictionary<(byte PatronFormat, ushort BdbFormatOwner, ushort BdbFormatType), FormatHandlerEntry> _formatHandlers = new();
    private readonly ConcurrentDictionary<Type, object> _modalityHandlers = new();
    private readonly Maybe<ILogger<GenericBiometricFormatRegistry>> _logger;
    
    /// <summary>
    /// Initializes a new instance of the <see cref="GenericBiometricFormatRegistry"/> class.
    /// </summary>
    /// <param name="logger">Optional logger for diagnostic output.</param>
    public GenericBiometricFormatRegistry(Maybe<ILogger<GenericBiometricFormatRegistry>> logger = default)
    {
        _logger = logger;
    }
    
    /// <inheritdoc />
    public Result RegisterFormatHandler<TDomainRecord>(IBiometricFormatHandler<TDomainRecord> formatHandler)
        where TDomainRecord : class
    {
        var attribute = formatHandler.GetType().GetCustomAttribute<BiometricFormatAttribute>();
        if (attribute == null)
        {
            return Result.Failure($"Format handler {formatHandler.GetType().Name} must be decorated with BiometricFormatAttribute");
        }
        
        var key = (attribute.PatronFormat, attribute.BdbFormatOwner, attribute.BdbFormatType);
        var entry = new FormatHandlerEntry(formatHandler, typeof(TDomainRecord), attribute);
        
        if (!_formatHandlers.TryAdd(key, entry))
        {
            // Allow duplicate registrations of the same format - just use the first one
            return Result.Success();
        }
        
        return Result.Success();
    }
    
    /// <inheritdoc />
    public Result RegisterModalityHandler<TDomainRecord>(IBiometricModalityHandler<TDomainRecord, ExtractedBiometricData> modalityHandler)
        where TDomainRecord : class
    {
        var domainType = typeof(TDomainRecord);
        
        if (!_modalityHandlers.TryAdd(domainType, modalityHandler))
        {
            // Allow duplicate registrations of the same modality - just use the first one
            return Result.Success();
        }
        
        return Result.Success();
    }
    
    /// <inheritdoc />
    public Maybe<object> GetFormatHandler(byte patronFormat, ushort bdbFormatOwner, ushort bdbFormatType)
    {
        var key = (patronFormat, bdbFormatOwner, bdbFormatType);
        _logger.Execute(logger =>
        {
            logger.LogDebug("Looking for format handler (0x{PatronFormat:X2}, 0x{BdbFormatOwner:X4}, 0x{BdbFormatType:X4})", 
                patronFormat, bdbFormatOwner, bdbFormatType);
            logger.LogDebug("Registry has {Count} format handlers registered", _formatHandlers.Count);
            foreach (var kvp in _formatHandlers)
            {
                logger.LogDebug("  - (0x{PatronFormat:X2}, 0x{BdbFormatOwner:X4}, 0x{BdbFormatType:X4}) -> {HandlerType}",
                    kvp.Key.PatronFormat, kvp.Key.BdbFormatOwner, kvp.Key.BdbFormatType, kvp.Value.Handler.GetType().Name);
            }
        });
        
        return _formatHandlers.TryGetValue(key, out var entry) 
            ? Maybe<object>.From(entry.Handler) 
            : Maybe<object>.None;
    }
    
    /// <inheritdoc />
    public Maybe<IBiometricModalityHandler<TDomainRecord, ExtractedBiometricData>> GetModalityHandler<TDomainRecord>()
        where TDomainRecord : class
    {
        var domainType = typeof(TDomainRecord);
        return _modalityHandlers.TryGetValue(domainType, out var handler) 
            ? Maybe<IBiometricModalityHandler<TDomainRecord, ExtractedBiometricData>>.From((IBiometricModalityHandler<TDomainRecord, ExtractedBiometricData>)handler)
            : Maybe<IBiometricModalityHandler<TDomainRecord, ExtractedBiometricData>>.None;
    }
    
    /// <inheritdoc />
    public Result<BiometricExtraction> ProcessBiometricData(
        byte patronFormat,
        ushort bdbFormatOwner,
        ushort bdbFormatType,
        ImmutableArray<byte> data)
    {
        return GetFormatHandler(patronFormat, bdbFormatOwner, bdbFormatType)
            .ToResult($"No format handler registered for patron={patronFormat:X2}, owner={bdbFormatOwner:X4}, type={bdbFormatType:X4}")
            .Bind(handlerObj => ProcessWithHandler(handlerObj, data, patronFormat, bdbFormatOwner, bdbFormatType));
    }
    
    /// <inheritdoc />
    public ImmutableList<BiometricFormatInfo> GetRegisteredFormats()
    {
        return _formatHandlers.Values
            .Select(entry => new BiometricFormatInfo(
                entry.Attribute.FormatName,
                entry.Attribute.PatronFormat,
                entry.Attribute.BdbFormatOwner,
                entry.Attribute.BdbFormatType))
            .ToImmutableList();
    }
    
    private Result<BiometricExtraction> ProcessWithHandler(
        object handlerObj,
        ImmutableArray<byte> data,
        byte patronFormat,
        ushort bdbFormatOwner,
        ushort bdbFormatType)
    {
        // Use reflection to invoke the generic handler
        var handlerType = handlerObj.GetType();
        
        // Find the IBiometricFormatHandler<T> interface to get domain type
        var formatInterface = handlerType.GetInterfaces()
            .FirstOrDefault(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IBiometricFormatHandler<>));
            
        if (formatInterface == null)
        {
            return Result.Failure<BiometricExtraction>("Handler does not implement IBiometricFormatHandler<T>");
        }
        
        var domainType = formatInterface.GetGenericArguments()[0];
        
        // Get the parse method
        var parseMethod = Maybe<MethodInfo>.From(handlerType.GetMethod(nameof(IBiometricFormatHandler<object>.ParseBiometricData)));
        if (parseMethod.HasNoValue)
        {
            return Result.Failure<BiometricExtraction>("Format handler does not implement ParseBiometricData method");
        }
        
        // Parse the data
        var parseResult = Maybe<object>.From(parseMethod.Value.Invoke(handlerObj, new object[] { data }));
        if (parseResult.HasNoValue)
        {
            return Result.Failure<BiometricExtraction>("Parse method returned no result");
        }
        
        // Check if parse was successful using reflection on Result<T>
        var parseResultType = parseResult.Value.GetType();
        var isSuccessProperty = Maybe<PropertyInfo>.From(parseResultType.GetProperty("IsSuccess"));
        var valueProperty = Maybe<PropertyInfo>.From(parseResultType.GetProperty("Value"));
        var errorProperty = Maybe<PropertyInfo>.From(parseResultType.GetProperty("Error"));
        
        if (isSuccessProperty.HasNoValue || valueProperty.HasNoValue || errorProperty.HasNoValue)
        {
            return Result.Failure<BiometricExtraction>("Invalid Result type from parse method");
        }
        
        var isSuccess = (bool)isSuccessProperty.Value.GetValue(parseResult.Value)!;
        if (!isSuccess)
        {
            var errorObj = Maybe<object>.From(errorProperty.Value.GetValue(parseResult.Value));
            var error = errorObj.Map(e => e.ToString()).GetValueOrDefault("Unknown parse error");
            return Result.Failure<BiometricExtraction>($"Failed to parse biometric data: {error}");
        }
        
        var domainRecord = Maybe<object>.From(valueProperty.Value.GetValue(parseResult.Value));
        if (domainRecord.HasNoValue)
        {
            return Result.Failure<BiometricExtraction>("Parsed domain record has no value");
        }
        
        // Try to get modality handler using reflection
        var getModalityHandlerMethod = Maybe<MethodInfo>.From(typeof(GenericBiometricFormatRegistry).GetMethod(nameof(GetModalityHandler)));
        if (getModalityHandlerMethod.HasNoValue)
        {
            return Result.Failure<BiometricExtraction>("Could not find GetModalityHandler method");
        }
        
        var genericMethod = getModalityHandlerMethod.Value.MakeGenericMethod(domainType);
        var modalityHandlerMaybe = Maybe<object>.From(genericMethod.Invoke(this, System.Array.Empty<object>()));
        if (modalityHandlerMaybe.HasNoValue)
        {
            return Result.Failure<BiometricExtraction>("Could not get modality handler");
        }
        
        // Extract modality handler from Maybe<T>
        var modalityHandlerType = modalityHandlerMaybe.Value.GetType();
        var hasValueProperty = Maybe<PropertyInfo>.From(modalityHandlerType.GetProperty("HasValue"));
        var modalityValueProperty = Maybe<PropertyInfo>.From(modalityHandlerType.GetProperty("Value"));
        
        if (hasValueProperty.HasNoValue || modalityValueProperty.HasNoValue)
        {
            return Result.Failure<BiometricExtraction>("Invalid Maybe type for modality handler");
        }
        
        var hasValue = (bool)hasValueProperty.Value.GetValue(modalityHandlerMaybe.Value)!;
        if (!hasValue)
        {
            _logger.Execute(logger => logger.LogError("No modality handler registered for domain type: {DomainType}", domainType.Name));
            return Result.Failure<BiometricExtraction>($"No modality handler registered for {domainType.Name}");
        }
        
        var modalityHandler = Maybe<object>.From(modalityValueProperty.Value.GetValue(modalityHandlerMaybe.Value));
        if (modalityHandler.HasNoValue)
        {
            return Result.Failure<BiometricExtraction>("Modality handler has no value");
        }
        
        // Use the modality handler to extract and analyze
        return ProcessWithModalityHandler(modalityHandler.Value, domainRecord.Value, patronFormat, bdbFormatOwner, bdbFormatType);
    }
    
    private Result<BiometricExtraction> ProcessWithModalityHandler(
        object modalityHandler,
        object domainRecord,
        byte patronFormat,
        ushort bdbFormatOwner,
        ushort bdbFormatType)
    {
        var handlerType = modalityHandler.GetType();
        
        // Get methods
        var extractMethod = Maybe<MethodInfo>.From(handlerType.GetMethod(nameof(IBiometricModalityHandler<object, ExtractedBiometricData>.ExtractBiometricData)));
        var analyzeMethod = Maybe<MethodInfo>.From(handlerType.GetMethod(nameof(IBiometricModalityHandler<object, ExtractedBiometricData>.AnalyzeBiometricData)));
        var validateMethod = Maybe<MethodInfo>.From(handlerType.GetMethod(nameof(IBiometricModalityHandler<object, ExtractedBiometricData>.ValidateModalityRequirements)));
        
        if (extractMethod.HasNoValue || analyzeMethod.HasNoValue || validateMethod.HasNoValue)
        {
            return Result.Failure<BiometricExtraction>("Modality handler does not implement required methods");
        }
        
        // Extract data
        var extractResult = Maybe<object>.From(extractMethod.Value.Invoke(modalityHandler, new[] { domainRecord }));
        var extractedDataMaybe = extractResult.Bind(result => IsResultSuccess(result));
        if (extractedDataMaybe.HasNoValue)
        {
            var error = extractResult.Bind(GetResultError).GetValueOrDefault("Extract method returned no result");
            _logger.Execute(logger => logger.LogError("Failed to extract biometric data: {Error}", error));
            return Result.Failure<BiometricExtraction>($"Failed to extract biometric data: {error}");
        }
        
        // Analyze data
        var analyzeResult = Maybe<object>.From(analyzeMethod.Value.Invoke(modalityHandler, new[] { domainRecord }));
        var analysisResultsMaybe = analyzeResult.Bind(IsResultSuccess);
        var analysisResults = analysisResultsMaybe.HasValue
            ? analysisResultsMaybe.Value?.ToString() ?? "No analysis available"
            : analyzeResult.Bind(GetResultError).GetValueOrDefault("Analysis failed: Unknown error");
        
        // Validate data
        var validateResult = Maybe<object>.From(validateMethod.Value.Invoke(modalityHandler, new[] { domainRecord }));
        var validationSuccessMaybe = validateResult.Bind(IsResultSuccess);
        var validationResults = validationSuccessMaybe.HasValue
            ? ImmutableList<string>.Empty
            : ImmutableList.Create(validateResult.Bind(GetResultError).GetValueOrDefault("Validation failed: Unknown error"));
        
        var isValid = validationResults.IsEmpty;
        
        var sourceFormat = new BiometricFormatInfo(
            GetFormatName(patronFormat, bdbFormatOwner, bdbFormatType),
            patronFormat,
            bdbFormatOwner,
            bdbFormatType);
        
        var extraction = new BiometricExtraction(
            (ExtractedBiometricData)extractedDataMaybe.Value,
            analysisResults,
            validationResults,
            isValid,
            sourceFormat);
            
        return Result.Success(extraction);
    }
    
    private static Maybe<object> IsResultSuccess(object resultObj)
    {
        var resultType = resultObj.GetType();
        var isSuccessProperty = Maybe<PropertyInfo>.From(resultType.GetProperty("IsSuccess"));
        var valueProperty = Maybe<PropertyInfo>.From(resultType.GetProperty("Value"));
        
        if (isSuccessProperty.HasNoValue || valueProperty.HasNoValue) 
            return Maybe<object>.None;
        
        var isSuccess = (bool)isSuccessProperty.Value.GetValue(resultObj)!;
        if (!isSuccess) 
            return Maybe<object>.None;
            
        return Maybe<object>.From(valueProperty.Value.GetValue(resultObj));
    }
    
    private static Maybe<string> GetResultError(object resultObj)
    {
        var resultType = resultObj.GetType();
        
        // First check if it's a failure
        var isSuccessProperty = Maybe<PropertyInfo>.From(resultType.GetProperty("IsSuccess"));
        if (isSuccessProperty.HasValue)
        {
            var isSuccess = (bool)isSuccessProperty.Value.GetValue(resultObj)!;
            if (isSuccess)
            {
                return Maybe<string>.None; // No error on success
            }
        }
        
        var errorProperty = Maybe<PropertyInfo>.From(resultType.GetProperty("Error"));
        
        return errorProperty
            .Bind(prop => Maybe<object>.From(prop.GetValue(resultObj)))
            .Map(error => error?.ToString() ?? "Unknown error");
    }
    
    private string GetFormatName(byte patronFormat, ushort bdbFormatOwner, ushort bdbFormatType)
    {
        var key = (patronFormat, bdbFormatOwner, bdbFormatType);
        return _formatHandlers.TryGetValue(key, out var entry)
            ? entry.Attribute.FormatName
            : $"Unknown Format ({patronFormat:X2}, {bdbFormatOwner:X4}, {bdbFormatType:X4})";
    }
    
    private sealed record FormatHandlerEntry(
        object Handler,
        Type DomainType,
        BiometricFormatAttribute Attribute);
}