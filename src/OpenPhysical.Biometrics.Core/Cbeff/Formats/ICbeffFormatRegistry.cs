namespace OpenPhysical.Biometrics.Core.Cbeff.Formats;

using CSharpFunctionalExtensions;
using OpenPhysical.Domains.Cbeff;

/// <summary>
/// Registry for managing CBEFF format handlers.
/// </summary>
public interface ICbeffFormatRegistry
{
    /// <summary>
    /// Registers a format handler for specific patron format and BDB format type.
    /// </summary>
    /// <param name="patronFormat">The patron format identifier.</param>
    /// <param name="bdbFormatOwner">The BDB format owner identifier.</param>
    /// <param name="bdbFormatType">The BDB format type identifier.</param>
    /// <param name="format">The format handler to register.</param>
    /// <returns>Success if registered, failure if format already exists.</returns>
    Result RegisterFormat(byte patronFormat, ushort bdbFormatOwner, ushort bdbFormatType, ICbeffFormat format);
    
    /// <summary>
    /// Gets a format handler for the given CBEFF header.
    /// </summary>
    /// <param name="header">The CBEFF header containing format identifiers.</param>
    /// <returns>The format handler if found, none otherwise.</returns>
    Maybe<ICbeffFormat> GetFormat(CbeffHeader header);
    
    /// <summary>
    /// Gets a format handler for the given format identifiers.
    /// </summary>
    /// <param name="patronFormat">The patron format identifier.</param>
    /// <param name="bdbFormatOwner">The BDB format owner identifier.</param>
    /// <param name="bdbFormatType">The BDB format type identifier.</param>
    /// <returns>The format handler if found, none otherwise.</returns>
    Maybe<ICbeffFormat> GetFormat(byte patronFormat, ushort bdbFormatOwner, ushort bdbFormatType);
    
    /// <summary>
    /// Gets all registered formats.
    /// </summary>
    /// <returns>All registered format handlers with their identifiers.</returns>
    IEnumerable<(byte patronFormat, ushort bdbFormatOwner, ushort bdbFormatType, ICbeffFormat format)> GetAllFormats();
}