namespace OpenPhysical.Biometrics.Infrastructure.Cbeff;

using System;
using System.Threading.Tasks;
using CSharpFunctionalExtensions;
using OpenPhysical.Biometrics.Core.Cbeff;
using OpenPhysical.Domains.Biometrics;
using OpenPhysical.Domains.Cbeff;
using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.X509;

/// <summary>
/// Service for building CBEFF containers from biometric data.
/// </summary>
public sealed class CbeffBuilderService : ICbeffBuilder
{
    private readonly ICbeffEncoder _encoder;
    private readonly ICbeffSigner? _signer;

    /// <summary>
    /// Initializes a new instance of the <see cref="CbeffBuilderService"/> class.
    /// </summary>
    /// <param name="encoder">The CBEFF encoder service.</param>
    /// <param name="signer">The application signing service, supplied when signed containers are requested.</param>
    public CbeffBuilderService(ICbeffEncoder encoder, ICbeffSigner? signer = null)
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
        CbeffMetadata metadata
    )
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
        Maybe<Guid> includeCardUuid = default
    )
    {
        if (_signer is null)
            return Result.Failure<CbeffPayload>(
                "The application must supply an ICbeffSigner to build a signed container."
            );

        // First build the unsigned CBEFF
        var buildResult = await BuildAsync(biometricData, modality, format, metadata);
        return await buildResult.Bind(payload =>
            _signer.SignAsync(payload, signingCertificate, privateKey, includeCardUuid)
        );
    }
}
