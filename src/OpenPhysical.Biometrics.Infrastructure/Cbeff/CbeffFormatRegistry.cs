namespace OpenPhysical.Biometrics.Infrastructure.Cbeff;

using CSharpFunctionalExtensions;
using OpenPhysical.Biometrics.Core.Cbeff.Formats;
using ICbeffFormat = OpenPhysical.Biometrics.Core.Cbeff.Formats.ICbeffFormat;
using OpenPhysical.Domains.Cbeff;
using System.Collections.Concurrent;

/// <summary>
/// Registry implementation for CBEFF format handlers.
/// </summary>
public sealed class CbeffFormatRegistry : Core.Cbeff.Formats.ICbeffFormatRegistry
{
    private readonly ConcurrentDictionary<FormatKey, ICbeffFormat> _formats = new();
    
    /// <inheritdoc />
    public Result RegisterFormat(byte patronFormat, ushort bdbFormatOwner, ushort bdbFormatType, ICbeffFormat format)
    {
        if (format == null)
            return Result.Failure("Format handler cannot be null");
            
        var key = new FormatKey(patronFormat, bdbFormatOwner, bdbFormatType);
        
        if (!_formats.TryAdd(key, format))
        {
            return Result.Failure($"Format already registered for patron {patronFormat:X2}, owner {bdbFormatOwner:X4}, type {bdbFormatType:X4}");
        }
        
        return Result.Success();
    }
    
    /// <inheritdoc />
    public Maybe<ICbeffFormat> GetFormat(CbeffHeader header)
    {
        if (header == null)
            return Maybe<ICbeffFormat>.None;
            
        return GetFormat(header.PatronHeaderVersion, header.BdbFormatOwner, header.BdbFormatType);
    }
    
    /// <inheritdoc />
    public Maybe<ICbeffFormat> GetFormat(byte patronFormat, ushort bdbFormatOwner, ushort bdbFormatType)
    {
        var key = new FormatKey(patronFormat, bdbFormatOwner, bdbFormatType);
        
        return _formats.TryGetValue(key, out var format) 
            ? Maybe<ICbeffFormat>.From(format)
            : Maybe<ICbeffFormat>.None;
    }
    
    /// <inheritdoc />
    public IEnumerable<(byte patronFormat, ushort bdbFormatOwner, ushort bdbFormatType, ICbeffFormat format)> GetAllFormats()
    {
        return _formats.Select(kvp => (
            kvp.Key.PatronFormat,
            kvp.Key.BdbFormatOwner,
            kvp.Key.BdbFormatType,
            kvp.Value
        ));
    }
    
    /// <summary>
    /// Immutable key for format lookup.
    /// </summary>
    private readonly record struct FormatKey(byte PatronFormat, ushort BdbFormatOwner, ushort BdbFormatType);
}