namespace OpenPhysical.Biometrics.Infrastructure.Certificates;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using CSharpFunctionalExtensions;
using Microsoft.Extensions.Logging;
using OpenPhysical.Biometrics.Core.Certificates;
using Org.BouncyCastle.OpenSsl;
using Org.BouncyCastle.Utilities.Collections;
using Org.BouncyCastle.X509;

/// <summary>
/// Provides default trusted certificates from embedded resources.
/// </summary>
public sealed class TrustedCertificateProvider : ITrustedCertificateProvider
{
    private readonly ILogger<TrustedCertificateProvider> _logger;
    private readonly Assembly _assembly;

    /// <summary>
    /// Initializes a new instance of the <see cref="TrustedCertificateProvider"/> class.
    /// </summary>
    /// <param name="logger">The logger instance.</param>
    public TrustedCertificateProvider(ILogger<TrustedCertificateProvider> logger)
    {
        _logger = logger;
        _assembly = Assembly.GetExecutingAssembly();
    }

    /// <inheritdoc />
    public Result<IStore<X509Certificate>> GetDefaultCertificates()
    {
        try
        {
            var certificates = new List<X509Certificate>();

            // List of embedded certificate resource names
            var certificateResources = new[]
            {
                // PIV certificates
                "icam-test-root-ca.crt",
                "icam-test-signing-ca.crt",
                "icam-test-content-signer.crt",
                "icam-piv-root-ca-gen1-3.crt",
                "icam-piv-signing-ca-gen3.crt",
                "icam-piv-content-signer-gen3.crt",
                "icam-piv-content-signer-gen1-2.crt",
                "icam-piv-content-signer.crt",
                "icam-piv-signing-ca.crt",
                // PIV-I certificates
                "icam-piv-i-root-ca-gen3.crt",
                "icam-piv-i-signing-ca-gen3.crt",
                "icam-piv-i-content-signer-gen1-2.crt",
                "icam-piv-i-content-signer-gen3.crt",
            };

            foreach (var resourceName in certificateResources)
            {
                var certResult = LoadEmbeddedCertificate(resourceName);
                if (certResult.IsSuccess)
                {
                    certificates.Add(certResult.Value);
                    _logger.LogInformation(
                        new EventId(665816, "TrustedCertificateProvider"),
                        "TrustedCertificateProvider stage completed."
                    );
                }
                else
                {
                    _logger.LogError(
                        new EventId(727694, "TrustedCertificateProvider"),
                        "TrustedCertificateProvider failed."
                    );
                    // No silent failures - this is a hard error
                    return Result.Failure<IStore<X509Certificate>>(
                        $"Failed to load required certificate {resourceName}: {certResult.Error}"
                    );
                }
            }

            if (certificates.Count == 0)
            {
                return Result.Failure<IStore<X509Certificate>>(
                    "No default certificates could be loaded"
                );
            }

            _logger.LogInformation("Loaded {Count} default certificates", certificates.Count);
            return Result.Success(CollectionUtilities.CreateStore(certificates));
        }
        catch (Exception ex)
        {
            _logger.LogError(
                new EventId(731170, "TrustedCertificateProvider"),
                "TrustedCertificateProvider failed."
            );
            return Result.Failure<IStore<X509Certificate>>(
                $"Failed to get default certificates: {ex.Message}"
            );
        }
    }

    /// <inheritdoc />
    public Result<IStore<X509Certificate>> GetCertificatesByName(string namePattern)
    {
        return GetDefaultCertificates()
            .Map(certificateStore =>
            {
                var filtered = new List<X509Certificate>();
                foreach (var cert in certificateStore.EnumerateMatches(null))
                {
                    if (
                        cert.SubjectDN.ToString()
                            .Contains(namePattern, StringComparison.OrdinalIgnoreCase)
                        || cert.IssuerDN.ToString()
                            .Contains(namePattern, StringComparison.OrdinalIgnoreCase)
                    )
                    {
                        filtered.Add(cert);
                    }
                }
                return CollectionUtilities.CreateStore(filtered);
            });
    }

    private Result<X509Certificate> LoadEmbeddedCertificate(string resourceName)
    {
        try
        {
            // Find the resource in the assembly
            var resourceFullName = _assembly
                .GetManifestResourceNames()
                .FirstOrDefault(name =>
                    name.EndsWith(resourceName, StringComparison.OrdinalIgnoreCase)
                );

            if (resourceFullName == null)
            {
                // Try to load from file system as fallback (for development)
                var filePath = Path.Combine(
                    Path.GetDirectoryName(_assembly.Location) ?? string.Empty,
                    "Certificates",
                    "Resources",
                    resourceName
                );

                if (File.Exists(filePath))
                {
                    var certBytes = File.ReadAllBytes(filePath);
                    var cert = new X509Certificate(certBytes);
                    return Result.Success(cert);
                }

                return Result.Failure<X509Certificate>(
                    $"Embedded resource {resourceName} not found"
                );
            }

            using (var stream = _assembly.GetManifestResourceStream(resourceFullName))
            {
                if (stream == null)
                {
                    return Result.Failure<X509Certificate>(
                        $"Failed to get stream for resource {resourceName}"
                    );
                }

                using (var reader = new StreamReader(stream, Encoding.UTF8))
                {
                    var pemContent = reader.ReadToEnd();
                    using (var stringReader = new StringReader(pemContent))
                    {
                        var pemReader = new PemReader(stringReader);
                        var pemObject = pemReader.ReadPemObject();

                        if (pemObject == null)
                        {
                            return Result.Failure<X509Certificate>(
                                $"No PEM object found in {resourceName}"
                            );
                        }

                        var certificate = new X509Certificate(pemObject.Content);
                        return Result.Success(certificate);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(
                new EventId(743425, "TrustedCertificateProvider"),
                "TrustedCertificateProvider failed."
            );
            return Result.Failure<X509Certificate>(
                $"Failed to load certificate {resourceName}: {ex.Message}"
            );
        }
    }
}
