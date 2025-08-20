namespace OpenPhysical.Biometrics.Infrastructure.Cbeff;

using CSharpFunctionalExtensions;
using OpenPhysical.Biometrics.Core.Cbeff;
using OpenPhysical.Domains.Biometrics;
using OpenPhysical.Domains.Cbeff;
using System;
using System.Threading.Tasks;
using Org.BouncyCastle.X509;
using Org.BouncyCastle.Crypto;

/// <summary>
/// Service for building CBEFF containers from biometric data.
/// </summary>
public sealed class CbeffBuilderService : ICbeffBuilder
{
    private readonly ICbeffEncoder _encoder;
    private readonly ICbeffSigner _signer;
    
    /// <summary>
    /// Initializes a new instance of the <see cref="CbeffBuilderService"/> class.
    /// </summary>
    /// <param name="encoder">The CBEFF encoder service.</param>
    /// <param name="signer">The CBEFF signer service.</param>
    public CbeffBuilderService(ICbeffEncoder encoder, ICbeffSigner signer)
    {
        _encoder = encoder;
        _signer = signer;
    }
    
    /// <summary>
    /// Creates a CBEFF container from formatted biometric data.
    /// </summary>
    public async Task<Result<CbeffPayload>> BuildAsync(
        byte[] biometricData,
        BiometricModality modality,
        BiometricFormat format,
        CbeffMetadata metadata)
    {
        return await _encoder.EncodeAsync(biometricData, modality, format, metadata);
    }
    
    /// <summary>
    /// Creates a signed CBEFF container from formatted biometric data.
    /// </summary>
    public async Task<Result<CbeffPayload>> BuildSignedAsync(
        byte[] biometricData,
        BiometricModality modality,
        BiometricFormat format,
        CbeffMetadata metadata,
        X509Certificate signingCertificate,
        AsymmetricKeyParameter privateKey,
        Maybe<Guid> includeCardUuid = default)
    {
        // First build the unsigned CBEFF
        var buildResult = await BuildAsync(biometricData, modality, format, metadata);
        return await buildResult.Bind(payload => 
            _signer.SignAsync(payload, signingCertificate, privateKey, includeCardUuid));
    }
}