namespace OpenPhysical.Biometrics.Core.Cbeff.Formats;

/// <summary>
/// Attribute used to mark classes as biometric format handlers for automatic registration.
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
public sealed class BiometricFormatAttribute : Attribute
{
    /// <summary>
    /// Gets the patron format identifier this handler supports.
    /// </summary>
    public byte PatronFormat { get; }

    /// <summary>
    /// Gets the BDB format owner this handler supports.
    /// </summary>
    public ushort BdbFormatOwner { get; }

    /// <summary>
    /// Gets the BDB format type this handler supports.
    /// </summary>
    public ushort BdbFormatType { get; }

    /// <summary>
    /// Gets the format name for display purposes.
    /// </summary>
    public string FormatName { get; }

    /// <summary>
    /// Initializes a new instance of the BiometricFormatAttribute class.
    /// </summary>
    /// <param name="patronFormat">The patron format identifier.</param>
    /// <param name="bdbFormatOwner">The BDB format owner.</param>
    /// <param name="bdbFormatType">The BDB format type.</param>
    /// <param name="formatName">The format name for display.</param>
    public BiometricFormatAttribute(
        byte patronFormat,
        ushort bdbFormatOwner,
        ushort bdbFormatType,
        string formatName
    )
    {
        PatronFormat = patronFormat;
        BdbFormatOwner = bdbFormatOwner;
        BdbFormatType = bdbFormatType;
        FormatName = formatName;
    }
}
