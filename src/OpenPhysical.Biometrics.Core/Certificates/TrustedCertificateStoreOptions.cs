namespace OpenPhysical.Biometrics.Core.Certificates;

using System;
using System.IO;

/// <summary>
/// Configuration options for the trusted certificate store.
/// </summary>
public class TrustedCertificateStoreOptions
{
    /// <summary>
    /// Gets or sets the path where trusted certificates are stored.
    /// </summary>
    public string StorePath { get; set; } = GetDefaultStorePath();

    /// <summary>
    /// Gets or sets a value indicating whether the store should be initialized with default certificates if empty.
    /// </summary>
    public bool AutoInitializeWithDefaults { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether the store should create the directory if it doesn't exist.
    /// </summary>
    public bool CreateDirectoryIfNotExists { get; set; } = true;

    /// <summary>
    /// Gets the default store path for the current platform.
    /// </summary>
    private static string GetDefaultStorePath()
    {
        return Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "OpenPhysical",
            "Biometrics",
            "TrustedCertificates"
        );
    }
}
